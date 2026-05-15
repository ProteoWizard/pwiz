/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Statistics from a target-decoy pairing pass.
    /// Maps to Rust <c>osprey_core::types::PairingStats</c>.
    /// </summary>
    public class PairingStats
    {
        /// <summary>Number of target entries seen.</summary>
        public int NTargets { get; set; }

        /// <summary>Number of decoy entries seen.</summary>
        public int NDecoys { get; set; }

        /// <summary>
        /// Number of decoys successfully paired with a target
        /// (decoy.Id rewritten to share base_id with the target).
        /// </summary>
        public int NPaired { get; set; }

        /// <summary>Number of decoys for which no target match was found.</summary>
        public int NUnpairedDecoys { get; set; }

        /// <summary>Number of targets that no decoy claimed as its pair.</summary>
        public int NUnpairedTargets { get; set; }

        /// <summary>
        /// Fraction of decoys successfully paired with a target. Returns
        /// 1.0 when there are no decoys (no work to do, trivially OK) so
        /// callers can compare against a minimum-fraction threshold
        /// without special-casing empty libraries.
        /// </summary>
        public double PairedFraction
        {
            get { return NDecoys == 0 ? 1.0 : (double)NPaired / NDecoys; }
        }
    }

    /// <summary>
    /// Pair library-supplied decoys with their targets via amino-acid
    /// composition. Run after <c>LibraryDecoyMarker.ApplyLibraryDecoyMarking</c>
    /// so decoys already carry <c>IsDecoy = true</c>.
    /// Maps to Rust <c>osprey_core::types::pair_library_decoys_by_composition</c>.
    /// </summary>
    public static class LibraryDecoyPairing
    {
        /// <summary>
        /// Pair each decoy with the target entry sharing the same protein
        /// accession (after stripping any of <paramref name="decoyPrefixes"/>),
        /// the same amino-acid composition (a permutation invariant), and
        /// the same precursor charge. When found, rewrites the decoy's
        /// <see cref="LibraryEntry.Id"/> so its
        /// <c>base_id = Id &amp; 0x7FFFFFFF</c> matches the target's id.
        ///
        /// Determinism: when a (accession, charge, composition) bucket
        /// holds multiple peptides, entries are sorted by
        /// <c>(Sequence, Id)</c> on both sides and zipped 1:1.
        ///
        /// Shared peptides: when a decoy lists multiple accessions, they
        /// are sorted lexicographically and the first un-claimed target
        /// match wins.
        /// </summary>
        public static PairingStats PairLibraryDecoysByComposition(
            IList<LibraryEntry> library,
            IList<string> decoyPrefixes)
        {
            var stats = new PairingStats();
            if (library == null || library.Count == 0)
                return stats;

            // Phase 1: build target index keyed by (accession, charge,
            // sorted_aa) -> list of target library indices.
            var targetIndex =
                new Dictionary<TargetKey, List<int>>(TargetKeyComparer.Instance);
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                if (entry == null)
                    continue;
                if (entry.IsDecoy)
                {
                    stats.NDecoys++;
                    continue;
                }
                stats.NTargets++;
                if (entry.ProteinIds == null || entry.ProteinIds.Count == 0)
                    continue;
                string aa = SortedAa(entry.Sequence);
                for (int p = 0; p < entry.ProteinIds.Count; p++)
                {
                    var key = new TargetKey(entry.ProteinIds[p], entry.Charge, aa);
                    if (!targetIndex.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        targetIndex[key] = list;
                    }
                    list.Add(idx);
                }
            }

            // Sort each bucket deterministically by (sequence, id).
            foreach (var list in targetIndex.Values)
            {
                if (list.Count > 1)
                {
                    var lib = library;
                    list.Sort((a, b) =>
                    {
                        int c = string.CompareOrdinal(lib[a].Sequence, lib[b].Sequence);
                        if (c != 0) return c;
                        return lib[a].Id.CompareTo(lib[b].Id);
                    });
                }
            }

            // Phase 2: scan decoys in (sequence, id) order, claim a target
            // slot from the index. claimed[targetIdx] = true once paired.
            var decoyOrder = new List<int>();
            for (int i = 0; i < library.Count; i++)
            {
                if (library[i] != null && library[i].IsDecoy)
                    decoyOrder.Add(i);
            }
            decoyOrder.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(library[a].Sequence, library[b].Sequence);
                if (c != 0) return c;
                return library[a].Id.CompareTo(library[b].Id);
            });

            var claimed = new HashSet<int>();
            // Collected pairings; applied after the read-only index scan.
            var pairings = new List<KeyValuePair<int, int>>();

            foreach (var decoyIdx in decoyOrder)
            {
                var decoy = library[decoyIdx];
                if (decoy.ProteinIds == null || decoy.ProteinIds.Count == 0)
                    continue;
                string aa = SortedAa(decoy.Sequence);
                byte charge = decoy.Charge;
                var accs = new List<string>(decoy.ProteinIds);
                accs.Sort(StringComparer.Ordinal);

                int matched = -1;
                for (int a = 0; a < accs.Count && matched < 0; a++)
                {
                    string targetAcc = StripDecoyPrefix(accs[a], decoyPrefixes);
                    var key = new TargetKey(targetAcc, charge, aa);
                    if (!targetIndex.TryGetValue(key, out var candidates))
                        continue;
                    for (int t = 0; t < candidates.Count; t++)
                    {
                        if (!claimed.Contains(candidates[t]))
                        {
                            matched = candidates[t];
                            break;
                        }
                    }
                }

                if (matched >= 0)
                {
                    claimed.Add(matched);
                    pairings.Add(new KeyValuePair<int, int>(decoyIdx, matched));
                }
            }

            // Phase 3: apply pairings (mutations).
            for (int i = 0; i < pairings.Count; i++)
            {
                int decoyIdx = pairings[i].Key;
                int targetIdx = pairings[i].Value;
                uint targetId = library[targetIdx].Id;
                library[decoyIdx].Id = targetId | LibraryEntry.DECOY_ID_BIT;
            }

            stats.NPaired = pairings.Count;
            stats.NUnpairedDecoys = stats.NDecoys - stats.NPaired;
            // saturating_sub: never go negative (defense-in-depth; not
            // load-bearing because claimed.Count <= n_targets by
            // construction).
            stats.NUnpairedTargets = Math.Max(0, stats.NTargets - claimed.Count);
            return stats;
        }

        /// <summary>
        /// Sorted canonical form of a peptide's amino-acid composition.
        /// `PEPK` and `KPEP` both map to `EKPP`. Maps to Rust
        /// <c>sorted_aa</c>.
        /// </summary>
        private static string SortedAa(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return string.Empty;
            char[] chars = sequence.ToCharArray();
            Array.Sort(chars); // Array.Sort OK: sorting a single char[] to canonicalize AA composition; ties are byte-identical so stability is irrelevant
            return new string(chars);
        }

        /// <summary>
        /// Strip the first matching prefix (case-insensitive) from
        /// <paramref name="acc"/>; returns the trimmed accession or the
        /// input unchanged if no prefix matched. Maps to Rust
        /// <c>strip_decoy_prefix</c>.
        /// </summary>
        private static string StripDecoyPrefix(string acc, IList<string> prefixes)
        {
            if (string.IsNullOrEmpty(acc) || prefixes == null)
                return acc;
            for (int i = 0; i < prefixes.Count; i++)
            {
                string p = prefixes[i];
                if (string.IsNullOrEmpty(p))
                    continue;
                if (acc.Length >= p.Length &&
                    acc.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    return acc.Substring(p.Length);
                }
            }
            return acc;
        }

        // Composite key for the target index. Mirrors Rust's
        // (String, u8, String) tuple key.
        private struct TargetKey : IEquatable<TargetKey>
        {
            public readonly string Accession;
            public readonly byte Charge;
            public readonly string Composition;

            public TargetKey(string accession, byte charge, string composition)
            {
                Accession = accession ?? string.Empty;
                Charge = charge;
                Composition = composition ?? string.Empty;
            }

            public bool Equals(TargetKey other)
            {
                return Charge == other.Charge &&
                       string.Equals(Accession, other.Accession, StringComparison.Ordinal) &&
                       string.Equals(Composition, other.Composition, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TargetKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Accession.GetHashCode();
                    h = (h * 397) ^ Charge.GetHashCode();
                    h = (h * 397) ^ Composition.GetHashCode();
                    return h;
                }
            }
        }

        private sealed class TargetKeyComparer : IEqualityComparer<TargetKey>
        {
            public static readonly TargetKeyComparer Instance = new TargetKeyComparer();
            public bool Equals(TargetKey x, TargetKey y) { return x.Equals(y); }
            public int GetHashCode(TargetKey obj) { return obj.GetHashCode(); }
        }
    }
}
