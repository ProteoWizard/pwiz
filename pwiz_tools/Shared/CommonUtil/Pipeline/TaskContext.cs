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
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Pipeline
{
    /// <summary>
    /// Shared state passed between <see cref="PipelineTask"/> steps: the run's build version,
    /// its exit code, logging, the registry of task instances in the pipeline, and the typed
    /// byproduct cache through which downstream tasks read upstream state. A consumer reads a
    /// byproduct by type (<see cref="Get{TInfo}"/>) rather than naming its producer; a miss
    /// lazily materializes the producing task (<see cref="Demand{T}"/>), which lets producers
    /// rehydrate their outputs from the files on disk when their <see cref="PipelineTask.Run"/>
    /// never ran. Modeled directly on Skyline's <c>PeakScoringContext</c> (its
    /// <c>AddInfo</c> / <c>TryGetInfo</c> pair) and extracted from Osprey's pipeline context,
    /// which derives from this and adds the search configuration.
    ///
    /// <para>Constructed once at the top of a run and alive for the pipeline's duration. A host
    /// supplies the build version its stamps carry and the reader that finds the stamp inside
    /// each of its artifact formats.</para>
    /// </summary>
    public abstract class TaskContext
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private readonly Func<string, ArtifactStamp> _readStamp;
        private readonly Dictionary<Type, PipelineTask> _tasksByType;

        /// <summary>
        /// Task types whose state is already in memory this run: either the driver ran them (it
        /// calls <see cref="MarkMaterialized"/> after <see cref="PipelineTask.Run"/>) or a
        /// <see cref="Demand{T}"/> / <see cref="Get{TInfo}"/> first touch drove their
        /// <see cref="PipelineTask.Rehydrate"/>. A task is materialized at most once, no matter
        /// how many consumers reach it.
        /// </summary>
        private readonly HashSet<Type> _materialized = new HashSet<Type>();

        /// <summary>
        /// Typed byproduct cache: state that one task computes and one or more downstream tasks
        /// read, keyed by the byproduct's purpose type. Publish is once-only; a second publish of
        /// the same type is a programming defect.
        /// </summary>
        private readonly Dictionary<Type, object> _byproducts = new Dictionary<Type, object>();

        /// <summary>
        /// Maps each byproduct type to the concrete task that publishes it, built once at
        /// construction from each task's <see cref="PipelineTask.Publishes"/>. Every registered
        /// byproduct has exactly one producer (the constructor throws on a duplicate).
        /// </summary>
        private readonly Dictionary<Type, Type> _producerByByproduct = new Dictionary<Type, Type>();

#if DEBUG
        /// <summary>
        /// DEBUG-only record of the byproduct types read through <see cref="TryGet{TInfo}"/>, for
        /// a host's guard over a shared mutable buffer republished at successive milestones (see
        /// <see cref="WasConsumed"/>).
        /// </summary>
        private readonly HashSet<Type> _consumedByproducts = new HashSet<Type>();
#endif

        /// <param name="tasks">Every task instance participating in the pipeline, each of a
        /// distinct type.</param>
        /// <param name="version">The build version every stamp this context issues carries, and
        /// the one a reused artifact's stamp must match.</param>
        /// <param name="readStamp">Reads the stamp inside an artifact at a path, whatever its
        /// format, or null when the file is missing, unreadable or unstamped; never throws.</param>
        /// <param name="logInfo">Sink for a line of prose; null discards.</param>
        /// <param name="logWarning">Sink for a warning line; null discards.</param>
        /// <param name="logError">Sink for an error line; null discards.</param>
        protected TaskContext(IEnumerable<PipelineTask> tasks, string version, Func<string, ArtifactStamp> readStamp,
            Action<string> logInfo, Action<string> logWarning, Action<string> logError)
        {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            _readStamp = readStamp ?? throw new ArgumentNullException(nameof(readStamp));
            _logInfo = logInfo ?? (_ => { });
            _logWarning = logWarning ?? (_ => { });
            _logError = logError ?? (_ => { });

            var list = new List<PipelineTask>(tasks);
            _tasksByType = new Dictionary<Type, PipelineTask>(list.Count);
            foreach (var task in list)
            {
                if (task == null)
                    throw new ArgumentException(@"Pipeline task list contains a null entry.", nameof(tasks));
                _tasksByType.Add(task.GetType(), task);
            }

            // Invert each producer's declared byproducts into the byproduct -> producer registry
            // that Get{TInfo} resolves a cache miss through. A byproduct with two producers is a
            // definition defect (the registry could not pick which task to materialize), so fail
            // fast at construction rather than silently lose one.
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
        }

        /// <summary>The build version this run's stamps carry (see <see cref="Stamp"/>).</summary>
        public string Version { get; }

        /// <summary>
        /// Exit code a task requests when it returns <c>false</c> from <see cref="PipelineTask.Run"/>
        /// to stop the pipeline; zero means the stop was a success (a worker's boundary).
        /// </summary>
        public int ExitCode { get; set; }

        public void LogInfo(string message) { _logInfo(message); }
        public void LogWarning(string message) { _logWarning(message); }
        public void LogError(string message) { _logError(message); }

        /// <summary>The stamp a writer embeds in an artifact it is producing now, for <paramref name="task"/> with <paramref name="key"/>.</summary>
        public ArtifactStamp Stamp(string task, string key)
        {
            return new ArtifactStamp(task, Version, key);
        }

        /// <summary>The stamp inside the artifact at <paramref name="path"/>, or null (see the constructor's reader).</summary>
        public ArtifactStamp ReadStamp(string path)
        {
            return _readStamp(path);
        }

        /// <summary>
        /// True when the artifact at <paramref name="path"/> exists and carries a stamp that is
        /// current for <paramref name="task"/>, this build and <paramref name="key"/> - the one
        /// test for "may this run reuse it".
        /// </summary>
        public bool IsCurrent(string path, string task, string key)
        {
            var stamp = ReadStamp(path);
            return stamp != null && stamp.IsCurrent(task, Version, key);
        }

        /// <summary>
        /// Resolve an upstream producer and ensure its state is materialized before returning
        /// it. The first consumer to demand a given producer triggers its
        /// <see cref="PipelineTask.Rehydrate"/>; later demands return the same instance without
        /// re-materializing. A producer the driver already ran this pass is materialized already.
        /// </summary>
        public T Demand<T>() where T : PipelineTask
        {
            return (T)DemandByType(typeof(T), materialize: true);
        }

        /// <summary>
        /// This run's instance of task <typeparamref name="T"/>, or null when the run has none,
        /// without materializing it - for a task that asks whether another will run.
        /// </summary>
        public T TaskOf<T>() where T : PipelineTask
        {
            return _tasksByType.TryGetValue(typeof(T), out var task) ? (T)task : null;
        }

        /// <summary>
        /// Record that the driver has run <paramref name="task"/>, so a later
        /// <see cref="Demand{T}"/> / <see cref="Get{TInfo}"/> for it returns the already-computed
        /// state instead of driving <see cref="PipelineTask.Rehydrate"/>.
        /// </summary>
        public void MarkMaterialized(PipelineTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            _materialized.Add(task.GetType());
        }

        /// <summary>
        /// Publish a byproduct value for downstream tasks, keyed by its purpose type. Once-only:
        /// publishing the same type twice in a run is a programming defect (two producers, or a
        /// producer running twice) and throws, unless the host says republishing is legitimate
        /// right now (<see cref="AllowRepublish"/>).
        /// </summary>
        public void Publish<TInfo>(TInfo info)
        {
#if DEBUG
            OnPublishing(info);
#endif
            if (AllowRepublish)
                _byproducts[typeof(TInfo)] = info;
            else
                _byproducts.Add(typeof(TInfo), info);
        }

        /// <summary>
        /// Read a byproduct value if it has been published, without triggering any producer.
        /// Use <see cref="Get{TInfo}"/> when a miss should lazily materialize the registered
        /// producer instead of returning <c>false</c>.
        /// </summary>
        public bool TryGet<TInfo>(out TInfo info)
        {
            if (_byproducts.TryGetValue(typeof(TInfo), out var obj))
            {
                info = (TInfo)obj;
#if DEBUG
                _consumedByproducts.Add(typeof(TInfo));
#endif
                return true;
            }
            info = default;
            return false;
        }

        /// <summary>
        /// Retrieve a published byproduct, lazily materializing its producer on a miss: the
        /// registered producer's <see cref="PipelineTask.Rehydrate"/> runs and publishes the
        /// value, and the cache is read again. Throws <see cref="UnknownByproductException"/> if
        /// the type has no registered producer, or if the producer ran but did not publish it -
        /// both programming defects, surfaced loudly rather than as a silent default.
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
        /// Drop a byproduct once its consumers are done with it, so the pipeline stops pinning
        /// it for the rest of the run. Returns <c>true</c> if an entry was removed. RELEASE IS
        /// FINAL: a producer is materialized at most once per context, so a later
        /// <see cref="Get{TInfo}"/> does not rebuild the value; it throws
        /// <see cref="UnknownByproductException"/>. Release only when every consumer has run.
        /// </summary>
        public bool Release<TInfo>()
        {
            return _byproducts.Remove(typeof(TInfo));
        }

        /// <summary>
        /// <see cref="Get{TInfo}"/> followed by <see cref="Release{TInfo}"/>: take the value and
        /// drop the pipeline's reference to it in one step. For a large byproduct with a single
        /// consumer this is the form to use; a separate release can be lost in a refactor and
        /// re-pin the memory. Same finality as <see cref="Release{TInfo}"/>.
        /// </summary>
        public TInfo Consume<TInfo>()
        {
            var info = Get<TInfo>();
            Release<TInfo>();
            return info;
        }

        /// <summary>
        /// Driver-owned skip predicate: <c>true</c> when every file <paramref name="task"/>
        /// declares in <see cref="PipelineTask.Outputs"/> already exists on disk carrying an
        /// embedded stamp current for this task, this build and its
        /// <see cref="PipelineTask.OutputValidityKey"/>, so the task's work is durably present
        /// and need not be recomputed. A task that declares no outputs can never be skipped.
        /// </summary>
        public bool CanRehydrate(PipelineTask task)
        {
            var outputs = new List<string>(task.Outputs(this));
            if (outputs.Count == 0)
                return false;
            string key = task.ValidityKey(this);
            foreach (var output in outputs)
            {
                if (!IsCurrent(output, task.Name, task.OutputValidityKey(this, key, output)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// When true, <see cref="Publish{TInfo}"/> replaces an existing slot instead of throwing.
        /// False by default; a host turns it on only for a diagnostic that legitimately
        /// re-materializes producers (Osprey's drop-between-tasks experiment).
        /// </summary>
        protected virtual bool AllowRepublish => false;

#if DEBUG
        /// <summary>
        /// DEBUG hook called before each <see cref="Publish{TInfo}"/>, for a host's invariant
        /// over its own byproduct types. Default: nothing.
        /// </summary>
        protected virtual void OnPublishing<TInfo>(TInfo info)
        {
        }

        /// <summary>DEBUG: whether <paramref name="byproductType"/> has been read through <see cref="TryGet{TInfo}"/>.</summary>
        protected bool WasConsumed(Type byproductType)
        {
            return _consumedByproducts.Contains(byproductType);
        }
#endif

        /// <summary>
        /// Drop every byproduct except those of the types in <paramref name="keep"/>, and forget
        /// which producers have materialized, so the next task must reload whatever it needs
        /// from disk. Returns how many byproducts were dropped. For a host's diagnostic that
        /// makes an in-process pipeline behave like one split across processes.
        /// </summary>
        protected int DropByproductsExcept(ISet<Type> keep)
        {
            var dropped = new List<Type>();
            foreach (var type in _byproducts.Keys)
            {
                if (!keep.Contains(type))
                    dropped.Add(type);
            }
            foreach (var type in dropped)
                _byproducts.Remove(type);
            _materialized.Clear();
#if DEBUG
            _consumedByproducts.Clear();
#endif
            return dropped.Count;
        }

        /// <summary>
        /// Resolve a registered task by its runtime type, optionally materializing it: the
        /// producer's <see cref="PipelineTask.Rehydrate"/> is driven at most once. Unlike the
        /// driver loop, which inspects Run's bool and stops the pipeline on false, a consumer's
        /// lazy demand has no return channel, so a failed Rehydrate that set a non-zero
        /// <see cref="ExitCode"/> is surfaced as <see cref="RehydrateFailedException"/>. A
        /// success-stop (false with exit code zero) is left benign.
        /// </summary>
        private PipelineTask DemandByType(Type taskType, bool materialize)
        {
            if (!_tasksByType.TryGetValue(taskType, out var task))
                throw new UnknownTaskException(taskType);
            if (materialize && _materialized.Add(taskType))
            {
                if (!task.Rehydrate(this) && ExitCode != 0)
                    throw new RehydrateFailedException(taskType, task.Name, ExitCode);
            }
            return task;
        }
    }

    /// <summary>
    /// Thrown by <see cref="TaskContext.Demand{T}"/> (and the registry-driven materialization in
    /// <see cref="TaskContext.Get{TInfo}"/>) when a task asks for an upstream producer that was
    /// not added to the pipeline at construction time: always a programming defect.
    /// </summary>
    public sealed class UnknownTaskException : InvalidOperationException
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
    /// Thrown by <see cref="TaskContext.Get{TInfo}"/> when a byproduct type has no registered
    /// producer, or when its producer ran but did not publish the value: a missing
    /// <see cref="PipelineTask.Publishes"/> registration, or a producer path that forgot to
    /// publish. Both are programming defects.
    /// </summary>
    public sealed class UnknownByproductException : InvalidOperationException
    {
        public Type RequestedType { get; }

        public UnknownByproductException(Type requestedType)
            : base(string.Format(@"Byproduct type '{0}' has no registered producer, or its producer did not publish it.",
                requestedType?.FullName))
        {
            RequestedType = requestedType;
        }
    }

    /// <summary>
    /// Thrown by <see cref="TaskContext.Get{TInfo}"/> / <see cref="TaskContext.Demand{T}"/> when a
    /// lazily driven <see cref="PipelineTask.Rehydrate"/> reports failure and has set a non-zero
    /// <see cref="TaskContext.ExitCode"/>. Carries the task type and the exit code the failing
    /// task requested; the task has already logged why.
    /// </summary>
    public sealed class RehydrateFailedException : UserMessageException
    {
        public Type TaskType { get; }
        public int ExitCode { get; }

        public RehydrateFailedException(Type taskType, string taskName, int exitCode)
            : base(string.Format(MessageResources.RehydrateFailedException_The__0__step_could_not_reload_its_results_from_the_intermediate_files__exit_code__1___,
                taskName, exitCode))
        {
            TaskType = taskType;
            ExitCode = exitCode;
        }
    }
}
