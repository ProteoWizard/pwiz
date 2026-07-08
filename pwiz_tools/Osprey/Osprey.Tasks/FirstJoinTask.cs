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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// First-join phase of the Osprey pipeline (Stage 5 in the
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
    internal sealed class FirstJoinTask : OspreyTask
    {
        public override string Name => @"FirstPassFDR";

        /// <summary>
        /// Computes the Stage 5 first-join (Percolator first-pass FDR + Stage 6
        /// planning) in straight-through, --task FirstPassFDR (StopAfterStage5), and
        /// the --input-scores full-pipeline. Excluded in --task PerFileScoring
        /// (stops at Stage 1-4), --task PerFileRescoring, and the --task SecondPassFDR
        /// stage (where it rehydrates the bundle rather than recomputing).
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx)
        {
            var c = ctx.Config;
            bool inputs = c.InputScores != null && c.InputScores.Count > 0;
            // The (inputs && StopAfterStage5) clause leans on a CLI-enforced
            // invariant: StopAfterStage5 is set by --task FirstPassFDR, which
            // requires --input-scores, so StopAfterStage5 implies inputs at
            // parse time -- a --task FirstPassFDR run can never reach here without
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
            // --task FirstPassFDR); a worker-mode consumer materializes it via
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

            // Stage 5: First-pass FDR. The Percolator path prints its own
            // "Running First-pass Percolator on N entries..." line from the FDR
            // engine, so the generic header would just be a redundant second
            // header right after the [TASK] FirstPassFDR banner. Emit it only for
            // the other methods (Simple / fallback), which otherwise go straight
            // to per-file result lines with no header of their own.
            if (config.FdrMethod != FdrMethod.Percolator)
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running {0} FDR control on coelution results...",
                    config.FdrMethod));
            }

            var swFdr = Stopwatch.StartNew();
            var featureContributions = RunFdr(perFileEntries, config, ctx);
            swFdr.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] Percolator/Simple FDR: {0:F1}s",
                swFdr.Elapsed.TotalSeconds));

            LogFirstPassResultsAndDump(perFileEntries, config, ctx, featureContributions);

            // First-pass protein FDR: runs on the full pre-compaction
            // peptide pool so target and decoy proteins compete on a
            // symmetric set. Sets RunProteinQvalue on every FdrEntry,
            // which Stage 6 reconciliation reads via the protein-rescue
            // gate in ConsensusRts.Compute. Mirrors Rust pipeline.rs:3029
            // ("First-pass protein FDR").
            if (config.ProteinFdr.HasValue && perFileEntries.Count > 0)
            {
                ctx.LogInfo(string.Empty);
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
                    @"--task FirstPassFDR: {0}/{1} 1st-pass fdr_scores.bin sidecar " +
                    @"writes failed; boundary file pair is incomplete. See warnings above.",
                    fdrSidecarFailures, perFileEntries.Count));
                ctx.ExitCode = 1;
                return false;
            }

            // FDRBench input TSV (pass 1): emit the full pre-compaction first-pass
            // pool -- every scored non-decoy target, regardless of q-value, with its
            // first-pass run + experiment q-values and raw SVM discriminant -- BEFORE
            // compaction drops the non-surviving entries. Mirrors Rust
            // pipeline.rs write_fdrbench_peptide_input (emitted after first-pass
            // protein FDR, before the compaction HashSet is built). Pass 2 (the
            // post-compaction reported set) is emitted from MergeNodeTask; the two
            // are mutually exclusive per run (--fdrbench-pass).
            WriteFdrBenchPass1IfRequested(perFileEntries, config, ctx);

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
            PipelineContext ctx,
            FeatureContributions contributions = null)
        {
            LogFirstPassResults(perFileEntries, config, ctx);

            if (ctx.Diagnostics?.DumpPercolator ?? false)
                ctx.Diagnostics?.WriteStage5PercolatorDump(perFileEntries);
            if (ctx.Diagnostics?.PercolatorOnly ?? false)
                OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_PERCOLATOR_ONLY");

            // --model-diagnostics: emit the self-contained interactive HTML
            // report from the just-scored, pre-compaction first-pass entries
            // (decoys + entrapment still present) and the trained model. Opt-in
            // and off the default output path, so it can't affect any other
            // output; a failure is logged and swallowed inside Write.
            if (config.ModelDiagnostics)
            {
                var libraryById = ctx.Get<LibraryById>().Value;
                ModelDiagnosticsReport.Write(perFileEntries, contributions, libraryById, config, ctx.LogInfo);
            }
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
        /// Write the pass-1 FDRBench input TSV from the pre-compaction first-pass
        /// pool when <c>--fdrbench</c> is set with a pass mask that includes pass 1
        /// (<c>--fdrbench-pass 1</c> or <c>both</c>). Emits every scored non-decoy
        /// target (regardless of q-value) with its first-pass run + experiment
        /// q-values and raw SVM discriminant -- the assumption the second-pass
        /// reported set rests on. No-op for the default pass-2 (emitted
        /// post-compaction by <see cref="MergeNodeTask"/>) and when no FDRBench
        /// output was requested. Called on the straight-through Run path only: the
        /// pre-compaction pool exists solely here, mirroring Rust osprey, which
        /// emits at the same point in its single pipeline.
        /// </summary>
        private void WriteFdrBenchPass1IfRequested(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx)
        {
            var benchPath = FdrBenchInputWriter.PathForPass(config, OspreyConfig.FDRBENCH_PASS_1);
            if (benchPath == null)
                return;

            var libraryById = ctx.Get<LibraryById>().Value;
            var swFdrBench = Stopwatch.StartNew();
            // Reconcile the library against the external manifest: reconstruct the
            // extras' pairing and drop unmatched entrapment (Met-clip artifacts) so the
            // TSV and the emitted manifest stay consistent and stock FDRBench works.
            var pairing = EntrapmentPairing.Build(libraryById, config.DecoyPairingManifestPath);
            var benchResult = FdrBenchInputWriter.WritePeptideInput(
                benchPath, perFileEntries, libraryById, config.FdrLevel,
                config.FdrBenchPerRun, pairing.ExcludedEntrapment);
            string manifestPath = benchPath + @".pairing.tsv";
            int manifestRows = FdrBenchInputWriter.WritePairingManifest(manifestPath, libraryById, pairing);
            swFdrBench.Stop();
            ctx.LogInfo(string.Format(@"Wrote FDRBench input (pass 1, {0}) to {1}: {2} rows",
                config.FdrBenchPerRun ? @"per-run" : @"per-precursor",
                benchPath, benchResult.Rows));
            ctx.LogInfo(string.Format(@"Wrote FDRBench pairing manifest (from the searched library) to {0}: {1} peptides",
                manifestPath, manifestRows));
            pairing.LogSummary(ctx.LogInfo);
            if (benchResult.MissingLibrary > 0)
                ctx.LogInfo(string.Format(
                    @"{0} FDRBench rows had no library entry; peptide and protein columns left blank",
                    benchResult.MissingLibrary));
            if (benchResult.TruncatedProtein > 0)
                ctx.LogInfo(string.Format(
                    @"{0} FDRBench rows had oversize protein-ID lists; truncated with ';...+N_more'",
                    benchResult.TruncatedProtein));
            ctx.LogInfo(string.Format(@"[STAGE-WALL] fdrbench-pass1: {0:F1}s",
                swFdrBench.Elapsed.TotalSeconds));
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
                    var stats = RescoreCompaction.Apply(bundle);
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
                                firstPassBaseIds.Add(entry.EntryId & ScoringTaskShared.BASE_ID_MASK);
                            }
                        }
                    }
                    int beforeCount = 0, afterCount = 0;
                    foreach (var kvp in perFileEntries)
                    {
                        beforeCount += kvp.Value.Count;
                        kvp.Value.RemoveAll(e => !firstPassBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
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
        /// --task FirstPassFDR StopAfterStage5 exit paths.
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
            ctx.LogInfo(@"Reconciliation planning");

            // Four cross-file planning phases (multi-charge consensus, cross-run
            // consensus RTs, per-file calibration refit, reconciliation
            // planning), each routing its diagnostic dump through ctx.Diagnostics.
            var plan = new Stage6Planner(ctx).Plan(
                perFileEntries, perFileCalibrations, perFileParquetPaths, config);

            // Stage 5 → Stage 6 boundary: write the per-file
            // .reconciliation.json envelope (the .fdr_scores.bin
            // companion was already written above pre-compaction).
            // Pairs with the --task PerFileRescoring Stage 6
            // worker mode (next sprint).
            //
            // Surfaces gap-fill targets via out param so the in-
            // process Stage 6 rescore call below can execute them.
            int reconWriteFailures = WriteReconciliationFiles(
                perFileEntries,
                plan.ReconciliationActions,
                plan.Consensus,
                plan.RefinedCalibrations,
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
                        @"--task FirstPassFDR: {0}/{1} reconciliation.json " +
                        @"writes failed; boundary file pair is incomplete. See warnings above.",
                        reconWriteFailures, perFileEntries.Count));
                    ctx.ExitCode = 1;
                    return false;
                }
                ctx.LogInfo(string.Format(
                    @"--task FirstPassFDR: Stage 5 + reconciliation planning " +
                    @"complete; wrote {0} reconciliation.json + matching fdr_scores.bin " +
                    @"sidecar pair(s). Exiting before Stage 6 rescore.",
                    perFileEntries.Count));
                // Success: return true (not false). The stop after Stage 5 is now
                // a membership fact -- PerFileRescore and MergeNode are excluded
                // by IsIncluded under --task FirstPassFDR, so the driver loop iterates no
                // further. The failure path above keeps ExitCode=1; return false.
                ctx.ExitCode = 0;
                return true;
            }

            // Surface outputs for the next task.
            _didPlan = true;
            _perFileConsensusTargets = plan.PerFileConsensusTargets;
            _reconciliationActions = plan.ReconciliationActions
                ?? new Dictionary<(string, int), ReconcileAction>();
            _refinedCalibrations = plan.RefinedCalibrations;
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
        /// the input mzML (or, in --task FirstPassFDR mode, the synthetic input
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
            // --task SecondPassFDR will validate against. Sort + dedup once;
            // BuildReconciliationFile copies the list into the wire form.
            var joinFileStems = new List<string>(perFileEntries.Count);
            foreach (var fEntry in perFileEntries)
            {
                if (!string.IsNullOrEmpty(fEntry.Key))
                    joinFileStems.Add(fEntry.Key);
            }
            joinFileStems.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to dedup adjacent identical stems immediately below; equal keys are byte-identical so tie order is irrelevant
            for (int i = joinFileStems.Count - 1; i > 0; i--)
            {
                if (string.Equals(joinFileStems[i], joinFileStems[i - 1], StringComparison.Ordinal))
                    joinFileStems.RemoveAt(i);
            }

            // The join-wide first-pass passing base_id set. perFileEntries is
            // already compacted here (a base_id passing peptide-q in ANY file is
            // kept in ALL files), so the distinct base_ids remaining across all
            // files ARE that set. Persisted per file (below) so an HPC
            // PerFileRescore worker -- which only has its own file in memory --
            // compacts to the same set the in-memory straight-through pipeline
            // uses, instead of a per-file subset that drops cross-file entries.
            var globalBaseIds = new HashSet<uint>();
            foreach (var fEntry in perFileEntries)
                foreach (var e in fEntry.Value)
                    globalBaseIds.Add(e.EntryId & ScoringTaskShared.BASE_ID_MASK);

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
                    searchHash, libraryHash, joinFileStems, globalBaseIds);
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
        /// --task FirstPassFDR mode where InputFiles is empty we synthesize the
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
            // --task FirstPassFDR fallback: derive a synthetic mzML path from the
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
            IReadOnlyList<string> joinFileStems,
            HashSet<uint> globalBaseIds)
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
            // Array.Sort OK: EntryId is effectively unique here (reconcile actions are
            // keyed by distinct per-file entry index, at most one action per row), so the
            // comparator does not tie in practice. Tie hazard, conversion deferred: if a
            // file ever carried duplicate EntryIds each with an action they would tie, and
            // this is not a #4362 approved U-site (converting could change the golden).
            useCwt.Sort((a, b) => a.EntryId.CompareTo(b.EntryId)); // Array.Sort OK: (see above) EntryId effectively unique; tie hazard deferred, not a #4362 approved U-site
            forced.Sort((a, b) => a.EntryId.CompareTo(b.EntryId)); // Array.Sort OK: (see above) EntryId effectively unique; tie hazard deferred, not a #4362 approved U-site

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

            // Sorted ascending for deterministic, byte-parity output.
            var baseIdArray = new uint[globalBaseIds.Count];
            globalBaseIds.CopyTo(baseIdArray);
            Array.Sort(baseIdArray); // Array.Sort OK: unique uint base_ids, single primitive array, no ties

            return new ReconciliationFile
            {
                FileStems = fileStems,
                FirstPassBaseIds = baseIdArray,
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
        private FeatureContributions RunFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx)
        {
            switch (config.FdrMethod)
            {
                case FdrMethod.Percolator:
                    return RunPercolatorFdr(perFileEntries, config, ctx);

                case FdrMethod.Simple:
                    PercolatorEngine.RunSimpleFdr(perFileEntries, config, ctx.LogInfo);
                    return null;

                default:
                    ctx.LogWarning(string.Format(
                        "FDR method {0} not yet supported, falling back to simple",
                        config.FdrMethod));
                    PercolatorEngine.RunSimpleFdr(perFileEntries, config, ctx.LogInfo);
                    return null;
            }
        }

        /// <summary>
        /// Run Percolator-based FDR control (Stage 5). Thin facade over
        /// <see cref="PercolatorEngine.RunPercolatorFdr"/>: supplies the PIN
        /// feature names and routes logging through <c>ctx.LogInfo</c>. Static +
        /// internal so <see cref="MergeNodeTask"/> can call it for the 2nd-pass
        /// run after Stage 6 reconciliation (the HPC distribution case where
        /// workers wrote reconciled .scores.parquet but no
        /// .2nd-pass.fdr_scores.bin sidecars; mirrors Rust pipeline.rs:4394-4468).
        /// </summary>
        internal static FeatureContributions RunPercolatorFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx,
            string passLabel = "First-pass")
        {
            bool aborted = PercolatorEngine.RunPercolatorFdr(
                perFileEntries, config,
                OspreyFeatureCalculators.BuildFeatureInfos(ParquetScoreCache.PIN_FEATURE_NAMES),
                ctx.LogInfo, out var contributions,
                BuildPercolatorDiagnostics(ctx.Diagnostics), passLabel);
            if (aborted)
            {
                // A diagnostic-only (*Only) Stage 5 dump fired. The FDR engine left
                // the run a pure no-op and signalled here; the Tasks layer -- not
                // the engine -- owns the process exit (this is the early-exit the
                // engine's former inline Environment.Exit(0) used to perform).
                ctx.LogInfo(@"[BISECT] Percolator diagnostic-only dump complete - aborting run");
                Environment.Exit(0);
            }
            // The trained model's feature contributions, for the --model-diagnostics
            // report. Null on the Simple/second-pass paths that don't produce one.
            return contributions;
        }

        /// <summary>
        /// Translate the run's <see cref="IOspreyDiagnostics"/> Stage 5 Percolator
        /// gate flags into the small <see cref="PercolatorDiagnosticsConfig"/> the
        /// FDR engine accepts. Returns <c>null</c> when diagnostics are off or no
        /// Percolator dump is requested -- the common case -- so the engine's dump
        /// call sites short-circuit on a single null check and allocate nothing.
        /// </summary>
        private static PercolatorDiagnosticsConfig BuildPercolatorDiagnostics(IOspreyDiagnostics diag)
        {
            if (diag == null ||
                !(diag.DumpStandardizer || diag.DumpPercInput ||
                  diag.DumpSubsample || diag.DumpSvmWeights))
            {
                return null;
            }
            return new PercolatorDiagnosticsConfig
            {
                DumpStandardizer = diag.DumpStandardizer,
                StandardizerOnly = diag.StandardizerOnly,
                DumpPercInput = diag.DumpPercInput,
                PercInputOnly = diag.PercInputOnly,
                DumpSubsample = diag.DumpSubsample,
                SubsampleOnly = diag.SubsampleOnly,
                DumpSvmWeights = diag.DumpSvmWeights,
                SvmWeightsOnly = diag.SvmWeightsOnly
            };
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
            // Orchestration (compute + propagation + summary logging) lives in
            // ProteinFdrEngine.RunFirstPass (shared with the --task SecondPassFDR
            // rehydration path in PerFileRescoreTask). It returns the parsimony /
            // FDR artifacts so we can emit the Stage-6 diagnostic dump here WITHOUT
            // recomputing them. The dump + ProteinFdrOnly early-exit stay in this
            // Tasks facade because Osprey.FDR cannot reference
            // Osprey.Diagnostics (the Diagnostics project references FDR).
            var result = ProteinFdrEngine.RunFirstPass(
                perFileEntries, fullLibrary, config, ctx.LogInfo);

            if (ctx.Diagnostics?.DumpProteinFdr ?? false)
            {
                ctx.Diagnostics?.WriteStage6ProteinFdrDump(
                    result.BestScores, result.ProteinFdr.PeptideQvalues);
                if (ctx.Diagnostics?.ProteinFdrOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_PROTEIN_FDR_ONLY");
            }
        }
    }
}
