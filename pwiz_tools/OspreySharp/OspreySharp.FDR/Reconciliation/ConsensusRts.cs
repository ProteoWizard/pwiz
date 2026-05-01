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
using System.Linq;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR.Reconciliation
{
    /// <summary>
    /// Computes consensus library RTs for peptides detected across runs.
    /// Port of <c>compute_consensus_rts</c> in
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public static class ConsensusRts
    {
        private const string DECOY_PREFIX = @"DECOY_";

        /// <summary>
        /// For each target peptide passing <paramref name="consensusFdr"/> at the
        /// run-precursor level (hard gate), and its paired decoy, computes a
        /// consensus library RT using a sigmoid-of-SVM-score weighted median of
        /// per-run detections mapped back to library RT space.
        /// </summary>
        /// <param name="perFileEntries">
        /// Per-file scored entries (after first-pass FDR). Order is preserved;
        /// output consensus is sorted deterministically regardless of input
        /// order.
        /// </param>
        /// <param name="perFileCalibrations">
        /// Per-file RT calibrations for inverse prediction (measured → library).
        /// </param>
        /// <param name="consensusFdr">
        /// FDR threshold for selecting consensus peptides (typically 0.01).
        /// </param>
        /// <param name="proteinFdrThreshold">
        /// If &gt; 0, rescue borderline peptides whose first-pass protein
        /// q-value is &lt;= this threshold. Lets peptides from strong proteins
        /// contribute to consensus RT computation even if their own peptide
        /// q-value is borderline. Typically set to <c>config.ProteinFdr</c>.
        /// Pass 0.0 to disable.
        /// </param>
        /// <param name="invPredictTrace">
        /// If non-null, populated with one <see cref="InvPredictRecord"/> per
        /// detection contributing to a consensus computation, capturing the
        /// (apex_rt, library_rt, weight) triple that flows into the weighted
        /// median. The caller drives the diagnostic dump (see
        /// <c>OspreyDiagnostics.WriteStage6InvPredictDump</c>).
        /// </param>
        public static IReadOnlyList<PeptideConsensusRT> Compute(
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            double consensusFdr,
            double proteinFdrThreshold,
            IList<InvPredictRecord> invPredictTrace = null)
        {
            if (perFileEntries == null)
                throw new ArgumentNullException(nameof(perFileEntries));
            if (perFileCalibrations == null)
                throw new ArgumentNullException(nameof(perFileCalibrations));

            // 1. Collect target peptides passing the run-level hard gate
            //    (or rescued by protein FDR for peptide-level borderline cases).
            var targetPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (Qualifies(entry, consensusFdr, proteinFdrThreshold))
                        targetPeptides.Add(entry.ModifiedSequence);
                }
            }
            if (targetPeptides.Count == 0)
                return Array.Empty<PeptideConsensusRT>();

            // 2. Collect paired decoy peptides (DECOY_<target_seq>).
            var decoyPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy)
                        continue;
                    var targetSeq = entry.ModifiedSequence.StartsWith(DECOY_PREFIX, StringComparison.Ordinal)
                        ? entry.ModifiedSequence.Substring(DECOY_PREFIX.Length)
                        : entry.ModifiedSequence;
                    if (targetPeptides.Contains(targetSeq))
                        decoyPeptides.Add(entry.ModifiedSequence);
                }
            }

            // 3. Collect detections for consensus peptides. For targets, only
            //    detections that qualify. For decoys, all detections of paired
            //    decoy sequences.
            //    Detection tuple: (fileName, apexRt, score, peakWidth, coelutionSum).
            var detections = new Dictionary<(string, bool), List<Detection>>();
            foreach (var kvp in perFileEntries)
            {
                var fileName = kvp.Key;
                foreach (var entry in kvp.Value)
                {
                    bool include = entry.IsDecoy
                        ? decoyPeptides.Contains(entry.ModifiedSequence)
                        : targetPeptides.Contains(entry.ModifiedSequence) &&
                          Qualifies(entry, consensusFdr, proteinFdrThreshold);
                    if (!include)
                        continue;

                    var key = (entry.ModifiedSequence, entry.IsDecoy);
                    if (!detections.TryGetValue(key, out var list))
                    {
                        list = new List<Detection>();
                        detections[key] = list;
                    }
                    list.Add(new Detection
                    {
                        FileName = fileName,
                        ApexRt = entry.ApexRt,
                        Score = entry.Score,
                        PeakWidth = entry.EndRt - entry.StartRt,
                        CoelutionSum = entry.CoelutionSum,
                    });
                }
            }

            // 4. Per-peptide consensus computation.
            var consensus = new List<PeptideConsensusRT>();
            foreach (var kvp in detections)
            {
                var modifiedSequence = kvp.Key.Item1;
                var isDecoy = kvp.Key.Item2;
                var dets = kvp.Value;
                if (dets.Count == 0)
                    continue;

                var libraryRtWeights = new List<(double Value, double Weight)>(dets.Count);
                var peakWidthWeights = new List<(double Value, double Weight)>(dets.Count);

                foreach (var det in dets)
                {
                    if (!perFileCalibrations.TryGetValue(det.FileName, out var cal))
                        continue;
                    double libraryRt = cal.InversePredict(det.ApexRt);
                    if (!IsFinite(libraryRt) || !(det.CoelutionSum > 0.0))
                        continue;

                    // Weight by sigmoid(SVM score). Floor at 1e-6 so every
                    // detection keeps a non-zero weight (avoids degenerate
                    // zero-total-weight when all scores are very negative).
                    double weight = Math.Max(1e-6, 1.0 / (1.0 + Math.Exp(-det.Score)));
                    libraryRtWeights.Add((libraryRt, weight));
                    peakWidthWeights.Add((det.PeakWidth, weight));

                    invPredictTrace?.Add(new InvPredictRecord
                    {
                        FileName = det.FileName,
                        ModifiedSequence = modifiedSequence,
                        IsDecoy = isDecoy,
                        ApexRt = det.ApexRt,
                        LibraryRt = libraryRt,
                        Weight = weight,
                    });
                }

                if (libraryRtWeights.Count == 0)
                    continue;

                double consensusLibraryRt = WeightedMedian(libraryRtWeights);
                double medianPeakWidth = WeightedMedian(peakWidthWeights);
                int nRunsDetected = libraryRtWeights.Count;

                // Within-peptide RT MAD in library RT space. Requires >= 3
                // detections for a stable estimate (MAD on 2 points is half the
                // range and not robust).
                double? apexLibraryRtMad = null;
                if (nRunsDetected >= 3)
                {
                    var absDevs = new double[nRunsDetected];
                    for (int i = 0; i < nRunsDetected; i++)
                        absDevs[i] = Math.Abs(libraryRtWeights[i].Value - consensusLibraryRt);
                    Array.Sort(absDevs);
                    int mid = absDevs.Length / 2;
                    apexLibraryRtMad = absDevs.Length % 2 == 0
                        ? 0.5 * (absDevs[mid - 1] + absDevs[mid])
                        : absDevs[mid];
                }

                consensus.Add(new PeptideConsensusRT
                {
                    ModifiedSequence = modifiedSequence,
                    IsDecoy = isDecoy,
                    ConsensusLibraryRt = consensusLibraryRt,
                    MedianPeakWidth = medianPeakWidth,
                    NRunsDetected = nRunsDetected,
                    ApexLibraryRtMad = apexLibraryRtMad,
                });
            }

            // 5. Sort for deterministic output: decoys after targets, then by
            //    modified_sequence (ordinal).
            consensus.Sort((a, b) =>
            {
                int cmp = a.IsDecoy.CompareTo(b.IsDecoy);
                if (cmp != 0)
                    return cmp;
                return string.CompareOrdinal(a.ModifiedSequence, b.ModifiedSequence);
            });

            return consensus;
        }

        private static bool Qualifies(FdrEntry entry, double consensusFdr, double proteinFdrThreshold)
        {
            if (entry.IsDecoy)
                return false;
            if (entry.RunPrecursorQvalue > consensusFdr)
                return false;
            return entry.RunPeptideQvalue <= consensusFdr ||
                   (proteinFdrThreshold > 0.0 && entry.RunProteinQvalue <= proteinFdrThreshold);
        }

        private static bool IsFinite(double d)
        {
            return !double.IsNaN(d) && !double.IsInfinity(d);
        }

        /// <summary>
        /// Cumulative-weight median. Sorts by value ascending, walks the
        /// cumulative weight, returns the first value whose cumulative weight
        /// crosses half the total. All values must be finite; zero-weight
        /// pairs are permitted but should be avoided by callers (caller
        /// floors weight at 1e-6).
        /// </summary>
        internal static double WeightedMedian(IReadOnlyList<(double Value, double Weight)> pairs)
        {
            if (pairs.Count == 0)
                return 0.0;
            if (pairs.Count == 1)
                return pairs[0].Value;

            var sorted = pairs.ToArray();
            Array.Sort(sorted, (a, b) => a.Value.CompareTo(b.Value));

            double totalWeight = 0.0;
            for (int i = 0; i < sorted.Length; i++)
                totalWeight += sorted[i].Weight;
            double half = totalWeight / 2.0;

            double cumulative = 0.0;
            for (int i = 0; i < sorted.Length; i++)
            {
                cumulative += sorted[i].Weight;
                if (cumulative >= half)
                    return sorted[i].Value;
            }
            return sorted[sorted.Length - 1].Value;
        }

        private struct Detection
        {
            public string FileName;
            public double ApexRt;
            public double Score;
            public double PeakWidth;
            public double CoelutionSum;
        }
    }
}
