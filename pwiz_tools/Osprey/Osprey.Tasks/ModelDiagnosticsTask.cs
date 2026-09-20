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
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// <c>--task ModelDiagnostics</c>: regenerate ONLY the <c>--model-diagnostics</c> HTML
    /// for a COMPLETED analysis, from that run's own outputs. Exists because judging a
    /// diagnostics change on a large cohort otherwise means re-running the whole search -
    /// 7 hours on the 82-file SEA-AD set - or accepting a stale page written by an older
    /// build.
    ///
    /// <para>A selector, not a stage. Like <see cref="SpectraCacheTask"/> it is not one of
    /// the four HPC fan-out nodes, but where that task runs a one-task pipeline of its own,
    /// this one runs the CANONICAL pipeline unchanged: Stages 1-5 rehydrate from their valid
    /// stamps, each FDR pass folds its own report from its completed artifacts, and every
    /// other artifact write is suppressed through <see cref="OspreyConfig.DiagnosticsOnly"/>.
    /// So this class is never in a pipeline list, its <see cref="IsIncluded"/> is false and
    /// its <see cref="Run"/> / <see cref="Rehydrate"/> are unreachable; what it owns is the
    /// name, the facts the pipeline asks of the selected task, and the two flags the
    /// selection implies. It validates like the full pipeline (the base default), because
    /// the caller re-issues the completed run's command line verbatim plus the selector.</para>
    ///
    /// <para>The non-degenerate version - a fifth canonical stage after SecondPassFDR that
    /// OWNS the report render, replacing the <c>DiagnosticsOnly</c> write-suppression
    /// threaded through the other tasks - is the natural follow-up, but it moves behavior
    /// and is deliberately not part of making the task list authoritative.</para>
    /// </summary>
    internal sealed class ModelDiagnosticsTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the tests and the report
        /// spell it from here.
        /// </summary>
        public const string TASK_NAME = @"ModelDiagnostics";

        public override string Name => TASK_NAME;

        /// <summary>
        /// Admitted to the per-run survivor loader for the same reason the rescore worker is:
        /// it consumes that loader and nothing else. Admitting it is what stops it falling to
        /// the all-runs bundle, which retains every run's survivors and grew 0.10 GB/file on a
        /// 446-run cohort - past a 63.7 GB box by file ~310, measured 2026-09-10.
        /// </summary>
        public override bool HydratesPerRun => true;

        /// <summary>
        /// Lets <c>SecondPassFDR</c> compute the pass-2 view, so it folds the same Stage 7
        /// join and must not be pushed back onto the resident pool.
        /// </summary>
        public override bool RunsStage7Join => true;

        /// <summary>
        /// The selector IS the request for the report; without the flag the run would
        /// recompute the pass-2 view and write nothing, a silent no-op. Sets none of the
        /// three membership flags: it is a member of every canonical task, exactly like the
        /// straight-through run, and suppresses artifact WRITES rather than membership.
        /// </summary>
        public override void ApplySelection(OspreyConfig config)
        {
            config.ModelDiagnostics = true;
            config.DiagnosticsOnly = true;
        }

        /// <summary>
        /// Naming the blib here would read as "the blib is being rebuilt", and an operator
        /// who then sees its timestamp unchanged concludes the run failed.
        /// </summary>
        public override string DescribeOutput(OspreyConfig config)
        {
            return string.Format(@"{0} (report only; no other artifact is written)",
                ModelDiagnosticsReport.ReportPath(config));
        }

        public override bool IsIncluded(PipelineContext ctx) => false;

        public override bool Run(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"ModelDiagnostics is a selector, never a pipeline task.");
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"ModelDiagnostics is a selector, never a pipeline task.");
        }
    }
}
