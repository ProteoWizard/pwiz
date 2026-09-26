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
    /// The tasks a run can select and the pipeline they compose: two explicit lists.
    /// <see cref="All"/> is every <c>--task</c> value, in <c>--help</c> order; the
    /// <c>--task</c> value list, the name lookup behind it and the reflection guard in the
    /// tests all derive from it. <see cref="Pipeline"/> is the canonical stages in execution
    /// order, and it is the pipeline that says which tasks are its stages - a task does not
    /// declare that of itself. A task that is selectable but not a stage names, here, the
    /// pipeline its selection runs. Adding a stage is one class deriving from
    /// <see cref="OspreyTask"/> plus its place in the two lists; nothing in the exe's argument
    /// model, its validation or the task library's predicates switches on a task name.
    /// (The <c>--task</c> help prose still describes the two selector-only tasks by name; a
    /// new selector gets a sentence there.)
    ///
    /// <para>A fresh set per run rather than a static singleton: tasks hold per-run state
    /// (the one-shot hydrate guards, the shared entry buffer), and the tests run several
    /// pipelines in one process. A run creates ONE set and shares its instances between the
    /// selection and the pipeline it walks, which is what lets a stage ask whether it IS the
    /// selection by reference; the argument model creates a set of its own, at type init,
    /// for the value list alone.</para>
    ///
    /// <para>One pipeline today. A second - selectable by a <c>--pipeline &lt;name&gt;</c> -
    /// would be another ordered list declared beside this one, not another fact on the
    /// tasks.</para>
    /// </summary>
    public sealed class OspreyTasks
    {
        private readonly IReadOnlyDictionary<OspreyTask, IReadOnlyList<OspreyTask>> _pipelineBySelector;

        /// <summary>
        /// Every selectable task, in <c>--help</c> order: the data-staging step first, then
        /// the canonical stages in execution order, then the render over completed products.
        /// </summary>
        public IReadOnlyList<OspreyTask> All { get; }

        /// <summary>
        /// The canonical pipeline: the stages a full run walks, in execution order
        /// (PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR), alternating
        /// fan-out and join, then the optional TrainingExport fan-out, included only when
        /// <c>--training-export</c> asks for it.
        /// </summary>
        public IReadOnlyList<OspreyTask> Pipeline { get; }

        private OspreyTasks(IReadOnlyList<OspreyTask> all, IReadOnlyList<OspreyTask> pipeline,
            IReadOnlyDictionary<OspreyTask, IReadOnlyList<OspreyTask>> pipelineBySelector)
        {
            All = all;
            Pipeline = pipeline;
            _pipelineBySelector = pipelineBySelector;
            // The lists are declared by hand, so check them once at construction rather than
            // letting a slip surface as "unknown task" or as a selection that runs nothing:
            // every task listed once, every stage listed, every selector-only task given the
            // pipeline it runs.
            if (all.Distinct().Count() != all.Count)
                throw new InvalidOperationException(@"A task is listed more than once.");
            foreach (var stage in pipeline)
            {
                if (!all.Contains(stage))
                    throw new InvalidOperationException(string.Format(@"Stage {0} is not in the task list.", stage.Name));
            }
            foreach (var task in all.Where(t => !pipeline.Contains(t)))
            {
                if (!pipelineBySelector.ContainsKey(task))
                    throw new InvalidOperationException(string.Format(@"--task {0} is not a stage and names no pipeline to run.", task.Name));
            }
            // A selector's pipeline is built from THIS set's instances - the membership rule
            // compares by reference, so a fresh instance in a declared pipeline would be a
            // namesake no selection ever matches - and a stage declares no pipeline of its own
            // (PipelineFor answers the canonical one for a stage before consulting this map).
            foreach (var kv in pipelineBySelector)
            {
                if (!all.Contains(kv.Key) || pipeline.Contains(kv.Key))
                    throw new InvalidOperationException(string.Format(@"--task {0} declares a pipeline but is a stage or is not listed.", kv.Key.Name));
                if (kv.Value.Any(t => !all.Contains(t)))
                    throw new InvalidOperationException(string.Format(@"--task {0}'s pipeline uses a task instance that is not in the task list.", kv.Key.Name));
            }
        }

        /// <summary>
        /// A fresh set of instances: the seven tasks, the canonical pipeline over five of them
        /// (the fifth optional), and what the other two run when selected.
        /// </summary>
        public static OspreyTasks Create()
        {
            var spectraCache = new SpectraCacheTask();
            var perFileScoring = new PerFileScoringTask();
            var firstPassFdr = new FirstPassFdrTask();
            var perFileRescore = new PerFileRescoreTask();
            var secondPassFdr = new SecondPassFdrTask();
            var trainingExport = new TrainingExportTask();
            var modelDiagnostics = new ModelDiagnosticsTask();

            // The training export is the fifth stage and an OPTIONAL one: it walks after the
            // final join like any stage, and OspreyConfig.Includes leaves it out of every run
            // whose --training-export is off (TrainingExportTask.IsEnabled).
            var pipeline = new OspreyTask[] { perFileScoring, firstPassFdr, perFileRescore, secondPassFdr, trainingExport };
            return new OspreyTasks(
                new OspreyTask[] { spectraCache, perFileScoring, firstPassFdr, perFileRescore, secondPassFdr, trainingExport, modelDiagnostics },
                pipeline,
                new Dictionary<OspreyTask, IReadOnlyList<OspreyTask>>
                {
                    // SpectraCache stages data rather than analyzing it: a one-task pipeline
                    // of its own, so the canonical pipeline's membership rules stay about the
                    // analysis itself. ModelDiagnostics is a render over a completed analysis:
                    // it runs the canonical stages, which rehydrate from their stamps and fold
                    // the report with every other write suppressed.
                    { spectraCache, new OspreyTask[] { spectraCache } },
                    { modelDiagnostics, pipeline },
                });
        }

        /// <summary>
        /// The task named <paramref name="name"/>, matched case-insensitively so the operator
        /// may type <c>firstpassfdr</c>, or null when no task has that name. The <c>Name</c>
        /// the match returns is the canonical spelling, which is what the log and the
        /// sidecars then carry.
        /// </summary>
        public OspreyTask FindByName(string name)
        {
            return All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The stages a selection walks, in execution order: the canonical pipeline when
        /// nothing is selected or a stage of it is (the selected stage is then the only one
        /// included - see <see cref="OspreyConfig.Includes"/>), and the pipeline this set
        /// declares for a selector-only task. The selection must be one of this set's
        /// instances, not a namesake from another set, so the pipeline and the selection
        /// agree by reference.
        /// </summary>
        public IReadOnlyList<OspreyTask> PipelineFor(OspreyTask selected)
        {
            if (selected == null || Pipeline.Contains(selected))
                return Pipeline;
            if (_pipelineBySelector.TryGetValue(selected, out var pipeline))
                return pipeline;
            throw new ArgumentException(@"The selected task is not one of the tasks this run created.", nameof(selected));
        }
    }
}
