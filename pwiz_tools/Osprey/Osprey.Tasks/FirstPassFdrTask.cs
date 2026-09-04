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
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// FirstPassFDR phase of the Osprey pipeline (Stage 5 in the
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
    internal sealed class FirstPassFdrTask : OspreyTask
    {
        public override string Name => @"FirstPassFDR";

        /// <summary>
        /// Computes Stage 5 (Percolator first-pass FDR + Stage 6
        /// planning) in straight-through, --task FirstPassFDR (StopAfterStage5), and
        /// the --input-scores full-pipeline. Excluded in --task PerFileScoring
        /// (stops at Stage 1-4), --task PerFileRescoring, and the --task SecondPassFDR
        /// stage (where it rehydrates the bundle rather than recomputing).
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx) => IsIncludedFor(ctx.Config);

        /// <summary>
        /// Pure membership predicate behind <see cref="IsIncluded"/>, exposed so a caller
        /// that needs to know whether first-pass Percolator trains in THIS process asks the
        /// one definition instead of re-deriving it.
        ///
        /// <para><see cref="PerFileScoringTask"/>'s pre-compaction-pool decision used
        /// <c>!NoJoin</c> as a proxy for exactly this question. That proxy is right for every
        /// task except <c>--task SecondPassFDR</c>, which leaves <c>NoJoin</c> false while
        /// setting <c>ExpectReconciledInput</c> - so this task is EXCLUDED, nothing trains,
        /// and the resident pre-compaction pool the proxy forced was pure waste at O(files)
        /// (issue #4486). Calling the predicate keeps the two from drifting again.</para>
        /// </summary>
        internal static bool IsIncludedFor(OspreyConfig c)
        {
            bool inputs = c.InputScores != null && c.InputScores.Count > 0;
            // The (inputs && StopAfterStage5) clause leans on a CLI-enforced
            // invariant: StopAfterStage5 is set by --task FirstPassFDR, which
            // requires --input-scores, so StopAfterStage5 implies inputs at
            // parse time -- a --task FirstPassFDR run can never reach here without
            // InputScores.
            // ProgramTests.TestValidateFirstPassFdrRequiresInputScores pins that
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
            typeof(CompactedEntries), typeof(PlanningPerformed),
            typeof(ProteinCompactStratum), typeof(FirstPassSurvivorSource)
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

        // The protein-compact stratum (base_ids of >=2-peptide 1st-pass proteins), built
        // during first-pass protein FDR and read by the compaction gate to ADMIT present-
        // protein peptides that did not individually pass 1st-pass FDR (so they get
        // reconciled + rescored + reported). Null unless OSPREY_PASS2_QVALUE=protein-compact.
        private HashSet<uint> _proteinCompactStratum;

        /// <summary>The post-compaction surviving base_ids, so the Stage 5 -&gt; 6 boundary can
        /// release the library fragments nothing can score any more. Null on the legacy resident
        /// path, which simply skips the release.
        ///
        /// <para>This is the LIBRARY-retention set, and it stays a separate field from the one
        /// <see cref="_survivorLoader"/> filters on rather than collapsing into it: the two are
        /// the same on the projection path but not on <see cref="Rehydrate"/>, which takes this
        /// one off the reconciliation bundle (<c>GlobalFirstPassBaseIds</c>) while the loader
        /// filters on the compaction's retained set - that union'd with the planner's action
        /// targets. Feeding either set to the other's consumer would be wrong in one direction
        /// or the other.</para></summary>
        private HashSet<uint> _firstPassBaseIds;

        // Rebuilds any one file's survivors from its .scores.parquet + finalized
        // 1st-pass sidecar, so a per-file consumer never needs the all-files buffer
        // (issues #4526, #4536). Set on the projection path from the passing base_id set
        // computed here, and on the rehydrate path from the retained set the compaction
        // hands back on the bundle; null only on the legacy resident path, whose consumers
        // fall back to CompactedEntries.
        private FirstPassSurvivorLoader _survivorLoader;
        /// <summary>True once the compaction gate has decided to hand Stage 6 the survivor
        /// LOADER instead of a materialized buffer, so the per-file lists it published are
        /// empty by design rather than by failure.</summary>
        private bool _survivorsStreamed;
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

            // The analysis-wide 1st-pass EXPERIMENT sidecar (format v5, issue #4486). This task
            // writes it in WriteExperimentSidecar and counts a failure to do so against the same
            // total as the per-file writes - because the Stage 5 -> Stage 6 boundary is
            // incomplete without it - but it was declared by nobody, so the driver could call
            // this task done with the file absent and leave Stage 6's compaction and Stage 7's
            // seeder reading an artifact that was never produced.
            //
            // Conditional on exactly what WriteExperimentSidecar is conditional on: with no
            // output blib there is nothing to name the file after and none is written, so
            // declaring it there would make IsTaskAlreadyDone permanently false.
            string experimentPath = FdrExperimentSidecar.PathFor(
                ctx.Config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(ctx.Config),
                FdrScoresSidecar.Pass.FirstPass);
            if (!string.IsNullOrEmpty(experimentPath))
                yield return experimentPath;

            // The pass-1 diagnostics product, when --model-diagnostics is on. Declaring it is
            // what puts the report inside the resume driver's forward scan instead of beside
            // it: a cohort that completed its first pass without the flag has every other
            // output current, so adding the flag makes this the one thing outstanding and the
            // driver runs the task - where Run's arm produces exactly it and nothing else.
            //
            // CONDITIONAL ON THE FLAG, for the reason SecondPassFdrTask's report output is:
            // declaring it unconditionally would leave every run that never asked for
            // diagnostics permanently invalid, re-running FirstPassFDR forever.
            if (ctx.Config.ModelDiagnostics)
            {
                string diagnosticsPath = ModelDiagnosticsReport.Pass1SidecarPath(ctx.Config);
                if (!string.IsNullOrEmpty(diagnosticsPath))
                    yield return diagnosticsPath;
            }
        }

        /// <summary>
        /// True when the pass-1 diagnostics product is the single declared output this task
        /// still owes: it is absent, and every other declared output exists with a current
        /// validity stamp. The condition Run's fold arm turns on.
        ///
        /// <para>Asked over <see cref="Outputs"/> rather than a hand-listed set, so a future
        /// output is covered without anyone remembering to add it here - the failure direction
        /// of a forgotten entry is then a redundant recompute rather than a wrongly-adopted
        /// first pass.</para>
        /// </summary>
        private bool OnlyDiagnosticsProductOutstanding(PipelineContext ctx)
        {
            string diagnosticsPath = ModelDiagnosticsReport.Pass1SidecarPath(ctx.Config);
            if (string.IsNullOrEmpty(diagnosticsPath) || File.Exists(diagnosticsPath))
                return false;
            string validityKey = ValidityKey(ctx);
            foreach (string output in Outputs(ctx))
            {
                if (string.Equals(output, diagnosticsPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (PerFileResumeDriver.IsCurrent(output, Name, validityKey))
                    continue;
                // Name the first output that failed. Declining here is not an error - it means a
                // genuine first pass is owed - but on a large cohort it is the difference between
                // seconds and hours, and without this line the only symptom is that the run takes
                // a very long time and still produces the right answer.
                ctx.LogInfo(string.Format(
                    @"FirstPassFDR: not folding diagnostics from completed work - {0} is {1}, " +
                    @"so the first pass is re-run.",
                    output, File.Exists(output) ? @"present but not current for this analysis"
                        : @"missing"));
                return false;
            }
            return true;
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            // OSPREY_EXPERIMENT_AGG changes this task's OWN output (the experiment-wide precursor
            // and peptide q maps), so it has to invalidate the cache. Without it, re-running an A/B
            // arm in an output directory that already holds the other arm's results makes
            // TaskValiditySidecar.IsValid return true, the driver skips Run entirely - taking the
            // unrecognized-value warning with it, since that lives inside Run - and the previous
            // mode's q is silently reused and recorded as the new arm's measurement. The flag is
            // read from a process-wide static rather than the config, which is why it is not
            // already covered by SearchIdentity; promoting it to a real command argument would
            // subsume this line.
            // The aggregation suffix (empty unless engaged) is built by the ONE shared helper the
            // downstream tasks also use, so the three keys cannot drift apart. The floor toggles
            // are part of it: they feed the aggregate written into this task's own Pass1Path
            // output, so a floor sweep in one directory would otherwise reuse the previous arm's
            // q as the new arm's measurement.
            // The 2nd-pass mode belongs here even though pass 2 runs later: protein-compact is
            // the only mode that needs the >=2-peptide stratum, and this task is where the
            // stratum is computed and written into the 1st-pass model sidecar. A sidecar written
            // under transfer carries no stratum, so a protein-compact re-run that adopted it
            // would be reading an artifact that cannot answer its question.
            // The sidecar FORMAT VERSION belongs here because this task's output is the
            // .1st-pass.fdr_scores.bin every later stage reads back. Resuming a directory written
            // before a format bump would find this task still valid, skip it, and then have every
            // v4 reader refuse the v3 file by version - leaving RestorePass1Scalars to seed
            // nothing and write ResetScores defaults into the 2nd-pass sidecars under only a
            // warning. Including the version turns that silent-wrong-output path into a clean
            // recompute.
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash()
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.TrainSampleValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx);
        }

        public override bool Run(PipelineContext ctx)
        {
            // Compute path (Stage 5 first-pass FDR + Stage 6 planning): the
            // upstream PerFileScoring task did NOT hydrate a rescore bundle, so
            // this run owns the full FirstPassFDR work. The bundle-present
            // disk-load counterpart lives in Rehydrate. The driver reaches this
            // task here only in the bundle-absent modes (straight-through,
            // --task FirstPassFDR); a worker-mode consumer materializes it via
            // ctx.Demand which routes to Rehydrate.
            var config = ctx.Config;

            // ScoredEntries (pre-compaction) -- this task is the one that
            // compacts the shared buffer below, so it reads it before that.
            var perFileEntries = ctx.Get<ScoredEntries>().Value;
            var perFileCalibrations = ctx.Get<PerFileCalibrations>().Value;
            var perFileIsolationMz = ctx.Get<PerFileIsolationMz>().Value;
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            var fullLibrary = ctx.Get<FullLibrary>().Value;

            // OSPREY_EXPERIMENT_AGG family, re-checked at the CONSUMING site against FirstPassFDR's
            // real file count. Program.ValidateArgs runs the same helper at startup from the
            // command line, which is where an operator wants the message.
            //
            // The two counts are NOT the same number and are not meant to be: startup counts the
            // files named on the command line, while this counts the files that actually produced
            // scored entries (PerFileScoringTask adds a file only on success). A run can therefore
            // pass at startup and be refused here - which is the point of checking twice, not a
            // drift to be eliminated.
            //
            // This MUST stay above the sidecar deletion below. Deleting first meant an argument
            // error destroyed the Stage-5 validity sidecars of a run that had computed no FDR at
            // all, so the operator fixed the variable and paid for a full recompute - hours at 82
            // files. The damage concentrated exactly on the sweep workflow, because the arm is part
            // of ValidityKey, so a warm re-run into a directory holding a different arm always
            // takes this path. Nothing above this point writes or removes any output.
            //
            // Every check inside is gated on the aggregation being engaged, so a default run
            // that merely inherited a stale sweep variable is untouched.
            string aggError = OspreyEnvironment.ValidateExperimentAggSettings(perFileEntries.Count);
            if (aggError != null)
                throw new InvalidOperationException(aggError);

            // NOT a blanket wipe of every declared output's marker on entry. That was written
            // against a partially-written output, and there is no such state: every one of these
            // artifacts is committed through FileSaver, an atomic rename, so a file is absent or
            // complete. What the wipe actually did was destroy the record of which files this
            // task had already finished - the only thing a per-file resume can read - so a run
            // interrupted at file 300 of 446 came back and re-scored all 446.
            //
            // Staleness is handled where it can be handled correctly: each writer clears its own
            // marker immediately before its own write and stamps it immediately after
            // (FlushPartialSidecar, WriteFdrScoresSidecars, WriteExperimentSidecar,
            // WriteReconciliationFiles). A marker therefore never outlives the file it vouches
            // for, and one that survives a crash vouches for a file that really is complete.
            //
            // SecondPassFdrTask still has the blanket form and wants the same treatment.

            // The diagnostics product is the ONLY outstanding output: every computational
            // artifact this task produces is already on disk and key-current, and the driver
            // reached Run solely because the pass-1 diagnostics JSON is missing - which is what
            // happens when a cohort finished its first pass WITHOUT --model-diagnostics and the
            // flag is added on a later invocation. Adopt the completed first pass rather than
            // recomputing it: Rehydrate is exactly that path, and it folds the report per run
            // and writes it on the way through.
            //
            // Placed ABOVE every writer below, because "runs the task" must mean "produces the
            // one missing output" and not "redoes the search". Getting this wrong is expensive
            // and SILENT: a FirstPassFDR that genuinely re-ran would clear the stamps of outputs
            // that are already correct and spend 4h46m on a 446-run cohort to produce a report -
            // and it would produce the RIGHT report, so no gate would ever report the cost.
            if (config.ModelDiagnostics && OnlyDiagnosticsProductOutstanding(ctx))
            {
                ctx.LogInfo(@"FirstPassFDR: every output but the model-diagnostics product is " +
                            @"current; folding the report from the completed first pass.");
                return Rehydrate(ctx);
            }

            // Stage 5: First-pass FDR. The Percolator framework (SVM or Gbdt) prints
            // its own "Running First-pass Percolator on N entries..." line from the FDR
            // engine, so the generic header would just be a redundant second
            // header right after the [TASK] FirstPassFDR banner. Emit it only for
            // the other methods (Simple / fallback), which otherwise go straight
            // to per-file result lines with no header of their own.
            if (!config.FdrMethod.UsesPercolatorFramework())
            {
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"Running {0} FDR control on coelution results...",
                    config.FdrMethod));
            }

            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo,
                string.Format(@"Stage 5 start: {0} files loaded (stubs), before first-pass FDR", perFileEntries.Count));

            // The line above reports GC.GetTotalMemory(false) -- allocated-since-last-GC,
            // so it carries whatever garbage scoring left behind, and two runs doing
            // identical work can differ by tens of GB on GC timing alone. This one forces
            // a collection first, so it is the LIVE set entering first-pass FDR: the only
            // number that answers whether the run fits in a given box.
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage5-start-live",
                string.Format(@"(post-GC, entering first-pass FDR, files={0})", perFileEntries.Count));

            // Phase 4 (issue #4355): first-pass Percolator reloads each entry's PIN
            // features on demand -- one file at a time, from that file's
            // .scores.parquet by ParquetIndex -- keeping only scalar scores
            // resident instead of holding all N files' 21-feature vectors at once
            // (which OOM'd 80-file joins; the Phase-1 bulk reload that used to sit
            // here did exactly that). The FDR engine drives the reload through this
            // delegate, so Osprey.FDR takes no Osprey.IO dependency. The f64 parquet
            // roundtrip is regression-exact (the same reload the second pass uses
            // via Pass2FdrSidecar.MapFeaturesByScoreIndex). A file with no mapped
            // parquet path yields an empty row set, so its entries fall back to
            // basic features -- matching the pre-streaming builder's fallback.
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = fileName =>
            {
                if (perFileParquetPaths.TryGetValue(fileName, out string scoresPath))
                    return ParquetScoreCache.LoadPinFeaturesFromParquet(scoresPath);
                return Array.Empty<double[]>();
            };

            // Issue #4355 step (b) increment ii: when the projection flag is set (and
            // the production Percolator method is in use), route the first-pass FDR
            // peak through the thin FdrProjection buffer instead of holding the full
            // FdrEntry stub buffer resident across Percolator + protein FDR + the
            // sidecar write + compaction. RunFirstPassProjection returns the reloaded
            // full-FdrEntry survivor buffer (from parquet + the just-written 1st-pass
            // sidecar), which then flows into PlanStage6 / Publish exactly as the
            // legacy compacted buffer does -- the blast radius is confined to this
            // pre-compaction span. Falls back to the legacy FdrEntry-buffer path
            // (the byte-identity oracle) when the flag is off or FdrMethod != Percolator.
            // FDRBench pass-1 (#4377) reads the full pre-compaction first-pass pool resident
            // (decoys + entrapment, with scores) -- exactly what the projection path drops to
            // bound memory -- so when it is requested, take the resident (legacy) path so that
            // report still emits. Off the default output path, so byte-identity is unaffected
            // (the regression gate never sets it).
            // OSPREY_PASS2_QVALUE=transfer takes the SAME lean projection first-pass path as the
            // default: it no longer forces the resident pool. The per-run-only redesign (see
            // TODO-osprey_pass2_per_run_only_qvalue) drops the FULL pre-compaction score->q table
            // (the aggregating structure that needed every entry's features resident, and the
            // 82-file OOM) and instead maps each ADJUSTED peak through that file's OWN 1st-pass
            // (score -> run q) sidecar table at pass 2. The frozen model it still needs is captured
            // on the projection path via the RunFirstPassProjection captureModel hook.
            // --model-diagnostics is NOT here: it now STREAMS its pass-1 report off the
            // projection path via a ModelDiagnosticsData.Accumulator fed by the score-pass sink
            // (RunFirstPassProjection below), folding each pre-compaction row into the reduced
            // report structures rather than holding the whole-run FdrEntry pool resident -- which
            // OOM'd an 82-file run at FirstPassFDR. The reductions are order-independent, so the
            // streamed report is byte-identical to the resident build, and it stays off the
            // default output path.
            bool needsResidentFirstPassPool =
                !string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1;
            // NOTE: transfer-compete does NOT force the resident pool -- it only needs the
            // trained 1st-pass MODEL (not the full-population score->q table), which the
            // streaming projection path publishes cheaply via captureModel below. Forcing
            // resident here OOMs on large (entrapment) libraries.
            if (OspreyEnvironment.UseFdrProjection && config.FdrMethod.UsesPercolatorFramework() &&
                !needsResidentFirstPassPool)
            {
                // Null unless PerFileScoring took the lean path and streamed the rows
                // straight from parquet (issue #4397); RunFirstPassProjection then builds
                // from the fat stubs instead.
                //
                // Consume, not Get: this is the projection set's only consumer, and the
                // byproduct cache is process-lifetime. Leaving it published pinned ~5.7 GiB
                // (191 M rows x 32 B) plus the interned peptide table through reconciliation
                // and the blib write -- memory that scales with total scored entries across
                // all files, exactly what streaming the projection was meant to stop paying
                // for. Consume drops the pipeline's reference up front so no later path,
                // including the StopAfterStage5 early return below, can retain it (#4405).
                var prebuiltProjections = ctx.Consume<FdrProjections>().Value;
                var survivors = RunFirstPassProjection(
                    perFileEntries, perFileParquetPaths, fullLibrary, config, ctx, loadFileFeatures,
                    prebuiltProjections);
                prebuiltProjections = null;

                if (survivors == null)
                    return false;  // StopAfterStage5 sidecar failure; ExitCode already set
                perFileEntries = survivors;
            }
            else
            {
                var swFdr = Stopwatch.StartNew();
                var featureContributions = RunFdr(perFileEntries, config, ctx, loadFileFeatures);
                // Persist the model on the RESIDENT path too. The projection path does it in
                // its captureModel hook, but this path's hook only publishes - and the block
                // in PlanStage6 that used to persist for BOTH was removed with this change.
                // Left as it was, OSPREY_FDR_PROJECTION=0 and --fdrbench-pass 1 would produce a
                // directory carrying a stratum and no model, which LoadFromAny reads as no
                // frozen state at all.
                if (ctx.TryGet<FirstPassPercolatorModel>(out var residentModel) &&
                    residentModel.Results != null)
                {
                    PersistFirstPassModel(residentModel.Results, perFileParquetPaths,
                        ValidityKey(ctx), ctx);
                }
                swFdr.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] Percolator/Simple FDR: {0:F1}s",
                    swFdr.Elapsed.TotalSeconds));
                ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after first-pass Percolator FDR");
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"first-pass-fdr-live",
                    string.Format(@"(post-GC, resident pool, files={0})", perFileEntries.Count));

                LogFirstPassResultsAndDump(perFileEntries, config, ctx, featureContributions);

                // First-pass protein FDR: runs on the full pre-compaction
                // peptide pool so target and decoy proteins compete on a
                // symmetric set. Sets ExperimentProteinQvalue on every FdrEntry,
                // which Stage 6 reconciliation reads via the protein-rescue
                // gate in ConsensusRts.Compute. Runs unconditionally (not gated
                // on --protein-fdr), matching Rust where config.protein_fdr is a
                // plain f64 (default 0.01) and this block is gated only on
                // !can_skip_fdr. Mirrors Rust pipeline.rs:3029 ("First-pass
                // protein FDR").
                if (perFileEntries.Count > 0)
                {
                    ctx.LogInfo(string.Empty);
                    var swFirstPassProtein = Stopwatch.StartNew();
                    RunFirstPassProteinFdr(perFileEntries, fullLibrary, perFileParquetPaths, config, ctx);
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
                // pipeline.rs write_fdrbench_peptide_input (#4377). Pass 2 (the
                // post-compaction reported set) is emitted from SecondPassFdrTask; the two
                // are mutually exclusive per run (--fdrbench-pass). Reached only on the
                // resident (legacy) first-pass path -- --fdrbench pass 1 routes here via
                // the projection gate above.
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
                // Phase 4 (issue #4355): the first-pass score pass now streams features
                // per file and never assigns them onto these FdrEntry stubs, so on the
                // straight-through path they are already null here. But the
                // --input-scores / per-file-resume stub loaders DO populate
                // FdrEntry.Features when hydrating, so null them defensively to keep the
                // "Features != null means this entry was rescored" sentinel that
                // ReconciledParquetWriter.BuildOverlay relies on valid going into
                // Stage 6 (mirrors the resume-rehydrate re-null and
                // PerFileScoringTask.HydrateRescoreBundleIfPresent). SecondPassFDR reloads
                // features from the reconciled parquet.
                foreach (var kvp in perFileEntries)
                    foreach (var entry in kvp.Value)
                        entry.Features = null;
                CompactFirstPass(perFileEntries, null, config, ctx);
                ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after Stage-5 CompactFirstPass");
            }

            // NOTE: no 2nd-pass FDR sidecar overlay here. Stage 7
            // (SecondPassFdrTask) owns its own 2nd-pass rehydrate -- it reloads (or
            // recomputes) the 2nd-pass scores onto the shared entry buffer
            // before protein FDR and the blib write. A former overlay at this
            // point was redundant (its result was overwritten by SecondPassFDR's,
            // and nothing between here and Stage 7 consumes it -- Stage 6 is a
            // no-op once a 2nd-pass sidecar exists) and forced Stage 5 to reach
            // forward into SecondPassFdrTask for its validity key. Removed so Stage 5
            // holds no knowledge of what runs after it.

            // Stage 6: planning checkpoint -- multi-charge consensus +
            // consensus RTs + per-file calibration refit + reconciliation
            // planning. Produces the inputs PerFileRescoreTask consumes.
            // Runs even single-file -- multi-charge consensus + the planning
            // checkpoint must still execute to match Rust; cross-run
            // reconciliation degenerates to zero actions there.
            if (perFileEntries.Count >= 1 && config.Reconciliation.Enabled)
            {
                if (!PlanStage6(perFileEntries, perFileCalibrations, perFileIsolationMz,
                        perFileParquetPaths, fullLibrary, config, ctx))
                    return false;
            }

            // Reconciliation planning was the last consumer that needs every file's
            // survivors at once. Drop the CONTENTS now, keeping the outer per-file list
            // (the shared buffer identity every milestone wraps) so nothing downstream
            // has to learn a new shape: Stage 6 refills one file at a time from the
            // survivor source and empties it again after that file's reconciled parquet
            // is written. Without this the 88.9 M entries stay live for the whole rescore
            // - 28 GB across 5.5 hours at 163 files, issue #4526.
            // The post-compaction counterpart of the pre-compaction resident-pool guard: taking
            // the resident handoff has to be NAMED, exactly as forcing the legacy first-pass
            // pool does. Checked here because this is where the decision is made, and BEFORE
            // the release so a refused run fails with an actionable message rather than an OOM
            // five hours into Stage 6.
            string handoffError = PerFileScoringTask.Stage6ResidentHandoffGuardError(
                _survivorLoader != null && !config.StopAfterStage5,
                OspreyEnvironment.Stage6StreamSurvivors,
                OspreyEnvironment.AllowUnfixedResident);
            if (handoffError != null)
                throw new InvalidOperationException(handoffError);

            if (OspreyEnvironment.Stage6StreamSurvivors && _survivorLoader != null && !config.StopAfterStage5)
            {
                foreach (var kvp in perFileEntries)
                {
                    kvp.Value.Clear();
                    kvp.Value.TrimExcess();
                }
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"stage5-handoff-released",
                    string.Format(@"(post-GC, survivors released after planning, files={0})",
                        perFileEntries.Count));
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

            // Hand these over rather than keeping a copy. The byproducts now hold them, and this
            // task has no further use for them - every field below is read only by Rehydrate,
            // which is mutually exclusive with the Run that just executed.
            //
            // Nulling matters because a byproduct release alone frees NOTHING while the task
            // object still points at the same objects: the task instance lives in the pipeline
            // array for the life of the process, so these survive every consumer. That is how
            // FirstPassFDR came to hold its whole-experiment planning products - the
            // reconciliation action map alone is 30.8 M entries at 446 runs, order 13 GB with
            // the rest - from the end of planning through the entire rescore, with no reader.
            _perFileConsensusTargets = null;
            _reconciliationActions = null;
            _refinedCalibrations = null;
            _perFileGapFillForRescore = null;
            ReleaseUnscorableLibraryFragments(fullLibrary, ctx);
            ctx.Publish(new CompactedEntries(perFileEntries));
            // Null off the projection path (legacy resident / rehydrate), where a
            // consumer falls back to the buffer above.
            ctx.Publish(new FirstPassSurvivorSource(_survivorLoader));
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

            // When the rescore hydrates per run there is nothing for this task to adopt. Building
            // the experiment-wide bundle here would rebuild exactly what the per-run loop is
            // about to read one run at a time - and it would do it by the OTHER route: not the
            // upstream load (already skipped), but this task's own
            // LoadOwnReconciliationBundle -> StreamOwnReconciliationBundle, which walks every
            // run's envelope and parquet. Removing an O(runs) pre-load from one producer only
            // moves it if the second producer is left standing.
            //
            // What downstream still needs from this task is the per-run SURVIVOR LOADER, and it
            // needs the retained base_id set to build it. That set comes from the analysis-wide
            // summary rather than from a bundle assembled by reading every run - which is the
            // whole reason the summary exists.
            if (ScoringTaskShared.CanHydratePerRun(config))
                return RehydrateForPerRunRescore(ctx, perFileEntries);

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
            bool builtOwnBundle = bundle == null;
            if (builtOwnBundle)
            {
                bundle = LoadOwnReconciliationBundle(ctx, perFileEntries);
                if (bundle == null)
                    return false;  // load failure; ExitCode already set
            }

            ctx.LogInfo(@"Bundle hydration: skipping first-pass Percolator (sidecar provides q-values).");

            // The bundle's PreCompactionTallies are non-null only when the hydrate that
            // produced it STREAMED (compacting each file as it loaded, so it never held the
            // all-files pre-compaction pool) - either the upstream worker-mode load or this
            // task's own LoadOwnReconciliationBundle on a lean resume. The per-file
            // passing-target counts below are then read from those tallies rather than
            // recomputed off perFileEntries, which by that point holds only survivors - the
            // same reason the lean projection path reads them off its score-pass sink. The
            // --model-diagnostics report is streamed off the same load for the same reason,
            // and its accumulator is null only where the RESIDENT twin ran, which is exactly
            // where perFileEntries still IS the pre-compaction pool the batch report reads.
            LogFirstPassResultsAndDump(perFileEntries, config, ctx, null,
                bundle.PreCompactionTallies, bundle.ModelDiagnosticsAccumulator);
            // The accumulator holds the whole run's --model-diagnostics reduction (~1-2 GB
            // at 82 files) and has exactly one reader, the WriteFromAccumulator call the
            // line above just made. It reached here on the published RescoreBundle, whose
            // byproduct slot lives for the process, so leaving the property set would pin
            // that memory through Stage 6 and SecondPassFDR for nothing.
            bundle.ModelDiagnosticsAccumulator = null;

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
            // Release here too, not just on Run. This path is a RESUME - which is exactly what
            // an operator does after the OOM this release exists to prevent - so skipping it
            // would leave the whole library resident in the one run that most needs it lean.
            // The bundle carries both halves of the retained set: the base_id set below, and
            // PerFileGapFill just published above.
            //
            // RetainedBaseIds, not GlobalFirstPassBaseIds: the compaction retains the global set
            // UNION the planner's action targets, so the global set alone can be a strict subset
            // of what survives. Releasing on the smaller set could free the library spectrum of
            // an entry Stage 6 still rescores. That gap is believed unreachable today - the
            // planner runs after compaction on the computed path, so its targets are already in
            // the envelope's set - but the relationship is not symmetric: the retained set is a
            // superset, so using it is safe whether or not the union is empty, and using the
            // other one is safe only while the argument holds. Falls back for an empty join,
            // where Apply never ran and nothing survived to release against.
            _firstPassBaseIds = bundle.RetainedBaseIds ?? bundle.GlobalFirstPassBaseIds;
            _perFileGapFillForRescore = bundle.PerFileGapFill;
            ReleaseUnscorableLibraryFragments(ctx.Get<FullLibrary>().Value, ctx);
            ctx.Publish(new PerFileConsensusTargets(ConsensusTargetsFromBundle(ctx, bundle)));
            ctx.Publish(new CompactedEntries(perFileEntries));

            // The same per-file survivor source Run publishes, so an arm that rescores streams
            // exactly as a computed run does (issue #4536). Before this the slot was published
            // null here, and PerFileRescore's streamed branches are all gated on it, so the
            // all-files survivor buffer stayed live across the whole rescore. What a resume
            // lacked was only the passing base_id set to rebuild from, and the compaction just
            // above now hands that back on the bundle.
            if (!TryBuildResumeSurvivorLoader(ctx, bundle, perFileEntries, out _survivorLoader))
                return false;  // missing parquet path; ExitCode already set
            ctx.Publish(new FirstPassSurvivorSource(_survivorLoader));

            // ... but only an arm that actually RESCORES gets anything from releasing the
            // buffer, and this task's own bundle source is what says which arm this is. With a
            // worker-supplied RescoreBundle, Stage 6 runs the rescore and refills one file at a
            // time from the source above. Having built the bundle from our OWN sidecars, there
            // is no rescore to run at all: PerFileRescore self-gates to a no-op (didPlan is
            // false and RescoreBundle is null) and its refill is the whole of the deferred
            // pool build, so releasing here would cost a full extra parquet + sidecar pass
            // over every file to undo.
            //
            // Since #4597 that refill happens on SecondPassFDR's pull rather than at the end
            // of Stage 6, so releasing here WOULD buy a real window on this arm - the width
            // of the rescore that does not happen. Left alone deliberately: the arm's own
            // point is that there is no rescore to bound, and paying a whole-run reload to
            // free a buffer Stage 7 immediately rebuilds is the trade #4526 was careful not
            // to make. Reconsider only alongside #4486, which is what makes Stage 7's
            // whole-run input smaller rather than moving it.
            //
            // The buffer is therefore still resident from here to the end of Stage 7 on that
            // arm - as it is on EVERY arm, because that whole-run pool is what Stage 7 takes
            // as input, however it is built. That residency is a property of Stage 7's input,
            // not of resuming, and it is #4486's to remove. This issue bounds the RESCORE
            // window, which is the part Stage 6 owns.
            bool rescoreWillStream = !builtOwnBundle && !config.StopAfterStage5;
            // Same call Run makes. streamingAvailable is "this run will stream", not "a loader
            // exists": passing the latter would refuse an OSPREY_STAGE6_STREAM_SURVIVORS=0
            // resume whose behaviour is identical either way, which is a guard inventing work
            // for an operator rather than preventing any.
            string handoffError = PerFileScoringTask.Stage6ResidentHandoffGuardError(
                _survivorLoader != null && rescoreWillStream,
                OspreyEnvironment.Stage6StreamSurvivors,
                OspreyEnvironment.AllowUnfixedResident);
            if (handoffError != null)
                throw new InvalidOperationException(handoffError);

            // Drop the CONTENTS, keeping the outer per-file list (the shared buffer identity
            // every milestone wraps), exactly as Run does after planning. Consensus targets were
            // computed off the full buffer immediately above, which is its last all-files reader.
            if (OspreyEnvironment.Stage6StreamSurvivors && _survivorLoader != null && rescoreWillStream)
            {
                foreach (var kvp in perFileEntries)
                {
                    kvp.Value.Clear();
                    kvp.Value.TrimExcess();
                }
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"resume-handoff-released",
                    string.Format(@"(post-GC, survivors released after rehydrate, files={0})",
                        perFileEntries.Count));
            }
            // The bundle-adopt / resume path never plans, so the rescore gate is
            // false (PerFileRescore falls back to the no-op unless a worker
            // RescoreBundle is present). Mirrors the old "FirstPassFDR rehydrates ->
            // DidPlan is false" semantics, now as a published slot.
            ctx.Publish(new PlanningPerformed(false));
            return true;
        }

        /// <summary>
        /// The resume counterpart of the loader <see cref="ReloadFirstPassSurvivors"/> builds on
        /// the projection path: rebuild any ONE file's post-compaction survivors from its
        /// <c>.scores.parquet</c> plus its finalized <c>.1st-pass.fdr_scores.bin</c>, so Stage 6
        /// refills a file at a time instead of reading them off a buffer somebody had to hold
        /// for the whole rescore (issue #4536).
        ///
        /// <para>The set to filter to is <see cref="RescoreInputs.RetainedBaseIds"/>, taken off
        /// the bundle the compaction just ran on rather than
        /// <see cref="RescoreInputs.GlobalFirstPassBaseIds"/>. The two are NOT the same set:
        /// compaction retains the global one UNION the base_ids of every planner action target,
        /// and a target rescued by a sibling file's evidence is in the second term only.
        /// Filtering to the global set alone would drop exactly those entries on the rebuild,
        /// leaving them their stale Stage 4 boundaries in the blib - the divergence
        /// <see cref="RescoreCompaction"/>'s union step exists to prevent.</para>
        ///
        /// <para>Returns true with a NULL loader ONLY for an empty join, which is also the only
        /// case where a null loader costs nothing: there is no survivor buffer to bound. Any
        /// other missing precondition returns false with
        /// <see cref="PipelineContext.ExitCode"/> set, rather than falling back to the resident
        /// handoff. That asymmetry is the point of the issue this fixes: a silent fallback is
        /// how an O(files) path reached a default run in the first place, and
        /// <c>Stage6ResidentHandoffGuardError</c> cannot catch it - it reads a null loader as
        /// "this run could not stream" and exempts it. So the preconditions are faults here,
        /// where they can still be reported.</para>
        /// </summary>
        private static bool TryBuildResumeSurvivorLoader(
            PipelineContext ctx,
            RescoreInputs bundle,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            out FirstPassSurvivorLoader loader)
        {
            loader = null;
            if (bundle.RetainedBaseIds == null)
            {
                // Null means CompactFirstPass skipped RescoreCompaction.Apply, which it does
                // only for an empty join. Checked rather than assumed: if some later change
                // gives Apply a second skip, the loader would go null on a populated run and
                // Stage 6 would silently take the resident buffer again - the exact regression
                // shape that produced this issue, and one no guard downstream can see.
                if (perFileEntries.Count == 0)
                    return true;
                ctx.LogError(string.Format(
                    @"Resume rehydrate: the first-pass compaction published no retained base_id " +
                    @"set for {0} joined file(s), so the Stage 6 survivor handoff cannot be " +
                    @"streamed. RescoreCompaction.Apply must run whenever any file is joined.",
                    perFileEntries.Count));
                ctx.ExitCode = 1;
                return false;
            }
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            foreach (var kvp in perFileEntries)
            {
                if (perFileParquetPaths != null && perFileParquetPaths.ContainsKey(kvp.Key))
                    continue;
                ctx.LogError(string.Format(
                    @"Resume rehydrate: no scores parquet path published for {0}, so its " +
                    @"first-pass survivors cannot be rebuilt for the Stage 6 rescore.", kvp.Key));
                ctx.ExitCode = 1;
                return false;
            }
            loader = new FirstPassSurvivorLoader(
                perFileParquetPaths, ctx.Config, bundle.RetainedBaseIds,
                ctx.Get<SequencePool>().Value);
            return true;
        }

        /// <summary>
        /// Build the post-Stage-5 rescore bundle from THIS task's own
        /// <c>.1st-pass.fdr_scores.bin</c> + <c>.reconciliation.json</c> sidecars
        /// for a straight-through resume, where the driver skipped
        /// <see cref="Run"/> because those outputs were already valid on disk
        /// (<see cref="PipelineContext.CanRehydrate"/>) and a downstream task is
        /// the first to touch this task's state. Overlays the first-pass q-values
        /// onto the per-file stubs and parses the reconciliation envelopes -- the
        /// same bundle PerFileScoring's worker-mode hydration produces, but owned
        /// here against this task's own outputs. Returns <c>null</c> (with
        /// <see cref="PipelineContext.ExitCode"/> set) on a load failure; because
        /// CanRehydrate gated the sidecars as valid, that is a genuine fault, not
        /// a "recompute instead" case. Clears PIN features on the overlaid stubs,
        /// exactly as the worker hydration does, so PerFileRescore's "Features !=
        /// null means rescored" parquet criterion and SecondPassFDR's feature reload
        /// stay correct.
        ///
        /// <para>Which hydrate runs is decided by what upstream actually loaded,
        /// not by a flag. Stage 5 takes the LEAN load unless an opt-in output reads
        /// every entry's resident features (<c>NeedsResidentPool</c>), and the lean
        /// load publishes one EMPTY stub list per scored file. Those empty lists
        /// cannot carry the sidecar overlay: <c>FdrScoresSidecar.TryRead</c> binds
        /// each record to a stub by <c>entry_id</c> and fails the file when one is
        /// missing, so any sidecar that carries at least one record is refused
        /// against zero stubs. (A zero-record sidecar would "succeed" vacuously -
        /// the refusal comes from the records, not from the emptiness.) So the batch
        /// <see cref="RescoreHydration.HydrateReconciliationOverlay"/> is only
        /// correct when the resident pool is present. The lean case loads each
        /// file's stubs from its own <c>.scores.parquet</c> through
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/> instead, which
        /// compacts each file before touching the next and so never holds more than
        /// ONE file's pre-compaction pool (~1.19 GB per file, vs O(files) for the
        /// batch twin). That is what lets <c>--model-diagnostics</c> report off this
        /// path without the resident pool (issue #4505): the accumulator is fed from
        /// the pass that already reads every sidecar, so the report comes off the
        /// same PRE-compaction rows the batch write would have read.</para>
        /// </summary>
        private bool RehydrateForPerRunRescore(
            PipelineContext ctx, List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            var retainedBaseIds = ScoringTaskShared.ReadRetainedBaseIds(ctx.Config, out string error);
            if (retainedBaseIds == null)
            {
                ctx.LogError(error);
                ctx.ExitCode = 1;
                return false;
            }
            ctx.LogInfo(string.Format(
                @"Per-run rescore: FirstPassFDR publishes the survivor loader only; no " +
                @"experiment-wide bundle is built for {0} run(s).", perFileEntries.Count));

            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            _survivorLoader = new FirstPassSurvivorLoader(
                perFileParquetPaths, ctx.Config, retainedBaseIds, ctx.Get<SequencePool>().Value);
            _firstPassBaseIds = retainedBaseIds;

            // Gap-fill and the refined calibrations ARE read across runs here, and that is the
            // honest scope of the remaining coupling rather than an oversight. Stage 7's pool
            // rebuild (PerFileRescoreTask.Rehydrate -> OverlayReconciledIntoFiles) overlays every
            // run's reconciled parquet and needs the gap-fill targets to restore the detections
            // gap-fill transferred into runs that did not find them independently. Published
            // empty, it costs exactly those: 94 missing RetentionTimes rows on Stellar, with
            // NRunsDetected falling 3 -> 2.
            //
            // This is envelope JSON only - no parquet pass, no stub materialisation - so it is a
            // small fraction of the all-runs pre-load this path removed. It should become LAZY
            // (built on first read, like the RescoredEntries milestone) so a --task
            // PerFileRescoring worker, which never rebuilds the pool, does not pay it at all.
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var sequencePool = ctx.Get<SequencePool>().Value;
            // ONE read fills BOTH maps, so both byproducts share a single build guarded here
            // rather than each carrying its own factory - two factories over the same two
            // dictionaries would read every envelope twice for whoever asked second.
            bool envelopesRead = false;
            Action readEnvelopes = () =>
            {
                if (envelopesRead)
                    return;
                envelopesRead = true;
                var sw = Stopwatch.StartNew();
                RescoreHydration.ReadGapFillAndCalibrations(
                    perFileParquetPaths.Values, perFileGapFill, refinedCalibrations, sequencePool);
                ctx.LogInfo(string.Format(
                    @"Read gap-fill and refined calibrations from {0} run envelope(s) in {1:F1}s",
                    perFileGapFill.Count, sw.Elapsed.TotalSeconds));
            };
            // NOT assigned to _perFileGapFillForRescore. That field's only reader is the release
            // below, and doing so would force the read right here - deferring it and then
            // immediately demanding it. The release does not need it: see the note at its call
            // site, which already says the analysis-wide retained set alone is the right
            // predicate on THIS path because every run's gap-fill target is already inside the
            // set the planner wrote. Leaving it null makes that stated reasoning the actual
            // behaviour instead of a claim next to a redundant union.
            _perFileGapFillForRescore = null;

            // Release on this path too, for the reason Rehydrate's own release block gives: this
            // is a RESUME, which is what an operator runs after the OOM the release exists to
            // prevent, so skipping it would leave the whole library resident in the run that most
            // needs it lean. Mode 6 asserts it happens on every leg, including each --task
            // PerFileRescoring worker.
            //
            // The retained set is the right predicate here even though the release elsewhere
            // unions in the gap-fill targets: this path is only taken for --input-scores, where
            // every run's gap-fill target is already inside the analysis-wide retained set the
            // planner wrote. Skipping the release was tried while chasing a rehydrate-leg
            // divergence and changed that divergence not at all - the cause was elsewhere - so
            // this is not the place to economise on correctness grounds.
            ReleaseUnscorableLibraryFragments(ctx.Get<FullLibrary>().Value, ctx);

            // Every planning slot is published EMPTY on purpose. They are not absent - a
            // consumer's ctx.Get still succeeds - but they carry nothing, because the per-run
            // hydrate reads each run's actions, gap-fill and refined calibration out of that
            // run's own reconciliation.json inside its own iteration. Publishing them empty
            // rather than skipping the publish keeps the byproduct contract intact: an
            // unpublished slot throws UnknownByproductException at the reader, turning a
            // deliberate design into a crash at an unrelated call site.
            //
            // CompactedEntries republishes the SAME list objects ScoredEntries holds - one empty
            // list per run, in input order. Those objects are the shared backing store the
            // rescore loop refills and drains one run at a time, so identity matters and
            // contents do not. No compaction runs here: the lists are empty, and each run is
            // compacted against the retained set as it is hydrated.
            ctx.Publish(new ReconciliationActions(new Dictionary<(string, int), ReconcileAction>()));
            ctx.Publish(new RefinedCalibrations(
                () => { readEnvelopes(); return refinedCalibrations; }));
            ctx.Publish(new PerFileGapFillForRescore(
                () => { readEnvelopes(); return perFileGapFill; }));
            ctx.Publish(new PerFileConsensusTargets(
                new Dictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>>()));
            ctx.Publish(new CompactedEntries(perFileEntries));
            ctx.Publish(new FirstPassSurvivorSource(_survivorLoader));
            ctx.Publish(new PlanningPerformed(false));
            return true;
        }

        /// <summary>
        /// Build the post-Stage-6 rescore bundle from THIS task's own
        /// sidecars for a straight-through resume. See the class remarks on the
        /// bundle-adopt path.
        /// </summary>
        private RescoreInputs LoadOwnReconciliationBundle(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            // Resolve each loaded file's own .scores.parquet path in perFileEntries
            // order so both hydrates' index-correspondence contract (entries[i] <->
            // parquetPaths[i]) holds; PerFileScoring published these paths as
            // PerFileParquetPaths.
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            var parquetPaths = new List<string>(perFileEntries.Count);
            long residentStubs = 0;
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
                residentStubs += kvp.Value.Count;
            }

            // Zero stubs across every file IS the lean load's signature: it appends a keyed
            // entry per scored file with an empty list, while every resident arm appends real
            // ones. Tested on the DATA rather than read off a published flag because it is the
            // overlay's actual precondition - "are there stubs to overlay onto" - and the
            // batch hydrate fails on exactly this shape.
            //
            // The alternative signal is FdrProjections, non-null exactly on the lean arm.
            // Equivalent today. If the two ever disagree, that is a bug in Stage 5's own
            // lean/fat bookkeeping, and this test still picks the hydrate the data can serve.
            // (An earlier version of this comment claimed an all-zero-row run could not reach
            // here because it stops at PerFileScoring's "no scored entries" boundary. That is
            // NOT true on the lazy-Demand path, which swallows the stop when ExitCode == 0.
            // The conclusion survives for a different reason: such a run writes no 1st-pass
            // sidecar, so FirstPassFDR Runs rather than Rehydrates and never reaches this line.)
            bool leanStubs = residentStubs == 0;

            RescoreInputs bundle;
            try
            {
                bundle = leanStubs
                    ? StreamOwnReconciliationBundle(ctx, perFileEntries, parquetPaths)
                    : RescoreHydration.HydrateReconciliationOverlay(perFileEntries, parquetPaths,
                        LoadFirstPassExperimentRecords(ctx.Config, ctx),
                        ctx.Get<SequencePool>().Value);
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
            // stays correct and SecondPassFDR reloads features from the reconciled
            // parquet -- mirrors PerFileScoringTask.HydrateRescoreBundleIfPresent.
            //
            // Skipped on the streaming arm, where it is provably a no-op: those stubs come
            // from LoadFdrStubsFromParquet, which never assigns Features. Running it anyway
            // would store null over null across every file's survivors - ~88.9 M reference
            // writes at 163 files, each tripping the GC write barrier and dirtying the card
            // table over the whole survivor buffer, immediately before Stage 6 adopts it.
            if (!leanStubs)
            {
                foreach (var kvp in perFileEntries)
                    foreach (var entry in kvp.Value)
                        entry.Features = null;
            }

            return bundle;
        }

        /// <summary>
        /// The file-count-bounded arm of <see cref="LoadOwnReconciliationBundle"/>: load
        /// each file's stubs from its own <c>.scores.parquet</c> inside
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/>, so one file's
        /// pre-compaction pool is resident at a time rather than all of them.
        ///
        /// <para><paramref name="perFileEntries"/> arrives holding the lean load's empty
        /// per-file lists and is CLEARED here, because the streaming hydrate appends the
        /// survivors itself and requires an empty buffer on entry. The list OBJECT is
        /// kept - it is the published <c>ScoredEntries</c> buffer that
        /// <see cref="Rehydrate"/> goes on to compact and republish as
        /// <c>CompactedEntries</c> - so clearing rather than replacing it is what keeps
        /// those consumers pointed at the same buffer.</para>
        ///
        /// <para>The per-file hook is the run's one look at each file's PRE-compaction
        /// pool, and both reductions taken there are the ones a resident pool used to
        /// serve afterwards: the run-level passing-target count Stage 5 reports per file,
        /// and the <c>--model-diagnostics</c> report reduction. The report needs those
        /// rows specifically - compaction discards ~52x of them, mostly the decoys and
        /// entrapment its FDP and calibration views are built from - so feeding it here
        /// is what makes the streamed report identical to the batch one rather than a
        /// plausible-looking survivors-only page.</para>
        ///
        /// <para>COST, stated rather than hidden: this is the SECOND full pass over every
        /// <c>.scores.parquet</c> on a lean resume. Stage 5 already streamed all five scalar
        /// columns of every file to build the counts-only projection - whose only consumer
        /// is <see cref="Run"/>, so on this path its surviving product is a log line and the
        /// empty-set gate - and this method then re-opens each file for the full stub read.
        /// The pre-#4505 mdiag full resume read each parquet ONCE, fat. The trade is
        /// deliberate: one extra sequential scan per file buys an O(files) -> O(1-file)
        /// pre-compaction pool, which is the whole point at 82 files. Removing the
        /// redundancy means making the Stage 5 lean load lazy about work only Run consumes,
        /// which is a separate change.</para>
        /// </summary>
        private RescoreInputs StreamOwnReconciliationBundle(
            PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<string> parquetPaths)
        {
            var config = ctx.Config;
            // File names in load order. The accumulator is seeded with them so its per-run
            // report rows are keyed by the same index the hook reports, and the streaming
            // hydrate rederives its own names from the parquet stems (checked below).
            var fileNames = perFileEntries.ConvertAll(kv => kv.Key);
            // This log line is what regression.ps1 mode 5 asserts on. It has to come from
            // THIS arm: Rehydrate's own "Bundle hydration" line is emitted before the bundle
            // source is even known, so a worker-supplied bundle logs it too and it cannot
            // witness the own-sidecar streaming path.
            ctx.LogInfo(string.Format(
                @"Resume rehydrate: streaming the first-pass bundle from {0} file(s) " +
                @"(one file's pre-compaction pool resident at a time).", fileNames.Count));

            // Diagnostics must never take down a real run - the invariant
            // ModelDiagnosticsReport states three times and enforces with catch-all guards
            // around its own builders. This path calls one of those builders directly
            // (BuildClassificationFromLibrary reads the --decoy-pairing-manifest through a
            // StreamReader and allocates multi-million-entry dictionaries), and the only
            // enclosing handler catches InvalidDataException, so an IOException or an OOM
            // would kill a search for the sake of an opt-in HTML page - where the batch write
            // it replaces logged and swallowed it. A failure here drops the report, not the
            // run: the accumulator stays null and LogFirstPassResultsAndDump skips it.
            ModelDiagnosticsData.Accumulator mdiagAccumulator = null;
            if (config.ModelDiagnostics)
            {
                try
                {
                    mdiagAccumulator = BuildModelDiagnosticsAccumulator(
                        fileNames, ctx.Get<LibraryById>().Value, config, ctx.LogInfo);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        @"--model-diagnostics: could not build the report accumulator on this " +
                        @"resume, so no report will be written. The search is unaffected. {0}",
                        ex.Message));
                }
            }
            // Hydrate into a LOCAL buffer, then swap the contents in only on success.
            // perFileEntries is the PUBLISHED ScoredEntries list, and the streaming hydrate
            // appends per file, so clearing it up front would leave it holding a partial
            // survivor set with the original keys gone if any file threw midway. The list
            // OBJECT still has to be the one that was published (Rehydrate compacts and
            // republishes it as CompactedEntries), which is why the contents are copied
            // rather than the reference replaced.
            // The analysis-wide retained set this task itself wrote when planning ended. A
            // resume reads it back rather than rebuilding the union from every run's envelope,
            // which is what makes the rehydrate below a single pass. InvalidDataException so it
            // joins the one graceful-failure policy the caller already applies to this hydrate.
            var resumeRetainedBaseIds = ScoringTaskShared.ReadRetainedBaseIds(config, out string retainedError);
            if (resumeRetainedBaseIds == null)
                throw new InvalidDataException(retainedError);

            var hydrated = new List<KeyValuePair<string, List<FdrEntry>>>(fileNames.Count);
            var bundle = RescoreHydration.HydrateCompactedStreaming(
                hydrated, parquetPaths,
                (fileIdx, fileName, parquetPath) => LoadResumeStubs(fileName, parquetPath,
                    ctx.Get<SequencePool>().Value),
                (fileIdx, fileName, stubs, tally) =>
                {
                    ScoringTaskShared.TallyPreCompaction(config, stubs, tally);
                    if (mdiagAccumulator != null)
                        ScoringTaskShared.FeedModelDiagnostics(mdiagAccumulator, fileIdx, stubs);
                },
                LoadFirstPassExperimentRecords(config, ctx),
                resumeRetainedBaseIds,
                ctx.Get<SequencePool>().Value);

            // The hydrate re-derived every key from its parquet stem, while the accumulator
            // above and the published PerFileParquetPaths map are keyed by the ORIGINAL
            // ScoredEntries stems. They agree today because GetScoresPath appends exactly
            // ".scores.parquet" and SyntheticInputFromParquet strips it - a round trip, not a
            // guarantee. Left unchecked, a divergence is SILENT: PerFileRescore looks its
            // parquet up by the new key, misses, and that file keeps its 1st-pass boundaries
            // all the way into the blib. Assert the coupling instead of relying on it.
            for (int i = 0; i < hydrated.Count; i++)
            {
                if (!string.Equals(hydrated[i].Key, fileNames[i], StringComparison.Ordinal))
                {
                    throw new InvalidDataException(string.Format(
                        @"Resume rehydrate: file {0} is keyed '{1}' by the loaded scores but " +
                        @"'{2}' by its parquet stem. The rescore looks parquet paths up by " +
                        @"the loaded key, so proceeding would silently leave this file " +
                        @"un-rescored.", i, fileNames[i], hydrated[i].Key));
                }
            }

            perFileEntries.Clear();
            perFileEntries.AddRange(hydrated);
            bundle.PerFileEntries = perFileEntries;
            bundle.ModelDiagnosticsAccumulator = mdiagAccumulator;
            return bundle;
        }

        /// <summary>
        /// One file's stub load for <see cref="StreamOwnReconciliationBundle"/>, with any
        /// parquet fault wrapped as <see cref="InvalidDataException"/>.
        ///
        /// <para><see cref="LoadOwnReconciliationBundle"/> catches exactly that type and
        /// promises an operator-facing "failed to hydrate reconciliation bundle from own
        /// sidecars" line plus <c>ExitCode = 1</c>. That was free for the batch arm, which
        /// never opened a parquet; this arm does, so a truncated or locked
        /// <c>.scores.parquet</c> would otherwise throw <c>IOException</c> straight out of
        /// <c>Rehydrate</c> with no exit code and none of that text. Mirrors both upstream
        /// resume loaders, which wrap for the same reason.</para>
        /// </summary>
        private static List<FdrEntry> LoadResumeStubs(string fileName, string parquetPath,
            LibraryStringInterner sequencePool)
        {
            try
            {
                return ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath, null, sequencePool);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    @"failed to load scored entries for {0} from {1}: {2}",
                    fileName, parquetPath, ex.Message), ex);
            }
        }

        /// <summary>
        /// Shared Stage 5 reporting for <see cref="Run"/> and
        /// <see cref="Rehydrate"/>: log per-file first-pass passing counts,
        /// then the diagnostic Percolator dump (gated by
        /// OSPREY_DUMP_PERCOLATOR; written before compaction drops rows so the
        /// cross-impl diff sees both targets and decoys) and the
        /// OSPREY_PERCOLATOR_ONLY measurement exit.
        ///
        /// <paramref name="preCompactionTallies"/> non-null says the hydrate that produced
        /// <paramref name="perFileEntries"/> compacted each file as it loaded, so this list
        /// holds SURVIVORS, not the pre-compaction pool. Both consumers below key off that:
        /// the per-file counts come from the tallies, and the Percolator dump is skipped
        /// rather than written from rows it would misreport.
        ///
        /// <paramref name="mdiagAccumulator"/> is the streamed
        /// <c>--model-diagnostics</c> reduction the bounded reconciled-bundle rehydrate
        /// folded every PRE-compaction row into as it loaded (see
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/>), non-null only there.
        /// It is what lets that path report at all: <paramref name="perFileEntries"/> has
        /// already lost the ~52x non-survivors - mostly the decoys and entrapment the FDP
        /// and calibration views are built from - so building the report off it would
        /// silently produce a plausible WRONG page. Null everywhere else, where
        /// <paramref name="perFileEntries"/> IS the pre-compaction pool and the batch
        /// <see cref="ModelDiagnosticsReport.Write"/> reduces it directly.
        /// </summary>
        private void LogFirstPassResultsAndDump(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx,
            FeatureContributions contributions = null,
            IReadOnlyList<PreCompactionTally> preCompactionTallies = null,
            ModelDiagnosticsData.Accumulator mdiagAccumulator = null)
        {
            LogFirstPassResults(perFileEntries, config, ctx, preCompactionTallies);

            bool dumpWritten = false;
            if (ctx.Diagnostics?.DumpPercolator ?? false)
            {
                // The dump's whole value is that it is written BEFORE compaction drops rows,
                // so the cross-impl diff sees decoys as well as targets. A non-null tally list
                // means the hydrate that produced perFileEntries already compacted each file as
                // it loaded, so what is left here is the survivors - roughly 1/52 of the rows,
                // with the decoys gone. Writing that would produce a file that LOOKS like a
                // Stage 5 dump and silently disagrees with every reference it is diffed
                // against, which is worse than not writing one. The lean projection path emits
                // no dump either (LogFirstPassResultsProjection has no dump point at all), so
                // skipping matches it; the warning is what keeps the operator from reading an
                // absent file as "the run had nothing to say".
                if (preCompactionTallies == null)
                {
                    ctx.Diagnostics?.WriteStage5PercolatorDump(perFileEntries);
                    dumpWritten = true;
                }
                else
                {
                    // Names the exact token, like every ResidentPoolGuardError message: an
                    // operator told to "set the matching token" without being told which one
                    // has to go read the source to act on the warning.
                    ctx.LogWarning(string.Format(
                        @"OSPREY_DUMP_PERCOLATOR: no Stage 5 dump written. This run hydrated " +
                        @"its first-pass state through the bounded per-file path, which " +
                        @"compacts each file as it loads, so the PRE-compaction pool the dump " +
                        @"reports never exists all at once. Re-run with " +
                        @"OSPREY_FDR_PROJECTION=0 OSPREY_ALLOW_UNFIXED_RESIDENT={0} for the " +
                        @"resident dump.", ResidentPaths.PROJECTION_OFF));
                }
            }
            if (ctx.Diagnostics?.PercolatorOnly ?? false)
            {
                // Master exited here unconditionally, and that stays true for every
                // combination it could reach - including OSPREY_PERCOLATOR_ONLY without
                // OSPREY_DUMP_PERCOLATOR, which the two env vars being read independently
                // makes legal. The one NEW combination is a dump that was asked for and
                // could not be written: exiting 0 there would hand a bisection harness a
                // success code with a stale dump or none, so it fails instead of lying.
                if ((ctx.Diagnostics?.DumpPercolator ?? false) && !dumpWritten)
                {
                    throw new InvalidOperationException(string.Format(
                        @"OSPREY_DUMP_PERCOLATOR + OSPREY_PERCOLATOR_ONLY: no Stage 5 dump " +
                        @"could be written on this run (see the warning above), so there is " +
                        @"nothing to stop after and exiting would report success without the " +
                        @"file. Re-run with OSPREY_FDR_PROJECTION=0 " +
                        @"OSPREY_ALLOW_UNFIXED_RESIDENT={0}.", ResidentPaths.PROJECTION_OFF));
                }
                OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_PERCOLATOR_ONLY");
            }

            // --model-diagnostics: emit the self-contained interactive HTML
            // report from the just-scored, pre-compaction first-pass entries
            // (decoys + entrapment still present) and the trained model. Opt-in
            // and off the default output path, so it can't affect any other
            // output; a failure is logged and swallowed inside the writer.
            if (config.ModelDiagnostics)
            {
                // Same file order either way: the accumulator was seeded with the input-file
                // order and perFileEntries keeps every file's key even once compacted.
                var cal = BuildCalibrationData(ctx, perFileEntries.ConvertAll(kv => kv.Key));
                var libraryById = ctx.Get<LibraryById>().Value;
                if (mdiagAccumulator != null)
                {
                    // The peak co-assignment panel (issue #4522) is built from the per-file FDR
                    // sidecars, NOT from perFileEntries - the same source the projection path
                    // uses, so there is one implementation of this panel rather than two.
                    //
                    // A non-null mdiagAccumulator means this is the bounded rehydrate path, and
                    // the remarks on this method say what that implies: perFileEntries "has
                    // already lost the ~52x non-survivors - mostly the decoys and entrapment",
                    // so building the report off it "would silently produce a plausible WRONG
                    // page". That is precisely what the earlier resident build did here. It
                    // agreed with the sidecar build on the acceptance boundary (0.0120) and on
                    // the accepted count (28,926) and still reported 72 detected decoys against
                    // 468, because compaction had already dropped the rest - target denominators
                    // intact, decoy class quietly gutted. The regression then overwrote the
                    // straight-through report with this one, so every measurement taken after a
                    // full run was the wrong page.
                    var coAssignment = PeakCoAssignmentSource.Build(
                        perFileEntries.ConvertAll(kv => kv.Key),
                        ctx.Get<PerFileParquetPaths>().Value, config,
                        mdiagAccumulator.ClassByBaseId, libraryById,
                        LoadFirstPassExperimentRecords(config, ctx), ctx.LogInfo);
                    ModelDiagnosticsReport.WriteFromAccumulator(
                        mdiagAccumulator, contributions, cal, config, ctx.LogInfo, coAssignment,
                        ValidityKey(ctx));
                }
                else
                {
                    ModelDiagnosticsReport.Write(perFileEntries, contributions, libraryById, cal,
                        config, ctx.LogInfo, ValidityKey(ctx));
                }
            }
        }

        /// <summary>
        /// Assemble the CAL-view <see cref="ModelDiagnosticsData.CalibrationData"/> for
        /// the report from the per-file calibration diagnostics
        /// (<see cref="PerFileCalibrationDiagnostics"/>) PerFileScoringTask captured at
        /// Stage 3. The rows are ordered by <paramref name="orderedFileNames"/> (input-file
        /// order) so the report's file selector matches the rest of the page. Returns
        /// <c>null</c> when no rows were captured (a resumed / rehydrated run, or none of
        /// the files calibrated), which hides the CAL tab. HasEntrapment is true when any
        /// row carries an entrapment FDP curve; MassUnit is the per-run unit the byproduct
        /// stashed at capture (the row does not carry it).
        /// </summary>
        private static ModelDiagnosticsData.CalibrationData BuildCalibrationData(
            PipelineContext ctx,
            IReadOnlyList<string> orderedFileNames)
        {
            IReadOnlyDictionary<string, ModelDiagnosticsData.CalFileRow> byFile = null;
            string massUnit = null;
            if (ctx.TryGet<PerFileCalibrationDiagnostics>(out var diag) && diag != null)
            {
                byFile = diag.Value;
                massUnit = diag.MassUnit;
            }
            if (byFile == null || byFile.Count == 0)
                return null;

            var files = new List<ModelDiagnosticsData.CalFileRow>(orderedFileNames.Count);
            var omitted = new List<string>();
            foreach (var name in orderedFileNames)
            {
                if (byFile.TryGetValue(name, out var row) && row != null)
                    files.Add(row);
                else
                    omitted.Add(name);
            }
            // A file with no captured calibration diagnostics (calibration failed / was skipped, or a
            // distributed run that did not persist matches) would otherwise vanish from the CAL
            // selector -- exactly the file a reviewer most wants to spot. Surface the omission rather
            // than dropping it silently. (A fuller fix is a placeholder "bad" row in the selector.)
            if (omitted.Count > 0)
                ctx.LogWarning(string.Format(
                    "CAL view: {0} of {1} file(s) have no captured calibration diagnostics and are " +
                    "omitted from the calibration report: [{2}]",
                    omitted.Count, orderedFileNames.Count, string.Join(", ", omitted)));
            if (files.Count == 0)
                return null;

            bool hasEntrapment = false;
            foreach (var row in files)
            {
                if (row.Fdp != null)
                {
                    hasEntrapment = true;
                    break;
                }
            }

            return new ModelDiagnosticsData.CalibrationData
            {
                Files = files,
                HasEntrapment = hasEntrapment,
                // Per-run unit stashed on the byproduct at capture (the row does not carry
                // it); default "ppm" if a run somehow recorded none.
                MassUnit = !string.IsNullOrEmpty(massUnit) ? massUnit : @"ppm",
                FileCount = files.Count,
            };
        }

        /// <summary>
        /// Build the streaming <see cref="ModelDiagnosticsData.Accumulator"/> that a
        /// pre-compaction row source folds each row into, so the pass-1
        /// <c>--model-diagnostics</c> report emits without the resident FdrEntry pool. Derives the
        /// entrapment classification from the searched library -- the same source and one-time
        /// logging as the resident <see cref="ModelDiagnosticsReport.Write"/> path -- and seeds the
        /// accumulator with the input-file order plus the run FDR level.
        ///
        /// Two row sources share it: the projection path's score-pass sink (fed as first-pass
        /// Percolator scores each row) and the streaming reconciled-bundle rehydrate
        /// (<see cref="RescoreHydration.HydrateCompactedStreaming"/>, fed per file after the
        /// 1st-pass sidecar overlay and before compaction discards the non-survivors). Both feed
        /// rows in the same nested (file, row) order the batch <see cref="ModelDiagnosticsData.Build"/>
        /// walks, which is what keeps the streamed report identical to the resident one.
        /// <paramref name="libraryById"/> is passed rather than pulled from the context because
        /// the rehydrate caller runs before <c>LibraryById</c> is published.
        /// </summary>
        internal static ModelDiagnosticsData.Accumulator BuildModelDiagnosticsAccumulator(
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            Action<string> logInfo)
        {
            ModelDiagnosticsReport.BuildClassificationFromLibrary(config, libraryById, logInfo,
                out var classByBaseId, out var pairByBaseId, out var entrapmentRatio);
            var runNames = new string[fileNames.Count];
            for (int i = 0; i < runNames.Length; i++)
                runNames[i] = fileNames[i];
            return new ModelDiagnosticsData.Accumulator(
                runNames, classByBaseId, pairByBaseId, entrapmentRatio, config.RunFdr, config.FdrLevel);
        }

        /// <summary>
        /// Log per-file and total first-pass passing-target counts at the
        /// configured run-level FDR. <paramref name="preCompactionTallies"/>, when present,
        /// is indexed positionally against <paramref name="perFileEntries"/> and is COMPLETE
        /// - one tally per file - so a short list is an inconsistency, not a fallback case.
        /// </summary>
        private void LogFirstPassResults(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx,
            IReadOnlyList<PreCompactionTally> preCompactionTallies = null)
        {
            int passingTargets = 0;
            for (int fileIdx = 0; fileIdx < perFileEntries.Count; fileIdx++)
            {
                var kvp = perFileEntries[fileIdx];
                // Prefer the tally the streaming hydrate reduced while this file's full
                // PRE-compaction stub list was resident: these counts are pre-compaction on
                // every other path too, and kvp.Value has already lost the non-survivors
                // when the tallies are present. Identical predicate either way.
                int fileTargets;
                if (preCompactionTallies != null)
                {
                    // Falling back to counting kvp.Value here would silently report the
                    // POST-compaction survivor count (~52x too small) as if it were the
                    // pre-compaction one. The tallies are built one per file in this same
                    // order, so a missing entry means hydrate and join disagree about the
                    // file set - stop rather than publish a plausible wrong number.
                    if (fileIdx >= preCompactionTallies.Count)
                    {
                        throw new InvalidOperationException(string.Format(
                            @"First-pass results: no pre-compaction tally for {0} (file {1} of {2}, " +
                            @"but the streaming hydrate captured only {3}). The tallies must cover " +
                            @"every joined file.",
                            kvp.Key, fileIdx + 1, perFileEntries.Count, preCompactionTallies.Count));
                    }
                    fileTargets = preCompactionTallies[fileIdx].PassingTargets;
                }
                else
                {
                    fileTargets = 0;
                    foreach (var entry in kvp.Value)
                    {
                        if (!entry.IsDecoy &&
                            entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                        {
                            fileTargets++;
                        }
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
        /// post-compaction by <see cref="SecondPassFdrTask"/>) and when no FDRBench
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
                    // When the bundle was hydrated by the streaming path, Apply saw an
                    // already-compacted buffer and re-derived the same retain set, so it
                    // removed nothing and its EntriesBefore == EntriesAfter. Report the real
                    // pre-compaction total from the per-file tallies that hydrate captured,
                    // so this line means the same thing on both paths.
                    // long: at ~4.2 M pre-compaction stubs a file this total overflows an
                    // int past ~505 files.
                    long entriesBefore = bundle.PreCompactionTallies != null
                        ? bundle.TotalPreCompactionStubs
                        : stats.EntriesBefore;
                    ctx.LogInfo(string.Format(
                        @"First-pass compaction: {0} -> {1} entries ({2} passing base_ids; {3} action(s) dropped)",
                        entriesBefore, stats.EntriesAfter,
                        stats.FirstPassBaseIds, stats.DroppedActions));
                }
                else
                {
                    var firstPassBaseIds = new HashSet<uint>();
                    // Peptide-q compaction gate: a dedicated field (default 0.01 = RunFdr)
                    // loosenable to broaden the reconciliation pool, mirroring Rust
                    // config.reconciliation_compaction_fdr (pipeline.rs:4650). Previously
                    // hardwired to config.RunFdr, which C# could not loosen independently.
                    double peptideGate = config.ReconciliationCompactionFdr;
                    // Protein-rescue gate is always active (default 0.01), matching
                    // Rust pipeline.rs:4651/4658 where protein_compaction_gate =
                    // config.protein_fdr (a plain f64, never a null switch).
                    double proteinGate = config.EffectiveProteinFdr;
                    foreach (var kvp in perFileEntries)
                    {
                        foreach (var entry in kvp.Value)
                        {
                            if (entry.IsDecoy)
                                continue;
                            uint baseId = entry.EntryId & ScoringTaskShared.BASE_ID_MASK;
                            // Compaction gate: passes peptide-q, OR passes protein-rescue, OR
                            // (protein-compact) is in the >=2-peptide protein stratum -- the
                            // last clause admits present-protein peptides that failed 1st-pass
                            // FDR so they get reconciled + rescored + reported.
                            if (entry.RunPeptideQvalue <= peptideGate ||
                                entry.ExperimentProteinQvalue <= proteinGate ||
                                (_proteinCompactStratum != null && _proteinCompactStratum.Contains(baseId)))
                            {
                                firstPassBaseIds.Add(baseId);
                            }
                        }
                    }
                    // long, matching the sibling counters at RescoreHydration.cs:173 and
                    // PerFileScoringTask.cs:553: this branch sums the RESIDENT all-files
                    // PRE-compaction pool, and at ~4.2 M stubs a file an int total overflows
                    // past ~505 files - inside the 500-file target these paths are being sized
                    // for, and silently, since nothing here is in a checked context.
                    long beforeCount = 0, afterCount = 0;
                    // Reported for the same reason as RescoreCompaction.Apply, and this is the
                    // loop that does the heavier work of the two: it runs on the uncompacted
                    // pool and genuinely removes, where Apply's streaming-hydrate path finds
                    // the set already retained and removes nothing.
                    int fpCompactIdx = 0;
                    using (var progress = new ProgressReporter(
                               string.Format(@"Compacting {0} file(s) to the first-pass retained set",
                                             perFileEntries.Count),
                               perFileEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        foreach (var kvp in perFileEntries)
                        {
                            progress.Report(fpCompactIdx++);
                            beforeCount += kvp.Value.Count;
                            kvp.Value.RemoveAll(e => !firstPassBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
                            kvp.Value.TrimExcess();
                            afterCount += kvp.Value.Count;
                        }
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
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(@"Reconciliation planning");

            // The library lookups gap-fill needs, and the per-file envelope's join-wide
            // fields. Built once, before any file is planned, because none of them varies by
            // file and the library scan is O(library).
            BuildGapFillLibraryLookups(fullLibrary, out var libLookup, out var libPrecursorMz);
            var writerState = PrepareReconciliationWrite(perFileEntries, config);
            var gapFillByFile = new Dictionary<string, List<GapFillTarget>>(StringComparer.Ordinal);
            int reconWriteFailures = 0;

            // The analysis-wide retained base_id set, accumulated as each run is planned: the
            // join-wide first-pass passing set UNION the base_id of every action target in every
            // run. Both terms are what RescoreCompaction.Apply retains, and this is the only
            // point in the analysis where the second one becomes knowable - a run's envelope is
            // written the moment that run's planning finishes, so it cannot carry actions the
            // runs planned after it have not produced yet.
            //
            // Accumulating here rather than deriving it later is what lets --task PerFileRescoring
            // stop being a join: without this summary every consumer had to rebuild the union by
            // reading all 446 envelopes before it could compact a single run. Bounded by the
            // LIBRARY, not by run count - both terms are base_id sets over the same library - so
            // this costs one entry per library precursor no matter how large the cohort.
            var retainedBaseIds = new HashSet<uint>();
            // The join-wide term is identical on every plan, so union it once. Re-unioning it
            // per run would be correct but would cost 446 x 744,943 set probes on the CHS cohort
            // to add nothing after the first.
            bool retainedSeeded = false;

            // Stage 5 -> Stage 6 boundary: each file's .reconciliation.json envelope is written
            // the moment that file's planning finishes, rather than after every file's. Same
            // artifact, one phase earlier, and it is what lets the planner release the file's
            // entries immediately - the whole point of not holding the all-files survivor
            // buffer. Pairs with the --task PerFileRescoring Stage 6 worker mode.
            //
            // Gap-fill targets are collected here too, for the in-process Stage 6 rescore below.
            Stage6FilePlanned onFilePlanned = filePlan =>
            {
                if (filePlan.GapFill != null && filePlan.GapFill.Count > 0)
                {
                    var copy = new List<GapFillTarget>(filePlan.GapFill.Count);
                    foreach (var target in filePlan.GapFill)
                        copy.Add(target);
                    gapFillByFile[filePlan.FileName] = copy;
                }
                if (!retainedSeeded && filePlan.GlobalBaseIds != null)
                {
                    retainedBaseIds.UnionWith(filePlan.GlobalBaseIds);
                    retainedSeeded = true;
                }
                AccumulateActionTargetBaseIds(filePlan, retainedBaseIds);
                reconWriteFailures += WriteReconciliationFile(
                    writerState, filePlan, perFileParquetPaths, config, ctx);
            };

            // Four cross-file planning phases (multi-charge consensus, cross-run
            // consensus RTs, per-file calibration refit, reconciliation
            // planning), each routing its diagnostic dump through ctx.Diagnostics.
            // Two passes over the files, one file resident at a time -- see Stage6Planner.
            var planner = new Stage6Planner(ctx);
            Stage6Planner.Stage6Plan plan;
            try
            {
                if (_survivorsStreamed)
                {
                    var fileNames = new List<string>(perFileEntries.Count);
                    foreach (var kvp in perFileEntries)
                        fileNames.Add(kvp.Key);
                    plan = planner.Plan(fileNames, LoadSurvivorsForPlanning, perFileCalibrations,
                        perFileParquetPaths, libLookup, libPrecursorMz, perFileIsolationMz, config,
                        onFilePlanned);
                }
                else
                {
                    plan = planner.Plan(perFileEntries, perFileCalibrations, perFileParquetPaths,
                        libLookup, libPrecursorMz, perFileIsolationMz, config, onFilePlanned);
                }
            }
            catch (InvalidDataException ex)
            {
                // A survivor load fault. The materialized path reported this as an error plus
                // exit 1, and losing that on the streamed path would replace an actionable
                // message with a stack trace at the process boundary. The message already names
                // the file and what to do about it.
                ctx.LogError(ex.Message);
                ctx.ExitCode = 1;
                return false;
            }

            // The 1st-pass model and the protein-compact stratum used to be written HERE, at the
            // end of planning. Both now land when the phase that computes them ends - the model
            // in PersistFirstPassModel as training returns it, the stratum in
            // BuildAndPublishProteinCompactStratum as protein FDR finishes - because writing
            // them here meant a 446-file run held both in memory for 228 minutes and lost them
            // to any interruption. Nothing replaces the block: by the time planning runs, both
            // artifacts have been on disk for hours.

            // Planning is complete, so the retained set is too. Write it BEFORE the
            // StopAfterStage5 exit: --task FirstPassFDR is precisely the phase whose job is to
            // leave behind the analysis-wide summaries --task PerFileRescoring then reads, and a
            // summary written only on the straight-through path would be missing in the one
            // configuration that exists to consume it.
            if (!WriteRetainedBaseIdSummary(retainedBaseIds, perFileParquetPaths, config, ctx))
                return false;

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
                // a membership fact -- PerFileRescore and SecondPassFDR are excluded
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
            _perFileGapFillForRescore = gapFillByFile;
            return true;
        }

        /// <summary>
        /// One file's survivors, loaded on demand for Stage 6 planning and released by the
        /// planner before the next file is read. THROWS on a load fault rather than returning
        /// null: planning around a file whose survivors could not be read would silently plan a
        /// smaller cohort than the compaction gate selected, and the resulting envelopes would
        /// look complete.
        /// </summary>
        private IReadOnlyList<FdrEntry> LoadSurvivorsForPlanning(string fileName)
        {
            var stubs = _survivorLoader.Load(fileName, out string error);
            if (stubs == null)
                throw new InvalidDataException(error);
            return stubs;
        }

        /// <summary>
        /// Write the per-file <c>.1st-pass.fdr_scores.bin</c> sidecars at
        /// the pre-compaction Stage 5 boundary (every stub, passing or
        /// not, gets persisted with its RUN-scope q-values + SVM score),
        /// and the one analysis-wide <c>.1st-pass.fdr_experiment.bin</c>
        /// beside the blib carrying the EXPERIMENT-scope columns. Mirrors
        /// the persist_fdr_scores call in osprey/src/pipeline.rs at line
        /// ~3180 (immediately after first-pass FDR, before compaction or
        /// protein FDR). Stage 6 workers re-apply the q-value threshold
        /// themselves to derive the post-compaction passing set; the
        /// protein-rescue half of that predicate reads the experiment
        /// file, which is why both are written here rather than only the
        /// per-file half (issue #4486).
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
            var experiment = new FdrExperimentAccumulator();
            // Stamped per file as each sidecar lands, NOT left to the driver's post-Run pass.
            // The driver stamps every declared output only after Run returns, so a task that
            // writes 446 durable artifacts and then dies in a later step leaves all 446
            // unmarked and the next invocation redoes work that is already complete and
            // correct on disk. A 446-file run lost 3h45m of streaming ingest, Percolator and
            // protein FDR that way on 2026-09-01: it wrote every one of these sidecars at
            // 07:09 and was killed at 08:41 in the survivor reload that follows.
            //
            // Stamping here is sound because these sidecars are written ONCE and never
            // mutated (#4621), so a file that exists is a file that is finished - there is no
            // partially-updated state for a marker to vouch for wrongly. That immutability is
            // the precondition; without it "present" would not imply "complete".
            string validityKey = ValidityKey(ctx);
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogWarning(string.Format(
                        "No sidecar base path for `{0}` — skipping fdr_scores.bin write", fileName));
                    failures++;
                    continue;
                }
                foreach (var e in kvp.Value)
                {
                    experiment.Add(e.EntryId, e.ExperimentPrecursorQvalue,
                        e.ExperimentPeptideQvalue, e.ExperimentProteinQvalue,
                        e.ExperimentAggregateScore, e.Pep);
                }
                string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);
                // Clear first so a marker from an earlier invocation cannot outlive the file it
                // vouches for if this write throws halfway.
                PerFileResumeDriver.ClearStale(fdrPath, Name);
                try
                {
                    FdrScoresSidecar.Write(fdrPath, kvp.Value, FdrScoresSidecar.Pass.FirstPass);
                    string parquetPath;
                    perFileParquetPaths.TryGetValue(fileName, out parquetPath);
                    PerFileResumeDriver.Stamp(fdrPath, Name, OspreyVersion.Current, validityKey,
                        parquetPath == null ? Array.Empty<string>() : new[] { parquetPath },
                        ctx.LogWarning);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to write 1st-pass fdr_scores.bin for {0}: {1}", fileName, ex.Message));
                    failures++;
                }
            }
            failures += WriteExperimentSidecar(
                experiment, FdrScoresSidecar.Pass.FirstPass, config, ctx);
            return failures;
        }

        /// <summary>
        /// Write the one analysis-wide experiment-scope sidecar beside the output blib.
        /// Counted into the same failure total as the per-file writes, because the Stage 5 →
        /// Stage 6 boundary is incomplete without it: the compaction predicate's protein-rescue
        /// half reads this file, so a Stage 6 worker that cannot find it cannot reproduce the
        /// in-process passing set.
        /// </summary>
        /// <returns>1 if the write failed or the analysis has no output blib to name it
        /// after, 0 on success.</returns>
        // Not static: it stamps its own validity marker, which needs the task's Name and
        // ValidityKey. This file is a declared Output, so leaving it unstamped would hold the
        // whole task un-resumable no matter how many per-file sidecars were marked.
        private int WriteExperimentSidecar(FdrExperimentAccumulator experiment,
            FdrScoresSidecar.Pass pass, OspreyConfig config, PipelineContext ctx)
        {
            string path = FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config), pass);
            if (string.IsNullOrEmpty(path))
            {
                ctx.LogWarning(
                    "No output blib to name the experiment-scope FDR sidecar after — skipping " +
                    "fdr_experiment.bin write. Stage 6 compaction will not find the protein " +
                    "q-values it rescues on.");
                return 1;
            }
            PerFileResumeDriver.ClearStale(path, Name);
            try
            {
                FdrExperimentSidecar.Write(path, experiment.Records, pass);
                ctx.LogInfo(string.Format(
                    @"Wrote experiment-scope FDR sidecar: {0} ({1} distinct entry ids)",
                    path, experiment.Count));
                PerFileResumeDriver.Stamp(path, Name, OspreyVersion.Current, ValidityKey(ctx),
                    Array.Empty<string>(), ctx.LogWarning);
                return 0;
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    "Failed to write {0}: {1}", path, ex.Message));
                return 1;
            }
        }

        /// <summary>
        /// Build the <c>(modified_sequence, charge) -&gt; (target_id, decoy_id)</c> and
        /// <c>entry_id -&gt; precursor_mz</c> lookups gap-fill identification needs. Decoy ID
        /// convention: <c>target_id | 0x80000000</c> (mirrors Rust at pipeline.rs:3330-3340).
        /// O(library), built once for the whole cohort.
        /// </summary>
        private static void BuildGapFillLibraryLookups(
            List<LibraryEntry> fullLibrary,
            out Dictionary<(string ModifiedSequence, byte Charge), (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            out Dictionary<uint, double> libPrecursorMz)
        {
            libLookup = new Dictionary<(string ModifiedSequence, byte Charge), (uint TargetEntryId, uint DecoyEntryId)>();
            libPrecursorMz = new Dictionary<uint, double>();
            foreach (var entry in fullLibrary)
            {
                if (entry.IsDecoy)
                    continue;
                uint decoyId = entry.Id | 0x80000000u;
                libLookup[(entry.ModifiedSequence, entry.Charge)] = (entry.Id, decoyId);
                libPrecursorMz[entry.Id] = entry.PrecursorMz;
            }
        }

        /// <summary>Everything a per-file <c>.reconciliation.json</c> envelope carries that is
        /// the same for every file of the run. Resolved once, before planning starts.</summary>
        private sealed class ReconciliationWriteState
        {
            public string SearchHash;
            public string LibraryHash;
            /// <summary>The multi-file stems set, sorted and deduped. It goes into every
            /// per-file envelope so a worker rescoring its single parquet can compute the
            /// join-wide reconciliation hash that --task SecondPassFDR validates against.</summary>
            public List<string> JoinFileStems;
        }

        private static ReconciliationWriteState PrepareReconciliationWrite(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, OspreyConfig config)
        {
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
            return new ReconciliationWriteState
            {
                SearchHash = config.Identity.SearchParameterHash(),
                LibraryHash = config.Identity.LibraryIdentityHash(),
                JoinFileStems = joinFileStems,
            };
        }

        /// <summary>
        /// Write ONE file's <c>.reconciliation.json</c> envelope at the Stage 5 -> Stage 6
        /// boundary, from the plan the planner just produced for it. Pairs with the
        /// <c>--task PerFileRescoring</c> Stage 6 worker mode.
        ///
        /// <para>Per file, as each file's planning ends, rather than for every file after all
        /// planning: the entries it describes can then be released immediately, which is what
        /// lets Stage 6 planning run without the all-files survivor buffer. It is also the same
        /// rule the model and the stratum now follow - an artifact is written by the phase that
        /// produces it.</para>
        /// </summary>
        /// <returns>1 if the write failed (already logged), 0 on success.</returns>
        private int WriteReconciliationFile(
            ReconciliationWriteState state,
            Stage6FilePlan filePlan,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            string fileName = filePlan.FileName;
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(sidecarBase))
            {
                ctx.LogWarning(string.Format(
                    "No sidecar base path for `{0}` - skipping reconciliation.json write", fileName));
                return 1;
            }
            string reconPath = ReconciliationFile.PathForInput(sidecarBase);
            var reconFile = BuildReconciliationFile(
                filePlan.Entries, filePlan.Actions,
                filePlan.GapFill ?? Array.Empty<GapFillTarget>(),
                filePlan.RefinedCalibration,
                state.SearchHash, state.LibraryHash, state.JoinFileStems, filePlan.GlobalBaseIds);
            PerFileResumeDriver.ClearStale(reconPath, Name);
            try
            {
                ReconciliationFile.Save(reconPath, reconFile);
                ctx.LogInfo(string.Format(
                    "Wrote reconciliation.json for {0} ({1} use_cwt + {2} forced + {3} gap-fill)",
                    fileName,
                    reconFile.UseCwtPeakActions.Count,
                    reconFile.ForcedIntegrationActions.Count,
                    reconFile.GapFillTargets.Count));
                // The third declared Output kind. All three must be stamped as they land or
                // the task stays un-resumable on whichever one was missed.
                PerFileResumeDriver.Stamp(reconPath, Name, OspreyVersion.Current,
                    ValidityKey(ctx), Array.Empty<string>(), ctx.LogWarning);
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    "Failed to write reconciliation.json for {0}: {1}", fileName, ex.Message));
                return 1;
            }
            return 0;
        }


        /// <summary>
        /// Add the base_id of every action target in one run's plan to the analysis-wide retained
        /// set. Masked to the base_id (decoy bit stripped) for the reason
        /// <see cref="RescoreCompaction"/> masks: a target and its paired decoy share a base_id,
        /// so retaining the base_id keeps both and preserves the target-decoy invariant.
        ///
        /// <para>A null <see cref="Stage6FilePlan.Actions"/> means planning did not run for this
        /// run at all - an empty consensus, a single-file analysis, or a diagnostic dump about to
        /// exit - which contributes no targets, as distinct from an empty list, which is a
        /// planned result that also contributes none. Neither is an error here.</para>
        /// </summary>
        private static void AccumulateActionTargetBaseIds(Stage6FilePlan filePlan, HashSet<uint> retained)
        {
            if (filePlan.Actions == null || filePlan.Entries == null)
                return;
            foreach (var action in filePlan.Actions)
            {
                int vecIdx = action.Key;
                if (vecIdx < 0 || vecIdx >= filePlan.Entries.Count)
                    continue;
                retained.Add(filePlan.Entries[vecIdx].EntryId & ScoringTaskShared.BASE_ID_MASK);
            }
        }

        /// <summary>
        /// Write the analysis-wide <c>.1st-pass.retained_base_ids.bin</c> summary once planning
        /// has produced the complete union. Returns false with
        /// <see cref="PipelineContext.ExitCode"/> set when the write fails.
        ///
        /// <para>A write failure is FATAL rather than a warning, unlike the per-run envelope
        /// writes this phase counts and reports. The asymmetry is deliberate: a missing envelope
        /// fails the run that needs it, loudly, at the point it is read, whereas a missing
        /// summary is silently survivable - every consumer can still rebuild the union by reading
        /// every envelope, which is exactly the O(files) pre-pass this artifact exists to
        /// delete. Degrading quietly back onto that path is how the resident behaviour would
        /// return without anyone noticing, so the phase fails instead.</para>
        ///
        /// <para>No output blib means no analysis-scope artifact to name
        /// (<see cref="RetainedBaseIdSidecar.PathFor"/> returns null), which is not a failure -
        /// it is a configuration with no analysis-wide consumer.</para>
        /// </summary>
        private static bool WriteRetainedBaseIdSummary(
            HashSet<uint> retainedBaseIds,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            string siblingPath = ScoringTaskShared.ArtifactSiblingPath(config);
            string path = RetainedBaseIdSidecar.PathFor(config.OutputBlib, siblingPath);
            if (string.IsNullOrEmpty(path))
                return true;
            try
            {
                RetainedBaseIdSidecar.Write(path, retainedBaseIds);
            }
            catch (Exception ex)
            {
                ctx.LogError(string.Format(
                    @"Failed to write the analysis-wide retained base_id summary {0}: {1}. " +
                    @"--task PerFileRescoring reads this file to compact each run without " +
                    @"re-reading every run's reconciliation.json.", path, ex.Message));
                ctx.ExitCode = 1;
                return false;
            }
            ctx.LogInfo(string.Format(
                @"Wrote analysis-wide retained base_id summary: {0} base_id(s) across {1} run(s)",
                retainedBaseIds.Count, perFileParquetPaths.Count));
            return true;
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
            PipelineContext ctx,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null)
        {
            switch (config.FdrMethod)
            {
                // Both run the same semi-supervised target-decoy framework; FdrMethod
                // rides along in the config and selects the classifier (linear SVM vs
                // gradient-boosted trees) at the two seams that touch it inside the
                // engine. Nothing else about the run differs, so there is no separate
                // Gbdt pipeline to dispatch to.
                case FdrMethod.Percolator:
                case FdrMethod.Gbdt:
                    return RunPercolatorFdr(perFileEntries, config, ctx, loadFileFeatures: loadFileFeatures);

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
        /// <c>PercolatorEngine.RunPercolatorFdr</c>: supplies the PIN
        /// feature names and routes logging through <c>ctx.LogInfo</c>. Static +
        /// internal so <see cref="SecondPassFdrTask"/> can call it for the 2nd-pass
        /// run after Stage 6 reconciliation (the HPC distribution case where
        /// workers wrote reconciled .scores.parquet but no
        /// .2nd-pass.fdr_scores.bin sidecars; mirrors Rust pipeline.rs:4394-4468).
        /// </summary>
        internal static FeatureContributions RunPercolatorFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config,
            PipelineContext ctx,
            string passLabel = "First-pass",
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null,
            PercolatorResults frozenModel = null)
        {
            // loadFileFeatures is supplied by the first-pass caller (FirstPassFdrTask.Run)
            // so the engine streams PIN features per file from parquet (issue #4355
            // Phase 4). The 2nd-pass caller (Pass2FdrSidecar) leaves it null and
            // pre-reloads features onto the stubs, so the engine reads them resident.

            // OSPREY_PASS2_QVALUE=transfer: on the FIRST-pass run only, publish the trained
            // model (FoldWeights / FoldBiases / Standardizer) as a byproduct so the
            // SecondPassFDR 2nd-pass step can re-score reconciled features with this FROZEN
            // model instead of retraining. Null (a pure no-op in the engine) on the default
            // percolator path and on the 2nd-pass run, so scoring stays byte-identical.
            Action<PercolatorResults> captureModel = null;
            if ((OspreyEnvironment.Pass2TransferQ || OspreyEnvironment.Pass2TransferCompete ||
                 OspreyEnvironment.Pass2ProteinCompact) &&
                string.Equals(passLabel, @"First-pass", StringComparison.Ordinal))
            {
                // Publish is add-only (throws on a duplicate key); guard so a first pass
                // that somehow ran twice in one process degrades to a no-op rather than a
                // raw ArgumentException. The passLabel gate already makes this unreachable
                // on normal paths.
                captureModel = results =>
                {
                    if (!ctx.TryGet<FirstPassPercolatorModel>(out _))
                    {
                        // Stamp the arm THIS pass ran under; the 2nd pass may be another process.
                        ctx.Publish(new FirstPassPercolatorModel
                        {
                            Results = results,
                            ExperimentAgg = OspreyEnvironment.ExperimentAgg
                        });
                    }
                };
            }

            bool aborted = PercolatorEngine.RunPercolatorFdr(
                perFileEntries, config,
                OspreyFeatureCalculators.BuildFeatureInfos(
                    ParquetScoreCache.PIN_FEATURE_NAMES),
                ctx.LogInfo, out var contributions,
                BuildPercolatorDiagnostics(ctx.Diagnostics), passLabel, loadFileFeatures,
                captureModel, frozenModel);
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
        /// Projection-buffer counterpart of the <see cref="FdrEntry"/>
        /// <see cref="RunPercolatorFdr(System.Collections.Generic.List{System.Collections.Generic.KeyValuePair{string,System.Collections.Generic.List{FdrEntry}}},OspreyConfig,PipelineContext,string,System.Func{string,System.Collections.Generic.IReadOnlyList{double[]}},PercolatorResults)"/>
        /// facade: run Percolator FDR over the thin <see cref="FdrProjectionSet"/>
        /// buffer, supplying the PIN feature names + the Stage 5 diagnostics config and
        /// owning the diagnostic-only process exit. Used by the projection 1st-pass
        /// span and the projection 2nd-pass path (<see cref="Pass2FdrSidecar"/>, issue
        /// #4374). The projection engine ALWAYS streams, so a per-file feature loader
        /// is mandatory.
        /// </summary>
        internal static void RunPercolatorFdr(
            FdrProjectionSet projections,
            OspreyConfig config,
            PipelineContext ctx,
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            IFdrOutputSink sink)
        {
            bool aborted = PercolatorEngine.RunPercolatorFdr(
                projections, config,
                OspreyFeatureCalculators.BuildFeatureInfos(
                    ParquetScoreCache.PIN_FEATURE_NAMES),
                ctx.LogInfo, sink, BuildPercolatorDiagnostics(ctx.Diagnostics), passLabel,
                loadFileFeatures);
            if (aborted)
            {
                // A diagnostic-only (*Only) Stage 5 dump fired; mirror the FdrEntry
                // facade -- the Tasks layer owns the process exit.
                ctx.LogInfo(@"[BISECT] Percolator diagnostic-only dump complete - aborting run");
                Environment.Exit(0);
            }
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
        /// full pre-compaction peptide pool. Sets the one experiment-wide
        /// ExperimentProteinQvalue; the second-pass protein FDR overwrites it
        /// later, and WritePass2ExperimentSidecar writes that pass-2 value into
        /// the 2nd-pass sidecar (#4559). Detected-peptide filter uses
        /// run_peptide_qvalue, the strict peptide-level gate, matching Rust
        /// pipeline.rs:3045-3049 exactly. Protein-FDR gate is config.RunFdr
        /// (1x), the Savitski-2015 convention applied at first pass, NOT the
        /// 2x relaxed gate the post-output Stage 8 protein FDR uses.
        /// </summary>
        private void RunFirstPassProteinFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
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

            // Build + publish the protein-compact stratum (legacy path). Gated on the mode:
            // it scans the full library, and it is read only by the compaction gate + pass-2.
            if (OspreyEnvironment.Pass2ProteinCompact)
            {
                BuildAndPublishProteinCompactStratum(
                    result, fullLibrary, perFileParquetPaths, ValidityKey(ctx), ctx);
            }

            if (ctx.Diagnostics?.DumpProteinFdr ?? false)
            {
                ctx.Diagnostics?.WriteStage6ProteinFdrDump(
                    result.BestScores, result.ProteinFdr.PeptideQvalues);
                if (ctx.Diagnostics?.ProteinFdrOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_PROTEIN_FDR_ONLY");
            }
        }

        /// <summary>
        /// Stage 5 -> 6 boundary: drop <c>Fragments</c> from every library entry that nothing
        /// downstream can score or write, keeping the identity fields on all of them. See
        /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> for the rationale and the
        /// safety argument.
        ///
        /// <para>The retained set is the post-compaction survivors (<c>_firstPassBaseIds</c>,
        /// already pair-symmetric so a target's decoy rides along) PLUS the gap-fill candidates.
        /// Gap-fill has to be in it: <c>GapFillTargetIdentifier</c> looks up the MISSING charge
        /// states of passing peptides through the library, so by construction it reaches
        /// entries that did NOT survive compaction and still needs their spectra.</para>
        ///
        /// <para>Called from BOTH <see cref="Run"/> (projection path) and
        /// <see cref="Rehydrate"/> (resume / bundle-adopt), which set
        /// <c>_firstPassBaseIds</c> from the compaction result and the reconciliation bundle
        /// respectively. <see cref="LibraryFragmentRelease.RunsOnThisLeg"/> decides whether this
        /// leg releases at all, and is the same predicate the validity-key suffix reads, so the
        /// key can never claim an arm the code did not run.</para>
        ///
        /// <para>Deliberately NOT gated on diagnostics. The only dump that reads fragments is
        /// <c>WriteCalXicEntryDumpAndExit</c>, a Stage 3 dump that exits the process where it
        /// stands, so it cannot observe a Stage 5 -&gt; 6 release. Gating on
        /// <c>ctx.Diagnostics</c> would instead disable this in exactly the regression run
        /// that is compared against the committed golden - <c>regression.ps1 -DumpProteinFdr</c>
        /// sets OSPREY_DUMP_STAGE7_PROTEIN_FDR, and any variable in the forced-dump bundle makes
        /// <c>ctx.Diagnostics</c> non-null - making the byte-identity gate vacuous for this
        /// feature.</para>
        /// </summary>
        private void ReleaseUnscorableLibraryFragments(List<LibraryEntry> fullLibrary, PipelineContext ctx)
        {
            // _firstPassBaseIds is the one meaningful null here: it separates the paths that
            // computed a surviving set (projection Run, bundle-adopt Rehydrate) from the legacy
            // resident path that did not. fullLibrary is not checked - it is a published
            // byproduct every other reader dereferences unguarded, so a null is a wiring fault
            // and should say so.
            if (!LibraryFragmentRelease.RunsOnThisLeg(ctx) || _firstPassBaseIds == null)
                return;

            var retained = LibraryFragmentRelease.BuildRetainedBaseIds(
                _firstPassBaseIds, _perFileGapFillForRescore);
            int released = LibraryFragmentRelease.ReleaseFragments(fullLibrary, retained);
            ctx.LogInfo(string.Format(
                @"Released library fragments for {0} of {1} entries ({2} base_ids retained for rescore + gap-fill)",
                released, fullLibrary.Count, retained.Count));
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after library-fragment release");
        }

        /// <summary>
        /// Build the protein-compact stratum: the base_ids of every library precursor whose
        /// peptide maps to a protein detected in the 1st pass by &gt;=2 DISTINCT peptides (the
        /// honest multi-hit anchor -- single-hit proteins break the independent-filtering
        /// assumption; the entrapment prototype showed &gt;=2 restores FDP control at full
        /// gain). Bounded by the library -> flat in file count. Read by the compaction gate
        /// (to admit these peptides for reconciliation) and by <c>Pass2FdrSidecar</c>'s
        /// stratified competition (to re-scope q over them).
        /// </summary>
        private static HashSet<uint> BuildProteinCompactStratum(
            FirstPassProteinFdrResult result, List<LibraryEntry> fullLibrary, Action<string> log)
        {
            // ModifiedSequence -> its protein ids (from target library entries).
            // ProteinIds is IReadOnlyList<string> after the LibraryStringInterner change (#4424).
            var pepProteins = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var e in fullLibrary)
                if (!e.IsDecoy && e.ProteinIds != null && e.ProteinIds.Count > 0 &&
                    !pepProteins.ContainsKey(e.ModifiedSequence))
                    pepProteins[e.ModifiedSequence] = e.ProteinIds;

            // Count DISTINCT detected peptides per protein; keep proteins with >=2.
            var protPepCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pep in result.DetectedPeptides)
                if (pepProteins.TryGetValue(pep, out var pids))
                    foreach (var p in pids)
                        protPepCount[p] = protPepCount.TryGetValue(p, out int c) ? c + 1 : 1;
            var present2 = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in protPepCount)
                if (kv.Value >= 2) present2.Add(kv.Key);

            // stratum = base_ids of every library precursor of a present-2 protein (target
            // and paired decoy share a base_id, so this is pair-symmetric).
            var stratum = new HashSet<uint>();
            foreach (var e in fullLibrary)
            {
                if (e.ProteinIds == null) continue;
                foreach (var p in e.ProteinIds)
                    if (present2.Contains(p)) { stratum.Add(e.Id & ~LibraryEntry.DECOY_ID_BIT); break; }
            }
            log(string.Format(
                "protein-compact: {0} proteins with >=2 detected peptides -> stratum of {1} base_ids " +
                "(from {2} detected peptides).",
                present2.Count, stratum.Count, result.DetectedPeptides.Count));
            return stratum;
        }

        /// <summary>Build the protein-compact stratum, stash it for the compaction gate
        /// (<see cref="_proteinCompactStratum"/>), publish it for the pass-2 stratified
        /// competition, and PERSIST it. Called on BOTH the legacy and projection first-pass
        /// paths so the compaction set is identical either way.
        ///
        /// <para>Persisted here, where protein FDR has just produced it, rather than at the end
        /// of <see cref="PlanStage6"/> where it used to ride out with the model. Those are two
        /// different phases and the gap between them is the whole survivor reload - the step
        /// that is the memory peak of a large cohort, and therefore the step a run is most
        /// likely to die in. Writing the stratum before it means a run that dies there resumes
        /// at the compaction gate instead of repeating the score passes and protein FDR.</para>
        ///
        /// <para>Best-effort, like the model: nothing downstream requires the file, and a
        /// resume that cannot find it recomputes.</para></summary>
        private void BuildAndPublishProteinCompactStratum(
            FirstPassProteinFdrResult result, List<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<string, string> perFileParquetPaths, string validityKey,
            PipelineContext ctx)
        {
            _proteinCompactStratum = BuildProteinCompactStratum(result, fullLibrary, ctx.LogInfo);
            ctx.Publish(new ProteinCompactStratum(_proteinCompactStratum));
            if (perFileParquetPaths == null)
                return;
            // Sorted and serialized ONCE. The copies are byte-identical, and at 446 files a
            // ~0.9 M-id stratum re-rendered per file is several GB of writes for nothing.
            string stratumJson = FirstPassModelIO.SerializeStratum(_proteinCompactStratum);
            if (stratumJson == null)
                return;
            int stratumWrites = 0;
            foreach (var kvp in perFileParquetPaths)
            {
                string path = FirstPassModelIO.StratumPathFor(kvp.Value, kvp.Key);
                PerFileResumeDriver.ClearStale(path, Name);
                try
                {
                    FirstPassModelIO.WriteText(path, stratumJson);
                    stratumWrites++;
                    PerFileResumeDriver.Stamp(path, Name, OspreyVersion.Current, validityKey,
                        new[] { kvp.Value }, ctx.LogWarning);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(@"Could not persist the protein-compact stratum for '" + kvp.Key + @"': " + ex.Message);
                }
            }
            if (stratumWrites > 0)
            {
                ctx.LogInfo(string.Format(
                    @"Persisted the protein-compact stratum ({0} file sidecar(s)).", stratumWrites));
            }
        }

        /// <summary>
        /// Projection-buffer first-pass FDR span (issue #4355 step (b) increment ii).
        /// Materializes the thin <see cref="FdrProjectionSet"/> from the cold
        /// hand-off buffer, RELEASES the <see cref="FdrEntry"/> stubs so they are not
        /// resident across the SVM peak, then drives first-pass Percolator +
        /// first-pass protein FDR + the 1st-pass sidecar write + compaction entirely
        /// off the projection. Finally reloads full <see cref="FdrEntry"/> survivors
        /// from each file's ORIGINAL parquet + the just-written 1st-pass sidecar and
        /// returns them, so <see cref="PlanStage6"/> / Stage 6 / Stage 7 consume the
        /// identical survivor buffer the legacy compacted-in-place path produced.
        /// Returns <c>null</c> (with <see cref="PipelineContext.ExitCode"/> set) only
        /// on a StopAfterStage5 sidecar-write failure or a survivor-reload fault.
        /// </summary>
        private List<KeyValuePair<string, List<FdrEntry>>> RunFirstPassProjection(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            FdrProjectionSet prebuiltProjections)
        {
            // Preferred path (issue #4397): PerFileScoring streamed these rows straight
            // out of the per-file .scores.parquet, so the fat FdrEntry stub buffer was
            // never allocated at all (it cost ~53 GB at 191M rows). Fall back to building
            // from stubs on the paths that still publish them - rehydrate / reconciled-input, or when
            // a resident pool is required. BuildFromEntries releases each file's stubs
            // incrementally (releaseStubs: true) so the projection never coexists with the
            // full stub buffer. Clearing the hand-off ScoredEntries lists is safe: nothing
            // downstream of this task reads ScoredEntries on a compute path -- the survivor
            // buffer is published as CompactedEntries.
            var projections = prebuiltProjections ??
                FdrProjectionSet.BuildFromEntries(perFileEntries, releaseStubs: true);
            int beforeCount = projections.TotalRows;
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, projections.IsCountsOnly
                ? string.Format(
                    @"projection counts-only: {0} rows across {1} files (no resident rows); FdrEntry stubs released",
                    beforeCount, projections.PerFile.Count)
                : string.Format(
                    @"projection built: {0} rows, {1} distinct peptides; FdrEntry stubs released",
                    beforeCount, projections.PeptideById.Length));

            // Stage 5: first-pass Percolator over the projection. Same SVM, same
            // dispatch, same q-values -- only the resident buffer differs. The lean
            // struct no longer stores the q-value outputs (issue #4355 struct-shrink
            // S0/S1); a StoringSink keeps ONLY {RunPeptideQ, RunProteinQ} resident (48 B)
            // that protein FDR + compaction read, and streams the other four q-values to
            // the phase-1 partial sidecar as it goes.
            //
            // Two-phase 1st-pass sidecar (issue #4355 struct-shrink S1). Phase 1: the
            // StoringSink writes each file's PARTIAL .1st-pass.fdr_scores.bin during the
            // score pass (experiment_protein_qvalue = 1.0 placeholder), so the four streamed
            // q-values are never held resident. This flush resolves the per-file sidecar
            // path the same way the pre-S1 single-phase write did, so the survivor reload
            // and the Stage 6 worker read identical bytes; it returns a per-file failure
            // count the sink accumulates (sink.PartialWriteFailures) for the
            // StopAfterStage5 gate. Phase 2 patches [52..60] after protein FDR (below).
            // Hoisted: the sidecar stamp below runs once per file inside the score pass, and
            // recomputing the key per file would hash the search + library identity 446 times.
            string sidecarValidityKey = ValidityKey(ctx);

            int FlushPartialSidecar(string fileName, IReadOnlyList<FdrScoreRecord> records)
            {
                string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogWarning(string.Format(
                        "No sidecar base path for `{0}` — skipping fdr_scores.bin write", fileName));
                    return 1;
                }
                string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);
                // Cleared BEFORE the write so a marker from an earlier invocation can never
                // outlive the file it vouches for, and stamped AFTER, so the marker's presence
                // means this build finished this file. FdrScoresSidecar.Write commits through
                // FileSaver, i.e. an atomic rename, so the sidecar is absent or complete and
                // never half-written; the marker adds the one thing existence cannot say, which
                // is WHICH task and which validity key produced it.
                //
                // Per file, at write time, on purpose. The driver stamps declared outputs only
                // after Run returns, and this score pass takes 137 minutes over 446 files - so a
                // machine lost partway (the Windows-Update case) previously left every finished
                // sidecar unmarked and unusable, and the restart re-scored all 446. Stamping
                // here makes recovery proportional to work completed.
                PerFileResumeDriver.ClearStale(fdrPath, Name);
                try
                {
                    FdrScoresSidecar.Write(fdrPath, records, FdrScoresSidecar.Pass.FirstPass);
                    string parquetPath;
                    perFileParquetPaths.TryGetValue(fileName, out parquetPath);
                    PerFileResumeDriver.Stamp(fdrPath, Name, OspreyVersion.Current, sidecarValidityKey,
                        parquetPath == null ? Array.Empty<string>() : new[] { parquetPath },
                        ctx.LogWarning);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to write 1st-pass fdr_scores.bin for {0}: {1}", fileName, ex.Message));
                    return 1;
                }
                return 0;
            }

            // --model-diagnostics: build the streaming report accumulator (entrapment
            // classification from the searched library) and hand it to the score-pass sink, which
            // folds every pre-compaction row into it; capture the trained model for the Model tab.
            // Null off the report path, so byte-neutral there.
            var mdiagAccumulator = config.ModelDiagnostics
                ? BuildModelDiagnosticsAccumulator(projections.PerFile.ConvertAll(kv => kv.Key),
                    ctx.Get<LibraryById>().Value, config, ctx.LogInfo)
                : null;
            FeatureContributions mdiagContributions = null;
            Action<FeatureContributions> captureContributions = null;
            if (mdiagAccumulator != null)
                captureContributions = c => mdiagContributions = c;

            // OSPREY_PASS2_QVALUE=transfer / transfer-compete / protein-compact: publish the
            // trained (frozen) 1st-pass model so the SecondPassFDR 2nd pass can re-score the
            // reconciled features with it instead of retraining (transfer-compete / protein-compact
            // then recompute q/PEP by a fresh target-decoy competition). The projection first pass
            // (this method) is the SAME lean path the default percolator mode takes, so the model
            // must be captured HERE, not on the resident RunPercolatorFdr overload. Null (a pure
            // no-op in the engine) on the default path, so scoring stays byte-identical. Streaming
            // only, so no full-population pool is held resident (avoids the entrapment-library OOM).
            //
            // The model is also PERSISTED here, the moment training returns it. It used to be
            // written at the end of PlanStage6 instead, which on a 446-file cohort is 228
            // minutes after it was computed - so a run killed anywhere in the score passes,
            // protein FDR or the survivor reload left a few hundred KB of finished state
            // nowhere on disk and had to retrain from scratch. Training is the first phase, so
            // persisting its product when it ends is what makes every later phase resumable.
            var reloadedModel = LoadCurrentModelSidecar(perFileParquetPaths, sidecarValidityKey);
            Action<PercolatorResults> captureModel = results =>
            {
                if ((OspreyEnvironment.Pass2TransferQ || OspreyEnvironment.Pass2TransferCompete ||
                     OspreyEnvironment.Pass2ProteinCompact) &&
                    !ctx.TryGet<FirstPassPercolatorModel>(out _))
                {
                    // Stamp the arm THIS pass ran under; the 2nd pass may be another process.
                    ctx.Publish(new FirstPassPercolatorModel
                    {
                        Results = results,
                        // The arm the TRAINING process ran under, not this one's. When the model
                        // came off disk those differ, and the recorded arm is what the pass-2
                        // mean-best-N refusal is evaluated against - so re-reading the
                        // environment here would judge a reused model by the wrong arm.
                        ExperimentAgg = reloadedModel?.ExperimentAgg ?? OspreyEnvironment.ExperimentAgg
                    });
                }
                // Nothing to write when the model came off disk: it is already there, already
                // stamped, and rewriting it would replace an artifact a marker attests with a
                // byte-identical copy the marker no longer describes.
                if (reloadedModel == null)
                    PersistFirstPassModel(results, perFileParquetPaths, sidecarValidityKey, ctx);
            };

            // Collapses the score pass's EXPERIMENT-scope columns to one record per distinct
            // entry_id (format v5, issue #4486). Protein FDR fills its protein q below, then
            // the whole thing is written once beside the blib.
            // PER-FILE RESUME GATE. Which files already carry a 1st-pass sidecar this build
            // wrote, under this arm and this cohort? Those need no re-scoring: the sidecar holds
            // their scores and run q-values, and the sink can be fed from it.
            //
            // Per FILE rather than per phase on purpose. A phase-level gate still redoes a
            // 99%-complete phase, and this score pass is 137 minutes over 446 files - so a
            // machine lost partway (a Windows Update, a blue screen) has to resume proportional
            // to what it finished, not to what the phase was.
            //
            // Presence alone is not the test. FdrScoresSidecar.Write commits through FileSaver,
            // so a sidecar that exists is complete - but completeness is not identity, and
            // IsCurrent also demands the validity key, which carries the search and library
            // hashes, the pick arm, the pass-2 mode AND the cohort (the reconciliation hash is
            // taken over the sorted input file stems). Drop 82 more files into a directory and
            // every existing sidecar stops being current, which is exactly right.
            var resumableFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in projections.PerFile)
            {
                string resumeBase = ScoringTaskShared.ResolveSidecarBasePath(
                    kv.Key, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(resumeBase))
                    continue;
                if (PerFileResumeDriver.IsCurrent(
                        FdrScoresSidecar.Pass1Path(resumeBase), Name, sidecarValidityKey))
                    resumableFiles.Add(kv.Key);
            }
            if (resumableFiles.Count > 0)
            {
                // Named, not just counted: an operator reading this needs to see WHICH files were
                // taken from disk, because a wrong-cohort adoption would look identical to a
                // right one in a bare count.
                ctx.LogInfo(string.Format(
                    @"Resume: {0} of {1} file(s) already carry a current 1st-pass sidecar and will " +
                    @"not be re-scored ({2} to score).",
                    resumableFiles.Count, projections.PerFile.Count,
                    projections.PerFile.Count - resumableFiles.Count));
                foreach (var kv in projections.PerFile)
                {
                    if (resumableFiles.Contains(kv.Key))
                        ctx.LogInfo(string.Format(@"  resume-skip: {0}", kv.Key));
                }
            }

            // FORWARD SCAN: with every file's sidecar current AND the analysis-wide experiment
            // sidecar current, everything from here to the compaction gate has already been done
            // and is on disk. Re-running it is not a safety margin, it is 20 of this task's 29
            // minutes at 86 files spent reproducing artifacts byte-for-byte:
            //
            //   pass 1 builds the experiment competition, whose result IS the experiment sidecar;
            //   pass 2 emits the per-file sidecars, which are what the gate above just matched;
            //   protein FDR's q-values are read back out of the experiment sidecar by the gate;
            //   model diagnostics is a report.
            //
            // So resume where the work actually stops: at ComputeFirstPassBaseIds, which streams
            // the finalized sidecars off disk rather than reading anything this block would have
            // built. The two byproducts the skipped region publishes are taken from the per-file
            // .1st-pass.model.json instead - the model and the protein-compact stratum - so a
            // consumer sees the same values from the same run that produced the scores.
            //
            // Deliberately all-or-nothing: one missing sidecar means the competition would differ,
            // so anything short of complete falls through and recomputes.
            string experimentPathForResume = FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config),
                FdrScoresSidecar.Pass.FirstPass);
            FirstPassModelIO.Sidecar resumeSidecar = null;
            // Each condition reported by name when it refuses. A silent fall-through here looks
            // exactly like a run that never had the artifacts, and the whole point of the fast
            // path is that an operator can tell why they are waiting 29 minutes instead of 8.
            if (projections.PerFile.Count > 0 && resumableFiles.Count == projections.PerFile.Count)
            {
                var refusals = new List<string>();
                if (string.IsNullOrEmpty(experimentPathForResume))
                    refusals.Add(@"no experiment-sidecar path (no output blib to name it after)");
                else if (!PerFileResumeDriver.IsCurrent(experimentPathForResume, Name, sidecarValidityKey))
                    refusals.Add(string.Format(@"experiment sidecar not current: {0}", experimentPathForResume));
                var probe = FirstPassModelIO.LoadFromAny(perFileParquetPaths);
                if (probe == null)
                    refusals.Add(@"no readable .1st-pass.model.json beside any input parquet");
                else if (probe.Model == null)
                    refusals.Add(@".1st-pass.model.json carries no model");
                else if (OspreyEnvironment.Pass2ProteinCompact && probe.StratumBaseIds == null)
                    refusals.Add(@"no protein-compact stratum (.1st-pass.stratum.json, or the " +
                                 @"legacy field in .1st-pass.model.json)");
                if (refusals.Count > 0)
                {
                    ctx.LogInfo(string.Format(
                        @"Resume: every sidecar is current but the compaction-gate entry was refused ({0}); " +
                        @"the score passes will run.", string.Join(@"; ", refusals)));
                }
            }
            bool canEnterAtGate =
                projections.PerFile.Count > 0 &&
                resumableFiles.Count == projections.PerFile.Count &&
                !string.IsNullOrEmpty(experimentPathForResume) &&
                PerFileResumeDriver.IsCurrent(experimentPathForResume, Name, sidecarValidityKey) &&
                (resumeSidecar = FirstPassModelIO.LoadFromAny(perFileParquetPaths)) != null &&
                resumeSidecar.Model != null &&
                // protein-compact's gate admits present-protein peptides through the stratum, so
                // entering at the gate WITHOUT it selects a different, smaller survivor set - and
                // does it silently, because nothing downstream can tell a stratum that was never
                // loaded from one that was legitimately empty. The stratum is its own artifact
                // (.1st-pass.stratum.json, written when protein FDR ends), so a run killed
                // before protein FDR has the model and no stratum and correctly falls through to
                // recompute; LoadFromAny pairs the two from the same stem.
                (!OspreyEnvironment.Pass2ProteinCompact || resumeSidecar.StratumBaseIds != null);
            if (canEnterAtGate)
            {
                ctx.LogInfo(string.Format(
                    @"Resume: all {0} sidecars and the experiment sidecar are current - skipping the " +
                    @"score passes, protein FDR and model diagnostics, and entering at the compaction gate.",
                    projections.PerFile.Count));
                ctx.Publish(new FirstPassPercolatorModel
                {
                    Results = resumeSidecar.Model,
                    ExperimentAgg = resumeSidecar.ExperimentAgg
                });
                // Gated on the MODE, matching the canEnterAtGate clause above. The stratum file
                // is not a declared Output, so nothing invalidates or removes it when a later
                // run uses a different pass-2 arm - and the compaction gate applies whatever it
                // is handed, so an ungated adoption would silently widen the survivor set of an
                // arm that never computes a stratum at all.
                if (OspreyEnvironment.Pass2ProteinCompact && resumeSidecar.StratumBaseIds != null)
                {
                    _proteinCompactStratum = resumeSidecar.StratumBaseIds;
                    ctx.Publish(new ProteinCompactStratum(_proteinCompactStratum));
                    ctx.LogInfo(string.Format(
                        @"Resume: reloaded the persisted protein-compact stratum ({0} base ids).",
                        _proteinCompactStratum.Count));
                }
                return CompactFromSidecars(projections, perFileParquetPaths, beforeCount, config, ctx);
            }

            var experiment = new FdrExperimentAccumulator();
            var sink = new FdrStoringSink(projections, config, @"First-pass", FlushPartialSidecar,
                experiment, mdiagAccumulator);

            // Which files' 1st-pass sidecars are on disk. Seeded with the ones an earlier run
            // left, and GROWN by pass 1 as it writes each file it scores - which is what lets
            // pass 2 read every score back instead of reloading features and repeating the dot
            // product for all of them. An adopted file is marked on the sink for the same reason
            // a freshly written one is: its sidecar exists, so the sink must not write it again.
            var scoresOnDisk = new HashSet<string>(resumableFiles, StringComparer.Ordinal);
            for (int f = 0; f < projections.PerFile.Count; f++)
            {
                if (resumableFiles.Contains(projections.PerFile[f].Key))
                    sink.MarkSidecarWritten(f);
            }

            // Pass 1 hands each file's finished run-scope output straight to the sidecar writer.
            // Nothing about it is provisional: the score and both run q-values are final the
            // moment that file's rows have been walked, and no later phase revises them.
            int pass1WriteFailures = 0;
            FileRunScopeSink flushFileRunScope =
                (fileName, fileIndex, rowCount, entryIds, scores, runPrecQ, runPeptQ) =>
                {
                    var records = new List<FdrScoreRecord>(rowCount);
                    for (int r = 0; r < rowCount; r++)
                        records.Add(new FdrScoreRecord(entryIds[r], scores[r], runPrecQ[r], runPeptQ[r]));
                    // Marked before the result is known: a failed write must not be retried by
                    // the sink either, because FdrScoresSidecar registers the path on the way in
                    // and would refuse the second attempt as a double write.
                    sink.MarkSidecarWritten(fileIndex);
                    int failures = FlushPartialSidecar(fileName, records);
                    if (failures == 0)
                    {
                        scoresOnDisk.Add(fileName);
                        return;
                    }
                    // Fatal HERE, naming the write that failed. Since the sink can no longer
                    // write this file either, continuing produces a run that walks every
                    // remaining row and then dies an hour later in the compaction gate saying
                    // the sidecar could not be READ - blaming a missing artifact instead of the
                    // write that never happened.
                    pass1WriteFailures += failures;
                    throw new IOException(string.Format(
                        @"First-pass sidecar write failed for '{0}'. The score pass cannot " +
                        @"continue: these sidecars are write-once, so no later phase can " +
                        @"supply the file, and every downstream stage reads it. See the " +
                        @"warning above for the underlying cause.", fileName));
                };
            var featureInfos = OspreyFeatureCalculators.BuildFeatureInfos(ParquetScoreCache.PIN_FEATURE_NAMES);
            var swFdr = Stopwatch.StartNew();
            bool aborted;
            if (projections.IsCountsOnly)
            {
                // Stage B (issue #4355 struct-shrink S3): the lean 1st pass holds NO resident
                // FdrProjection[] -- stream every row's identity + features from the per-file
                // .scores.parquet. The row source reads the scalar columns (entry_id / charge /
                // is_decoy / coelution_sum / modseq) in parquet row order (== the resident sort
                // order on the 1st pass, since the parquet is written (entry_id,charge,scan)-sorted),
                // and loadFileFeatures loads that file's feature vectors by the running row ordinal.
                // perFileParquetPaths has every projection file (the counts-only producer read the
                // same parquet to count its rows), so the indexer cannot miss.
                Action<string, Action<uint, byte, bool, double, string>> streamFileRows =
                    (fileName, onRow) => ParquetScoreCache.ReadFdrStubScalars(perFileParquetPaths[fileName], onRow);
                // Feeds the scorer a file's scores off its 1st-pass sidecar so the pass does not
                // load that file's feature vectors or re-run the dot product. Consulted by BOTH
                // passes, and the set grows during pass 1 - so on a cold run this is what stops
                // pass 2 recomputing all 446 files' scores 82 minutes after pass 1 computed
                // them. Returns false for a file whose sidecar is not on disk, which scores
                // normally.
                Func<string, Action<uint, double>, bool> tryStreamCompletedScores =
                    (fileName, onScore) =>
                    {
                        if (!scoresOnDisk.Contains(fileName))
                            return false;
                        string doneBase = ScoringTaskShared.ResolveSidecarBasePath(
                            fileName, perFileParquetPaths, config);
                        if (string.IsNullOrEmpty(doneBase))
                            return false;
                        return FdrScoresSidecar.ReadRecords(
                            FdrScoresSidecar.Pass1Path(doneBase), FdrScoresSidecar.Pass.FirstPass,
                            rec => onScore(rec.EntryId, rec.Score));
                    };
                // Training is the one phase whose product does not depend on how far the run
                // got: the model is a function of the cohort, the library, the arm and the
                // seed, all of which the validity key covers. So a CURRENT .1st-pass.model.json
                // is reusable however many files were scored - which is the whole point of
                // writing it when training ends rather than at the end of the task. It saves 21
                // minutes of training-subset feature loading at 446 files.
                //
                // Reused rather than skipped: the scorer still publishes it through captureModel,
                // so a second pass that needs the frozen first-pass model gets the SAME model the
                // scores on disk were produced by. Synthesising a stub to satisfy the arithmetic
                // would publish a meaningless model and corrupt pass 2 silently.
                //
                // The marker is what makes partial reuse safe. The all-sidecars-current gate
                // below corroborates cohort identity through the sidecars themselves; here there
                // is no such corroboration, so an unstamped model file - one written before this
                // artifact was stamped - is not adopted.
                // Requires at least one file to actually be resumable. The model file is not a
                // declared Output, so deleting the declared ones - the standard way to force a
                // clean re-score - leaves it behind, and adopting it then would score every file
                // with the previous arm's discriminant while the log says only that a model was
                // reused. With one sidecar adopted there is already an on-disk score that model
                // produced, so reusing it is the consistent choice rather than a surprising one.
                PercolatorResults pretrainedModel = resumableFiles.Count > 0 ? reloadedModel?.Model : null;
                if (pretrainedModel != null)
                {
                    ctx.LogInfo(string.Format(
                        @"Resume: reusing the persisted 1st-pass model instead of retraining " +
                        @"({0} of {1} file(s) already scored).",
                        resumableFiles.Count, projections.PerFile.Count));
                }
                else if (reloadedModel?.Model != null)
                {
                    ctx.LogInfo(
                        @"Resume: a current 1st-pass model is on disk but no file's scores are, " +
                        @"so the model is retrained rather than adopted for a full re-score.");
                }
                aborted = PercolatorEngine.RunFirstPassStreaming(
                    projections.PerFile.ConvertAll(kv => kv.Key), streamFileRows, loadFileFeatures,
                    config, featureInfos, ctx.LogInfo, sink, BuildPercolatorDiagnostics(ctx.Diagnostics),
                    @"First-pass", captureContributions, captureModel, tryStreamCompletedScores,
                    pretrainedModel, flushFileRunScope);
                // Says whether the pass-1 write actually engaged. Without it a run in which
                // flushFileRunScope never fired would look identical from the outside - the
                // sink would have written the same sidecars from pass 2, and the output would
                // be byte-identical - so the log is the only place the distinction is visible.
                ctx.LogInfo(string.Format(
                    @"First-pass: {0} of {1} file(s) had their sidecar written during pass 1, so " +
                    @"pass 2 read those scores back instead of reloading features.",
                    scoresOnDisk.Count, projections.PerFile.Count));
            }
            else
            {
                aborted = PercolatorEngine.RunPercolatorFdr(
                    projections, config, featureInfos,
                    ctx.LogInfo, sink, BuildPercolatorDiagnostics(ctx.Diagnostics),
                    @"First-pass", loadFileFeatures, captureContributions, captureModel);
            }
            swFdr.Stop();
            if (aborted)
            {
                // A diagnostic-only (*Only) Stage 5 dump fired; mirror the static
                // RunPercolatorFdr wrapper's process exit.
                ctx.LogInfo(@"[BISECT] Percolator diagnostic-only dump complete - aborting run");
                Environment.Exit(0);
            }
            ctx.LogInfo(string.Format(@"[TIMING] Percolator/Simple FDR: {0:F1}s",
                swFdr.Elapsed.TotalSeconds));
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after first-pass Percolator FDR");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"first-pass-fdr-live",
                string.Format(@"(post-GC, projection path, rows={0})", projections.TotalRows));

            LogFirstPassResultsProjection(projections, sink, config, ctx);

            // --model-diagnostics: emit the pass-1 HTML report from the streamed accumulator.
            // Placed here -- after first-pass Percolator, BEFORE first-pass protein FDR -- to
            // match the resident path's report point (LogFirstPassResultsAndDump runs before
            // RunFirstPassProteinFdr), so neither build reads a protein q the report does not use.
            // The CAL view comes from the Stage-3 per-file byproduct; the Model tab from the
            // captured first-pass contributions.
            if (mdiagAccumulator != null)
            {
                var cal = BuildCalibrationData(ctx, projections.PerFile.ConvertAll(kv => kv.Key));
                // The peak co-assignment panel (issue #4522) needs each row's DETECTION apex RT,
                // which the streamed fold never sees: this path carries no RT at all. Rebuild it
                // from the per-file sidecars just flushed by the score pass, joined to their
                // parquet apex_rt. Reusing the accumulator's classification avoids re-running the
                // multi-minute library classification for the same answer.
                // From the in-memory accumulator, NOT the sidecar: this runs before protein FDR,
                // and the experiment sidecar is not written until after it. The three columns the
                // panel reads - experiment precursor q, peptide q and the aggregate - are all
                // final here; only the protein q, which the panel does not use, is still pending.
                var coAssignment = PeakCoAssignmentSource.Build(
                    projections.PerFile.ConvertAll(kv => kv.Key), perFileParquetPaths, config,
                    mdiagAccumulator.ClassByBaseId, ctx.Get<LibraryById>().Value,
                    experiment.Records, ctx.LogInfo);
                ModelDiagnosticsReport.WriteFromAccumulator(
                    mdiagAccumulator, mdiagContributions, cal, config, ctx.LogInfo, coAssignment,
                    ValidityKey(ctx));
            }

            // First-pass protein FDR streamed off the per-file sidecar + parquet scalars
            // (issue #4355 struct-shrink S2): read each file's Score / run_peptide_qvalue
            // from the just-written .1st-pass.fdr_scores.bin, joined by entry_id with the
            // modseq / IsDecoy from the parquet scalars, run the identical parsimony +
            // picked-protein FDR, and patch each row's experiment_protein_qvalue [52..60] straight
            // back onto the sidecar -- so the resident FdrProjectionOutputs array is gone.
            // The Stage-6 diagnostic dump reads the returned artifacts, exactly as the
            // FdrEntry path's RunFirstPassProteinFdr does. Runs unconditionally (not gated
            // on --protein-fdr), matching Rust where config.protein_fdr is a plain f64
            // (default 0.01), gated only on !can_skip_fdr.
            int patchFailures = 0;
            if (projections.TotalRows > 0)
            {
                ctx.LogInfo(string.Empty);
                var swProt = Stopwatch.StartNew();
                var proteinResult = RunFirstPassProteinFdrStreaming(
                    projections, perFileParquetPaths, fullLibrary, config, ctx, experiment,
                    out patchFailures);
                if (proteinResult == null)
                    return null;  // streaming sidecar / parquet read fault; ExitCode already set
                if (ctx.Diagnostics?.DumpProteinFdr ?? false)
                {
                    ctx.Diagnostics?.WriteStage6ProteinFdrDump(
                        proteinResult.BestScores, proteinResult.ProteinFdr.PeptideQvalues);
                    if (ctx.Diagnostics?.ProteinFdrOnly ?? false)
                        OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_PROTEIN_FDR_ONLY");
                }
                swProt.Stop();
                ctx.LogInfo(string.Format(@"[TIMING] First-pass protein FDR: {0:F1}s",
                    swProt.Elapsed.TotalSeconds));

                // Build + publish the protein-compact stratum on the PROJECTION (production)
                // path too -- the compaction gate below (ComputeFirstPassBaseIds) reads it to
                // admit present-protein peptides that did not pass 1st-pass FDR.
                if (OspreyEnvironment.Pass2ProteinCompact)
                {
                    BuildAndPublishProteinCompactStratum(
                        proteinResult, fullLibrary, perFileParquetPaths, sidecarValidityKey, ctx);
                }
            }

            // The experiment-scope columns are complete now that protein FDR has resolved its
            // half, so write the one analysis-wide sidecar. It must land BEFORE
            // ComputeFirstPassBaseIds below, which reads its protein q for the compaction
            // predicate's protein-rescue clause.
            int experimentFailures = WriteExperimentSidecar(
                experiment, FdrScoresSidecar.Pass.FirstPass, config, ctx);

            // Combine the per-file write failures the sink accumulated during the score pass
            // with the protein-q resolve failures and the experiment write for the
            // StopAfterStage5 boundary gate.
            int sidecarFailures =
                sink.PartialWriteFailures + pass1WriteFailures + patchFailures + experimentFailures;
            if (sidecarFailures > 0 && config.StopAfterStage5)
            {
                ctx.LogError(string.Format(
                    @"--task FirstPassFDR: {0}/{1} 1st-pass fdr_scores.bin sidecar " +
                    @"writes failed; boundary file pair is incomplete. See warnings above.",
                    sidecarFailures, projections.PerFile.Count));
                ctx.ExitCode = 1;
                return null;
            }

            // Compaction predicate streamed over the finalized per-file sidecar -> passing
            // base_id set (identical to CompactFirstPass's non-bundle branch, risk #7). The
            // stratum (protein-compact) admits present-protein peptides that failed 1st-pass FDR.
            return CompactFromSidecars(projections, perFileParquetPaths, beforeCount, config, ctx);
        }

        /// <summary>
        /// The persisted 1st-pass model, but only when a marker attests it was written by this
        /// build for THIS cohort and arm - otherwise null, and the caller trains as it always
        /// did. Returns the first current copy found; the per-file copies are identical, so any
        /// one is authoritative.
        ///
        /// <para>Marker-checked, unlike <see cref="FirstPassModelIO.LoadFromAny"/>. That reader
        /// is only reached once every per-file sidecar has already matched this validity key, so
        /// the cohort is established by the time it runs. A model reused on a PARTIAL resume has
        /// no such corroboration: nothing else in the directory would contradict a model trained
        /// on a different cohort, and adopting one would silently score the run with the wrong
        /// discriminant.</para>
        /// </summary>
        private FirstPassModelIO.Sidecar LoadCurrentModelSidecar(
            IReadOnlyDictionary<string, string> perFileParquetPaths, string validityKey)
        {
            if (perFileParquetPaths == null)
                return null;
            foreach (var kvp in perFileParquetPaths)
            {
                string path = FirstPassModelIO.PathFor(kvp.Value, kvp.Key);
                if (!PerFileResumeDriver.IsCurrent(path, Name, validityKey))
                    continue;
                var sidecar = FirstPassModelIO.Load(path);
                if (sidecar?.Model != null)
                    return sidecar;
            }
            return null;
        }

        /// <summary>
        /// Persist the trained 1st-pass model beside each file's other Stage-5 sidecars, the
        /// moment training produces it. Written per file (identical copies) so a distributed
        /// <c>--task SecondPassFDR</c> node finds it by the same input-file stem it uses for
        /// every other reconciled sidecar.
        ///
        /// <para>Best-effort: a write failure must not fail the run, because nothing downstream
        /// requires the file to exist - SecondPassFDR keeps its pre-existing fail-fast and a
        /// resume simply retrains. <see cref="FirstPassModelIO.Save"/> is a no-op for the GBDT
        /// and degenerate models, which carry no linear weights to persist.</para>
        /// </summary>
        private void PersistFirstPassModel(
            PercolatorResults results, IReadOnlyDictionary<string, string> perFileParquetPaths,
            string validityKey, PipelineContext ctx)
        {
            if (results == null || perFileParquetPaths == null)
                return;
            int modelWrites = 0;
            foreach (var kvp in perFileParquetPaths)
            {
                string path = FirstPassModelIO.PathFor(kvp.Value, kvp.Key);
                // Cleared before the write and stamped after, so a marker can never outlive the
                // file it vouches for. Save commits through FileSaver, so the artifact itself is
                // absent or complete; the marker adds which task, build and validity key made it.
                PerFileResumeDriver.ClearStale(path, Name);
                try
                {
                    if (!FirstPassModelIO.Save(path, results, OspreyEnvironment.ExperimentAgg))
                        continue;
                    modelWrites++;
                    PerFileResumeDriver.Stamp(path, Name, OspreyVersion.Current, validityKey,
                        new[] { kvp.Value }, ctx.LogWarning);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(@"Could not persist 1st-pass model sidecar for '" + kvp.Key + @"': " + ex.Message);
                }
            }
            if (modelWrites > 0)
            {
                ctx.LogInfo(string.Format(
                    @"Persisted the trained 1st-pass model ({0} file sidecar(s)); a run interrupted " +
                    @"after this point resumes without retraining.", modelWrites));
            }
        }

        /// <summary>
        /// The compaction gate, the survivor reload, and the count line - the part of Stage 5
        /// that reads its inputs off disk rather than out of the score pass.
        ///
        /// <para>Shared by the compute path and the resume path deliberately. The resume path
        /// exists precisely because these three steps are the only ones whose inputs are NOT
        /// already on disk in finished form, so a second copy of them would be the one thing
        /// guaranteed to drift.</para>
        /// </summary>
        private List<KeyValuePair<string, List<FdrEntry>>> CompactFromSidecars(
            FdrProjectionSet projections,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            int beforeCount,
            OspreyConfig config,
            PipelineContext ctx)
        {
            var firstPassBaseIds = ComputeFirstPassBaseIds(
                projections, perFileParquetPaths, config, ctx, _proteinCompactStratum,
                out var rowsPerBaseId);
            if (firstPassBaseIds == null)
                return null;  // streaming sidecar read fault; ExitCode already set
            _firstPassBaseIds = firstPassBaseIds;

            // The survivor LOADER, not a survivor buffer. Collecting every file's survivors
            // into one list is what made this step the memory peak of a large cohort - 289 M
            // entries and ~100 GB at 446 files (issue #4526) - and nothing downstream needs
            // them together: Stage 6 planning walks the files, and so do Stage 6 and Stage 7.
            // So the buffer is never built, and the per-file lists below stay empty; every
            // consumer takes a file from the loader and drops it again.
            //
            // The loader reads the ORIGINAL parquet + the 1st-pass sidecar, so ParquetIndex
            // comes from LoadFdrStubsFromParquet on the original parquet (risk #9), keeping
            // Stage 6's positional CWT lookup byte-identical -- the same parquet + sidecar
            // round-trip modes 2/3 already validate.
            _survivorLoader = new FirstPassSurvivorLoader(
                perFileParquetPaths, config, firstPassBaseIds, ctx.Get<SequencePool>().Value);

            // Summing the per-base_id row counts over the passing set gives exactly what a
            // materialized reload would have counted: a survivor is a parquet row whose
            // base_id passed, and the sidecar carries one record per parquet row.
            int afterCount = 0;
            foreach (uint baseId in firstPassBaseIds)
            {
                if (rowsPerBaseId.TryGetValue(baseId, out int rows))
                    afterCount += rows;
            }
            ctx.LogInfo(string.Format(
                @"First-pass compaction: {0} -> {1} entries ({2} passing base_ids)",
                beforeCount, afterCount, firstPassBaseIds.Count));
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"after Stage-5 CompactFirstPass");

            // OSPREY_STAGE6_STREAM_SURVIVORS=0 keeps the materialized buffer: it is the A/B
            // byte-identity oracle for the streamed default, and it can only be that if it
            // still produces the buffer Stage 6 would read. It is guarded (and refused without
            // OSPREY_ALLOW_UNFIXED_RESIDENT) precisely because it is the O(files) shape.
            if (!OspreyEnvironment.Stage6StreamSurvivors)
                return ReloadFirstPassSurvivors(projections, afterCount, ctx);

            var survivors = new List<KeyValuePair<string, List<FdrEntry>>>(projections.PerFile.Count);
            foreach (var kvp in projections.PerFile)
                survivors.Add(new KeyValuePair<string, List<FdrEntry>>(kvp.Key, new List<FdrEntry>()));
            _survivorsStreamed = true;
            return survivors;
        }

        /// <summary>
        /// Collect every file's survivors into one buffer - the O(files) shape the streamed
        /// default exists to avoid (289 M entries and ~100 GB at 446 files, issue #4526). Kept
        /// only for <c>OSPREY_STAGE6_STREAM_SURVIVORS=0</c>, which is the A/B byte-identity
        /// oracle for the streamed path and would be no oracle at all if it stopped producing
        /// the buffer.
        ///
        /// <para><paramref name="expectedCount"/> is the survivor count the compaction gate
        /// derived arithmetically, from rows-per-base_id summed over the passing set. This is
        /// the one path that also has the materialized answer, so it checks the two against
        /// each other: a mismatch means the gate's count is wrong on EVERY path, including the
        /// streamed one where nothing else could notice.</para>
        /// </summary>
        private List<KeyValuePair<string, List<FdrEntry>>> ReloadFirstPassSurvivors(
            FdrProjectionSet projections, int expectedCount, PipelineContext ctx)
        {
            var survivors = new List<KeyValuePair<string, List<FdrEntry>>>(projections.PerFile.Count);
            // Per-file progress: reloading each file's survivor stubs from parquet + the 1st-pass
            // sidecar was the ~70 s silent "First-pass compaction" gap at 82 files. Console-only.
            using (var reloadProgress = new ProgressReporter(
                       string.Format(@"Reloading first-pass survivors from {0} file(s)", projections.PerFile.Count),
                       projections.PerFile.Count))
            {
                int reloadDone = 0;
                int loadedCount = 0;
                foreach (var kvp in projections.PerFile)
                {
                    reloadProgress.Report(++reloadDone);
                    var stubs = _survivorLoader.Load(kvp.Key, out string error);
                    if (stubs == null)
                    {
                        ctx.LogError(error);
                        ctx.ExitCode = 1;
                        return null;
                    }
                    loadedCount += stubs.Count;
                    survivors.Add(new KeyValuePair<string, List<FdrEntry>>(kvp.Key, stubs));
                }
                if (loadedCount != expectedCount)
                {
                    throw new InvalidOperationException(string.Format(
                        @"First-pass compaction counted {0} survivors from the passing base_id set " +
                        @"but the reload produced {1}. The reported compaction boundary is wrong on " +
                        @"every path, including the streamed one that has nothing to compare against.",
                        expectedCount, loadedCount));
                }
            }
            return survivors;
        }

        /// <summary>
        /// Projection-buffer counterpart of <see cref="LogFirstPassResults"/>: log
        /// per-file and total first-pass passing-target counts from the projection
        /// rows. (The OSPREY_DUMP_PERCOLATOR pre-compaction dump is a cross-impl
        /// bisection diagnostic that needs the full FdrEntry buffer the projection
        /// path deliberately does not hold; it is not emitted on the projection path,
        /// which is production/gate-only.)
        /// </summary>
        private void LogFirstPassResultsProjection(
            FdrProjectionSet projections, FdrStoringSink sink,
            OspreyConfig config, PipelineContext ctx)
        {
            // Read the per-file passing-target counts the sink accumulated during the
            // score pass (!IsDecoy && EffectiveRunQvalue <= RunFdr) rather than
            // recomputing EffectiveRunQvalue off the resident q-value array: at S1
            // RunPrecursorQvalue is no longer resident, so it cannot be recomputed for
            // FdrLevel.Precursor. The tally is the identical predicate the tail [COUNT]
            // block uses, so the logged counts are unchanged.
            var filePassingTargets = sink.FilePassingTargets;
            int passingTargets = 0;
            for (int f = 0; f < projections.PerFile.Count; f++)
            {
                int fileTargets = filePassingTargets[f];
                ctx.LogInfo(string.Format(@"  {0}: {1} precursors at {2:P1} run-level FDR",
                    projections.PerFile[f].Key, fileTargets, config.RunFdr));
                passingTargets += fileTargets;
            }
            ctx.LogInfo(string.Format(@"Total: {0} precursors pass run-level FDR across all files",
                passingTargets));
        }

        /// <summary>
        /// First-pass protein FDR streamed off disk (issue #4355 struct-shrink S2), replacing
        /// the resident-projection overload + the separate phase-2 sidecar patch. Pass 1
        /// builds the detected-peptide set + per-peptide best scores by streaming, per file in
        /// projection order, the just-written <c>.1st-pass.fdr_scores.bin</c> (Score +
        /// run_peptide_qvalue keyed by entry_id) joined with the parquet scalars (the modseq
        /// PeptideById was interned from + IsDecoy) into a pure
        /// <see cref="FirstPassProteinFdrAccumulator"/>, which runs the identical parsimony +
        /// picked-protein FDR. Pass 2 patches each file's <c>experiment_protein_qvalue</c>
        /// <c>[52..60]</c> from the reducer's peptide -> q map (folding the resident
        /// <c>PropagateProteinQvalues</c> + the old phase-2 patch into one streaming pass).
        /// Returns <c>null</c> (ExitCode set) on any sidecar / parquet read fault -- the task
        /// just wrote these files, so a read failure is a genuine fault (the survivor reload
        /// below would fail on the same file). <paramref name="patchFailures"/> counts files
        /// whose experiment_protein_qvalue patch failed, for the StopAfterStage5 boundary gate.
        /// </summary>
        private FirstPassProteinFdrResult RunFirstPassProteinFdrStreaming(
            FdrProjectionSet projections,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx,
            FdrExperimentAccumulator experiment,
            out int patchFailures)
        {
            patchFailures = 0;

            // Pass 1: detected-peptide + best-score reductions, streamed per file in
            // projection order (the reductions are order-independent, but streaming in the
            // resident path's file/row order keeps the best-scores insertion order identical).
            var accumulator = new FirstPassProteinFdrAccumulator(config.RunFdr);
            int proteinReduceFiles = 0;
            using (var reduceProgress = new ProgressReporter(string.Format(
                       @"Computing first-pass protein FDR ({0} files)", projections.PerFile.Count), projections.PerFile.Count))
            foreach (var kvp in projections.PerFile)
            {
                reduceProgress.Report(++proteinReduceFiles);
                if (!StreamFirstPassFileScores(kvp.Key, perFileParquetPaths, config, ctx,
                        (modseq, isDecoy, record) =>
                            accumulator.Add(modseq, isDecoy, record.Score, record.RunPeptideQvalue)))
                {
                    return null;  // ExitCode set in the helper
                }
            }
            var result = accumulator.Finish(fullLibrary, config);
            ProteinFdrEngine.LogFirstPassSummary(result, config, ctx.LogInfo);

            // Pass 2: resolve each entry's experiment_protein_qvalue from peptide -> q into the
            // experiment-scope map, which is written once beside the blib after this returns.
            // The modseq MUST come from the parquet scalars (the same value the pass-1
            // PeptideQvalues keys were built from), not re-derived from the library, so the
            // peptide -> q lookup matches.
            //
            // Before the v5 scope split this loop REWROTE every file's sidecar to push the
            // value back in - 52.3 GB of serial, un-parallelizable rewrite at 257 files, in the
            // stage that is already the bottleneck, to store one number per run of a value that
            // is the same in every run. It now finishes an in-memory map instead, and the
            // per-file sidecars written by the score pass are never reopened (issue #4486).
            var peptideQvalues = result.ProteinFdr.PeptideQvalues;
            int proteinResolveFiles = 0;
            using (var resolveProgress = new ProgressReporter(string.Format(
                       @"Resolving first-pass protein q-values ({0} files)", projections.PerFile.Count), projections.PerFile.Count))
            foreach (var kvp in projections.PerFile)
            {
                resolveProgress.Report(++proteinResolveFiles);
                string fileName = kvp.Key;
                string parquetPath = perFileParquetPaths[fileName];  // present: pass 1 read it
                try
                {
                    ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                        (entryId, charge, isDecoy, coelutionSum, modseq) =>
                        {
                            double q;
                            // Normalize a present-but-null modseq to "" so the lookup matches the
                            // pass-1 PeptideQvalues keys (the accumulator normalizes the same way);
                            // a null Dictionary key would otherwise throw. See StreamFirstPassFileScores.
                            if (!peptideQvalues.TryGetValue(modseq ?? string.Empty, out q))
                                q = 1.0;
                            experiment.SetProteinQvalue(entryId, q);
                        });
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        "Failed to resolve 1st-pass protein q-values for {0}: {1}", fileName, ex.Message));
                    patchFailures++;
                }
            }
            return result;
        }

        /// <summary>
        /// Stream one file's first-pass rows to <paramref name="onRow"/> as
        /// <c>(modseq, isDecoy, FdrScoreRecord)</c>: read the file's
        /// <c>.1st-pass.fdr_scores.bin</c> into an entry_id -> record map (one file resident;
        /// bounded), then stream the parquet scalars (the modseq source PeptideById was
        /// interned from + IsDecoy) in parquet-row order, joining each row to its sidecar
        /// record by entry_id. Returns <c>false</c> (ExitCode set) on a missing parquet path, a
        /// missing sidecar base path, or an unreadable / size-mismatched sidecar. A parquet row
        /// whose entry_id is absent from the sidecar is SKIPPED, not a fault: the sidecar is a
        /// SUBSET of the parquet rows, so a row with no record is simply not a first-pass row
        /// (superset tolerance mirroring the survivor reload -- see the inline note below).
        /// </summary>
        private bool StreamFirstPassFileScores(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx,
            Action<string, bool, FdrScoreRecord> onRow)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                ctx.LogError(string.Format(
                    @"First-pass protein FDR: no scores parquet path for {0}", fileName));
                ctx.ExitCode = 1;
                return false;
            }
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(sidecarBase))
            {
                ctx.LogError(string.Format(
                    @"First-pass protein FDR: no sidecar base path for {0}", fileName));
                ctx.ExitCode = 1;
                return false;
            }
            string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);

            var recordByEntryId = new Dictionary<uint, FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(fdrPath, FdrScoresSidecar.Pass.FirstPass,
                    record => recordByEntryId[record.EntryId] = record))
            {
                ctx.LogError(string.Format(
                    @"First-pass protein FDR: failed to read .1st-pass.fdr_scores.bin for {0} " +
                    @"(expected at {1})", fileName, fdrPath));
                ctx.ExitCode = 1;
                return false;
            }

            ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                (entryId, charge, isDecoy, coelutionSum, modseq) =>
                {
                    // Mirror the survivor reload's superset tolerance (FdrScoresSidecar.TryRead):
                    // the sidecar is written from the projection, a SUBSET of the parquet rows, so
                    // a parquet row with no sidecar record is not a first-pass row -- skip it (the
                    // resident path never saw it either). Today parquet == projection == sidecar
                    // exactly (LoadFdrStubsFromParquet and ReadFdrStubScalars share the row-group
                    // skip rule with no per-row filter), so this never triggers; keeping the
                    // reader's contract aligned with the reload's avoids aborting a run on any
                    // future parquet-superset case (e.g. an Astral gap-fill row).
                    // Normalize a present-but-null modseq to "" so the protein-FDR accumulator's
                    // Dictionary<string,...> key never sees null (which would throw); matches the
                    // resident path, where FdrProjectionSet.Builder interned null modseqs as "".
                    if (recordByEntryId.TryGetValue(entryId, out FdrScoreRecord record))
                        onRow(modseq ?? string.Empty, isDecoy, record);
                });
            return true;
        }

        /// <summary>
        /// Compute the post-first-pass passing base_id set by streaming the finalized per-file
        /// <c>.1st-pass.fdr_scores.bin</c> sidecar, using the identical predicate as
        /// <see cref="CompactFirstPass"/>'s non-bundle branch (targets whose run peptide
        /// q-value passes the compaction peptide gate, or whose run protein q-value passes the
        /// always-active protein-rescue gate; risk #7). entry_id carries the target/decoy flag
        /// (<see cref="LibraryEntry.DECOY_ID_BIT"/>) + base_id
        /// (<see cref="ScoringTaskShared.BASE_ID_MASK"/>): decoys minted
        /// <c>target.Id | DECOY_ID_BIT</c> are skipped, and target / paired decoy share a
        /// base_id, so the masked id set drives the survivor filter symmetrically -- exactly
        /// what the resident (projection + outputs) read did, now off disk (issue #4355
        /// struct-shrink S2). Returns <c>null</c> (ExitCode set) on a sidecar read fault (the
        /// same file the survivor reload below overlays).
        /// </summary>
        private static HashSet<uint> ComputeFirstPassBaseIds(
            FdrProjectionSet projections,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx,
            HashSet<uint> stratum,
            out Dictionary<uint, int> rowsPerBaseId)
        {
            var firstPassBaseIds = new HashSet<uint>();
            // Rows per base_id, over every file and both labels. Summed over the passing set
            // below, this IS the survivor count - so the compaction line can report what
            // survived without anyone materializing the survivors to count them. O(distinct
            // base_ids) (~0.9 M at 446 files), and free here because this pass already visits
            // every record and computes every base_id.
            rowsPerBaseId = new Dictionary<uint, int>();
            var rowCounts = rowsPerBaseId;
            // Peptide-q compaction gate: the dedicated field (default 0.01 = RunFdr)
            // loosenable to broaden the reconciliation pool, mirroring Rust
            // config.reconciliation_compaction_fdr (pipeline.rs:4650) -- identical to
            // the legacy CompactFirstPass twin, not hardwired to config.RunFdr.
            double peptideGate = config.ReconciliationCompactionFdr;
            // Protein-rescue gate is always active (default 0.01), matching Rust
            // pipeline.rs:4651/4658 (protein_compaction_gate = config.protein_fdr, a
            // plain f64, never a null switch). First-pass protein FDR runs unconditionally
            // on this path too, so experiment_protein_qvalue is populated.
            double proteinGate = config.EffectiveProteinFdr;

            // The protein-rescue half of the predicate reads an EXPERIMENT-scope value, so it
            // comes from the analysis-wide sidecar rather than the per-file one (format v5,
            // issue #4486). Both are Stage 5 outputs and this is Stage 5's own compaction, so
            // the read is ordinary producer-to-consumer adjacency - and the experiment file was
            // written above, before this call, precisely so it is available here. An entry with
            // no experiment record cannot be rescued (its q defaults to 1.0), which is the same
            // answer the pre-split sidecar's 1.0 placeholder gave.
            var experimentByEntryId = LoadFirstPassExperimentRecords(config, ctx);
            if (experimentByEntryId == null)
                return null;  // ExitCode set in the helper

            int compactFiles = 0;
            using (var compactProgress = new ProgressReporter(string.Format(
                       @"Compacting first-pass results ({0} files)", projections.PerFile.Count), projections.PerFile.Count))
            foreach (var kvp in projections.PerFile)
            {
                compactProgress.Report(++compactFiles);
                string fileName = kvp.Key;
                string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogError(string.Format(
                        @"First-pass compaction: no sidecar base path for {0}", fileName));
                    ctx.ExitCode = 1;
                    return null;
                }
                string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);
                if (!FdrScoresSidecar.ReadRecords(fdrPath, FdrScoresSidecar.Pass.FirstPass,
                        record =>
                        {
                            // Decoy bit in entry_id == IsDecoy (decoys minted
                            // target.Id | DECOY_ID_BIT); skip decoys, mask to the shared base_id.
                            uint rowBaseId = record.EntryId & ScoringTaskShared.BASE_ID_MASK;
                            // Counted BEFORE the decoy skip: a base_id is kept or dropped with
                            // its paired decoy, so the survivor count includes both labels.
                            rowCounts[rowBaseId] = rowCounts.TryGetValue(rowBaseId, out int n) ? n + 1 : 1;
                            if ((record.EntryId & LibraryEntry.DECOY_ID_BIT) != 0)
                                return;
                            uint baseId = rowBaseId;
                            double proteinQ =
                                experimentByEntryId.TryGetValue(record.EntryId, out var exp)
                                    ? exp.ExperimentProteinQvalue
                                    : 1.0;
                            // The stratum clause (protein-compact) admits present-protein peptides
                            // (>=2 first-pass-detected-peptide proteins) that failed 1st-pass FDR --
                            // identical to the legacy CompactFirstPass twin.
                            if (record.RunPeptideQvalue <= peptideGate ||
                                proteinQ <= proteinGate ||
                                (stratum != null && stratum.Contains(baseId)))
                            {
                                firstPassBaseIds.Add(baseId);
                            }
                        }))
                {
                    ctx.LogError(string.Format(
                        @"First-pass compaction: failed to read .1st-pass.fdr_scores.bin for {0} " +
                        @"(expected at {1})", fileName, fdrPath));
                    ctx.ExitCode = 1;
                    return null;
                }
            }
            return firstPassBaseIds;
        }

        /// <summary>
        /// Load the analysis-wide 1st-pass experiment-scope records, for the consumers that
        /// need an EXPERIMENT-scope column beside the per-file run-scope ones (format v5,
        /// issue #4486). One file for the whole analysis - ~12.3 M records / 0.44 GB at 257
        /// files - so it is read whole rather than streamed per input.
        ///
        /// <para>Returns <c>null</c> with <c>ExitCode</c> set when the file is missing or
        /// unreadable. That is deliberately fatal rather than a fallback to 1.0: a silently
        /// absent protein q turns the compaction predicate's protein-rescue clause off, which
        /// drops rescued precursors from the reconciliation pool and reports a smaller result
        /// that still looks like a successful run.</para>
        /// </summary>
        private static Dictionary<uint, FdrExperimentRecord> LoadFirstPassExperimentRecords(
            OspreyConfig config, PipelineContext ctx)
        {
            string path = FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config),
                FdrScoresSidecar.Pass.FirstPass);
            if (string.IsNullOrEmpty(path))
            {
                ctx.LogError(
                    @"First-pass compaction: no output blib, so no experiment-scope FDR " +
                    @"sidecar to read the protein-rescue q-values from.");
                ctx.ExitCode = 1;
                return null;
            }
            var map = FdrExperimentSidecar.ReadMap(path, FdrScoresSidecar.Pass.FirstPass);
            if (map == null)
            {
                ctx.LogError(string.Format(
                    @"First-pass compaction: failed to read the experiment-scope FDR sidecar " +
                    @"(expected at {0})", path));
                ctx.ExitCode = 1;
            }
            return map;
        }

    }
}
