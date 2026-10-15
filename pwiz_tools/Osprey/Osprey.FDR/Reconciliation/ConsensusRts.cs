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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.Reconciliation
{
    /// <summary>
    /// Computes consensus library RTs for peptides detected across runs.
    /// Port of <c>compute_consensus_rts</c> in
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public static class ConsensusRts
    {
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
        /// q-value is borderline. Typically set to <c>config.EffectiveProteinFdr</c>.
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

            var accumulator = new Accumulator(consensusFdr, proteinFdrThreshold);
            foreach (var kvp in perFileEntries)
                accumulator.AddFile(kvp.Key, kvp.Value);
            return accumulator.Build(perFileCalibrations, invPredictTrace);
        }

        /// <summary>
        /// The same computation fed ONE FILE AT A TIME, so a caller that can load a file's
        /// entries and drop them again never has to hold every file's at once.
        ///
        /// <para><b>Why this exists.</b> <see cref="Compute"/> takes all files' entries and walks
        /// them three times, which made the caller materialise the whole survivor buffer -
        /// 289 M entries and ~100 GB at 446 files, the peak that killed that run. What the
        /// computation actually NEEDS across files is far smaller: the qualifying peptide and
        /// base-id sets, and one detection per (peptide, run). On a 257-file cohort that is
        /// ~96 K peptides and ~24 M detections, about three orders of magnitude below the
        /// survivor count. Accumulating those and dropping each file's entries replaces an
        /// O(files x entries) structure with an O(distinct) one, which is the difference
        /// between a peak that grows with the cohort and one that does not.</para>
        ///
        /// <para><b>Order.</b> Feed files in the same order <see cref="Compute"/> would have
        /// seen them: within a peptide, detections are kept in arrival order, and the weighted
        /// median resolves ties by that order. The consensus list itself is sorted in
        /// <see cref="Build"/>, so it does not depend on file order - but the
        /// <c>invPredictTrace</c> diagnostic does.</para>
        /// </summary>
        public sealed class Accumulator
        {
            private readonly double _consensusFdr;
            private readonly double _proteinFdrThreshold;
            private readonly HashSet<string> _targetPeptides = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<uint> _targetBaseIds = new HashSet<uint>();
            // Every decoy sequence seen, with the base ids it was seen under. O(distinct decoy
            // sequences), and the reason the decoy half needs no second pass over the files:
            // pairing is decided here at the barrier, once every target base id is known.
            private readonly Dictionary<string, HashSet<uint>> _decoyBaseIdsBySequence =
                new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);
            private readonly Dictionary<(string, bool), List<Detection>> _detections =
                new Dictionary<(string, bool), List<Detection>>();

            public Accumulator(double consensusFdr, double proteinFdrThreshold)
            {
                _consensusFdr = consensusFdr;
                _proteinFdrThreshold = proteinFdrThreshold;
            }

            /// <summary>
            /// Fold one file's entries in. The caller may release them as soon as this returns.
            /// </summary>
            public void AddFile(string fileName, IReadOnlyList<FdrEntry> entries)
            {
                if (entries == null)
                    return;
                foreach (var entry in entries)
                {
                    if (entry.IsDecoy)
                    {
                        // Collected unconditionally and pruned in Build. Whether a decoy
                        // contributes depends on whether its SEQUENCE pairs to a qualifying
                        // target anywhere in the cohort, which is not knowable until every file
                        // has been seen - and the sequence-level closure is wider than the
                        // per-entry base-id test, because charge variants share a modified
                        // sequence but not a base id. Filtering here would silently drop the
                        // detections that difference admits.
                        uint decoyBaseId = entry.EntryId & 0x7FFFFFFFu;
                        if (!_decoyBaseIdsBySequence.TryGetValue(entry.ModifiedSequence, out var seenBaseIds))
                        {
                            seenBaseIds = new HashSet<uint>();
                            _decoyBaseIdsBySequence[entry.ModifiedSequence] = seenBaseIds;
                        }
                        seenBaseIds.Add(decoyBaseId);
                        AddDetection(entry.ModifiedSequence, true, fileName, entry);
                        continue;
                    }

                    // A target's own qualification is the whole test. The all-files variant also
                    // asks whether the sequence is in the qualifying set, but a target that
                    // qualifies is what PUT it there, so that clause can never fail for one.
                    if (!Qualifies(entry, _consensusFdr, _proteinFdrThreshold))
                        continue;
                    _targetPeptides.Add(entry.ModifiedSequence);
                    _targetBaseIds.Add(entry.EntryId & 0x7FFFFFFFu);
                    AddDetection(entry.ModifiedSequence, false, fileName, entry);
                }
            }

            /// <summary>
            /// Resolve decoy pairing, then compute one consensus per (sequence, decoy flag).
            /// </summary>
            public IReadOnlyList<PeptideConsensusRT> Build(
                IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
                IList<InvPredictRecord> invPredictTrace = null)
            {
                if (perFileCalibrations == null)
                    throw new ArgumentNullException(nameof(perFileCalibrations));
                if (_targetPeptides.Count == 0)
                    return Array.Empty<PeptideConsensusRT>();

                // Paired decoys by base_id linkage (entry_id & 0x7FFFFFFF) rather than by
                // stripping a "DECOY_" prefix from the modified sequence. The prefix strip only
                // works for Osprey-generated decoys; it silently misses library-supplied decoys
                // (Carafe etc.) whose modified sequence carries no prefix. Pairing was already
                // established by the FDRBench manifest or composition fallback during library
                // load. Mirrors Rust reconciliation.rs::compute_consensus_rts.
                var decoySequences = new HashSet<string>(StringComparer.Ordinal);
                foreach (var kvp in _decoyBaseIdsBySequence)
                {
                    foreach (uint baseId in kvp.Value)
                    {
                        if (!_targetBaseIds.Contains(baseId))
                            continue;
                        decoySequences.Add(kvp.Key);
                        break;
                    }
                }
                var unpaired = new List<(string, bool)>();
                foreach (var kvp in _detections)
                {
                    if (kvp.Key.Item2 && !decoySequences.Contains(kvp.Key.Item1))
                        unpaired.Add(kvp.Key);
                }
                foreach (var key in unpaired)
                    _detections.Remove(key);

                // Per-peptide consensus computation.
                var consensus = new List<PeptideConsensusRT>();
                foreach (var kvp in _detections)
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
                        Array.Sort(absDevs); // Array.Sort OK: median of single primitive array, no parallel data
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

                // Sort for deterministic output: decoys after targets, then by
                // modified_sequence (ordinal). This is what makes the result independent of
                // the dictionary's enumeration order, and therefore of file order.
                // Array.Sort OK: one consensus entry per (IsDecoy, ModifiedSequence), so the
                // (IsDecoy, ModifiedSequence) key is unique and the comparator never returns 0.
                consensus.Sort((a, b) => // Array.Sort OK: (see above) (IsDecoy, ModifiedSequence) is unique, comparator never ties
                {
                    int cmp = a.IsDecoy.CompareTo(b.IsDecoy);
                    if (cmp != 0)
                        return cmp;
                    return string.CompareOrdinal(a.ModifiedSequence, b.ModifiedSequence);
                });

                return consensus;
            }

            private void AddDetection(string modifiedSequence, bool isDecoy, string fileName, FdrEntry entry)
            {
                var key = (modifiedSequence, isDecoy);
                if (!_detections.TryGetValue(key, out var list))
                {
                    list = new List<Detection>();
                    _detections[key] = list;
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

        private static bool Qualifies(FdrEntry entry, double consensusFdr, double proteinFdrThreshold)
        {
            if (entry.IsDecoy)
                return false;
            if (entry.RunPrecursorQvalue > consensusFdr)
                return false;
            return entry.RunPeptideQvalue <= consensusFdr ||
                   (proteinFdrThreshold > 0.0 && entry.ExperimentProteinQvalue <= proteinFdrThreshold);
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
            Array.Sort(sorted, (a, b) => a.Value.CompareTo(b.Value)); // Array.Sort OK: weighted median; tied Values are by definition equal so output is invariant under tie-permutation

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
