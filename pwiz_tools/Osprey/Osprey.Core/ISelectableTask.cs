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
    /// still names no task: every member is a fact about the selected task, answered by the
    /// task itself, and a task added later answers them without anything here changing.
    ///
    /// <para>Each fact used to be a switch over an enum of task names, one per consumer,
    /// which is how <c>--task ModelDiagnostics</c> was routed down the per-run rescore path:
    /// an exclusion list cannot know about a sixth member. Asked of the task, a fact fails
    /// CLOSED - the defaults on <c>OspreyTask</c> admit a new task to nothing until its author
    /// decides otherwise.</para>
    /// </summary>
    public interface ISelectableTask
    {
        /// <summary>
        /// The task's stable name: the <c>--task</c> value, the <c>[TASK]</c> log token and
        /// the <c>.osprey.task</c> sidecar stamp are all this one spelling.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// May hydrate ONE run's survivors at a time from the analysis-wide retained base_id
        /// summary instead of an all-runs bundle - the bounded Stage 6 route. True of the
        /// rescore worker and of the diagnostics render, which consume the per-run loader and
        /// nothing else.
        /// </summary>
        bool HydratesPerRun { get; }

        /// <summary>
        /// Starts AFTER Stage 4: handed a directory of per-run artifacts rather than spectra,
        /// so per-file scoring does not run for it and its upstream state materializes
        /// through the disk load instead. True of the two joins and the rescore worker.
        /// </summary>
        bool StartsAfterPerFileScoring { get; }

        /// <summary>
        /// Reads each run's rows from the Stage 6 <c>.scores-reconciled.parquet</c> rather
        /// than the Stage 4 <c>.scores.parquet</c>. A property of the task, not of what is on
        /// disk: a re-run over a completed directory must not hand a first pass the survivor
        /// subset just because the reconciled sibling exists.
        /// </summary>
        bool ReadsReconciledScores { get; }

        /// <summary>
        /// This process runs Stage 7's join, so a per-run source published for that join is
        /// actually folded by something. A task that stops earlier must answer false, or it
        /// publishes for a consumer that never arrives.
        /// </summary>
        bool RunsStage7Join { get; }

        /// <summary>
        /// One of the per-file HPC workers: sees ONE input and never computes an
        /// experiment-wide score, so its input count is not the cohort size.
        /// </summary>
        bool IsPerFileWorker { get; }

        /// <summary>
        /// Set the config flags this task implies once it has been selected - the
        /// pipeline-membership flags the tasks' <c>IsIncluded</c> predicates read, and any
        /// output-mode flag the selector stands for. Called once, by
        /// <see cref="OspreyConfig.SelectTask"/>, after the command line has parsed.
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
