/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Stage 6 planning subsystem extracted from <see cref="FirstJoinTask"/>.
    /// Runs the four cross-file planning phases that produce the rescore plan
    /// <see cref="PerFileRescoreTask"/> executes: multi-charge consensus per
    /// file, cross-run consensus RTs, per-file calibration refit, and
    /// reconciliation planning. Mirrors the Stage 6 entry block in
    /// osprey/src/pipeline.rs ~3208-3273.
    ///
    /// Standalone collaborator (does not inherit <see cref="AbstractScoringTask"/>):
    /// takes the pipeline context for logging, and routes every diagnostic dump
    /// through the injected <c>_ctx.Diagnostics</c> sink (the *_ONLY abort uses
    /// <c>OspreyDiagnosticsLog.ExitAfterDump</c>), preserving the Stage-6 dump
    /// call order bisection relies on. Pure planning -- writing the
    /// .reconciliation.json envelopes and publishing the typed byproduct slots
    /// stays in <see cref="FirstJoinTask"/>.
    /// </summary>
    internal sealed class Stage6Planner
    {
        /// <summary>
        /// The four cross-file planning byproducts Stage 6 produces. Consumed by
        /// <see cref="FirstJoinTask"/> to write the reconciliation envelopes and
        /// to publish the typed byproduct slots <see cref="PerFileRescoreTask"/>
        /// reads. <see cref="ReconciliationActions"/> is null when reconciliation
        /// was skipped (single-file / empty consensus).
        /// </summary>
        internal sealed class Stage6Plan
        {
            public IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets;
            public IReadOnlyList<PeptideConsensusRT> Consensus;
            // Concrete Dictionary (not IReadOnlyDictionary) to match the
            // WriteReconciliationFiles parameter the caller forwards it to.
            public Dictionary<string, RTCalibration> RefinedCalibrations;
            public IReadOnlyDictionary<(string File, int Index), ReconcileAction> ReconciliationActions;
        }

        private readonly PipelineContext _ctx;

        internal Stage6Planner(PipelineContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Run the four Stage 6 planning phases in order and return the plan.
        /// Phase order matches Rust pipeline.rs:3217 -- multi-charge consensus
        /// runs first (independent), then cross-run consensus RTs feed the
        /// calibration refit, which feeds reconciliation planning.
        /// </summary>
        internal Stage6Plan Plan(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            var perFileConsensusTargets = ComputeMultiChargeConsensus(perFileEntries, config);
            var consensus = ComputeConsensusRts(perFileEntries, perFileCalibrations, config);
            var refinedCalibrations = RefitCalibrations(perFileEntries, consensus, config);
            var reconciliationActions = PlanReconciliation(
                perFileEntries, perFileParquetPaths, refinedCalibrations,
                perFileCalibrations, consensus, config);

            return new Stage6Plan
            {
                PerFileConsensusTargets = perFileConsensusTargets,
                Consensus = consensus,
                RefinedCalibrations = refinedCalibrations,
                ReconciliationActions = reconciliationActions,
            };
        }

        /// <summary>
        /// Phase 1: multi-charge consensus per file (independent -- runs first
        /// per Rust pipeline.rs:3217, before consensus RT computation).
        /// </summary>
        private IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>>
            ComputeMultiChargeConsensus(
                List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config)
        {
            var perFileConsensusTargets = new Dictionary<string,
                IReadOnlyList<(int Index, double Apex, double Start, double End)>>();
            foreach (var kvp in perFileEntries)
            {
                perFileConsensusTargets[kvp.Key] =
                    MultiChargeConsensus.SelectRescoreTargets(kvp.Value, config.RunFdr);
            }
            int totalMulticharge = 0;
            foreach (var kvp in perFileConsensusTargets)
                totalMulticharge += kvp.Value.Count;
            _ctx.LogInfo(string.Format(
                @"Stage 6 multi-charge consensus: {0} entries need re-scoring across {1} files",
                totalMulticharge, perFileEntries.Count));

            if (_ctx.Diagnostics?.DumpMulticharge ?? false)
            {
                var perFileForDump = new List<KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
                foreach (var kvp in perFileEntries)
                {
                    perFileForDump.Add(new KeyValuePair<string,
                        IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
                }
                _ctx.Diagnostics?.WriteStage6MultichargeDump(
                    perFileForDump, perFileConsensusTargets);
                if (_ctx.Diagnostics?.MultichargeOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_MULTICHARGE_ONLY");
            }
            return perFileConsensusTargets;
        }

        /// <summary>
        /// Phase 2: cross-run consensus RTs (target peptides + paired decoys,
        /// sigmoid(score)-weighted median, hard run_precursor_qvalue gate).
        /// Cross-file consensus is only meaningful with &gt; 1 file -- mirrors
        /// Rust pipeline.rs:4146 where reconciliation_enabled requires
        /// per_file_entries.len() &gt; 1; single-file runs skip consensus,
        /// refit, and reconciliation entirely, leaving multi-charge consensus
        /// rescore as the only Stage 6 work performed.
        /// </summary>
        private IReadOnlyList<PeptideConsensusRT> ComputeConsensusRts(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            OspreyConfig config)
        {
            var perFileForRecon = new List<KeyValuePair<string,
                IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                perFileForRecon.Add(new KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
            }
            // Cross-impl bisection trace for InversePredict: if the
            // OSPREY_DUMP_INV_PREDICT env var is set, ConsensusRts populates
            // this list with one row per detection. The dump is driven via
            // OspreyDiagnostics so the FDR project doesn't have to know about
            // the diagnostic file format.
            List<InvPredictRecord> invPredictTrace = null;
            if (_ctx.Diagnostics?.DumpInvPredict ?? false)
                invPredictTrace = new List<InvPredictRecord>();

            IReadOnlyList<PeptideConsensusRT> consensus =
                perFileEntries.Count > 1
                    ? ConsensusRts.Compute(
                        perFileForRecon, perFileCalibrations,
                        config.Reconciliation.ConsensusFdr,
                        config.ProteinFdr ?? 0.0,
                        invPredictTrace)
                    : Array.Empty<PeptideConsensusRT>();

            if (invPredictTrace != null)
            {
                _ctx.Diagnostics?.WriteStage6InvPredictDump(invPredictTrace);
                if (_ctx.Diagnostics?.InvPredictOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_INV_PREDICT_ONLY");
            }
            int nTargets = 0, nDecoys = 0;
            foreach (var c in consensus)
            {
                if (c.IsDecoy) nDecoys++;
                else nTargets++;
            }
            _ctx.LogInfo(string.Format(
                @"Stage 6 consensus: {0} target peptides, {1} decoy peptides",
                nTargets, nDecoys));

            // Skip the dump on empty consensus to match Rust's
            // dump_stage6_consensus, which silently elides the file when there
            // is nothing to write (gated on Some(file) in Rust, derived from
            // !consensus.is_empty()). Without this gate, C# emits a header-only
            // cs_stage6_consensus.tsv and Test-Regression sees an
            // asymmetric-absence FAIL even though both sides agree on empty.
            if ((_ctx.Diagnostics?.DumpConsensus ?? false) && consensus.Count > 0)
            {
                _ctx.Diagnostics?.WriteStage6ConsensusDump(consensus);
                if (_ctx.Diagnostics?.ConsensusOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CONSENSUS_ONLY");
            }
            return consensus;
        }

        /// <summary>
        /// Phase 3: per-file calibration refit on consensus peptides.
        /// </summary>
        private Dictionary<string, RTCalibration> RefitCalibrations(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyList<PeptideConsensusRT> consensus,
            OspreyConfig config)
        {
            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            foreach (var kvp in perFileEntries)
            {
                var refined = CalibrationRefit.Refit(consensus, kvp.Value,
                    config.Reconciliation.ConsensusFdr);
                if (refined != null)
                    refinedCalibrations[kvp.Key] = refined;
            }
            _ctx.LogInfo(string.Format(
                @"Stage 6 refit: {0}/{1} files produced refined calibrations",
                refinedCalibrations.Count, perFileEntries.Count));

            if (_ctx.Diagnostics?.DumpLoessFit ?? false)
            {
                _ctx.Diagnostics?.WriteStage6LoessFitDump(refinedCalibrations);
                if (_ctx.Diagnostics?.LoessFitOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_LOESS_FIT_ONLY");
            }

            if (_ctx.Diagnostics?.DumpRefit ?? false)
            {
                _ctx.Diagnostics?.WriteStage6RefitDump(refinedCalibrations);
                if (_ctx.Diagnostics?.RefitOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_REFIT_ONLY");
            }
            return refinedCalibrations;
        }

        /// <summary>
        /// Phase 4: reconciliation planning. Reads each file's CWT candidates
        /// from the parquet cache and asks the planner to choose, per (file,
        /// entry), whether to keep the existing peak, switch to a stored CWT
        /// candidate at the consensus RT, or force an integration window.
        /// Mirrors Rust pipeline.rs reconciliation block at ~3260-3380. Only
        /// runs when the cross-file consensus is non-empty (Rust
        /// pipeline.rs:4223): single-file runs (or any case where no peptide had
        /// enough cross-replicate evidence to form a consensus RT) degenerate to
        /// zero reconciliation actions. Returns null when skipped.
        /// </summary>
        private IReadOnlyDictionary<(string File, int Index), ReconcileAction> PlanReconciliation(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyList<PeptideConsensusRT> consensus,
            OspreyConfig config)
        {
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> reconciliationActions = null;
            var perFileCwtCandidates = CwtCandidateLoader.Load(
                perFileEntries, perFileParquetPaths, _ctx.LogWarning);
            var perFileForPlan = new List<KeyValuePair<string,
                IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                perFileForPlan.Add(new KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
            }
            if (perFileCwtCandidates.Count == perFileEntries.Count
                && consensus.Count > 0)
            {
                reconciliationActions = ReconciliationPlanner.Plan(
                    consensus,
                    perFileForPlan,
                    perFileCwtCandidates,
                    refinedCalibrations,
                    perFileCalibrations,
                    config.Reconciliation.ConsensusFdr);
                _ctx.LogInfo(string.Format(
                    @"Stage 6 reconciliation: {0} per-(file, entry) actions planned",
                    reconciliationActions.Count));
            }
            else if (consensus.Count == 0)
            {
                _ctx.LogInfo(@"Stage 6 reconciliation: skipped (empty consensus; single-file or no cross-file evidence)");
            }
            else
            {
                _ctx.LogInfo(string.Format(
                    @"Stage 6 reconciliation: skipped (CWT candidates loaded for {0}/{1} files)",
                    perFileCwtCandidates.Count, perFileEntries.Count));
            }

            // Stage 6 cross-impl bisection dump for the planner output. Fires
            // unconditionally when OSPREY_DUMP_RECONCILIATION=1 is set so the
            // skipped / empty paths still produce a header-only TSV and still
            // honor OSPREY_RECONCILIATION_ONLY for early exit. Mirrors the Rust
            // side after the reconciliation block closes.
            if (_ctx.Diagnostics?.DumpReconciliation ?? false)
            {
                var dumpActions = reconciliationActions
                    ?? new Dictionary<(string File, int Index), ReconcileAction>();
                _ctx.Diagnostics?.WriteStage6ReconciliationDump(
                    dumpActions, perFileForPlan);
                if (_ctx.Diagnostics?.ReconciliationOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_RECONCILIATION_ONLY");
            }
            return reconciliationActions;
        }
    }
}
