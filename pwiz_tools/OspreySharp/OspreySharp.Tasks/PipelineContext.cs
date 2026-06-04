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
    /// in the current pipeline. Downstream tasks read upstream state as
    /// typed byproducts through <see cref="Get{TInfo}"/> rather than
    /// receiving constructor parameters; a <see cref="Get{TInfo}"/> miss
    /// lazily materializes the producing task (via <see cref="Demand{T}"/>),
    /// which lets producers transparently rehydrate their outputs from
    /// sibling artifacts (e.g. the worker entry path) when their
    /// <see cref="OspreyTask.Run"/> never ran.
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
        /// Task types whose state is already in memory this run -- either the
        /// driver ran them (it calls <see cref="MarkMaterialized"/> after
        /// <see cref="OspreyTask.Run"/>) or a <see cref="Demand{T}"/> /
        /// <see cref="Get{TInfo}"/> first-touch drove their
        /// <see cref="OspreyTask.Rehydrate"/>. A task is materialized at most
        /// once, no matter how many consumers reach it; this single guard
        /// replaces the per-task <c>_runOrHydrated</c> field.
        /// </summary>
        private readonly HashSet<Type> _materialized = new HashSet<Type>();

        /// <summary>
        /// Typed byproduct cache: state that one task computes and one or more
        /// downstream tasks read, keyed by the byproduct's purpose type. Modeled
        /// directly on Skyline's
        /// <c>pwiz_tools/Skyline/Model/Results/Scoring/IPeakScoringModel.cs</c>
        /// <c>PeakScoringContext</c> (its <c>AddInfo&lt;TInfo&gt;</c> /
        /// <c>TryGetInfo&lt;TInfo&gt;</c> pair): a producer publishes a value once
        /// and consumers retrieve it by type without naming the producer. Publish
        /// is once-only (a second publish of the same type is a programming
        /// defect). A consumer reaching for a byproduct whose producer has not yet
        /// run materializes it lazily through <see cref="Get{TInfo}"/>, which
        /// demands the registered producer (see <see cref="_producerByByproduct"/>).
        /// </summary>
        private readonly Dictionary<Type, object> _byproducts = new Dictionary<Type, object>();

        /// <summary>
        /// Maps each byproduct purpose type to the concrete task that publishes
        /// it, built once at construction from each task's
        /// <see cref="OspreyTask.Publishes"/>. This is the single registration
        /// point that lets <see cref="Get{TInfo}"/> lazily materialize a skipped
        /// producer on a cache miss, replacing the former pattern of every
        /// consumer naming the producer task at its call site. A byproduct that
        /// is a shared mutable buffer (in-place mutated by several tasks rather
        /// than published once and read) is deliberately NOT registered here: its
        /// materialization is a task-ordering dependency the consumer expresses
        /// by demanding the correct mutator directly, not a value-presence miss.
        /// </summary>
        private readonly Dictionary<Type, Type> _producerByByproduct = new Dictionary<Type, Type>();

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

            // Invert each producer's declared byproducts into the
            // byproduct -> producer registry that Get{TInfo} resolves a cache
            // miss through. A byproduct with two producers is a definition
            // defect (the registry could not pick which task to materialize),
            // so fail fast at construction rather than silently lose one.
            foreach (var task in list)
            {
                foreach (var byproductType in task.Publishes)
                {
                    if (_producerByByproduct.ContainsKey(byproductType))
                        throw new ArgumentException(string.Format(
                            @"Byproduct type '{0}' is published by more than one task; each registered byproduct must have a single producer.",
                            byproductType.FullName), nameof(tasks));
                    _producerByByproduct.Add(byproductType, task.GetType());
                }
            }
            Tasks = list;
        }

        public void LogInfo(string message) { _logInfo(message); }
        public void LogWarning(string message) { _logWarning(message); }
        public void LogError(string message) { _logError(message); }

        /// <summary>
        /// Resolve a registered task by its runtime <see cref="Type"/>, optionally
        /// materializing it. Shared core behind the generic <see cref="Demand{T}"/>
        /// and the registry-driven lazy materialization in
        /// <see cref="Get{TInfo}"/> (which knows the producer only as a
        /// <see cref="Type"/> from <see cref="_producerByByproduct"/>, so it cannot
        /// use the generic overload). When <paramref name="materialize"/> is true,
        /// the producer's <see cref="OspreyTask.Rehydrate"/> is driven at most once
        /// via the <see cref="_materialized"/> one-shot guard.
        /// </summary>
        private OspreyTask DemandByType(Type taskType, bool materialize)
        {
            if (!_tasksByType.TryGetValue(taskType, out var task))
                throw new UnknownTaskException(taskType);
            if (materialize && _materialized.Add(taskType))
                task.Rehydrate(this);
            return task;
        }

        /// <summary>
        /// Record that the driver has run <paramref name="task"/>, so a later
        /// <see cref="Demand{T}"/> / <see cref="Get{TInfo}"/> for it returns the
        /// already-computed state instead of driving <see cref="OspreyTask.Rehydrate"/>.
        /// Called by <c>AnalysisPipeline.RunTask</c> after each
        /// <see cref="OspreyTask.Run"/>. This is what lets tasks drop their former
        /// per-instance <c>_runOrHydrated</c> guard: the context's
        /// <see cref="_materialized"/> set now coordinates the driver-Run path and
        /// the lazy-Rehydrate path with a single source of truth.
        /// </summary>
        public void MarkMaterialized(OspreyTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            _materialized.Add(task.GetType());
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
            return (T)DemandByType(typeof(T), materialize: true);
        }

        /// <summary>
        /// Publish a byproduct value for downstream tasks, keyed by its purpose
        /// type <typeparamref name="TInfo"/>. Once-only: publishing the same type
        /// twice in a run is a programming defect (two producers, or a producer
        /// running twice) and throws. The <c>AddInfo&lt;TInfo&gt;</c> counterpart
        /// of Skyline's <c>PeakScoringContext</c>.
        /// </summary>
        public void Publish<TInfo>(TInfo info)
        {
            _byproducts.Add(typeof(TInfo), info);
        }

        /// <summary>
        /// Read a byproduct value if it has been published, without triggering
        /// any producer. Pure cache lookup -- the <c>TryGetInfo&lt;TInfo&gt;</c>
        /// counterpart of Skyline's <c>PeakScoringContext</c>. Use
        /// <see cref="Get{TInfo}"/> when a miss should lazily materialize the
        /// registered producer instead of returning <c>false</c>.
        /// </summary>
        public bool TryGet<TInfo>(out TInfo info)
        {
            if (_byproducts.TryGetValue(typeof(TInfo), out var obj))
            {
                info = (TInfo)obj;
                return true;
            }
            info = default;
            return false;
        }

        /// <summary>
        /// Retrieve a published byproduct, lazily materializing its producer on a
        /// miss. If <typeparamref name="TInfo"/> is not yet in the cache, the
        /// registered producer (<see cref="_producerByByproduct"/>) is demanded --
        /// its <see cref="OspreyTask.Rehydrate"/> runs and publishes the value --
        /// and the cache is read again. This is the public dataflow surface that
        /// replaces consumers reaching through a named producer task's getter:
        /// the consumer asks the context for the value by type and the context
        /// owns when (and by which task) it comes into being.
        ///
        /// Throws <see cref="UnknownByproductException"/> if the type has no
        /// registered producer, or if the producer ran but did not publish it --
        /// both programming defects (a missing <see cref="OspreyTask.Publishes"/>
        /// registration, or a producer path that forgot to publish), surfaced
        /// loudly rather than returning a silent default.
        /// </summary>
        public TInfo Get<TInfo>()
        {
            if (TryGet(out TInfo info))
                return info;
            if (_producerByByproduct.TryGetValue(typeof(TInfo), out var producerType))
            {
                DemandByType(producerType, materialize: true);
                if (TryGet(out info))
                    return info;
            }
            throw new UnknownByproductException(typeof(TInfo));
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
    /// Thrown by <see cref="PipelineContext.Demand{T}"/> (and the
    /// registry-driven materialization in <see cref="PipelineContext.Get{TInfo}"/>)
    /// when a task asks for an upstream producer that was not added to the
    /// pipeline at construction time. This is always a programming defect (the
    /// pipeline definition is missing the producer); fail fast and hard so it
    /// surfaces in testing rather than at runtime.
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

    /// <summary>
    /// Thrown by <see cref="PipelineContext.Get{TInfo}"/> when a byproduct type
    /// has no registered producer, or when its producer ran but did not publish
    /// the value. Both are programming defects -- a missing
    /// <see cref="OspreyTask.Publishes"/> registration, or a producer code path
    /// that neglected to <see cref="PipelineContext.Publish{TInfo}"/> -- and are
    /// surfaced loudly rather than degrading to a silent default value.
    /// </summary>
    public sealed class UnknownByproductException : Exception
    {
        public Type RequestedType { get; }

        public UnknownByproductException(Type requestedType)
            : base(string.Format(@"Byproduct type '{0}' has no registered producer, or its producer did not publish it.",
                requestedType?.FullName))
        {
            RequestedType = requestedType;
        }
    }
}
