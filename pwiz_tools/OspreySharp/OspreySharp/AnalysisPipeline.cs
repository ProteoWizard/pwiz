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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

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
    public partial class AnalysisPipeline : AbstractScoringTask
    {
        // AbstractScoringTask requires Name + Run(ctx) overrides;
        // AnalysisPipeline never runs as a task itself (it is the
        // pipeline orchestrator). The stubs satisfy the abstract
        // contract; they are never invoked because AnalysisPipeline
        // is not added to a Tasks.Pipeline.
        public override string Name => @"AnalysisPipeline";
        public override bool Run(Tasks.PipelineContext ctx) =>
            throw new System.NotSupportedException(
                @"AnalysisPipeline is not a runnable task; call Run(OspreyConfig) instead.");

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
                // Per-file scoring phase (Stages 1-4): library load
                // + decoys + per-file ProcessFile loop. Driven by
                // PerFileScoringTask; outputs (FullLibrary,
                // LibraryById, PerFileEntries, PerFileCalibrations,
                // PerFileParquetPaths) flow back through instance
                // properties on the task. Returning false here
                // means the library was empty (error), the
                // --join-at-pass=2 sidecar load failed (error), or
                // the --no-join CLI flag wants us to stop after the
                // per-file fan-out (success). ctx.ExitCode is the
                // process exit code AnalysisPipeline.Run should
                // propagate in any of those cases.
                var perFileScoringTask = new PerFileScoring.PerFileScoringTask(this);
                var perFileScoringCtx = new Tasks.PipelineContext(
                    config, LogInfo, LogWarning, LogError);
                var perFileScoringPipeline = new Tasks.Pipeline(
                    new Tasks.OspreyTask[] { perFileScoringTask });
                if (!perFileScoringPipeline.Execute(perFileScoringCtx))
                    return perFileScoringCtx.ExitCode;

                var fullLibrary = perFileScoringTask.FullLibrary;
                var libraryById = perFileScoringTask.LibraryById;
                var perFileEntries = perFileScoringTask.PerFileEntries;
                var perFileCalibrations = perFileScoringTask.PerFileCalibrations;
                var perFileParquetPaths = perFileScoringTask.PerFileParquetPaths;

                // First-join phase: Stage 5 first-pass FDR + first-pass
                // protein FDR + 1st-pass FDR sidecar persistence +
                // post-FDR compaction + (on --join-at-pass=2) 2nd-pass
                // sidecar overlay + Stage 6 planning. Mirrors the
                // workflow.html "first join" boundary -- the natural
                // HPC fan-out / join transition where all-file
                // representation is required. Returning false here
                // means a real error or a successful StopAfterStage5
                // exit; in either case ctx.ExitCode is the process
                // exit code AnalysisPipeline.Run should propagate.
                var firstJoinTask = new FirstJoin.FirstJoinTask(
                    perFileEntries, perFileCalibrations,
                    perFileParquetPaths, fullLibrary);
                var firstJoinCtx = new Tasks.PipelineContext(
                    config, LogInfo, LogWarning, LogError);
                var firstJoinPipeline = new Tasks.Pipeline(new Tasks.OspreyTask[] { firstJoinTask });
                if (!firstJoinPipeline.Execute(firstJoinCtx))
                    return firstJoinCtx.ExitCode;

                // Stage 6 per-file rescore: driven by PerFileRescoreTask.
                // Only runs when FirstJoinTask actually planned (i.e.
                // not on --join-at-pass=2, not on empty perFileEntries,
                // and only when Reconciliation.Enabled is true).
                // Encapsulates ExecuteStage6Rescore + the cross-impl
                // bisection dump + the per-process diagnostic-writer
                // close calls that pair with it. Mirrors the Rust
                // call site at pipeline.rs:run_analysis ~line 3850.
                if (firstJoinTask.DidPlan)
                {
                    var rescoreCtx = new Tasks.PipelineContext(
                        config, LogInfo, LogWarning, LogError);
                    var rescorePipeline = new Tasks.Pipeline(new Tasks.OspreyTask[]
                    {
                        new PerFileRescore.PerFileRescoreTask(
                            this,
                            perFileEntries,
                            firstJoinTask.PerFileConsensusTargets,
                            firstJoinTask.ReconciliationActions,
                            firstJoinTask.RefinedCalibrations,
                            perFileCalibrations,
                            firstJoinTask.PerFileGapFillForRescore,
                            perFileParquetPaths,
                            fullLibrary)
                    });
                    rescorePipeline.Execute(rescoreCtx);
                }

                // Merge-node phase: 2nd-pass FDR sidecar persistence (when
                // applicable), run-wide protein FDR, blib output. Driven
                // through the Phase A task-based pipeline so the
                // boundary between Stage 6 (per-file rescore) and the
                // post-second-join merge work is explicit. The Pipeline
                // logs per-task timings; the inline log+timing wrappers
                // that used to bracket each substep here have moved into
                // MergeNodeTask alongside the substeps themselves.
                var mergeCtx = new Tasks.PipelineContext(
                    config, LogInfo, LogWarning, LogError);
                var mergePipeline = new Tasks.Pipeline(new Tasks.OspreyTask[]
                {
                    new MergeNode.MergeNodeTask(
                        perFileEntries, fullLibrary,
                        libraryById, perFileParquetPaths)
                });
                mergePipeline.Execute(mergeCtx);

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

        #region Stage 1: Library Loading
        #endregion

        #region Stage 2-4: Per-File Processing
        #endregion

        #region Stage 5: FDR Control

        #endregion

        #region Stage 8: Protein FDR

        #endregion

        #region Stage 9: Blib Output

        #endregion

        #region Utility Methods
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
