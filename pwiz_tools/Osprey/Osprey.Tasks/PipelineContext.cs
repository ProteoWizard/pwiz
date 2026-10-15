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
using pwiz.Common.Pipeline;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Shared state passed between <see cref="OspreyTask"/> steps: Osprey's
    /// <see cref="OspreyConfig"/>, run plan and diagnostics sink on top of the shared
    /// <see cref="TaskContext"/>, which holds the registry of task instances, the typed
    /// byproduct cache downstream tasks read upstream state through
    /// (<see cref="TaskContext.Get{TInfo}"/>, with a miss lazily materializing the producer via
    /// <see cref="TaskContext.Demand{T}"/> so it can rehydrate from sibling artifacts), the
    /// exit code and the stamp-based resume check. The stamps carry
    /// <see cref="OspreyVersion.Current"/> and are read by <see cref="ArtifactValidity"/>.
    ///
    /// The context is constructed once at the top of <c>AnalysisPipeline.Run</c> and lives for
    /// the duration of the pipeline execution.
    /// </summary>
    public sealed class PipelineContext : TaskContext, IOspreyLog
    {
#if DEBUG
        /// <summary>
        /// DEBUG-only guard for the shared-mutable-buffer ordering invariant of
        /// the <see cref="PerFileEntries"/> milestone family (ScoredEntries ->
        /// CompactedEntries -> RescoredEntries). Those milestones wrap the SAME
        /// backing list and mutate it in place; correctness relies on each
        /// milestone being consumed before the buffer is re-published at the next
        /// milestone (see PipelineByproducts.cs). This records, per backing-list reference,
        /// the milestone type last published over it; the base context records which
        /// byproduct types were read (<see cref="TaskContext.WasConsumed"/>). Keying on the
        /// list reference (not a static type order) makes the guard path-independent: the
        /// reconciled-input path publishes RescoredEntries directly over the ScoredEntries
        /// buffer, skipping CompactedEntries.
        /// </summary>
        private readonly Dictionary<object, Type> _milestoneByBuffer =
            new Dictionary<object, Type>(ReferenceComparer.Instance);
#endif

        /// <summary>
        /// The configuration parsed from CLI args and the input library.
        ///
        /// Mutation contract: fields that feed
        /// <see cref="SearchIdentity.SearchParameterHash"/> /
        /// <see cref="SearchIdentity.LibraryIdentityHash"/> /
        /// <see cref="SearchIdentity.ReconciliationParameterHash"/> must
        /// remain stable for the life of the run, so a worker can
        /// reproduce the same hash a straight-through invocation would
        /// stamp into its parquet footers. NOTHING is written to the config at
        /// pipeline entry any more: it is complete when parsing ends
        /// (<c>OspreyCommandArgs.ToConfig</c>, which is also where
        /// <c>--input-list</c> is expanded into <c>InputFiles</c>). Run-time state that is not
        /// parsed config (e.g. file parallelism) lives on <see cref="RunPlan"/>
        /// instead. For per-file scratch that
        /// mutates hash-affecting fields (e.g. the MS2-calibrated
        /// FragmentTolerance), tasks must use
        /// <c>OspreyConfig.ShallowClone</c> so the mutation stays
        /// scoped to one file.
        /// </summary>
        public OspreyConfig Config { get; }

        /// <summary>
        /// Run-time decisions that are not parsed config: what this run resolved
        /// after parsing, for every task to read (e.g. the file parallelism).
        /// </summary>
        public RunPlan RunPlan { get; } = new RunPlan();

        // No public list of the pipeline's tasks. The driver walks its own list, and a task
        // that needs state from a sibling asks Get/Demand for the state, never the task.
        // The one reader this property ever had was a predicate asking whether ITS OWN
        // consumer was going to run, so it could decide whether to do whole-run work - the
        // second copy of the membership truth table that issue #4597 deleted. Leaving the hook
        // in place is an invitation to write that predicate again.

        /// <summary>
        /// The cross-implementation bisection diagnostics sink for this run, or
        /// <c>null</c> when diagnostics are off (no <c>-d</c> / no OSPREY_DUMP_*
        /// env var) - the common case. Tasks read the gate flags and emit their
        /// byte-stable dumps through the null-conditional operator (e.g.
        /// <c>ctx.Diagnostics?.DumpConsensus ?? false</c>,
        /// <c>ctx.Diagnostics?.WriteStage6ConsensusDump(...)</c>) instead of the
        /// exe-only <c>OspreyDiagnostics</c> static facade, so the task layer does
        /// not depend on the top-level exe project. Null-by-default is deliberate:
        /// the <c>?.</c> short-circuit is a single null check - no virtual call,
        /// arguments not evaluated - matching the existing <c>IScoringDiagnostics</c>
        /// idiom. The driver injects the live sink at construction (see
        /// <c>OspreyDiagnostics.Active</c>).
        /// </summary>
        public IOspreyDiagnostics Diagnostics { get; }

        public PipelineContext(OspreyConfig config,
            IEnumerable<OspreyTask> tasks,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError,
            IOspreyDiagnostics diagnostics = null)
            : base(tasks, OspreyVersion.Current, ArtifactValidity.ReadStamp, logInfo, logWarning, logError)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Diagnostics = diagnostics;
        }

        /// <summary>A machine-channel line; <see cref="OspreyLog.Write"/> decides whether it is emitted.</summary>
        public void LogInfo(LogTag tag, string text) { OspreyLog.Write(LogInfo, tag, text); }

        /// <summary>Implementer-grade detail: emitted only under --verbose (same sink as LogInfo).</summary>
        public void LogVerbose(string message) { if (OspreyOutput.Verbose) LogInfo(message); }

        /// <summary>
        /// DIAGNOSTIC. Drop every byproduct except the library, and forget which producers have
        /// materialized, so the next task must reload whatever it needs from disk.
        ///
        /// <para>This exists to answer one question: can the in-process pipeline behave the way
        /// the HPC split does - each task dropping what it held and the next picking up from
        /// artifacts? <c>regression.ps1</c> gets that behaviour for free by running 8 separate
        /// processes, each with a fresh context; the single-process path cannot, because the
        /// context is built on materialize-once-hold-forever. That asymmetry is why
        /// straight-through holds structures the HPC path never does.</para>
        ///
        /// <para>The library is kept because it is the one thing an HPC node also pays for, and
        /// it is the term that would otherwise dominate any comparison: 8 processes each load it
        /// (4.19 GB / ~2 min on the CHS library), so a naive process-count comparison measures
        /// library loads rather than the handoff. Holding it isolates the variable under test.</para>
        ///
        /// <para>FINDING: the first version of this kept the producers of the KEPT slots marked, so
        /// nothing would rebuild the library - and FirstPassFDR then died immediately on
        /// "ScoredEntries has no registered producer". FullLibrary and ScoredEntries are
        /// published by the SAME task (PerFileScoringTask), so "keep the library, drop the rest"
        /// is not expressible: marking that task materialized to protect the library also blocks
        /// re-materialization of everything else it owns. The library is not a separate concern
        /// in the byproduct graph; it is owned by a scoring task. An HPC node sidesteps this by
        /// loading the library itself, in its own process, with no shared producer to negotiate
        /// with. So materialization is forgotten entirely.</para>
        /// </summary>
        public void DropAllButLibrary()
        {
            var keep = new HashSet<Type> { typeof(FullLibrary), typeof(LibraryById), typeof(SequencePool) };
            int dropped = DropByproductsExcept(keep);
            LogInfo(LogTag.DROP, string.Format(
                @"Released {0} byproduct(s) at the task boundary; the library stays resident.",
                dropped));
        }

        /// <summary>
        /// Publishing twice into one slot is a real defect and ByproductContextTest pins the
        /// throw. The ONLY case that legitimately republishes is the DropAllButLibrary
        /// diagnostic, where a producer re-materializes from disk and its Rehydrate republishes
        /// every slot it owns - so the guard is relaxed there and nowhere else. Relaxing it
        /// globally turned that test red, which is the test working: an invariant weakened for
        /// one experiment must not be weakened for the default path.
        /// </summary>
        protected override bool AllowRepublish => OspreyEnvironment.DropBetweenTasks;

#if DEBUG
        /// <summary>
        /// DEBUG-time enforcement of the <see cref="PerFileEntries"/> milestone
        /// ordering invariant. When a milestone is published over a backing list
        /// that already holds a prior milestone (same list reference), assert the
        /// prior milestone was consumed (read via <see cref="TaskContext.TryGet{TInfo}"/> /
        /// <see cref="TaskContext.Get{TInfo}"/>) before this in-place re-publish. Non-milestone
        /// byproducts are ignored. See <see cref="_milestoneByBuffer"/>.
        /// </summary>
        protected override void OnPublishing<TInfo>(TInfo info)
        {
            if (!(info is PerFileEntries milestone))
                return;
            // BufferIdentity, not Value: this guard needs list identity only, and reading
            // Value would MATERIALIZE a deferred milestone (RescoredEntries), making the
            // DEBUG build pay at publish time the very work deferral exists to move to the
            // consumer's pull - and pay it before the rescore that fills it has run.
            object buffer = milestone.BufferIdentity;
            if (buffer == null)
                return;
            if (_milestoneByBuffer.TryGetValue(buffer, out var priorMilestone))
            {
                System.Diagnostics.Debug.Assert(WasConsumed(priorMilestone),
                    string.Format(
                        @"PerFileEntries ordering invariant violated: milestone '{0}' is being published over a backing list still holding unconsumed milestone '{1}'. Each milestone must be consumed before the shared buffer is re-published at the next milestone.",
                        typeof(TInfo).Name, priorMilestone.Name));
            }
            _milestoneByBuffer[buffer] = typeof(TInfo);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            int IEqualityComparer<object>.GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
#endif
    }
}
