/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The cross-run half of Stage 6 planning, run by <see cref="FirstPassFdrTask"/>: one pass
    /// over every run's survivors, folding each into the cross-run reductions, and then the
    /// consensus retention times built from them. Mirrors the Stage 6 entry block in
    /// osprey/src/pipeline.rs ~3208-3273 up to the consensus.
    ///
    /// <para><b>The per-run half is not here.</b> Refitting a run's calibration, planning its
    /// reconciliation actions and finding its gap-fill targets read only that run plus the
    /// <see cref="Stage6Consensus"/> this produces, so they run in
    /// <see cref="PerFileRescoreTask"/>, one run at a time, on the node that rescores the run
    /// (<see cref="RunReconciliationPlanner"/>). They used to be a second pass here, which on a
    /// 446-run cohort kept 42 minutes of per-run work in the one join every HPC run waits
    /// for.</para>
    ///
    /// <para>One run resident at a time. What the consensus needs across runs is the qualifying
    /// peptide, base-id and precursor sets and one detection per (peptide, run) - on a 257-run
    /// cohort ~96 K peptides and ~24 M detections, three orders of magnitude below the survivor
    /// count. Nothing here is O(runs x entries).</para>
    /// </summary>
    internal sealed class Stage6Planner
    {
        /// <summary>What the pass produces: the consensus every run plans from, and the
        /// join-wide set of base_ids that survived first-pass compaction.</summary>
        internal sealed class Result
        {
            public Stage6Consensus Consensus;

            /// <summary>The distinct base_ids of every run's survivors. Every reconciliation
            /// action targets a survivor, so this is the whole set the rescore retains.</summary>
            public HashSet<uint> GlobalBaseIds;
        }

        private readonly PipelineContext _ctx;

        internal Stage6Planner(PipelineContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Fold every run into the cross-run reductions, then build the consensus. Phase order
        /// matches Rust pipeline.rs:3217 - multi-charge consensus first (independent), then
        /// cross-run consensus RTs, which feed each run's calibration refit and planning.
        ///
        /// <para><paramref name="loadFileEntries"/> is called once per run, and its result may be
        /// released as soon as that run is folded.</para>
        /// </summary>
        internal Result Plan(
            IReadOnlyList<string> fileNames,
            Func<string, IReadOnlyList<FdrEntry>> loadFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            if (fileNames == null)
                throw new ArgumentNullException(nameof(fileNames));
            if (loadFileEntries == null)
                throw new ArgumentNullException(nameof(loadFileEntries));

            var scan = ScanFiles(fileNames, loadFileEntries, perFileParquetPaths, config);
            var consensus = BuildConsensus(scan, perFileCalibrations, fileNames.Count);
            if (consensus.Count == 0)
            {
                if (fileNames.Count == 1)
                {
                    // A one-file search says nothing here: it has no cross-run reconciliation to
                    // skip. A --task run over one file does: that task is named for the
                    // reconciliation.
                    if (!ScoringTaskShared.IsSingleFileSearch(config))
                        _ctx.LogInfo(OspreyTasksResources.Stage6Planner_PlanFiles_Cross_run_reconciliation__skipped__because_there_is_only_one_file_);
                }
                else
                {
                    _ctx.LogInfo(OspreyTasksResources.Stage6Planner_PlanFiles_Cross_run_reconciliation__skipped__because_no_peptide_was_detected_in_enough_files_to_);
                }
            }
            return new Result
            {
                Consensus = new Stage6Consensus
                {
                    Consensus = consensus,
                    PassingBaseIds = scan.PassingBaseIds,
                    PassingPrecursors = scan.PassingPrecursors,
                },
                GlobalBaseIds = scan.GlobalBaseIds,
            };
        }

        /// <summary>What the pass accumulates: the cross-run reductions the consensus and each
        /// run's plan need. Every member is O(distinct peptides), O(distinct precursors) or
        /// O(peptides x runs) - none is O(runs x entries).</summary>
        private sealed class ScanResult
        {
            public ConsensusRts.Accumulator ConsensusAccumulator;
            public HashSet<(uint, byte)> PassingBaseIds;
            public HashSet<(string ModifiedSequence, byte Charge)> PassingPrecursors;
            public HashSet<uint> GlobalBaseIds;
            public int TotalMulticharge;
        }

        /// <summary>
        /// Fold every run into the cross-run reductions, and count its multi-charge rescore
        /// targets while it is loaded. Each run is released before the next is read.
        /// </summary>
        private ScanResult ScanFiles(
            IReadOnlyList<string> fileNames,
            Func<string, IReadOnlyList<FdrEntry>> loadFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            var scan = new ScanResult
            {
                // Cross-file consensus is only meaningful with > 1 file -- mirrors Rust
                // pipeline.rs:4146 where reconciliation_enabled requires per_file_entries.len()
                // > 1; single-file runs skip consensus, refit and reconciliation entirely,
                // leaving multi-charge consensus rescore as the only Stage 6 work performed.
                ConsensusAccumulator = fileNames.Count > 1
                    ? new ConsensusRts.Accumulator(
                        config.Reconciliation.ConsensusFdr, config.EffectiveProteinFdr)
                    : null,
                PassingBaseIds = new HashSet<(uint, byte)>(),
                PassingPrecursors = new HashSet<(string ModifiedSequence, byte Charge)>(),
                GlobalBaseIds = new HashSet<uint>(),
            };

            // Fail-fast: a footer-only metadata check confirms every file's stubs are in
            // range of its parquet (no decode, nothing resident). A missing / corrupt /
            // out-of-range Stage-4 parquet makes the run abort with a clear
            // "delete + regenerate {file}" error rather than reconciling only the good files.
            // Collected across the whole pass and thrown at the barrier, so one corrupt
            // parquet does not hide the others -- and still before any planning happens.
            var cwtInvalid = new List<string>();

            _ctx.LogBlockReads(@"Planning before the consensus pass");
            using (var scanProgress = new ProgressReporter(
                       ScoringTaskShared.IsSingleFileSearch(config)
                           ? OspreyTasksResources.Stage6Planner_ScanFiles_Multi_charge_consensus_planning
                           : CountText.Format(fileNames.Count, OspreyTasksResources.Stage6Planner_ScanFiles_Cross_run_consensus_for_1_file,
                               OspreyTasksResources.Stage6Planner_ScanFiles_Cross_run_consensus_across__0__files),
                       fileNames.Count))
            {
                int done = 0;
                // Loading a file's survivors, choosing its multi-charge targets and range-checking
                // its parquet are the file's own work and run on the file lanes. Everything folded
                // into the scan result is shared - the consensus accumulator and the passing sets
                // keep insertion order - so it goes in on this thread, in file order.
                OrderedFileLanes.Run(fileNames.Count, _ctx.RunPlan.FirstPassFdrLanes, i =>
                {
                    var fileEntries = loadFileEntries(fileNames[i]);
                    var fileInvalid = new List<string>();
                    CwtCandidateLoader.ValidateFileInRange(fileNames[i], fileEntries, perFileParquetPaths, fileInvalid);
                    return (Entries: fileEntries,
                        Targets: MultiChargeConsensus.SelectRescoreTargets(fileEntries, config.RunFdr),
                        Invalid: fileInvalid);
                }, (i, file) =>
                {
                    string fileName = fileNames[i];
                    var entries = file.Entries;
                    // Counted, not kept: each run's rescore derives its own multi-charge targets
                    // from its own survivors, so the cross-run map that stood here had no reader.
                    scan.TotalMulticharge += file.Targets.Count;
                    scan.ConsensusAccumulator?.AddFile(fileName, entries);
                    ReconciliationPlanner.CollectPassingBaseIds(
                        entries, config.Reconciliation.ConsensusFdr, scan.PassingBaseIds);
                    GapFillTargetIdentifier.CollectPassingPrecursors(
                        entries, config.Reconciliation.ConsensusFdr, scan.PassingPrecursors);
                    // The join-wide first-pass passing base_id set. These entries are already
                    // compacted (a base_id passing peptide-q in ANY file is kept in ALL files),
                    // so the distinct base_ids across all files ARE that set.
                    foreach (var entry in entries)
                        scan.GlobalBaseIds.Add(entry.EntryId & ScoringTaskShared.BASE_ID_MASK);
                    cwtInvalid.AddRange(file.Invalid);
                    scanProgress.Report(++done);
                });
            }
            CwtCandidateLoader.ThrowIfAnyInvalid(cwtInvalid, fileNames.Count);

            _ctx.LogInfo(CountText.Format(fileNames.Count,
                OspreyTasksResources.Stage6Planner_ScanFiles_Multi_charge_consensus___1__charge_states_will_be_re_scored_at_the_peak_boundaries_of_the_,
                OspreyTasksResources.Stage6Planner_ScanFiles_Multi_charge_consensus___1__charge_states_across__0__files_will_be_re_scored_at_the_peak_,
                scan.TotalMulticharge));
            return scan;
        }

        /// <summary>
        /// The barrier: cross-run consensus RTs (target peptides + paired decoys,
        /// sigmoid(score)-weighted median, hard run_precursor_qvalue gate) computed from what
        /// the pass accumulated. No file is read here.
        /// </summary>
        private IReadOnlyList<PeptideConsensusRT> BuildConsensus(
            ScanResult scan,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            int fileCount)
        {
            // Cross-impl bisection trace for InversePredict: if the
            // OSPREY_DUMP_INV_PREDICT env var is set, the consensus computation populates
            // this list with one row per detection. The dump is driven via
            // OspreyDiagnostics so the FDR project doesn't have to know about
            // the diagnostic file format.
            List<InvPredictRecord> invPredictTrace = null;
            if (_ctx.Diagnostics?.DumpInvPredict ?? false)
                invPredictTrace = new List<InvPredictRecord>();

            IReadOnlyList<PeptideConsensusRT> consensus = scan.ConsensusAccumulator != null
                ? scan.ConsensusAccumulator.Build(perFileCalibrations, invPredictTrace)
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
            // One file has no other run to agree with, so there is no consensus to report.
            if (fileCount > 1)
            {
                _ctx.LogInfo(string.Format(
                    OspreyTasksResources.Stage6Planner_BuildConsensus_Cross_run_consensus_retention_times_computed_for__0__target_and__1__decoy_peptides_,
                    nTargets, nDecoys));
            }

            // Fires UNCONDITIONALLY when OSPREY_DUMP_CONSENSUS=1, so an empty consensus
            // still produces a header-only cs_stage6_consensus.tsv. Its one consumer,
            // Test-Snapshot, is same-impl, and a reader of the directory must be able to tell
            // "empty consensus" from "the dump crashed before writing". See "Never
            // conditionally write an output artifact" in ai/docs/osprey-development-guide.md.
            if (_ctx.Diagnostics?.DumpConsensus ?? false)
            {
                _ctx.Diagnostics?.WriteStage6ConsensusDump(consensus);
                if (_ctx.Diagnostics?.ConsensusOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CONSENSUS_ONLY");
            }
            return consensus;
        }
    }
}
