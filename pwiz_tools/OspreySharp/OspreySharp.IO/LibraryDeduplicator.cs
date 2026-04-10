using System;
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Deduplicates library entries by (modified_sequence, charge).
    /// Ported from osprey-io/src/library/mod.rs deduplicate_library().
    /// </summary>
    public static class LibraryDeduplicator
    {
        /// <summary>
        /// Deduplicate library entries by (modified_sequence, charge).
        /// When multiple entries share the same peptide+charge, keeps the one with the
        /// most fragments (ties broken by highest total fragment intensity). Averages
        /// retention_time across all duplicates and merges protein_ids and gene_names.
        /// Re-assigns sequential IDs.
        /// </summary>
        public static List<LibraryEntry> DeduplicateLibrary(List<LibraryEntry> entries)
        {
            int originalCount = entries.Count;

            // Group entries by (modified_sequence, charge)
            var groups = new Dictionary<string, List<LibraryEntry>>();

            foreach (var entry in entries)
            {
                string key = entry.ModifiedSequence + "\t" + entry.Charge;
                List<LibraryEntry> group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<LibraryEntry>();
                    groups[key] = group;
                }
                group.Add(entry);
            }

            var deduped = new List<LibraryEntry>(groups.Count);

            foreach (var group in groups.Values)
            {
                if (group.Count == 1)
                {
                    deduped.Add(group[0]);
                    continue;
                }

                // Average retention time across all duplicates
                double sumRt = 0;
                foreach (var e in group)
                    sumRt += e.RetentionTime;
                double avgRt = sumRt / group.Count;

                // Merge protein_ids and gene_names from all entries
                var allProteins = new SortedSet<string>();
                var allGenes = new SortedSet<string>();
                foreach (var e in group)
                {
                    foreach (string p in e.ProteinIds)
                        allProteins.Add(p);
                    foreach (string g in e.GeneNames)
                        allGenes.Add(g);
                }

                // Pick the best entry: most fragments, then highest total intensity
                group.Sort((a, b) =>
                {
                    int fragCmp = b.Fragments.Count.CompareTo(a.Fragments.Count);
                    if (fragCmp != 0)
                        return fragCmp;

                    double sumA = 0;
                    foreach (var f in a.Fragments)
                        sumA += f.RelativeIntensity;
                    double sumB = 0;
                    foreach (var f in b.Fragments)
                        sumB += f.RelativeIntensity;
                    return sumB.CompareTo(sumA);
                });

                var best = group[0];
                best.RetentionTime = avgRt;
                best.ProteinIds = new List<string>(allProteins);
                best.GeneNames = new List<string>(allGenes);
                deduped.Add(best);
            }

            // Sort deterministically before assigning IDs
            deduped.Sort((a, b) =>
            {
                int cmp = string.Compare(a.ModifiedSequence, b.ModifiedSequence, StringComparison.Ordinal);
                if (cmp != 0)
                    return cmp;
                return a.Charge.CompareTo(b.Charge);
            });

            // Re-assign sequential IDs
            for (int i = 0; i < deduped.Count; i++)
                deduped[i].Id = (uint)i;

            return deduped;
        }
    }
}
