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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// What one file's planning produced, handed to the caller the moment that file is done
    /// so its <c>.reconciliation.json</c> can be written and its entries released.
    ///
    /// <para>A named object rather than six positional parameters: the two calibrations, the
    /// actions and the gap-fill list are easy to transpose, and a transposition here would
    /// write a plausible-looking envelope describing the wrong file.</para>
    /// </summary>
    internal sealed class Stage6FilePlan
    {
        public string FileName;
        public IReadOnlyList<FdrEntry> Entries;
        /// <summary>This file's non-Keep actions by entry index, or null when it has none.</summary>
        public IReadOnlyList<KeyValuePair<int, ReconcileAction>> Actions;
        public IReadOnlyList<GapFillTarget> GapFill;
        public RTCalibration RefinedCalibration;
        /// <summary>The join-wide first-pass passing base_id set, complete before the first
        /// file is planned. Every file's envelope records the same set, which is what lets an
        /// HPC worker holding one file compact to the join's set rather than its own.</summary>
        public HashSet<uint> GlobalBaseIds;
    }

    /// <summary>Receives each file's plan as it is produced. See <see cref="Stage6FilePlan"/>.</summary>
    internal delegate void Stage6FilePlanned(Stage6FilePlan plan);

    /// <summary>
    /// Stage 6 planning subsystem extracted from <see cref="FirstPassFdrTask"/>.
    /// Runs the four cross-file planning phases that produce the rescore plan
    /// <see cref="PerFileRescoreTask"/> executes: multi-charge consensus per
    /// file, cross-run consensus RTs, per-file calibration refit, and
    /// reconciliation planning. Mirrors the Stage 6 entry block in
    /// osprey/src/pipeline.rs ~3208-3273.
    ///
    /// <para><b>Two passes over the files, never all of them at once.</b> The phases were
    /// written against one buffer holding every file's survivors, which at 446 files is 289 M
    /// entries and ~100 GB - the peak that killed that run (issue #4526). What they actually
    /// need across files is far smaller: the qualifying peptide, base-id and precursor sets,
    /// and one detection per (peptide, run). On a 257-file cohort that is ~96 K peptides and
    /// ~24 M detections, three orders of magnitude below the survivor count. So pass A folds
    /// each file into those reductions and drops it, the barrier turns them into the consensus,
    /// and pass B replays each file to refit, plan, gap-fill and hand the result straight to
    /// <see cref="Stage6FilePlanned"/> - which writes the envelope and lets the entries go.
    /// Nothing here is O(files x entries).</para>
    ///
    /// Standalone collaborator (not part of the scoring-task family):
    /// takes the pipeline context for logging, and routes every diagnostic dump
    /// through the injected <c>_ctx.Diagnostics</c> sink (the *_ONLY abort uses
    /// <c>OspreyDiagnosticsLog.ExitAfterDump</c>), preserving the Stage-6 dump
    /// call order bisection relies on. Pure planning -- writing the
    /// .reconciliation.json envelopes and publishing the typed byproduct slots
    /// stays in <see cref="FirstPassFdrTask"/>.
    /// </summary>
    internal sealed class Stage6Planner
    {
        /// <summary>
        /// The four cross-file planning byproducts Stage 6 produces. Consumed by
        /// <see cref="FirstPassFdrTask"/> to publish the typed byproduct slots
        /// <see cref="PerFileRescoreTask"/> reads. <see cref="ReconciliationActions"/> is null
        /// when reconciliation was skipped (single-file / empty consensus).
        /// </summary>
        internal sealed class Stage6Plan
        {
            public IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets;
            public IReadOnlyList<PeptideConsensusRT> Consensus;
            // Concrete Dictionary (not IReadOnlyDictionary) to match the
            // parameter the caller forwards it to.
            public Dictionary<string, RTCalibration> RefinedCalibrations;
            public IReadOnlyDictionary<(string File, int Index), ReconcileAction> ReconciliationActions;
        }

        private readonly PipelineContext _ctx;

        internal Stage6Planner(PipelineContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Resident overload: adapts an already-materialized all-files buffer to the streaming
        /// one below. Used by the legacy / rehydrate first-pass paths, which hold the buffer
        /// for their own reasons; the loader simply hands back the list they already have, so
        /// this costs nothing and there is only ever ONE implementation of the phases.
        /// </summary>
        internal Stage6Plan Plan(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            IReadOnlyDictionary<uint, double> libPrecursorMz,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            OspreyConfig config,
            Stage6FilePlanned onFilePlanned)
        {
            var byName = new Dictionary<string, IReadOnlyList<FdrEntry>>(StringComparer.Ordinal);
            var fileNames = new List<string>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                fileNames.Add(kvp.Key);
                byName[kvp.Key] = kvp.Value;
            }
            return Plan(fileNames, name => byName[name], perFileCalibrations, perFileParquetPaths,
                libLookup, libPrecursorMz, perFileIsolationMz, config, onFilePlanned);
        }

        /// <summary>
        /// Run the four Stage 6 planning phases and return the plan, loading each file's
        /// survivors twice rather than holding all of them once. Phase order matches Rust
        /// pipeline.rs:3217 -- multi-charge consensus first (independent), then cross-run
        /// consensus RTs, which feed the calibration refit, which feeds reconciliation
        /// planning.
        ///
        /// <para><paramref name="loadFileEntries"/> is called twice per file - once in each
        /// pass - and its result may be released as soon as this returns.</para>
        /// </summary>
        internal Stage6Plan Plan(
            IReadOnlyList<string> fileNames,
            Func<string, IReadOnlyList<FdrEntry>> loadFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            IReadOnlyDictionary<uint, double> libPrecursorMz,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            OspreyConfig config,
            Stage6FilePlanned onFilePlanned)
        {
            if (fileNames == null)
                throw new ArgumentNullException(nameof(fileNames));
            if (loadFileEntries == null)
                throw new ArgumentNullException(nameof(loadFileEntries));

            var scan = ScanFiles(fileNames, loadFileEntries, perFileParquetPaths, config);
            var consensus = BuildConsensus(scan, perFileCalibrations, fileNames.Count);
            return PlanFiles(fileNames, loadFileEntries, scan, consensus, perFileCalibrations,
                perFileParquetPaths, libLookup, libPrecursorMz, perFileIsolationMz, config,
                onFilePlanned);
        }

        /// <summary>What pass A accumulates: phase 1's per-file rescore targets plus the
        /// cross-file reductions the barrier and pass B need. Every member is O(distinct
        /// peptides), O(distinct precursors) or O(peptides x runs) - none is O(files x
        /// entries), which is the whole point of the split.</summary>
        private sealed class ScanResult
        {
            public Dictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets;
            public ConsensusRts.Accumulator ConsensusAccumulator;
            public HashSet<(uint, byte)> PassingBaseIds;
            public HashSet<(string ModifiedSequence, byte Charge)> PassingPrecursors;
            public HashSet<uint> GlobalBaseIds;
            public int TotalMulticharge;
            /// <summary>Every file's entries, materialized ONLY when a Stage-6 diagnostic dump
            /// that needs them is requested. Null on the production path, which is what keeps
            /// the buffer this class exists to avoid from coming back through the dump.</summary>
            public List<KeyValuePair<string, IReadOnlyList<FdrEntry>>> EntriesForDump;
        }

        /// <summary>
        /// Pass A: fold every file into the cross-file reductions, and compute phase 1's
        /// per-file multi-charge rescore targets while the file is loaded. Each file is
        /// released before the next is read.
        /// </summary>
        private ScanResult ScanFiles(
            IReadOnlyList<string> fileNames,
            Func<string, IReadOnlyList<FdrEntry>> loadFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            bool dumpNeedsEntries = (_ctx.Diagnostics?.DumpMulticharge ?? false) ||
                                    (_ctx.Diagnostics?.DumpReconciliation ?? false);
            var scan = new ScanResult
            {
                PerFileConsensusTargets =
                    new Dictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>>(),
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
                EntriesForDump = dumpNeedsEntries
                    ? new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>(fileNames.Count)
                    : null,
            };

            // Fail-fast: a footer-only metadata check confirms every file's stubs are in
            // range of its parquet (no decode, nothing resident). A missing / corrupt /
            // out-of-range Stage-4 parquet makes the run abort with a clear
            // "delete + regenerate {file}" error rather than reconciling only the good files.
            // Collected across the whole pass and thrown at the barrier, so one corrupt
            // parquet does not hide the others -- and still before any planning happens.
            var cwtInvalid = new List<string>();

            using (var scanProgress = new ProgressReporter(
                       string.Format(@"Reconciliation scan (pass 1 of 2) across {0} file(s)", fileNames.Count),
                       fileNames.Count))
            {
                int done = 0;
                foreach (string fileName in fileNames)
                {
                    var entries = loadFileEntries(fileName);
                    var targets = MultiChargeConsensus.SelectRescoreTargets(entries, config.RunFdr);
                    scan.PerFileConsensusTargets[fileName] = targets;
                    scan.TotalMulticharge += targets.Count;
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
                    CwtCandidateLoader.ValidateFileInRange(fileName, entries, perFileParquetPaths, cwtInvalid);
                    scan.EntriesForDump?.Add(
                        new KeyValuePair<string, IReadOnlyList<FdrEntry>>(fileName, entries));
                    scanProgress.Report(++done);
                }
            }
            CwtCandidateLoader.ThrowIfAnyInvalid(cwtInvalid, fileNames.Count);

            _ctx.LogInfo(string.Format(
                @"Reconciliation multi-charge consensus: {0} entries need re-scoring across {1} files",
                scan.TotalMulticharge, fileNames.Count));

            if (_ctx.Diagnostics?.DumpMulticharge ?? false)
            {
                _ctx.Diagnostics?.WriteStage6MultichargeDump(
                    scan.EntriesForDump, scan.PerFileConsensusTargets);
                if (_ctx.Diagnostics?.MultichargeOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_MULTICHARGE_ONLY");
            }
            return scan;
        }

        /// <summary>
        /// The barrier: phase 2, cross-run consensus RTs (target peptides + paired decoys,
        /// sigmoid(score)-weighted median, hard run_precursor_qvalue gate) computed from what
        /// pass A accumulated. No file is read here.
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
            _ctx.LogInfo(string.Format(
                @"Reconciliation consensus: {0} target peptides, {1} decoy peptides",
                nTargets, nDecoys));

            // Fires UNCONDITIONALLY when OSPREY_DUMP_CONSENSUS=1, so an empty consensus
            // still produces a header-only cs_stage6_consensus.tsv - the same rule the
            // reconciliation dump below follows.
            //
            // This was gated on consensus.Count > 0 to match Rust's dump_stage6_consensus,
            // which elides the file when there is nothing to write, because the cross-impl
            // Test-Regression then saw an asymmetric-absence FAIL. That fixed the comparator's
            // complaint by deleting the FILE, which is backwards: a reader of the directory
            // then cannot tell "empty consensus" from "the dump crashed before writing", and
            // the comparator was subsequently taught to treat symmetric absence as agreement,
            // entrenching it. No live cross-impl gate compares this dump any more
            // (Compare-EndToEnd-Crossimpl.ps1 does not), and its one consumer, Test-Snapshot,
            // is same-impl. See "Never conditionally write an output artifact" in
            // ai/docs/osprey-development-guide.md.
            if (_ctx.Diagnostics?.DumpConsensus ?? false)
            {
                _ctx.Diagnostics?.WriteStage6ConsensusDump(consensus);
                if (_ctx.Diagnostics?.ConsensusOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CONSENSUS_ONLY");
            }
            return consensus;
        }

        /// <summary>
        /// Pass B: phase 3 (per-file calibration refit on consensus peptides), phase 4
        /// (reconciliation planning), and gap-fill identification - all per file, all handed to
        /// <paramref name="onFilePlanned"/> before the next file is read.
        /// </summary>
        private Stage6Plan PlanFiles(
            IReadOnlyList<string> fileNames,
            Func<string, IReadOnlyList<FdrEntry>> loadFileEntries,
            ScanResult scan,
            IReadOnlyList<PeptideConsensusRT> consensus,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            IReadOnlyDictionary<uint, double> libPrecursorMz,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            OspreyConfig config,
            Stage6FilePlanned onFilePlanned)
        {
            // A refit-only bisection dump exits before planning, exactly as it did when refit
            // and planning were separate passes over the whole cohort. Deciding it up front
            // keeps the early exit from doing a cohort's worth of planning it will discard.
            //
            // Gated on the DUMP flag as well as the ONLY flag, because the two env vars are
            // independent and only the dump block calls ExitAfterDump. Testing the ONLY flag
            // alone would skip planning and then NOT exit, so the run would write no
            // reconciliation envelope at all and still report success.
            bool refitOnlyExit =
                ((_ctx.Diagnostics?.DumpLoessFit ?? false) && (_ctx.Diagnostics?.LoessFitOnly ?? false)) ||
                ((_ctx.Diagnostics?.DumpRefit ?? false) && (_ctx.Diagnostics?.RefitOnly ?? false));
            // The reconciliation dump DOES plan - its whole subject is the planned actions -
            // but it exits immediately afterwards, so writing and stamping a full set of
            // envelopes on the way out would leave a diagnostic run looking like a completed
            // planning pass to the next resume.
            bool suppressEnvelopes = (_ctx.Diagnostics?.DumpReconciliation ?? false) &&
                                     (_ctx.Diagnostics?.ReconciliationOnly ?? false);
            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            // Empty consensus (single-file / no cross-file evidence) is a legitimate
            // skip, not an error -- only a corrupt input aborts (in pass A).
            bool planning = consensus.Count > 0 && !refitOnlyExit;
            var actions = planning ? new Dictionary<(string, int), ReconcileAction>() : null;
            // The planner and the gap-fill identifier are handed refinedCalibrations while it is
            // still being filled. That is safe BECAUSE each reads only the file it is planning,
            // and that file's refit is added immediately above the call -- the all-at-once
            // version could not have looked at another file's calibration either.
            var planner = planning
                ? new ReconciliationPlanner.FilePlanner(
                    consensus, scan.PassingBaseIds, refinedCalibrations, perFileCalibrations)
                : null;
            var gapFiller = planning
                ? new GapFillTargetIdentifier.FileIdentifier(
                    consensus, scan.PassingPrecursors, refinedCalibrations, perFileCalibrations,
                    libLookup, libPrecursorMz, perFileIsolationMz)
                : null;

            using (var planProgress = new ProgressReporter(
                       string.Format(@"Reconciliation planning (pass 2 of 2) across {0} file(s)", fileNames.Count),
                       fileNames.Count))
            {
                int done = 0;
                foreach (string fileName in fileNames)
                {
                    var entries = loadFileEntries(fileName);
                    var refined = CalibrationRefit.Refit(consensus, entries, config.Reconciliation.ConsensusFdr);
                    if (refined != null)
                        refinedCalibrations[fileName] = refined;

                    List<KeyValuePair<int, ReconcileAction>> fileActions = null;
                    IReadOnlyList<GapFillTarget> fileGapFill = Array.Empty<GapFillTarget>();
                    if (planning)
                    {
                        // Load this file's CWT candidates on demand and release them at the end
                        // of the iteration -- one file resident at a time.
                        fileActions = new List<KeyValuePair<int, ReconcileAction>>();
                        planner.PlanFile(fileName, entries,
                            CwtCandidateLoader.LoadOneFile(fileName, perFileParquetPaths), fileActions);
                        foreach (var action in fileActions)
                            actions[(fileName, action.Key)] = action.Value;
                        fileGapFill = gapFiller.IdentifyFile(fileName, entries);
                    }

                    // Nothing is handed on when a diagnostic dump is about to end the run: the
                    // caller writes a per-file envelope from this, and an envelope recording
                    // "no actions" because planning was skipped would be indistinguishable
                    // from one that was planned and found none.
                    if (!refitOnlyExit && !suppressEnvelopes)
                    {
                        onFilePlanned?.Invoke(new Stage6FilePlan
                        {
                            FileName = fileName,
                            Entries = entries,
                            Actions = fileActions,
                            GapFill = fileGapFill,
                            RefinedCalibration = refined,
                            GlobalBaseIds = scan.GlobalBaseIds,
                        });
                    }
                    planProgress.Report(++done);
                }
            }

            _ctx.LogInfo(string.Format(
                @"Reconciliation calibration refit: {0}/{1} files produced refined calibrations",
                refinedCalibrations.Count, fileNames.Count));

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

            if (planning)
            {
                _ctx.LogInfo(string.Format(
                    @"Reconciliation: {0} per-(file, entry) actions planned", actions.Count));
            }
            else
            {
                _ctx.LogInfo(@"Reconciliation: skipped (empty consensus; single-file or no cross-file evidence)");
            }

            // Stage 6 cross-impl bisection dump for the planner output. Fires
            // unconditionally when OSPREY_DUMP_RECONCILIATION=1 is set so the
            // skipped / empty paths still produce a header-only TSV and still
            // honor OSPREY_RECONCILIATION_ONLY for early exit. Mirrors the Rust
            // side after the reconciliation block closes.
            if (_ctx.Diagnostics?.DumpReconciliation ?? false)
            {
                var dumpActions = (IReadOnlyDictionary<(string File, int Index), ReconcileAction>)actions
                    ?? new Dictionary<(string File, int Index), ReconcileAction>();
                _ctx.Diagnostics?.WriteStage6ReconciliationDump(dumpActions, scan.EntriesForDump);
                if (_ctx.Diagnostics?.ReconciliationOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_RECONCILIATION_ONLY");
            }

            return new Stage6Plan
            {
                PerFileConsensusTargets = scan.PerFileConsensusTargets,
                Consensus = consensus,
                RefinedCalibrations = refinedCalibrations,
                ReconciliationActions = actions,
            };
        }
    }
}
