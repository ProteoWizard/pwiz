/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
    /// <c>--task TrainingExport</c>: a selector, never a pipeline stage, the counterpart of
    /// <see cref="ModelDiagnosticsTask"/>. Selecting it asks for the training export
    /// (<c>--training-export</c>, <see cref="TrainingExportWriter"/>) and runs the canonical
    /// pipeline, where each run's <c>&lt;stem&gt;.training.parquet</c> is a declared output of
    /// <see cref="PerFileRescoreTask"/>. On a finished analysis every other task therefore
    /// rehydrates from its stamps and PerFileRescoring writes only the missing exports, from
    /// each run's own artifacts with no re-scoring; on an unfinished one the analysis runs with
    /// the export, exactly as if the flag had been given up front. It adds no node type to an
    /// HPC chain: the export rides the per-file PerFileRescoring nodes (P17). It names no output
    /// of its own either (the base <see cref="OspreyTask.DescribeOutput"/>), so the startup
    /// lines read as they do for <c>--training-export</c> alone: the blib, and the training
    /// export line that names the parquets.
    /// </summary>
    internal sealed class TrainingExportTask : OspreyTask
    {
        /// <summary>
        /// This task's name, as a constant so the CLI selector, the tests and the log spell it
        /// from here.
        /// </summary>
        public const string TASK_NAME = @"TrainingExport";

        public override string Name => TASK_NAME;

        /// <summary>
        /// Admitted to the per-run survivor loader for the same reason ModelDiagnostics is: a
        /// finished analysis walked by this selector must not fall to the all-runs bundle.
        /// </summary>
        public override bool HydratesPerRun => true;

        /// <summary>
        /// The selector IS the request for the export. Sets no stop boundary: every canonical
        /// stage is included, exactly like the straight-through run.
        /// </summary>
        public override void ApplySelection(OspreyConfig config)
        {
            config.TrainingExport.Enabled = true;
        }

        public override bool Run(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"TrainingExport is a selector, never a pipeline task.");
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            throw new InvalidOperationException(@"TrainingExport is a selector, never a pipeline task.");
        }
    }
}
