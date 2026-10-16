/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.db.DecoyPairPlanner
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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Plans the <c>DecoyPairs</c> table Carafe adds to a Skyline .blib when given a pairing
    /// manifest (<c>-pairing_manifest</c>): the manifest's target/decoy groups joined to the
    /// RefSpectra rows written, by I/L-normalized stripped sequence. Within a group, a target
    /// precursor pairs with a decoy precursor of the same charge and modification set, zipped
    /// in RefSpectra id order; the entrapment pair (<c>p_target</c> / <c>p_decoy</c>) is planned
    /// the same way with IsEntrapment set. Groups are visited in pair-index order and PairIDs
    /// count from 1. Method is <c>reverse</c> when the decoy is the C-terminus-preserving
    /// reverse of the target, else <c>cycle</c>.
    /// <para>
    /// One departure: Carafe's I/L normalization merges two library peptides that differ only
    /// by I and L, so both of their groups pair the same precursors and Carafe's insert fails
    /// on the DecoyPairs primary key, keeping only the rows before the first repeat. Here a
    /// pair whose target or decoy precursor is already paired is skipped instead, so every
    /// RefSpectra row appears at most once and every PairID has both of its rows.
    /// </para>
    /// </summary>
    public static class DecoyPairPlanner
    {
        private const string TARGET = PairingManifest.KIND_TARGET;
        private const string DECOY = PairingManifest.KIND_DECOY;
        private const string ENTRAPMENT_TARGET = PairingManifest.KIND_P_TARGET;
        private const string ENTRAPMENT_DECOY = PairingManifest.KIND_P_DECOY;

        /// <summary>
        /// Carafe's key for a precursor's modification set: the alphabase names, sorted
        /// ordinally and <c>;</c>-joined, positions dropped.
        /// </summary>
        public static string ModKey(IEnumerable<string> modNames)
        {
            var names = modNames.Select(m => m.Trim()).Where(m => m.Length > 0).ToList();
            names.Sort(StringComparer.Ordinal);
            return string.Join(@";", names);
        }

        /// <summary>
        /// The DecoyPairs rows for the precursors written, in Carafe's order.
        /// <paramref name="skippedPairs"/> counts the pairs left out for reusing a precursor.
        /// </summary>
        public static List<DecoyPairRow> Plan(IReadOnlyList<Precursor> precursors, IReadOnlyList<ManifestEntry> manifest,
            out int skippedPairs)
        {
            var bySequence = new Dictionary<string, List<Precursor>>(StringComparer.Ordinal);
            foreach (var precursor in precursors)
            {
                string key = PairingManifestReconciler.Normalize(precursor.StrippedSequence);
                if (!bySequence.TryGetValue(key, out var list))
                    bySequence.Add(key, list = new List<Precursor>());
                list.Add(precursor);
            }
            var groups = new SortedDictionary<int, Dictionary<string, string>>();
            foreach (var entry in manifest)
            {
                if (!groups.TryGetValue(entry.PairIndex, out var group))
                    groups.Add(entry.PairIndex, group = new Dictionary<string, string>(StringComparer.Ordinal));
                group[JavaText.Trim(entry.PeptideType ?? string.Empty).ToLowerInvariant()] = entry.Sequence;
            }
            var planned = new PlannedPairs();
            foreach (var group in groups.Values)
            {
                EmitSidePair(Lookup(group, TARGET), Lookup(group, DECOY), false, bySequence, planned);
                EmitSidePair(Lookup(group, ENTRAPMENT_TARGET), Lookup(group, ENTRAPMENT_DECOY), true, bySequence, planned);
            }
            skippedPairs = planned.Skipped;
            return planned.Rows;
        }

        /// <summary>
        /// Reads a pairing manifest (<c>sequence, decoy, proteins, peptide_type,
        /// peptide_pair_index</c>, columns found by header name), as Carafe's
        /// <c>parseManifest</c> does.
        /// </summary>
        public static List<ManifestEntry> ReadManifest(string path)
        {
            var rows = new List<ManifestEntry>();
            // Carafe reads an empty file as a manifest with no rows; the shared reader refuses one.
            if (new FileInfo(path).Length == 0)
                return rows;
            foreach (var row in PairingManifest.Read(path, true, out _))
                rows.Add(new ManifestEntry(JavaText.Trim(row.Sequence), row.IsDecoy, JavaText.Trim(row.KindText), row.PairIndex));
            return rows;
        }

        private static void EmitSidePair(string targetSequence, string decoySequence, bool entrapment,
            Dictionary<string, List<Precursor>> bySequence, PlannedPairs planned)
        {
            if (targetSequence == null || decoySequence == null)
                return;
            if (!bySequence.TryGetValue(PairingManifestReconciler.Normalize(targetSequence), out var targets) ||
                !bySequence.TryGetValue(PairingManifestReconciler.Normalize(decoySequence), out var decoys))
            {
                return;
            }
            string method = EntrapmentSequences.ReversePreservingCterm(targetSequence) == decoySequence ? @"reverse" : @"cycle";
            var decoyBuckets = BucketByChargeAndMods(decoys).ToDictionary(b => b.Key, b => b.Value, StringComparer.Ordinal);
            foreach (var bucket in BucketByChargeAndMods(targets))
            {
                if (!decoyBuckets.TryGetValue(bucket.Key, out var decoyBucket))
                    continue;
                var targetIds = bucket.Value.Select(p => p.RefSpectraId).OrderBy(id => id).ToList();
                var decoyIds = decoyBucket.Select(p => p.RefSpectraId).OrderBy(id => id).ToList();
                int count = Math.Min(targetIds.Count, decoyIds.Count);
                for (int i = 0; i < count; i++)
                    planned.Add(targetIds[i], decoyIds[i], entrapment, method);
            }
        }

        /// <summary>Precursors by "charge|modKey", buckets in first-seen order.</summary>
        private static List<KeyValuePair<string, List<Precursor>>> BucketByChargeAndMods(List<Precursor> precursors)
        {
            var buckets = new List<KeyValuePair<string, List<Precursor>>>();
            var byKey = new Dictionary<string, List<Precursor>>(StringComparer.Ordinal);
            foreach (var precursor in precursors)
            {
                string key = precursor.Charge.ToString(CultureInfo.InvariantCulture) + @"|" + precursor.ModificationKey;
                if (!byKey.TryGetValue(key, out var bucket))
                {
                    byKey.Add(key, bucket = new List<Precursor>());
                    buckets.Add(new KeyValuePair<string, List<Precursor>>(key, bucket));
                }
                bucket.Add(precursor);
            }
            return buckets;
        }

        private static string Lookup(Dictionary<string, string> group, string peptideType)
        {
            return group.TryGetValue(peptideType, out string sequence) ? sequence : null;
        }

        /// <summary>The rows planned so far, each RefSpectra id used once.</summary>
        private sealed class PlannedPairs
        {
            private readonly HashSet<int> _paired = new HashSet<int>();

            public List<DecoyPairRow> Rows { get; } = new List<DecoyPairRow>();

            public int Skipped { get; private set; }

            public void Add(int targetId, int decoyId, bool entrapment, string method)
            {
                if (_paired.Contains(targetId) || _paired.Contains(decoyId) || targetId == decoyId)
                {
                    Skipped++;
                    return;
                }
                _paired.Add(targetId);
                _paired.Add(decoyId);
                int pairId = Rows.Count / 2 + 1;
                Rows.Add(new DecoyPairRow(targetId, false, entrapment, pairId, null));
                Rows.Add(new DecoyPairRow(decoyId, true, entrapment, pairId, method));
            }
        }

        /// <summary>A written RefSpectra row that may join a pair.</summary>
        public sealed class Precursor
        {
            public Precursor(int refSpectraId, string strippedSequence, int charge, string modKey, string modifiedSequence = null)
            {
                RefSpectraId = refSpectraId;
                StrippedSequence = strippedSequence;
                Charge = charge;
                ModificationKey = modKey;
                ModifiedSequence = modifiedSequence ?? strippedSequence;
            }

            public int RefSpectraId { get; }
            public string StrippedSequence { get; }
            public int Charge { get; }
            /// <summary>The precursor's <see cref="DecoyPairPlanner.ModKey"/>.</summary>
            public string ModificationKey { get; }
            /// <summary>The written <c>peptideModSeq</c>, which with the charge identifies the precursor.</summary>
            public string ModifiedSequence { get; }
        }

        /// <summary>One row of the pairing manifest.</summary>
        public sealed class ManifestEntry
        {
            public ManifestEntry(string sequence, bool isDecoy, string peptideType, int pairIndex)
            {
                Sequence = sequence;
                IsDecoy = isDecoy;
                PeptideType = peptideType;
                PairIndex = pairIndex;
            }

            public string Sequence { get; }
            public bool IsDecoy { get; }
            public string PeptideType { get; }
            public int PairIndex { get; }
        }
    }
}
