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
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey
{
    /// <summary>
    /// Main analysis pipeline orchestrating the end-to-end Osprey workflow.
    /// Port of osprey/src/pipeline.rs run_analysis().
    ///
    /// Stages:
    /// 1. Load library + generate decoys
    /// 2. Per-file: load spectra, calibrate RT, run coelution scoring
    /// 3. First-pass FDR (Percolator or simple)
    /// 4. Protein FDR (optional)
    /// 5. Write blib output
    /// </summary>
    public class AnalysisPipeline
    {
        /// <summary>
        /// Run the complete analysis pipeline.
        /// </summary>
        /// <param name="config">Analysis configuration.</param>
        /// <param name="pipeline">The stages this run walks, in execution order - the list
        /// <see cref="OspreyConfig.SelectTask"/> was given (<see cref="OspreyTasks.PipelineFor"/>),
        /// so the driver and the selection share instances.</param>
        /// <returns>0 on success, non-zero on failure.</returns>
        public int Run(OspreyConfig config, IReadOnlyList<OspreyTask> pipeline)
        {
            var stopwatch = Stopwatch.StartNew();

            // The driver walks the same instances the selection was resolved against, or the
            // by-reference membership rule fails silently: every stage excluded (a no-op
            // "Analysis complete") for a second list, or every stage included for a namesake
            // selection. Program.Main hands SelectTask and this method one variable; refuse
            // anything else rather than run a pipeline the config does not describe.
            if (!ReferenceEquals(config.Pipeline, pipeline))
                throw new ArgumentException(@"The pipeline to run must be the one the config's task was selected with.", nameof(pipeline));

            try
            {
                // Select the diagnostics sink before any task runs -- the single
                // chokepoint every invocation reaches the pipeline through
                // (Program.Main, whatever --task it selected). -d forces the dump
                // bundle on; otherwise the sink self-enables only if an
                // OSPREY_DUMP_* / OSPREY_DIAG_* env var is set.
                OspreyDiagnostics.Initialize(config.Diagnostics);
                // An ambient/forgotten OSPREY_KEEP_FAILED_WRITES silently stops EVERY
                // FileSaver in the process from cleaning up an abandoned temp, in every
                // run that inherits it, not just the diagnostic session someone meant to
                // inspect - the same class of hazard OSPREY_ALLOW_UNFIXED_RESIDENT being
                // left set once masked a real regression for ten days. Unlike that flag
                // this one has no per-run cost to warn about even when it does nothing
                // (most runs leave no abandoned write), so log it unconditionally rather
                // than only when it turns out to matter.
                if (OspreyEnvironment.KeepFailedWrites)
                    LogWarning(@"OSPREY_KEEP_FAILED_WRITES is set: an abandoned FileSaver write leaves its temp file on disk instead of being cleaned up.");

                // No worker-mode entry normalization any more, and its absence is the
                // point. A --input-scores run arrived here with parquet paths and no
                // InputFiles, so the pipeline's FIRST act was to convert them back into
                // data-file names - a round trip through a synthetic <stem>.mzML that does
                // not exist, purely so the sidecar helpers could derive from a stem. Every
                // task now receives the stems it needs on -i, which is the direction the
                // derivation was always going.

                var ctx = new PipelineContext(config, pipeline,
                    LogInfo, LogWarning, LogError, OspreyDiagnostics.Active);

                // Phase B5 driver-owned dataflow: walk the pipeline and run each
                // INCLUDED stage whose outputs are not already valid on disk.
                // Membership is one rule over the selection and the pipeline
                // (OspreyConfig.Includes: everything, or the selected stage alone)
                // rather than a contiguous [StartAt..StopAfter] window or a
                // per-task predicate over flags. Excluded stages - and included
                // stages whose outputs already exist (ctx.CanRehydrate) - are
                // not run here; their state lazy-rehydrates through ctx.Demand
                // when a running stage reaches for it. A task returning false is
                // still the signal to stop: with a failure exit code the run ends
                // there (e.g. a sidecar-write failure); with exit code 0 the task
                // stopped on purpose (a per-file worker's boundary, an empty score
                // set), so the run is complete and says so like any other.
                foreach (var task in pipeline)
                {
                    if (!config.Includes(task))
                        continue;

                    if (ctx.CanRehydrate(task))
                    {
                        ctx.LogInfo(LogTag.TASK, @"{0}:skipping (outputs valid)", task.Name);
                        continue;
                    }

                    if (!RunTask(task, ctx))
                    {
                        if (ctx.ExitCode != 0)
                            return ctx.ExitCode;
                        break;
                    }
                }

                stopwatch.Stop();
                LogInfo(string.Empty);
                ctx.LogInfo(LogTag.TIMING, @"Total pipeline: {0:F1}s",
                    stopwatch.Elapsed.TotalSeconds);
                LogInfo(string.Format(OspreyResources.AnalysisPipeline_Run_Analysis_complete_in__0_, FormatDuration(stopwatch.Elapsed)));
                return 0;
            }
            catch (Exception ex)
            {
                // Skyline's line (CommonExceptionUtil.IsProgrammingDefect): a file or data
                // problem the user can act on is reported as its message; anything else is a
                // defect and is reported whole, not ex.Message plus ex.StackTrace. Message can
                // be empty and a wrapper carries its real cause only in InnerException, so the
                // pair could name the throwing frame while saying nothing about why: a 17-hour
                // 163-file run ended in "Pipeline failed: " and a bare BlibWriter constructor
                // frame. ToString() prints the type, the message, every inner exception and
                // the stack.
                LogError(Program.DescribeFailure(ex, OspreyResources.AnalysisPipeline_Run_Pipeline_failed___0_));
                return 1;
            }
        }

        #region Utility Methods

        /// <summary>
        /// Run a single task against <paramref name="ctx"/> with consistent
        /// start/done logging and wall-time measurement. Returns the task's
        /// own <see cref="OspreyTask.Run"/> result so the caller can
        /// short-circuit on <c>false</c> and propagate
        /// <see cref="PipelineContext.ExitCode"/>.
        ///
        /// The skip-if-outputs-valid decision now lives in the driver loop
        /// (<see cref="PipelineContext.CanRehydrate"/>): this is only called
        /// for a task that is included and whose outputs are not already on
        /// disk. Each output's writer embeds its validity stamp in the same
        /// commit as its content; the driver writes nothing after Run.
        /// </summary>
        private static bool RunTask(OspreyTask task, PipelineContext ctx)
        {
            // Note: nothing is cleared before Run. A stale output is simply
            // overwritten, stamp and all, and a pre-Run delete here would
            // wipe the per-file outputs that <see cref="PerFileScoringTask"/>
            // relies on for its within-task per-file skip.

            var sw = Stopwatch.StartNew();
            ctx.LogInfo(LogTag.TASK, @"{0}:starting", task.Name);
            bool keepGoing = task.Run(ctx);
            // The driver has now run this task, so its state is in memory: mark it
            // materialized so a later Demand/Get by a downstream task returns the
            // computed state instead of driving Rehydrate. Replaces the per-task
            // _runOrHydrated guard that formerly bridged the Run and Rehydrate paths.
            ctx.MarkMaterialized(task);
            sw.Stop();
            ctx.LogInfo(LogTag.TASK, @"{0}:done ({1:F1}s)",
                task.Name, sw.Elapsed.TotalSeconds);
            if (OspreyEnvironment.BlockReadMb > 0)
                ctx.LogInfo(LogTag.PATH, @"{0}: {1}", task.Name, BlockReadStats.Text());
            // DIAGNOSTIC (OSPREY_DROP_BETWEEN_TASKS=1): make the in-process pipeline behave like
            // the HPC split - this task drops everything but the library, and the next reloads
            // what it needs from artifacts. Off by default; the whole experiment reverts
            // together. See PipelineContext.DropAllButLibrary.
            if (OspreyEnvironment.DropBetweenTasks)
                ctx.DropAllButLibrary();

            // [STAGE-WALL] one line per task, labelled with the Rust Osprey stage it
            // covers, for the perf tooling that compares the two implementations
            // (Measure-Pipeline.ps1, Test-PerfGate.ps1, Get-MemoryReport.ps1). Machine text
            // only: the tag prints under --perf-stats and nowhere else. SecondPassFDR has no
            // entry on purpose, because its wall is NOT one Rust stage: it covers Rust's
            // stage 7 (second-pass FDR + protein FDR) and the blib write, so the task emits
            // those walls itself around the steps that match - second-pass-fdr (from
            // Pass2FdrSidecar), stage7 (protein FDR only) and blib - and Measure-Pipeline.ps1
            // adds second-pass-fdr into stage7. A whole-task stage7 line here would count the
            // second-pass FDR twice. SpectraCache and ModelDiagnostics are not Rust stages.
            string stageName = task.Name switch
            {
                PerFileScoringTask.TASK_NAME => @"stage1to4",
                FirstPassFdrTask.TASK_NAME => @"stage5",
                PerFileRescoreTask.TASK_NAME => @"stage6",
                _                => null,
            };
            if (stageName != null)
            {
                ctx.LogInfo(LogTag.STAGE_WALL, @"{0}: {1:F1}s",
                    stageName, sw.Elapsed.TotalSeconds);
            }

            return keepGoing;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                if (duration.Hours > 0)
                    return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__days__1__hours, (int)duration.TotalDays, duration.Hours);
                return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__days, (int)duration.TotalDays);
            }
            if (duration.TotalHours >= 1)
            {
                if (duration.Minutes > 0)
                    return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__hours__1__minutes, (int)duration.TotalHours, duration.Minutes);
                return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__hours, (int)duration.TotalHours);
            }
            if (duration.TotalMinutes >= 1)
            {
                if (duration.Seconds > 0)
                    return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__minutes__1__seconds, (int)duration.TotalMinutes, duration.Seconds);
                return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__minutes, (int)duration.TotalMinutes);
            }
            if (duration.TotalSeconds >= 1)
                return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__seconds, duration.TotalSeconds);
            return string.Format(OspreyResources.AnalysisPipeline_FormatDuration__0__ms, (int)duration.TotalMilliseconds);
        }

        private static void LogInfo(string message)
        {
            Program.LogInfo(message);
        }

        private static void LogWarning(string message)
        {
            Program.LogWarning(message);
        }

        private static void LogError(string message)
        {
            Program.LogError(message);
        }

        #endregion
    }
}
