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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.Reconciliation
{
    /// <summary>
    /// Plans inter-replicate peak reconciliation using consensus RTs and stored
    /// CWT candidates. Ports <c>plan_reconciliation</c> and
    /// <c>determine_reconcile_action</c> from
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public static class ReconciliationPlanner
    {
        // Minimum RT tolerance floor (minutes) accommodates scan-resolution rounding.
        private const double MIN_RT_TOLERANCE = 0.1;

        // Fallback global within-peptide MAD when fewer than one consensus peptide
        // has >= 3 detections (e.g., 2-replicate experiment).
        private const double FALLBACK_GLOBAL_MAD_LIB = 0.05;

        // MAD-to-sigma conversion factor (for a normal distribution).
        private const double MAD_TO_SIGMA = 1.4826;

        // Sigma multiplier for RT tolerance (3-sigma).
        private const double SIGMA_FACTOR = 3.0;

        // Minimum survivor count for sigma-clipped MAD before falling back to
        // unclipped median.
        private const int SIGMA_CLIP_MIN_SURVIVORS = 20;

        /// <summary>
        /// Plan inter-replicate peak reconciliation for all entries across all runs.
        /// In-memory / test overload: takes the per-file CWT candidate lists as a
        /// materialized dictionary and adapts it to the streaming overload below.
        /// Production (Stage 6) uses the streaming overload so it never holds all
        /// files' candidates resident.
        /// </summary>
        /// <returns>
        /// A dictionary keyed by (fileName, entryIndex) for entries that need
        /// re-scoring. Entries absent from the map implicitly retain their
        /// current peak (ReconcileAction.Keep).
        /// </returns>
        public static IReadOnlyDictionary<(string File, int Index), ReconcileAction> Plan(
            IReadOnlyList<PeptideConsensusRT> consensus,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>> perFileCwtCandidates,
            IReadOnlyDictionary<string, RTCalibration> perFileRefinedCal,
            IReadOnlyDictionary<string, RTCalibration> perFileOriginalCal,
            double experimentFdr)
        {
            if (perFileCwtCandidates == null)
                throw new ArgumentNullException(nameof(perFileCwtCandidates));
            return Plan(consensus, perFileEntries,
                fileName => perFileCwtCandidates.TryGetValue(fileName, out var fileCwt)
                    ? fileCwt
                    : null,
                perFileRefinedCal, perFileOriginalCal, experimentFdr);
        }

        /// <summary>
        /// Plan inter-replicate peak reconciliation for all entries across all runs.
        /// Streaming entry point: <paramref name="loadFileCwt"/> supplies one file's
        /// CWT candidate lists (indexed by <see cref="FdrEntry.ParquetIndex"/>) on
        /// demand -- it is called once per file inside the per-file loop and its
        /// result falls out of scope before the next file, so no more than one file's
        /// candidates are resident at a time (the streaming fix for the 82-file
        /// Stage-6 planning OOM). It may return null for a file with no candidates
        /// (treated as empty). The cross-file inputs (the passing-base-id set, the
        /// consensus map) never touch CWT candidates, so the streamed result is
        /// element-for-element identical to loading them all up front.
        /// </summary>
        /// <returns>
        /// A dictionary keyed by (fileName, entryIndex) for entries that need
        /// re-scoring. Entries absent from the map implicitly retain their
        /// current peak (ReconcileAction.Keep).
        /// </returns>
        public static IReadOnlyDictionary<(string File, int Index), ReconcileAction> Plan(
            IReadOnlyList<PeptideConsensusRT> consensus,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            Func<string, IReadOnlyList<IReadOnlyList<CwtCandidate>>> loadFileCwt,
            IReadOnlyDictionary<string, RTCalibration> perFileRefinedCal,
            IReadOnlyDictionary<string, RTCalibration> perFileOriginalCal,
            double experimentFdr)
        {
            if (consensus == null)
                throw new ArgumentNullException(nameof(consensus));
            if (perFileEntries == null)
                throw new ArgumentNullException(nameof(perFileEntries));
            if (loadFileCwt == null)
                throw new ArgumentNullException(nameof(loadFileCwt));
            if (perFileRefinedCal == null)
                throw new ArgumentNullException(nameof(perFileRefinedCal));
            if (perFileOriginalCal == null)
                throw new ArgumentNullException(nameof(perFileOriginalCal));

            // Consensus lookup by (modified_sequence, is_decoy).
            var consensusMap = new Dictionary<(string, bool), PeptideConsensusRT>();
            foreach (var c in consensus)
                consensusMap[(c.ModifiedSequence, c.IsDecoy)] = c;

            // Global within-peptide RT MAD (library RT space) — median of
            // per-peptide apex MADs across all target peptides with >= 3
            // detections. See the Rust docstring at reconciliation.rs:469-487
            // for the rationale: after cross-run alignment, within-peptide
            // scatter is roughly peptide-independent (instrument/LC
            // reproducibility), and the cross-peptide median is a far more
            // stable estimator than any single peptide's 3-5-replicate MAD.
            double globalWithinPeptideMadLib;
            {
                var peptideMads = new List<double>();
                foreach (var c in consensus)
                {
                    if (c.IsDecoy)
                        continue;
                    if (c.ApexLibraryRtMad.HasValue)
                        peptideMads.Add(c.ApexLibraryRtMad.Value);
                }
                if (peptideMads.Count == 0)
                {
                    globalWithinPeptideMadLib = FALLBACK_GLOBAL_MAD_LIB;
                }
                else
                {
                    peptideMads.Sort(); // Array.Sort OK: median of a single primitive (double) list, no parallel data; tie order is irrelevant
                    int mid = peptideMads.Count / 2;
                    globalWithinPeptideMadLib = peptideMads.Count % 2 == 0
                        ? 0.5 * (peptideMads[mid - 1] + peptideMads[mid])
                        : peptideMads[mid];
                }
            }

            // Passing precursors keyed on (base_id, charge) where any of the
            // four q-values passes at experimentFdr. Rationale at
            // reconciliation.rs:560-576 — blib admits a precursor if ANY level
            // passes, so reconciliation must include them in every file to keep
            // per-file boundaries self-consistent. We key on
            // base_id = EntryId & 0x7FFFFFFF (plus charge) rather than
            // modified_sequence so paired decoys are recognised regardless of
            // whether their modified_sequence carries a DECOY_ prefix: a target
            // and its library-supplied decoy (Carafe / FDRBench manifest) share
            // the same base_id by construction. Mirrors Rust plan_reconciliation
            // and the base_id linkage already used by ConsensusRts.Compute.
            var passingBaseIds = new HashSet<(uint, byte)>();
            foreach (var fileKvp in perFileEntries)
            {
                foreach (var entry in fileKvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    double bestQ = Math.Min(
                        Math.Min(entry.RunPrecursorQvalue, entry.RunPeptideQvalue),
                        Math.Min(entry.ExperimentPrecursorQvalue, entry.ExperimentPeptideQvalue));
                    if (bestQ <= experimentFdr)
                        passingBaseIds.Add((entry.EntryId & 0x7FFFFFFFu, entry.Charge));
                }
            }

            var actions = new Dictionary<(string, int), ReconcileAction>();
            var emptyCwt = new List<IReadOnlyList<CwtCandidate>>();

            // Per-file progress: planning reconciliation actions across all files ran
            // ~5 min silent on the 82-file join. Console-only, never affects the plan.
            var planProgress = new ProgressReporter(
                string.Format(@"Planning reconciliation across {0} file(s)", perFileEntries.Count),
                perFileEntries.Count);
            int planIdx = 0;
            foreach (var fileKvp in perFileEntries)
            {
                planProgress.Report(++planIdx);
                string fileName = fileKvp.Key;
                var entries = fileKvp.Value;

                // Refined calibration if present, else original.
                RTCalibration cal = null;
                if (!perFileRefinedCal.TryGetValue(fileName, out cal))
                    perFileOriginalCal.TryGetValue(fileName, out cal);
                if (cal == null)
                    continue;

                // Per-file RT tolerance ceiling from refined residuals.
                // Sigma-clipped MAD guards against wrong-peak residuals
                // inflating the ceiling; capped by the original first-pass
                // calibration MAD so each pass can only tighten. See the
                // Rust docstring at reconciliation.rs:570-607.
                double fileCalToleranceCeiling;
                {
                    double rawMad = cal.Stats().MAD;
                    double clipThreshold = rawMad * MAD_TO_SIGMA * SIGMA_FACTOR;
                    double clippedMad = SigmaClippedMad(cal.AbsResiduals, clipThreshold);
                    double refinedTolerance = Math.Max(
                        clippedMad * MAD_TO_SIGMA * SIGMA_FACTOR, MIN_RT_TOLERANCE);

                    double cap = refinedTolerance;
                    if (perFileOriginalCal.TryGetValue(fileName, out var originalCal))
                    {
                        cap = Math.Max(
                            originalCal.Stats().MAD * MAD_TO_SIGMA * SIGMA_FACTOR,
                            MIN_RT_TOLERANCE);
                    }

                    fileCalToleranceCeiling = Math.Min(refinedTolerance, cap);
                }

                // Per-peptide RT tolerance — global within-peptide MAD
                // converted to ~3-sigma, floored at MIN_RT_TOLERANCE and
                // capped by the file's cross-peptide ceiling.
                double peptideTolerance = Math.Min(
                    Math.Max(globalWithinPeptideMadLib * MAD_TO_SIGMA * SIGMA_FACTOR, MIN_RT_TOLERANCE),
                    fileCalToleranceCeiling);

                // Load this file's CWT candidates on demand and release them at
                // the end of the iteration -- one file resident at a time.
                var fileCwt = loadFileCwt(fileName) ?? emptyCwt;

                for (int entryIdx = 0; entryIdx < entries.Count; entryIdx++)
                {
                    var entry = entries[entryIdx];

                    // Only consider entries in the consensus set.
                    var key = (entry.ModifiedSequence, entry.IsDecoy);
                    if (!consensusMap.TryGetValue(key, out var consensusEntry))
                        continue;

                    // Only reconcile precursors that passed experiment-FDR (or
                    // their paired decoys). Pair by base_id (EntryId & 0x7FFFFFFF)
                    // so a library-supplied decoy whose modified_sequence has no
                    // DECOY_ prefix is still recognised -- see the passingBaseIds
                    // rationale above.
                    if (!passingBaseIds.Contains((entry.EntryId & 0x7FFFFFFFu, entry.Charge)))
                        continue;

                    double expectedRt = cal.Predict(consensusEntry.ConsensusLibraryRt);

                    IReadOnlyList<CwtCandidate> cwt;
                    if (entry.ParquetIndex < (uint)fileCwt.Count)
                        cwt = fileCwt[(int)entry.ParquetIndex];
                    else
                        cwt = Array.Empty<CwtCandidate>();

                    var action = DetermineAction(
                        apexRt: entry.ApexRt,
                        cwtCandidates: cwt,
                        expectedMeasuredRt: expectedRt,
                        rtTolerance: peptideTolerance,
                        halfWidth: consensusEntry.MedianPeakWidth / 2.0);

                    // Only store non-Keep actions (Keep is implicit absence).
                    if (!ReferenceEquals(action, ReconcileAction.Keep))
                        actions[(fileName, entryIdx)] = action;
                }
            }
            planProgress.Dispose();

            return actions;
        }

        /// <summary>
        /// Determine what reconciliation action to take for an entry given its
        /// consensus RT. Uses apex-proximity (not boundary containment) so
        /// wide-tailed wrong-apex peaks are correctly rejected. Ports
        /// <c>determine_reconcile_action</c> in
        /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
        /// </summary>
        /// <param name="apexRt">Current peak's apex RT (measured space, minutes).</param>
        /// <param name="cwtCandidates">Stored CWT candidates for this entry.</param>
        /// <param name="expectedMeasuredRt">Expected RT from the refined calibration.</param>
        /// <param name="rtTolerance">Allowed RT deviation from expected.</param>
        /// <param name="halfWidth">Half of consensus median peak width (for forced integration).</param>
        public static ReconcileAction DetermineAction(
            double apexRt,
            IReadOnlyList<CwtCandidate> cwtCandidates,
            double expectedMeasuredRt,
            double rtTolerance,
            double halfWidth)
        {
            // Current peak already at expected RT?
            if (Math.Abs(apexRt - expectedMeasuredRt) <= rtTolerance)
                return ReconcileAction.Keep;

            // Pick the CWT candidate whose apex is closest to expectedMeasuredRt,
            // provided it lies within tolerance.
            if (cwtCandidates != null)
            {
                int bestIdx = -1;
                double bestDeviation = double.PositiveInfinity;
                for (int i = 0; i < cwtCandidates.Count; i++)
                {
                    double deviation = Math.Abs(cwtCandidates[i].ApexRt - expectedMeasuredRt);
                    if (deviation > rtTolerance)
                        continue;
                    if (deviation < bestDeviation)
                    {
                        bestDeviation = deviation;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    var c = cwtCandidates[bestIdx];
                    return new ReconcileAction.UseCwtPeak(
                        candidateIndex: bestIdx,
                        startRt: c.StartRt,
                        apexRt: c.ApexRt,
                        endRt: c.EndRt);
                }
            }

            return new ReconcileAction.ForcedIntegration(
                expectedRt: expectedMeasuredRt,
                halfWidth: halfWidth);
        }

        /// <summary>
        /// Sigma-clipped median: filter absolute residuals at
        /// <paramref name="clipThreshold"/> then return the median of the
        /// survivors. If fewer than <see cref="SIGMA_CLIP_MIN_SURVIVORS"/>
        /// survivors remain, fall back to the raw median of the full input.
        /// </summary>
        internal static double SigmaClippedMad(IReadOnlyList<double> absResiduals, double clipThreshold)
        {
            if (absResiduals == null || absResiduals.Count == 0)
                return 0.0;

            var clipped = new List<double>(absResiduals.Count);
            for (int i = 0; i < absResiduals.Count; i++)
            {
                if (absResiduals[i] <= clipThreshold)
                    clipped.Add(absResiduals[i]);
            }

            if (clipped.Count < SIGMA_CLIP_MIN_SURVIVORS)
            {
                var all = new double[absResiduals.Count];
                for (int i = 0; i < all.Length; i++)
                    all[i] = absResiduals[i];
                Array.Sort(all); // Array.Sort OK: median of single primitive array, no parallel data
                return all[all.Length / 2];
            }

            clipped.Sort(); // Array.Sort OK: median of a single primitive (double) list, no parallel data; tie order is irrelevant
            return clipped[clipped.Count / 2];
        }
    }
}
