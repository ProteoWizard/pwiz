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
                // Select the diagnostics sink before any task runs -- the single
                // chokepoint every entry point reaches the pipeline through
                // (Program.Main and the rescore worker). -d forces the dump
                // bundle on; otherwise the sink self-enables only if an
                // OSPREY_DUMP_* / OSPREY_DIAG_* env var is set.
                OspreyDiagnostics.Initialize(config.Diagnostics);

                // Worker-mode entry normalization: in --input-scores modes
                // without explicit -i, synthesize InputFiles from the parquet
                // stems ONCE here, at pipeline entry, so the driver's
                // Outputs/IsTaskAlreadyDone skip checks and every per-task
                // accessor see a populated InputFiles regardless of which task
                // the run starts at. (Mutation-contract: InputFiles is a
                // pipeline-populated field that does NOT feed any identity
                // hash, so it may be written once at entry -- see
                // PipelineContext.Config. Previously this lived inside
                // PerFileScoringTask's join-only load, which the driver never
                // reached when PerFileScoring was the StartAt task, e.g.
                // `--task PerFileScoring --input-scores`.)
                if (config.InputScores != null && config.InputScores.Count > 0
                    && (config.InputFiles == null || config.InputFiles.Count == 0))
                {
                    var synthetic = new List<string>(config.InputScores.Count);
                    foreach (var p in config.InputScores)
                        synthetic.Add(RescoreHydration.SyntheticInputFromParquet(p));
                    config.InputFiles = synthetic;
                }

                var pipelineTasks = CanonicalPipeline();
                var ctx = new PipelineContext(config, pipelineTasks,
                    LogInfo, LogWarning, LogError, OspreyDiagnostics.Active);

                // Phase B5 driver-owned dataflow: walk the canonical pipeline
                // and run each INCLUDED task whose outputs are not already
                // valid on disk. Membership is a per-task fact
                // (OspreyTask.IsIncluded) rather than a contiguous
                // [StartAt..StopAfter] window. Excluded tasks -- and included
                // tasks whose outputs already exist (ctx.CanRehydrate) -- are
                // not run here; their state lazy-rehydrates through ctx.Demand
                // when a running task reaches for it. A task returning false is
                // still the signal to stop and propagate ctx.ExitCode (e.g. an
                // empty score set or a sidecar-write failure).
                foreach (var task in pipelineTasks)
                {
                    if (!task.IsIncluded(ctx))
                        continue;

                    if (ctx.CanRehydrate(task))
                    {
                        LogInfo(string.Format(@"[TASK] {0}:skipping (outputs valid)", task.Name));
                        continue;
                    }

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
        /// The canonical four-task pipeline in execution order:
        /// PerFileScoring -> FirstJoin -> PerFileRescore -> MergeNode.
        /// Single source of truth for the task list. Tasks read upstream
        /// state through ctx.Demand&lt;T&gt;().GetX() rather than constructor
        /// args; the driver runs each task that is
        /// <see cref="OspreyTask.IsIncluded"/> for the current config and whose
        /// outputs are not already valid on disk. Returning false from any task
        /// is the signal to stop and propagate ctx.ExitCode.
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
        /// disk. The task is run and fresh <c>.osprey.task</c> sidecars are
        /// written next to each declared output on success.
        /// </summary>
        private static bool RunTask(OspreyTask task, PipelineContext ctx)
        {
            // Note: stale-sidecar cleanup is the responsibility of each
            // task body. A task-level pre-Run delete here would wipe the
            // per-file sidecars that <see cref="PerFileScoringTask"/>
            // relies on for its within-task per-file skip; deletion has
            // to happen on per-file granularity for tasks that produce
            // per-file outputs. Tasks that produce a single coarse output
            // (e.g. MergeNodeTask's output.blib) delete their own
            // sidecars at the start of Run.

            var sw = Stopwatch.StartNew();
            ctx.LogInfo(string.Format(@"[TASK] {0}:starting", task.Name));
            bool keepGoing = task.Run(ctx);
            // The driver has now run this task, so its state is in memory: mark it
            // materialized so a later Demand/Get by a downstream task returns the
            // computed state instead of driving Rehydrate. Replaces the per-task
            // _runOrHydrated guard that formerly bridged the Run and Rehydrate paths.
            ctx.MarkMaterialized(task);
            sw.Stop();
            ctx.LogInfo(string.Format(@"[TASK] {0}:done ({1:F1}s)",
                task.Name, sw.Elapsed.TotalSeconds));

            // [STAGE-WALL] one line per task->stage with parseable format
            // for Measure-Pipeline.ps1 / Osprey-workflow.html perf tables.
            // MergeNodeTask emits its own stage7 + blib lines internally
            // (one task -> two pipeline stages).
            string stageName = task.Name switch
            {
                "PerFileScoring"   => "stage1to4",
                "FirstPassFDR"     => "stage5",
                "PerFileRescoring" => "stage6",
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
            // boundary (PerFileScoringTask under --task PerFileScoring, FirstJoinTask
            // under --task FirstPassFDR with StopAfterStage5); gating on
            // keepGoing alone would skip sidecar writes for those
            // successful early-exit modes and break resume.
            if (ctx.ExitCode == 0)
                WriteTaskSidecars(task, ctx);

            return keepGoing;
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
                    TaskValiditySidecar.Write(output, task.Name, OspreyVersion.Current, key, inputs);
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
