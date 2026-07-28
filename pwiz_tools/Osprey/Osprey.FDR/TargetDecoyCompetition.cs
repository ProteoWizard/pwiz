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
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Target-decoy competition, extracted from the original <c>PercolatorFdr</c>
    /// god class (issue #4468): group the scored population by base id, let each
    /// target compete against its paired decoy, and return the winners in
    /// descending score order.
    ///
    /// This is the step that turns a per-observation score array into the one
    /// observation per precursor that q-value estimation is allowed to count. The
    /// competition is parity-locked - the winner set AND its order are part of the
    /// observable output, not an implementation detail - so these move verbatim.
    /// </summary>
    internal static class TargetDecoyCompetition
    {
        // ============================================================
        // Target-decoy competition and q-value computation
        // ============================================================

        /// <summary>
        /// Core competition logic: group by base_id, compete, return winners sorted by score desc.
        ///
        /// This is deliberately a SEPARATE implementation from
        /// <see cref="FdrController.CompeteAndFilter{T}"/>, not a duplicate to be
        /// merged: the two serve different regimes. This array/index form is the
        /// hot Percolator path -- it works on pre-flattened primitive arrays and a
        /// caller-supplied index subset, returns winner arrays for downstream
        /// scratch-pooled q-value passes (see <c>CountPassing</c>), and
        /// allocates nothing on the scratch overload. <c>CompeteAndFilter</c> is
        /// the ergonomic generic form for simple-FDR callers
        /// (<see cref="PercolatorEngine.RunSimpleFdr"/>): it competes an
        /// <c>IEnumerable&lt;T&gt;</c> via score/decoy/id selectors and returns a
        /// typed result. Same competition rule (strict &gt;, ties to decoy), two
        /// shapes tuned to performance vs. ergonomics.
        /// </summary>
        public static void CompeteFromIndices(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            int[] indices,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var targets = new Dictionary<uint, KeyValuePair<int, double>>();
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>();

            // Throttled per-row progress for the large experiment / PEP competitions -- the
            // ~344M-row base_id reduction below ran ~90 s silent at 82 files. Console-only via
            // the caller's reporter (null on the small per-file per-run calls, which report at
            // their own per-file granularity); never affects the winners, so q-values are
            // byte-identical.
            long processed = 0;
            foreach (int idx in indices)
            {
                if (progress != null && (++processed & 0x3FFFFF) == 0)
                    progress.Report(processed);
                uint baseId = entryIds[idx] & PercolatorEntry.BASE_ID_MASK;
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
            }

            CompeteFromDicts(targets, decoys,
                out winnerIndices, out winnerScores, out winnerIsDecoy, out _);
        }

        /// <summary>
        /// Shared finish for target/decoy competition: given per-base_id best-target and
        /// best-decoy maps (winning row index + score), compete each pair (higher score wins,
        /// ties to decoy), add unpaired decoys, and sort winners by score desc / base_id asc.
        /// Extracted from <see cref="CompeteFromIndices"/> so the flat-array path (which builds
        /// the maps by walking an index subset) and the streaming path (issue #4355 struct-shrink
        /// S3, which builds the identical maps by pushing rows in flat (file,row) order) share the
        /// EXACT compete + sort, and so cannot drift. The stored index is the winning row's flat
        /// index / streaming ordinal; both label the same row because the streaming pass visits
        /// rows in the same order the flat arrays were built. <paramref name="winnerBaseIds"/>
        /// carries each winner's base_id (the map key) so the streaming path can key the
        /// experiment-precursor / PEP maps WITHOUT a resident <c>entryIds[]</c> array (the flat
        /// path recovers the same base_id via <c>entryIds[wi[rank]] &amp; PercolatorEntry.BASE_ID_MASK</c>).
        /// </summary>
        internal static void CompeteFromDicts(
            Dictionary<uint, KeyValuePair<int, double>> targets,
            Dictionary<uint, KeyValuePair<int, double>> decoys,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            out uint[] winnerBaseIds)
        {
            // Compete pairs: higher score wins, ties go to decoy
            var winners = new List<Tuple<int, double, bool, uint>>(targets.Count);
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;

                KeyValuePair<int, double> decoyEntry;
                if (decoys.TryGetValue(baseId, out decoyEntry))
                {
                    if (tScore > decoyEntry.Value)
                        winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                    else
                        winners.Add(Tuple.Create(decoyEntry.Key, decoyEntry.Value, true, baseId));
                }
                else
                {
                    winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                }
            }
            // Unpaired decoys
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                    winners.Add(Tuple.Create(kvp.Value.Key, kvp.Value.Value, true, kvp.Key));
            }

            // Sort by score desc, then base_id asc for deterministic tiebreaking.
            // Array.Sort OK: the secondary key Item4 is the unique base_id, so the
            // comparator never returns 0 and the unstable-sort tie path is unreachable.
            winners.Sort((a, b) => // Array.Sort OK: (see above) secondary key Item4 is unique base_id, comparator never ties
            {
                int cmp = b.Item2.CompareTo(a.Item2);
                if (cmp != 0)
                    return cmp;
                return a.Item4.CompareTo(b.Item4);
            });

            winnerIndices = new int[winners.Count];
            winnerScores = new double[winners.Count];
            winnerIsDecoy = new bool[winners.Count];
            winnerBaseIds = new uint[winners.Count];
            for (int i = 0; i < winners.Count; i++)
            {
                winnerIndices[i] = winners[i].Item1;
                winnerScores[i] = winners[i].Item2;
                winnerIsDecoy[i] = winners[i].Item3;
                winnerBaseIds[i] = winners[i].Item4;
            }
        }

        internal static void CompeteAll(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;
            CompeteFromIndices(scores, labels, entryIds, allIndices,
                out winnerIndices, out winnerScores, out winnerIsDecoy, progress);
        }

        /// <summary>
        /// Scratch-pooled internal variant of <see cref="CompeteFromIndices"/>.
        /// Writes winners into <paramref name="scratch"/>'s three
        /// CompetitionWinner* arrays (prefix [0..returned count) is
        /// active). Same algorithm as the allocating version; only the
        /// output destination differs. Returns the active winner count.
        /// </summary>
        internal static int CompeteFromIndicesInto(
            double[] scores, bool[] labels, uint[] entryIds,
            int[] indices, int indicesCount,
            SvmTrainScratch scratch)
        {
            // Allocate the small per-call dictionaries / list at full
            // expected capacity to avoid rehash growth. Could be pooled
            // on scratch in a follow-up; the n*p allocations above are
            // the bigger LOH issue.
            var targets = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);

            for (int ii = 0; ii < indicesCount; ii++)
            {
                int idx = indices[ii];
                uint baseId = entryIds[idx] & PercolatorEntry.BASE_ID_MASK;
                double s = scores[idx];
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
            }

            // Walk pairs into local struct array (parallel-array layout
            // avoids the per-element Tuple class allocation that the
            // public CompeteFromIndices pays).
            int maxWinners = targets.Count + decoys.Count;
            scratch.EnsureCountPassingCapacity(maxWinners);
            int[] winIdx = scratch.CompetitionWinnerIndices;
            double[] winScores = scratch.CompetitionWinnerScores;
            bool[] winDecoy = scratch.CompetitionWinnerIsDecoy;
            // baseIds for tie-break ordering; reuse CountPassingIndices
            // as a uint[] surrogate (interpret bits). Cleaner: small
            // separate buffer; for now allocate per-call (small).
            var winBaseIds = new uint[maxWinners];

            int n = 0;
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;
                KeyValuePair<int, double> de;
                if (decoys.TryGetValue(baseId, out de))
                {
                    if (tScore > de.Value)
                    { winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++; }
                    else
                    { winIdx[n] = de.Key; winScores[n] = de.Value; winDecoy[n] = true; winBaseIds[n] = baseId; n++; }
                }
                else
                {
                    winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++;
                }
            }
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                {
                    winIdx[n] = kvp.Value.Key; winScores[n] = kvp.Value.Value;
                    winDecoy[n] = true; winBaseIds[n] = kvp.Key; n++;
                }
            }

            // Sort: score desc, then baseId asc. Build index permutation
            // then permute the parallel arrays. Sorting an int[] of
            // length n with a comparison delegate beats the previous
            // List<Tuple<...>>.Sort because no per-element boxing was
            // required to populate the list.
            var perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;
            // The tie-break key (winBaseIds) is unique per row -- post-deduplication
            // best-per-precursor selection above guarantees one row per (base_id, isDecoy)
            // tuple -- so the comparator never returns 0 for distinct rows and introsort's
            // instability is moot. Exemption comment must be on the Array.Sort line itself
            // for the regex in CodeInspectionTest.TestNoUnstableArraySort to recognize it.
            Array.Sort(perm, (a, b) => // Array.Sort OK: unique baseId tie-break makes comparator total
            {
                int cmp = winScores[b].CompareTo(winScores[a]);
                if (cmp != 0) return cmp;
                return winBaseIds[a].CompareTo(winBaseIds[b]);
            });

            // Apply permutation in-place via scratch swap arrays. Reuse
            // the still-spare prefix of CountPassingQvalues as a double
            // swap buffer; for int and bool we need small temp arrays.
            var tmpIdx = new int[n];
            var tmpScores = new double[n];
            var tmpDecoy = new bool[n];
            for (int i = 0; i < n; i++)
            {
                tmpIdx[i] = winIdx[perm[i]];
                tmpScores[i] = winScores[perm[i]];
                tmpDecoy[i] = winDecoy[perm[i]];
            }
            Array.Copy(tmpIdx, winIdx, n);
            Array.Copy(tmpScores, winScores, n);
            Array.Copy(tmpDecoy, winDecoy, n);
            return n;
        }
    }
}
