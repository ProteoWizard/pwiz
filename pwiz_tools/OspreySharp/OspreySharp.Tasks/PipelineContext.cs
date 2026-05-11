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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Shared state passed between <see cref="OspreyTask"/> steps.
    ///
    /// Carries the <see cref="OspreyConfig"/>, logging callbacks, and
    /// the registry of <see cref="OspreyTask"/> instances participating
    /// in the current pipeline. Downstream tasks query upstream tasks
    /// through <see cref="GetTask{T}"/> and read their typed accessor
    /// methods rather than receiving constructor parameters. Each
    /// producer task owns the state it computes; consumers reach it
    /// through the producer, which lets producers transparently
    /// rehydrate their outputs from sibling artifacts (e.g. the worker
    /// entry path) when their <see cref="OspreyTask.Run"/> never ran.
    ///
    /// The context is constructed once at the top of
    /// <c>AnalysisPipeline.Run</c> (or <c>RescoreWorker.Run</c>) and
    /// lives for the duration of the pipeline execution.
    /// </summary>
    public sealed class PipelineContext
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private readonly Dictionary<Type, OspreyTask> _tasksByType;

        /// <summary>
        /// The configuration parsed from CLI args and the input library.
        /// Tasks may shallow-clone the config for per-file scratch
        /// (mirroring <c>OspreyConfig.ShallowClone</c>) but must not
        /// mutate the outer instance — downstream tasks rely on its
        /// post-CLI-parse state for hash-stable
        /// <see cref="OspreyConfig.SearchParameterHash"/> computations.
        /// </summary>
        public OspreyConfig Config { get; }

        /// <summary>
        /// Process exit code requested by a task that has returned
        /// <c>false</c> from <see cref="OspreyTask.Run"/>. Defaults
        /// to 0; tasks that short-circuit the pipeline because of an
        /// error must set this to a non-zero value before returning.
        /// The caller of <see cref="OspreyTask.Run"/> reads this to
        /// choose its own return code.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// The tasks participating in this pipeline, in execution order.
        /// The driver walks this list; tasks that need state from an
        /// upstream sibling look it up by type via <see cref="GetTask{T}"/>.
        /// </summary>
        public IReadOnlyList<OspreyTask> Tasks { get; }

        public PipelineContext(OspreyConfig config,
            IEnumerable<OspreyTask> tasks,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));
            _logInfo = logInfo ?? (_ => { });
            _logWarning = logWarning ?? (_ => { });
            _logError = logError ?? (_ => { });

            var list = new List<OspreyTask>(tasks);
            _tasksByType = new Dictionary<Type, OspreyTask>(list.Count);
            foreach (var task in list)
            {
                if (task == null)
                    throw new ArgumentException(@"Pipeline task list contains a null entry.", nameof(tasks));
                _tasksByType.Add(task.GetType(), task);
            }
            Tasks = list;
        }

        public void LogInfo(string message) { _logInfo(message); }
        public void LogWarning(string message) { _logWarning(message); }
        public void LogError(string message) { _logError(message); }

        /// <summary>
        /// Look up a task by its concrete type. Used by a task's
        /// <see cref="OspreyTask.Run"/> implementation to reach the
        /// typed accessor methods on an upstream producer
        /// (e.g. <c>ctx.GetTask&lt;FirstJoinTask&gt;().GetReconciliationActions()</c>).
        /// Throws <see cref="UnknownTaskException"/> if the requested
        /// type is not in the pipeline; treat that as a programming
        /// defect at pipeline-construction time rather than a runtime
        /// condition to handle.
        /// </summary>
        public T GetTask<T>() where T : OspreyTask
        {
            if (_tasksByType.TryGetValue(typeof(T), out var task))
                return (T)task;
            throw new UnknownTaskException(typeof(T));
        }
    }

    /// <summary>
    /// Thrown by <see cref="PipelineContext.GetTask{T}"/> when a task
    /// asks for an upstream producer that was not added to the pipeline
    /// at construction time. This is always a programming defect (the
    /// pipeline definition is missing the producer); fail fast and hard
    /// so it surfaces in testing rather than at runtime.
    /// </summary>
    public sealed class UnknownTaskException : Exception
    {
        public Type RequestedType { get; }

        public UnknownTaskException(Type requestedType)
            : base(string.Format(@"Task type '{0}' is not registered in the current pipeline.",
                requestedType?.FullName))
        {
            RequestedType = requestedType;
        }
    }
}
