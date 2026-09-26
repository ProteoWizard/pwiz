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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// What a task selected by <c>--task &lt;Name&gt;</c> tells the code that keys on the
    /// selection. The task classes live in <c>Osprey.Tasks</c>, above this assembly, and the
    /// selection is carried by <see cref="OspreyConfig.SelectedTask"/>, which lives here and
    /// is read on both sides of that boundary - by the exe that resolves the name and by the
    /// task library's own predicates. So the contract sits where both can see it, and Core
    /// still names no task.
    ///
    /// <para>Deliberately small. A task states what it IS - its name, whether it is a
    /// fan-out worker, what it consumes - and what its selection asks of the run. Where it
    /// sits in a pipeline, and which stages run alongside it, are the pipeline's facts, not
    /// the task's: they are answered by <see cref="OspreyConfig.Pipeline"/> and
    /// <see cref="OspreyConfig.Includes"/>, from the ordered stage list the selection was
    /// resolved against.</para>
    /// </summary>
    public interface ISelectableTask
    {
        /// <summary>
        /// The task's stable name: the <c>--task</c> value, the <c>[TASK]</c> log token and
        /// the <c>.osprey.task</c> sidecar stamp are all this one spelling.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A fan-out worker: sees ONE run at a time and never computes an experiment-wide
        /// score or writes an experiment-wide product, so its input count is not the cohort
        /// size and it stops before any join.
        /// </summary>
        bool IsPerFileWorker { get; }

        /// <summary>
        /// May hydrate ONE run's survivors at a time from the analysis-wide retained base_id
        /// summary instead of an all-runs bundle - the bounded Stage 6 route. True of a task
        /// that consumes the per-run loader and nothing else.
        /// </summary>
        bool HydratesPerRun { get; }

        /// <summary>
        /// Whether this stage is part of the run at all. True for every stage the pipeline
        /// always runs; an OPTIONAL stage answers from the option that asks for it, so with
        /// the option off it is excluded under every selection - not run, not stamped and not
        /// logged, which is what keeps every other artifact byte-identical. Selecting an
        /// optional stage by name implies its option (<see cref="ApplySelection"/>).
        /// </summary>
        bool IsEnabled(OspreyConfig config);

        /// <summary>
        /// Set the config flags this task implies once it has been selected - a stop
        /// boundary, an input gate, an output-mode flag the selector stands for. Called once,
        /// by <see cref="OspreyConfig.SelectTask"/>, after the command line has parsed.
        /// </summary>
        void ApplySelection(OspreyConfig config);

        /// <summary>
        /// What this task needs on the command line: null when the config satisfies it, or
        /// the error to report. Each task states its own requirements so the validation has
        /// no per-task switch to keep in step with the task list.
        /// </summary>
        string ValidateSelection(OspreyConfig config);

        /// <summary>
        /// What this task writes, for the startup settings echo, or null when the answer is
        /// the output blib like the full pipeline. A per-file worker names its per-file
        /// artifact here so the log does not read as if the blib were being rebuilt.
        /// </summary>
        string DescribeOutput(OspreyConfig config);
    }
}
