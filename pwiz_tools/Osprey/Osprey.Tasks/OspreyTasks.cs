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
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The one authoritative list of every task Osprey can run. The <c>--task</c> value list,
    /// the name lookup behind it, and the pipeline a run walks are all derived from
    /// <see cref="CreateAll"/>, so adding a task is one class deriving from
    /// <see cref="OspreyTask"/> plus one line here - nothing in the exe's argument model,
    /// its validation or the task library's predicates switches on a task name any more.
    /// (The <c>--task</c> help prose still describes the two selector-only tasks by name;
    /// a new selector gets a sentence there.)
    ///
    /// <para>A factory rather than a static singleton on purpose: tasks hold per-run state
    /// (the one-shot hydrate guards, the shared entry buffer), and the tests run several
    /// pipelines in one process. A run calls it once and shares the instances between the
    /// selection and the pipeline, which is what lets a task ask whether it IS the selected
    /// task by reference; the argument model calls it separately, at type init, for the
    /// value list alone.</para>
    /// </summary>
    public static class OspreyTasks
    {
        /// <summary>
        /// Every selectable task, in <c>--help</c> order: the data-staging step first, then
        /// the four canonical stages in execution order, then the render over completed
        /// products. A fresh set of instances per call.
        /// </summary>
        public static OspreyTask[] CreateAll()
        {
            return new OspreyTask[]
            {
                new SpectraCacheTask(),
                new PerFileScoringTask(),
                new FirstPassFdrTask(),
                new PerFileRescoreTask(),
                new SecondPassFdrTask(),
                new ModelDiagnosticsTask(),
            };
        }

        /// <summary>
        /// The task in <paramref name="allTasks"/> named <paramref name="name"/>, matched
        /// case-insensitively so the operator may type <c>firstpassfdr</c>, or null when no
        /// task has that name. The <c>Name</c> the match returns is the canonical spelling,
        /// which is what the log and the sidecars then carry.
        /// </summary>
        public static OspreyTask FindByName(IEnumerable<OspreyTask> allTasks, string name)
        {
            return allTasks.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The tasks a run walks, in execution order: the selected task alone when it
        /// <see cref="OspreyTask.RunsStandalone"/>, the canonical pipeline when it
        /// <see cref="OspreyTask.RunsCanonicalPipeline"/> (its membership predicates then
        /// decide what runs for the selection), and a refusal when it answers neither - a
        /// task that declares no pipeline must not silently run the whole analysis with
        /// itself never called. The selected task must be one of
        /// <paramref name="allTasks"/> - the same instance, not a namesake - so the pipeline
        /// and the selection agree by reference.
        /// </summary>
        public static OspreyTask[] PipelineFor(IReadOnlyList<OspreyTask> allTasks, ISelectableTask selected)
        {
            if (selected == null)
                return CanonicalPipeline(allTasks);
            var selectedTask = allTasks.FirstOrDefault(t => ReferenceEquals(t, selected));
            if (selectedTask == null)
                throw new ArgumentException(@"The selected task is not one of the tasks this run created.", nameof(selected));
            if (selectedTask.RunsStandalone)
                return new[] { selectedTask };
            if (selectedTask.RunsCanonicalPipeline)
                return CanonicalPipeline(allTasks);
            throw new InvalidOperationException(string.Format(
                @"--task {0} declares no pipeline to run: it is neither standalone nor a canonical-pipeline task.",
                selectedTask.Name));
        }

        /// <summary>
        /// The four canonical stages from <paramref name="allTasks"/>, in execution order:
        /// PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR.
        /// </summary>
        public static OspreyTask[] CanonicalPipeline(IEnumerable<OspreyTask> allTasks)
        {
            return allTasks.Where(t => t.InCanonicalPipeline).ToArray();
        }
    }
}
