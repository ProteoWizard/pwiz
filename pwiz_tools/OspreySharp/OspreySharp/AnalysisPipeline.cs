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
                var pipelineTasks = CanonicalPipeline();
                var startAt = DeriveStartAtTask(config);
                var stopAfter = DeriveStopAfterTask(config);
                var ctx = new PipelineContext(config, pipelineTasks,
                    LogInfo, LogWarning, LogError, startAt, stopAfter);

                // Phase B range-gating: tasks before StartAt are skipped
                // silently (their state lazy-rehydrates from disk via the
                // producer-task accessors when a downstream task queries
                // it); tasks at-or-after StartAt run normally through
                // RunTask's sidecar dance; iteration stops after StopAfter.
                bool inRange = false;
                foreach (var task in ctx.Tasks)
                {
                    if (!inRange)
                    {
                        if (task.GetType() != ctx.StartAtTask)
                            continue;
                        inRange = true;
                    }

                    if (!RunTask(task, ctx))
                        return ctx.ExitCode;

                    if (task.GetType() == ctx.StopAfterTask)
                        break;
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
        /// The canonical four-task pipeline in execution order:
        /// PerFileScoring -> FirstJoin -> PerFileRescore -> MergeNode.
        /// Single source of truth for the task list. Tasks read upstream
        /// state through ctx.GetTask&lt;T&gt;().GetX() rather than
        /// constructor args; each task short-circuits its own work when
        /// there is nothing to do (e.g. PerFileRescoreTask checks
        /// FirstJoinTask.DidPlan internally and returns true as a no-op
        /// when planning was skipped). Returning false from any task is
        /// the signal to stop and propagate ctx.ExitCode.
        /// </summary>
        internal static OspreyTask[] CanonicalPipeline()
        {
            return new OspreyTask[]
            {
                new PerFileScoringTask(),
                new FirstJoinTask(),
                new PerFileRescoreTask(),
                new MergeNodeTask(),
            };
        }

        /// <summary>
        /// Derives the StartAt task for <paramref name="config"/> from the
        /// HPC CLI flags it carries. The four mass-spec pipeline tasks
        /// (PerFileScoring -> FirstJoin -> PerFileRescore -> MergeNode) are
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

            // Note: stale-sidecar cleanup is the responsibility of each
            // task body. A task-level pre-Run delete here would wipe the
            // per-file sidecars that <see cref="PerFileScoringTask"/>
            // relies on for its within-task per-file skip; deletion has
            // to happen on per-file granularity for tasks that produce
            // per-file outputs. Tasks that produce a single coarse output
            // (e.g. MergeNodeTask's output.blib) delete their own
            // sidecars at the start of Run.

            var sw = Stopwatch.StartNew();
            ctx.LogInfo(string.Format(@"[task] {0}: starting", task.Name));
            bool keepGoing = task.Run(ctx);
            sw.Stop();
            ctx.LogInfo(string.Format(@"[task] {0}: done ({1:F1}s)",
                task.Name, sw.Elapsed.TotalSeconds));

            // [STAGE-WALL] one line per task->stage with parseable format
            // for Measure-Pipeline.ps1 / Osprey-workflow.html perf tables.
            // MergeNodeTask emits its own stage7 + blib lines internally
            // (one task -> two pipeline stages).
            string stageName = task.Name switch
            {
                "PerFileScoring" => "stage1to4",
                "FirstJoin"      => "stage5",
                "PerFileRescore" => "stage6",
                _                => null,
            };
            if (stageName != null)
            {
                ctx.LogInfo(string.Format(@"[STAGE-WALL] {0}: {1:F1}s",
                    stageName, sw.Elapsed.TotalSeconds));
            }

            // Write sidecars whenever the task ran without setting a
            // non-zero exit code. Several tasks intentionally return
            // false on success to stop the pipeline at a configured
            // boundary (PerFileScoringTask under --no-join, FirstJoinTask
            // under --join-only-with-StopAfterStage5); gating on
            // keepGoing alone would skip sidecar writes for those
            // successful early-exit modes and break resume.
            if (ctx.ExitCode == 0)
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
