/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.Common.CommonResources;

namespace pwiz.Common.Pipeline
{
    /// <summary>
    /// Walks a pipeline of <see cref="PipelineTask"/>s in order and runs each included task
    /// whose outputs are not already valid on disk. Included tasks whose outputs carry a
    /// current stamp (<see cref="TaskContext.CanRehydrate"/>) are not run; their state
    /// lazy-rehydrates through <see cref="TaskContext.Demand{T}"/> when a running task reaches
    /// for it. A task returning <c>false</c> stops the walk: with a failure exit code the run
    /// ends there; with exit code zero the task stopped on purpose (a per-file worker's
    /// boundary, an empty result), so the run is complete. A host derives from this to put its
    /// own text and measurements on the start, done and skip events.
    /// </summary>
    public class PipelineDriver
    {
        /// <summary>
        /// Run <paramref name="pipeline"/> against <paramref name="ctx"/>. <paramref name="includes"/>
        /// says which tasks this run walks (null: all of them). Returns zero when the walk
        /// finished or stopped on purpose, else the exit code the stopping task set.
        /// </summary>
        public int Run(IEnumerable<PipelineTask> pipeline, TaskContext ctx, Func<PipelineTask, bool> includes = null)
        {
            foreach (var task in pipeline)
            {
                if (includes != null && !includes(task))
                    continue;

                if (ctx.CanRehydrate(task))
                {
                    OnSkipped(task, ctx);
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                OnStarting(task, ctx);
                bool keepGoing = task.Run(ctx);
                // The driver has now run this task, so its state is in memory: mark it
                // materialized so a later Demand/Get by a downstream task returns the computed
                // state instead of driving Rehydrate.
                ctx.MarkMaterialized(task);
                stopwatch.Stop();
                OnDone(task, ctx, stopwatch.Elapsed);
                if (!keepGoing)
                    return ctx.ExitCode;
            }
            return 0;
        }

        /// <summary>A task whose outputs are current was not run.</summary>
        protected virtual void OnSkipped(PipelineTask task, TaskContext ctx)
        {
            ctx.LogInfo(string.Format(MessageResources.PipelineDriver_OnSkipped__0___skipping__its_outputs_are_current_, task.Name));
        }

        /// <summary>A task is about to run.</summary>
        protected virtual void OnStarting(PipelineTask task, TaskContext ctx)
        {
            ctx.LogInfo(string.Format(MessageResources.PipelineDriver_OnStarting__0___starting, task.Name));
        }

        /// <summary>A task has returned, after <paramref name="elapsed"/>.</summary>
        protected virtual void OnDone(PipelineTask task, TaskContext ctx, TimeSpan elapsed)
        {
            ctx.LogInfo(string.Format(MessageResources.PipelineDriver_OnDone__0___done___1_F1_s_, task.Name, elapsed.TotalSeconds));
        }
    }
}
