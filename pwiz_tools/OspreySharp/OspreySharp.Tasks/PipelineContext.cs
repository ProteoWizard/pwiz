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
using System.IO;
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
        /// Producer types whose <see cref="OspreyTask.Rehydrate"/> has already
        /// been driven by a <see cref="Demand{T}"/> first-touch this run.
        /// Reproduces the one-shot semantics of each task's former
        /// <c>_runOrHydrated</c> guard: a producer is materialized at most
        /// once, no matter how many consumers demand it.
        /// </summary>
        private readonly HashSet<Type> _materialized = new HashSet<Type>();

        /// <summary>
        /// The configuration parsed from CLI args and the input library.
        ///
        /// Mutation contract: fields that feed
        /// <see cref="SearchIdentity.SearchParameterHash"/> /
        /// <see cref="SearchIdentity.LibraryIdentityHash"/> /
        /// <see cref="SearchIdentity.ReconciliationParameterHash"/> must
        /// remain stable for the life of the run, so a worker can
        /// reproduce the same hash a straight-through invocation would
        /// stamp into its parquet footers. Pipeline-populated fields
        /// that do NOT feed those hashes (e.g. the worker-mode
        /// synthesis of <c>InputFiles</c> from <c>InputScores</c>) may be
        /// written once at pipeline entry. Run-time state that is not parsed
        /// config (e.g. file parallelism) lives on <see cref="RunPlan"/>
        /// instead. For per-file scratch that
        /// mutates hash-affecting fields (e.g. the MS2-calibrated
        /// FragmentTolerance), tasks must use
        /// <c>OspreyConfig.ShallowClone</c> so the mutation stays
        /// scoped to one file.
        /// </summary>
        public OspreyConfig Config { get; }

        /// <summary>
        /// Per-run, driver-owned pipeline state (not parsed config, not part
        /// of any cache / identity hash). See <see cref="RunPlan"/>.
        /// </summary>
        public RunPlan RunPlan { get; } = new RunPlan();

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
        /// The driver walks this list (running each that is
        /// <see cref="OspreyTask.IsIncluded"/> and not already valid on disk);
        /// tasks that need state from an upstream sibling reach it through
        /// <see cref="Demand{T}"/>.
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
        /// Look up a registered task by its concrete type. Internal lookup
        /// backing <see cref="Demand{T}"/>; consumers reach producers through
        /// <see cref="Demand{T}"/> (which materializes on first touch), not
        /// this raw accessor. Throws <see cref="UnknownTaskException"/> if the
        /// requested type is not in the pipeline; treat that as a
        /// programming defect at pipeline-construction time rather than a
        /// runtime condition to handle.
        /// </summary>
        private T GetTask<T>() where T : OspreyTask
        {
            if (_tasksByType.TryGetValue(typeof(T), out var task))
                return (T)task;
            throw new UnknownTaskException(typeof(T));
        }

        /// <summary>
        /// Resolve an upstream producer and ensure its state is materialized
        /// before returning it. The first consumer to demand a given producer
        /// triggers its <see cref="OspreyTask.Rehydrate"/>; subsequent demands
        /// return the same instance without re-materializing, via the
        /// <see cref="_materialized"/> one-shot guard. This is the
        /// lazy-rehydrate replacement for the <c>EnsureHydrated</c>-inside-getter
        /// pattern: callers ask the context for what they need and the context
        /// owns when the producer's state comes into being, rather than each
        /// accessor side-effecting a hydrate/run on read.
        ///
        /// A producer whose <see cref="OspreyTask.Run"/> already executed
        /// (driver ran it this pass) no-ops via its own one-shot guard. Each
        /// producer's <see cref="OspreyTask.Rehydrate"/> dispatches on its mode:
        /// it disk-loads worker-supplied state when present, else defers to
        /// <see cref="OspreyTask.Run"/> (whose per-file load picks up already-valid
        /// outputs on a straight-through resume).
        /// </summary>
        public T Demand<T>() where T : OspreyTask
        {
            var task = GetTask<T>();
            if (_materialized.Add(typeof(T)))
                task.Rehydrate(this);
            return task;
        }

        /// <summary>
        /// Driver-owned skip predicate: <c>true</c> when every file
        /// <paramref name="task"/> declares in <see cref="OspreyTask.Outputs"/>
        /// already exists on disk with a matching
        /// <see cref="OspreyTask.ValidityKey"/> sidecar — i.e. the task's work
        /// is durably present and need not be recomputed. A task that declares
        /// no outputs can never be skipped (returns <c>false</c>), matching the
        /// "purely-in-memory transformation" posture documented on
        /// <see cref="OspreyTask.Outputs"/>.
        ///
        /// Lifted verbatim from <c>AnalysisPipeline.IsTaskAlreadyDone</c> so the
        /// driver loop can ask the context "can this rehydrate instead of run?"
        /// rather than re-running the same outputs-valid check itself.
        /// </summary>
        public bool CanRehydrate(OspreyTask task)
        {
            var outputs = new List<string>(task.Outputs(this));
            if (outputs.Count == 0) return false;
            string key = task.ValidityKey(this);
            foreach (var output in outputs)
            {
                if (!File.Exists(output)) return false;
                if (!TaskValiditySidecar.IsValid(output, task.Name, key)) return false;
            }
            return true;
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
