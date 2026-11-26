/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// <c>--task FirstPass</c>: a selector, never a pipeline stage, for local iteration on the
    /// first-pass model. It runs <see cref="PerFileScoringTask"/> and then
    /// <see cref="FirstPassFdrTask"/>, and stops where <c>--task FirstPassFDR</c> stops. The
    /// per-run models train in PerFileScoring, so a directory
    /// that holds each run's features checkpoint (<c>&lt;stem&gt;.features.parquet</c>) and
    /// calibration - Stages 1-4, linked from an earlier run - trains every model again from the
    /// checkpoints and reduces them, without scoring a run from its spectra. An HPC chain never
    /// needs it: there the two tasks are separate nodes (doc 00, "In flight").
    /// </summary>
    internal sealed class FirstPassTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the tests and the log spell it
        /// from here.
        /// </summary>
        public const string TASK_NAME = OspreyTaskNames.FIRST_PASS;

        public override string Name => TASK_NAME;

        /// <summary>
        /// Stops after the first pass and its cross-run consensus, as <c>--task FirstPassFDR</c>
        /// does.
        /// </summary>
        public override void ApplySelection(OspreyConfig config)
        {
            config.StopAfterStage5 = true;
        }

        public override bool Run(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"FirstPass is a selector, never a pipeline task.");
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"FirstPass is a selector, never a pipeline task.");
        }
    }
}
