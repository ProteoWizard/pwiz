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
        internal static void CompeteFromIndices(
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

        /// <summary>
        /// Per-row reproducibility score for the experiment-wide competitions when
        /// OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt;: every row receives its (base_id, target/decoy)
        /// group's MEAN of best-N per-run scores - the mean of the group's N highest member
        /// scores, with each of the (N - k) undetected runs of a k-run group (k &lt; N)
        /// contributing the decoy floor. Stage-4 dedup guarantees at most one entry per base_id
        /// per file, so a group's members ARE its per-run scores and the top-N are the top-N
        /// distinct runs (the invariant behind nRunsDetected == observation count).
        ///
        /// Because every row of a group gets the SAME value, feeding this array to the existing
        /// max-based experiment competition (<see cref="CompeteAll"/> - the max-per-base_id
        /// reduction becomes a no-op) and the
        /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> roll-up yields precursor =
        /// mean(best-N) and peptide = max over its precursors, with decoys treated identically so
        /// the null stays honest. The roll-up stops at PEPTIDE: protein FDR ranks groups by the
        /// max RAW per-peptide SVM score (<c>ProteinFdr.CollectBestPeptideScores</c>), which never
        /// reads this aggregate, so mean(best-N) reaches protein-level results only indirectly,
        /// through which peptides clear the experiment-q gate.
        ///
        /// A single-file run - every group at one member - is the uniform monotonic transform
        /// x -&gt; (x + (N - 1) * floor) / N, so ranking and every q are unchanged from the max
        /// path. The same holds for any N at or above the largest observation count, which is why
        /// N above the run count is refused rather than silently accepted
        /// (<see cref="OspreyEnvironment.ValidateExperimentAggSettings"/>).
        /// </summary>
        internal static double[] ComputeBaseIdMeanBestN(double[] scores, bool[] labels, uint[] entryIds, int bestN)
        {
            int len = scores.Length;
            var targets = new Dictionary<uint, MeanBestNAcc>();
            var decoys = new Dictionary<uint, MeanBestNAcc>();
            var decoyScores = new List<double>();
            for (int idx = 0; idx < len; idx++)
            {
                uint baseId = entryIds[idx] & PercolatorEntry.BASE_ID_MASK;
                var dict = labels[idx] ? decoys : targets;
                // Non-finite scores are dropped from the floor SAMPLE, matching both
                // StreamingDecoyFloor.Add and MeanBestNAcc's own admission rule. NaN would give
                // List.Sort an inconsistent comparer and an undefined order, so the median read
                // out of it would be arbitrary. Infinity is worse than that: it only has to reach
                // the MEAN branch below to make the floor infinite, and AggregateScore's
                // (n - _len) * floor term is then 0 * Infinity == NaN for a FULLY detected group -
                // so one infinite decoy score would poison every base_id in the experiment through
                // the shared floor, not just its own group.
                if (labels[idx] && MeanBestNAcc.IsUsable(scores[idx]))
                    decoyScores.Add(scores[idx]);
                if (dict.TryGetValue(baseId, out MeanBestNAcc acc))
                {
                    acc.Add(scores[idx], bestN);
                    dict[baseId] = acc;
                }
                else
                {
                    dict[baseId] = MeanBestNAcc.First(scores[idx], bestN);
                }
            }

            double floor = ComputeFloorFromDecoyScores(decoyScores);

            var aggScore = new double[len];
            for (int idx = 0; idx < len; idx++)
            {
                uint baseId = entryIds[idx] & PercolatorEntry.BASE_ID_MASK;
                var dict = labels[idx] ? decoys : targets;
                aggScore[idx] = dict[baseId].AggregateScore(floor, bestN);
            }
            return aggScore;
        }

        /// <summary>Per-base_id accumulator for <see cref="ComputeBaseIdMeanBestN"/>: keeps the top-N
        /// member scores (the N best runs) in a fixed-capacity buffer, ascending. Value type with a
        /// small backing array -- the array is mutated in place, the length/count fields are copied
        /// back, so callers MUST write the struct back after <see cref="Add"/> (both existing call
        /// sites do). Internal (not private) so the streaming
        /// <see cref="StreamingFdr.StreamingFirstPassQ"/> can hold it as a field without tripping
        /// inconsistent-accessibility (CS0052). Allocates one length-N array per accumulator --
        /// acceptable for the opt-in mean(best-N) experiment path (never the flag-off golden). For
        /// N=2 the aggregate is bit-identical to the former best-2 form (IEEE addition is
        /// commutative).</summary>
        internal struct MeanBestNAcc
        {
            private double[] _top; // capacity N; _top[0.._len-1] holds the kept scores ascending
            private int _len;      // slots used == min(Count, N)
            public int Count;      // total observations seen

            /// <summary>True for a score this accumulator will admit. NaN and +/-Infinity are both
            /// excluded: a NaN can never be evicted (every comparison against it is false) and an
            /// infinite floor makes AggregateScore's <c>(n - _len) * floor</c> term 0*Inf = NaN
            /// even for a FULLY detected group, so a single bad value would poison the entire
            /// experiment rather than one base_id. Spelled out rather than double.IsFinite because
            /// net472 does not have it.</summary>
            internal static bool IsUsable(double score)
            {
                return !double.IsNaN(score) && !double.IsInfinity(score);
            }

            public static MeanBestNAcc First(double score, int n)
            {
                // An unusable FIRST observation must not seed the buffer - it would occupy _top[0]
                // permanently for exactly the reasons Add guards against. Seeding empty is correct
                // and not a special case: AggregateScore over _len == 0 is (n * floor) / n, the
                // floor, which is what "this unit has no valid observations" should score.
                var top = new double[n];
                if (!IsUsable(score))
                    return new MeanBestNAcc { _top = top, _len = 0, Count = 0 };
                top[0] = score;
                return new MeanBestNAcc { _top = top, _len = 1, Count = 1 };
            }

            public void Add(double score, int n)
            {
                // A NaN would be permanent: `_top[i] > score` is false for NaN so it lands in the
                // highest slot and breaks the ascending invariant, `score > _top[0]` is false
                // against NaN so it can never be evicted, and AggregateScore then returns NaN for
                // EVERY row of this base_id - which loses the competition silently (a NaN fails
                // `tScore > decoyEntry.Value`) and can strand a whole peptide in
                // AccumulatePeptideReps. The MAX aggregation this replaces was structurally immune,
                // so the guard is new surface, not an inherited gap. Dropping the observation
                // matches what max did with it: nothing.
                if (!IsUsable(score))
                    return;
                Count++;
                if (_len < n)
                {
                    // Still filling toward N: insert keeping the buffer ascending.
                    int i = _len - 1;
                    while (i >= 0 && _top[i] > score)
                    {
                        _top[i + 1] = _top[i];
                        i--;
                    }
                    _top[i + 1] = score;
                    _len++;
                }
                else if (score > _top[0])
                {
                    // Full: drop the smallest kept score, slot this one in ascending.
                    int i = 0;
                    while (i + 1 < _len && _top[i + 1] < score)
                    {
                        _top[i] = _top[i + 1];
                        i++;
                    }
                    _top[i] = score;
                }
            }

            public double AggregateScore(double floor, int n)
            {
                double sum = 0.0;
                for (int i = 0; i < _len; i++)
                    sum += _top[i];
                // The (n - _len) runs not detected each contribute the missing-run floor.
                sum += (n - _len) * floor;
                return sum / n;
            }
        }

        /// <summary>The missing-run floor for <see cref="ComputeBaseIdMeanBestN"/>: the expected
        /// score of a NON-detection, estimated from the decoy (null) score distribution. Default =
        /// the decoy MEDIAN (the typical null score, negative on average -- NOT 0, which is the SVM
        /// decision boundary and would drag a missing unit UP toward detection).
        /// OSPREY_MEANBEST2_FLOOR_MEAN switches to the decoy mean; OSPREY_MEANBEST2_FLOOR_PCT to a
        /// low decoy percentile (a harder reproducibility cut). Returns 0 only when there are no
        /// decoys. Sorts <paramref name="decoyScores"/> in place.
        ///
        /// Internal (not private) so the parity test can compare it DIRECTLY against its bounded
        /// streaming twin (<c>StreamingFdr.StreamingFirstPassQ.ComputeDecoyFloor</c>). The two are
        /// different estimators of the same statistic - an exact sorted quantile versus a
        /// fixed-width histogram - so they can never be asserted equal through the q-values alone,
        /// and the difference was previously untestable at any level.</summary>
        internal static double ComputeFloorFromDecoyScores(List<double> decoyScores)
        {
            if (decoyScores.Count == 0)
                return 0.0;
            if (OspreyEnvironment.MeanBest2FloorMean)
            {
                double sum = 0.0;
                foreach (double s in decoyScores)
                    sum += s;
                return sum / decoyScores.Count;
            }
            decoyScores.Sort(); // Array.Sort OK: median/percentile of decoy scores -- tie order cannot change the floor value
            double pct = OspreyEnvironment.MeanBest2FloorPercentile ?? 50.0;
            return PercentileOfSorted(decoyScores, pct);
        }

        /// <summary>Linear-interpolated percentile of an ascending-sorted list (pct in [0,100];
        /// 50 = median). Deterministic.</summary>
        private static double PercentileOfSorted(List<double> sorted, double pct)
        {
            if (sorted.Count == 1 || pct <= 0.0)
                return sorted[0];
            if (pct >= 100.0)
                return sorted[sorted.Count - 1];
            double rank = pct / 100.0 * (sorted.Count - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi)
                return sorted[lo];
            double frac = rank - lo;
            return sorted[lo] * (1.0 - frac) + sorted[hi] * frac;
        }
    }
}
