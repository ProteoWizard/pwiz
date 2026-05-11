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
using System.Diagnostics;
using System.IO;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp
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
        /// <returns>0 on success, non-zero on failure.</returns>
        public int Run(OspreyConfig config)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Pipeline definition: the four HPC-boundary phases in
                // order. Tasks read upstream state through
                // ctx.GetTask<T>().GetX() rather than constructor args;
                // each task short-circuits its own work when there is
                // nothing to do (e.g. PerFileRescoreTask checks
                // FirstJoinTask.DidPlan internally and returns true as a
                // no-op when planning was skipped). Returning false from
                // any task is the signal to stop and propagate
                // ctx.ExitCode.
                var pipelineTasks = new OspreyTask[]
                {
                    new PerFileScoringTask(),
                    new FirstJoinTask(),
                    new PerFileRescoreTask(),
                    new MergeNodeTask()
                };
                var startAt = DeriveStartAtTask(config);
                var stopAfter = DeriveStopAfterTask(config);
                var ctx = new PipelineContext(config, pipelineTasks,
                    LogInfo, LogWarning, LogError, startAt, stopAfter);

                // ctx.StartAtTask / ctx.StopAfterTask are set but not yet
                // honored as task-skip boundaries here. Activating that
                // gating depends on lazy-rehydrate accessors on the
                // producer tasks (commit 3); until those exist, every task
                // must run so it can hydrate its own state from disk via
                // its existing CLI-mode branch.
                foreach (var task in ctx.Tasks)
                {
                    if (!RunTask(task, ctx))
                        return ctx.ExitCode;
                }

                stopwatch.Stop();
                LogInfo("");
                LogInfo(string.Format("[TIMING] Total pipeline: {0:F1}s",
                    stopwatch.Elapsed.TotalSeconds));
                LogInfo(string.Format("Analysis complete in {0}", FormatDuration(stopwatch.Elapsed)));
                return 0;
            }
            catch (Exception ex)
            {
                LogError(string.Format("Pipeline failed: {0}", ex.Message));
                LogError(ex.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// Derives the StartAt task for <paramref name="config"/> from the
        /// HPC CLI flags it carries. The four mass-spec pipeline tasks
        /// (PerFileScoring → FirstJoin → PerFileRescore → MergeNode) are
        /// always present in the registry; this picks which one
        /// <see cref="AnalysisPipeline.Run"/> starts at, leaving any
        /// earlier tasks unrun (their lazy-rehydrate accessors fetch state
        /// from disk if a downstream task needs it).
        /// </summary>
        internal static Type DeriveStartAtTask(OspreyConfig config)
        {
            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            if (hasInputScores)
            {
                // --join-at-pass=2 --input-scores ...
                if (config.ExpectReconciledInput)
                    return typeof(MergeNodeTask);
                // --join-at-pass=1 --no-join --input-scores ... (rescore worker)
                if (config.NoJoin)
                    return typeof(PerFileRescoreTask);
                // --join-at-pass=1 --join-only --input-scores ...
                if (config.StopAfterStage5)
                    return typeof(FirstJoinTask);
            }
            return typeof(PerFileScoringTask);
        }

        /// <summary>
        /// Derives the StopAfter task for <paramref name="config"/> from the
        /// HPC CLI flags it carries. See <see cref="DeriveStartAtTask"/>
        /// for the parallel start-point derivation.
        /// </summary>
        internal static Type DeriveStopAfterTask(OspreyConfig config)
        {
            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            // --no-join (no --input-scores): only Stages 1-4
            if (config.NoJoin && !hasInputScores)
                return typeof(PerFileScoringTask);
            // --join-at-pass=1 --no-join --input-scores ... (rescore worker)
            if (config.NoJoin && hasInputScores)
                return typeof(PerFileRescoreTask);
            // --join-at-pass=1 --join-only (with or without --input-scores)
            if (config.StopAfterStage5)
                return typeof(FirstJoinTask);
            // --join-at-pass=2 --input-scores ... (post-reconciled merge only)
            if (config.ExpectReconciledInput && hasInputScores)
                return typeof(MergeNodeTask);
            return typeof(MergeNodeTask);
        }

        #region Utility Methods

        /// <summary>
        /// Run a single task against <paramref name="ctx"/> with consistent
        /// start/done logging and wall-time measurement. Returns the task's
        /// own <see cref="OspreyTask.Run"/> result so the caller can
        /// short-circuit on <c>false</c> and propagate
        /// <see cref="PipelineContext.ExitCode"/>.
        ///
        /// Phase B skip-if-outputs-valid: before invoking
        /// <see cref="OspreyTask.Run"/>, check whether every output declared
        /// by <see cref="OspreyTask.Outputs"/> exists with a matching
        /// <c>.osprey.task</c> sidecar (validity-key check against
        /// <see cref="OspreyTask.ValidityKey"/>). If so, the task's work is
        /// already on disk -- log and return true without executing.
        /// Otherwise, stale sidecars are cleared (so a mid-Run crash leaves
        /// no false-positive sidecar), the task is run, and fresh sidecars
        /// are written next to each declared output on success.
        /// </summary>
        private static bool RunTask(OspreyTask task, PipelineContext ctx)
        {
            if (IsTaskAlreadyDone(task, ctx))
            {
                ctx.LogInfo(string.Format(@"[task] {0}: skipping (outputs valid)", task.Name));
                return true;
            }

            DeleteTaskSidecars(task, ctx);

            var sw = Stopwatch.StartNew();
            ctx.LogInfo(string.Format(@"[task] {0}: starting", task.Name));
            bool keepGoing = task.Run(ctx);
            sw.Stop();
            ctx.LogInfo(string.Format(@"[task] {0}: done ({1:F1}s)",
                task.Name, sw.Elapsed.TotalSeconds));

            if (keepGoing)
                WriteTaskSidecars(task, ctx);

            return keepGoing;
        }

        private static bool IsTaskAlreadyDone(OspreyTask task, PipelineContext ctx)
        {
            var outputs = new List<string>(task.Outputs(ctx));
            if (outputs.Count == 0) return false;
            string key = task.ValidityKey(ctx);
            foreach (var output in outputs)
            {
                if (!File.Exists(output)) return false;
                if (!TaskValiditySidecar.IsValid(output, task.Name, key)) return false;
            }
            return true;
        }

        private static void WriteTaskSidecars(OspreyTask task, PipelineContext ctx)
        {
            string key = task.ValidityKey(ctx);
            var inputs = new List<string>(task.Inputs(ctx));
            foreach (var output in task.Outputs(ctx))
            {
                // A task may not have written every declared output (e.g. a
                // task that emits an output only when an optional config field
                // is set). Skip those rather than failing.
                if (!File.Exists(output)) continue;
                try
                {
                    TaskValiditySidecar.Write(output, task.Name, Program.VERSION, key, inputs);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        @"Failed to write {0} sidecar for {1}: {2}",
                        task.Name, output, ex.Message));
                }
            }
        }

        private static void DeleteTaskSidecars(OspreyTask task, PipelineContext ctx)
        {
            foreach (var output in task.Outputs(ctx))
                TaskValiditySidecar.Delete(output, task.Name);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                if (duration.Hours > 0)
                    return string.Format("{0} days {1} hours", (int)duration.TotalDays, duration.Hours);
                return string.Format("{0} days", (int)duration.TotalDays);
            }
            if (duration.TotalHours >= 1)
            {
                if (duration.Minutes > 0)
                    return string.Format("{0} hours {1} minutes", (int)duration.TotalHours, duration.Minutes);
                return string.Format("{0} hours", (int)duration.TotalHours);
            }
            if (duration.TotalMinutes >= 1)
            {
                if (duration.Seconds > 0)
                    return string.Format("{0} minutes {1} seconds", (int)duration.TotalMinutes, duration.Seconds);
                return string.Format("{0} minutes", (int)duration.TotalMinutes);
            }
            if (duration.TotalSeconds >= 1)
                return string.Format("{0:F3} seconds", duration.TotalSeconds);
            return string.Format("{0} ms", (int)duration.TotalMilliseconds);
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
