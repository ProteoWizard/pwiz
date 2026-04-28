/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR.Reconciliation
{
    /// <summary>
    /// Post-FDR multi-charge consensus leader selection. Groups a per-file
    /// entry list by modified sequence; for each group where at least one
    /// charge state passes FDR, picks the highest-SVM-score passing entry as
    /// the consensus leader. Any other charge state whose apex RT lies
    /// outside half the leader's peak width is returned as a re-scoring
    /// target at the leader's boundaries.
    /// Ports <c>select_post_fdr_consensus</c> in
    /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
    /// </summary>
    public static class MultiChargeConsensus
    {
        /// <summary>Minimum RT match tolerance floor (minutes).</summary>
        public const double MIN_RT_MATCH_TOLERANCE = 0.1;

        /// <summary>
        /// Return per-entry rescore targets (entryIndex, apex, start, end)
        /// derived from the multi-charge consensus leader in each peptide
        /// group. Groups with one entry, or where no charge state passes
        /// FDR, contribute nothing.
        /// </summary>
        public static IReadOnlyList<(int Index, double Apex, double Start, double End)> SelectRescoreTargets(
            IReadOnlyList<FdrEntry> entries,
            double fdrThreshold)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                var seq = entries[i].ModifiedSequence;
                if (!groups.TryGetValue(seq, out var indices))
                {
                    indices = new List<int>();
                    groups[seq] = indices;
                }
                indices.Add(i);
            }

            var targets = new List<(int, double, double, double)>();

            foreach (var indices in groups.Values)
            {
                if (indices.Count <= 1)
                    continue;

                int bestIdx = PickBestPassing(entries, indices, fdrThreshold);
                if (bestIdx < 0)
                    continue;

                double consensusApex = entries[bestIdx].ApexRt;
                double consensusStart = entries[bestIdx].StartRt;
                double consensusEnd = entries[bestIdx].EndRt;
                double consensusWidth = consensusEnd - consensusStart;
                double rtMatchTolerance = Math.Max(MIN_RT_MATCH_TOLERANCE, consensusWidth / 2.0);

                foreach (var idx in indices)
                {
                    if (idx == bestIdx)
                        continue;
                    double apexDiff = Math.Abs(entries[idx].ApexRt - consensusApex);
                    if (apexDiff > rtMatchTolerance)
                        targets.Add((idx, consensusApex, consensusStart, consensusEnd));
                }
            }

            return targets;
        }

        /// <summary>
        /// Pick the highest-SVM-score entry among the group members that pass
        /// FDR at <paramref name="fdrThreshold"/>. Ties on score are broken by
        /// lower run_precursor_qvalue. Returns -1 if no entry passes.
        /// </summary>
        private static int PickBestPassing(
            IReadOnlyList<FdrEntry> entries, IReadOnlyList<int> indices, double fdrThreshold)
        {
            int bestIdx = -1;
            double bestScore = 0;
            double bestQvalue = 0;

            foreach (var idx in indices)
            {
                var entry = entries[idx];
                if (entry.RunPrecursorQvalue > fdrThreshold)
                    continue;

                bool isBetter;
                if (bestIdx < 0)
                {
                    isBetter = true;
                }
                else if (entry.Score > bestScore)
                {
                    isBetter = true;
                }
                else if (entry.Score < bestScore)
                {
                    isBetter = false;
                }
                else
                {
                    // Tie on score → lower q-value wins.
                    isBetter = entry.RunPrecursorQvalue < bestQvalue;
                }

                if (isBetter)
                {
                    bestIdx = idx;
                    bestScore = entry.Score;
                    bestQvalue = entry.RunPrecursorQvalue;
                }
            }

            return bestIdx;
        }
    }
}
