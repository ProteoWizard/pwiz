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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Contracts;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Aggregate counts returned by <see cref="PerFileRescoreTask.ExecuteRescore"/>.
    /// Mirrors <c>RescoreStats</c> in
    /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
    /// </summary>
    public class RescoreStats
    {
        /// <summary>
        /// Total entries re-scored across all files: existing
        /// (consensus + reconciliation) plus gap-fill (CWT + forced).
        /// </summary>
        public int TotalRescored { get; set; }

        /// <summary>
        /// Number of non-Keep reconciliation actions executed across all files.
        /// </summary>
        public int TotalReconciliation { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the CWT-detected pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapCwt { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the forced-integration pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapForced { get; set; }
    }

    /// <summary>
    /// Stage 6 per-file rescore phase: re-scores each input file's
    /// previously-scored entries against the consensus + reconciliation
    /// boundaries produced by the FirstPassFDR phase, runs the gap-fill
    /// two-pass for missing precursors, and writes the reconciled
    /// results back into the per-file <c>.scores.parquet</c>. The HPC
    /// "second per-file fan-out" boundary in the
    /// <c>Osprey-workflow.html</c> view -- each input file's rescore is
    /// independent of the others.
    ///
    /// Single entry point: <see cref="Run"/> is invoked by
    /// <c>AnalysisPipeline</c>'s task driver during both
    /// straight-through pipeline runs and the stage6 worker mode
    /// (<c>--task PerFileRescoring</c>). The worker
    /// mode previously had a separate <c>RunWorker</c> entry that
    /// hand-assembled the upstream hydration; Phase C collapsed that
    /// path so the canonical pipeline's membership rule (OspreyConfig.Includes) + the
    /// upstream tasks' lazy-rehydrate handle it. Run reads upstream state
    /// as typed byproducts through <c>ctx.Get&lt;CompactedEntries&gt;()</c>, etc. (a cache
    /// miss materializes the producing task) - each run's actions, gap-fill and refined
    /// calibration are planned here from that run's survivors and the analysis-wide
    /// consensus, and written to its <c>reconciliation.json</c> - dispatches into
    /// <see cref="ExecuteRescore"/>, then runs the per-process
    /// diagnostic-writer close + cross-impl bisection dump. Reaches the
    /// scoring engine (RunCoelutionScoring via <see cref="ScoringPipeline"/>) directly
    /// rather than through a base class.
    /// </summary>
    internal sealed class PerFileRescoreTask : OspreyTask
    {
        // Equal-weight progress segments one file's Stage 6 rescore is divided into
        // for the --parallel-files "[i] p%" aggregate line: reload spectra, re-score
        // the subset, write the reconciled parquet. RescoreOneFile advances them via
        // MultiProgressReporter.Current?.BeginSegment(); off the parallel path those
        // are no-ops.
        private const int RESCORE_FILE_SEGMENTS = 3;

        // Captured during Run so SecondPassFdrTask (downstream) can reach
        // the post-rescore version. Per the ownership-transfer semantics
        // of the pipeline: this task is the producer of the post-rescore
        // perFileEntries; consumers query us rather than
        // PerFileScoringTask. When Run is a no-op (no planning state)
        // the list reference falls through unchanged from
        // PerFileScoringTask.
        private List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;

        // What the per-run rebuild will overlay, decided by Run and executed later, when a
        // consumer reads a run through the RescoredEntries milestone. Null until Run decides;
        // PoolPlanForBuild throws on a null rather than guessing, because every wrong guess here
        // is a plausible number rather than an error.
        private RescoredPoolPlan _poolPlan;

        // The entry_ids whose scores the rescore reset, per file, captured as it happened.
        // The deferred pool build has to reproduce that reset on a list it reloads from disk,
        // and the planner's indices address the list as the rescore saw it - an ordering the
        // rebuild is only accidentally guaranteed to reproduce, and will NOT once Stage 7
        // loads from the reconciled parquet, whose gap-fill rows are interleaved in canonical
        // position and shift every index after them (#4486). An id survives that; a position
        // does not.
        private readonly Dictionary<string, HashSet<uint>> _resetEntryIdsByFile
            = new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);

        // Guards the dictionary ABOVE, not the sets inside it. RescoreOneFile reaches it from
        // inside ExecuteRescore's parallel loop over files, and its TryGetValue-then-insert is a
        // read-modify-write: two workers inserting across a resize can drop an entry or walk a
        // torn bucket chain. A dropped key makes ResetRescoredTargetsForFile return early, so
        // that file's rescored survivors keep first-pass q-values in the rebuilt Stage 7 pool -
        // the over-reporting the reset exists to prevent, with no error. Each file's own
        // HashSet needs no lock: exactly one worker owns a file.
        private readonly object _resetEntryIdsLock = new object();

        // Admits one survivor refill at a time under the parallel file loop, so the
        // pre-compaction transient each load holds is not multiplied by the file parallelism.
        // See RescoreOneFileStreamed for why that trade is free.
        private readonly object _survivorLoadLock = new object();

        // Training exports that failed this run, by input: an export that failed while its run
        // re-scored is reported with the rest by WriteTrainingExports rather than retried there.
        private readonly ConcurrentDictionary<string, string> _trainingExportErrors =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// This task's name, as a constant so the CLI selector, the validity stamp another
        /// task looks for, and the tests all spell it from here rather than duplicating it (#4486).
        /// </summary>
        public const string TASK_NAME = OspreyTaskNames.PER_FILE_RESCORING;

        public override string Name => TASK_NAME;

        /// <summary>
        /// The Stage 6 fan-out worker: one run's scores in, its reconciled parquet out.
        /// </summary>
        public override bool IsPerFileWorker => true;

        /// <summary>
        /// Consumes the per-run survivor loader and nothing else, so it takes the bounded
        /// route rather than the all-runs bundle.
        /// </summary>
        public override bool HydratesPerRun => true;

        /// <summary>
        /// The worker writes each run's reconciled parquet and pass-2 sidecars, never the
        /// blib; <c>--output</c> is required only to locate the analysis-wide sidecars
        /// beside it (the retained base_id summary, the experiment sidecar). Naming the blib
        /// would read as "the blib is being rebuilt".
        /// </summary>
        public override string DescribeOutput(OspreyConfig config)
        {
            return DescribePerInputOutput(config, ParquetScoreCache.GetReconciledScoresPath,
                ParquetScoreCache.EXT_SCORES_RECONCILED, config.OutputDir);
        }

        // The final milestone of the shared mutable entry buffer: this task
        // overlays the Stage 6 rescore (or, in the --task SecondPassFDR reconciled-input path,
        // applies its own compaction) onto the same backing list. SecondPassFDR
        // pulls RescoredEntries, so a cache miss lazily materializes this task --
        // which is exactly what triggers the rescore/compaction in reconciled-input mode
        // where the driver does not run this task.
        public override IEnumerable<Type> Publishes => new[] { typeof(RescoredEntries) };

        // _perFileEntries is the shared buffer this task overlays in place; it is
        // published as the RescoredEntries milestone (in Run and the SecondPassFDR-mode
        // Rehydrate) for SecondPassFDR to pull via ctx.Get<RescoredEntries>().
        // Each run is rebuilt by the consumer that reads it, one at a time, so this task
        // ends where its HPC contract says it does - one file, then exit (issue #4597).

        // Phase B resume surface. The reconciled parquet is written to a
        // SEPARATE <stem>.scores-reconciled.parquet sibling, leaving the
        // upstream PerFileScoringTask <stem>.scores.parquet intact (so a
        // partial Stage 6 crash can no longer half-rewrite the Stage 4
        // output, and downstream readers can fall back to the original
        // for files that had no reconciliation work). ValidityKey adds the
        // reconciliation parameter hash because the rescored content
        // depends on it.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return FdrScoresSidecar.Pass1Path(input);
            if (ctx.Config.Reconciliation != null && ctx.Config.Reconciliation.Enabled)
            {
                string consensusPath = Stage6ConsensusFile.PathFor(ctx.Config);
                if (!string.IsNullOrEmpty(consensusPath))
                    yield return consensusPath;
            }
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            // A reconciled path per input, and every one of them is now written -
            // a file with no consensus/reconciliation/gap-fill work gets a faithful
            // copy (WriteUnchangedReconciled) rather than nothing. That is what lets
            // Stage 7 read ONE artifact per file instead of falling back to the Stage 4
            // parquet for whichever files had no work (issue #4486), and it also lets the
            // driver's task-level IsTaskAlreadyDone - which requires EVERY declared output
            // to exist - short-circuit the whole task on resume. Previously a single
            // no-work file blocked that short-circuit, and Run was re-entered to
            // fast per-file-skip its way back to the same answer.
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.GetReconciledScoresPath(input);

            // Each run's Stage 6 plan, written by this task as it plans the run just before
            // rescoring it, under the stamp its reconciled parquet carries.
            foreach (var input in ctx.Config.InputFiles)
                yield return ReconciliationFile.PathForInput(input);

            // The training export (--training-export) is this task's product: it reads the
            // spectra, reconciled boundaries and run q-values this task holds or has just
            // written, so it is declared here rather than as a stage of its own (P17). Added to
            // a finished analysis, it is then the only output outstanding, which is the
            // export-only arm at the top of Run.
            if (ExportsTraining(ctx.Config))
            {
                foreach (var input in ctx.Config.InputFiles)
                    yield return TrainingExportWriter.PathFor(input);
            }

            // The per-run 2nd-pass FDR sidecar is THIS task's output (#4486): the per-file half
            // of the second pass runs in the rescore worker, so the file is produced here and
            // merely READ by SecondPassFDR. Declaring it matters beyond provenance - the driver
            // checks each declared output's embedded stamp against the declaring task, so
            // leaving it on SecondPassFDR's list gave one binary two owners.
            //
            // In EVERY pass-2 mode and for every run (#4665). Both modes have a per-file half
            // the worker runs, and a run with no Stage 6 work gets its answer written like any
            // other, so there is no run whose sidecar some other task owes.
            foreach (var input in ctx.Config.InputFiles)
            {
                yield return FdrScoresSidecar.Pass2Path(input);
                // The decoy side of this file's competition, written by the same worker in the
                // same call (#4486). Declared for the same reason the sidecar is: it is an
                // output of THIS task, its validity is this task's validity, and a resume that
                // found the pool image but not this file would fold an empty null. Two declared
                // outputs make that state fail the task rather than be discovered downstream.
                // Only where there IS a competition: the transfer competes nothing.
                if (OspreyEnvironment.Pass2ProteinCompact)
                    yield return Pass2CompetitionDecoys.PathFor(input);
            }
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            // Stage 6 rescore + reconciliation gate on the experiment precursor q-value, which
            // the experiment-wide aggregation changes, so the reconciled parquets this task
            // writes differ across arms and must be invalidated alongside FirstPassFdrTask's Stage-5
            // outputs. Same shared suffix, so the two cannot disagree.
            // Same shared suffix for the 2nd-pass mode, for the same reason: this task writes the
            // per-file 2nd-pass sidecar, whose q-values ARE the mode's output.
            // The sidecar format version belongs here too, not only in FirstPassFdrTask: this
            // task WRITES the 2nd-pass sidecar, so a record-layout change (v3 -> v4) invalidates
            // its output exactly as it invalidates the 1st-pass one. Without it, FirstPassFDR
            // re-ran and rewrote v4 while this task and SecondPassFDR considered themselves
            // valid against v3 files.
            return base.ValidityKey(ctx)
                + @";fdrsidecar=" + FdrScoresSidecar.FormatVersion
                // NO ";expsidecar=" term. The experiment sidecar's format does not change with
                // this work: the best-of-runs floor is APPLIED to the q-values before they are
                // written rather than stored beside them, so the record keeps its columns and its
                // version. A term here would invalidate this task for a format that did not move,
                // and invalidating FirstPassFDR costs a 5-hour Stage 5 re-run at 446 files.
                + @";reconciliation=" + ctx.Config.Identity.ReconciliationParameterHash()
                + OspreyEnvironment.ExperimentAggValidityKeySuffix()
                + OspreyEnvironment.Pass2QValueValidityKeySuffix()
                + OspreyEnvironment.TrainSampleValidityKeySuffix()
                + LibraryFragmentRelease.ValidityKeySuffix(ctx)
                // The Stage 6 competition and the 2nd-pass sidecar score with the first-pass
                // model, so the classifier that trained it keys this task too (empty for the SVM).
                + PercolatorEngine.GbdtValidityKeySuffix(ctx.Config);
        }

        /// <summary>
        /// A training export's key: this task's key, the export settings, and the identities of
        /// the run's own files the export reads (<see cref="TrainingExportWriter.RunInputIdentities"/>).
        /// Changing an export setting or rewriting one run's inputs therefore redoes those
        /// exports and never re-scores; every other output keys on the task key alone, so
        /// adding the flag leaves them valid.
        /// </summary>
        public override string OutputValidityKey(PipelineContext ctx, string taskKey, string output)
        {
            string input = TrainingExportInputFor(ctx.Config, output);
            return input == null
                ? taskKey
                : taskKey + TrainingExportKeyTerms(ctx.Config) + TrainingExportWriter.RunInputIdentities(input);
        }

        public override bool Run(PipelineContext ctx)
        {
            // The export added to a finished analysis: nothing else of this task is outstanding,
            // so write the exports from each run's own artifacts and re-score nothing - P16's
            // pay-later path, the counterpart of FirstPassFdrTask.FoldDiagnosticsOnly. Taken
            // before anything below demands FirstPassFDR's buffer, which would load it.
            if (OnlyTrainingExportsOutstanding(ctx))
            {
                // A SecondPassFDR that still has to run loads the library anyway, so the
                // exports take that one load rather than a private second one.
                bool secondPassWillRun = SecondPassFdrWillRun(ctx);
                if (!WriteTrainingExports(ctx, secondPassWillRun))
                    return false;
                // The driver marks this task done after Run, so a SecondPassFDR that still has
                // to run would find RescoredEntries unpublished: publish it the way a skipped
                // task does.
                return !secondPassWillRun || Rehydrate(ctx);
            }
            return RunRescore(ctx) && WriteTrainingExports(ctx, false);
        }

        /// <summary>
        /// Writes each run's training export that is not current, from the run's own artifacts,
        /// stamping each as it lands. A run whose export was written while it re-scored is
        /// current and skipped; every other route - a run resumed as already re-scored, a run
        /// with no re-scoring work, the export-only arm - lands here, so no route leaves an
        /// export missing or stale. A failed run is reported and
        /// the rest still export; the task then fails, which stops the analysis before
        /// SecondPassFDR writes the blib, and a re-run retries only the failed exports.
        /// </summary>
        private bool WriteTrainingExports(PipelineContext ctx, bool demandPipelineLibrary)
        {
            var config = ctx.Config;
            if (!ExportsTraining(config) || config.InputFiles == null)
                return true;
            string key = ValidityKey(ctx);
            IReadOnlyDictionary<uint, LibraryEntry> library = null;
            int nFiles = config.InputFiles.Count;
            for (int i = 0; i < nFiles; i++)
            {
                string input = config.InputFiles[i];
                string output = TrainingExportWriter.PathFor(input);
                string runKey = OutputValidityKey(ctx, key, output);
                if (_trainingExportErrors.ContainsKey(input) || PerFileResumeDriver.IsCurrent(output, Name, runKey))
                    continue;
                library = library ?? TrainingExportWriter.ResolveTargets(ctx, demandPipelineLibrary);
                if (library == null)
                    return false;
                ctx.LogInfo(string.Format(OspreyTasksResources.PerFileRescoreTask_WriteTrainingExports_Training_export__0___1____2_,
                    i + 1, nFiles, Path.GetFileNameWithoutExtension(input)));
                WriteTrainingExport(input, output, runKey, library, ctx, config.NThreads, null, null);
            }
            if (_trainingExportErrors.IsEmpty)
                return true;
            foreach (var failure in _trainingExportErrors.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                ctx.LogError(string.Format(OspreyTasksResources.PerFileRescoreTask_WriteTrainingExports_The_training_export_failed_for__0____1_,
                    failure.Key, failure.Value));
            }
            ctx.ExitCode = 1;
            return false;
        }

        /// <summary>
        /// One run's training export, its stamp embedded as it lands, so a kill loses only the
        /// run in flight; a failure is recorded, not thrown, since this can run inside the
        /// per-file loop.
        ///
        /// <para>A failure whose message is written for the user (a damaged or mismatched
        /// file) is recorded as that message, and anything else as a defect, type and stack
        /// included, so it can be fixed - the line Program.DescribeFailure draws. An
        /// out-of-memory is never one run's failure, even wrapped by the export's window loop:
        /// it propagates, as every other catch of a pipeline artifact lets it (#4615).</para>
        /// </summary>
        private void WriteTrainingExport(string input, string output, string runKey,
            IReadOnlyDictionary<uint, LibraryEntry> library, PipelineContext ctx, int maxThreads,
            SpectraWindowIndex spectra, MzCalibrationResult ms2Cal)
        {
            try
            {
                TrainingExportWriter.ExportRun(input, output, library, ctx, maxThreads,
                    OspreyVersion.Stamp(Name, runKey), spectra, ms2Cal);
            }
            catch (Exception ex) when (!IsOutOfMemory(ex))
            {
                var reported = CommonExceptionUtil.UnwrapUserException(ex);
                _trainingExportErrors[input] = !CommonExceptionUtil.IsProgrammingDefect(reported) && !string.IsNullOrEmpty(reported.Message)
                    ? reported.Message
                    : ex.ToString();
            }
        }

        /// <summary>
        /// Whether the only outputs of this task not current are training exports: the flag
        /// added to an analysis whose re-scoring is done. The counterpart of FirstPassFdrTask's
        /// OnlyDiagnosticsProductOutstanding - every other declared output must be current
        /// against its own key, so a partly re-scored analysis still takes the full path.
        /// </summary>
        private bool OnlyTrainingExportsOutstanding(PipelineContext ctx)
        {
            if (!ExportsTraining(ctx.Config))
                return false;
            string key = ValidityKey(ctx);
            bool anyExportOutstanding = false;
            foreach (string output in Outputs(ctx))
            {
                bool current = PerFileResumeDriver.IsCurrent(output, Name, OutputValidityKey(ctx, key, output));
                if (TrainingExportInputFor(ctx.Config, output) != null)
                    anyExportOutstanding |= !current;
                else if (!current)
                    return false;
            }
            return anyExportOutstanding;
        }

        /// <summary>Whether SecondPassFDR is in this run and will run rather than rehydrate.</summary>
        private static bool SecondPassFdrWillRun(PipelineContext ctx)
        {
            var secondPass = ctx.TaskOf<SecondPassFdrTask>();
            return secondPass != null && ScoringTaskShared.Includes<SecondPassFdrTask>(ctx.Config) &&
                   !ctx.CanRehydrate(secondPass);
        }

        /// <summary>An out-of-memory, alone or among what a parallel loop threw.</summary>
        private static bool IsOutOfMemory(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(e => e is OutOfMemoryException);
        }

        /// <summary>Asked for, and never under a diagnostics-only render, which writes nothing but its report.</summary>
        private static bool ExportsTraining(OspreyConfig config)
        {
            return config.TrainingExport.Enabled && !config.DiagnosticsOnly;
        }

        /// <summary>The input whose training export <paramref name="output"/> is, or null.</summary>
        private static string TrainingExportInputFor(OspreyConfig config, string output)
        {
            // The extension first, so the task's other outputs - three of every four under
            // protein-compact - never pay the scan of every input.
            if (!ExportsTraining(config) || config.InputFiles == null ||
                !output.EndsWith(TrainingExportParquet.EXT, StringComparison.Ordinal))
            {
                return null;
            }
            foreach (string input in config.InputFiles)
            {
                if (string.Equals(TrainingExportWriter.PathFor(input), output, StringComparison.Ordinal))
                    return input;
            }
            return null;
        }

        /// <summary>The export settings a training export keys on.</summary>
        private static string TrainingExportKeyTerms(OspreyConfig config)
        {
            var settings = config.TrainingExport;
            return string.Format(CultureInfo.InvariantCulture, @";trainexport={0};maxq={1:R};claimq={2:R};xics={3}",
                TrainingExportParquet.FORMAT_VERSION, settings.EffectiveMaxQ(config.RunFdr),
                settings.EffectiveClaimantQ, settings.WriteXics ? 1 : 0);
        }

        /// <summary>
        /// Stage 6: re-score each run against the consensus and reconciliation boundaries and
        /// write its reconciled parquet (and, under protein-compact, its per-run second pass).
        /// </summary>
        private bool RunRescore(PipelineContext ctx)
        {
            // Compute path (Stage 6 rescore): re-score each file's entries
            // against the consensus + reconciliation boundaries and write the
            // reconciled parquets. Used by the straight-through pipeline and
            // the stage6 rescore worker. The --task SecondPassFDR node,
            // which has only reconciled parquets + sidecars (no mzMLs to
            // rescore from), takes Rehydrate instead: the driver reaches this
            // task here only in the rescore-capable modes, and a SecondPassFDR
            // consumer materializes it via ctx.Demand, which routes to Rehydrate.
            // CompactedEntries: the post-FirstPassFDR buffer. Demanding it
            // materializes FirstPassFDR (running its compaction + Stage 6 planning
            // when the driver skipped it in worker-rescore mode), which is also
            // what makes the planning byproducts read by ExecuteRescore below
            // available -- one Get expresses the whole dependency.
            _perFileEntries = ctx.Get<CompactedEntries>().Value;

            // This stage's own concurrent-file count (--parallel-files-rescoring, else
            // --parallel-files), not scoring's: re-scoring holds far less per file, so the two
            // do not want the same count.
            ResolveRescoringLanes(ctx, ctx.LogInfo);

            // The loader for the survivor lists FirstPassFDR released after planning
            // (issue #4526); null when this run kept the resident buffer, which leaves
            // nothing to refill and makes the deferred build below a no-op.
            var survivorLoader = PublishedSurvivorLoader(ctx);

            // Publish the RescoredEntries milestone over the shared backing list
            // now, while we hold its reference. ExecuteRescore (below) DRAINS that list file by
            // file, and a consumer brings each run back to its post-rescore state from disk
            // through the per-file source, one at a time (milestone token over a shared store -
            // see PipelineByproducts.cs). WHAT that rebuild overlays is decided below, before
            // Run returns, and parked in _poolPlan; deciding it at pull time would make it
            // depend on state read then (see RescoredPoolPlan). A worker never pulls, so it
            // never pays for it (issue #4597).
            //
            // The per-file source is re-enumerable, which is what makes the bounded join the
            // DEFAULT rather than a property of --task SecondPassFDR. The paragraph that stood
            // here said this leg could not have
            // one because MaterializeRescoredFile is ONE-SHOT - it overlays the reconciled
            // parquet and appends gap-fill rows, so a second call for one run duplicates them,
            // which once exited a straight-through Stellar run 1 on AssertSidecarDescribesPool.
            // True, and true of a NON-EMPTY list only: given an empty one it takes its
            // rebuild-from-the-reconciled-parquet branch, where the gap-fill rows are already
            // merged and no overlay runs, and that branch repeats identically. So the condition
            // is not "make the materializer idempotent" but "hand it a list it can rebuild",
            // which is what BuildRunPerRunSource establishes before offering the source at all.
            var rescored = new RescoredEntries(_perFileEntries,
                BuildRunPerRunSource(ctx, survivorLoader)) { Lanes = ctx.RunPlan.FileLanes };
            ctx.Publish(rescored);

            // Self-gate: rescore + reconciliation only run when there is
            // planning state to act on AND the rescore hasn't already been
            // done upstream. State comes from FirstPassFdrTask's consensus:
            // in-process (DidPlan=true) or from its consensus file on the
            // worker and resume paths (DidPlan=false); either way each run is
            // planned in the per-run hydrate. A 2nd-pass FDR
            // sidecar already on disk for any file is the signal that a
            // previous run COMPLETED Stage 7 here, so the rescore engine
            // has already produced the reconciled output; re-running it
            // would re-apply reconciliation actions on top of
            // already-reconciled values, so this branch falls back to
            // the no-op alongside the no-state case. The signal got
            // stronger when the 2nd-pass sidecars stopped being written
            // conditionally: it used to be absent after a completed run
            // that happened to have no rescore work, which is the same
            // ambiguity that made a missing file unreadable as evidence. Probe-the-disk on
            // 2nd-pass sidecar presence replaces the prior
            // ExpectReconciledInput gate (Phase C: mechanism-driven, not
            // flag-driven) for the worker self-gate cases below;
            // ExpectReconciledInput keeps the hard short-circuit above for
            // the strict --task SecondPassFDR reconciled-input path. Downstream SecondPassFdrTask
            // reads the RescoredEntries milestone of this same backing list.
            // Read the planning gate from the typed byproduct registry rather
            // than reaching for the concrete FirstPassFdrTask. ctx.Get lazily
            // materializes the slot's producer (FirstPassFdrTask) if it has not run
            // yet, so the value is always populated; FirstPassFDR publishes
            // PlanningPerformed alongside CompactedEntries (already read above)
            // from every materialization path - Run with its own _didPlan, Rehydrate with false.
            //
            // The false value is the FIRST of two gates behind defect (b): a straight-through
            // resume of a Stage-5-only directory reads false here, so it self-gated to a
            // refill-only no-op and left SecondPassFDR
            // nothing to fold. Publishing true instead does NOT fix it - the rescore still has
            // no execution path for a self-built bundle - so the fix belongs here, in telling
            // "no rescore needed because the outputs exist" apart from "no rescore possible
            // because nobody supplied a bundle". See
            // TODO-20260901_osprey_firstpassfdr_resume.md.
            bool didPlan = ctx.Get<PlanningPerformed>().Value;
            // EVERY file, not the first one found. This short-circuited on the first current
            // pass-2 sidecar and treated that as proof the whole rescore was done - but a
            // PARTIALLY completed cohort is the normal state of every resume, which is the case
            // the interruption story rests on: a user reclaims the machine mid-run and finishes
            // later without losing work.
            //
            // Measured 2026-09-03 on the 446-file CHS cohort. Killed at 141 of 446 rescored and
            // restarted, the run skipped Stage 5 correctly and then rescored NOTHING: zero
            // "Re-scoring file" lines, 141 reconciled parquets for its whole life, and 305 runs
            // left to carry 1st-pass q-values into the picked-protein FDR and the blib. It then
            // took the refill-only arm and rebuilt the entire 446-run survivor pool, climbing
            // 0.68 GB per percent toward ~86 GB on a 63.7 GB box.
            //
            // That is the same silent no-op the remark below records from a stale sidecar
            // version, reached by ordinary interruption rather than a format change. The gate has
            // to tell "every file is done" from "some file is done", and `any` cannot.
            //
            // Counted rather than short-circuited, and reported, because a partial state has to
            // be VISIBLE rather than inferred from what the run does next. ExecuteRescore already
            // skips per file, so proceeding with a partial set re-scores only what is missing.
            int pass2Present = 0;
            int pass2Expected = 0;
            string pass2Key = ValidityKey(ctx);
            if (ctx.Config.InputFiles != null)
            {
                foreach (var inputFile in ctx.Config.InputFiles)
                {
                    pass2Expected++;
                    // Presence is not currency. A bare File.Exists cannot see a version, so a
                    // sidecar left by a build before the v3 -> v4 record change satisfied this
                    // gate and made the WHOLE Stage 6 rescore a no-op - the run then finished
                    // green carrying 1st-pass q-values into the picked-protein FDR and the .blib.
                    if (IsFileRescored(inputFile, ParquetScoreCache.GetReconciledScoresPath(inputFile), pass2Key))
                        pass2Present++;
                }
            }
            bool allPass2Present = pass2Expected > 0 && pass2Present == pass2Expected;
            if (pass2Present > 0 && !allPass2Present)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_Run_Resuming___0__of__1__runs_already_have_second_pass_intermediate_files__re_scoring_the_,
                    pass2Present, pass2Expected, pass2Expected - pass2Present));
                ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_RESCORE_RESUME, @"adopted={0} rescore={1}",
                    pass2Present, pass2Expected - pass2Present));
            }

            // The second source of "there is a plan to execute", alongside FirstPassFDR's
            // in-process planning (didPlan). When the rescore hydrates per run
            // the plan is not held anywhere at this point - each run is planned inside its
            // iteration, from the consensus file - so the older signal is legitimately
            // false and the gate below would self-gate a run that has every input it needs.
            //
            // That is not hypothetical: removing the experiment-wide bundle made this gate fire
            // and the phase-3 worker wrote no .scores-reconciled.parquet at all, which the gate
            // caught as a missing file rather than as a wrong number. It is the same shape as
            // defect (b) - "no rescore needed because the outputs exist" confused with "no
            // rescore possible because nobody supplied a bundle" - and it is why allPass2Present
            // stays OUTSIDE this term: a completed run must still no-op.
            bool perRunPlanAvailable = ScoringTaskShared.CanHydratePerRun(ctx.Config);
            // "No rescore POSSIBLE because nobody supplied a bundle" and "no rescore NEEDED
            // because the outputs exist" are different answers, and only the second one may
            // no-op. With work OUTSTANDING and no plan to do it, returning success writes a blib
            // that is silently missing those runs - measured 2026-09-03 on Astral, where this
            // arm fired for a cohort with 1 of 3 runs still to re-score. The log even announced
            // "re-scoring the remaining 1" and the task ended in the same second.
            //
            // The allPass2Present half of the disjunct was fixed first (it broke on the first
            // file with a current sidecar); this is the same defect in the other half, which no
            // amount of counting reaches. Fail loudly instead - a resume that cannot finish is
            // exactly the case an operator must be told about, not one to paper over.
            bool noRescorePossible = !perRunPlanAvailable;
            // A plan this process COULD execute is not a plan it WILL, and the gate has to ask
            // the second question. --task ModelDiagnostics re-renders a COMPLETED analysis and
            // writes no artifact but the report (DiagnosticsOnly), so it never re-scores whatever
            // sources are available to it.
            //
            // That distinction is what carried the Astral incident above: --model-diagnostics
            // made perRunPlanAvailable false back then, so "can" and "will" happened to agree and
            // keying on the plan alone was enough. Admitting the task to CanHydratePerRun - the
            // fix for its unbounded hydrate - made perRunPlanAvailable TRUE and silently disarmed
            // this abort for the one task that can never satisfy it. The cohort would then fall
            // through into a real Stage 6 rescore: hours of work, and .scores-reconciled.parquet
            // writes on an analysis this command is documented not to disturb.
            bool willRescoreHere = !ctx.Config.DiagnosticsOnly && (didPlan || !noRescorePossible);
            if (!willRescoreHere && pass2Present < pass2Expected)
            {
                // The --model-diagnostics FLAG is still not a clause here. It used to append
                // "because --model-diagnostics keeps the all-runs hydrate", which was true when
                // the flag excluded the per-run hydrate and is now false on both counts:
                // CanHydratePerRun stopped excluding it, and the Stage 7 join no longer declines
                // under it either. An operator told that would drop the flag, re-run for hours
                // and hit the identical refusal. The TASK is a different matter: it is the whole
                // reason this run will not re-score, so it is named.
                // Not DiagnosticsOnly: FirstPassFDR did not plan in this process and the
                // per-run hydrate is unavailable.
                string reason = ctx.Config.DiagnosticsOnly
                    ? string.Format(OspreyTasksResources.PerFileRescoreTask_Run___task_ModelDiagnostics_only_builds_the_report_for_a_completed_analysis__Finish_the_,
                        OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME))
                    : string.Format(OspreyTasksResources.PerFileRescoreTask_Run_the_cross_run_reconciliation_data_they_need_is_not_available_to_this_run__To_rebuild_it__,
                        FdrScoresSidecar.FIRST_PASS_FILE_PATTERN);
                ctx.LogError(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_Run_Cannot_resume___0__of__1__runs_still_need_re_scoring__but__2_,
                    pass2Expected - pass2Present, pass2Expected, reason));
                ctx.ExitCode = 1;
                return false;
            }

            // DiagnosticsOnly joins this arm whether or not FirstPassFDR planned: a plan it
            // could execute is still not one it will (see willRescoreHere above), and with the
            // incomplete-cohort abort already taken, everything left is a completed second pass
            // to fold the report from. Without this term a diagnostics run whose first-pass
            // ValidityKey had drifted (a changed OSPREY_* setting) planned, fell through to
            // ExecuteRescore, and rewrote every reconciled parquet under a command documented
            // to write nothing but the report.
            if ((!didPlan || ctx.Config.DiagnosticsOnly) && (noRescorePossible || allPass2Present))
            {
                // No rescore to run. The RESIDENT arm does nothing at all here - it leaves the
                // buffer exactly as Stage 5 compacted it, and SecondPassFDR reloads the rescored
                // features from the valid reconciled parquets on disk. So the streamed arm has
                // to do exactly one thing: put back the contents FirstPassFDR released, which is
                // the refill-only plan below. Overlaying the reconciled parquets as well (as
                // this did) applied Stage-6 boundaries the resident arm never applies, so the
                // two arms stopped writing the same answer on this path.
                _poolPlan = RescoredPoolPlan.RefillOnly(_perFileEntries, survivorLoader);
                return true;
            }

            // Per-file validity is handled inside ExecuteRescore's loop:
            // each output carries its own stamp, so a per-file skip keeps
            // the already-rescored files and only the file(s) re-rescored
            // are overwritten.

            // Join file stems for the reconciled parquet metadata hash.
            // In the in-process pipeline _perFileEntries has every file in
            // the run; in worker mode (--task PerFileRescoring) it has
            // a single file. Pass _perFileEntries keys when there's more
            // than one; null means "let ExecuteRescore fall back to the
            // InputFiles-derived hash" (preserves v1 behavior).
            IReadOnlyList<string> joinFileStems = null;
            if (_perFileEntries != null && _perFileEntries.Count > 1)
            {
                var stems = new List<string>(_perFileEntries.Count);
                foreach (var kv in _perFileEntries)
                    stems.Add(kv.Key);
                joinFileStems = stems;
            }

            // Each run is planned here, from its own survivors and the analysis-wide consensus,
            // just before it is rescored (RunReconciliationPlanner), and its plan is written to
            // its reconciliation.json. The whole-experiment planning maps that used to arrive
            // here - order 13 GB at 446 runs - are not built anywhere.
            var rescoreStats = ExecuteRescore(
                _perFileEntries,
                ctx.Get<PerFileCalibrations>().Value,
                ctx.Get<PerFileParquetPaths>().Value,
                ctx.Get<FullLibrary>().Value,
                ctx.Config,
                ctx,
                out var rescoredFiles,
                joinFileStems);
            ctx.LogInfo(string.Format(ScoringTaskShared.IsSingleFileSearch(ctx.Config)
                    ? OspreyTasksResources.PerFileRescoreTask_Run_Multi_charge_consensus_re_scored__0__peaks_
                    : OspreyTasksResources.PerFileRescoreTask_Run_Cross_run_reconciliation_re_scored__0__peaks__including_missing_peaks_,
                rescoreStats.TotalRescored));
            ctx.LogInfo(LogTag.COUNT, LogKey.Format(LogKey.COUNT_RESCORED_PEAKS, @"total={0} actions={1}",
                rescoreStats.TotalRescored, rescoreStats.TotalReconciliation));

            // No rebuild of the whole-run buffer here. The streamed loop emptied every file's
            // list as it went, and each run is brought back by the consumer that reads it,
            // through the per-run source. What is
            // decided HERE rather than there is which reconciled parquets that rebuild may
            // overlay: this is the last moment the question has the right answer (see
            // RescoredPoolPlan), and the answer is cheap - one embedded-stamp read per file.
            // Snapshot under the lock rather than handing the live dictionary on. The
            // parallel loop above has joined, so its contents are final - but passing the
            // mutable instance lets shared state outlive the region that guarded it, and
            // the next caller cannot see that the guarantee is positional.
            Dictionary<string, HashSet<uint>> resetEntryIds;
            lock (_resetEntryIdsLock)
                resetEntryIds = new Dictionary<string, HashSet<uint>>(
                    _resetEntryIdsByFile, StringComparer.Ordinal);
            _poolPlan = new RescoredPoolPlan(_perFileEntries, survivorLoader, rescoredFiles,
                CurrentReconciledPaths(ctx), resetEntryIds);

            // Flush + close the persistent per-process diagnostic
            // dump writers (no-ops when their env vars are unset).
            // Mirrors the worker-mode close calls in RunWorker; without
            // these, the in-process pipeline path can leave the writers
            // unflushed and produce truncated bisection dumps.
            ctx.Diagnostics?.CloseMpInputsDump();
            // ClosePredictRtDump disabled with the rest of the predict-rt
            // diagnostic (perf hotspot); restore alongside WritePredictRtCall.
            // ctx.Diagnostics?.ClosePredictRtDump();
            ctx.Diagnostics?.CloseCwtPathDump();
            return true;
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            // Disk-load path for --task SecondPassFDR: every input parquet already
            // has osprey.reconciled = "true" (asserted by
            // ParquetScoreCache.CheckParquetMetadata when ExpectReconciledInput
            // is set), so Stage 5 first-pass Percolator AND Stage 6 planning /
            // rescore have ALREADY been performed upstream by the worker nodes
            // that wrote those parquets. We must NOT touch FirstPassFdrTask here --
            // demanding it would re-run Stage 5 first-pass Percolator from
            // scratch on the reconciled parquets (producing wildly different
            // action counts than the planner saw on the raw Stage 4 inputs) and
            // then attempt a Stage 6 rescore that needs mzML files the SecondPassFDR
            // node does not have (in production HPC SecondPassFDR ships only
            // sidecars + reconciled parquets, no mzMLs). SecondPassFdrTask is
            // responsible for 2nd-pass Percolator (Bug C) and protein FDR + blib
            // output starting from this hydrated, reconciled state. Mirrors Rust
            // pipeline.rs:3313-3344 which gates the entire Stage 5+6 block on
            // `!config.expect_reconciled_input`.
            //
            // Compaction still needs to run though: PerFileScoringTask's
            // bundle-hydration path loads ALL entries from the parquet,
            // including ones that failed first-pass FDR. FirstPassFdrTask's normal
            // flow would run this compaction inline after first-pass Percolator
            // (and we skip FirstPassFdrTask entirely here). Without it,
            // SecondPassFdrTask's 2nd-pass Percolator would train on ~3x too many
            // entries -- specifically the non-passing first-pass entries whose
            // 1st-pass q-values are 1.0 -- and the SVM would learn a much worse
            // decision boundary than the in-memory pipeline's, producing
            // different per-precursor scores and different protein-FDR results.
            // The compaction reads first-pass q-values already overlaid onto
            // each entry from the .1st-pass.fdr_scores.bin sidecar by
            // PerFileScoringTask's bundle hydration; no fresh FDR computation.
            //
            // Straight-through resume: the driver skipped this task's Run
            // because its reconciled parquets are already valid on disk
            // (CanRehydrate) and a downstream task (SecondPassFDR) is the first to
            // touch its state. A resumed Run self-gates to a no-op here --
            // FirstPassFDR rehydrates (so DidPlan is false) and there is no rescore
            // bundle, so ExecuteRescore never runs and the shared buffer is left
            // at its post-compaction (CompactedEntries) state; SecondPassFDR reloads
            // the rescored features from the valid reconciled parquets on disk.
            // Reproduce exactly that end state by loading the CompactedEntries
            // milestone (which materializes FirstPassFDR's own pure rehydrate) and
            // publishing it as RescoredEntries -- never calling Run, so Rehydrate
            // stays pure. The --task SecondPassFDR path (ExpectReconciledInput)
            // below is a different rehydrate that must NOT materialize FirstPassFDR.
            //
            // Every arm publishes RescoredEntries with this stage's lane count for Stage 7's
            // per-run fold, so it is resolved first. No log line: nothing is re-scored here.
            ResolveRescoringLanes(ctx, null);
            if (!ctx.Config.ExpectReconciledInput)
            {
                _perFileEntries = ctx.Get<CompactedEntries>().Value;

                // The SAME conversion the reconciled-input arm below already had, on the arm
                // that carries every ordinary run. Both arms end with a milestone over the same
                // shared buffer; what the source changes is that Stage 7 rebuilds one run, folds
                // it and drops it, instead of the two whole-run loops beneath this block
                // bringing all 446 to their post-rescore state and holding them - 91.1 GB
                // private, measured on a 446-run resume, which is what admitting only
                // --task SecondPassFDR to the fold left standing.
                //
                // LAZY: with a source in hand there is no reason to do the work before the
                // consumer that folds asks for it.
                //
                // Null only when the retained base_id summary is not current: the driver
                // rehydrates this task only when every reconciled parquet is current, and a
                // parquet this build cannot read in the survivor shape is not
                // (ParquetScoreCache.ReadStamp). That is refused, naming the summary, rather than
                // bringing every run to its post-rescore state at once, which the all-runs loop
                // here once did and Stage 7 then failed on anyway.
                var resumeSource = BuildResumePerRunSource(ctx);
                if (resumeSource == null)
                {
                    ctx.LogError(ScoringTaskShared.UnstreamableJoinError(ctx.Config));
                    ctx.ExitCode = 1;
                    return false;
                }
                ctx.Publish(new RescoredEntries(_perFileEntries, resumeSource) { Lanes = ctx.RunPlan.FileLanes });
                return true;
            }

            // ScoredEntries, NOT CompactedEntries: the reconciled-input path must NOT
            // materialize FirstPassFDR (that would re-run Stage 5 Percolator on the
            // reconciled parquets). Reading the pre-compaction milestone keeps the dependency
            // on PerFileScoring alone, mirroring the old explicit Demand<PerFileScoringTask>.
            // That load published one EMPTY list per run and read no rows; Stage 7's fold fills
            // them one run at a time through the per-run source and drops them again (#4486).
            _perFileEntries = ctx.Get<ScoredEntries>().Value;

            // Never null here: PerFileScoringTask refuses a --task SecondPassFDR directory that
            // cannot be folded run by run before loading anything, which is what retired the
            // all-runs arm that once stood here - the resident pool, the first-pass protein FDR
            // recomputed over it, and its compaction.
            var stage7Source = BuildStage7PerRunSource(
                ctx.Get<PerFileParquetPaths>().Value, ctx.Config, ctx);
            if (stage7Source == null)
                throw new InvalidOperationException(@"Second-pass FDR reached Stage 7 with no per-run source");
            ctx.Publish(new RescoredEntries(_perFileEntries, stage7Source) { Lanes = ctx.RunPlan.FileLanes });
            return true;
        }

        /// <summary>
        /// Resolves this stage's concurrent-file count (<see cref="FileStage.Rescoring"/>) onto
        /// <see cref="RunPlan.RescoringFileParallelism"/>, which the re-score fan-out and Stage 7's
        /// per-run fold (<see cref="RunPlan.FileLanes"/>) read. <paramref name="log"/> is null on
        /// the rehydrate paths, which re-score nothing.
        /// </summary>
        private static void ResolveRescoringLanes(PipelineContext ctx, Action<string> log)
        {
            var config = ctx.Config;
            // Auto, after scoring ran on lanes in this process: keep scoring's count. A fresh probe
            // here sees the library and FirstPassFDR's products resident, and budgets with
            // scoring's per-file estimate - many times what a re-scored file holds - so it would
            // give re-scoring FEWER lanes than scoring had. Until re-scoring has a footprint
            // estimate of its own, the count that fit scoring is the better bound.
            if (config.GetFileParallelism(FileStage.Rescoring, out _).Mode == FileParallelismMode.Auto &&
                ctx.RunPlan.ScoringFileParallelism > 1)
            {
                ctx.RunPlan.RescoringFileParallelism = ctx.RunPlan.ScoringFileParallelism;
                return;
            }
            ctx.RunPlan.RescoringFileParallelism = PerFileScoringTask.ResolveFileParallelism(
                config, FileStage.Rescoring, config.InputFiles?.Count ?? 0, log);
        }

        // RunWorker + its helpers (AddIfNotNull, LoadOriginalRtCalibration)
        // were removed in Phase C. The stage6 worker mode
        // (--task PerFileRescoring) now routes through
        // AnalysisPipeline.Run with StartAt = StopAfter =
        // PerFileRescoreTask. Upstream state previously assembled in
        // RunWorker (library load, hydration, compaction, consensus,
        // calibration) is produced by PerFileScoringTask's joinOnly
        // load and the per-run hydrate. Run() above is the only entry point.

        /// <summary>
        /// Execute the per-file Stage 6 rescore loop. Mirrors
        /// <c>rescore_per_file_loop</c> in
        /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
        ///
        /// For each file with at least one re-scoring target:
        /// <list type="number">
        ///   <item>Build boundary_overrides keyed by entry_id.</item>
        ///   <item>Subset the library to the entries that need re-scoring.</item>
        ///   <item>Stream MS2 by isolation window from the .spectra.bin cache (required).</item>
        ///   <item>Reload MS2/MS1 mass calibration from the sibling .calibration.json.</item>
        ///   <item>Pick the refined RT calibration when present, else fall back to
        ///       the original first-pass calibration.</item>
        ///   <item>Call <see cref="ScoringPipeline"/>.RunCoelutionScoring with the override-aware
        ///       <see cref="ScoringContext"/>.</item>
        ///   <item>Overlay the re-scored entries back onto the per-file
        ///       FdrEntry stubs by entry_id, preserving ParquetIndex.</item>
        /// </list>
        ///
        /// The mutable <paramref name="perFileEntries"/> is updated in place
        /// (Score, Pep, q-values, Features, ApexRt/StartRt/EndRt, etc.).
        /// Returns <see cref="RescoreStats"/> with the per-stage counts.
        /// </summary>
        internal RescoreStats ExecuteRescore(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx,
            out HashSet<string> rescoredFiles,
            IReadOnlyList<string> joinFileStems = null)
        {

            // file_name -> input_files index, used to pick the right mzML
            // path for spectra cache load + sibling .calibration.json.
            var fileNameToIdx = BuildFileNameToIndex(config.InputFiles);

            // Cross-file inputs every per-file rescore reads, bundled so
            // RescoreOneFile takes one collaborator object instead of a dozen
            // positional parameters.
            var inputs = new RescorePassInputs
            {
                PerFileCalibrations = perFileCalibrations,
                ParquetPaths = perFileParquetPaths,
                FullLibrary = fullLibrary,
                // The run's shared entry_id index. The reconciled-parquet writer
                // used to build its own ~6.3M-entry copy of this map per FILE,
                // under the parallel loop - loop-invariant work at O(files x library).
                LibraryById = ctx.Get<LibraryById>().Value,
                Config = config,
                FileNameToIdx = fileNameToIdx,
                TaskValidityKey = ValidityKey(ctx),
                JoinFileStems = joinFileStems,
            };
            inputs.Pass2Worker = CreatePass2Worker(config, ctx, Name, inputs.TaskValidityKey);
            var runPlanner = CreateRunPlanner(perFileCalibrations, perFileParquetPaths, fullLibrary, config, ctx);
            inputs.HydrateRun = BuildPerRunHydrate(perFileParquetPaths, config, ctx);
            var planStamp = OspreyVersion.Stamp(Name, inputs.TaskValidityKey);
            var sequencePool = ctx.Get<SequencePool>().Value;
            inputs.PlanRun = run => RescoreHydration.ApplyPlan(run,
                PlanAndWriteRun(runPlanner, run.FileName, run.InputPath, run.Survivors, planStamp),
                sequencePool);

            // Clean PERSISTENT floor entering reconciliation (post-GC, before the
            // rescore loop repopulates the heavy per-entry arrays) -- fires early,
            // so it lands even if a long run is later killed. #4376.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"reconciliation start (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"reconciliation-floor",
                string.Format(@"(post-GC, entering rescore, files={0})", perFileEntries.Count));

            int nTotalFiles = perFileEntries.Count;
            // The Stage 6 rescore is the "second per-file fan-out": each file's rescore
            // is independent (its own entry list + its own .scores-reconciled.parquet),
            // and it reuses the same RunCoelutionScoring the Stage 1-4 fan-out already
            // runs concurrently. So run files in parallel under this stage's own count
            // (RunPlan.FileLanes, resolved at the top of RunRescore). Output is byte-identical
            // to the sequential loop because the per-file work shares no mutable state - which
            // regression.ps1 cannot see, as it never passes --parallel-files; SubsetPipelineTest
            // compares the two. Per-file results land by index so the accumulation is order-free.
            int parallelism = ctx.RunPlan.FileLanes;
            var counts = new (int Rescored, int GapCwt, int GapForced, bool Scored)[nTotalFiles];
            // Per-file survivor-refill failures, collected rather than thrown from inside the
            // parallel body so the actionable message survives (see RescoreOneFileStreamed).
            var loadErrors = new string[nTotalFiles];

            if (nTotalFiles == 1 || parallelism == 1)
            {
                for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
                    counts[fileNum] = RescoreOneFileStreamed(
                        fileNum, nTotalFiles, perFileEntries[fileNum],
                        inputs, ctx, out loadErrors[fileNum]);
            }
            else
            {
                // Legend mapping each aggregate-line slot to its file, then the
                // concurrent rescore collapsed onto the throttled "[i] p%" line +
                // per-file buffered blocks (same MultiProgressReporter as scoring).
                ctx.LogInfo(string.Format(OspreyTasksResources.PerFileRescoreTask_ExecuteRescore_Re_scoring__0__files_in_parallel_, nTotalFiles));
                for (int i = 0; i < nTotalFiles; i++)
                {
                    string key = perFileEntries[i].Key;
                    string label =
                        inputs.FileNameToIdx.TryGetValue(key, out int inputIdx)
                        && inputIdx < inputs.Config.InputFiles.Count
                            ? inputs.Config.InputFiles[inputIdx]
                            : key;
                    ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(@"{0}. {1}", i + 1, label));
                }
                // One file at a time from a shared queue, in input order, so a lane that finishes
                // takes the next file rather than working through a block dealt to it up front.
                var multi = new MultiProgressReporter();
                OrderedFileLanes.For(nTotalFiles, parallelism, fileNum =>
                {
                    using (multi.BeginFile(fileNum, RESCORE_FILE_SEGMENTS))
                    {
                        counts[fileNum] = RescoreOneFileStreamed(
                            fileNum, nTotalFiles, perFileEntries[fileNum],
                            inputs, ctx, out loadErrors[fileNum]);
                    }
                });
            }

            // Fail the rescore on any per-file refill failure, HERE rather than inside the
            // loop above: the message names the file and the missing artifact, which is the
            // whole value of the diagnostic, and net472 loses it through AggregateException.
            foreach (string loadError in loadErrors)
            {
                if (loadError != null)
                    throw new InvalidDataException(loadError);
            }

            // Reconciliation memory probe (#4376). The pre-GC snapshot's peak
            // working set reflects the transient in-loop peak (parallelism x the
            // per-file ~1.5 GB spectra + parquet reload); the forced-GC line is the
            // clean PERSISTENT managed heap (all files' rescored FdrEntry buffer +
            // library). Zero-cost when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"reconciliation end (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"reconciliation-resident",
                string.Format(@"(files={0}, file_parallelism={1})", nTotalFiles, parallelism));

            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;
            // The files that actually reached the scoring engine, so the streamed rebuild can
            // reapply the score / q-value reset to exactly those - see ResetRescoredTargets.
            rescoredFiles = new HashSet<string>();
            for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
            {
                var c = counts[fileNum];
                totalRescored += c.Rescored;
                totalGapCwt += c.GapCwt;
                totalGapForced += c.GapForced;
                if (c.Scored)
                    rescoredFiles.Add(perFileEntries[fileNum].Key);
            }

            // Report what the per-file second-pass worker did, once the parallel loop has joined:
            // the counts are run-wide, so they are emitted once rather than per file (#4486).
            // Nothing is published for Stage 7, which checks each answer by the validity stamp
            // on disk, because a byproduct cannot cross the process boundary an HPC chain puts
            // between Stage 6 and Stage 7.
            inputs.Pass2Worker.LogSummary(ctx);
            inputs.Pass2Worker.Dispose();
            runPlanner.LogSummary();

            return new RescoreStats
            {
                TotalRescored = totalRescored,
                TotalReconciliation = runPlanner.ActionCount,
                TotalGapCwt = totalGapCwt,
                TotalGapForced = totalGapForced,
            };
        }

        /// <summary>
        /// Build the per-file second-pass worker for this run.
        ///
        /// <para>Every pass-2 mode has one (#4665), and there is no Stage 7 fallback for a run
        /// without it: SecondPassFDR folds the per-run sidecars this worker writes and computes
        /// none of its own. So a 1st-pass model sidecar that cannot be read, or a model with no
        /// usable model/standardizer, stops the run HERE, where the remedy is a re-run of the
        /// first pass, rather than at the top of Stage 7 after every run has been re-scored.
        /// Both used to leave the run to Stage 7, which answered it from the whole pool.</para>
        ///
        /// <para>The three whole-run constants it needs come from the analysis's
        /// <c>.1st-pass.model.json</c> and <c>.1st-pass.stratum.json</c>, which
        /// <c>FirstPassFdrTask</c> already writes - the frozen model, the experiment-agg arm and
        /// the protein stratum. The fourth input is the analysis-wide 1st-pass experiment
        /// sidecar (issue #4486).</para>
        /// </summary>
        private static Pass2PerFileWorker CreatePass2Worker(OspreyConfig config,
            PipelineContext ctx, string taskName, string taskValidityKey)
        {
            var sidecar = FirstPassModelIO.LoadForAnalysis(config);
            var scorer = sidecar?.Model != null ? FrozenModelScorer.TryCreate(sidecar.Model) : null;
            if (scorer == null)
            {
                throw new InvalidOperationException(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2FrozenCompetition_Second_pass_FDR_cannot_run__the_saved_first_pass_model__a_first_pass_intermediate_file__,
                    OspreyArgNames.Text(OspreyArgNames.TASK)));
            }
            var inputByName = new Dictionary<string, string>(StringComparer.Ordinal);
            if (config.InputFiles != null)
            {
                foreach (string inputFile in config.InputFiles)
                    inputByName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;
            }
            void WriteAnswer(string fileName, IReadOnlyList<FdrScoreRecord> records,
                IReadOnlyDictionary<uint, (double score, uint entryId)> bestDecoy)
            {
                // --task ModelDiagnostics touches no artifact but the report, exactly as the
                // Stage 7 writer this replaces.
                if (config.DiagnosticsOnly)
                    return;
                if (!inputByName.TryGetValue(fileName, out string inputFile))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass per-file competition wrote records for '{0}', which matches " +
                        @"no configured input file, so its sidecar has nowhere to go. See issue #4486.",
                        fileName));
                }
                // Every artifact embeds the PRODUCER's stamp: they belong to PerFileRescoring, so
                // whatever invalidates PerFileRescoring invalidates them. Stamping SecondPassFDR's
                // key here would leave a file that outlives the inputs it was computed from. Each
                // commits atomically with its stamp, and a file is this run's answer only when
                // every artifact carries the stamp of the reconciled parquet beside it
                // (Pass2FdrSidecar.HasWorkerStamp), so an interruption between the two writes
                // leaves a file that is re-scored on resume rather than one Stage 7 folds against
                // a missing or stale decoy artifact. The decoys are written only where there was
                // a competition to produce them - the transfer passes none.
                var stamp = OspreyVersion.Stamp(taskName, taskValidityKey);
                if (bestDecoy != null)
                    Pass2CompetitionDecoys.Write(Pass2CompetitionDecoys.PathFor(inputFile), bestDecoy, stamp);
                FdrScoresSidecar.Write(FdrScoresSidecar.Pass2Path(inputFile), records,
                    FdrScoresSidecar.Pass.SecondPass, stamp);
            }
            bool transfer = !OspreyEnvironment.Pass2ProteinCompact;
            // The competition is constrained to the stratum, and competing without one would be
            // the full-population form that was removed (#4581), not a smaller version of this.
            // An EMPTY stratum is not a missing one: no protein had 2 or more detected peptides.
            // The competition then admits only the peaks reconciliation changed, and a run that
            // reports nothing ends in SecondPassFDR's error saying so.
            if (!transfer && sidecar.StratumBaseIds == null)
            {
                throw new InvalidOperationException(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_CreatePass2Worker_The_saved_first_pass_model_carries_no_list_of_precursor_candidates_from_proteins_with_2_or_more_detections__,
                    OspreyArgNames.TaskText(FirstPassFdrTask.TASK_NAME)));
            }
            // The transfer carries each precursor's pass-1 experiment values, and the loader reads
            // an absent sidecar as an empty map - every run would be written at q = 1.0.
            if (transfer)
                Pass2FdrSidecar.RequireTransferExperimentSidecar(config);
            return new Pass2PerFileWorker(
                scorer,
                transfer,
                transfer ? null : sidecar.StratumBaseIds,
                Pass2FdrSidecar.LoadPass1ExperimentRecords(config),
                WriteAnswer,
                ctx.LogWarning);
        }

        /// <summary>
        /// Cross-file inputs shared by every <see cref="RescoreOneFile"/> call:
        /// the planner's per-file byproducts, the library/config, and the
        /// resume/identity keys. Bundled so the per-file worker takes one
        /// collaborator object rather than a dozen positional parameters.
        /// </summary>
        private sealed class RescorePassInputs
        {
            public IReadOnlyDictionary<string, RTCalibration> PerFileCalibrations;
            public IReadOnlyDictionary<string, string> ParquetPaths;
            public List<LibraryEntry> FullLibrary;
            public IReadOnlyDictionary<uint, LibraryEntry> LibraryById;
            public OspreyConfig Config;
            public Dictionary<string, int> FileNameToIdx;
            public string TaskValidityKey;
            public IReadOnlyList<string> JoinFileStems;

            /// <summary>
            /// The per-file half of the second pass, which every pass-2 mode runs HERE rather
            /// than in Stage 7 (issues #4486, #4665).
            ///
            /// <para>Built ONCE for the pass and shared across the parallel file loop. It is
            /// safe to share: the stratum and the experiment records are read-only, the transfer
            /// tallies are summed under a lock, and the rest of its mutable state - the seeders
            /// and the frozen scorer's standardization buffer - is per thread. The scorer was
            /// once called read-only while its Score wrote a shared buffer (#4706).
            /// See <see cref="Pass2PerFileWorker"/>.</para>
            /// </summary>
            public Pass2PerFileWorker Pass2Worker;

            /// <summary>
            /// Hydrate ONE run's Stage-6 inputs from that run's own artifacts - the fan-out shape
            /// this task is supposed to have: each iteration loads its run's survivors, actions,
            /// gap-fill and refined calibration, uses them, and drops them.
            ///
            /// <para>The file-keyed dictionaries this replaced were the join shape, and they are
            /// what made a per-run task scale with run count: building them for 86 runs cost
            /// 8m42s and 17.2 GB before the first run was rescored.</para>
            ///
            /// <para>Called under <c>_survivorLoadLock</c>, so it need not be thread-safe.</para>
            /// </summary>
            public Func<string, RunRescoreInputs> HydrateRun;

            /// <summary>
            /// Plan ONE hydrated run from its survivors and the analysis-wide consensus, write its
            /// <c>.reconciliation.json</c>, and apply the plan to it. Called from the file lanes
            /// OUTSIDE <c>_survivorLoadLock</c> - planning holds only the run's survivors and its
            /// CWT candidates, and serializing it would put the per-run planning this task took
            /// over from the join back in a single file. Thread-safe.
            /// </summary>
            public Action<RunRescoreInputs> PlanRun;
        }

        /// <summary>
        /// Bracket one file's <see cref="RescoreOneFile"/> with its per-run hydrate and
        /// release (issue #4526).
        ///
        /// <para>The refill targets the file's EXISTING list object rather than replacing
        /// it, because that list is the shared backing store every
        /// <see cref="PerFileEntries"/> milestone wraps - swapping the reference would
        /// leave the published milestones pointing at the old one. Contents are transient;
        /// identity is not.</para>
        ///
        /// <para>Safe under the parallel file loop: each call touches only its own file's
        /// list, and the loader's only per-file state is its survivor-count check, which
        /// locks.</para>
        ///
        /// <para><c>loadError</c> is set when this file's survivors could not be refilled, so
        /// the caller can fail the whole rescore with an actionable message AFTER the parallel
        /// loop. Throwing from inside <c>Parallel.For</c> wraps the exception in an
        /// AggregateException whose net472 message does not include the inner text, and nothing
        /// in Osprey unwraps it, so the reason was lost.</para>
        /// </summary>
        private (int Rescored, int GapCwt, int GapForced, bool Scored) RescoreOneFileStreamed(
            int fileNum, int nTotalFiles, KeyValuePair<string, List<FdrEntry>> file,
            RescorePassInputs inputs, PipelineContext ctx, out string loadError)
        {
            loadError = null;

            // Fan-out shape first: this run's inputs come from this run's own artifacts,
            // hydrated here and dropped in the finally. Serialized for the same reason the
            // refill below is - one run's full pre-compaction pool is transiently resident, and
            // multiplying that by the file parallelism would add several GB of peak to a change
            // whose whole purpose is removing peak.
            // DECIDE BEFORE LOADING. RescoreOneFile's first act is this same check, but by then
            // the hydrate below has already run: this run's .scores.parquet read, its ~106 MB
            // 1st-pass sidecar overlaid, and the result compacted - all of it discarded a
            // microsecond later by "skipping (outputs valid)". Measured 2026-09-03 on the
            // 446-file CHS cohort: ~4.7 s per already-complete file, 11m14s to walk 141 of them,
            // and serialized under _survivorLoadLock so it cannot even overlap.
            //
            // The check itself is a stamp read (PerFileResumeDriver.IsCurrent). Asking it first
            // is what makes "these 400 runs are already done" cost seconds instead of half an
            // hour - which is the whole promise of resuming an interrupted cohort.
            //
            // Left in RescoreOneFile as well: that method has other callers, and the second call
            // on this path is a stamp read against a file this one just certified.
            if (TryResumeRescoredFile(fileNum, nTotalFiles, file.Key, file.Value, inputs, ctx))
                return (0, 0, 0, false);

            RunRescoreInputs run;
            // Serialized: the hydrate transiently holds the file's FULL pre-compaction stub set
            // plus the sidecar byte array before filtering down to survivors, which at 163
            // files is ~700 MB for one file. Under the parallel rescore that transient would
            // otherwise be multiplied by the file parallelism - several GB of new peak in a
            // change whose entire purpose is lowering peak. The rescore itself takes 2-2.5
            // minutes per file against a few seconds for this load, so admitting one loader at
            // a time costs a rounding error of wall clock and keeps the transient at 1x.
            lock (_survivorLoadLock)
            {
                try
                {
                    run = inputs.HydrateRun(file.Key);
                }
                catch (InvalidDataException ex)
                {
                    // Report rather than throw, so the parallel loop reports every bad run
                    // instead of surfacing one wrapped in an AggregateException whose net472
                    // message drops the inner text.
                    loadError = ex.Message;
                    return (0, 0, 0, false);
                }
                file.Value.Clear();
                file.Value.AddRange(run.Survivors);
            }
            try
            {
                inputs.PlanRun(run);
            }
            catch (InvalidDataException ex)
            {
                // Reported, not thrown, for the reason the hydrate's failure is: the loop reports
                // every bad run. The run is not rescored, so its entries are dropped here.
                loadError = ex.Message;
                file.Value.Clear();
                return (0, 0, 0, false);
            }
            try
            {
                return RescoreOneFile(fileNum, nTotalFiles, file.Key, file.Value, inputs, ctx, run);
            }
            finally
            {
                // Drop this file's entries before the next one loads its own - but ONLY when
                // its reconciled parquet is actually on disk, because that file is what Stage 7
                // rebuilds them from. RescoreOneFile makes the same call about the heavy payload
                // for the same reason (see the wroteReconciled gate it applies to
                // ReleaseRescoredPayload): when the write no-opped or failed, these entries are
                // the ONLY copy of the rescore, and dropping them would discard it on a warning.
                if (ReconciledParquetOnDisk(file.Key, inputs))
                {
                    file.Value.Clear();
                    file.Value.TrimExcess();
                }
            }
        }

        /// <summary>
        /// Run the Stage 6 rescore for a single file: resume-skip check, target
        /// assembly, subset scoring + overlay, gap-fill, and the reconciled
        /// parquet write-back. The per-file <paramref name="fdrEntries"/> buffer
        /// is updated in place. Returns the per-file (rescored, gap-CWT,
        /// gap-forced) counts the caller accumulates. The scoring orchestration
        /// is kept whole here (parity-locked); only the cross-file plumbing was
        /// lifted up into <see cref="ExecuteRescore"/>.
        /// </summary>
        private (int Rescored, int GapCwt, int GapForced, bool Scored) RescoreOneFile(
            int fileNum, int nTotalFiles, string fileName, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx, RunRescoreInputs run)
        {
            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;

            // Per-file resume: a file whose reconciled parquet is already on
            // disk with a matching sidecar is overlaid in place and skipped.
            if (TryResumeRescoredFile(fileNum, nTotalFiles, fileName, fdrEntries, inputs, ctx))
                return (totalRescored, totalGapCwt, totalGapForced, false);

            // Assemble this file's rescore targets (multi-charge consensus +
            // reconciliation dedup + gap-fill) and resolve its input mzML.
            // Bails when there is no work or the file has no input_files entry.
            if (!TryAssembleRescoreTargets(fileNum, nTotalFiles, fileName, inputs, ctx, run,
                    out var combinedTargets, out var gapFillTargets, out string inputFile))
            {
                // Still write this file's reconciled parquet. Leaving it out is what made the
                // Stage 4 parquet a required Stage 7 input (issue #4486). And still answer its
                // second pass (#4665): a run with no Stage 6 work is a run whose peaks did not
                // move, which is an answer, and SecondPassFDR computes no per-run answer of its
                // own for this one to fall back on.
                string unchangedInput = WriteUnchangedReconciled(fileName, fdrEntries, inputs, ctx);
                if (unchangedInput != null)
                    WritePass2Answer(fileName, unchangedInput, fdrEntries, inputs);
                return (totalRescored, totalGapCwt, totalGapForced, false);
            }

            var config = inputs.Config;
            var fullLibrary = inputs.FullLibrary;

            // Clone the outer config for this file's ScoringContexts.
            // RunCoelutionScoring reassigns config.FragmentTolerance to
            // the MS2-calibrated tolerance (AnalysisPipeline.cs ~line 3552);
            // without a per-file clone the mutation persists on the outer
            // config, leaks into subsequent files, AND poisons the
            // WriteReconciledParquet hash stamp (config.Identity.SearchParameterHash()
            // would then reflect the calibrated tolerance, not the value
            // a fresh --task SecondPassFDR invocation recomputes from CLI
            // defaults -- causing search_hash mismatch errors). Mirrors
            // the per-file clone pattern in ProcessFile.
            var fileConfig = config.ShallowClone();

            // Divide the inner main-search thread budget across concurrently rescored
            // files so total demand stays near core count (mirrors ProcessFile under
            // --parallel-files). The subset rescore is light, but this still avoids
            // thread oversubscription when several files re-score at once.
            if (ctx.RunPlan.RescoringFileParallelism > 1)
                fileConfig.NThreads = Math.Max(1, config.NThreads / ctx.RunPlan.RescoringFileParallelism);

            // Build the per-file scoring subset: boundary_overrides keyed
            // by entry_id + the subset library RunCoelutionScoring scores.
            var (boundaryOverrides, subsetLibrary) =
                BuildScoringSubset(combinedTargets, fdrEntries, fullLibrary);

            // Segment 1/3 (read): index this file's spectra cache -- the file's first
            // progress slice on the --parallel-files "[i] p%" aggregate line.
            MultiProgressReporter.Current?.BeginSegment();
            // Stream this file's MS2 by isolation window from the .spectra.bin cache the
            // original Stage 1-4 run wrote, instead of materializing the whole ~6 GB
            // resident List<Spectrum>: build the seekable SpectraWindowIndex (MS1 + the
            // isolation windows come from it too) and load each window on
            // demand during the re-score. Stage 6 REQUIRES that cache; there is no mzML
            // fallback -- a rescore without the cache is a deployment error, not a reason
            // to re-read the 6 GB mzML (LoadSpectraForRescore throws).
            SpectraWindowIndex spectraIndex = ScoringTaskShared.LoadSpectraForRescore(inputFile, fileName,
                OspreyTasksResources.PerFileScoringTask_LoadJoinOnlyScores_Re_scoring, true, ctx);
            ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                OspreyTasksResources.PerFileRescoreTask_LoadSpectraForRescore___Streaming__1__MS1_and__0__MS_MS_spectra_from_cache_for__2_,
                spectraIndex.Ms2Count, spectraIndex.Ms1Spectra.Count, fileName));
            var ms1Spectra = spectraIndex.Ms1Spectra.ToList();

            // Load-boundary memory probe. With streaming this measures the small index +
            // MS1 on the reconciliation floor (library + this file's parquet stubs) -- the
            // ~6 GB resident MS2 the resident load used to root here is gone (that reduction
            // is the point of this change). Kept as permanent instrumentation to catch a
            // future regression that re-materializes the MS2 at this boundary; the forced-GC
            // line is the live number and, under Profile-Osprey.ps1 -MemoryProfile, captures
            // a "perfile-rescore-loaded" retention snapshot. Zero cost -- collection
            // included -- when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"perfile-rescore-loaded (pre-GC)");
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"perfile-rescore-loaded",
                @"(post-GC, streaming index resident)");

            // Load the sibling .calibration.json so the search uses the
            // same MS2/MS1 mass calibrations the original Stage 1-4 run
            // used. The file is written by the original ProcessFile call
            // and read here -- same disk-roundtrip path the worker uses.
            ScoringTaskShared.LoadMassCalibrations(inputFile, OspreyTasksResources.PerFileScoringTask_LoadJoinOnlyScores_Re_scoring,
                out MzCalibrationResult ms2Cal,
                out MzCalibrationResult ms1Cal,
                out double? rtMadFromCalJson);

            // Pick the RT calibration: refined (from this run's Stage 6 plan, made in its
            // hydrate) wins; original first-pass falls back, which is this
            // run's own .calibration.json.
            RTCalibration rtCal = run.RefinedCalibration;
            if (rtCal == null)
                inputs.PerFileCalibrations.TryGetValue(fileName, out rtCal);

            // Bisection seam DISABLED (paired with the per-candidate
            // WritePredictRtCall, which was removed from the scoring
            // hotspot). Dumped the cal's library_rts + fitted_values once
            // per file. Mirrors Rust's dump_predict_rt_arrays at
            // pipeline.rs ~2886. To restore, re-enable this and the
            // WritePredictRtCall in CoelutionScorer. See
            // ai/todos/active/TODO-20260606_ospreysharp_diagnostics_di.md.
            // if (rtCal != null)
            // {
            //     ctx.Diagnostics.WritePredictRtArrays(
            //         fileName, rtCal.LibraryRts, rtCal.FittedValues);
            // }

            // Build the scoring context with the boundary overrides.
            // RunCoelutionScoring inspects context.BoundaryOverrides
            // inside ScoreCandidate and routes through the override
            // peak-construction path.
            var context = new ScoringContext(fileConfig, fileName);
            context.BoundaryOverrides = boundaryOverrides;
            context.OriginalRtMad = rtMadFromCalJson;

            // Isolation windows come straight from the streaming index, the same list
            // Stages 1-4 scored, so the window fan-out is unchanged.
            var isolationWindows = spectraIndex.IsolationWindows.ToList();

            // Stream each isolation window's calibrated MS2 from the index on demand. The
            // providers hold no per-window state (each GetCalibratedWindow is a fresh decode +
            // in-place calibration), so re-scoring the file three times just re-reads windows -- no
            // resident ~6 GB list, and none of the per-pass whole-list calibrated COPIES the
            // resident provider built. (That repeated-scoring is exactly why the resident path
            // had to pass consumeInputMzs:false; streaming has no such constraint.)
            //
            // The first pass to touch the file reads it cold: on a large cohort the cache left the
            // file cache hours ago, while every run was scored. It reads one window block at a
            // time (SpectraWindowIndex.LoadWindowSerialRead), which on a spinning disk took a cold
            // SEA-AD run's windows from ~85 MB/s to the disk's sequential rate. The passes after it
            // find the windows warm, where parallel LoadWindow is faster.
            IWindowSpectraProvider coldProvider =
                new StreamingWindowSpectraProvider(spectraIndex, ms2Cal, serialBlockReads: OspreyEnvironment.SerialWindowReads);
            IWindowSpectraProvider spectraProvider = subsetLibrary.Count > 0
                ? new StreamingWindowSpectraProvider(spectraIndex, ms2Cal)
                : coldProvider;

            // Segment 2/3 (score): the subset re-score; its "Re-scoring isolation
            // windows" reporter feeds this slice (the bulk of the file's motion).
            MultiProgressReporter.Current?.BeginSegment();
            // Re-score the subset.
            var swRescore = Stopwatch.StartNew();
            List<FdrEntry> rescored;
            if (subsetLibrary.Count > 0)
            {
                rescored = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    subsetLibrary, coldProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    context, passLabel: OspreyTasksResources.PerFileRescoreTask_Rescore_Re_scoring_isolation_windows);
            }
            else
            {
                rescored = new List<FdrEntry>();
            }
            swRescore.Stop();

            // Overlay the re-scored subset back onto the per-file stubs,
            // resetting discriminant fields to Rust to_fdr_entry defaults.
            HashSet<uint> resetEntryIds;
            lock (_resetEntryIdsLock)
            {
                if (!_resetEntryIdsByFile.TryGetValue(fileName, out resetEntryIds))
                {
                    resetEntryIds = new HashSet<uint>(combinedTargets.Count);
                    _resetEntryIdsByFile[fileName] = resetEntryIds;
                }
            }
            var (nOverlay, nNoPeak) =
                OverlayRescoredEntries(fdrEntries, combinedTargets, rescored, resetEntryIds);
            totalRescored += nOverlay;
            if (nNoPeak > 0)
            {
                ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                    OspreyTasksResources.PerFileRescoreTask_private____0__targets_had_no_signal_within_their_new_peak_boundaries__their_scores_were_reset_,
                    nNoPeak));
            }

            ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                OspreyTasksResources.PerFileRescoreTask_private___Re_scored__0__of__1__peaks___2_s_,
                nOverlay, combinedTargets.Count, swRescore.Elapsed.TotalSeconds));

            // PHASE 2 -- gap-fill two-pass.
            if (gapFillTargets.Count > 0)
            {
                var (nGapCwt, nGapForced) = RunGapFillTwoPass(
                    gapFillTargets, fullLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal, ms2Cal, ms1Cal,
                    fileConfig, fileName, rtMadFromCalJson, fdrEntries, ctx);
                totalGapCwt += nGapCwt;
                totalGapForced += nGapForced;
                totalRescored += nGapCwt + nGapForced;
            }

            // Segment 3/3 (write): the reconciled parquet write-back.
            MultiProgressReporter.Current?.BeginSegment();
            // PHASE 3 -- reconciled parquet write-back + sidecar stamp.
            bool wroteReconciled = WriteReconciledAndStamp(fileName, inputFile, fdrEntries, inputs, ctx);

            // PHASE 3b -- the per-file half of the SECOND pass (#4486).
            //
            // Here, and not in Stage 7, because this is the only moment all three of its inputs
            // are simultaneously true: this file's survivors are in hand, its reconciled parquet
            // is on disk (the frozen recompute reads it), and the heavy per-entry payload has not
            // been dropped yet. Stage 7 could only reach the same state by holding every file's
            // survivors resident and re-opening the 1st-pass sidecars - 52.3 GB of them at 257
            // files - which is the whole of what this move removes.
            //
            // Gated on wroteReconciled for the same reason ReleaseRescoredPayload below is: when
            // the write did not persist, the reconciled parquet this reads either does not exist
            // or was deleted after a failed write, and competing against a stale Stage 4 parquet
            // would produce a run q for peaks that no longer exist. Skipping leaves the file with
            // no current answer, so the task does not complete and a resume re-scores it.
            if (wroteReconciled)
                WritePass2Answer(fileName, inputFile, fdrEntries, inputs);

            // Per-file rescore high-water mark: the raw (pre-GC) working_set peak and
            // managed_heap since the last collection -- the during-scoring transient (the
            // per-window streamed+calibrated spectra + the scored/gap-fill entries). With
            // streaming there is no resident MS2 nor the per-pass whole-list calibrated
            // copies here, so this is the reduced "after" peak vs the resident baseline.
            // A forced-GC [MEM] is deliberately NOT taken (it would just show the
            // post-release floor). Zero cost when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx, @"perfile-rescore-peak (pre-GC)");

            // Apex retention snapshot (dotMemory only). Once the resident MS2 is streamed,
            // the remaining per-file accumulation is the scored + reconciled entries still
            // rooted by fdrEntries here (each carries the heavy Features / CwtCandidates /
            // Fragment* / ReferenceXic* arrays). dotMemory forces its own GC before the
            // snapshot, so the write-back's parquet-reload transient collapses and the
            // dominators are exactly that retained entry set. Taken BEFORE the release
            // below so the snapshot still shows the pre-release apex it was written to
            // show. No-op unless a Profile-Osprey.ps1 -MemoryProfile session is attached.
            ProfilerHooks.CaptureRetentionSnapshot(@"perfile-rescore-apex");

            // Drop this file's heavy per-entry payload now that its reconciled parquet is
            // on disk. Without this the fat arrays stay rooted through every remaining
            // file, which is the O(files) Stage-6 growth term of #4472: the survivor stubs
            // themselves are lean (10 scalar columns off the parquet), but rescoring
            // replaces them with entries carrying Features / CwtCandidates / Fragment* /
            // ReferenceXic* at roughly 1-3 KB each, and gap fill appends more.
            //
            // MUST run AFTER WriteReconciledAndStamp above: ReconciledParquetWriter uses
            // "Features != null" as the "this row was rescored" sentinel when it builds the
            // overlay, so releasing before the write would silently emit a parquet with no
            // overlaid rows.
            //
            // And ONLY when that write actually persisted. WriteReconciledAndStamp no-ops
            // when the file has no ParquetPaths entry or its original parquet is missing, and
            // DELETES the reconciled parquet after a failed write - in all three cases these
            // arrays are still the only copy of the rescore, so dropping them would discard
            // it silently. Mirrors the Stage-4 release, which is likewise gated on its write
            // having happened (PerFileScoringTask's parquetFooterMetadata != null branch).
            if (wroteReconciled)
                ReleaseRescoredPayload(fdrEntries);

            // The training export, while this run's spectra index and calibration are in hand,
            // and after the payload above is released: the export reads its rows back from the
            // reconciled parquet, so nothing it uses is released, and holding the rescore's
            // arrays through it only raised the per-file peak. Every run this does not reach -
            // resumed, with no re-scoring work, or failed here - WriteTrainingExports writes
            // from disk; the file is the same either way.
            if (wroteReconciled && ExportsTraining(inputs.Config))
            {
                string exportPath = TrainingExportWriter.PathFor(inputFile);
                WriteTrainingExport(inputFile, exportPath, OutputValidityKey(ctx, inputs.TaskValidityKey, exportPath),
                    inputs.LibraryById, ctx, fileConfig.NThreads, spectraIndex, ms2Cal);
            }

            // Deterministically drop this file's transients before the next file loads its
            // own: the streaming index + MS1 and the write-back's full-parquet reload. At
            // 100s of files .NET's Server GC otherwise defers collection until it nears the
            // RAM ceiling -- so the reconciliation working set rides up with file count
            // instead of staying flat. Forcing the collection here mirrors Rust's
            // per-iteration spectra drop (pipeline.rs:3338, in Rust's strictly sequential
            // reconciliation file loop) and keeps the working set at ~the persistent floor +
            // one file's transient. Output is unchanged (GC timing only). Skipped under
            // file-parallelism > 1, where concurrent files legitimately share residency and
            // a blocking GC would stall the other in-flight rescores.
            spectraProvider = null;
            coldProvider = null;
            spectraIndex = null;
            ms1Spectra = null;
            isolationWindows = null;
            rescored = null;
            if (ctx.RunPlan.RescoringFileParallelism <= 1)
                GC.Collect();

            // Post-release floor: the persistent set that survives after this file's
            // transients (streaming index, MS1, scored entries) are dropped. Forced-GC so
            // it is the true floor, and it captures a "perfile-rescore-live" retention
            // snapshot to pair with the loaded snapshot. Zero cost -- collection included --
            // when OSPREY_LOG_MEMORY is unset.
            ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx, @"perfile-rescore-live",
                @"(post-GC, after release)");

            return (totalRescored, totalGapCwt, totalGapForced, true);
        }

        /// <summary>
        /// Write one file's second-pass answer - its <c>.2nd-pass.fdr_scores.bin</c>, and under
        /// protein-compact the decoy side of its competition - from its survivors and the
        /// reconciled parquet just written for them.
        ///
        /// <para>Called for EVERY run (#4665): the one Stage 6 re-scored and the one it had no
        /// work for. There is no run whose answer SecondPassFDR computes instead.</para>
        /// </summary>
        private static void WritePass2Answer(string fileName, string inputFile,
            List<FdrEntry> fdrEntries, RescorePassInputs inputs)
        {
            string parquetPath = inputs.ParquetPaths != null &&
                                 inputs.ParquetPaths.TryGetValue(fileName, out string p)
                ? p
                : null;
            if (parquetPath == null)
            {
                // Absence is a stop. A file whose parquet is unmapped here would otherwise
                // silently get no 2nd-pass sidecar, and the join reads a missing file as a
                // file that contributed nothing - lowering nothing, reporting nothing, and
                // leaving its precursors to be represented by other runs alone.
                // Exception text for internal diagnostics, not displayed to user
                throw new InvalidOperationException(string.Format(
                    @"Second-pass per-file competition for '{0}': no scores parquet is mapped " +
                    @"for this file, so its reconciled parquet cannot be located. See issue #4486.",
                    fileName));
            }
            inputs.Pass2Worker.ComputeStampAndWrite(
                fileName,
                FdrScoresSidecar.Pass1Path(inputFile),
                // The reconciled parquet the refusal above is worded for. Derived rather than
                // probed: this task wrote it moments ago, so its absence is a failure to
                // report, not a reason to read Stage 4's.
                ParquetScoreCache.ReconciledPathFromScoresPath(parquetPath),
                fdrEntries);
        }

        /// <summary>
        /// Release the heavy per-entry payload a rescore attaches, once the file's
        /// reconciled parquet has been written. These six arrays have no reader after
        /// Stage 6: <see cref="ReconciledParquetWriter"/> consumes them per file during
        /// the write above, the Stage-6 planner loads CWT candidates through its own
        /// per-file loader rather than off these entries, and the resident 2nd pass
        /// reloads features from the reconciled parquet
        /// (<c>Pass2FdrSidecar.LoadReconciledFeaturesByScoreIndex</c>) rather than reading
        /// them here. Keeping them rooted is what made Stage-6 memory O(files) - the
        /// entries themselves are lean stubs until a rescore fattens them.
        ///
        /// Only the payload is dropped; the entry objects stay in the caller's list
        /// because <see cref="RescoredEntries"/> is published over that shared backing
        /// list and SecondPassFDR reads it for the run-wide reductions.
        ///
        /// Called on all three paths that end with a file's reconciled parquet on disk: the
        /// fresh rescore (gated on the write having persisted), the per-file resume skip
        /// (<see cref="TryResumeRescoredFile"/>), and the whole-task
        /// <see cref="Rehydrate"/> - the last two re-fatten the same six arrays out of that
        /// parquet when they reload it, so without this they hold
        /// the full payload for every file at once. All three therefore leave the identical
        /// buffer shape.
        /// </summary>
        private static void ReleaseRescoredPayload(List<FdrEntry> fdrEntries)
        {
            if (fdrEntries == null)
                return;
            foreach (var entry in fdrEntries)
            {
                entry.Features = null;
                entry.CwtCandidates = null;
                entry.FragmentMzs = null;
                entry.FragmentIntensities = null;
                entry.ReferenceXicRts = null;
                entry.ReferenceXicIntensities = null;
            }
        }

        /// <summary>
        /// Whether <paramref name="inputFile"/> is done for this task: its reconciled parquet at
        /// <paramref name="reconciledPath"/> is current for <paramref name="validityKey"/> and the
        /// worker's 2nd-pass answer beside it carries the same stamp.
        ///
        /// <para>ONE definition, called by both the cohort-level count in <see cref="Run"/> and
        /// the per-file skip in <see cref="TryResumeRescoredFile"/>. They used to ask different
        /// questions - the count asked whether the sidecar was readable, the skip asked whether
        /// the reconciled parquet was stamped - and a crash between the two writes made them
        /// disagree about the same file in the same run. Two notions of "done" is the defect;
        /// this is the fix.</para>
        ///
        /// <para>A file with no rescore work needs the answer too (#4665). It used to get none -
        /// Stage 7 answered for it - and was exempted here on the parquet's
        /// <c>osprey.rescored</c> flag, because requiring an answer it would never get counted it
        /// as outstanding forever. The worker now answers every run, so the exemption has no
        /// subject, and a kill between the parquet and the answer reads as not done for every
        /// run alike.</para>
        /// </summary>
        private bool IsFileRescored(string inputFile, string reconciledPath, string validityKey)
        {
            return PerFileResumeDriver.IsCurrent(reconciledPath, Name, validityKey) &&
                   Pass2FdrSidecar.HasWorkerStamp(inputFile);
        }

        /// <summary>
        /// Per-file resume probe. When the file's reconciled parquet (and, where this task
        /// writes them, its 2nd-pass artifacts) carry a current PerFileRescoring stamp, CLEARS
        /// this file's in-memory entries and returns true so the caller skips re-scoring - the
        /// same thing the rescore arm does once its own reconciled parquet is written, and for
        /// the same reason: that parquet is what the deferred pool build restores them
        /// from, so Stage 7 loads its own.
        ///
        /// <para>It used to OVERLAY the reconciled values back onto the entries instead, to
        /// keep a downstream SecondPassFDR from reading 1st-pass RTs off them. That left this
        /// arm as the one place still building the all-runs buffer the rescore arm had stopped
        /// building, and cost ~5.8 s per skipped file - 13m43s to walk 141 already-complete
        /// runs at 446 before the first real rescore.</para>
        /// Pairs with the worker (stage6) crash-resume contract: re-invoking
        /// the same CLI on the same inputs is a no-op for files whose rescore
        /// completed. Otherwise returns false, and the rescore overwrites any stale artifact.
        /// </summary>
        private bool TryResumeRescoredFile(
            int fileNum, int nTotalFiles, string fileName,
            List<FdrEntry> fdrEntries, RescorePassInputs inputs, PipelineContext ctx)
        {
            // The rescore READS the original Stage 4 parquet and WRITES a
            // separate <stem>.scores-reconciled.parquet. Resume validity is
            // keyed on the reconciled output (the task's declared Output),
            // not on the original read source.
            bool hasParquetPath = inputs.ParquetPaths.TryGetValue(fileName, out string perFileParquetPath);
            string reconciledPath = hasParquetPath
                ? ParquetScoreCache.ReconciledPathFromScoresPath(perFileParquetPath)
                : null;
            // A file is done when BOTH of its products are, not just the parquet. A crash
            // between the reconciled-parquet write and the 2nd-pass sidecar write leaves a file
            // whose parquet stamp is valid and whose sidecar does not exist, and this arm used
            // to read only the stamp - so the same run would COUNT the file as outstanding
            // ("445 of 446 run(s) already carry a current 2nd-pass sidecar; re-scoring the
            // remaining 1") and then skip it as complete four lines later, rescoring nothing.
            // Observed for real: a native AccessViolation killed a 446-file run mid-stamp at
            // 02:00 on 2026-09-04, and the resume silently dropped that run's 2nd-pass data.
            //
            // Mode 8 cannot present this state - it amputates BOTH products, so the two notions
            // agree. Only a crash between the two writes splits them.
            bool done = hasParquetPath &&
                        inputs.FileNameToIdx.TryGetValue(fileName, out int inputIdx) &&
                        inputs.Config.InputFiles != null &&
                        inputIdx < inputs.Config.InputFiles.Count &&
                        IsFileRescored(inputs.Config.InputFiles[inputIdx], reconciledPath, inputs.TaskValidityKey);
            if (done)
            {
                ctx.LogInfo(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_TryResumeRescoredFile_File__0___1____2__was_already_re_scored__keeping_it_,
                    fileNum + 1, nTotalFiles, fileName));

                // CLEAR, do not overlay. This arm used to rebuild the file's in-memory entries
                // from the reconciled parquet so a downstream SecondPassFDR would not read
                // 1st-pass RTs off them. That made the skip arm the one place still building
                // the all-runs buffer the rescore arm had stopped building - item 6 of the
                // target-shape violation table, "a per-run quantity accumulated into an
                // all-runs container for no reason except that the task was written as a
                // join". PerFileRescoring is not a join and must build nothing spanning runs.
                //
                // The rescore arm above already does exactly this, for exactly this reason:
                // "Drop this file's entries before the next one loads its own - but ONLY when
                // its reconciled parquet is actually on disk, because that file is what the
                // deferred pool build restores them from." That premise is not merely true
                // here, it is this arm's ENTRY CONDITION - IsCurrent just certified the
                // reconciled parquet. So Stage 7 loads its own, from the same file the overlay
                // was reading, at the moment it actually needs it.
                //
                // MaterializeRescoredFile keys on exactly this: an empty list plus a current
                // reconciled path takes its loadedReconciled branch and reads the survivors
                // with Stage 6's boundaries already applied - which is what the overlay was
                // reproducing by hand, one file at a time, inside the fan-out task.
                //
                // Measured cost of the overlay it replaces: ~5.8 s per skipped file - 13m43s to
                // walk 141 already-complete runs on the 446-file cohort, before the first real
                // rescore. Not decode; ~648 K FdrEntry allocations plus a canonical re-sort,
                // done to restore four doubles into a buffer that should not exist.
                fdrEntries.Clear();
                fdrEntries.TrimExcess();
                return true;
            }
            // Nothing is cleared here: a stale reconciled parquet is overwritten, stamp and all,
            // where it is rewritten (WriteReconciledAndStamp), and a file with no Stage 6 work
            // keeps a current parquet as it is (WriteUnchangedReconciled).
            return false;
        }

        /// <summary>
        /// Assemble the per-file rescore target set: merge multi-charge
        /// consensus with reconciliation actions (reconciliation wins on
        /// conflict -- the inter-replicate peak boundary is more authoritative
        /// than the multi-charge consensus boundary), collect this file's
        /// gap-fill targets, and resolve its input mzML path. Logs the
        /// re-scoring banner + entry breakdown when there is work. Returns
        /// false -- caller skips the file -- when there is no work to do or the
        /// file has no input_files entry.
        /// </summary>
        private bool TryAssembleRescoreTargets(
            int fileNum, int nTotalFiles, string fileName,
            RescorePassInputs inputs, PipelineContext ctx, RunRescoreInputs run,
            out Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
            out List<GapFillTarget> gapFillTargets,
            out string inputFile)
        {
            combinedTargets = new Dictionary<int, (double Apex, double Start, double End)>();
            inputFile = null;

            // The per-run hydrate supplies all three from THIS run's own artifacts. Consensus is
            // derived here rather than carried, because MultiChargeConsensus reads one run's
            // entries and nothing else - which is what made building it for every run up front
            // pure waste.
            var consensusTargets = MultiChargeConsensus.SelectRescoreTargets(
                run.Survivors, inputs.Config.RunFdr);
            var reconTargets = ReconTargetsForRun(run);
            gapFillTargets = run.GapFill ?? new List<GapFillTarget>();

            // Merge consensus + reconciliation into a per-(idx, override) map.
            foreach (var t in consensusTargets)
                combinedTargets[t.Index] = (t.Apex, t.Start, t.End);
            foreach (var t in reconTargets)
                combinedTargets[t.Index] = (t.Apex, t.Start, t.End);

            // Skip files with no work to do.
            if (combinedTargets.Count == 0 && gapFillTargets.Count == 0)
                return false;

            if (!inputs.FileNameToIdx.TryGetValue(fileName, out int inputIdx))
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.PerFileRescoreTask_TryAssembleRescoreTargets_Cross_run_reconciliation___0__is_not_among_the_inputs_of_this_run__skipping_it_, fileName));
                return false;
            }
            inputFile = inputs.Config.InputFiles[inputIdx];

            ctx.LogInfo(string.Format(
                OspreyTasksResources.PerFileRescoreTask_TryAssembleRescoreTargets_Re_scoring_file__0___1____2_, fileNum + 1, nTotalFiles, fileName));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_RESCORE_FILE, @"{0}/{1}", fileNum + 1, nTotalFiles));
            ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                OspreyTasksResources.PerFileRescoreTask_TryAssembleRescoreTargets____0__peaks_to_re_score_at_new_boundaries___1__missing_peaks,
                combinedTargets.Count,
                gapFillTargets.Count));
            return true;
        }

        /// <summary>
        /// PHASE 3 -- reconciled parquet write-back. Reads the original Stage 4
        /// parquet and writes a separate <c>.scores-reconciled.parquet</c>
        /// sibling (leaving the original intact) with this task's stamp in its
        /// footer, in the same commit, so a failed write can never mark stale
        /// reconciled content valid (which would let Stage 7 / a future resume
        /// consume old rescored content). On failure removes whatever parquet
        /// is at the reconciled path so the next run re-rescores this file.
        ///
        /// Returns whether the reconciled parquet was actually PERSISTED. False when
        /// this file has no <c>ParquetPaths</c> entry or its original parquet is gone
        /// (both no-ops) and when the write failed (the output was just deleted). The
        /// caller must not drop the heavy per-entry payload on a false return: those
        /// arrays are then still the only copy of the rescore, and
        /// <see cref="ReconciledParquetWriter"/> reads them off the entries on the retry.
        /// </summary>
        private bool WriteReconciledAndStamp(
            string fileName, string inputFile, List<FdrEntry> fdrEntries,
            RescorePassInputs inputs, PipelineContext ctx)
        {
            var config = inputs.Config;
            // ParquetPaths is non-null here (dereferenced at the resume probe).
            if (!inputs.ParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                !File.Exists(parquetPath))
            {
                return false;
            }

            string reconciledOutPath = ParquetScoreCache.ReconciledPathFromScoresPath(parquetPath);
            // The parquet embeds this task's stamp in its footer, in the same commit as its rows.
            bool wrote = ReconciledParquetWriter.Write(parquetPath, reconciledOutPath, fdrEntries, fileName,
                inputs.LibraryById, config, inputs.JoinFileStems,
                OspreyVersion.Stamp(Name, inputs.TaskValidityKey), ctx.LogInfo, ctx.LogWarning);
            if (wrote)
                return true;

            // Remove whatever an earlier run left at the reconciled path (output mechanics, the
            // task's own concern) so the next run re-rescores from scratch.
            try
            {
                if (File.Exists(reconciledOutPath))
                    File.Delete(reconciledOutPath);
            }
            catch (Exception ex)
            {
                ctx.LogWarning(TextUtil.GetIndentation(1) + string.Format(
                    OspreyTasksResources.PerFileRescoreTask_WriteReconciledAndStamp___Failed_to_remove_the_incomplete_re_scored_results_file__0__after_a_failed_write___1_,
                    reconciledOutPath, ex.Message));
            }
            return false;
        }

        /// <summary>
        /// Write the reconciled parquet for a file that had no rescore work, so that EVERY
        /// input has one on disk.
        ///
        /// <para>The output is a faithful copy: <c>ReconciledParquetWriter.BuildOverlay</c>
        /// selects gap-fill by a null <c>ParquetIndex</c> and re-scored rows by a
        /// non-null <c>Features</c>, and a no-work file has neither, so the stream replaces
        /// nothing and appends nothing. Only the reconciliation metadata is added.</para>
        ///
        /// <para>Written because the alternative is worse than the duplication. Skipping it
        /// made the Stage 4 parquet a REQUIRED Stage 7 input for whichever files happened to
        /// have no work, which is how Stage 7 came to read both parquets - one to get the
        /// rows and one to get the reconciled values back (issue #4486). Every file having
        /// this artifact is what lets Stage 7 read a single source per file.</para>
        ///
        /// <para>Silent when the file has no <c>input_files</c> entry: that is the other
        /// reason <see cref="TryAssembleRescoreTargets"/> bails, it already warned, and
        /// there is no input path to stamp the resume record against.</para>
        ///
        /// <para>Returns the run's input file when its reconciled parquet is current on disk -
        /// written here or already there - so the caller can answer its second pass from it, and
        /// null when there is no such parquet to answer from.</para>
        /// </summary>
        private string WriteUnchangedReconciled(
            string fileName, List<FdrEntry> fdrEntries, RescorePassInputs inputs, PipelineContext ctx)
        {
            if (inputs.FileNameToIdx == null ||
                !inputs.FileNameToIdx.TryGetValue(fileName, out int inputIdx))
            {
                return null;
            }
            string inputFile = inputs.Config.InputFiles[inputIdx];
            // A current copy is left as it is. This file's resume arm can still decline it -
            // its 2nd-pass sidecar is outstanding after an interruption between the two writes -
            // and rewriting an identical copy moves only its mtime, which is part of the identity
            // a training export of this run is keyed on, so the export was rewritten whenever the
            // re-run landed in a later second (#4729).
            if (inputs.ParquetPaths.TryGetValue(fileName, out string parquetPath) &&
                PerFileResumeDriver.IsCurrent(ParquetScoreCache.ReconciledPathFromScoresPath(parquetPath),
                    Name, inputs.TaskValidityKey))
            {
                return inputFile;
            }
            return WriteReconciledAndStamp(fileName, inputFile, fdrEntries, inputs, ctx)
                ? inputFile
                : null;
        }

        /// <summary>
        /// Pre-group reconciliation actions by file. Mirrors the Rust
        /// pre-grouping at pipeline.rs:2719-2744 -- a single pass over
        /// the action map produces (file -> [(idx, apex, start, end)])
        /// so the per-file loop just looks up its slice. Returns the
        /// per-file map; <paramref name="totalReconciliation"/> receives
        /// the count of non-Keep actions grouped.
        /// </summary>
        private static Dictionary<string, List<(int Index, double Apex, double Start, double End)>>
            GroupReconciliationActionsByFile(
                IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
                out int totalReconciliation)
        {
            var perFileReconTargets =
                new Dictionary<string, List<(int Index, double Apex, double Start, double End)>>();
            totalReconciliation = 0;
            foreach (var kvp in reconciliationActions)
            {
                var fileName = kvp.Key.FileName;
                var idx = kvp.Key.Index;
                double apex, start, end;
                if (kvp.Value is ReconcileAction.UseCwtPeak useCwt)
                {
                    apex = useCwt.ApexRt;
                    start = useCwt.StartRt;
                    end = useCwt.EndRt;
                }
                else if (kvp.Value is ReconcileAction.ForcedIntegration forced)
                {
                    apex = forced.ExpectedRt;
                    start = forced.ExpectedRt - forced.HalfWidth;
                    end = forced.ExpectedRt + forced.HalfWidth;
                }
                else
                {
                    // Keep: planner omits these from the map by design,
                    // but stay defensive -- skip rather than crash.
                    continue;
                }
                if (!perFileReconTargets.TryGetValue(fileName, out var list))
                {
                    list = new List<(int, double, double, double)>();
                    perFileReconTargets[fileName] = list;
                }
                list.Add((idx, apex, start, end));
                totalReconciliation++;
            }
            return perFileReconTargets;
        }

        /// <summary>
        /// The per-run hydrate this pass uses: each run is hydrated from its own files, beside
        /// the analysis-wide summaries FirstPassFDR left behind, exactly as a single-run HPC
        /// node does - on every route, the straight-through pipeline included. The join-wide
        /// dictionaries the planner held were the alternative, and no route read them.
        ///
        /// <para>Throws when the run has no per-run route: <see cref="InvalidDataException"/>
        /// naming the retained base_id summary when it cannot be read, which the rescore
        /// reports as an error, and <see cref="InvalidOperationException"/> when the selection
        /// declines the per-run route, which no rescoring selection does.</para>
        /// </summary>
        private static Func<string, RunRescoreInputs> BuildPerRunHydrate(
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            // The SAME predicate the --input-scores load reads, so the two sites cannot
            // disagree about whether the per-file lists are empty survivors-to-be.
            if (!ScoringTaskShared.CanHydratePerRun(config) &&
                ScoringTaskShared.PerRunSurvivorLoaderAvailable(config))
            {
                throw new InvalidOperationException(
                    @"The Stage 6 rescore was reached by a selection that does not hydrate per run.");
            }
            var retainedBaseIds = ScoringTaskShared.ReadRetainedBaseIds(config, out string retainedError);
            if (retainedBaseIds == null)
                throw new InvalidDataException(retainedError);
            var experimentRecords = FdrExperimentSidecar.ReadMap(
                FdrExperimentSidecar.PathFor(config.OutputBlib,
                    ScoringTaskShared.ArtifactSiblingPath(config), FdrScoresSidecar.Pass.FirstPass),
                FdrScoresSidecar.Pass.FirstPass);
            // Say which shape this pass took. Without it the only evidence is a memory profile,
            // and "the gate is green so the new path must have run" is precisely the inference
            // that let an earlier resume fix report success while testing the old path (defect
            // (b2), TODO-20260901_osprey_firstpassfdr_resume). A test can assert this line.
            // Count the runs from the published parquet paths. It used to read InputScores,
            // which was null on a straight-through run, and it threw a NullReferenceException
            // on the first one after the predicate above stopped requiring that list. The
            // paths are the right source regardless: they are what the loop iterates.
            ctx.LogVerbose(CountText.Format(perFileParquetPaths.Count,
                OspreyTasksResources.PerFileRescoreTask_BuildPerRunHydrate_Re_scoring_loads_the_file_from_its_own_intermediate_files__the_list_of__1__kept_target_,
                OspreyTasksResources.PerFileRescoreTask_BuildPerRunHydrate_Re_scoring_loads_one_file_at_a_time___0__files__from_its_own_intermediate_files__the_list_,
                retainedBaseIds.Count));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_RESCORE_HYDRATE, @"per-run runs={0}",
                perFileParquetPaths.Count));
            var sequencePool = ctx.Get<SequencePool>().Value;
            return fileName =>
            {
                if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Per-run rescore hydrate: no scores parquet path published for {0}",
                        fileName));
                }
                // RAW pre-compaction stubs, which is what HydrateOneRun contracts for: it
                // overlays the 1st-pass sidecar over the full row set and compacts afterwards.
                // NOT FirstPassSurvivorLoader - that loader has already overlaid and filtered,
                // so handing its output here would overlay a second time onto a list that is
                // no longer the sidecar's superset. Same read the join-shaped load performs
                // per file (LoadJoinOnlyScoresForFile), one run at a time.
                return RescoreHydration.HydrateOneRun(parquetPath, retainedBaseIds,
                    experimentRecords,
                    (name, path) => ParquetScoreCache.LoadFdrStubsFromParquet(path, null, sequencePool));
            };
        }

        /// <summary>
        /// The per-run planner: the analysis-wide consensus FirstPassFDR wrote, plus this task's
        /// calibrations, isolation windows and library - the baseline of a fan-out task, bounded
        /// by the library and the distinct peptides. The consensus is read when the first run
        /// is planned, so a resume that finds every run done reads nothing. A consensus that
        /// cannot be read fails that run with <see cref="InvalidDataException"/> naming the
        /// file: planning without it would write plans with no reconciliation in them.
        /// </summary>
        private static RunReconciliationPlanner CreateRunPlanner(
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            PipelineContext ctx)
        {
            Func<Stage6Consensus> loadConsensus = config.Reconciliation.Enabled
                ? () => Stage6ConsensusFile.Load(config)
                : () => null;
            return new RunReconciliationPlanner(loadConsensus, perFileCalibrations,
                ctx.Get<PerFileIsolationMz>().Value, fullLibrary, perFileParquetPaths, config, ctx);
        }

        /// <summary>
        /// Plan one run and write its plan to its <c>.reconciliation.json</c>, stamped with the
        /// stamp its reconciled parquet will carry, so the run's outputs are current together
        /// (<see cref="IsFileRescored"/>). Written before the rescore that executes it, so a
        /// kill mid-rescore leaves a plan whose parquet is missing - a run that is not done.
        /// A write failure is reported as <see cref="InvalidDataException"/>, which the loop
        /// collects per run.
        /// </summary>
        private static ReconciliationFile PlanAndWriteRun(RunReconciliationPlanner runPlanner,
            string fileName, string syntheticInput, List<FdrEntry> survivors, ArtifactStamp stamp)
        {
            // Never reached under --task ModelDiagnostics, which writes nothing but its report:
            // that task does not rescore (see willRescoreHere in RunRescore).
            var plan = runPlanner.PlanRun(fileName, survivors);
            try
            {
                ReconciliationFile.Save(ReconciliationFile.PathForInput(syntheticInput), plan, stamp);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteReconciliationFile_Could_not_write_the_cross_run_reconciliation_file_for__0____1_,
                    fileName, ex.Message), ex);
            }
            return plan;
        }

        /// <summary>
        /// The per-run source Stage 7 folds through on the STRAIGHT-THROUGH resume: bring ONE
        /// run's list to its post-rescore state (<see cref="MaterializeResumedFile"/>), so the
        /// join holds one run at a time. Nothing in it reads another run's entries.
        ///
        /// <para>RE-ENUMERABLE, which is the property <see cref="RescoredEntries.StreamFiles"/>
        /// needs: a fold pass drops each run's list, so a second pass finds it empty, and the
        /// half here rebuilds an empty list from disk identically. Unconditionally so, unlike
        /// <see cref="BuildRunPerRunSource"/>: a resume runs no rescore, so no run's list ever
        /// holds state that is not already on disk, and there is nothing a drop can lose.</para>
        ///
        /// <para>Null when the run cannot stream. Two ways: the admission itself
        /// (<see cref="ScoringTaskShared.CanStreamStage7Join(OspreyConfig)"/>, which is where
        /// the reconciled parquets are required to be readable - asked in FULL here, unlike
        /// <see cref="BuildRunPerRunSource"/>, because a resume enters after Stage 6 has
        /// written them), or no published survivor loader - and the second is not a preference.
        /// A run whose survivors were never released has nothing on disk to rebuild them from,
        /// so folding would DROP the only copy on its way past. That is the same condition
        /// <see cref="Run"/> states for its own deferred build; the difference is only that
        /// this arm asks for the loader without the Stage-6 switch (see
        /// <see cref="PublishedSurvivorLoader"/>). <see cref="Rehydrate"/> refuses either way;
        /// there is no all-runs fallback.</para>
        /// </summary>
        private Action<string, List<FdrEntry>> BuildResumePerRunSource(PipelineContext ctx)
        {
            if (!ScoringTaskShared.CanStreamStage7Join(ctx.Config))
                return null;
            var loader = PublishedSurvivorLoader(ctx);
            if (loader == null)
                return null;
            // Taken ONCE, here, as RescoredPoolPlan describes: the
            // reconciled-parquet judgement is made in Run and travels with the plan (a driver
            // re-stamp that once forced this no longer exists). The answer travels; the
            // question does not.
            var reconciledPaths = CurrentReconciledPaths(ctx);
            // Says which shape Stage 7 took, for the reason its --task SecondPassFDR sibling
            // gives: without it the only evidence is a memory profile, and "the gate is green so
            // the new path must have run" is the inference that lets a resident path pass as a
            // streamed one.
            ctx.LogVerbose(CountText.Format(_perFileEntries.Count,
                OspreyTasksResources.PerFileRescoreTask_BuildResumePerRunSource_Second_pass_FDR_reads_the_re_scored_results_of_the_file___1__of_1_current__,
                OspreyTasksResources.PerFileRescoreTask_BuildResumePerRunSource_Second_pass_FDR_reads_the_re_scored_results_one_file_at_a_time___1__of__0__files_current__,
                reconciledPaths.Count));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run runs={0}", _perFileEntries.Count));
            return (fileName, survivors) =>
                MaterializeResumedFile(fileName, survivors, loader, reconciledPaths, ctx);
        }

        private static Action<string, List<FdrEntry>> BuildStage7PerRunSource(
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            if (!ScoringTaskShared.CanStreamStage7Join(config))
                return null;
            // THROWS rather than returning null. Returning null here would publish a
            // source-less milestone over the empty per-run lists the loader has already
            // created, and Stage 7 would fold 446 empty runs and write an empty .blib with
            // exit 0. The two sites decide on different evidence - the loader header-probes,
            // this reads the body, and they are minutes apart on a large cohort - so this is
            // reachable without anything being wrong with the caller.
            var retainedBaseIds = ScoringTaskShared.ReadRetainedBaseIdsOrFail(config);
            // Says which shape Stage 7 took, for the reason its rescore sibling gives: without
            // it the only evidence is a memory profile, and "the gate is green so the new path
            // must have run" is the inference that lets a resident path pass as a streamed one.
            ctx.LogVerbose(CountText.Format(perFileParquetPaths.Count,
                OspreyTasksResources.PerFileRescoreTask_BuildStage7PerRunSource_Second_pass_FDR_reads_the_file_from_its_own_intermediate_files__the_list_of__1__kept_pairs_is_read_once_,
                OspreyTasksResources.PerFileRescoreTask_BuildStage7PerRunSource_Second_pass_FDR_reads_one_file_at_a_time___0__files__from_its_own_intermediate_files__the_list_is_read_once_,
                retainedBaseIds.Count));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run runs={0}", perFileParquetPaths.Count));
            var sequencePool = ctx.Get<SequencePool>().Value;
            return (fileName, survivors) =>
            {
                if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass join hydrate: no scores parquet path published for {0}",
                        fileName));
                }
                // The published path IS the reconciled parquet on this leg: it is reached only
                // under --task SecondPassFDR, and ScoringTaskShared.ScoresPathsForInputs
                // resolves that task's paths to the reconciled artifact by task membership
                // rather than by probing disk. Asserted rather than re-resolved, because
                // rebuilding from a Stage 4 path would give this arm the PRE-reconciliation
                // rows (first-pass boundaries, no Stage-6 gap-fill) while the resident arm on
                // the same command line reads the reconciled file. Two different .blib files
                // from one command line is precisely what the byte-identity oracle exists to
                // prevent, and it would not have caught it: both arms would be self-consistent.
                if (!ParquetScoreCache.IsReconciledScoresPath(parquetPath))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass join hydrate: run '{0}' published '{1}', which is not a " +
                        @"{2} file. The join rebuilds every run from its reconciled artifact; a " +
                        @"{3} output here would silently produce first-pass boundaries with no " +
                        @"gap-fill rows.",
                        fileName, parquetPath, ParquetScoreCache.EXT_SCORES_RECONCILED, PerFileScoringTask.TASK_NAME));
                }
                RescoreHydration.RefillOneRunSurvivors(fileName, parquetPath, survivors,
                    retainedBaseIds,
                    (name, path) => ParquetScoreCache.LoadFdrStubsFromParquet(path, null, sequencePool));
            };
        }

        /// <summary>
        /// One run's reconciliation targets, projected through the same
        /// <see cref="GroupReconciliationActionsByFile"/> the join path uses so the two cannot
        /// interpret an action differently. <see cref="RunRescoreInputs.ReconciliationActions"/>
        /// is deliberately keyed <c>(file_name, vec_idx)</c> with exactly one file in it, which
        /// is what lets that helper be reused unchanged rather than reimplemented per run - the
        /// kind of duplication that drifts silently when a new <c>ReconcileAction</c> shape is
        /// added to one copy.
        /// </summary>
        private static List<(int Index, double Apex, double Start, double End)> ReconTargetsForRun(
            RunRescoreInputs run)
        {
            var byFile = GroupReconciliationActionsByFile(run.ReconciliationActions, out _);
            return byFile.TryGetValue(run.FileName, out var targets)
                ? targets
                : new List<(int, double, double, double)>();
        }

        /// <summary>
        /// Build the file_name -> input_files index map used to pick the
        /// right data-file path for the spectra-cache load + sibling
        /// .calibration.json. One source on every route: the user's -i list.
        /// A worker's used to be SYNTHESIZED from --input-scores parquet stems by
        /// Program.Main, which is the round trip that flag forced; both routes now
        /// name the data files, so the stem matches the file_name keys in
        /// perFileEntries without a second derivation.
        /// </summary>
        private static Dictionary<string, int> BuildFileNameToIndex(IReadOnlyList<string> inputFiles)
        {
            var fileNameToIdx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < inputFiles.Count; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(inputFiles[i]) ?? string.Empty;
                fileNameToIdx[stem] = i;
            }
            return fileNameToIdx;
        }

        /// <summary>
        /// Build the per-file scoring subset: the boundary_overrides map
        /// keyed by entry_id, and the subset library handed to
        /// <see cref="ScoringPipeline"/>.RunCoelutionScoring so it
        /// doesn't waste work on entries we're not re-scoring. The subset
        /// is the same library entries the original Stage 1-4 scoring used,
        /// just a smaller list.
        /// </summary>
        private static (Dictionary<uint, (double Apex, double Start, double End)> BoundaryOverrides,
            List<LibraryEntry> SubsetLibrary) BuildScoringSubset(
                Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
                List<FdrEntry> fdrEntries,
                List<LibraryEntry> fullLibrary)
        {
            // Build boundary_overrides keyed by entry_id. Also collect the
            // subset of library ids the search engine needs to score.
            var boundaryOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var subsetIds = new HashSet<uint>();
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                boundaryOverrides[entryId] = kvp.Value;
                subsetIds.Add(entryId);
            }

            List<LibraryEntry> subsetLibrary;
            if (subsetIds.Count == 0)
            {
                subsetLibrary = new List<LibraryEntry>();
            }
            else
            {
                subsetLibrary = new List<LibraryEntry>(subsetIds.Count);
                foreach (var libEntry in fullLibrary)
                {
                    if (subsetIds.Contains(libEntry.Id))
                        subsetLibrary.Add(libEntry);
                }
            }
            return (boundaryOverrides, subsetLibrary);
        }

        /// <summary>
        /// Overlay re-scored entries back onto the per-file FdrEntry stubs
        /// by entry_id, preserving the original ParquetIndex so the
        /// write-back step can target the right Parquet row (post-compaction
        /// Vec position != Parquet row index).
        ///
        /// Mirror Rust's to_fdr_entry semantics: post-rescore stubs carry
        /// default Score (0.0), q-values (1.0), and Pep (1.0). Percolator
        /// (Stage 7, second-pass FDR) recomputes these from the new
        /// Features. Without this reset the Osprey ScoreCandidate's
        /// <c>Score = coelutionSum</c> initializer bleeds through, producing
        /// 173k rows of post-rescore divergence vs the Rust worker's
        /// rust_stage6_rescored.tsv. Targets where RunCoelutionScoring
        /// returned no entry (no peak at the override boundary) STILL get
        /// their existing stub reset in place -- Rust's worker emits zeroed
        /// stubs for every override regardless of peak success. Returns
        /// (entries overlaid, no-peak resets).
        /// </summary>
        private static (int NOverlay, int NNoPeak) OverlayRescoredEntries(
            List<FdrEntry> fdrEntries,
            Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
            List<FdrEntry> rescored,
            HashSet<uint> resetEntryIds)
        {
            // Pass 1: index the rescored results by entry_id so we
            // can look up successful re-scores in the second pass.
            var rescoredByEntryId = new Dictionary<uint, FdrEntry>();
            foreach (var entry in rescored)
            {
                rescoredByEntryId[entry.EntryId] = entry;
            }

            // Pass 2: iterate every combined target.
            int nOverlay = 0;
            int nNoPeak = 0;
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                // entry_id ALONE, deliberately, and this is the one place the full
                // (entry_id, charge, scan_number) identity is the wrong key. That triple is
                // not invariant across a rescore: re-integrating at the consensus boundary
                // moves the apex, so the row's scan_number afterwards differs from the one it
                // had when this target was chosen. The Stage 4 parquet stores the BEFORE scan
                // and the reconciled parquet the AFTER, so a triple captured here matches
                // whichever file the rebuild happens to read and not the other - it matched
                // zero of 37,098 targets on Stellar the first time this ran against the
                // reconciled parquet. entry_id survives the move, and post-compaction
                // DeduplicatePairs makes it unique per file, so it selects exactly one row
                // either way.
                resetEntryIds?.Add(entryId);
                if (rescoredByEntryId.TryGetValue(entryId, out FdrEntry rescoredEntry))
                {
                    rescoredEntry.ResetScores();
                    rescoredEntry.ParquetIndex = fdrEntries[idx].ParquetIndex;
                    fdrEntries[idx] = rescoredEntry;
                    nOverlay++;
                }
                else
                {
                    // No peak at the override boundary -- reset to
                    // defaults in place to match Rust's behavior.
                    fdrEntries[idx].ResetScores();
                    nNoPeak++;
                }
            }
            return (nOverlay, nNoPeak);
        }


        /// <summary>
        /// True when this file's <c>.scores-reconciled.parquet</c> is on disk AND current for
        /// this run's validity key, i.e. the deferred pool build really can restore its
        /// rescored state from it. Asks the same question <see cref="TryResumeRescoredFile"/>
        /// asks, so the answer cannot disagree with it: a parquet the rebuild would refuse to
        /// overlay must not be treated as a safe place to drop this file's entries.
        /// </summary>
        private bool ReconciledParquetOnDisk(string fileName, RescorePassInputs inputs)
        {
            if (inputs.ParquetPaths == null ||
                !inputs.ParquetPaths.TryGetValue(fileName, out string scoresPath))
            {
                return false;
            }
            return PerFileResumeDriver.IsCurrent(
                ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath), Name,
                inputs.TaskValidityKey);
        }

        /// <summary>
        /// The per-run survivor loader FirstPassFDR published, which Stage 6 refills from and
        /// Stage 7 folds with, or null when this run keeps the resident buffer. Non-null
        /// requires that FirstPassFDR published a source, and only the projection path
        /// computes the passing base_id set it needs.
        /// </summary>
        private static FirstPassSurvivorLoader PublishedSurvivorLoader(PipelineContext ctx)
        {
            return ctx.TryGet<FirstPassSurvivorSource>(out var source) ? source?.Value : null;
        }

        /// <summary>
        /// Everything the deferred pool build will do, DECIDED by <see cref="Run(PipelineContext)"/> and
        /// executed later, when a consumer pulls the <see cref="RescoredEntries"/> milestone.
        ///
        /// <para>The work is deferred; the decisions are not. <see cref="ReconciledPaths"/> is
        /// decided once, in Run, and travels with the plan, so the deferred build cannot reach
        /// a different answer. That was first forced by a driver re-stamp, after Run, of every
        /// declared output that merely existed - including a stale
        /// <c>.scores-reconciled.parquet</c> this run had rejected and never rewrote, which a
        /// later <c>PerFileResumeDriver.IsCurrent</c> then accepted, overlaying another arm's
        /// boundaries into this run's blib. That re-stamp no longer exists.</para>
        ///
        /// <para>The planner byproducts travel too rather than being re-read from the context,
        /// so a build in Stage 7 does not hold their last read hostage - see
        /// <c>PipelineContext.Release</c>, "release only when every consumer has already run".</para>
        /// </summary>
        private sealed class RescoredPoolPlan
        {
            /// <param name="buffer">The shared per-file buffer to bring to its post-rescore
            /// state.</param>
            /// <param name="loader">Refills the released survivor lists; null when this run
            /// kept the resident buffer, which leaves nothing to refill.</param>
            /// <param name="rescoredFiles">The files that reached the scoring engine in THIS
            /// process, or null when no rescore ran at all (the self-gated no-op, where the
            /// plan is a refill and nothing else). EMPTY is not null: a rescore that ran and
            /// skipped every file still overlays, exactly as a cold run does.</param>
            /// <param name="reconciledPaths">file name -> the <c>.scores-reconciled.parquet</c>
            /// judged CURRENT in Run. A file absent from this map is a
            /// run Stage 6 did not persist, and it is a hard failure - see
            /// <see cref="ReconciledPathOrFail"/>. Null only on the refill-only plan, which
            /// does not overlay at all.</param>
            /// <param name="resetEntryIds">Per file, the entry_ids whose scores the rescore
            /// reset - captured DURING the rescue rather than re-derived from planner indices,
            /// so the reset survives any reordering of the list it is replayed onto.</param>
            public RescoredPoolPlan(
                List<KeyValuePair<string, List<FdrEntry>>> buffer,
                FirstPassSurvivorLoader loader,
                HashSet<string> rescoredFiles,
                IReadOnlyDictionary<string, string> reconciledPaths,
                IReadOnlyDictionary<string, HashSet<uint>> resetEntryIds)
            {
                Buffer = buffer;
                Loader = loader;
                RescoredFiles = rescoredFiles;
                ReconciledPaths = reconciledPaths;
                ResetEntryIds = resetEntryIds;
            }

            /// <summary>The no-rescore plan: put back what FirstPassFDR released, and stop.</summary>
            public static RescoredPoolPlan RefillOnly(
                List<KeyValuePair<string, List<FdrEntry>>> buffer, FirstPassSurvivorLoader loader)
            {
                return new RescoredPoolPlan(buffer, loader, null, null, null);
            }

            public List<KeyValuePair<string, List<FdrEntry>>> Buffer { get; }
            public FirstPassSurvivorLoader Loader { get; }
            public HashSet<string> RescoredFiles { get; }
            public IReadOnlyDictionary<string, string> ReconciledPaths { get; }
            public IReadOnlyDictionary<string, HashSet<uint>> ResetEntryIds { get; }
        }

        /// <summary>
        /// The per-run source Stage 7 folds through on the straight-through COMPUTE path, or
        /// null when this run cannot supply one - a <c>--task PerFileRescoring</c> worker with
        /// no retained summary, which never pulls it.
        ///
        /// <para>Its body is <see cref="MaterializeRescoredFile"/>.</para>
        ///
        /// <para>A third condition cannot be asked here, and is enforced at fold time instead:
        /// a run whose reconciled parquet never reached disk keeps its re-scored entries in
        /// memory, and a fold that drops them loses the only copy. Only the rescore this
        /// method runs BEFORE can decide that, so the source raises it when asked.</para>
        ///
        /// <para>Two conditions here, and neither is a preference. A run with no survivor loader kept
        /// its resident buffer and has nothing on disk to rebuild a run from, so folding would
        /// DROP the only copy on its way past. And every list must ALREADY be empty: that is
        /// what makes <see cref="MaterializeRescoredFile"/> repeatable, because an empty list
        /// takes its rebuild-from-the-reconciled-parquet branch and skips the gap-fill-appending
        /// overlay that a second call would apply twice. Both hold together exactly when
        /// FirstPassFDR released the survivors, which is the default; the checked form is used
        /// rather than that inference because the inference is the kind that stops being true
        /// quietly.</para>
        ///
        /// <para>Asked WITHOUT the reconciled-parquet term
        /// (<see cref="ScoringTaskShared.Stage7StreamAdmittedBeforeRescore"/>): this decision is
        /// taken at the top of Stage 6, and the parquets the full predicate asks about are what
        /// the rescore below is about to write. This arm does not need them - a run without a
        /// reconciled sibling is rebuilt from its Stage 4 parquet plus its 1st-pass sidecar,
        /// which is the no-work file's normal path here.</para>
        /// </summary>
        private Action<string, List<FdrEntry>> BuildRunPerRunSource(
            PipelineContext ctx, FirstPassSurvivorLoader survivorLoader)
        {
            if (survivorLoader == null ||
                !ScoringTaskShared.Stage7StreamAdmittedBeforeRescore(ctx.Config))
            {
                return null;
            }
            foreach (var kv in _perFileEntries)
            {
                if (kv.Value.Count > 0)
                    return null;
            }
            // The SAME [PATH] second-pass-join key as the other two arms, deliberately: the gate
            // asserts the per-run fold by that key, and an arm that streams under a different
            // key is an arm no verifier can see. Said HERE, at the top of Stage 6, because
            // this is where the decision is taken - a worker that exits before Stage 7 has still
            // made it, and the alternative (report it when the fold starts) is a fact about the
            // consumer rather than about this task.
            ctx.LogVerbose(CountText.Format(_perFileEntries.Count,
                OspreyTasksResources.PerFileRescoreTask_BuildRunPerRunSource_Second_pass_FDR_will_read_the_file_from_its_own_intermediate_files_,
                OspreyTasksResources.PerFileRescoreTask_BuildRunPerRunSource_Second_pass_FDR_will_read_one_file_at_a_time___0__files__from_their_own_intermediate_));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run runs={0}", _perFileEntries.Count));
            return (fileName, entries) =>
            {
                var plan = PoolPlanForBuild();
                // A run with no current reconciled parquet fails in MaterializeRescoredFile
                // below, on BOTH arms, rather than being caught here for the fold alone.
                // ExecuteRescore drops a run's entries only when its reconciled parquet reached
                // disk, KEEPING them when the write no-opped or failed - so on the resident arm
                // that run's rescore survives in memory and the output looks right while
                // nothing was persisted. A fold drops every run it hands over, so the same run
                // would be rebuilt from a parquet that is not there. Neither is a run that
                // finished: one arm cannot be allowed to treat as routine what the other has
                // to fail on, or the missing artifact is a property of the arm.
                //
                // CLEARED here rather than relying on the caller having dropped the run.
                // StreamFiles does drop it, but MaterializeFile leaves that to its caller, and
                // the whole repeatability argument above rests on the list being empty - so the
                // source establishes that itself instead of inheriting it from a call site.
                entries.Clear();
                // plan.Loader is non-null by construction: both plan branches are built from
                // the same survivorLoader this method already refused to proceed without.
                MaterializeRescoredFile(ctx, plan, fileName, entries);
            };
        }

        /// <summary>
        /// The plan <see cref="Run"/> parked, or a hard failure.
        ///
        /// <para>A pull before Run decided is a programming defect, not a case to guess at:
        /// every guess available here - refill-only, or an overlay against paths not yet judged -
        /// silently produces a wrong reported set rather than an error.</para>
        /// </summary>
        private RescoredPoolPlan PoolPlanForBuild()
        {
            if (_poolPlan == null)
            {
                throw new InvalidOperationException(
                    @"RescoredEntries was pulled before PerFileRescoring decided how to build the survivor pool.");
            }
            return _poolPlan;
        }

        /// <summary>
        /// Bring ONE file's survivor list to its post-rescore state: load, reset the rescored
        /// targets, overlay the reconciled values, for a consumer that asks for one file at a
        /// time and DROPS it (#4486).
        ///
        /// <para>Nothing here reads another file's entries, so calling it per file in buffer
        /// order is the same work in the same order as the whole-run build. That equivalence is
        /// the reason a streamed Stage 7 can produce byte-identical output: the difference is
        /// only how long each file's list stays alive.</para>
        /// </summary>
        private void MaterializeRescoredFile(PipelineContext ctx, RescoredPoolPlan plan,
            string fileName, List<FdrEntry> entries)
        {
            // The refill-only plan FIRST, because it is the one route here that legitimately
            // never reads a reconciled parquet - not a run missing one. The resident arm does
            // nothing at all on this route: it leaves the buffer as Stage 5 compacted it and
            // SecondPassFDR reloads the rescored features from the reconciled parquets by
            // identity, so overlaying here would apply Stage-6 boundaries twice (see the
            // RefillOnly call site). Its one job is to put back what FirstPassFDR released.
            if (plan.RescoredFiles == null)
            {
                MaterializeFileSurvivors(fileName, entries, plan.Loader, ctx);
                return;
            }
            // ONE parquet, not two. This file's reconciled parquet already holds the survivor
            // subset with Stage 6's boundaries applied and the gap-fill rows merged - so
            // reading it makes both the Stage 4 read and the overlay that put those values
            // back unnecessary (#4486). Stage 6 originally OVERWROTE the Stage 4 parquet,
            // which is why one read used to give both; splitting the files left Stage 7
            // reading one for the rows and the other for the values.
            // The per-run source clears the list before every call, so the rows always come
            // from the reconciled parquet, Stage 6's boundaries and gap-fill rows included.
            string reconciledPath = ReconciledPathOrFail(fileName, plan.ReconciledPaths, ctx);
            MaterializeFileSurvivors(fileName, entries, plan.Loader, ctx, reconciledPath);
            ResetRescoredTargetsForFile(plan, fileName, entries);
        }

        /// <summary>
        /// Every file whose <c>.scores-reconciled.parquet</c> is on disk and CURRENT for this
        /// run, as a file name -> path map. Asked while <see cref="Run"/> still holds the
        /// answer; see <see cref="RescoredPoolPlan"/> for why after it does not.
        /// </summary>
        private IReadOnlyDictionary<string, string> CurrentReconciledPaths(PipelineContext ctx)
        {
            var current = new Dictionary<string, string>();
            var parquetPaths = ctx.Get<PerFileParquetPaths>().Value;
            if (parquetPaths == null)
                return current;
            string validityKey = ValidityKey(ctx);
            foreach (var kv in _perFileEntries)
            {
                if (!parquetPaths.TryGetValue(kv.Key, out string scoresPath))
                    continue;
                string reconciledPath = ParquetScoreCache.ReconciledPathFromScoresPath(scoresPath);
                if (PerFileResumeDriver.IsCurrent(reconciledPath, Name, validityKey))
                    current.Add(kv.Key, reconciledPath);
            }
            return current;
        }

        /// <summary>
        /// Bring ONE file's list to its post-rescore state on a RESUME, from that file's OWN
        /// <c>.scores-reconciled.parquet</c> - the per-run source Stage 7 folds through
        /// (<see cref="BuildResumePerRunSource"/>).
        ///
        /// <para>ONE parquet, not two, which is the Boundary 3 -&gt; 4 contract: the reconciled
        /// parquet holds the survivor subset with Stage 6's boundaries already applied and the
        /// gap-fill rows already merged in canonical position, so reading it makes both the
        /// Stage 4 read and an overlay that put those values back unnecessary. It also lands
        /// the rows in canonical order (<see cref="FirstPassSurvivorLoader"/> sorts, and its
        /// callers must not re-order).</para>
        ///
        /// <para>CLEARED first rather than relying on the caller having dropped the run - the
        /// same establish-your-own-precondition <see cref="BuildRunPerRunSource"/> states, and
        /// for the same reason. StreamFiles does drop it, but RescoredEntries.MaterializeFile
        /// invokes the source WITHOUT clearing and leaves dropping to its caller, whose own doc
        /// names "merely finished one of several passes over it" as legitimate. The clear is
        /// safe because the run is rebuilt from disk on the next line.</para>
        /// </summary>
        private static void MaterializeResumedFile(string fileName, List<FdrEntry> entries,
            FirstPassSurvivorLoader loader,
            IReadOnlyDictionary<string, string> reconciledPaths, PipelineContext ctx)
        {
            string reconciledPath = ReconciledPathOrFail(fileName, reconciledPaths, ctx);
            entries.Clear();
            MaterializeFileSurvivors(fileName, entries, loader, ctx, reconciledPath);
        }

        /// <summary>
        /// This run's CURRENT <c>.scores-reconciled.parquet</c>, or a hard failure naming the
        /// run. Absence is a fault, never a case to work around: Stage 6 writes this artifact
        /// for every run - <see cref="WriteUnchangedReconciled"/> covers the run it did no work
        /// on - so a run missing from the current set means the write never landed or what
        /// landed is not what this run would produce. That is principle P13 read from the
        /// consumer's end: the producer writes unconditionally so that absence stays
        /// unambiguous, and the consumer is then entitled to treat it as failure.
        ///
        /// <para>The Stage 4 parquet is NOT a substitute, which is what this method exists to
        /// stop being re-derived. It carries pre-reconciliation boundaries and none of the
        /// gap-fill rows, so substituting it writes 1st-pass boundaries for that run into the
        /// blib from a process that exits 0 - and on the resident arm, where the entries are
        /// still in memory, it hides that the run was never persisted at all.</para>
        /// </summary>
        private static string ReconciledPathOrFail(string fileName,
            IReadOnlyDictionary<string, string> reconciledPaths, PipelineContext ctx)
        {
            string reconciledPath = null;
            if (reconciledPaths != null && reconciledPaths.TryGetValue(fileName, out reconciledPath))
                return reconciledPath;
            // Logged as well as thrown: the top-level handler prints the message and a stack
            // trace, where an operator needs the run named in the log beside the phase that
            // failed.
            string error = string.Format(
                OspreyTasksResources.PerFileRescoreTask_ReconciledPathOrFail_Second_pass_FDR__run___0___has_no_current_re_scored_results_file___scores_reconciled_,
                fileName, ParquetScoreCache.EXT_SCORES_RECONCILED, TASK_NAME, ParquetScoreCache.EXT_SCORES,
                OspreyArgNames.TaskText(TASK_NAME));
            ctx.LogError(error);
            ctx.ExitCode = 1;
            throw new InvalidDataException(error);
        }

        /// <summary>
        /// Refill ONE file's survivor list, or leave it alone when it already holds entries
        /// so a second call is a no-op. Every caller now supplies the reconciled-parquet
        /// override except the refill-only plan, which deliberately does not overlay at all
        /// (see <see cref="MaterializeRescoredFile"/>).
        /// </summary>
        private static void MaterializeFileSurvivors(string fileName, List<FdrEntry> entries,
            FirstPassSurvivorLoader loader, PipelineContext ctx,
            string parquetPathOverride = null)
        {
            if (entries.Count > 0)
                return;
            var stubs = loader.Load(fileName, parquetPathOverride, out string error);
            if (stubs == null)
            {
                ctx.LogError(error);
                ctx.ExitCode = 1;
                throw new InvalidDataException(error);
            }
            entries.AddRange(stubs);
        }

        /// <summary>
        /// Reapply the score / q-value reset that <see cref="OverlayRescoredEntries"/>
        /// performs on every rescore target, which the rebuild-from-disk cannot recover.
        ///
        /// <para>A fresh rescore sets each target's Score to 0 and all q-values and Pep to
        /// 1.0, because the 2nd pass is what assigns their real values. That reset lives
        /// ONLY in memory: <c>ReconciledParquetWriter</c> persists boundaries, area and
        /// features, not scores, and a reload from it deliberately preserves each row's
        /// 1st-pass Score / q-values. So a
        /// buffer rebuilt from the reconciled parquet carries 1st-pass q-values where a
        /// fresh run carries 1.0.</para>
        ///
        /// <para>That used to be invisible, because the retired pass-2 Percolator recomputed
        /// q for every entry. Under the frozen-model modes that replaced it (#4528,
        /// protein-compact now the default) an OFF-stratum survivor KEEPS its 1st-pass q,
        /// so the difference reaches the report: without this reset the Stellar straight-
        /// through run reported 31,583 precursors against the golden 29,364.</para>
        ///
        /// <para>The target set is the entry_ids <see cref="OverlayRescoredEntries"/> actually
        /// reset, captured while it ran. It used to be re-derived from the planner's indices,
        /// which address the list as the rescore saw it - correct only for as long as the
        /// rebuild reproduces that order exactly, and NOT once Stage 7 loads from the
        /// reconciled parquet, where the gap-fill rows are already interleaved in canonical
        /// position and shift every index after them (#4486). Identity survives a reordering;
        /// a position does not. It is also the narrower statement: the ids are what was reset,
        /// where planner targets are only what was proposed.</para>
        ///
        /// <para>A no-op for a file that never reached <see cref="OverlayRescoredEntries"/> -
        /// having planner targets is not the same as having been rescored: a file that took
        /// the per-file resume skip (<see cref="TryResumeRescoredFile"/>), or whose
        /// <see cref="TryAssembleRescoreTargets"/> returned false because it has no
        /// <c>input_files</c> entry, returns before the reset and keeps its real 1st-pass
        /// q-values. Resetting those too would zero q-values the resident path leaves alone,
        /// and under protein-compact an off-stratum survivor KEEPS its 1st-pass q - so they
        /// would drop out of the report. That is the mirror image of the over-reporting this
        /// reset exists to fix.</para>
        /// </summary>
        private static void ResetRescoredTargetsForFile(RescoredPoolPlan plan, string fileName,
            List<FdrEntry> entries)
        {
            if (plan.ResetEntryIds == null ||
                !plan.ResetEntryIds.TryGetValue(fileName, out var resetIds) ||
                resetIds.Count == 0)
            {
                return;
            }
            int reset = 0;
            foreach (var entry in entries)
            {
                if (!resetIds.Contains(entry.EntryId))
                    continue;
                entry.ResetScores();
                reset++;
            }
            // Every captured id must match exactly one row. Fewer means the rebuild did not
            // reproduce the list the rescore reset, and the survivors it missed would carry
            // 1st-pass q-values a fresh run sets to 1.0 - which under the frozen-model modes
            // reaches the report (the Stellar straight-through run once counted 31,583
            // precursors against the golden 29,364 for exactly this). More means an entry_id
            // is duplicated in the file, which the post-compaction dedup is supposed to make
            // impossible. Neither is a case to guess at.
            if (reset != resetIds.Count)
            {
                throw new InvalidOperationException(string.Format(
                    @"{0} rebuild: reset {1} peaks for {2} but the rescore reset {3}. " +
                    @"The rebuilt survivor list does not match the one the rescore produced.",
                    TASK_NAME, reset, fileName, resetIds.Count));
            }
        }

        /// <summary>
        /// PHASE 2 gap-fill two-pass for a single file: a CWT pass (prefilter
        /// disabled, peaks picked freely) followed by a forced-integration
        /// pass for the targets CWT missed. CWT + forced results are appended
        /// to <paramref name="fdrEntries"/> as new gap-fill stubs (ParquetIndex
        /// sentinel + score-reset, mirroring Rust to_fdr_entry semantics).
        /// Decoys are intentionally excluded from gap-fill: forcing a random
        /// decoy sequence to be scored at the target's consensus RT has no
        /// biological basis (decoys are not expected to co-elute with their
        /// paired target), and the 1st-pass parquet already has a score for
        /// every decoy at its own natural-but-best peak. Gap-filling decoys
        /// also re-scored them at consensus RT and APPENDED a second parquet
        /// row alongside the existing 1st-pass row, producing exact-duplicate
        /// rows in the reconciled parquet. Those duplicates cascaded into
        /// different max-per-modseq aggregations cross-impl and a 1.1e-4
        /// group_qvalue drift on Astral 3-file. Targets are still gap-filled
        /// because they were missing from this file by definition. Returns
        /// (CWT hits, forced integrations).
        /// </summary>
        private (int NGapCwt, int NGapForced) RunGapFillTwoPass(
            List<GapFillTarget> gapFillTargets,
            List<LibraryEntry> fullLibrary,
            IWindowSpectraProvider spectraProvider,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCal,
            MzCalibrationResult ms2Cal,
            MzCalibrationResult ms1Cal,
            OspreyConfig fileConfig,
            string fileName,
            double? rtMadFromCalJson,
            List<FdrEntry> fdrEntries, PipelineContext ctx)
        {
            int nGapCwt = 0;
            int nGapForced = 0;

            // Both passes collect here instead of appending straight onto fdrEntries, so the
            // whole gap-fill block can be appended ONCE in ascending EntryId order (below).
            // The two passes emit in scoring order - all CWT hits, then all forced - which is
            // an order no other path can reconstruct: it depends on which targets CWT happened
            // to hit, and that is recorded nowhere on disk. The rebuild-from-disk therefore had
            // to append by ascending TargetEntryId and could not match a file that produced
            // both kinds. Every 163-file file does (e.g. 10,965 CWT + 2,361 forced), so the two
            // buffers would differ at scale while 3-file Stellar stayed green. Sorting here
            // makes the order a property of the data rather than of the scoring emit sequence,
            // which both paths can then reproduce.
            var gapFillAppended = new List<FdrEntry>();

            // Build gap-fill library subset (targets only).
            var gapFillIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                gapFillIds.Add(gf.TargetEntryId);
            }
            var gapFillLibrary = new List<LibraryEntry>(gapFillIds.Count);
            foreach (var libEntry in fullLibrary)
            {
                if (gapFillIds.Contains(libEntry.Id))
                    gapFillLibrary.Add(libEntry);
            }

            HashSet<uint> cwtHitIds;
            if (gapFillLibrary.Count > 0)
            {
                // Pass 1: CWT pass with prefilter disabled. Clone
                // fileConfig (already a per-file clone) so the
                // disable is scoped to this CWT pass and doesn't
                // affect the forced-integration pass below.
                var cwtConfig = fileConfig.ShallowClone();
                cwtConfig.PrefilterEnabled = false;
                var cwtContext = new ScoringContext(cwtConfig, fileName);
                cwtContext.OriginalRtMad = rtMadFromCalJson;
                // No BoundaryOverrides -- CWT picks peaks freely.

                var swCwt = Stopwatch.StartNew();
                var cwtResults = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    gapFillLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    cwtContext, passLabel: OspreyTasksResources.PerFileRescoreTask_Rescore_Finding_missing_peaks_in_isolation_windows, logSearchSettings: false);
                swCwt.Stop();

                cwtHitIds = new HashSet<uint>();
                foreach (var entry in cwtResults)
                    cwtHitIds.Add(entry.EntryId);
                nGapCwt = cwtResults.Count;

                // Collect the CWT results as new FdrEntry stubs with no Stage 4
                // row (null ParquetIndex) + score-reset (mirroring Rust
                // to_fdr_entry semantics for new stubs).
                foreach (var entry in cwtResults)
                {
                    entry.ParquetIndex = null;
                    entry.ResetScores();
                    gapFillAppended.Add(entry);
                }

                ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                    OspreyTasksResources.PerFileRescoreTask_private___Missing_peaks_found_by_peak_detection___0____1_s_,
                    nGapCwt, swCwt.Elapsed.TotalSeconds));
            }
            else
            {
                cwtHitIds = new HashSet<uint>();
            }

            // Pass 2: Forced integration for targets CWT missed.
            // Decoys are intentionally excluded from gap-fill (see
            // gapFillIds build above).
            var forcedOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var forcedIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                double start = gf.ExpectedRt - gf.HalfWidth;
                double end = gf.ExpectedRt + gf.HalfWidth;
                if (!cwtHitIds.Contains(gf.TargetEntryId))
                {
                    forcedOverrides[gf.TargetEntryId] = (gf.ExpectedRt, start, end);
                    forcedIds.Add(gf.TargetEntryId);
                }
            }

            if (forcedOverrides.Count > 0)
            {
                var forcedLibrary = new List<LibraryEntry>(forcedIds.Count);
                foreach (var libEntry in gapFillLibrary)
                {
                    if (forcedIds.Contains(libEntry.Id))
                        forcedLibrary.Add(libEntry);
                }

                var forcedContext = new ScoringContext(fileConfig, fileName);
                forcedContext.BoundaryOverrides = forcedOverrides;
                forcedContext.OriginalRtMad = rtMadFromCalJson;

                var swForced = Stopwatch.StartNew();
                var forcedResults = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                    forcedLibrary, spectraProvider, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    forcedContext, passLabel: OspreyTasksResources.PerFileRescoreTask_Rescore_Integrating_missing_peaks_at_imputed_boundaries_in_isolation_windows,
                    logSearchSettings: false);
                swForced.Stop();
                nGapForced = forcedResults.Count;

                foreach (var entry in forcedResults)
                {
                    entry.ParquetIndex = null;
                    entry.ResetScores();
                    gapFillAppended.Add(entry);
                }

                ctx.LogInfo(TextUtil.GetIndentation(1) + string.Format(
                    OspreyTasksResources.PerFileRescoreTask_private___Missing_peaks_integrated_at_imputed_boundaries___0____1_s_,
                    nGapForced, swForced.Elapsed.TotalSeconds));
            }

            // Append the whole block at the END of the buffer, in ascending EntryId - the one
            // order the rebuild-from-disk can also produce (it appends by ascending
            // TargetEntryId from the persisted target list). Appending at the end rather than
            // merging into canonical position is itself required: the Stage 6 planner's
            // positional indices address the survivors as loaded, so anything inserted before
            // them shifts every index after it.
            //
            // The CWT and forced blocks are disjoint (forced runs only for the targets CWT
            // missed), but EntryId is NOT unique within this list: neither pass runs
            // DeduplicatePairs or DeduplicateDoubleCounting, and ScoreWindow admits a candidate
            // per isolation window, so one target scored in two overlapping windows emits two
            // rows. Charge and ScanNumber separate them, and every row carries the same
            // null ParquetIndex (no Stage 4 row) - so the comparison can only tie on rows that
            // agree on all four keys, which are indistinguishable anyway. The rebuild-from-disk
            // reproduces this by appending EVERY reconciled row for a target, in the parquet's
            // canonical (entry_id, charge, scan) order, which is the order this sort produces.
            // OrderBy, not Sort: ties ARE expected here - these rows are appended before the
            // writer numbers them, so CANONICAL_ORDER's terminal key is unresolved for all of
            // them and equal-key rows are common. List.Sort is unstable, and these are FdrEntry
            // OBJECTS carrying distinct features and blobs, so "indistinguishable" is true of
            // the comparator key but not of the rows. A stable sort makes the emitted order a
            // function of the input order rather than of the sort's internal partitioning.
            gapFillAppended = gapFillAppended.OrderBy(e => e, FdrEntry.CANONICAL_COMPARER).ToList();
            fdrEntries.AddRange(gapFillAppended);

            return (nGapCwt, nGapForced);
        }
    }
}
