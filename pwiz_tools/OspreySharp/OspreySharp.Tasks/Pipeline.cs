/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Sequential driver for a list of <see cref="OspreyTask"/>
    /// instances. The pipeline is built once at the top of
    /// <c>AnalysisPipeline.Run</c>, then invoked by
    /// <see cref="Execute"/> which walks the tasks in declaration
    /// order and dispatches each <see cref="OspreyTask.Run"/>.
    /// </summary>
    public sealed class Pipeline
    {
        private readonly IReadOnlyList<OspreyTask> _tasks;

        /// <summary>
        /// Construct a pipeline that will execute <paramref name="tasks"/>
        /// in iteration order.
        /// </summary>
        public Pipeline(IEnumerable<OspreyTask> tasks)
        {
            if (tasks == null)
                throw new ArgumentNullException(nameof(tasks));
            _tasks = new List<OspreyTask>(tasks);
        }

        /// <summary>
        /// Tasks the pipeline will execute, in order. Exposed so callers
        /// can introspect the schedule (logging, validity-key checks in
        /// the Phase B resume layer).
        /// </summary>
        public IReadOnlyList<OspreyTask> Tasks => _tasks;

        /// <summary>
        /// Run each task in order against <paramref name="ctx"/>.
        /// Per-task wall time is logged via
        /// <see cref="PipelineContext.LogInfo"/>. Stops at the first
        /// task whose <see cref="OspreyTask.Run"/> returns <c>false</c>
        /// and propagates that signal back to the caller. The caller
        /// is responsible for inspecting <see cref="PipelineContext.ExitCode"/>
        /// to choose its own return code.
        /// </summary>
        /// <returns>
        /// <c>true</c> if every task ran to completion; <c>false</c> if
        /// any task short-circuited the pipeline.
        /// </returns>
        public bool Execute(PipelineContext ctx)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));
            foreach (var task in _tasks)
            {
                var sw = Stopwatch.StartNew();
                ctx.LogInfo(string.Format(@"[task] {0}: starting", task.Name));
                bool keepGoing = task.Run(ctx);
                sw.Stop();
                ctx.LogInfo(string.Format(@"[task] {0}: done ({1:F1}s)",
                    task.Name, sw.Elapsed.TotalSeconds));
                if (!keepGoing)
                    return false;
            }
            return true;
        }
    }
}
