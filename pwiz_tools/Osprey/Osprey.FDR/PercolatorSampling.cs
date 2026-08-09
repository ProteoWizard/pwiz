/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Selection of which entries train the Percolator model, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468): best-precursor-per-peptide collapsing, the
    /// peptide-grouped stratified fold split, subsampling to a training-size cap,
    /// and the composition of those into a training subset.
    ///
    /// These are pure, deterministic index transforms - they read scores and
    /// labels and return index arrays, never mutating their inputs or touching
    /// the model. They are also parity-locked primitives shared by the straight
    /// through and projection paths (see <see cref="PercolatorEngine"/>), so the
    /// order they emit is part of the observable output, not an implementation
    /// detail: the fold split must keep a target and its paired decoy assignable
    /// independently, matching Rust.
    /// </summary>
    public static class PercolatorSampling
    {
        /// <summary>
        /// Find the best-scoring precursor per peptide from a set of indices.
        /// Returns global indices sorted for deterministic order.
        /// </summary>
        public static int[] BestPrecursorPerPeptide(
            int[] indices, double[] scores, bool[] labels, string[] peptides)
        {
            var best = new Dictionary<string, KeyValuePair<int, double>>();

            foreach (int idx in indices)
            {
                string pept = peptides[idx];
                KeyValuePair<int, double> existing;
                if (best.TryGetValue(pept, out existing))
                {
                    if (scores[idx] > existing.Value)
                        best[pept] = new KeyValuePair<int, double>(idx, scores[idx]);
                }
                else
                {
                    best[pept] = new KeyValuePair<int, double>(idx, scores[idx]);
                }
            }

            var result = new List<int>(best.Count);
            foreach (var kvp in best)
                result.Add(kvp.Value.Key);
            result.Sort(); // Array.Sort OK: best-per-peptide entry indices are distinct ints, so no ties; single primitive array anyway
            return result.ToArray();
        }

        // ============================================================
        // Fold assignment
        // ============================================================

        /// <summary>
        /// Create stratified fold assignments grouped by target peptide, keeping pairs together.
        /// </summary>
        public static int[] CreateStratifiedFoldsByPeptide(
            bool[] labels, string[] peptides, uint[] entryIds, int nFolds)
        {
            // 1. Build base_id -> target peptide mapping
            var baseIdToTargetPeptide = new Dictionary<uint, string>();
            for (int i = 0; i < labels.Length; i++)
            {
                uint baseId = entryIds[i] & PercolatorEntry.BASE_ID_MASK;
                if (!labels[i])
                {
                    if (!baseIdToTargetPeptide.ContainsKey(baseId))
                        baseIdToTargetPeptide[baseId] = peptides[i];
                }
            }

            // 2. Map each entry to its group key
            var groupKeys = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                uint baseId = entryIds[i] & PercolatorEntry.BASE_ID_MASK;
                string key;
                if (!baseIdToTargetPeptide.TryGetValue(baseId, out key))
                    key = peptides[i];
                groupKeys[i] = key;
            }

            // 3. Group all entries by target peptide
            var peptideGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < groupKeys.Length; i++)
            {
                List<int> list;
                if (!peptideGroups.TryGetValue(groupKeys[i], out list))
                {
                    list = new List<int>();
                    peptideGroups[groupKeys[i]] = list;
                }
                list.Add(i);
            }

            // 4. Sort for deterministic assignment, round-robin assign folds
            var sortedKeys = new List<string>(peptideGroups.Keys);
            sortedKeys.Sort(StringComparer.Ordinal); // Array.Sort OK: keys are distinct peptide-group dictionary keys, so the comparator never ties

            var foldAssignments = new int[labels.Length];
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                int fold = i % nFolds;
                foreach (int idx in peptideGroups[sortedKeys[i]])
                    foldAssignments[idx] = fold;
            }

            return foldAssignments;
        }

        // ============================================================
        // Subsampling
        // ============================================================

        /// <summary>
        /// Build the SVM training subset for one Percolator pass: best-per-precursor
        /// dedup, then -- only when the deduped count still exceeds
        /// <paramref name="maxTrainSize"/> -- a peptide-grouped subsample. Returns
        /// indices into the original <paramref name="entries"/> list;
        /// <paramref name="bestPerPrecursor"/> returns the post-dedup indices so the
        /// caller can emit its own path-specific [COUNT] dedup line. Owned here so
        /// the direct (<see cref="PercolatorTrainer.RunPercolator"/>) and streaming
        /// (PercolatorEngine.RunPercolatorStreaming) paths select identical subsets
        /// for identical input instead of hand-mirroring the dedup + index map-back.
        /// </summary>
        /// <returns>
        /// Never null. Both return paths hand back a materialized array - the
        /// best-per-precursor set when it already fits <paramref name="maxTrainSize"/>,
        /// otherwise the peptide-group subsample of it. Callers must not test the result
        /// for null: doing so reads as a real alternative path and hides dead code, which
        /// is exactly what it did in <see cref="PercolatorTrainer.RunPercolator"/> until
        /// issue #4468.
        /// </returns>
        internal static int[] BuildTrainingSubset(
            bool[] labels, uint[] entryIds, string[] peptides,
            IList<PercolatorEntry> entries, int maxTrainSize, ulong seed,
            out int[] bestPerPrecursor, double[] bestScores = null)
        {
            bestPerPrecursor = SelectBestPerPrecursor(labels, entryIds, entries, bestScores);
            if (maxTrainSize <= 0 || bestPerPrecursor.Length <= maxTrainSize)
                return bestPerPrecursor;

            // Project the deduped rows into local arrays, subsample by peptide
            // group, then map the local selection back to entries indices.
            int m = bestPerPrecursor.Length;
            var dedupLabels = new bool[m];
            var dedupEntryIds = new uint[m];
            var dedupPeptides = new string[m];
            for (int i = 0; i < m; i++)
            {
                int gi = bestPerPrecursor[i];
                dedupLabels[i] = labels[gi];
                dedupEntryIds[i] = entryIds[gi];
                dedupPeptides[i] = peptides[gi];
            }

            int[] localSelected = SubsampleByPeptideGroup(
                dedupLabels, dedupEntryIds, dedupPeptides, maxTrainSize, seed);
            var trainSubset = new int[localSelected.Length];
            for (int i = 0; i < localSelected.Length; i++)
                trainSubset[i] = bestPerPrecursor[localSelected[i]];
            return trainSubset;
        }

        /// <summary>
        /// Pick the best-scoring observation per (base_id, isDecoy) tuple across all
        /// entries. Used to deduplicate multi-file observations of the same precursor
        /// before SVM training, so the SVM doesn't see the same peptide N times.
        ///
        /// Score for ranking is taken from PercolatorEntry.Features[0], which is
        /// coelution_sum (matches Rust's selection criterion in pipeline.rs).
        ///
        /// When <paramref name="bestScores"/> is supplied (issue #4355 Phase 4
        /// streaming path, where the stubs carry no resident feature vector) the
        /// per-entry ranking value is read from that array instead of
        /// <c>Features[0]</c>. The two are byte-identical on the first pass
        /// (<c>bestScores[i]</c> is the entry's <c>CoelutionSum</c>, which
        /// <c>CoelutionScorer</c> assigns from <c>features[0]</c>), so the selected
        /// subset is unchanged; only the value's source moves off the O(N) vector.
        /// </summary>
        public static int[] SelectBestPerPrecursor(
            bool[] labels, uint[] entryIds, IList<PercolatorEntry> entries,
            double[] bestScores = null)
        {
            int n = labels.Length;
            // Map base_id to best target index, separately for targets and decoys
            var bestTarget = new Dictionary<uint, int>();
            var bestDecoy = new Dictionary<uint, int>();

            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & PercolatorEntry.BASE_ID_MASK;
                double score = bestScores != null ? bestScores[i] : entries[i].Features[0];

                Dictionary<uint, int> map = labels[i] ? bestDecoy : bestTarget;
                int existing;
                if (map.TryGetValue(baseId, out existing))
                {
                    double existingScore = bestScores != null
                        ? bestScores[existing] : entries[existing].Features[0];
                    if (score > existingScore)
                        map[baseId] = i;
                }
                else
                {
                    map[baseId] = i;
                }
            }

            var result = new int[bestTarget.Count + bestDecoy.Count];
            int idx = 0;
            foreach (int i in bestTarget.Values)
                result[idx++] = i;
            foreach (int i in bestDecoy.Values)
                result[idx++] = i;
            Array.Sort(result); // Array.Sort OK: result holds unique entry indices (one per base_id from bestTarget/bestDecoy), so no ties
            return result;
        }

        /// <summary>
        /// Subsample entries by peptide group, keeping target-decoy pairs and charge states together.
        /// </summary>
        internal static int[] SubsampleByPeptideGroup(
            bool[] labels, uint[] entryIds, string[] peptides,
            int maxEntries, ulong seed)
        {
            int n = labels.Length;
            if (n <= maxEntries)
            {
                var all = new int[n];
                for (int i = 0; i < n; i++)
                    all[i] = i;
                return all;
            }

            // Build peptide groups (group by target peptide via base_id)
            var baseIdToTargetPeptide = new Dictionary<uint, string>();
            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & PercolatorEntry.BASE_ID_MASK;
                if (!labels[i])
                {
                    if (!baseIdToTargetPeptide.ContainsKey(baseId))
                        baseIdToTargetPeptide[baseId] = peptides[i];
                }
            }

            var peptideGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                uint baseId = entryIds[i] & PercolatorEntry.BASE_ID_MASK;
                string key;
                if (!baseIdToTargetPeptide.TryGetValue(baseId, out key))
                    key = peptides[i];
                List<int> list;
                if (!peptideGroups.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    peptideGroups[key] = list;
                }
                list.Add(i);
            }

            // Sort deterministically and shuffle with Fisher-Yates
            var groups = new List<KeyValuePair<string, List<int>>>(peptideGroups);
            groups.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal)); // Array.Sort OK: Keys are distinct peptide-group dictionary keys, so the comparator never ties (the following Fisher-Yates shuffle then randomizes deterministically)

            ulong rngState = seed;
            for (int i = groups.Count - 1; i >= 1; i--)
            {
                rngState ^= rngState << 13;
                rngState ^= rngState >> 7;
                rngState ^= rngState << 17;
                int j = (int)(rngState % (ulong)(i + 1));
                var tmp = groups[i];
                groups[i] = groups[j];
                groups[j] = tmp;
            }

            // Select groups until we reach maxEntries
            var selected = new List<int>(maxEntries);
            foreach (var group in groups)
            {
                if (selected.Count + group.Value.Count > maxEntries && selected.Count > 0)
                    break;
                selected.AddRange(group.Value);
            }

            selected.Sort(); // Array.Sort OK: selected holds distinct entry indices, so no ties; single primitive array anyway
            return selected.ToArray();
        }
    }
}
