/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Threading;
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
    /// AnalysisPipeline methods (RunFdr, first-pass protein FDR,
    /// the first-pass sidecar write, WriteReconciliationFiles) plus the
    /// FDR.Reconciliation static helpers (MultiChargeConsensus,
    /// ConsensusRts, CalibrationRefit, ReconciliationPlanner). The
    /// inline planning block from <c>AnalysisPipeline.Run</c> moved
    /// here verbatim; the only changes are LogInfo / LogWarning /
    /// LogError → ctx.LogInfo etc., and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Each file's planning output goes into its own <c>.reconciliation.json</c>, which
    /// the next task (PerFileRescoreTask) reads one run at a time; only the planned-action
    /// count is published. The
    /// <c>PlanningPerformed</c> byproduct is the gate for that next task —
    /// it is <c>true</c> only when the Stage 6 planning block actually ran.
    /// </summary>
    internal sealed class FirstPassFdrTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the validity stamp another
        /// task looks for, and the tests all spell it from here rather than duplicating it.
        /// </summary>
        public const string TASK_NAME = OspreyTaskNames.FIRST_PASS_FDR;

        public override string Name => TASK_NAME;

        /// <summary>
        /// Exit after Stage 5 + reconciliation planning, having written the boundary
        /// files for each input. The ONLY setter of that flag.
        /// </summary>
        public override void ApplySelection(OspreyConfig config)
        {
            config.StopAfterStage5 = true;
        }

        /// <summary>
        /// The Stage 5 -> Stage 6 boundary file pair is only meaningful with 2+ siblings
        /// to reconcile against and reconciliation enabled, so both are rejected early on
        /// top of the shared input / library / output requirement.
        /// </summary>
        public override string ValidateSelection(OspreyConfig config)
        {
            string err = base.ValidateSelection(config);
            if (err != null)
                return err;
            if (config.InputFiles.Count < 2)
            {
                return string.Format(
                    OspreyTasksResources.FirstPassFdrTask_ValidateSelection___task__0__requires_at_least_2_input_files____input___but__1__were_given__The_,
                    OspreyArgNames.TaskText(Name), config.InputFiles.Count, PerFileRescoreTask.TASK_NAME,
                    OspreyArgNames.Text(OspreyArgNames.INPUT));
            }
            if (!config.Reconciliation.Enabled)
            {
                return string.Format(
                    OspreyTasksResources.FirstPassFdrTask_ValidateSelection___task__0__requires_cross_run_reconciliation__which_the_configuration_turns_off__The_,
                    OspreyArgNames.TaskText(Name), PerFileRescoreTask.TASK_NAME);
            }
            return null;
        }

        /// <summary>
        /// The first join writes per-run intermediate files and the analysis-wide ones beside
        /// <c>--output</c>, never the blib; naming the blib would read as "the blib is being
        /// rebuilt".
        /// </summary>
        public override string DescribeOutput(OspreyConfig config)
        {
            return string.Format(
                OspreyTasksResources.FirstPassFdrTask_DescribeOutput_first_pass_intermediate_files_next_to_each_input_s__scores_parquet_file__and_the_analysis_,
                config.OutputBlib, ParquetScoreCache.EXT_SCORES);
        }

        /// <summary>
        /// True when a FirstPassFDR in this configuration would produce nothing but the pass-1
        /// diagnostics product, folded from a first pass that is already complete on disk.
        ///
        /// <para>Asked by <see cref="PerFileScoringTask"/> BEFORE it chooses how to hydrate
        /// <c>ScoredEntries</c>. That choice used to turn on membership alone - "FirstPassFDR is
        /// in this pipeline, so it will train Percolator" - which is false in exactly this case:
        /// the task is present and trains nothing, because the only output it owes is a report it
        /// folds one run at a time. Under <c>--task FirstPassFDR --model-diagnostics</c> that
        /// bought the RESIDENT pre-compaction pool - 109 GB at file 165 of a 446-run cohort -
        /// before <see cref="Run"/> reached the arm that would have said no analysis was owed.</para>
        ///
        /// <para><see cref="Run"/>'s arm asks through this same method, so the hydrate decision
        /// and the fold decision cannot drift apart. A bare instance can answer for the one the
        /// pipeline holds because every term is derived from <paramref name="ctx"/> and its
        /// config; the task carries no state until <see cref="Run"/> is under way.</para>
        /// </summary>
        internal static bool WillOnlyFoldDiagnostics(PipelineContext ctx)
        {
            // Short-circuits before any file probe on the overwhelmingly common path, where the
            // flag is off and no diagnostics product is owed at all.
            return ctx.Config.ModelDiagnostics &&
                   new FirstPassFdrTask().OnlyDiagnosticsProductOutstanding(ctx);
        }

        // Stage 5/6 planning byproducts this task publishes. The same four types
        // are published from Run (Stage-5 computed values) and from the
        // bundle-adopt Rehydrate path -- publishing into one typed slot from
        // both producers is what dissolves the former dual-source getters
        // (_didPlan ? computed : bundle.X), since a consumer reads the slot
        // without caring which path filled it.
        public override IEnumerable<Type> Publishes => new[]
        {
            typeof(PlannedActionCount),
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
        /// release the library fragments nothing can score any more. Null until the compaction
        /// (or the bundle adopt) has run, and the release is skipped while it is.
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
        // hands back on the bundle; null where the resume has no retained set to build it
        // from, whose consumers fall back to CompactedEntries.
        private FirstPassSurvivorLoader _survivorLoader;
        // Built once, on first use, when OSPREY_STUB_IDENTITY asks for it. See StubIdentity.
        private LibraryIdentity _stubIdentity;
        // Non-Keep actions Stage 6 planning produced, for the rescore summary; 0 when planning
        // did not run. The actions themselves are in each run's reconciliation.json.
        private int _plannedActionCount;

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

            // The analysis-wide retained base_id summary, which PerFileRescoring reads. Declared
            // so a summary from another build or other settings is rewritten rather than read;
            // conditional on the output blib exactly as WriteRetainedBaseIdSummary is.
            string retainedPath = RetainedBaseIdSidecar.PathFor(
                ctx.Config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(ctx.Config));
            if (!string.IsNullOrEmpty(retainedPath))
                yield return retainedPath;

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
        /// Produce the pass-1 diagnostics product and nothing else, from a first pass that is
        /// already complete on disk. Streams each run's pre-compaction rows into the report
        /// accumulator and discards them, so what is resident is the library, the accumulator's
        /// O(distinct) reductions, and ONE run's rows.
        ///
        /// <para>The difference from <see cref="Rehydrate"/> is what is NOT built: no Stage 6
        /// bundle, so no whole-run survivor pool, no reconciliation planning, no calibration
        /// capture. Those are what a rescore needs; a report needs none of them, and retaining
        /// the survivors is what put a 446-run fold over a 63.7 GB box at run 266.</para>
        ///
        /// <para>Reached from <c>--task FirstPassFDR --model-diagnostics</c>, and from
        /// <c>--task ModelDiagnostics</c>, which no longer refuses a missing product and names
        /// this task as its producer - it falls into the ordinary pipeline, where this arm is
        /// what "run FirstPassFDR" means once the first pass is already complete (P16).</para>
        /// </summary>
        private bool FoldDiagnosticsOnly(PipelineContext ctx)
        {
            var config = ctx.Config;
            var perFileEntries = ctx.Get<ScoredEntries>().Value;
            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            var fileNames = perFileEntries.ConvertAll(kv => kv.Key);

            var parquetPaths = new List<string>(fileNames.Count);
            foreach (string fileName in fileNames)
            {
                if (!perFileParquetPaths.TryGetValue(fileName, out string path))
                {
                    ctx.LogError(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_FoldDiagnosticsOnly___task_ModelDiagnostics__no__scores_parquet_file_is_known_for__0__, fileName,
                        OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME), ParquetScoreCache.EXT_SCORES));
                    ctx.ExitCode = 1;
                    return false;
                }
                parquetPaths.Add(path);
            }

            var libraryById = ctx.Get<LibraryById>().Value;
            ModelDiagnosticsData.Accumulator accumulator;
            try
            {
                accumulator = BuildModelDiagnosticsAccumulator(fileNames, libraryById, config, ctx);
            }
            catch (Exception ex)
            {
                // Same policy as every other diagnostics builder: an opt-in report never takes
                // down a run. Here the run IS the report, so there is nothing left to do.
                ctx.LogError(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_FoldDiagnosticsOnly___task_ModelDiagnostics__could_not_prepare_the_report___0_, ex.Message,
                    OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME)));
                ctx.ExitCode = 1;
                return false;
            }

            var retainedBaseIds = ScoringTaskShared.ReadRetainedBaseIds(config, out string retainedError);
            if (retainedBaseIds == null)
            {
                ctx.LogError(retainedError);
                ctx.ExitCode = 1;
                return false;
            }

            // The analysis-wide experiment records, read ONCE for both the fold and the panel. Both
            // only read them, and each read is the whole sidecar - ~400 MB at 446 runs - which
            // this used to do twice back to back (issue #4664).
            var experimentRecords = LoadFirstPassExperimentRecords(config, ctx);

            // The co-assignment panel is built from the per-file FDR sidecars and the
            // experiment-scope sidecar, NOT from the fold - so the fold can be skipped without
            // taking the panel with it. That separation is what
            // <see cref="OspreyEnvironment.CoAssignmentPanelOnly"/> exploits: on the 446-run
            // cohort the fold is 54 of this task's 63 minutes and the panel is 8.
            bool coAssignOnly = OspreyEnvironment.CoAssignmentPanelOnly;
            if (coAssignOnly)
            {
                ctx.LogInfo(string.Format(
                    @"{0}: OSPREY_MDIAG_COASSIGN_ONLY is set - SKIPPING the " +
                    @"per-run fold over {1} run(s). The report will carry the peak co-assignment " +
                    @"panel and NOTHING else, and it will be written unstamped so nothing can " +
                    @"adopt it. This is a measurement harness, not a product.",
                    OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME), parquetPaths.Count));
            }
            else
            {
                RescoreHydration.FoldPreCompactionPerRun(
                    parquetPaths,
                    ctx.Get<SequencePool>().Value,
                    (fileIdx, fileName, rows) =>
                        ScoringTaskShared.FeedModelDiagnostics(accumulator, fileIdx, rows),
                    experimentRecords,
                    retainedBaseIds,
                    ctx);
            }

            // Reuses the accumulator's classification, which took minutes to compute over 6.2M
            // library entries - and which is built BEFORE the fold, so it is available on the
            // harness path too.
            var coAssignment = PeakCoAssignmentSource.Build(
                fileNames, perFileParquetPaths, config, accumulator.ClassByBaseId, libraryById,
                experimentRecords, ctx);
            // No validity key on the harness path. The stamp is what lets a later render trust a
            // product, and this one describes a single panel over an accumulator that was never
            // fed; stamping it would let a ten-minute measurement stand in for the hour-long
            // answer, silently and permanently.
            ModelDiagnosticsReport.WriteFromAccumulator(accumulator, null,
                BuildCalibrationData(ctx, fileNames), config, ctx, coAssignment,
                coAssignOnly ? null : ValidityKey(ctx));
            if (coAssignOnly)
                MoveHarnessProductAside(ctx, config);
            return true;
        }

        /// <summary>
        /// Move the harness run's report off the DECLARED output path, to a sibling nothing
        /// declares or reads.
        ///
        /// <para>Withholding the validity key leaves the report unstamped, so it is never taken as
        /// current. The move was first forced by a driver re-stamp of every declared output that
        /// EXISTED once the task returned, which left a one-panel report carrying a full-looking
        /// stamp; that re-stamp no longer exists. Leaving the path EMPTY still says "nothing
        /// durable here", and the next real run rebuilds it.</para>
        /// </summary>
        private static void MoveHarnessProductAside(PipelineContext ctx, OspreyConfig config)
        {
            string declared = ModelDiagnosticsReport.Pass1SidecarPath(config);
            if (string.IsNullOrEmpty(declared) || !File.Exists(declared))
                return;
            string aside = Path.ChangeExtension(declared, null) + @".coassign-only.json";
            try
            {
                if (File.Exists(aside))
                    File.Delete(aside);
                File.Move(declared, aside);
                ctx.LogInfo(string.Format(
                    @"{0}: harness report moved to {1}. The declared product " +
                    @"path is left EMPTY on purpose, so nothing adopts it.",
                    OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME), aside));
            }
            catch (Exception ex)
            {
                // Loud, because the failure mode is the one this method exists to prevent: a
                // partial report sitting at the declared path, about to be adopted as if it
                // described the cohort.
                ctx.LogWarning(string.Format(
                    @"{0}: could not move the harness report off the declared " +
                    @"output path ({1}). DELETE {2} by hand before trusting this directory.",
                    OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME), ex.Message, declared));
            }
        }

        /// <summary>
        /// True when the pass-1 diagnostics product is the single declared output this task
        /// still owes: it is absent or not current, and every other declared output exists with
        /// a current validity stamp. The condition Run's fold arm turns on.
        ///
        /// <para>"Not current" and not just "absent": a stale product left by another arm or
        /// build made this decline, the gate route that FirstPassFDR then takes skips the
        /// diagnostics, and nothing ever rebuilt it - every invocation re-ran the task.</para>
        ///
        /// <para>Asked over <see cref="Outputs"/> rather than a hand-listed set, so a future
        /// output is covered without anyone remembering to add it here - the failure direction
        /// of a forgotten entry is then a redundant recompute rather than a wrongly-adopted
        /// first pass.</para>
        /// </summary>
        private bool OnlyDiagnosticsProductOutstanding(PipelineContext ctx)
        {
            string diagnosticsPath = ModelDiagnosticsReport.Pass1SidecarPath(ctx.Config);
            string validityKey = ValidityKey(ctx);
            if (string.IsNullOrEmpty(diagnosticsPath) || PerFileResumeDriver.IsCurrent(diagnosticsPath, Name, validityKey))
                return false;
            var outputs = Outputs(ctx).ToList();
            foreach (string output in outputs)
            {
                if (string.Equals(output, diagnosticsPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (PerFileResumeDriver.IsCurrent(output, Name, validityKey))
                    continue;
                // Name the first output that failed. Declining here is not an error - it means a
                // genuine first pass is owed - but on a large cohort it is the difference between
                // seconds and hours, and without this line the only symptom is that the run takes
                // a very long time and still produces the right answer. Only when an earlier
                // analysis is on disk: on a cold run every output is missing, nothing is re-run,
                // and the line would read as a problem where there is none.
                if (outputs.Any(File.Exists))
                {
                    ctx.LogInfo(string.Format(File.Exists(output)
                            ? OspreyTasksResources.FirstPassFdrTask_OnlyDiagnosticsProductOutstanding_Model_diagnostics___0__is_out_of_date_for_this_analysis__so_the_first_pass_is_re_run_to_
                            : OspreyTasksResources.FirstPassFdrTask_OnlyDiagnosticsProductOutstanding_Model_diagnostics___0__is_missing__so_the_first_pass_is_re_run_to_build_the_report_,
                        output));
                }
                return false;
            }
            return true;
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            // OSPREY_EXPERIMENT_AGG changes this task's OWN output (the experiment-wide precursor
            // and peptide q maps), so it has to invalidate the cache. Without it, re-running an A/B
            // arm in an output directory that already holds the other arm's results makes
            // the outputs' embedded stamps read as current, the driver skips Run entirely - taking the
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
            // v4 reader refuse the v3 file by version - which once left the second pass to seed
            // nothing and write ResetScores defaults into the 2nd-pass sidecars under only a
            // warning. Including the version turns that silent-wrong-output path into a clean
            // recompute.
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash()
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                // NO ";expsidecar=" term. The experiment sidecar's format does not change with
                // this work: the best-of-runs floor is APPLIED to the q-values before they are
                // written rather than stored beside them, so the record keeps its columns and its
                // version. A term here would invalidate this task for a format that did not move,
                // and invalidating FirstPassFDR costs a 5-hour Stage 5 re-run at 446 files.
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.TrainSampleValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx)
                // The classifier this task trains, and its tree settings. Empty for the linear
                // SVM, so no percolator directory is invalidated.
                + PercolatorEngine.GbdtValidityKeySuffix(ctx.Config);
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
            // This MUST stay above every writer below. When a marker deletion stood there, an
            // argument error destroyed the Stage-5 validity records of a run that had computed no
            // FDR at all, so the operator fixed the variable and paid for a full recompute - hours at 82
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
            // Staleness is handled where it can be handled correctly: each writer embeds its
            // stamp in the same FileSaver commit as its content (FlushPartialSidecar,
            // WriteExperimentSidecar, WriteReconciliationFile). A stamp
            // therefore never outlives the content it vouches for, and a stale file is simply
            // overwritten.

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
            // and SILENT: a FirstPassFDR that genuinely re-ran would rewrite outputs that are
            // already correct and spend 4h46m on a 446-run cohort to produce a report -
            // and it would produce the RIGHT report, so no gate would ever report the cost.
            if (WillOnlyFoldDiagnostics(ctx))
            {
                ctx.LogInfo(OspreyTasksResources.FirstPassFdrTask_Run_Model_diagnostics__the_first_pass_is_already_complete__so_its_report_is_built_from_the_);
                ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_MODEL_DIAGNOSTICS, @"fold-pass1"));
                // Fold the product FIRST, always, and by the bounded route. Rehydrate cannot be
                // relied on to do it: with the --model-diagnostics exclusion gone from
                // CanHydratePerRun, Rehydrate takes RehydrateForPerRunRescore, which publishes
                // the survivor loader and folds NOTHING - so leaving the fold to it would
                // silently produce no report on exactly the resume this arm exists for.
                if (!FoldDiagnosticsOnly(ctx))
                    return false;

                // The product exists now. Anything downstream still needs this task's state, and
                // it gets it the ordinary way - cheaply, since that same exclusion removal lets
                // the per-run arm serve it. With nothing after Stage 5 there is nothing to hand
                // over, and building Stage 6's bundle there would retain every run's survivors:
                // 0.15-0.22 GB per run measured, over a 63.7 GB box by run 266 of 446.
                return config.StopAfterStage5 || Rehydrate(ctx);
            }

            // Stage 5: First-pass FDR. The Percolator framework (SVM or Gbdt) prints
            // its own "Running First-pass Percolator on N entries..." line from the FDR
            // engine, so there is no generic header here: it would be a redundant second
            // header right after the [TASK] FirstPassFDR banner.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx,
                string.Format(@"Stage 5 start: {0} files loaded (stubs), before first-pass FDR", perFileEntries.Count));

            // The line above reports GC.GetTotalMemory(false) -- allocated-since-last-GC,
            // so it carries whatever garbage scoring left behind, and two runs doing
            // identical work can differ by tens of GB on GC timing alone. This one forces
            // a collection first, so it is the LIVE set entering first-pass FDR: the only
            // number that answers whether the run fits in a given box.
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"stage5-start-live",
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

            // Issue #4355 step (b) increment ii: route the first-pass FDR peak through the thin
            // FdrProjection buffer instead of holding the full FdrEntry stub buffer resident
            // across Percolator + protein FDR + the sidecar write + compaction.
            // RunFirstPassProjection returns the reloaded full-FdrEntry survivor buffer (from
            // parquet + the just-written 1st-pass sidecar), which then flows into PlanStage6 /
            // Publish. The legacy FdrEntry-buffer path that stood beside it as a byte-identity
            // oracle was removed with OSPREY_FDR_PROJECTION (2026-10-10).
            // FDRBench pass-1 (#4377) streams off the per-file sidecars and the experiment map
            // (WriteFdrBenchPass1FromSidecarsIfRequested). OSPREY_PASS2_QVALUE=transfer and
            // transfer-compete need only the trained 1st-pass MODEL, which the projection path
            // publishes via the RunFirstPassProjection captureModel hook. --model-diagnostics
            // STREAMS its pass-1 report off the projection path via a
            // ModelDiagnosticsData.Accumulator fed by the score-pass sink, folding each
            // pre-compaction row into the reduced report structures; the reductions are
            // order-independent, so the streamed report is byte-identical to a resident build.
            //
            // Null unless PerFileScoring took the lean path and streamed the rows
            // straight from parquet (issue #4397); RunFirstPassProjection then builds
            // from the fat stubs instead.
            //
            // Consume, not Get: this is the projection set's only consumer, and the
            // byproduct cache is process-lifetime. Leaving it published pinned ~5.7 GiB
            // (191 M rows x 32 B) plus the interned peptide table through reconciliation
            // and the blib write - memory that scales with total scored entries across
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
            if (_survivorLoader != null && !config.StopAfterStage5)
            {
                foreach (var kvp in perFileEntries)
                {
                    kvp.Value.Clear();
                    kvp.Value.TrimExcess();
                }
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"stage5-handoff-released",
                    string.Format(@"(post-GC, survivors released after planning, files={0})",
                        perFileEntries.Count));
            }

            // Publish the planned-action count (0 when PlanStage6 was skipped / stopped after
            // Stage 5), plus the CompactedEntries milestone of the shared buffer. The
            // whole-experiment planning maps - the action map alone 30.8 M entries at 446 runs,
            // order 13 GB with the consensus targets, refined calibrations and gap-fill - are
            // no longer built: each run's slice is in its own reconciliation.json, which is
            // all the per-run rescore reads (issue #4664).
            ctx.Publish(new PlannedActionCount(_plannedActionCount));
            ReleaseUnscorableLibraryFragments(fullLibrary, ctx);
            ctx.Publish(new CompactedEntries(perFileEntries));
            // Set by the compaction gate on every Run that reaches here; a consumer that
            // finds it null falls back to the buffer above.
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
            // is to publish the per-run survivor loader. The compute
            // counterpart is Run.
            var config = ctx.Config;
            var perFileEntries = ctx.Get<ScoredEntries>().Value;

            // What downstream still needs from this task is the per-run SURVIVOR LOADER, and it
            // needs the retained base_id set to build it. That set comes from the analysis-wide
            // summary rather than from a bundle assembled by reading every run - which is the
            // whole reason the summary exists.
            if (ScoringTaskShared.CanHydratePerRun(config))
                return RehydrateForPerRunRescore(ctx, perFileEntries);

            // No per-run route, so there is no state this task can rebuild. The all-runs
            // reconciliation bundle that stood here, every run's first-pass survivors at once, had
            // no route left that could finish: without the analysis-wide retained base_id summary
            // it needed that same summary to compact against, and with it the per-run route above
            // is the one every task that consumes this state takes. Refused before any run is
            // read, naming what is missing.
            //
            // The summary is a declared output of this task, so a missing one makes the task not
            // current and a full re-run rebuilds it.
            if (ScoringTaskShared.ReadRetainedBaseIds(config, out string retainedError) == null)
            {
                ctx.LogError(retainedError);
                ctx.ExitCode = 1;
                return false;
            }
            throw new InvalidOperationException(string.Format(
                @"First-pass state was demanded by a selection that does not rehydrate per run, although the retained base_id summary is readable: {0}",
                RetainedBaseIdSidecar.PathFor(config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config))));
        }

        /// <summary>
        /// Rebuild this task's state for a resume, where the driver skipped <see cref="Run"/>
        /// because its outputs were already valid on disk
        /// (<see cref="PipelineContext.CanRehydrate"/>) and a downstream task is the first to
        /// touch its state: publish the per-run survivor loader, built from the analysis-wide
        /// retained base_id summary, and every planning slot EMPTY - each run's actions,
        /// gap-fill and refined calibration are read from its own reconciliation.json when it
        /// is re-scored. Returns false (with <see cref="PipelineContext.ExitCode"/> set) when the
        /// summary cannot be read.
        /// </summary>
        private bool RehydrateForPerRunRescore(
            PipelineContext ctx, List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            // The lists this arm republishes as CompactedEntries must be EMPTY: every load that
            // reaches it (the lean --input-files load and the per-run --input-scores names load)
            // leaves them so, and each run is compacted against the retained set as it is
            // hydrated. Checked rather than assumed, because uncompacted stubs here would fold
            // the second pass over every pre-compaction row - a wrong report with no error.
            // O(files) to count.
            long residentStubs = 0;
            foreach (var kvp in perFileEntries)
                residentStubs += kvp.Value.Count;
            if (residentStubs > 0)
            {
                throw new InvalidOperationException(string.Format(
                    @"The per-run rescore arm was reached with {0:N0} first-pass stubs still " +
                    @"resident across {1} runs, which the per-run survivor loader cannot serve.",
                    residentStubs, perFileEntries.Count));
            }
            var retainedBaseIds = ScoringTaskShared.ReadRetainedBaseIds(ctx.Config, out string error);
            if (retainedBaseIds == null)
            {
                ctx.LogError(error);
                ctx.ExitCode = 1;
                return false;
            }
            ctx.LogVerbose(CountText.Format(perFileEntries.Count,
                OspreyTasksResources.FirstPassFdrTask_RehydrateForPerRunRescore_Re_scoring_loads_the_file_on_its_own__so_no_combined_cross_run_reconciliation_data_is_,
                OspreyTasksResources.FirstPassFdrTask_RehydrateForPerRunRescore_Re_scoring_loads_each_of__0__files_on_its_own__so_no_combined_cross_run_reconciliation_));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_FIRST_PASS_FDR, @"survivor-loader-only runs={0}",
                perFileEntries.Count));

            var perFileParquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            _survivorLoader = new FirstPassSurvivorLoader(
                perFileParquetPaths, ctx.Config, retainedBaseIds, ctx.Get<SequencePool>().Value);
            _firstPassBaseIds = retainedBaseIds;

            // No cross-run gap-fill or refined-calibration map is built here: the per-run hydrate
            // reads each run's own out of its reconciliation.json inside its own iteration. The
            // release below needs none either - see the note at its call site.

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

            // The planned-action count is published as ZERO on purpose: planning did not run in
            // this process, and the per-run hydrate reads each run's actions, gap-fill and
            // refined calibration out of that run's own reconciliation.json. Publishing it
            // rather than skipping the publish keeps the byproduct contract intact: an
            // unpublished slot throws UnknownByproductException at the reader, turning a
            // deliberate design into a crash at an unrelated call site.
            //
            // CompactedEntries republishes the SAME list objects ScoredEntries holds - one empty
            // list per run, in input order. Those objects are the shared backing store the
            // rescore loop refills and drains one run at a time, so identity matters and
            // contents do not. No compaction runs here: the lists are empty, and each run is
            // compacted against the retained set as it is hydrated.
            ctx.Publish(new PlannedActionCount(0));
            ctx.Publish(new CompactedEntries(perFileEntries));
            ctx.Publish(new FirstPassSurvivorSource(_survivorLoader));
            ctx.Publish(new PlanningPerformed(false));
            return true;
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
                    OspreyTasksResources.FirstPassFdrTask_BuildCalibrationData_Calibration_report___0__of__1__files_have_no_calibration_diagnostics_and_are_left_out_of_,
                    omitted.Count, orderedFileNames.Count, string.Join(@", ", omitted)));
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
                MassUnit = !string.IsNullOrEmpty(massUnit) ? massUnit : MzCalibration.UNIT_PPM,
                FileCount = files.Count,
            };
        }

        /// <summary>
        /// Build the streaming <see cref="ModelDiagnosticsData.Accumulator"/> that a
        /// pre-compaction row source folds each row into, so the pass-1
        /// <c>--model-diagnostics</c> report emits without the resident FdrEntry pool. Derives the
        /// entrapment classification from the searched library, logged once, and seeds the
        /// accumulator with the input-file order plus the run FDR level.
        ///
        /// Row sources share it: the projection path's score-pass sink (fed as first-pass
        /// Percolator scores each row), the diagnostics-only fold (fed per file after the
        /// 1st-pass sidecar overlay) and SecondPassFdrTask's pass-2 fold. They feed
        /// rows in the same nested (file, row) order the batch <c>ModelDiagnosticsData.Build</c>
        /// test oracle walks, which is what keeps the streamed report identical to it.
        /// <paramref name="libraryById"/> is passed rather than pulled from the context because
        /// the rehydrate caller runs before <c>LibraryById</c> is published.
        ///
        /// <para><c>pass</c> selects which reductions the accumulator folds: 1 for the
        /// pre-compaction first pass, 2 for SecondPassFdrTask's streamed fold over the final
        /// reported pool. Everything else - the classification, the run-name seeding, the run FDR
        /// and level - is derived identically for both, which is why the second pass shares this
        /// helper rather than re-deriving them and letting the two drift.</para>
        /// </summary>
        internal static ModelDiagnosticsData.Accumulator BuildModelDiagnosticsAccumulator(
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            IOspreyLog log,
            int pass = 1)
        {
            ModelDiagnosticsReport.BuildClassificationFromLibrary(config, libraryById, log, pass,
                out var classByBaseId, out var pairByBaseId, out var entrapmentRatio);
            var runNames = new string[fileNames.Count];
            for (int i = 0; i < runNames.Length; i++)
                runNames[i] = fileNames[i];
            return new ModelDiagnosticsData.Accumulator(
                runNames, classByBaseId, pairByBaseId, entrapmentRatio, config.RunFdr,
                config.FdrLevel, pass);
        }

        /// <summary>
        /// The pass-1 FDRBench input, its pairing manifest and log lines, read off the
        /// persisted per-file <c>.1st-pass.fdr_scores.bin</c> joined to the parquet scalars and
        /// the experiment-scope map, one file at a time, instead of walking a resident
        /// <see cref="FdrEntry"/> pool. This is what lets <c>--fdrbench-pass 1</c> (and
        /// so <c>both</c>) run without forcing the O(files) resident path (issue #4507): every
        /// value the TSV needs is on disk or in an O(distinct entries) map by the time the
        /// experiment sidecar has been written, and nothing here retains a row past its file.
        ///
        /// <para>Feeds <see cref="FdrBenchInputWriter.PeptideInputSink"/>, which owns the dedup,
        /// ordering and formatting, walking files in run order and rows in parquet-row order,
        /// which is the only order the sink is sensitive to (an exact q-value tie keeps the row
        /// seen first). The resident emitter it was checked against was removed with
        /// OSPREY_FDR_PROJECTION (2026-10-10). Runs BEFORE compaction, so the pool it emits is
        /// the full pre-compaction first pass. A separate pass over the sidecars rather than a
        /// second callback inside the protein-FDR reduce: it costs one more sidecar + scalar
        /// walk per file, paid only when pass 1 was asked for, and keeps the oracle file's
        /// producer independent of the reduce it would otherwise ride on. Returns <c>false</c>
        /// (ExitCode set) on a sidecar or parquet read fault - a requested oracle file that
        /// cannot be written is a failed run, not a warning.</para>
        /// </summary>
        private bool WriteFdrBenchPass1FromSidecarsIfRequested(
            FdrProjectionSet projections,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentById,
            OspreyConfig config,
            PipelineContext ctx)
        {
            return EmitFdrBenchPass1(config, ctx, sink =>
            {
                int files = 0;
                string error = null;
                using (var progress = new ProgressReporter(CountText.Format(projections.PerFile.Count,
                           OspreyTasksResources.FirstPassFdrTask_WriteFdrBenchPass1FromSidecarsIfRequested_Writing_first_pass_FDRBench_input_for_1_file,
                           OspreyTasksResources.FirstPassFdrTask_WriteFdrBenchPass1FromSidecarsIfRequested_Writing_first_pass_FDRBench_input_for__0__files),
                           projections.PerFile.Count))
                {
                    // A lane reads a file and builds its rows; the sink, which dedups across files
                    // with first-seen ties (or streams per-run rows straight to the TSV), takes them
                    // here in file order.
                    var stubIdentity = StubIdentity(ctx);
                    OrderedFileLanes.RunWhile(projections.PerFile.Count, ctx.RunPlan.FirstPassFdrLanes, f =>
                    {
                        string runName = projections.PerFile[f].Key;
                        var rows = new List<FdrBenchInputWriter.Row>();
                        string fileError = TryStreamFirstPassFileScores(runName, perFileParquetPaths, config, stubIdentity,
                            projections.RowCount(f), (modseq, charge, isDecoy, record) =>
                            {
                                if (isDecoy)
                                    return;
                                if (!experimentById.TryGetValue(record.EntryId, out var exp))
                                {
                                    // Every observation the score pass wrote a sidecar record
                                    // for was folded into the experiment map in the same pass,
                                    // so a miss is a defect in that contract, not a data case.
                                    throw new InvalidOperationException(string.Format(
                                        @"FDRBench pass 1: entry {0} in {1} has a 1st-pass sidecar " +
                                        @"record but no experiment-scope record", record.EntryId, runName));
                                }
                                rows.Add(FdrBenchInputWriter.Row.FromSidecar(
                                    modseq, charge, in record, in exp, sink.EffectiveLevel));
                            });
                        return (Rows: rows, Error: fileError);
                    }, (f, file) =>
                    {
                        progress.Report(++files);
                        error = file.Error;
                        if (error != null)
                            return false;
                        string runName = projections.PerFile[f].Key;
                        foreach (var row in file.Rows)
                            sink.Add(runName, row);
                        return true;
                    });
                }
                if (error != null)
                {
                    ctx.LogError(error);
                    ctx.ExitCode = 1;
                    return false;
                }
                return true;
            });
        }

        /// <summary>
        /// Everything the two pass-1 emitters share: the pass gate, the pairing reconciliation,
        /// the sink's lifetime, the manifest beside the TSV and the log lines - so the resident
        /// and streamed producers differ in nothing but where their rows come from.
        /// <paramref name="produce"/> feeds the sink and returns <c>false</c> to abandon the
        /// file (the sink then discards its temp); this returns that verdict, or <c>true</c>
        /// when no pass-1 file was requested.
        /// </summary>
        private static bool EmitFdrBenchPass1(OspreyConfig config, PipelineContext ctx,
            Func<FdrBenchInputWriter.PeptideInputSink, bool> produce)
        {
            var benchPath = FdrBenchInputWriter.PathForPass(config, OspreyConfig.FDRBENCH_PASS_1);
            if (benchPath == null)
                return true;

            var libraryById = ctx.Get<LibraryById>().Value;
            var swFdrBench = Stopwatch.StartNew();
            // Reconcile the library against the external manifest: reconstruct the
            // extras' pairing and drop unmatched entrapment (Met-clip artifacts) so the
            // TSV and the emitted manifest stay consistent and stock FDRBench works.
            var pairing = EntrapmentPairing.Build(libraryById, config.DecoyPairingManifestPath);
            FdrBenchInputWriter.Result benchResult;
            using (var sink = new FdrBenchInputWriter.PeptideInputSink(
                       benchPath, libraryById, config.FdrLevel, config.FdrBenchPerRun,
                       pairing.ExcludedEntrapment))
            {
                if (!produce(sink))
                    return false;
                benchResult = sink.Commit(ctx.RunPlan.FirstPassFdrLanes);
            }
            string manifestPath = benchPath + FdrBenchInputWriter.EXT_PAIRING;
            int manifestRows = FdrBenchInputWriter.WritePairingManifest(manifestPath, libraryById, pairing);
            swFdrBench.Stop();
            ctx.LogInfo(string.Format(OspreyTasksResources.FirstPassFdrTask_EmitFdrBenchPass1_Wrote_first_pass_FDRBench_input___0___to__1____2__rows,
                config.FdrBenchPerRun ? @"per-run" : @"per-precursor",
                benchPath, benchResult.Rows));
            ctx.LogInfo(string.Format(OspreyTasksResources.FirstPassFdrTask_EmitFdrBenchPass1_Wrote_the_FDRBench_pairing_manifest__from_the_searched_library__to__0____1__peptides,
                manifestPath, manifestRows));
            pairing.LogSummary(ctx);
            if (benchResult.MissingLibrary > 0)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_EmitFdrBenchPass1__0__FDRBench_rows_had_no_matching_library_peptide__their_peptide_and_protein_columns_were_,
                    benchResult.MissingLibrary));
            }
            if (benchResult.TruncatedProtein > 0)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_EmitFdrBenchPass1__0__FDRBench_rows_had_very_long_protein_ID_lists__which_were_truncated_with_______N_more__,
                    benchResult.TruncatedProtein, FdrBenchInputWriter.TRUNCATION_MARKER_PATTERN));
            }
            ctx.LogInfo(LogTag.STAGE_WALL, @"fdrbench-pass1: {0:F1}s",
                swFdrBench.Elapsed.TotalSeconds);
            return true;
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
            ctx.LogBlockReads(@"First-pass before planning");
            ctx.LogInfo(string.Empty);
            ctx.LogInfo(ScoringTaskShared.IsSingleFileSearch(config)
                ? OspreyTasksResources.FirstPassFdrTask_PlanStage6_Multi_charge_consensus_planning
                : OspreyTasksResources.FirstPassFdrTask_PlanStage6_Cross_run_reconciliation_planning);

            // The library lookups gap-fill needs, and the per-file envelope's join-wide
            // fields. Built once, before any file is planned, because none of them varies by
            // file and the library scan is O(library).
            BuildGapFillLibraryLookups(fullLibrary, out var libLookup, out var libPrecursorMz);
            var writerState = PrepareReconciliationWrite(perFileEntries, config);
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
            // buffer. Pairs with the --task PerFileRescoring Stage 6 worker mode, and the
            // in-process rescore reads the same envelopes.
            Stage6FilePlanned onFilePlanned = filePlan =>
            {
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
                // The compaction gate always hands planning the survivor LOADER, so the per-file
                // lists it published are empty by design rather than by failure.
                var fileNames = new List<string>(perFileEntries.Count);
                foreach (var kvp in perFileEntries)
                    fileNames.Add(kvp.Key);
                plan = planner.Plan(fileNames, LoadSurvivorsForPlanning, perFileCalibrations,
                    perFileParquetPaths, libLookup, libPrecursorMz, perFileIsolationMz, config,
                    onFilePlanned);
            }
            catch (InvalidDataException ex)
            {
                // A survivor load fault. Reported as an error plus exit 1, because a stack
                // trace at the process boundary would replace an actionable message. The message already names
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
            if (!WriteRetainedBaseIdSummary(retainedBaseIds, perFileParquetPaths, config, OutputStamp(ctx), ctx))
                return false;

            if (config.StopAfterStage5)
            {
                if (reconWriteFailures > 0)
                {
                    LogIntermediateWriteFailures(ctx, reconWriteFailures, perFileEntries.Count);
                    ctx.ExitCode = 1;
                    return false;
                }
                ctx.LogInfo(CountText.Format(perFileEntries.Count,
                    OspreyTasksResources.FirstPassFdrTask_PlanStage6___task_FirstPassFDR_complete__first_pass_FDR_and_reconciliation_planning_done_for_1_file__,
                    OspreyTasksResources.FirstPassFdrTask_PlanStage6___task_FirstPassFDR_complete__first_pass_FDR_and_reconciliation_planning_done_for__0__,
                    OspreyArgNames.TaskText(TASK_NAME), PerFileRescoreTask.TASK_NAME));
                // Success: return true (not false). The stop after Stage 5 is now
                // a membership fact -- PerFileRescore and SecondPassFDR are excluded
                // by the membership rule under --task FirstPassFDR (OspreyConfig.Includes), so the driver loop iterates no
                // further. The failure path above keeps ExitCode=1; return false.
                ctx.ExitCode = 0;
                return true;
            }

            // Surface outputs for the next task.
            _didPlan = true;
            _plannedActionCount = plan.ActionCount ?? 0;
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
        /// Write the one analysis-wide experiment-scope sidecar beside the output blib.
        /// Counted into the same failure total as the per-file writes, because the Stage 5 →
        /// Stage 6 boundary is incomplete without it: the compaction predicate's protein-rescue
        /// half reads this file, so a Stage 6 worker that cannot find it cannot reproduce the
        /// in-process passing set.
        /// </summary>
        /// <returns>1 if the write failed or the analysis has no output blib to name it
        /// after, 0 on success.</returns>
        // Not static: it embeds this task's validity stamp, which needs the task's Name and
        // ValidityKey. This file is a declared Output, so an unstamped one would hold the
        // whole task un-resumable no matter how many per-file sidecars were current.
        private int WriteExperimentSidecar(FdrExperimentAccumulator experiment,
            FdrScoresSidecar.Pass pass, OspreyConfig config, PipelineContext ctx)
        {
            string path = FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config), pass);
            if (string.IsNullOrEmpty(path))
            {
                ctx.LogWarning(
                    string.Format(OspreyTasksResources.FirstPassFdrTask_WriteExperimentSidecar_There_is_no_output__blib_to_name_the_whole_experiment_first_pass_intermediate_file_after__,
                        LibrarySource.EXT_BLIB));
                return 1;
            }
            try
            {
                FdrExperimentSidecar.Write(path, experiment.Records, pass, OutputStamp(ctx));
                ctx.LogVerbose(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteExperimentSidecar_Wrote_experiment_level_FDR_results_for__1__precursor_candidates_to__0_,
                    path, experiment.Count));
                return 0;
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteExperimentSidecar_Failed_to_write__0____1_, path, ex.Message));
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
                    OspreyTasksResources.FirstPassFdrTask_WriteReconciliationFile_Could_not_locate_the_intermediate_files_for__0___so_its_cross_run_reconciliation_file_was_,
                    fileName));
                return 1;
            }
            string reconPath = ReconciliationFile.PathForInput(sidecarBase);
            var reconFile = BuildReconciliationFile(
                filePlan.Entries, filePlan.Actions,
                filePlan.GapFill ?? Array.Empty<GapFillTarget>(),
                filePlan.RefinedCalibration,
                state.SearchHash, state.LibraryHash, state.JoinFileStems, filePlan.GlobalBaseIds);
            try
            {
                ReconciliationFile.Save(reconPath, reconFile, OutputStamp(ctx));
                // Skyline's peak-boundary-imputation vocabulary: use_cwt re-picks a peak, forced
                // integration imputes its boundaries, and a gap-fill target is a missing peak.
                // Only when planning ran: without a cross-run consensus (a single file) every
                // count is zero and the line describes work that cannot happen.
                if (filePlan.Actions != null)
                {
                    ctx.LogInfo(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_WriteReconciliationFile_Cross_run_reconciliation_for__0____1__peaks_to_re_pick___2__peak_boundaries_to_impute___3_,
                        fileName,
                        reconFile.UseCwtPeakActions.Count,
                        reconFile.ForcedIntegrationActions.Count,
                        reconFile.GapFillTargets.Count));
                }
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteReconciliationFile_Could_not_write_the_cross_run_reconciliation_file_for__0____1_, fileName, ex.Message));
                return 1;
            }
            return 0;
        }


        /// <summary>
        /// Add the base_id of every action target in one run's plan to the analysis-wide retained
        /// set. Masked to the base_id (decoy bit stripped) for the reason
        /// the first-pass compaction masks: a target and its paired decoy share a base_id,
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
        /// The <c>--task FirstPassFDR</c> error for intermediate files that could not be written:
        /// the per-file <c>.1st-pass.fdr_scores.bin</c> or <c>.reconciliation.json</c> the next
        /// task reads. Each failed write has already logged its own warning naming the file, so
        /// this line gives the count and the consequence.
        /// </summary>
        private static void LogIntermediateWriteFailures(PipelineContext ctx, int failures, int fileCount)
        {
            ctx.LogError(string.Format(
                OspreyTasksResources.FirstPassFdrTask_LogIntermediateWriteFailures___task_FirstPassFDR___0__of__1__first_pass_intermediate_files_could_not_be_written__so_,
                failures, fileCount, OspreyArgNames.TaskText(TASK_NAME)));
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
            ArtifactStamp stamp,
            PipelineContext ctx)
        {
            string siblingPath = ScoringTaskShared.ArtifactSiblingPath(config);
            string path = RetainedBaseIdSidecar.PathFor(config.OutputBlib, siblingPath);
            if (string.IsNullOrEmpty(path))
                return true;
            try
            {
                RetainedBaseIdSidecar.Write(path, retainedBaseIds, stamp);
            }
            catch (Exception ex)
            {
                ctx.LogError(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteRetainedBaseIdSummary_Failed_to_write_the_analysis_wide_list_of_kept_precursor_candidates__0____1___The_next_, path, ex.Message,
                    PerFileRescoreTask.TASK_NAME));
                ctx.ExitCode = 1;
                return false;
            }
            ctx.LogVerbose(CountText.Format(perFileParquetPaths.Count,
                OspreyTasksResources.FirstPassFdrTask_WriteRetainedBaseIdSummary_Wrote_the_list_of_kept_precursor_candidates___1__target_decoy_pairs_in_1_file,
                OspreyTasksResources.FirstPassFdrTask_WriteRetainedBaseIdSummary_Wrote_the_list_of_kept_precursor_candidates___1__target_decoy_pairs_across__0__files,
                retainedBaseIds.Count));
            ctx.LogInfo(LogTag.COUNT, LogKey.Format(LogKey.COUNT_RETAINED_SUMMARY_WRITTEN, @"base-ids={0} runs={1}",
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
        /// Stage 5 -> 6 boundary: drop <c>Fragments</c> from every library entry that nothing
        /// downstream can score or write, keeping the identity fields on all of them. See
        /// <see cref="OspreyEnvironment.ReleaseLibraryFragments"/> for the rationale and the
        /// safety argument.
        ///
        /// <para>The retained set is the post-compaction survivors (<c>_firstPassBaseIds</c>,
        /// already pair-symmetric so a target's decoy rides along) PLUS the gap-fill candidates.
        /// The gap-fill term is a belt rather than a necessity, and the reason this doc used to
        /// give for it was wrong - see
        /// <see cref="LibraryFragmentRelease.BuildRetainedBaseIds(HashSet{uint}, IReadOnlyDictionary{string, List{GapFillTarget}})"/>,
        /// which carries the corrected argument (issue #4650). In short: a gap-fill target is a
        /// precursor that PASSED in a sibling replicate and is absent from THIS file's rows, so
        /// its base_id is already in the join-wide first-pass set. "Did not survive compaction"
        /// is true of the file's ROW, never of the base_id.</para>
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
            // _firstPassBaseIds is the one meaningful null here: an empty join adopts a bundle
            // whose compaction never ran, so there is no surviving set to release against.
            // fullLibrary is not checked - it is a published
            // byproduct every other reader dereferences unguarded, so a null is a wiring fault
            // and should say so.
            if (!LibraryFragmentRelease.RunsOnThisLeg(ctx) || _firstPassBaseIds == null)
                return;

            // No gap-fill term: every gap-fill target's base_id is already in the join-wide set
            // (see BuildRetainedBaseIds), and no all-runs gap-fill map is held any more.
            var retained = LibraryFragmentRelease.BuildRetainedBaseIds(_firstPassBaseIds, null);
            int released = LibraryFragmentRelease.ReleaseFragments(fullLibrary, retained);
            ctx.LogInfo(LogTag.Mem(@"library-fragments"), @"Released library fragments for {0} of {1} library precursors ({2} base_ids retained for rescore + gap-fill)",
                released, fullLibrary.Count, retained.Count);
            LibraryFragmentRelease.LogRelease(ctx, released, fullLibrary.Count, retained.Count,
                LogKey.SCOPE_RESCORE_GAP_FILL);
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"after library-fragment release");
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
                OspreyTasksResources.FirstPassFdrTask_BuildProteinCompactStratum__0__proteins_have_2_or_more_detections___1__detected_peptides___their__2__precursor_,
                present2.Count, result.DetectedPeptides.Count, stratum.Count));
            return stratum;
        }

        /// <summary>Build the protein-compact stratum, stash it for the compaction gate
        /// (<see cref="_proteinCompactStratum"/>), publish it for the pass-2 stratified
        /// competition, and PERSIST it. Called from the projection first pass, as protein FDR
        /// finishes.
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
            string stratumJson = FirstPassModelIO.SerializeStratum(_proteinCompactStratum,
                ArtifactStamp.ForCurrentBuild(Name, validityKey));
            if (stratumJson == null)
                return;
            int stratumWrites = 0;
            foreach (var kvp in perFileParquetPaths)
            {
                string path = FirstPassModelIO.StratumPathFor(kvp.Value, kvp.Key);
                try
                {
                    FirstPassModelIO.WriteText(path, stratumJson);
                    stratumWrites++;
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_BuildAndPublishProteinCompactStratum_Could_not_save_the_precursor_candidates_from_proteins_with_2_or_more_detections_for___0___,
                        kvp.Key, ex.Message));
                }
            }
            if (stratumWrites > 0)
            {
                ctx.LogVerbose(CountText.Format(stratumWrites,
                    OspreyTasksResources.FirstPassFdrTask_BuildAndPublishProteinCompactStratum_Saved_the_precursor_candidates_from_proteins_with_2_or_more_detections_for_1_file_,
                    OspreyTasksResources.FirstPassFdrTask_BuildAndPublishProteinCompactStratum_Saved_the_precursor_candidates_from_proteins_with_2_or_more_detections_for__0__files_));
            }
        }

        /// <summary>
        /// Sets <see cref="RunPlan.FirstPassFdrLanes"/> for this stage's per-file phases, before
        /// the first of them. Free memory is measured here, after Stage 1-4 state is loaded and
        /// before the first pass grows its own.
        ///
        /// <para>In a straight-through run PerFileScoring's freed heap is still committed here
        /// and counts as used: SEA-AD 82 files at 64 GB read 29.3 GB free and ran 1 lane
        /// (4,082 s). Releasing it first reads 45.2 GB and runs 3 (2,123 s), so the reading is
        /// taken after <see cref="SystemMemory.AvailablePhysicalBytesAfterCollect"/>. That is done
        /// whenever there is more than one file, including under <c>OSPREY_FDR_FILE_LANES</c>, so a
        /// forced lane count enters the stage in the same memory state as the count it is compared
        /// with: at 1 lane, entering with the heap still committed cost 784 s (4,082 s vs 3,298 s
        /// for the same stage in a fresh process).</para>
        /// </summary>
        private static void ResolveFileLanes(PipelineContext ctx, int nFiles, long maxRowsPerFile)
        {
            long availableBytes = nFiles > 1
                ? SystemMemory.AvailablePhysicalBytesAfterCollect()
                : SystemMemory.AvailablePhysicalBytes();
            ctx.RunPlan.FirstPassFdrLanes = FdrLaneResolver.Resolve(nFiles, ctx.Config.NThreads,
                maxRowsPerFile, availableBytes, OspreyEnvironment.FdrFileLanes, ctx.LogInfo);
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
        /// survivor set the compaction gate selected.
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
            // The only path (issue #4397): PerFileScoring counted these rows from each
            // per-file .scores.parquet footer, so the fat FdrEntry stub buffer was never
            // allocated (it cost ~53 GB at 191M rows), and the first pass streams every row's
            // identity and features from the parquet. Every PerFileScoring load publishes this
            // counts-only set; the resident projection built from stubs, every run's rows at
            // once, had no producer left and survives only as a test oracle in Osprey.FDR.
            var projections = prebuiltProjections;
            if (projections == null || !projections.IsCountsOnly)
            {
                throw new InvalidOperationException(
                    @"The first pass was reached without the counts-only projection PerFileScoring publishes.");
            }
            long maxRows = 0;
            for (int f = 0; f < projections.PerFile.Count; f++)
                maxRows = Math.Max(maxRows, projections.RowCount(f));
            ResolveFileLanes(ctx, projections.PerFile.Count, maxRows);
            // long with TotalRows: this is the PRE-compaction cohort total, 1,342,686,095 at
            // 446 files. afterCount below is deliberately left int - it is the post-compaction
            // survivor count, ~289 M, a different magnitude with room to spare.
            long beforeCount = projections.TotalRows;
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, string.Format(
                @"projection counts-only: {0} rows across {1} files (no resident rows); FdrEntry stubs released",
                beforeCount, projections.PerFile.Count));

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
            // Hoisted: the sidecar stamp below is written once per file inside the score pass, and
            // recomputing the key per file would hash the search + library identity 446 times.
            string sidecarValidityKey = ValidityKey(ctx);
            var sidecarStamp = ArtifactStamp.ForCurrentBuild(Name, sidecarValidityKey);

            int FlushPartialSidecar(string fileName, IReadOnlyList<FdrScoreRecord> records)
            {
                string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                if (string.IsNullOrEmpty(sidecarBase))
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Cannot_write_the_first_pass_intermediate_file_for___0____no_location_could_be_derived_, fileName));
                    return 1;
                }
                string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);
                // The sidecar carries its own validity stamp - WHICH task, build and key produced
                // it - in the same atomic FileSaver commit as its records, so it is absent or
                // complete-and-identified, never one without the other.
                //
                // Per file, at write time, on purpose: this score pass takes 137 minutes over 446
                // files, so a machine lost partway (the Windows-Update case) must resume
                // proportional to the files it finished, not to the phase.
                try
                {
                    FdrScoresSidecar.Write(fdrPath, records, FdrScoresSidecar.Pass.FirstPass, sidecarStamp);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Failed_to_write_the_first_pass_intermediate_file_for__0____1_, fileName, ex.Message));
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
                    ctx.Get<LibraryById>().Value, config, ctx)
                : null;
            FeatureContributions mdiagContributions = null;
            Action<FeatureContributions> captureContributions = null;
            if (mdiagAccumulator != null)
                captureContributions = c => mdiagContributions = c;

            // Collapses the score pass's EXPERIMENT-scope columns to one record per distinct
            // entry_id (format v5, issue #4486). Protein FDR fills its protein q below, then
            // the whole thing is written once beside the blib.
            // PER-FILE RESUME GATE. Which files already carry a 1st-pass sidecar stamped with this
            // task's validity key, by this build - this arm and this cohort? Those need no
            // re-scoring: the sidecar holds their scores and run q-values, and the sink can be fed
            // from it.
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
                    OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming___0__of__1__files_already_have_up_to_date_first_pass_intermediate_files__scoring_,
                    resumableFiles.Count, projections.PerFile.Count,
                    projections.PerFile.Count - resumableFiles.Count));
                foreach (var kv in projections.PerFile)
                {
                    if (resumableFiles.Contains(kv.Key))
                        ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection___up_to_date___0_, kv.Key));
                }
            }

            // Training is the one phase whose product does not depend on how far the run got:
            // the model is a function of the cohort, the library, the arm, the classifier and the
            // seed, all of which the validity key covers. So a CURRENT .1st-pass.model.json is
            // reusable however many files were scored - which is the whole point of writing it
            // when training ends rather than at the end of the task. It saves 21 minutes of
            // training-subset feature loading at 446 files. Only the counts-only first pass
            // adopts it; the resident projection path always trains.
            //
            // The marker is what makes partial reuse safe. The all-sidecars-current gate below
            // corroborates cohort identity through the sidecars themselves; here there is no such
            // corroboration, so an unstamped model file - one written before this artifact was
            // stamped - is not adopted.
            //
            // Requires at least one file to actually be resumable. The model file is not a
            // declared Output, so deleting the declared ones - the standard way to force a clean
            // re-score - leaves it behind, and adopting it then would score every file with the
            // previous arm's discriminant while the log says only that a model was reused. With
            // one sidecar adopted there is already an on-disk score that model produced, so
            // reusing it is the consistent choice rather than a surprising one.
            var reloadedModel = LoadCurrentModelSidecar(perFileParquetPaths, sidecarValidityKey);
            PercolatorResults adoptedModel = resumableFiles.Count > 0
                ? reloadedModel?.Model
                : null;

            // OSPREY_PASS2_QVALUE=transfer / transfer-compete / protein-compact: publish the
            // trained (frozen) 1st-pass model so the SecondPassFDR 2nd pass can re-score the
            // reconciled features with it instead of retraining (transfer-compete / protein-compact
            // then recompute q/PEP by a fresh target-decoy competition). The projection first pass
            // (this method) is the SAME lean path the default percolator mode takes, so the model
            // must be captured HERE. Null (a pure
            // no-op in the engine) on the default path, so scoring stays byte-identical. Streaming
            // only, so no full-population pool is held resident (avoids the entrapment-library OOM).
            //
            // The model is also PERSISTED here, the moment training returns it, whenever it was
            // trained rather than adopted (PublishFirstPassModel). It used to be written at the
            // end of PlanStage6 instead, which on a 446-file cohort is 228 minutes after it was
            // computed - so a run killed anywhere in the score passes, protein FDR or the survivor
            // reload left its finished model nowhere on disk and had to retrain from scratch.
            // Training is the first phase, so persisting its product when it ends is what makes
            // every later phase resumable.
            Action<PercolatorResults> captureModel = results => PublishFirstPassModel(
                results, reloadedModel, adoptedModel, perFileParquetPaths, sidecarValidityKey, ctx);

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
            bool canEnterAtGate = false;
            // Each condition reported by name when it refuses. A silent fall-through here looks
            // exactly like a run that never had the artifacts, and the whole point of the fast
            // path is that an operator can tell why they are waiting 29 minutes instead of 8.
            if (projections.PerFile.Count > 0 && resumableFiles.Count == projections.PerFile.Count)
            {
                var refusals = CompactionGateRefusals(experimentPathForResume, sidecarValidityKey,
                    perFileParquetPaths, config, out resumeSidecar);
                if (refusals.Count > 0)
                {
                    ctx.LogInfo(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__the_first_pass_intermediate_files_are_up_to_date__but_first_pass_scoring_runs_,
                        string.Join(@"; ", refusals)));
                }
                canEnterAtGate = refusals.Count == 0;
            }
            if (canEnterAtGate)
            {
                ctx.LogInfo(string.Format(config.ModelDiagnostics
                        ? OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__first_pass_intermediate_files_for_all__0__files_are_up_to_date__skipping_first_
                        : OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__first_pass_intermediate_files_for_all__0__files_are_up_to_date__skipping_first_pass_scoring_and_protein_FDR,
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
                    ctx.LogVerbose(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Reloaded_the_saved_list_of__0__precursor_candidates_from_proteins_with_2_or_more_,
                        _proteinCompactStratum.Count));
                }
                // A run that re-enters here - killed after every sidecar landed but before this
                // task finished, and restarted - owes the same pass-1 FDRBench file the
                // uninterrupted run would have written at this point. Everything the emitter
                // reads is what made the gate entry legal (current per-file sidecars, a current
                // experiment sidecar), so it costs one streamed pass, not the score passes this
                // entry skips. This is the only re-entry that writes it: a COMPLETED run asked
                // again for --fdrbench never reaches this task at all, because the TSVs are not
                // declared outputs, and that is a pre-existing gap for pass 2 as much as pass 1.
                // The map is loaded once and handed on to the compaction predicate below, which
                // would otherwise read the same file again.
                IReadOnlyDictionary<uint, FdrExperimentRecord> experimentById = null;
                if (FdrBenchInputWriter.PathForPass(config, OspreyConfig.FDRBENCH_PASS_1) != null)
                {
                    experimentById = LoadFirstPassExperimentRecords(config, ctx);
                    if (experimentById == null)
                        return null;  // ExitCode set in the helper
                    if (!WriteFdrBenchPass1FromSidecarsIfRequested(
                            projections, perFileParquetPaths, experimentById, config, ctx))
                    {
                        return null;  // ExitCode set in the helper
                    }
                }
                return CompactFromSidecars(projections, perFileParquetPaths, beforeCount, config, ctx,
                    experimentById);
            }

            var experiment = new FdrExperimentAccumulator();
            var sink = new FdrStoringSink(projections, config, @"First-pass", FlushPartialSidecar,
                experiment, mdiagAccumulator);

            // Which files' 1st-pass sidecars are on disk. Seeded with the ones an earlier run
            // left, and GROWN by pass 1 as it writes each file it scores - which is what lets
            // pass 2 read every score back instead of reloading features and repeating the dot
            // product for all of them. An adopted file is marked on the sink for the same reason
            // a freshly written one is: its sidecar exists, so the sink must not write it again.
            // Read and grown from the score pass's file lanes at once, so every access holds its
            // lock.
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
                (fileName, fileIndex, rowCount, entryIds, scores, runPrecQ, runPeptQ, apexRts) =>
                {
                    var records = new List<FdrScoreRecord>(rowCount);
                    for (int r = 0; r < rowCount; r++)
                        records.Add(new FdrScoreRecord(
                            entryIds[r], scores[r], runPrecQ[r], runPeptQ[r], apexRts[r]));
                    // Marked before the result is known: a failed write must not be retried by
                    // the sink either, because FdrScoresSidecar registers the path on the way in
                    // and would refuse the second attempt as a double write.
                    sink.MarkSidecarWritten(fileIndex);
                    int failures = FlushPartialSidecar(fileName, records);
                    if (failures == 0)
                    {
                        lock (scoresOnDisk)
                            scoresOnDisk.Add(fileName);
                        return;
                    }
                    // Fatal HERE, naming the write that failed. Since the sink can no longer
                    // write this file either, continuing produces a run that walks every
                    // remaining row and then dies an hour later in the compaction gate saying
                    // the sidecar could not be READ - blaming a missing artifact instead of the
                    // write that never happened.
                    Interlocked.Add(ref pass1WriteFailures, failures);
                    throw new IOException(string.Format(
                        OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Could_not_write_the_first_pass_intermediate_file_for___0____and_the_analysis_cannot_, fileName));
                };
            var featureInfos = OspreyFeatureCalculators.BuildFeatureInfos(ParquetScoreCache.PIN_FEATURE_NAMES);
            var swFdr = Stopwatch.StartNew();
            bool aborted;
            // Stage B (issue #4355 struct-shrink S3): the lean 1st pass holds NO resident
            // FdrProjection[] -- stream every row's identity + features from the per-file
            // .scores.parquet. The row source reads the scalar columns (entry_id / charge /
            // is_decoy / coelution_sum / modseq) in parquet row order (== the resident sort
            // order on the 1st pass, since the parquet is written (entry_id,charge,scan)-sorted),
            // and loadFileFeatures loads that file's feature vectors by the running row ordinal.
            // perFileParquetPaths has every projection file, so the indexer cannot miss - but
            // NOT for the reason this used to give ("the counts-only producer read the same
            // parquet to count its rows"): the resume producer reads the footer now, not the
            // rows. What holds is that PerFileScoringTask sets perFileParquetPaths[fileName]
            // unconditionally, outside the fat/lean branch and outside any existence test,
            // and Run seeds every path up front. Moving that assignment inside the lean
            // branch - where it looks redundant, since that arm adds an empty entry list -
            // would break this indexer deep inside the Stage-5 streaming pass.
            var stubIdentity = StubIdentity(ctx);
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows =
                (fileName, columns, onRow) =>
                    ParquetScoreCache.ReadFdrStubScalars(perFileParquetPaths[fileName], onRow, columns, stubIdentity);
            // Feeds the scorer a file's scores off its 1st-pass sidecar so the pass does not
            // load that file's feature vectors or re-run the dot product. Consulted by BOTH
            // passes, and the set grows during pass 1 - so on a cold run this is what stops
            // pass 2 recomputing all 446 files' scores 82 minutes after pass 1 computed
            // them. Returns false for a file whose sidecar is not on disk, which scores
            // normally.
            CompletedScoreStreamer tryStreamCompletedScores =
                (fileName, onRecord) =>
                {
                    lock (scoresOnDisk)
                    {
                        if (!scoresOnDisk.Contains(fileName))
                            return false;
                    }
                    string doneBase = ScoringTaskShared.ResolveSidecarBasePath(
                        fileName, perFileParquetPaths, config);
                    if (string.IsNullOrEmpty(doneBase))
                        return false;
                    // Both run q-values travel with the score. The record carries them
                    // already - it is the record pass 1 wrote - so handing them on costs
                    // nothing here and saves the reader a sort per file.
                    return FdrScoresSidecar.ReadRecords(
                        FdrScoresSidecar.Pass1Path(doneBase), FdrScoresSidecar.Pass.FirstPass,
                        rec => onRecord(rec.EntryId, rec.Score,
                            rec.RunPrecursorQvalue, rec.RunPeptideQvalue));
                };
            // The adopted model is reused rather than skipped: the scorer still publishes it
            // through captureModel, so a second pass that needs the frozen first-pass model
            // gets the SAME model the scores on disk were produced by. Synthesising a stub to
            // satisfy the arithmetic would publish a meaningless model and corrupt pass 2
            // silently. See adoptedModel above for when a model is adopted.
            PercolatorResults pretrainedModel = adoptedModel;
            if (pretrainedModel != null)
            {
                ctx.LogInfo(CountText.Format(resumableFiles.Count,
                    OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__reusing_the_saved_first_pass_model_instead_of_training_again__1_of__1__files_,
                    OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__reusing_the_saved_first_pass_model_instead_of_training_again___0__of__1__files_,
                    projections.PerFile.Count));
            }
            else if (reloadedModel?.Model != null)
            {
                ctx.LogInfo(
                    OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_Resuming__a_saved_first_pass_model_exists_but_no_file_has_saved_scores__so_the_model_is_);
            }
            aborted = PercolatorEngine.RunFirstPassStreaming(
                projections.PerFile.ConvertAll(kv => kv.Key), streamFileRows, loadFileFeatures,
                config, featureInfos, ctx, sink, BuildPercolatorDiagnostics(ctx.Diagnostics),
                @"First-pass", captureContributions, captureModel, tryStreamCompletedScores,
                pretrainedModel, flushFileRunScope, ctx.RunPlan.FirstPassFdrLanes);
            // Says whether the pass-1 write actually engaged. Without it a run in which
            // flushFileRunScope never fired would look identical from the outside - the
            // sink would have written the same sidecars from pass 2, and the output would
            // be byte-identical - so the log is the only place the distinction is visible.
            ctx.LogVerbose(CountText.Format(projections.PerFile.Count,
                OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_First_pass__the_scores_of_the_file_were_saved_while_training_and_read_back__so_its_,
                OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_First_pass__the_scores_of__1__of__0__files_were_saved_while_training_and_read_back__so_,
                scoresOnDisk.Count));
            swFdr.Stop();
            if (aborted)
            {
                // A diagnostic-only (*Only) Stage 5 dump fired. The FDR engine left the run a
                // pure no-op and signalled here; the Tasks layer, not the engine, owns the
                // process exit.
                ctx.LogInfo(LogTag.BISECT, @"Percolator diagnostic-only dump complete - aborting run");
                Environment.Exit(0);
            }
            ctx.LogInfo(LogTag.TIMING, @"Percolator/Simple FDR: {0:F1}s",
                swFdr.Elapsed.TotalSeconds);
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"after first-pass Percolator FDR");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"first-pass-fdr-live",
                string.Format(@"(post-GC, projection path, rows={0})", projections.TotalRows));

            LogFirstPassResultsProjection(projections, sink, config, ctx);

            // --model-diagnostics: emit the pass-1 HTML report from the streamed accumulator.
            // Placed here, after first-pass Percolator and BEFORE first-pass protein FDR, so
            // the report reads no protein q it does not use.
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
                    experiment.Records, ctx);
                ModelDiagnosticsReport.WriteFromAccumulator(
                    mdiagAccumulator, mdiagContributions, cal, config, ctx, coAssignment,
                    ValidityKey(ctx));
            }

            // First-pass protein FDR streamed off the per-file sidecar + parquet scalars
            // (issue #4355 struct-shrink S2): read each file's Score / run_peptide_qvalue
            // from the just-written .1st-pass.fdr_scores.bin, joined by entry_id with the
            // modseq / IsDecoy from the parquet scalars, run the identical parsimony +
            // picked-protein FDR, and patch each row's experiment_protein_qvalue [52..60] straight
            // back onto the sidecar -- so the resident FdrProjectionOutputs array is gone.
            // The Stage-6 diagnostic dump reads the returned artifacts. Runs unconditionally (not gated
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
                ctx.LogInfo(LogTag.TIMING, @"First-pass protein FDR: {0:F1}s",
                    swProt.Elapsed.TotalSeconds);

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
                LogIntermediateWriteFailures(ctx, sidecarFailures, projections.PerFile.Count);
                ctx.ExitCode = 1;
                return null;
            }

            // FDRBench input TSV (pass 1): the full pre-compaction first pass, streamed off the
            // sidecars the score pass just finalized and the experiment map just written,
            // BEFORE compaction drops the non-survivors.
            if (!WriteFdrBenchPass1FromSidecarsIfRequested(
                    projections, perFileParquetPaths, experiment.Records, config, ctx))
            {
                return null;  // ExitCode set in the helper
            }

            // Compaction predicate streamed over the finalized per-file sidecar -> passing
            // base_id set (the same predicate RescoreCompaction applies, risk #7). The
            // stratum (protein-compact) admits present-protein peptides that failed 1st-pass FDR.
            return CompactFromSidecars(projections, perFileParquetPaths, beforeCount, config, ctx);
        }

        /// <summary>
        /// Why a resume whose per-file sidecars are all current may NOT enter at the compaction
        /// gate, one reason per condition that fails, or an empty list when it may. The model
        /// sidecar the conditions were checked against comes back through
        /// <paramref name="modelSidecar"/> (null when none was readable), so the caller publishes
        /// the object that passed rather than a second read of it.
        ///
        /// <para>The gate entry skips the score passes and publishes the persisted model as this
        /// run's first-pass model, so that model must be of the classifier this run uses. The
        /// validity key records the FDR method, so a current directory cannot hold the other
        /// classifier's model except through a defect; refusing here makes such a directory
        /// recompute instead of publishing it to Stage 6 and the second pass.</para>
        ///
        /// <para>protein-compact's gate admits present-protein peptides through the stratum, so
        /// entering at the gate WITHOUT it selects a different, smaller survivor set - and does
        /// it silently, because nothing downstream can tell a stratum that was never loaded from
        /// one that was legitimately empty. The stratum is its own artifact
        /// (.1st-pass.stratum.json, written when protein FDR ends), so a run killed before
        /// protein FDR has the model and no stratum and correctly falls through to recompute;
        /// <see cref="FirstPassModelIO.LoadFromAny"/> pairs the two from the same stem.</para>
        /// </summary>
        internal List<string> CompactionGateRefusals(string experimentPath, string validityKey,
            IReadOnlyDictionary<string, string> perFileParquetPaths, OspreyConfig config,
            out FirstPassModelIO.Sidecar modelSidecar)
        {
            var refusals = new List<string>();
            if (string.IsNullOrEmpty(experimentPath))
            {
                refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_there_is_no_output__blib_to_name_the_whole_experiment_first_pass_intermediate_file_after,
                    LibrarySource.EXT_BLIB));
            }
            else if (!PerFileResumeDriver.IsCurrent(experimentPath, Name, validityKey))
                refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_the_whole_experiment_first_pass_intermediate_file_is_not_up_to_date___0_, experimentPath));
            modelSidecar = FirstPassModelIO.LoadFromAny(perFileParquetPaths);
            if (modelSidecar == null)
            {
                refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_no_readable_first_pass_model_file___1st_pass_model_json__was_found_beside_the_inputs___,
                    FirstPassModelIO.EXT_MODEL, ParquetScoreCache.EXT_SCORES));
            }
            else if (modelSidecar.Model == null)
            {
                refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_the_first_pass_model_file___1st_pass_model_json__holds_no_model,
                    FirstPassModelIO.EXT_MODEL));
            }
            else if (modelSidecar.Stamp == null || !modelSidecar.Stamp.IsCurrent(Name, validityKey))
            {
                // A model left by an earlier run under another key - this run's own model write
                // failed - must not be published as this run's first pass.
                refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_the_first_pass_model_file___0___is_not_up_to_date,
                    FirstPassModelIO.EXT_MODEL));
            }
            else
            {
                bool runUsesTrees = config.FdrClassifier == FdrClassifier.Gbdt;
                if (modelSidecar.Model.IsGradientBoostedTrees != runUsesTrees)
                {
                    refusals.Add(string.Format(@".1st-pass.model.json holds {0} and this run uses {1}",
                        PercolatorResults.ClassifierName(modelSidecar.Model.IsGradientBoostedTrees),
                        PercolatorResults.ClassifierName(runUsesTrees)));
                }
                if (OspreyEnvironment.Pass2ProteinCompact && modelSidecar.StratumBaseIds == null)
                {
                    refusals.Add(string.Format(OspreyTasksResources.FirstPassFdrTask_RunFirstPassProjection_no_list_of_precursors_from_proteins_with_2_or_more_first_pass_peptides___0___was_found,
                        FirstPassModelIO.EXT_STRATUM, ParquetScoreCache.EXT_SCORES));
                }
            }
            return refusals;
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
        /// The first pass's hand-off of the model its scorer settled on: publish it for a frozen
        /// second pass, and persist it unless it is the model this run adopted off disk.
        ///
        /// <para>Persisted whenever it was TRAINED, whatever is on disk already. A current model
        /// file does not mean the run used it: the scorer adopts one only when some file's scores
        /// are resumable, and retrains otherwise. Stage 6 and a distributed SecondPassFDR read
        /// whichever model file is beside the inputs (<see cref="FirstPassModelIO.LoadFromAny"/>),
        /// so skipping the write left them scoring with the stale model - of the other classifier,
        /// after a switch of <c>OSPREY_FDR_MODEL</c> - while the first-pass scores came from the new
        /// one. Only the adopted model is not written: it IS that file, already stamped, and
        /// rewriting it would replace an artifact a marker attests with a copy the marker no
        /// longer describes.</para>
        /// </summary>
        /// <param name="results">The model the scorer trained or adopted.</param>
        /// <param name="reloadedModel">The current model sidecar found on disk, or null.</param>
        /// <param name="adoptedModel">The model the scorer was handed instead of training, or null
        ///   when it trains.</param>
        /// <param name="perFileParquetPaths">Stem to score parquet, for the per-file copies.</param>
        /// <param name="validityKey">The key each written copy is stamped with.</param>
        /// <param name="ctx">The pipeline context the model is published on.</param>
        internal void PublishFirstPassModel(PercolatorResults results,
            FirstPassModelIO.Sidecar reloadedModel, PercolatorResults adoptedModel,
            IReadOnlyDictionary<string, string> perFileParquetPaths, string validityKey,
            PipelineContext ctx)
        {
            bool adopted = adoptedModel != null && ReferenceEquals(results, adoptedModel);
            if ((OspreyEnvironment.Pass2TransferQ || OspreyEnvironment.Pass2ProteinCompact) &&
                !ctx.TryGet<FirstPassPercolatorModel>(out _))
            {
                // Stamp the arm the TRAINING process ran under; the 2nd pass may be another process.
                ctx.Publish(new FirstPassPercolatorModel
                {
                    Results = results,
                    // For an adopted model that is the arm recorded beside it, not this one's: the
                    // recorded arm is what the pass-2 mean-best-N refusal is evaluated against, so
                    // re-reading the environment would judge a reused model by the wrong arm. A
                    // model trained here was trained under this process's arm.
                    ExperimentAgg = adopted
                        ? reloadedModel?.ExperimentAgg ?? OspreyEnvironment.ExperimentAgg
                        : OspreyEnvironment.ExperimentAgg
                });
            }
            if (!adopted)
                PersistFirstPassModel(results, perFileParquetPaths, validityKey, ctx);
        }

        /// <summary>
        /// Persist the trained 1st-pass model beside each file's other Stage-5 sidecars, the
        /// moment training produces it. Written per file (identical copies) so a distributed
        /// <c>--task SecondPassFDR</c> node finds it by the same input-file stem it uses for
        /// every other reconciled sidecar.
        ///
        /// <para>Best-effort: a write failure must not fail the run, because nothing downstream
        /// requires the file to exist - SecondPassFDR keeps its pre-existing fail-fast and a
        /// resume simply retrains. <see cref="FirstPassModelIO.Serialize"/> renders either
        /// classifier and declines only a model <see cref="FirstPassModelIO.Load"/> could not read
        /// back - a degenerate one, which carries nothing to persist.</para>
        /// </summary>
        private void PersistFirstPassModel(
            PercolatorResults results, IReadOnlyDictionary<string, string> perFileParquetPaths,
            string validityKey, PipelineContext ctx)
        {
            if (results == null || perFileParquetPaths == null)
                return;
            // Serialized ONCE. The copies are byte-identical, and a tree model is ~3.4 MB of JSON
            // that took 1.15 s to render, measured - per copy, when this rendered it per file.
            // Best-effort like the writes: a model that cannot be rendered (a non-finite weight)
            // is logged and not persisted, as it was when each per-file Save threw.
            string modelJson;
            try
            {
                modelJson = FirstPassModelIO.Serialize(results, OspreyEnvironment.ExperimentAgg,
                    ArtifactStamp.ForCurrentBuild(Name, validityKey));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(@"Could not serialize the 1st-pass model: " + ex.Message);
                modelJson = null;
            }
            int modelWrites = 0;
            string firstPath = null;
            foreach (var kvp in perFileParquetPaths)
            {
                string path = FirstPassModelIO.PathFor(kvp.Value, kvp.Key);
                // WriteText commits through FileSaver and the JSON carries its own validity stamp
                // (which task, build and key made it), so the artifact is absent or complete and
                // identified. A model left by an earlier run is current only if this build wrote
                // it with this key, so a declined model here cannot be mistaken for it.
                if (modelJson == null)
                    continue;
                try
                {
                    FirstPassModelIO.WriteText(path, modelJson);
                    modelWrites++;
                    firstPath = firstPath ?? path;
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(OspreyTasksResources.FirstPassFdrTask_PersistFirstPassModel_Could_not_save_the_first_pass_model_for___0_____1_,
                        kvp.Key, ex.Message));
                }
            }
            // One path, not one per file: the copies are identical, and a 446-file run would
            // otherwise print 446 lines here. A resume reuses the model instead of retraining.
            if (modelWrites == 1)
            {
                ctx.LogInfo(string.Format(OspreyTasksResources.FirstPassFdrTask_PersistFirstPassModel_Saved_the_trained_first_pass_model_to__0_, firstPath));
            }
            else if (modelWrites == 2)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_PersistFirstPassModel_Saved_the_trained_first_pass_model_to__0__and_for_the_other_input, firstPath));
            }
            else if (modelWrites > 2)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_PersistFirstPassModel_Saved_the_trained_first_pass_model_to__0__and_for_each_of_the_other__1__inputs,
                    firstPath, modelWrites - 1));
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
            long beforeCount,
            OspreyConfig config,
            PipelineContext ctx,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentById = null)
        {
            var firstPassBaseIds = ComputeFirstPassBaseIds(
                projections, perFileParquetPaths, config, ctx, _proteinCompactStratum, experimentById,
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
                perFileParquetPaths, config, firstPassBaseIds, ctx.Get<SequencePool>().Value)
            {
                Identity = StubIdentity(ctx)
            };

            // Summing the per-base_id row counts over the passing set gives exactly what a
            // materialized reload would have counted: a survivor is a parquet row whose
            // base_id passed, and the sidecar carries one record per parquet row.
            int afterCount = 0;
            foreach (uint baseId in firstPassBaseIds)
            {
                if (rowsPerBaseId.TryGetValue(baseId, out int rows))
                    afterCount += rows;
            }
            ScoringTaskShared.LogCompaction(ctx, beforeCount, afterCount, firstPassBaseIds.Count, null);
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"after Stage-5 CompactFirstPass");
            // The loads must sum to the count just reported. Only when every file has its own
            // stem: the loader is keyed by stem, so two inputs sharing one would both rebuild
            // from the same parquet (#4544) and the sum could not match.
            if (perFileParquetPaths.Count == projections.PerFile.Count)
                _survivorLoader.ExpectedSurvivorCount = afterCount;

            var survivors = new List<KeyValuePair<string, List<FdrEntry>>>(projections.PerFile.Count);
            foreach (var kvp in projections.PerFile)
                survivors.Add(new KeyValuePair<string, List<FdrEntry>>(kvp.Key, new List<FdrEntry>()));
            return survivors;
        }

        /// <summary>
        /// Log per-file and total first-pass passing-target counts from the projection
        /// rows.
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
                ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(OspreyTasksResources.FirstPassFdrTask_LogFirstPassResultsProjection____0____1__precursors_at__2__run_level_FDR,
                    projections.PerFile[f].Key, fileTargets, config.RunFdr));
                passingTargets += fileTargets;
            }
            LogRunLevelTotal(ctx, passingTargets, projections.PerFile.Count);
        }

        /// <summary>
        /// The total under the per-file run-level lines. Left out for one file, whose own line
        /// above already states the same number.
        /// </summary>
        private static void LogRunLevelTotal(PipelineContext ctx, int passingTargets, int nFiles)
        {
            if (nFiles > 1)
            {
                ctx.LogInfo(string.Format(OspreyTasksResources.FirstPassFdrTask_LogRunLevelTotal_Total___0__precursors_pass_run_level_FDR_across__1__files,
                    passingTargets, nFiles));
            }
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
            // Each file is reduced on its own lane and merged here in file order, which
            // FirstPassProteinFdrAccumulator.Merge makes identical to adding every row in order.
            ctx.LogBlockReads(@"First-pass before protein FDR");
            var accumulator = new FirstPassProteinFdrAccumulator(config.RunFdr);
            var stubIdentity = StubIdentity(ctx);
            int proteinReduceFiles = 0;
            string reduceError = null;
            using (var reduceProgress = new ProgressReporter(CountText.Format(projections.PerFile.Count,
                       OspreyTasksResources.FirstPassFdrTask_RunFirstPassProteinFdrStreaming_Computing_first_pass_protein_FDR_for_1_file,
                       OspreyTasksResources.FirstPassFdrTask_RunFirstPassProteinFdrStreaming_Computing_first_pass_protein_FDR_for__0__files), projections.PerFile.Count))
            {
                OrderedFileLanes.RunWhile(projections.PerFile.Count, ctx.RunPlan.FirstPassFdrLanes, f =>
                {
                    var fileAccumulator = new FirstPassProteinFdrAccumulator(config.RunFdr);
                    string error = TryStreamFirstPassFileScores(projections.PerFile[f].Key, perFileParquetPaths, config, stubIdentity,
                        projections.RowCount(f), (modseq, charge, isDecoy, record) =>
                            fileAccumulator.Add(modseq, isDecoy, record.Score, record.RunPeptideQvalue));
                    return (Accumulator: fileAccumulator, Error: error);
                }, (f, file) =>
                {
                    reduceProgress.Report(++proteinReduceFiles);
                    reduceError = file.Error;
                    if (reduceError != null)
                        return false;
                    accumulator.Merge(file.Accumulator);
                    return true;
                });
            }
            if (reduceError != null)
            {
                ctx.LogError(reduceError);
                ctx.ExitCode = 1;
                return null;
            }
            var result = accumulator.Finish(fullLibrary, config);
            ProteinFdrEngine.LogFirstPassSummary(result, config, ctx);

            // Pass 2: resolve each entry's experiment_protein_qvalue from peptide -> q into the
            // experiment-scope map, which is written once beside the blib after this returns.
            // The modseq must be the one the pass-1 PeptideQvalues keys were built from. Both
            // come from the same source per row group - the library entry the entry_id names
            // when the library holds every id (OSPREY_STUB_IDENTITY=1), the parquet otherwise -
            // and the library entry is exactly the peptide the file was written with, so the
            // peptide -> q lookup matches either way.
            //
            // Before the v5 scope split this loop REWROTE every file's sidecar to push the
            // value back in - 52.3 GB of serial, un-parallelizable rewrite at 257 files, in the
            // stage that is already the bottleneck, to store one number per run of a value that
            // is the same in every run. It now finishes an in-memory map instead, and the
            // per-file sidecars written by the score pass are never reopened (issue #4486).
            //
            // A lane reads a file's rows and resolves each one's protein q against the finished
            // (read-only) peptide map; the experiment map is updated here, in file order. The
            // file reduces to one value per entry - the LAST row's, which is the one a row-by-row
            // update would leave - so applying it is the same assignment, fewer times.
            var peptideQvalues = result.ProteinFdr.PeptideQvalues;
            int proteinResolveFiles = 0;
            int resolveFailures = 0;
            using (var resolveProgress = new ProgressReporter(CountText.Format(projections.PerFile.Count,
                       OspreyTasksResources.FirstPassFdrTask_RunFirstPassProteinFdrStreaming_Resolving_first_pass_protein_q_values_for_1_file,
                       OspreyTasksResources.FirstPassFdrTask_RunFirstPassProteinFdrStreaming_Resolving_first_pass_protein_q_values_for__0__files), projections.PerFile.Count))
            {
                OrderedFileLanes.Run(projections.PerFile.Count, ctx.RunPlan.FirstPassFdrLanes, f =>
                {
                    string parquetPath = perFileParquetPaths[projections.PerFile[f].Key];  // present: pass 1 read it
                    var qByEntryId = new Dictionary<uint, double>();
                    try
                    {
                        ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                            (entryId, charge, isDecoy, coelutionSum, modseq, apexRt) =>
                            {
                                double q;
                                // Normalize a present-but-null modseq to "" so the lookup matches the
                                // pass-1 PeptideQvalues keys (the accumulator normalizes the same way);
                                // a null Dictionary key would otherwise throw. See TryStreamFirstPassFileScores.
                                if (!peptideQvalues.TryGetValue(modseq ?? string.Empty, out q))
                                    q = 1.0;
                                qByEntryId[entryId] = q;
                            },
                            StubColumns.Core | StubColumns.SkipCoelutionSum, stubIdentity);
                        return (Resolved: qByEntryId, Error: null);
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException))
                    {
                        // The rows resolved before the failure still apply, as they did when the
                        // update ran row by row. Out of memory is not a file's fault and must stop
                        // the run, not become a warning.
                        return (Resolved: qByEntryId, Error: ex.Message);
                    }
                }, (f, file) =>
                {
                    resolveProgress.Report(++proteinResolveFiles);
                    foreach (var kv in file.Resolved)
                        experiment.SetProteinQvalue(kv.Key, kv.Value);
                    if (file.Error != null)
                    {
                        ctx.LogWarning(string.Format(
                            OspreyTasksResources.FirstPassFdrTask_RunFirstPassProteinFdrStreaming_Failed_to_resolve_first_pass_protein_q_values_for__0____1_,
                            projections.PerFile[f].Key, file.Error));
                        resolveFailures++;
                    }
                });
            }
            patchFailures = resolveFailures;
            return result;
        }

        // Experimental (OSPREY_STUB_IDENTITY): the library lookup that lets the first-pass walks
        // take charge, decoy flag and peptide from entry_id rather than decode them from every
        // row. Null when the switch is off. Call before starting lanes: it builds on first use.
        private LibraryIdentity StubIdentity(PipelineContext ctx)
        {
            if (OspreyEnvironment.StubIdentity == 0)
                return null;
            return _stubIdentity ?? (_stubIdentity = new LibraryIdentity(ctx.Get<FullLibrary>().Value));
        }

        /// <summary>
        /// Stream one file's first-pass rows to <paramref name="onRow"/> as
        /// <c>(modseq, charge, isDecoy, FdrScoreRecord)</c>: read the file's
        /// <c>.1st-pass.fdr_scores.bin</c> into an entry_id -> record map (one file resident;
        /// bounded), then stream the parquet scalars (the modseq source PeptideById was
        /// interned from + charge + IsDecoy) in parquet-row order, joining each row to its
        /// sidecar record by entry_id. Returns the error to report - null on success - for a
        /// missing parquet path, a missing sidecar base path, or an unreadable / size-mismatched
        /// sidecar. A parquet row whose entry_id is absent from the sidecar is SKIPPED, not a
        /// fault: the sidecar is a SUBSET of the parquet rows, so a row with no record is simply
        /// not a first-pass row (superset tolerance mirroring the survivor reload - see the
        /// inline note below). Shared by the first-pass protein FDR and the pass-1 FDRBench
        /// emitter, which is why the walk is in parquet-row order: both consumers depend on
        /// seeing rows in the order the resident path listed them.
        ///
        /// <para>Touches no shared state and logs nothing, so it runs on a file lane. The caller
        /// reports the error on reaching the file in file order - the same file, and only that
        /// file, the sequential walk would have stopped at.</para>
        /// </summary>
        private static string TryStreamFirstPassFileScores(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            LibraryIdentity identity,
            int parquetRows,
            Action<string, byte, bool, FdrScoreRecord> onRow)
        {
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                return string.Format(
                    OspreyTasksResources.FirstPassFdrTask_StreamFirstPassFileScores_Cannot_locate_the_scores_file_for___0___, fileName);
            }
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(sidecarBase))
            {
                return string.Format(
                    OspreyTasksResources.FirstPassFdrTask_StreamFirstPassFileScores_Cannot_locate_the_first_pass_intermediate_files_for___0___, fileName);
            }
            string fdrPath = FdrScoresSidecar.Pass1Path(sidecarBase);

            var records = new List<FdrScoreRecord>();
            if (!FdrScoresSidecar.ReadRecords(fdrPath, FdrScoresSidecar.Pass.FirstPass, records.Add))
            {
                return string.Format(
                    OspreyTasksResources.FirstPassFdrTask_StreamFirstPassFileScores_Failed_to_read_the_first_pass_intermediate_file_for___0_____1_, fileName, fdrPath);
            }
            // OSPREY_STUB_IDENTITY=1 (the default): every sidecar writer emits exactly the
            // parquet's rows, in parquet-row order, and the library supplies each row's peptide,
            // charge and decoy flag from its entry_id - so when the library holds every id, the
            // rows stream from the sidecar alone and the parquet is never opened. That is the join
            // below only while the sidecar really is one record per parquet row with unique ids in
            // the parquet's ascending order; a sidecar that is not - repeated, reordered or extra
            // records - takes the join, which keeps the last record per id and only parquet rows.
            if (identity != null && OspreyEnvironment.StubIdentity == 1 &&
                IsOneRecordPerRow(records, parquetRows) &&
                TryStreamFromLibrary(records, identity, onRow))
            {
                return null;
            }
            var recordByEntryId = new Dictionary<uint, FdrScoreRecord>(records.Count);
            foreach (var record in records)
                recordByEntryId[record.EntryId] = record;
            records = null;

            ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                (entryId, charge, isDecoy, coelutionSum, modseq, apexRt) =>
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
                        onRow(modseq ?? string.Empty, charge, isDecoy, record);
                },
                StubColumns.Core | StubColumns.SkipCoelutionSum, identity);
            return null;
        }

        // True when the sidecar holds one record per parquet row with strictly ascending entry
        // ids - the parquet's own order, since its rows are unique and sorted by entry id.
        private static bool IsOneRecordPerRow(List<FdrScoreRecord> records, int parquetRows)
        {
            if (records.Count != parquetRows)
                return false;
            for (int i = 1; i < records.Count; i++)
            {
                if (records[i].EntryId <= records[i - 1].EntryId)
                    return false;
            }
            return true;
        }

        // The sidecar-only half of TryStreamFirstPassFileScores. False, having emitted nothing,
        // when the library lacks any of the file's ids.
        private static bool TryStreamFromLibrary(List<FdrScoreRecord> records, LibraryIdentity identity,
            Action<string, byte, bool, FdrScoreRecord> onRow)
        {
            var entries = new LibraryEntry[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                entries[i] = identity.Find(records[i].EntryId);
                if (entries[i] == null)
                    return false;
            }
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                onRow(entries[i].ModifiedSequence ?? string.Empty, entries[i].Charge,
                    (record.EntryId & LibraryEntry.DECOY_ID_BIT) != 0, record);
            }
            return true;
        }

        /// <summary>
        /// Compute the post-first-pass passing base_id set by streaming the finalized per-file
        /// <c>.1st-pass.fdr_scores.bin</c> sidecar, using the compaction predicate (targets
        /// whose run peptide
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
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentById,
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
            // config.reconciliation_compaction_fdr (pipeline.rs:4650), not hardwired to
            // config.RunFdr.
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
            // A caller that already holds the map (the compaction-gate resume, which loaded it for
            // the FDRBench emitter) passes it; the cold arm reads the file it just wrote, which is
            // also the round trip that proves the persisted sidecar is what downstream will see.
            var experimentByEntryId = experimentById ?? LoadFirstPassExperimentRecords(config, ctx);
            if (experimentByEntryId == null)
                return null;  // ExitCode set in the helper

            // Each file's passing set and row counts are built on its own lane and merged here in
            // file order: a union and integer sums, so the result - and the order each base_id
            // first enters either collection - is what one walk over every file gives.
            ctx.LogBlockReads(@"First-pass before trim");
            int compactFiles = 0;
            string compactError = null;
            using (var compactProgress = new ProgressReporter(ScoringTaskShared.IsSingleFileSearch(config)
                       ? OspreyTasksResources.FirstPassFdrTask_ComputeFirstPassBaseIds_Trimming_first_pass_results_to_the_precursor_candidates_kept_for_re_scoring_and_second_
                       : string.Format(OspreyTasksResources.FirstPassFdrTask_ComputeFirstPassBaseIds_Trimming_first_pass_results_to_the_precursor_candidates_kept_for_cross_run_reconciliation_,
                           projections.PerFile.Count),
                       projections.PerFile.Count))
            {
                OrderedFileLanes.RunWhile(projections.PerFile.Count, ctx.RunPlan.FirstPassFdrLanes, f =>
                {
                    string fileName = projections.PerFile[f].Key;
                    var filePassing = new HashSet<uint>();
                    var fileRowCounts = new Dictionary<uint, int>();
                    string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
                    if (string.IsNullOrEmpty(sidecarBase))
                    {
                        return (Passing: filePassing, RowCounts: fileRowCounts, Error: string.Format(
                            OspreyTasksResources.FirstPassFdrTask_ComputeFirstPassBaseIds_Cannot_locate_the_first_pass_intermediate_files_for___0___, fileName));
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
                                fileRowCounts[rowBaseId] = fileRowCounts.TryGetValue(rowBaseId, out int n) ? n + 1 : 1;
                                if ((record.EntryId & LibraryEntry.DECOY_ID_BIT) != 0)
                                    return;
                                uint baseId = rowBaseId;
                                double proteinQ =
                                    experimentByEntryId.TryGetValue(record.EntryId, out var exp)
                                        ? exp.ExperimentProteinQvalue
                                        : 1.0;
                                // The stratum clause (protein-compact) admits present-protein peptides
                                // (>=2 first-pass-detected-peptide proteins) that failed 1st-pass FDR.
                                if (record.RunPeptideQvalue <= peptideGate ||
                                    proteinQ <= proteinGate ||
                                    (stratum != null && stratum.Contains(baseId)))
                                {
                                    filePassing.Add(baseId);
                                }
                            }))
                    {
                        return (Passing: filePassing, RowCounts: fileRowCounts, Error: string.Format(
                            OspreyTasksResources.FirstPassFdrTask_ComputeFirstPassBaseIds_Failed_to_read_the_first_pass_intermediate_file_for__0___expected_at__1___, fileName, fdrPath));
                    }
                    return (Passing: filePassing, RowCounts: fileRowCounts, Error: null);
                }, (f, file) =>
                {
                    compactProgress.Report(++compactFiles);
                    compactError = file.Error;
                    if (compactError != null)
                        return false;
                    foreach (var kv in file.RowCounts)
                        rowCounts[kv.Key] = rowCounts.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;
                    firstPassBaseIds.UnionWith(file.Passing);
                    return true;
                });
            }
            if (compactError != null)
            {
                ctx.LogError(compactError);
                ctx.ExitCode = 1;
                return null;
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
                ctx.LogError(string.Format(OspreyTasksResources.FirstPassFdrTask_LoadFirstPassExperimentRecords_There_is_no_output__blib__so_there_is_no_whole_experiment_first_pass_intermediate_file_to_,
                    LibrarySource.EXT_BLIB));
                ctx.ExitCode = 1;
                return null;
            }
            var map = FdrExperimentSidecar.ReadMap(path, FdrScoresSidecar.Pass.FirstPass);
            if (map == null)
            {
                ctx.LogError(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_LoadFirstPassExperimentRecords_Failed_to_read_the_whole_experiment_first_pass_intermediate_file___0_, path));
                ctx.ExitCode = 1;
            }
            return map;
        }

    }
}
