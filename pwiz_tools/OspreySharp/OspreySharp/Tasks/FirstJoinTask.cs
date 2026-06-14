/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// First-join phase of the OspreySharp pipeline (Stage 5 in the
    /// HPC-boundary view from <c>Osprey-workflow.html</c>): runs the
    /// first-pass Percolator SVM + protein FDR over the joined per-file
    /// scores, persists the per-file 1st-pass FDR sidecar, compacts
    /// each file's stub list to the post-first-pass passing base_ids,
    /// and (when reconciliation is enabled and we have at least one
    /// file's worth of evidence) plans the per-(file, entry)
    /// reconciliation actions that PerFileRescoreTask will execute.
    /// All work that requires all-file representation lives here —
    /// this is the natural fan-out / join boundary on an HPC node.
    ///
    /// Phase A scope: this task is a thin orchestration wrapper that
    /// delegates to the existing private (now <c>internal</c>)
    /// AnalysisPipeline methods (RunFdr, RunFirstPassProteinFdr,
    /// WriteFdrScoresSidecars, WriteReconciliationFiles) plus the
    /// FDR.Reconciliation static helpers (MultiChargeConsensus,
    /// ConsensusRts, CalibrationRefit, ReconciliationPlanner). The
    /// inline planning block from <c>AnalysisPipeline.Run</c> moved
    /// here verbatim; the only changes are LogInfo / LogWarning /
    /// LogError → ctx.LogInfo etc., and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Outputs (PerFileConsensusTargets, ReconciliationActions,
    /// RefinedCalibrations, PerFileGapFillForRescore) are exposed as
    /// instance properties for the next task (PerFileRescoreTask) to
    /// consume after this one completes successfully. The
    /// <c>PlanningPerformed</c> byproduct is the gate for that next task —
    /// it is <c>true</c> only when the Stage 6 planning block actually ran.
    /// </summary>
    internal sealed class FirstJoinTask : AbstractScoringTask
    {
        public override string Name => @"FirstJoin";

        /// <summary>
        /// Computes the Stage 5 first-join (Percolator first-pass FDR + Stage 6
        /// planning) in straight-through, --task FirstJoin (StopAfterStage5), and
        /// the --input-scores full-pipeline. Excluded in --task PerFileScoring
        /// (stops at Stage 1-4), --task PerFileRescore, and the --task MergeNode
        /// stage (where it rehydrates the bundle rather than recomputing).
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx)
        {
            var c = ctx.Config;
            bool inputs = c.InputScores != null && c.InputScores.Count > 0;
            // The (inputs && StopAfterStage5) clause leans on a CLI-enforced
            // invariant: StopAfterStage5 is set by --task FirstJoin, which
            // requires --input-scores, so StopAfterStage5 implies inputs at
            // parse time -- a --task FirstJoin run can never reach here without
            // InputScores.
            // ProgramTests.TestValidateFirstJoinRequiresInputScores pins that
            // rejection, since the membership truth table (PipelineMembershipTest)
            // does not encode the cross-flag dependency on its own.
            return (!inputs && !c.NoJoin)
                || (inputs && c.StopAfterStage5)
                || (inputs && !c.NoJoin && !c.ExpectReconciledInput);
        }

        // Stage 5/6 planning byproducts this task publishes. The same four types
        // are published from Run (Stage-5 computed values) and from the
        // bundle-adopt Rehydrate path -- publishing into one typed slot from
        // both producers is what dissolves the former dual-source getters
        // (_didPlan ? computed : bundle.X), since a consumer reads the slot
        // without caring which path filled it.
        public override IEnumerable<Type> Publishes => new[]
        {
            typeof(PerFileConsensusTargets), typeof(ReconciliationActions),
            typeof(RefinedCalibrations), typeof(PerFileGapFillForRescore),
            typeof(CompactedEntries), typeof(PlanningPerformed)
        };

        // Stage 6 planning state. Set by PlanStage6 (Run) and published into the
        // typed byproduct slots that downstream consumers pull via ctx.Get<T>();
        // the bundle-adopt Rehydrate path publishes the same slots from the
        // worker bundle instead. _didPlan feeds the published PlanningPerformed
        // slot -- the gate PerFileRescore's self-gate reads to tell "planning
        // ran" from "planning was skipped." Defaults are non-null empty
        // collections so a published slot from a no-op / stopped-after-Stage-5
        // run is never null.
        private bool _didPlan;
        private IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> _perFileConsensusTargets
            = new Dictionary<string, IReadOnlyList<(int, double, double, double)>>();
        private IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> _reconciliationActions
            = new Dictionary<(string, int), ReconcileAction>();
        private IReadOnlyDictionary<string, RTCalibration> _refinedCalibrations
            = new Dictionary<string, RTCalibration>();
        private IReadOnlyDictionary<string, List<GapFillTarget>> _perFileGapFillForRescore
            = new Dictionary<string, List<GapFillTarget>>();

        // Bundle.PerFileConsensusTargets is null at hydration time (consensus
        // is meaningful only post-compaction); compute on demand from the
        // post-compaction stub list. Matches the worker's RunWorker-side
        // multi-charge selection so the worker entry-path collapse keeps
        // identical consensus output regardless of which producer task
        // owned the hydration. Takes the already-resolved bundle so it serves
        // both the worker-published bundle and the straight-through-resume
        // bundle this task builds from its own sidecars.
        private IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>>
            ConsensusTargetsFromBundle(PipelineContext ctx, RescoreInputs bundle)
        {
            if (bundle == null) return _perFileConsensusTargets;
            if (bundle.PerFileConsensusTargets != null) return bundle.PerFileConsensusTargets;
            var computed = new Dictionary<string,
                IReadOnlyList<(int Index, double Apex, double Start, double End)>>();
            foreach (var kvp in bundle.PerFileEntries)
            {
                computed[kvp.Key] =
                    MultiChargeConsensus.SelectRescoreTargets(kvp.Value, ctx.Config.RunFdr);
            }
            bundle.PerFileConsensusTargets = computed;
            return computed;
        }

        // Phase B resume surface. Reads each file's .scores.parquet,
        // writes the .1st-pass.fdr_scores.bin sidecars and the
        // .reconciliation.json envelopes (the latter only when
        // reconciliation is enabled and we have multi-file evidence).
        // ValidityKey adds the reconciliation parameter hash so that
        // toggling reconciliation off/on between runs invalidates the
        // prior outputs.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.GetScoresPath(input);
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
            {
                yield return FdrScoresSidecar.Pass1Path(input);
                if (ctx.Config.Reconciliation != null && ctx.Config.Reconciliation.Enabled)
                    yield return ReconciliationFile.PathForInput(input);
            }
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash();
        }

        public override bool Run(PipelineContext ctx)
        {
            // Compute path (Stage 5 first-pass FDR + Stage 6 planning): the
            // upstream PerFileScoring task did NOT hydrate a rescore bundle, so
            // this run owns the full first-join work. The bundle-present
            // disk-load counterpart lives in Rehydrate. The driver reaches this
            // task here only in the bundle-absent modes (straight-through,
            // --task FirstJoin); a worker-mode consumer materializes it via
            // ctx.Demand which routes to Rehydrate.
            var config = ctx.Config;

            // Mid-Run crash safety: clear stale sidecars for the outputs
            // this task is about to produce. A crash before the matching
            // post-Run sidecar write leaves no false-positive sidecar
            // claiming the partially-written output is valid.
            foreach (var output in Outputs(ctx))
                TaskValiditySidecar.Delete(output, Name);

            // ScoredEntries (pre-compaction) -- this task is the one that
            // compacts the shared buffer below, so it reads it before that.
            var perFileEntries = ctx.Get<ScoredEntries>().Value;
            var perFileCalibrations = ctx.Get<PerFileCalibrations>().Value;
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            var fullLibrary = ctx.Get<FullLibrary>().Value;

            // Stage 5: First-pass FDR.
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"Running {0} FDR control on coelution results...",
                config.FdrMethod));

            var swFdr = Stopwatch.StartNew();
            RunFdr(perFileEntries, fullLibrary, config, ctx);
            swFdr.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] Percolator/Simple FDR: {0:F1}s",
                swFdr.Elapsed.TotalSeconds));

            LogFirstPassResultsAndDump(perFileEntries, config, ctx);

            // First-pass protein FDR: runs on the full pre-compaction
            // peptide pool so target and decoy proteins compete on a
            // symmetric set. Sets RunProteinQvalue on every FdrEntry,
            // which Stage 6 reconciliation reads via the protein-rescue
            // gate in ConsensusRts.Compute. Mirrors Rust pipeline.rs:3029
            // ("First-pass protein FDR").
            if (config.ProteinFdr.HasValue && perFileEntries.Count > 0)
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(@"First-pass protein FDR");
                var swFirstPassProtein = Stopwatch.StartNew();
                RunFirstPassProteinFdr(perFileEntries, fullLibrary, config, ctx);
                swFirstPassProtein.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] First-pass protein FDR: {0:F1}s",
                    swFirstPassProtein.Elapsed.TotalSeconds));
            }

            // Persist the per-file `.1st-pass.fdr_scores.bin` sidecars
            // BEFORE compaction so every stub (passing or not) carries
            // its q-values into the file. Mirrors osprey/src/pipeline.rs
            // around persist_fdr_scores at line ~3180. Stage 6 workers
            // re-derive the post-compaction set by applying the q-value
            // threshold themselves, so they need every entry's q-values
            // -- not just the survivors.
            int fdrSidecarFailures = WriteFdrScoresSidecars(
                perFileEntries, perFileParquetPaths, config, ctx);
            if (fdrSidecarFailures > 0 && config.StopAfterStage5)
            {
                ctx.LogError(string.Format(
                    @"--task FirstJoin: {0}/{1} 1st-pass fdr_scores.bin sidecar " +
                    @"writes failed; boundary file pair is incomplete. See warnings above.",
                    fdrSidecarFailures, perFileEntries.Count));
                ctx.ExitCode = 1;
                return false;
            }

            // Compaction: drop entries whose base_id (entry_id with the
            // decoy bit masked off) does not pass either the peptide-q
            // or protein-q gate. Target and paired decoy share base_id
            // and are kept or dropped together. Mirrors Rust
            // pipeline.rs:3094-3132. Without this, Stage 6 multi-charge
            // consensus selection groups by modified_sequence and
            // includes non-passing charge states that Rust has already
            // dropped, producing different rescore-target sets and
            // different per-file Vec positions.
            CompactFirstPass(perFileEntries, null, config, ctx);

            // NOTE: no 2nd-pass FDR sidecar overlay here. Stage 7
            // (MergeNodeTask) owns its own 2nd-pass rehydrate -- it reloads (or
            // recomputes) the 2nd-pass scores onto the shared entry buffer
            // before protein FDR and the blib write. A former overlay at this
            // point was redundant (its result was overwritten by MergeNode's,
            // and nothing between here and Stage 7 consumes it -- Stage 6 is a
            // no-op once a 2nd-pass sidecar exists) and forced Stage 5 to reach
            // forward into MergeNodeTask for its validity key. Removed so Stage 5
            // holds no knowledge of what runs after it.

            // Stage 6: planning checkpoint -- multi-charge consensus +
            // consensus RTs + per-file calibration refit + reconciliation
            // planning. Produces the inputs PerFileRescoreTask consumes.
            // Runs even single-file -- multi-charge consensus + the planning
            // checkpoint must still execute to match Rust; cross-run
            // reconciliation degenerates to zero actions there.
            if (perFileEntries.Count >= 1 && config.Reconciliation.Enabled)
            {
                if (!PlanStage6(perFileEntries, perFileCalibrations,
                        perFileParquetPaths, fullLibrary, config, ctx))
                    return false;
            }

            // Publish the Stage 6 planning byproducts (computed values, or the
            // empty defaults when PlanStage6 was skipped / stopped after Stage
            // 5), plus the CompactedEntries milestone of the shared buffer that
            // CompactFirstPass produced above. Getters still serve existing
            // consumers in this commit.
            ctx.Publish(new PerFileConsensusTargets(_perFileConsensusTargets));
            ctx.Publish(new ReconciliationActions(_reconciliationActions));
            ctx.Publish(new RefinedCalibrations(_refinedCalibrations));
            ctx.Publish(new PerFileGapFillForRescore(_perFileGapFillForRescore));
            ctx.Publish(new CompactedEntries(perFileEntries));
            // PlanStage6 (above) sets _didPlan only when the planning block ran;
            // publish it so PerFileRescore reads the gate from the registry
            // instead of reaching for this concrete task.
            ctx.Publish(new PlanningPerformed(_didPlan));
            return true;
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            // Disk-load path: the Stage 5 SVM scores + q-values, first-pass
            // protein FDR, and Stage 6 planning state already exist on disk in
            // the boundary sidecars (.1st-pass.fdr_scores.bin +
            // .reconciliation.json) a prior straight-through run wrote.
            // Re-running any of them here would re-train SVMs / re-plan on
            // identical inputs and drift vs the sidecars (mirrors Rust's
            // compute_fdr_from_stubs skip, pipeline.rs:3916). All that remains
            // is to adopt a post-Stage-5 bundle and compact. The compute
            // counterpart is Run.
            var config = ctx.Config;
            var perFileEntries = ctx.Get<ScoredEntries>().Value;

            // The bundle to adopt. In worker mode the upstream PerFileScoring
            // task hydrated it from sibling sidecars and published it. On a
            // straight-through resume it published null (no bundle): the driver
            // skipped THIS task's Run because its own 1st-pass + reconciliation
            // sidecars were already valid on disk (CanRehydrate) and a
            // downstream task is the first to touch its state. Build the
            // equivalent bundle here from those own outputs rather than
            // deferring to Run -- so a lazy Demand loads, never computes, and
            // Run stays outer-loop-only.
            var bundle = ctx.Get<RescoreBundle>().Value;
            if (bundle == null)
            {
                bundle = LoadOwnReconciliationBundle(ctx, perFileEntries);
                if (bundle == null)
                    return false;  // load failure; ExitCode already set
            }

            ctx.LogInfo(@"Bundle hydration: skipping first-pass Percolator (sidecar provides q-values).");

            LogFirstPassResultsAndDump(perFileEntries, config, ctx);

            // Compaction delegates to RescoreCompaction.Apply on the bundle
            // path so the pre-compaction (file, vec_idx) keys in
            // bundle.ReconciliationActions get rebuilt to post-compaction
            // indices for PerFileRescoreTask.
            CompactFirstPass(perFileEntries, bundle, config, ctx);

            // Publish the SAME four planning byproducts as Run, but sourced from
            // the adopted bundle (post-compaction). A consumer pulls
            // ctx.Get<ReconciliationActions>() etc. without knowing whether this
            // task computed them (Run), adopted them from the worker bundle, or
            // rebuilt them from its own sidecars (straight-through resume) --
            // the dual-source getter fallback collapses into one slot.
            ctx.Publish(new ReconciliationActions(bundle.ReconciliationActions));
            ctx.Publish(new RefinedCalibrations(bundle.RefinedCalibrations));
            ctx.Publish(new PerFileGapFillForRescore(bundle.PerFileGapFill));
            ctx.Publish(new PerFileConsensusTargets(ConsensusTargetsFromBundle(ctx, bundle)));
            ctx.Publish(new CompactedEntries(perFileEntries));
            // The bundle-adopt / resume path never plans, so the rescore gate is
            // false (PerFileRescore falls back to the no-op unless a worker
            // RescoreBundle is present). Mirrors the old "FirstJoin rehydrates ->
            // DidPlan is false" semantics, now as a published slot.
            ctx.Publish(new PlanningPerformed(false));
            return true;
        }

        /// <summary>
        /// Build the post-Stage-5 rescore bundle from THIS task's own
        /// <c>.1st-pass.fdr_scores.bin</c> + <c>.reconciliation.json</c> sidecars
        /// for a straight-through resume, where the driver skipped
        /// <see cref="Run"/> because those outputs were already valid on disk
        /// (<see cref="PipelineContext.CanRehydrate"/>) and a downstream task is
        /// the first to touch this task's state. Overlays the first-pass q-values
        /// onto the shared stubs and parses the reconciliation envelopes -- the
        /// same bundle PerFileScoring's worker-mode hydration produces, but owned
        /// here against this task's own outputs. Returns <c>null</c> (with
        /// <see cref="PipelineContext.ExitCode"/> set) on a load failure; because
        /// CanRehydrate gated the sidecars as valid, that is a genuine fault, not
        /// a "recompute instead" case. Clears PIN features on the overlaid stubs,
        /// exactly as the worker hydration does, so PerFileRescore's "Features !=
        /// null means rescored" parquet criterion and MergeNode's feature reload
        /// stay correct.
        /// </summary>
        private RescoreInputs LoadOwnReconciliationBundle(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            // Resolve each loaded file's own .scores.parquet path in
            // perFileEntries order so HydrateReconciliationOverlay's
            // index-correspondence contract (entries[i] <-> parquetPaths[i])
            // holds; PerFileScoring published these paths as PerFileParquetPaths.
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            var parquetPaths = new List<string>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                if (!perFileParquetPaths.TryGetValue(kvp.Key, out var path))
                {
                    ctx.LogError(string.Format(
                        @"Resume rehydrate: no scores parquet path published for {0}", kvp.Key));
                    ctx.ExitCode = 1;
                    return null;
                }
                parquetPaths.Add(path);
            }

            RescoreInputs bundle;
            try
            {
                bundle = RescoreHydration.HydrateReconciliationOverlay(perFileEntries, parquetPaths);
            }
            catch (InvalidDataException ex)
            {
                ctx.LogError(string.Format(
                    @"Resume rehydrate: failed to hydrate reconciliation bundle from own sidecars: {0}",
                    ex.Message));
                ctx.ExitCode = 1;
                return null;
            }

            // Clear PIN features on the overlaid stubs so PerFileRescore's
            // "Features != null means this entry was rescored" parquet criterion
            // stays correct and MergeNode reloads features from the reconciled
            // parquet -- mirrors PerFileScoringTask.HydrateRescoreBundleIfPresent.
            foreach (var kvp in perFileEntries)
                foreach (var entry in kvp.Value)
                    entry.Features = null;

            return bundle;
        }

        /// <summary>
        /// Shared Stage 5 reporting for <see cref="Run"/> and
        /// <see cref="Rehydrate"/>: log per-file first-pass passing counts,
        /// then the diagnostic Percolator dump (gated by
        /// OSPREY_DUMP_PERCOLATOR; written before compaction drops rows so the
        /// cross-impl diff sees both targets and decoys) and the
        /// OSPREY_PERCOLATOR_ONLY measurement exit.
        /// </summary>
        private void LogFirstPassResultsAndDump(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx)
        {
            LogFirstPassResults(perFileEntries, config, ctx);

            if (OspreyDiagnostics.DumpPercolator)
                OspreyDiagnostics.WriteStage5PercolatorDump(perFileEntries);
            if (OspreyDiagnostics.PercolatorOnly)
                OspreyDiagnostics.ExitAfterDump(@"OSPREY_PERCOLATOR_ONLY");
        }

        /// <summary>
        /// Log per-file and total first-pass passing-target counts at the
        /// configured run-level FDR.
        /// </summary>
        private void LogFirstPassResults(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx)
        {
            int passingTargets = 0;
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0;
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        fileTargets++;
                    }
                }
                ctx.LogInfo(string.Format(@"  {0}: {1} precursors at {2:P1} run-level FDR",
                    kvp.Key, fileTargets, config.RunFdr));
                passingTargets += fileTargets;
            }
            ctx.LogInfo(string.Format(@"Total: {0} precursors pass run-level FDR across all files",
                passingTargets));
        }

        /// <summary>
        /// First-pass compaction: drop entries whose base_id (entry_id with
        /// the decoy bit masked off) does not pass either the peptide-q or
        /// protein-q gate. Target and paired decoy share base_id and are
        /// kept or dropped together. On the bundle path, delegates to
        /// RescoreCompaction.Apply so the pre-compaction (file, vec_idx)
        /// keys in bundle.ReconciliationActions get rebuilt to
        /// post-compaction indices. Mirrors Rust pipeline.rs:3094-3132.
        /// </summary>
        private void CompactFirstPass(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            RescoreInputs bundle,
            OspreyConfig config,
            PipelineContext ctx)
        {
            if (perFileEntries.Count > 0)
            {
                if (bundle != null)
                {
                    // Bundle path: delegate to RescoreCompaction.Apply so
                    // the pre-compaction (file, vec_idx) keys in
                    // bundle.ReconciliationActions get rebuilt to
                    // post-compaction indices. Without this rebuild,
                    // PerFileRescoreTask's ExecuteRescore would look up
                    // reconcile actions at stale indices and overlay
                    // boundaries onto the wrong entries -- the exact
                    // failure the worker's hand-rolled compaction at
                    // RescoreCompaction.Apply was written to avoid.
                    var stats = RescoreCompaction.Apply(bundle, config);
                    ctx.LogInfo(string.Format(
                        @"First-pass compaction: {0} -> {1} entries ({2} passing base_ids; {3} action(s) dropped)",
                        stats.EntriesBefore, stats.EntriesAfter,
                        stats.FirstPassBaseIds, stats.DroppedActions));
                }
                else
                {
                    var firstPassBaseIds = new HashSet<uint>();
                    double peptideGate = config.RunFdr;
                    double proteinGate = config.ProteinFdr ?? 0.0;
                    foreach (var kvp in perFileEntries)
                    {
                        foreach (var entry in kvp.Value)
                        {
                            if (entry.IsDecoy)
                                continue;
                            if (entry.RunPeptideQvalue <= peptideGate ||
                                (proteinGate > 0.0 && entry.RunProteinQvalue <= proteinGate))
                            {
                                firstPassBaseIds.Add(entry.EntryId & BASE_ID_MASK);
                            }
                        }
                    }
                    int beforeCount = 0, afterCount = 0;
                    foreach (var kvp in perFileEntries)
                    {
                        beforeCount += kvp.Value.Count;
                        kvp.Value.RemoveAll(e => !firstPassBaseIds.Contains(e.EntryId & BASE_ID_MASK));
                        kvp.Value.TrimExcess();
                        afterCount += kvp.Value.Count;
                    }
                    ctx.LogInfo(string.Format(
                        @"First-pass compaction: {0} -> {1} entries ({2} passing base_ids)",
                        beforeCount, afterCount, firstPassBaseIds.Count));
                }
            }
        }

        /// <summary>
        /// Stage 6 planning checkpoint: multi-charge consensus per file,
        /// cross-run consensus RTs, per-file calibration refit, and
        /// reconciliation planning, then write the per-file
        /// .reconciliation.json envelopes. On success sets the
        /// <see cref="_didPlan"/> output fields the next task
        /// (PerFileRescoreTask) consumes and returns true. Returns false
        /// (with <see cref="PipelineContext.ExitCode"/> set) on the
        /// --task FirstJoin StopAfterStage5 exit paths.
        /// Mirrors pipeline.rs Stage 6 entry
        /// block at lines 3208-3273. The caller gates this on
        /// bundle == null (Stage 6 state already exists upstream on the
        /// bundle path) + Reconciliation.Enabled.
        /// </summary>
        private bool PlanStage6(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(@"Stage 6: planning");

            // 1. Multi-charge consensus per file (independent — runs
            //    first per Rust pipeline.rs:3217, before consensus
            //    RT computation).
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
            ctx.LogInfo(string.Format(
                @"Stage 6 multi-charge consensus: {0} entries need re-scoring across {1} files",
                totalMulticharge, perFileEntries.Count));

            if (OspreyDiagnostics.DumpMulticharge)
            {
                var perFileForDump = new List<KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
                foreach (var kvp in perFileEntries)
                {
                    perFileForDump.Add(new KeyValuePair<string,
                        IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
                }
                OspreyDiagnostics.WriteStage6MultichargeDump(
                    perFileForDump, perFileConsensusTargets);
                if (OspreyDiagnostics.MultichargeOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_MULTICHARGE_ONLY");
            }

            // 2. Cross-run consensus RTs (target peptides + paired
            //    decoys, sigmoid(score)-weighted median, hard
            //    run_precursor_qvalue gate).
            var perFileForRecon = new List<KeyValuePair<string,
                IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                perFileForRecon.Add(new KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
            }
            // Cross-impl bisection trace for InversePredict: if the
            // OSPREY_DUMP_INV_PREDICT env var is set, ConsensusRts
            // populates this list with one row per detection. The
            // caller drives the dump via OspreyDiagnostics so the
            // FDR project doesn't have to know about the diagnostic
            // file format.
            List<InvPredictRecord> invPredictTrace = null;
            if (OspreyDiagnostics.DumpInvPredict)
                invPredictTrace = new List<InvPredictRecord>();

            // Cross-file consensus is only meaningful with > 1 file.
            // Mirrors Rust pipeline.rs:4146 where reconciliation_enabled
            // requires per_file_entries.len() > 1 — single-file runs
            // skip consensus computation, refit, and reconciliation
            // entirely, leaving multi-charge consensus rescore as the
            // only Stage 6 work performed.
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
                OspreyDiagnostics.WriteStage6InvPredictDump(invPredictTrace);
                if (OspreyDiagnostics.InvPredictOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_INV_PREDICT_ONLY");
            }
            int nTargets = 0, nDecoys = 0;
            foreach (var c in consensus)
            {
                if (c.IsDecoy) nDecoys++;
                else nTargets++;
            }
            ctx.LogInfo(string.Format(
                @"Stage 6 consensus: {0} target peptides, {1} decoy peptides",
                nTargets, nDecoys));

            // Skip the dump on empty consensus to match Rust's
            // dump_stage6_consensus, which silently elides the
            // file when there is nothing to write (the dump is
            // gated on Some(file) in Rust, derived from
            // !consensus.is_empty()). Without this gate, C#
            // emits a header-only cs_stage6_consensus.tsv and
            // Test-Regression sees an asymmetric-absence FAIL
            // even though both sides agree on the empty result.
            if (OspreyDiagnostics.DumpConsensus && consensus.Count > 0)
            {
                OspreyDiagnostics.WriteStage6ConsensusDump(consensus);
                if (OspreyDiagnostics.ConsensusOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_CONSENSUS_ONLY");
            }

            // 3. Per-file calibration refit on consensus peptides.
            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            foreach (var kvp in perFileEntries)
            {
                var refined = CalibrationRefit.Refit(consensus, kvp.Value,
                    config.Reconciliation.ConsensusFdr);
                if (refined != null)
                    refinedCalibrations[kvp.Key] = refined;
            }
            ctx.LogInfo(string.Format(
                @"Stage 6 refit: {0}/{1} files produced refined calibrations",
                refinedCalibrations.Count, perFileEntries.Count));

            if (OspreyDiagnostics.DumpLoessFit)
            {
                OspreyDiagnostics.WriteStage6LoessFitDump(refinedCalibrations);
                if (OspreyDiagnostics.LoessFitOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_LOESS_FIT_ONLY");
            }

            if (OspreyDiagnostics.DumpRefit)
            {
                OspreyDiagnostics.WriteStage6RefitDump(refinedCalibrations);
                if (OspreyDiagnostics.RefitOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_REFIT_ONLY");
            }

            // 4. Reconciliation planning. Reads each file's CWT
            //    candidates from the parquet cache and asks the
            //    planner to choose, per (file, entry), whether to
            //    keep the existing peak, switch to a stored CWT
            //    candidate at the consensus RT, or force an
            //    integration window. Mirrors Rust pipeline.rs
            //    reconciliation block at ~3260-3380.
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> reconciliationActions = null;
            var perFileCwtCandidates = new Dictionary<string,
                IReadOnlyList<IReadOnlyList<CwtCandidate>>>();
            foreach (var kvp in perFileEntries)
            {
                if (perFileParquetPaths.TryGetValue(kvp.Key, out string parquetPath) &&
                    File.Exists(parquetPath))
                {
                    try
                    {
                        var cwtRows = ParquetScoreCache
                            .LoadCwtCandidatesFromParquet(parquetPath);
                        // The planner indexes CWT lists by
                        // entry.ParquetIndex (mirrors Rust at
                        // reconciliation.rs:672). cwtRows.Count is
                        // the parquet's raw Stage-4 row count;
                        // kvp.Value.Count is the post-first-pass-
                        // compaction stub count. They are not
                        // equal by design — what we actually need
                        // to validate is that every stub's
                        // ParquetIndex falls within cwtRows.
                        uint maxIdx = 0;
                        foreach (var entry in kvp.Value)
                        {
                            if (entry.ParquetIndex > maxIdx)
                                maxIdx = entry.ParquetIndex;
                        }
                        if (kvp.Value.Count > 0 && maxIdx >= cwtRows.Count)
                        {
                            ctx.LogWarning(string.Format(
                                @"CWT candidate row count out of range for {0}: " +
                                @"max stub ParquetIndex={1}, parquet has {2} rows -- " +
                                @"skipping reconciliation planning for this file",
                                kvp.Key, maxIdx, cwtRows.Count));
                            continue;
                        }
                        var converted = new List<IReadOnlyList<CwtCandidate>>(cwtRows.Count);
                        foreach (var row in cwtRows)
                            converted.Add(row);
                        perFileCwtCandidates[kvp.Key] = converted;
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(
                            @"Failed to load CWT candidates for {0}: {1}",
                            kvp.Key, ex.Message));
                    }
                }
            }
            var perFileForPlan = new List<KeyValuePair<string,
                IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                perFileForPlan.Add(new KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
            }
            // Match Rust pipeline.rs:4223: only run the reconciliation
            // planner when the cross-file consensus is non-empty.
            // Single-file runs (or any case where no peptide had
            // enough cross-replicate evidence to form a consensus
            // RT) degenerate to zero reconciliation actions in
            // Rust; C# previously planned regardless and produced
            // ~22k spurious use_cwt actions on Stellar single-file.
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
                ctx.LogInfo(string.Format(
                    @"Stage 6 reconciliation: {0} per-(file, entry) actions planned",
                    reconciliationActions.Count));
            }
            else if (consensus.Count == 0)
            {
                ctx.LogInfo(@"Stage 6 reconciliation: skipped (empty consensus; single-file or no cross-file evidence)");
            }
            else
            {
                ctx.LogInfo(string.Format(
                    @"Stage 6 reconciliation: skipped (CWT candidates loaded for {0}/{1} files)",
                    perFileCwtCandidates.Count, perFileEntries.Count));
            }

            // Stage 6 cross-impl bisection dump for the planner output.
            // Fires unconditionally when OSPREY_DUMP_RECONCILIATION=1
            // is set so the skipped / empty paths still produce a
            // header-only TSV and still honor OSPREY_RECONCILIATION_ONLY
            // for early exit. Mirrors the Rust side at
            // crates/osprey/src/pipeline.rs after the reconciliation
            // block closes.
            if (OspreyDiagnostics.DumpReconciliation)
            {
                var dumpActions = reconciliationActions
                    ?? new Dictionary<(string File, int Index), ReconcileAction>();
                OspreyDiagnostics.WriteStage6ReconciliationDump(
                    dumpActions, perFileForPlan);
                if (OspreyDiagnostics.ReconciliationOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_RECONCILIATION_ONLY");
            }

            // Stage 5 → Stage 6 boundary: write the per-file
            // .reconciliation.json envelope (the .fdr_scores.bin
            // companion was already written above pre-compaction).
            // Pairs with the --task PerFileRescore Stage 6
            // worker mode (next sprint).
            //
            // Surfaces gap-fill targets via out param so the in-
            // process Stage 6 rescore call below can execute them.
            int reconWriteFailures = WriteReconciliationFiles(
                perFileEntries,
                reconciliationActions,
                consensus,
                refinedCalibrations,
                perFileCalibrations,
                fullLibrary,
                perFileParquetPaths,
                config,
                out var perFileGapFillForRescore,
                ctx);

            if (config.StopAfterStage5)
            {
                if (reconWriteFailures > 0)
                {
                    ctx.LogError(string.Format(
                        @"--task FirstJoin: {0}/{1} reconciliation.json " +
                        @"writes failed; boundary file pair is incomplete. See warnings above.",
                        reconWriteFailures, perFileEntries.Count));
                    ctx.ExitCode = 1;
                    return false;
                }
                ctx.LogInfo(string.Format(
                    @"--task FirstJoin: Stage 5 + reconciliation planning " +
                    @"complete; wrote {0} reconciliation.json + matching fdr_scores.bin " +
                    @"sidecar pair(s). Exiting before Stage 6 rescore.",
                    perFileEntries.Count));
                // Success: return true (not false). The stop after Stage 5 is now
                // a membership fact -- PerFileRescore and MergeNode are excluded
                // by IsIncluded under --task FirstJoin, so the driver loop iterates no
                // further. The failure path above keeps ExitCode=1; return false.
                ctx.ExitCode = 0;
                return true;
            }

            // Surface outputs for the next task.
            _didPlan = true;
            _perFileConsensusTargets = perFileConsensusTargets;
            _reconciliationActions = reconciliationActions
                ?? new Dictionary<(string, int), ReconcileAction>();
            _refinedCalibrations = refinedCalibrations;
            _perFileGapFillForRescore = perFileGapFillForRescore
                ?? new Dictionary<string, List<GapFillTarget>>();
            return true;
        }

        /// <summary>
        /// Write the per-file <c>.1st-pass.fdr_scores.bin</c> sidecars at
        /// the pre-compaction Stage 5 boundary (every stub, passing or
        /// not, gets persisted with its q-values + SVM score). Mirrors
        /// the persist_fdr_scores call in osprey/src/pipeline.rs at line
        /// ~3180 (immediately after first-pass FDR, before compaction or
        /// protein FDR). Stage 6 workers re-apply the q-value threshold
        /// themselves to derive the post-compaction passing set.
        /// </summary>
        /// <returns>
        /// Number of files for which the sidecar write failed (0 means
        /// success). Callers in <see cref="OspreyConfig.StopAfterStage5"/>
        /// mode treat any failure as fatal — see the StopAfterStage5
        /// block at the end of the reconciliation phase — because the
        /// downstream Stage 6 worker would otherwise be missing a
        /// sidecar.
        /// </returns>
        private int WriteFdrScoresSidecars(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            int failures = 0;
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                string sidecarBase = ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogWarning(string.Format(
                        "No sidecar base path for `{0}` — skipping fdr_scores.bin write", fileName));
                    failures++;
                    continue;
                }
                string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);
                try
                {
                    FdrScoresSidecar.Write(fdrPath, kvp.Value, FdrScoresSidecar.Pass.FirstPass);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to write 1st-pass fdr_scores.bin for {0}: {1}", fileName, ex.Message));
                    failures++;
                }
            }
            return failures;
        }

        /// <summary>
        /// Write the per-file <c>.reconciliation.json</c> envelope for
        /// each input — the second half of the Stage 5 → Stage 6 boundary
        /// (the <c>.fdr_scores.bin</c> companion is written earlier,
        /// pre-compaction). Mirrors the matching block in
        /// osprey/src/pipeline.rs immediately after
        /// dump_stage6_reconciliation. The file is written sibling to
        /// the input mzML (or, in --task FirstJoin mode, the synthetic input
        /// path derived from the parquet stem).
        /// </summary>
        /// <returns>
        /// Number of files for which the reconciliation.json write failed
        /// (0 means success). Callers in
        /// <see cref="OspreyConfig.StopAfterStage5"/> mode treat any
        /// failure as fatal — the Stage 6 worker would otherwise be
        /// missing an envelope and either refuse to start or score the
        /// wrong files.
        /// </returns>
        private int WriteReconciliationFiles(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> reconciliationActions,
            IReadOnlyList<PeptideConsensusRT> consensus,
            Dictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            List<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            out Dictionary<string, List<GapFillTarget>> gapFillByFileOut,
            PipelineContext ctx)
        {
            string searchHash = config.Identity.SearchParameterHash();
            string libraryHash = config.Identity.LibraryIdentityHash();
            var actions = reconciliationActions
                ?? new Dictionary<(string File, int Index), ReconcileAction>();

            // Pre-group reconciliation actions by file name to avoid the
            // O(num_files * num_actions) walk that the previous
            // implementation performed inside BuildReconciliationFile
            // (one full Dictionary traversal per file).
            var actionsByFile = new Dictionary<string, List<KeyValuePair<int, ReconcileAction>>>(
                StringComparer.Ordinal);
            foreach (var kvp in actions)
            {
                if (!actionsByFile.TryGetValue(kvp.Key.File, out var list))
                {
                    list = new List<KeyValuePair<int, ReconcileAction>>();
                    actionsByFile[kvp.Key.File] = list;
                }
                list.Add(new KeyValuePair<int, ReconcileAction>(kvp.Key.Index, kvp.Value));
            }

            // Build the (modified_sequence, charge) → (target_id, decoy_id)
            // and entry_id → precursor_mz lookups from the library. Decoy
            // ID convention: target_id | 0x80000000 (mirrors Rust at
            // pipeline.rs:3330-3340).
            var libLookup = new Dictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)>();
            var libPrecursorMz = new Dictionary<uint, double>();
            foreach (var entry in fullLibrary)
            {
                if (entry.IsDecoy)
                    continue;
                uint decoyId = entry.Id | 0x80000000u;
                libLookup[(entry.ModifiedSequence, entry.Charge)] = (entry.Id, decoyId);
                libPrecursorMz[entry.Id] = entry.PrecursorMz;
            }

            // Compute per-file gap-fill targets. Per-file isolation-window
            // m/z intervals are not yet plumbed through C# (Stellar
            // calibration.json carries no isolation_scheme today, so the
            // filter is a no-op there); when extended to GPF datasets,
            // pass a non-null dictionary here.
            var perFileForGapFill = new List<KeyValuePair<string,
                IReadOnlyList<FdrEntry>>>(perFileEntries.Count);
            foreach (var kvp in perFileEntries)
            {
                perFileForGapFill.Add(new KeyValuePair<string,
                    IReadOnlyList<FdrEntry>>(kvp.Key, kvp.Value));
            }
            var gapFillByFile = GapFillTargetIdentifier.Identify(
                consensus,
                perFileForGapFill,
                refinedCalibrations,
                perFileCalibrations,
                config.Reconciliation.ConsensusFdr,
                libLookup,
                libPrecursorMz,
                perFileIsolationMz: null);

            // Mirror gapFillByFile into the out param for the in-process
            // Stage 6 rescore caller. Identify returns IReadOnlyList<>;
            // ExecuteStage6Rescore's perFileGapFill type is
            // IReadOnlyDictionary<string, List<GapFillTarget>>, so build
            // a List<> per file. The conversion is per-file (3-15 files
            // typical), each list 100-3000 GapFillTarget structs.
            gapFillByFileOut = new Dictionary<string, List<GapFillTarget>>(
                gapFillByFile.Count, StringComparer.Ordinal);
            foreach (var kvp in gapFillByFile)
            {
                var copy = new List<GapFillTarget>(kvp.Value.Count);
                foreach (var g in kvp.Value)
                    copy.Add(g);
                gapFillByFileOut[kvp.Key] = copy;
            }

            // The multi-file stems set goes into every per-file
            // reconciliation.json so a worker rescoring its single
            // parquet can compute the join-wide reconciliation hash that
            // --task MergeNode will validate against. Sort + dedup once;
            // BuildReconciliationFile copies the list into the wire form.
            var joinFileStems = new List<string>(perFileEntries.Count);
            foreach (var fEntry in perFileEntries)
            {
                if (!string.IsNullOrEmpty(fEntry.Key))
                    joinFileStems.Add(fEntry.Key);
            }
            joinFileStems.Sort(StringComparer.Ordinal);
            for (int i = joinFileStems.Count - 1; i > 0; i--)
            {
                if (string.Equals(joinFileStems[i], joinFileStems[i - 1], StringComparison.Ordinal))
                    joinFileStems.RemoveAt(i);
            }

            int failures = 0;
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                var fileEntries = kvp.Value;

                string sidecarBase = ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogWarning(string.Format(
                        "No sidecar base path for `{0}` — skipping reconciliation.json write", fileName));
                    failures++;
                    continue;
                }
                string reconPath = ReconciliationFile.PathForInput(sidecarBase);
                IReadOnlyList<GapFillTarget> fileGapFill;
                if (!gapFillByFile.TryGetValue(fileName, out fileGapFill))
                    fileGapFill = Array.Empty<GapFillTarget>();
                actionsByFile.TryGetValue(fileName, out var fileActions);
                var reconFile = BuildReconciliationFile(
                    fileEntries, fileActions, fileGapFill,
                    refinedCalibrations.TryGetValue(fileName, out var fileCal) ? fileCal : null,
                    searchHash, libraryHash, joinFileStems);
                try
                {
                    ReconciliationFile.Save(reconPath, reconFile);
                    ctx.LogInfo(string.Format(
                        "Wrote reconciliation.json for {0} ({1} use_cwt + {2} forced + {3} gap-fill)",
                        fileName,
                        reconFile.UseCwtPeakActions.Count,
                        reconFile.ForcedIntegrationActions.Count,
                        reconFile.GapFillTargets.Count));
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to write reconciliation.json for {0}: {1}", fileName, ex.Message));
                    failures++;
                }
            }
            return failures;
        }

        /// <summary>
        /// Resolve a path whose stem matches <paramref name="fileName"/>, used
        /// only as the base for sidecar file naming (the path itself need
        /// not exist). In normal mode this is the input mzML; in
        /// --task FirstJoin mode where InputFiles is empty we synthesize the
        /// path from the matching .scores.parquet by replacing the
        /// `.scores.parquet` suffix with `.mzML`. Mirrors the Rust
        /// `synthetic_input_from_parquet` helper.
        /// </summary>
        private static string ResolveSidecarBasePath(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            // Normal mode: prefer the actual input mzML path so sidecars
            // land next to the source mzML.
            if (config.InputFiles != null)
            {
                foreach (string inputPath in config.InputFiles)
                {
                    if (string.Equals(
                        Path.GetFileNameWithoutExtension(inputPath),
                        fileName,
                        StringComparison.Ordinal))
                    {
                        return inputPath;
                    }
                }
            }
            // --task FirstJoin fallback: derive a synthetic mzML path from the
            // matching parquet stem so all the existing sidecar path
            // helpers keep working without conditional branches.
            if (perFileParquetPaths != null
                && perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                string parent = Path.GetDirectoryName(parquetPath) ?? ".";
                return Path.Combine(parent, fileName + ".mzML");
            }
            return null;
        }

        /// <summary>
        /// Convert pre-grouped reconciliation actions for one file into
        /// the <see cref="ReconciliationFile"/> wire format: resolve each
        /// Vec index to its entry_id, split non-Keep actions into
        /// homogeneous use_cwt_peak / forced arrays, snapshot the
        /// refined RT calibration if present, and emit the gap-fill
        /// targets for this file (already sorted by
        /// <c>target_entry_id</c> by the identifier). The caller pre-
        /// groups actions by file so the per-file cost stays
        /// O(actions_for_this_file) rather than O(total_actions).
        /// </summary>
        private static ReconciliationFile BuildReconciliationFile(
            IReadOnlyList<FdrEntry> fileEntries,
            IReadOnlyList<KeyValuePair<int, ReconcileAction>> fileActions,
            IReadOnlyList<GapFillTarget> gapFillTargets,
            RTCalibration refinedCalibration,
            string searchHash,
            string libraryHash,
            IReadOnlyList<string> joinFileStems)
        {
            var useCwt = new List<UseCwtPeakEntry>();
            var forced = new List<ForcedIntegrationEntry>();
            if (fileActions != null)
            {
                foreach (var kvp in fileActions)
                {
                    int idx = kvp.Key;
                    if (idx < 0 || idx >= fileEntries.Count)
                        continue;
                    uint entryId = fileEntries[idx].EntryId;
                    var useCwtAction = kvp.Value as ReconcileAction.UseCwtPeak;
                    var forcedAction = kvp.Value as ReconcileAction.ForcedIntegration;
                    if (useCwtAction != null)
                    {
                        useCwt.Add(new UseCwtPeakEntry
                        {
                            ApexRt = useCwtAction.ApexRt,
                            CandidateIdx = (uint)useCwtAction.CandidateIndex,
                            EndRt = useCwtAction.EndRt,
                            EntryId = entryId,
                            StartRt = useCwtAction.StartRt,
                        });
                    }
                    else if (forcedAction != null)
                    {
                        forced.Add(new ForcedIntegrationEntry
                        {
                            EntryId = entryId,
                            ExpectedRt = forcedAction.ExpectedRt,
                            HalfWidth = forcedAction.HalfWidth,
                        });
                    }
                }
            }
            // Sort by entry_id for deterministic output (matches Rust).
            useCwt.Sort((a, b) => a.EntryId.CompareTo(b.EntryId));
            forced.Sort((a, b) => a.EntryId.CompareTo(b.EntryId));

            RefinedRtCalibrationJson refinedJson = null;
            if (refinedCalibration != null)
            {
                refinedJson = new RefinedRtCalibrationJson
                {
                    AbsResiduals = (double[])refinedCalibration.AbsResiduals.Clone(),
                    FittedRts = (double[])refinedCalibration.FittedValues.Clone(),
                    LibraryRts = (double[])refinedCalibration.LibraryRts.Clone(),
                    ResidualSd = refinedCalibration.ResidualSD,
                };
            }

            // Map per-file GapFillTarget records (already sorted by
            // target_entry_id) to the wire form. Field-for-field copy.
            var gap = new List<GapFillEntry>(gapFillTargets?.Count ?? 0);
            if (gapFillTargets != null)
            {
                foreach (var g in gapFillTargets)
                {
                    gap.Add(new GapFillEntry
                    {
                        Charge = g.Charge,
                        DecoyEntryId = g.DecoyEntryId,
                        ExpectedRt = g.ExpectedRt,
                        HalfWidth = g.HalfWidth,
                        ModifiedSequence = g.ModifiedSequence,
                        TargetEntryId = g.TargetEntryId,
                    });
                }
            }

            // Defensive copy so a later caller-side mutation of
            // joinFileStems doesn't leak into the serialized envelope.
            var fileStems = joinFileStems != null
                ? new List<string>(joinFileStems)
                : new List<string>();

            return new ReconciliationFile
            {
                FileStems = fileStems,
                ForcedIntegrationActions = forced,
                FormatVersion = ReconciliationFile.CurrentFormatVersion,
                GapFillTargets = gap,
                LibraryHash = libraryHash,
                RefinedRtCalibration = refinedJson,
                SearchHash = searchHash,
                UseCwtPeakActions = useCwt,
            };
        }

        /// <summary>
        /// Run FDR control using the configured method.
        /// </summary>
        private void RunFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            switch (config.FdrMethod)
            {
                case FdrMethod.Percolator:
                    RunPercolatorFdr(perFileEntries, fullLibrary, config, ctx);
                    break;

                case FdrMethod.Simple:
                    RunSimpleFdr(perFileEntries, config, ctx);
                    break;

                default:
                    ctx.LogWarning(string.Format(
                        "FDR method {0} not yet supported, falling back to simple",
                        config.FdrMethod));
                    RunSimpleFdr(perFileEntries, config, ctx);
                    break;
            }
        }

        /// <summary>
        /// Run Percolator-based FDR control.
        /// Builds PercolatorEntry objects from FdrEntry stubs and runs Percolator.
        /// Static + internal so <see cref="MergeNodeTask"/> can call it for
        /// the 2nd-pass run after Stage 6 reconciliation (the HPC distribution
        /// case where workers wrote reconciled .scores.parquet but no
        /// .2nd-pass.fdr_scores.bin sidecars; mirrors Rust pipeline.rs:4394-4468).
        /// </summary>
        internal static void RunPercolatorFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx,
            string passLabel = "First-pass")
        {
            // Sort each file's entries by EntryId so the SVM working-set
            // selection sees a canonical order regardless of upstream operation
            // history. The 1st-pass input is already entry_id-sorted via
            // DeduplicatePairs (AbstractScoringTask.cs), but the post-rescore
            // pool that feeds 2nd-pass Percolator can have gap-fill entries
            // appended after the sorted pre-existing rows. Re-sorting here
            // guarantees identical iteration order across Rust and OspreySharp;
            // without it, gap-fill ordering diverges and the cross-impl 2nd-pass
            // scores drift on multi-file datasets even when feature columns are
            // bit-equal. Mirrors Rust pipeline.rs::run_percolator_fdr.
            foreach (var kvp in perFileEntries)
            {
                kvp.Value.Sort((a, b) =>
                {
                    int c = a.EntryId.CompareTo(b.EntryId);
                    if (c != 0) return c;
                    c = a.Charge.CompareTo(b.Charge);
                    if (c != 0) return c;
                    c = a.ScanNumber.CompareTo(b.ScanNumber);
                    if (c != 0) return c;
                    return a.ParquetIndex.CompareTo(b.ParquetIndex);
                });
            }

            // Build PercolatorEntry list from all files
            var percEntries = new List<PercolatorEntry>();

            // Build library lookup for feature extraction
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var entry in fullLibrary)
                libraryById[entry.Id] = entry;

            int nWithFeatures = 0;
            int nWithoutFeatures = 0;
            int nInputTargets = 0;
            int nInputDecoys = 0;
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    // Prefer the 21-feature vector computed during coelution scoring.
                    // Fall back to an all-zeros vector only for stub entries (e.g. loaded
                    // from a Parquet cache without features) so the PercolatorEntry is
                    // well-formed.
                    double[] features;
                    if (fdrEntry.Features != null &&
                        fdrEntry.Features.Length == NUM_PIN_FEATURES)
                    {
                        features = fdrEntry.Features;
                        nWithFeatures++;
                    }
                    else
                    {
                        features = BuildBasicFeatures(fdrEntry, libraryById);
                        nWithoutFeatures++;
                    }

                    if (fdrEntry.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    // PSM Id must uniquely identify each observation so the
                    // result -> FdrEntry write-back can score every row
                    // independently. EntryId alone is NOT unique within a
                    // file: a single base_id with multiple scan-time
                    // observations (different scan numbers, same charge,
                    // same modified_sequence) shares one EntryId. Using
                    // "{fileName}_{EntryId}" collided those rows in
                    // resultMap, leaving the last-inserted score
                    // overwriting every same-EntryId observation's
                    // FdrEntry.Score and producing 176-185 score
                    // divergences per file vs. Rust's 4-component psm_id.
                    // Mirrors osprey-fdr/src/percolator.rs:5978-5980.
                    percEntries.Add(new PercolatorEntry
                    {
                        Id = string.Format("{0}_{1}_{2}_{3}",
                            fileName, fdrEntry.ModifiedSequence,
                            fdrEntry.Charge, fdrEntry.ScanNumber),
                        FileName = fileName,
                        Peptide = fdrEntry.ModifiedSequence,
                        Charge = fdrEntry.Charge,
                        IsDecoy = fdrEntry.IsDecoy,
                        EntryId = fdrEntry.EntryId,
                        Features = features
                    });
                }
            }

            ctx.LogInfo(string.Format(
                "[COUNT] {0} Percolator input: {1} entries ({2} targets, {3} decoys, {4} features)",
                passLabel, percEntries.Count, nInputTargets, nInputDecoys, NUM_PIN_FEATURES));
            ctx.LogInfo(string.Format(
                "[COUNT] {0} Percolator features computed: {1} entries with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures));

            ctx.LogInfo(string.Format("Running {0} Percolator on {1} entries...",
                passLabel, percEntries.Count));

            var percConfig = new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3,
                FeatureNames = ParquetScoreCache.PIN_FEATURE_NAMES
            };

            // Streaming vs direct dispatch, matching Rust
            // osprey/src/pipeline.rs::run_percolator_fdr. Above the
            // MaxTrainSize * 2 threshold the training set is dominated by
            // multi-observation-per-precursor redundancy; best-per-precursor
            // dedup + peptide-grouped subsample give the SVM a diverse
            // per-peptide training pool (same approach mokapot takes) and
            // keep the Stage 5 standardizer fit on the subset -- essential
            // for cross-impl byte parity with Rust once Astral-scale inputs
            // push past the threshold.
            PercolatorResults results;
            if (percConfig.MaxTrainSize > 0 &&
                percEntries.Count > percConfig.MaxTrainSize * 2)
            {
                results = RunPercolatorStreaming(percEntries, percConfig, ctx, passLabel);
            }
            else
            {
                results = PercolatorFdr.RunPercolator(percEntries, percConfig);
            }

            // Map results back to FdrEntry stubs
            var resultMap = new Dictionary<string, PercolatorResult>();
            foreach (var result in results.Entries)
                resultMap[result.Id] = result;

            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    // 4-component psm_id matches the construction in
                    // the loop above so each FdrEntry pulls back its
                    // own PercolatorResult. Mirrors Rust direct path.
                    string id = string.Format("{0}_{1}_{2}_{3}",
                        fileName, fdrEntry.ModifiedSequence,
                        fdrEntry.Charge, fdrEntry.ScanNumber);
                    PercolatorResult result;
                    if (resultMap.TryGetValue(id, out result))
                    {
                        fdrEntry.Score = result.Score;
                        fdrEntry.RunPrecursorQvalue = result.RunPrecursorQvalue;
                        fdrEntry.RunPeptideQvalue = result.RunPeptideQvalue;
                        fdrEntry.ExperimentPrecursorQvalue = result.ExperimentPrecursorQvalue;
                        fdrEntry.ExperimentPeptideQvalue = result.ExperimentPeptideQvalue;
                        fdrEntry.Pep = result.Pep;
                    }
                }
            }
            // Log FDR results
            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0;
                int fileDecoys = 0;
                foreach (var entry in kvp.Value)
                {
                    if (entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        if (entry.IsDecoy)
                            fileDecoys++;
                        else
                            fileTargets++;
                    }
                }
                ctx.LogInfo(string.Format(
                    "[COUNT] {0} Percolator pass [{1}]: {2} targets, {3} decoys at {4:P0} FDR",
                    passLabel, kvp.Key, fileTargets, fileDecoys, config.RunFdr));
                nTargetPassing += fileTargets;
                nDecoyPassing += fileDecoys;
            }

            ctx.LogInfo(string.Format(
                "{0} Percolator results: {1} targets, {2} decoys pass {3:P1} FDR",
                passLabel, nTargetPassing, nDecoyPassing, config.RunFdr));
            ctx.LogInfo(string.Format(
                "[COUNT] {0} total across files: {1}",
                passLabel, nTargetPassing));

            // Compute unique precursors across files (best q-value per modseq+charge)
            var bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    if (entry.EffectiveRunQvalue(config.FdrLevel) > config.RunFdr)
                        continue;
                    string pkey = entry.ModifiedSequence + "|" + entry.Charge;
                    double q = entry.EffectiveRunQvalue(config.FdrLevel);
                    double existing;
                    if (!bestQByPrecursor.TryGetValue(pkey, out existing) || q < existing)
                        bestQByPrecursor[pkey] = q;
                }
            }
            ctx.LogInfo(string.Format(
                "[COUNT] {0} unique precursors (best q across files): {1}",
                passLabel, bestQByPrecursor.Count));
        }

        /// <summary>
        /// Build a minimal PIN feature vector from an FdrEntry.
        /// Used as a fallback ONLY when <see cref="FdrEntry.Features"/> has not been
        /// populated (e.g. stubs loaded from a Parquet cache). In normal operation the
        /// 21-feature vector is computed during coelution scoring in
        /// <see cref="pwiz.OspreySharp.Scoring.CoelutionScorer"/> and stored on the entry.
        /// </summary>
        private static double[] BuildBasicFeatures(
            FdrEntry entry, Dictionary<uint, LibraryEntry> libraryById)
        {
            double[] features = new double[NUM_PIN_FEATURES];

            // 0: coelution_sum
            features[0] = entry.CoelutionSum;
            // 1: coelution_max (approximate as coelution_sum for basic version)
            features[1] = entry.CoelutionSum * 0.5;
            // 2: n_coeluting_fragments
            features[2] = 3.0;
            // 3: peak_apex
            features[3] = 0.0;
            // 4: peak_area
            features[4] = 0.0;
            // 5: peak_sharpness
            features[5] = 0.0;
            // 6: xcorr
            features[6] = 0.0;
            // 7: consecutive_ions
            features[7] = 0.0;
            // 8: explained_intensity
            features[8] = 0.0;
            // 9: mass_accuracy_mean
            features[9] = 0.0;
            // 10: abs_mass_accuracy_mean
            features[10] = 0.0;
            // 11: rt_deviation
            features[11] = 0.0;
            // 12: abs_rt_deviation
            features[12] = 0.0;
            // 13: ms1_precursor_coelution
            features[13] = 0.0;
            // 14: ms1_isotope_cosine
            features[14] = 0.0;
            // 15: median_polish_cosine
            features[15] = 0.0;
            // 16: median_polish_residual_ratio
            features[16] = 0.0;
            // 17: sg_weighted_xcorr
            features[17] = 0.0;
            // 18: sg_weighted_cosine
            features[18] = 0.0;
            // 19: median_polish_min_fragment_r2
            features[19] = 0.0;
            // 20: median_polish_residual_correlation
            features[20] = 0.0;

            return features;
        }

        /// <summary>
        /// Streaming Percolator dispatch for multi-observation-per-precursor
        /// inputs (total entries above <c>MaxTrainSize * 2</c>). Mirrors
        /// Rust's <c>run_percolator_fdr</c> streaming branch
        /// (osprey/src/pipeline.rs:4232-4580):
        /// <list type="number">
        /// <item>Best-per-precursor dedup across all per-file entries (one
        /// target + one decoy per base_id, by Features[0] = coelution_sum).
        /// </item>
        /// <item>Peptide-grouped subsample to <c>MaxTrainSize</c> using the
        /// same XOR-shift RNG and peptide-key sort order as Rust.</item>
        /// <item>Train fold models + standardizer on that subset
        /// (<c>TrainOnly = true</c>) -- the standardizer is fit on the
        /// subset, matching Rust's run_percolator-on-subset behaviour
        /// instead of fitting on the full 1M+ observation pool.</item>
        /// <item>Apply the averaged model to ALL entries for scoring, then
        /// compute PEP and per-run / experiment q-values on that flat
        /// score array.</item>
        /// </list>
        /// The selection uses <see cref="PercolatorFdr.SelectBestPerPrecursor"/>
        /// and <see cref="PercolatorFdr.SubsampleByPeptideGroup"/> -- the
        /// same helpers the direct path calls internally, so both paths
        /// select identical 300K subsets when given identical input.
        /// </summary>
        private static PercolatorResults RunPercolatorStreaming(
            List<PercolatorEntry> percEntries,
            PercolatorConfig percConfig,
            PipelineContext ctx,
            string passLabel)
        {
            int n = percEntries.Count;
            int maxTrain = percConfig.MaxTrainSize;

            // Pull labels / entry IDs / peptides into flat arrays for the
            // subset helpers.
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            for (int i = 0; i < n; i++)
            {
                labels[i] = percEntries[i].IsDecoy;
                entryIds[i] = percEntries[i].EntryId;
                peptides[i] = percEntries[i].Peptide;
            }

            // 1. Best-per-precursor dedup, then 2. peptide-grouped subsample when
            //    the dedup count still exceeds MaxTrainSize. Both steps are owned
            //    by PercolatorFdr.BuildTrainingSubset so this streaming path and
            //    the direct path select identical subsets for identical input.
            int[] bestIdx;
            int[] trainSubsetGlobalIdx = PercolatorFdr.BuildTrainingSubset(
                labels, entryIds, peptides, percEntries, maxTrain, percConfig.Seed,
                out bestIdx);
            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestIdx.Length; i++)
            {
                if (labels[bestIdx[i]]) dedupDecoys++;
                else dedupTargets++;
            }
            ctx.LogInfo(string.Format(
                "[COUNT] {0} Percolator streaming best-per-precursor: {1} entries ({2} targets, {3} decoys) from {4} total",
                passLabel, bestIdx.Length, dedupTargets, dedupDecoys, n));

            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubsetGlobalIdx.Length; i++)
            {
                if (labels[trainSubsetGlobalIdx[i]]) subDecoys++;
                else subTargets++;
            }
            ctx.LogInfo(string.Format(
                "[COUNT] {0} Percolator streaming subsample: {1} entries ({2} targets, {3} decoys)",
                passLabel, trainSubsetGlobalIdx.Length, subTargets, subDecoys));

            // 3. Build subset entry list + train.
            var subsetEntries = new List<PercolatorEntry>(trainSubsetGlobalIdx.Length);
            foreach (int i in trainSubsetGlobalIdx)
                subsetEntries.Add(percEntries[i]);
            var trainConfig = new PercolatorConfig
            {
                TrainFdr = percConfig.TrainFdr,
                TestFdr = percConfig.TestFdr,
                MaxIterations = percConfig.MaxIterations,
                NFolds = percConfig.NFolds,
                Seed = percConfig.Seed,
                CValues = percConfig.CValues,
                MaxTrainSize = percConfig.MaxTrainSize,
                FeatureNames = percConfig.FeatureNames,
                TrainOnly = true
            };
            PercolatorResults trainResults = PercolatorFdr.RunPercolator(subsetEntries, trainConfig);

            // 4. Apply averaged model to ALL entries and compute q-values.
            return PercolatorFdr.ScorePopulationAndComputeFdr(percEntries, trainResults, percConfig);
        }

        /// <summary>
        /// Run simple target-decoy competition FDR (no machine learning).
        /// Uses coelution_sum as the scoring function.
        /// </summary>
        private void RunSimpleFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx)
        {
            var fdrController = new FdrController(config.RunFdr);

            foreach (var kvp in perFileEntries)
            {
                var result = fdrController.CompeteAndFilter(
                    kvp.Value,
                    e => e.CoelutionSum,
                    e => e.IsDecoy,
                    e => e.EntryId);

                ctx.LogInfo(string.Format(
                    "  {0}: {1} targets pass (FDR={2:F4}, {3} target wins, {4} decoy wins)",
                    kvp.Key, result.PassingTargets.Count, result.FdrAtThreshold,
                    result.NTargetWins, result.NDecoyWins));

                // Assign q-values based on simple competition
                // Passing targets get fdr_at_threshold, non-passing get 1.0
                var passingIds = new HashSet<uint>();
                foreach (var target in result.PassingTargets)
                    passingIds.Add(target.EntryId);

                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy && passingIds.Contains(entry.EntryId))
                    {
                        entry.RunPrecursorQvalue = result.FdrAtThreshold;
                        entry.RunPeptideQvalue = result.FdrAtThreshold;
                        entry.ExperimentPrecursorQvalue = result.FdrAtThreshold;
                        entry.ExperimentPeptideQvalue = result.FdrAtThreshold;
                    }
                }
            }
        }

        /// <summary>
        /// First-pass protein FDR run BEFORE Stage 6 reconciliation, on the
        /// full pre-compaction peptide pool. Sets only RunProteinQvalue
        /// (leaves ExperimentProteinQvalue at its 1.0 default for the
        /// second-pass to overwrite). Detected-peptide filter uses
        /// run_peptide_qvalue, the strict peptide-level gate, matching Rust
        /// pipeline.rs:3045-3049 exactly. Protein-FDR gate is config.RunFdr
        /// (1x), the Savitski-2015 convention applied at first pass, NOT the
        /// 2x relaxed gate the post-output Stage 8 protein FDR uses.
        /// </summary>
        private void RunFirstPassProteinFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            // Detected-peptide count for logging only; the core computation +
            // propagation lives in ProteinFdr.RunFirstPassProteinFdr so the
            // --task MergeNode rehydration path (PerFileRescoreTask) can run the
            // same logic without duplicating it.
            int detectedCount = 0;
            var detectedTracker = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy && entry.RunPeptideQvalue <= config.RunFdr)
                        detectedTracker.Add(entry.ModifiedSequence);
                }
            }
            detectedCount = detectedTracker.Count;
            ctx.LogInfo(string.Format(
                "[COUNT] First-pass detected peptides for protein FDR: {0} unique",
                detectedCount));

            ProteinFdr.RunFirstPassProteinFdr(perFileEntries, fullLibrary, config);

            // Recompute summary counters for log parity with the prior inline
            // implementation. The static helper has already mutated entries
            // via PropagateProteinQvalues; the parsimony / FDR objects below
            // are rebuilt for logging + the diagnostic dump only.
            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedTracker);
            var bestScores = ProteinFdr.CollectBestPeptideScores(perFileEntries);
            var proteinFdr = ProteinFdr.ComputeProteinFdr(parsimony, bestScores, config.RunFdr);
            int nAtRunFdr = 0;
            foreach (var qv in proteinFdr.GroupQvalues.Values)
            {
                if (qv <= config.RunFdr)
                    nAtRunFdr++;
            }
            ctx.LogInfo(string.Format(
                "First-pass protein FDR: {0} target groups at {1:P1} FDR",
                nAtRunFdr, config.RunFdr));

            if (OspreyDiagnostics.DumpProteinFdr)
            {
                OspreyDiagnostics.WriteStage6ProteinFdrDump(
                    bestScores, proteinFdr.PeptideQvalues);
                if (OspreyDiagnostics.ProteinFdrOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_PROTEIN_FDR_ONLY");
            }
        }
    }
}
