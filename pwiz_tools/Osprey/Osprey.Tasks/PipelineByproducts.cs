/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Tasks
{
    // Each type below names a single pipeline byproduct so PipelineContext's
    // typed cache (Publish/TryGet/Get) can key on the value's PURPOSE rather
    // than its raw CLR type. Several byproducts share a raw type -- e.g.
    // IReadOnlyDictionary<string, RTCalibration> is BOTH the per-file
    // calibrations and the refined calibrations -- which a typeof()-keyed cache
    // could not tell apart. This mirrors how Skyline's PeakScoringContext keys
    // on purpose types (e.g. MQuestAnalyteCrossCorrelations) instead of a bare
    // collection type. The wrappers are thin and publish-once: the producer
    // wraps its value, consumers read .Value. They carry no behavior -- the
    // type identity is the whole point. The one mutable shared buffer is
    // modeled as a small state hierarchy (see PerFileEntries below) so that it,
    // too, resolves through the byproduct->producer registry uniformly.

    /// <summary>The spectral library (with decoys) produced by Stage 1.</summary>
    internal sealed class FullLibrary
    {
        public List<LibraryEntry> Value { get; }
        public FullLibrary(List<LibraryEntry> value) { Value = value; }
    }

    /// <summary>Stage 1 library indexed by entry id, for Stage 7/8 lookups.</summary>
    internal sealed class LibraryById
    {
        public IReadOnlyDictionary<uint, LibraryEntry> Value { get; }
        public LibraryById(IReadOnlyDictionary<uint, LibraryEntry> value) { Value = value; }
    }

    /// <summary>Per-file first-pass RT calibrations from Stages 2-4.</summary>
    internal sealed class PerFileCalibrations
    {
        public IReadOnlyDictionary<string, RTCalibration> Value { get; }
        public PerFileCalibrations(IReadOnlyDictionary<string, RTCalibration> value) { Value = value; }
    }

    /// <summary>
    /// Per-file isolation-window m/z intervals (half-open <c>[Lo, Hi)</c>) from
    /// Stages 2-4 -- the gap-fill m/z filter's per-file coverage map. Straight
    /// through, each file's list is built from its extracted isolation windows
    /// (<c>center +/- width/2</c>); on an HPC merge node (no mzML) it is
    /// rehydrated from the <c>isolation_scheme</c> block in calibration.json.
    /// Always published non-null (empty when no scheme is available), so the
    /// byproduct exists for every run. Parallels <see cref="PerFileCalibrations"/>
    /// and is keyed by the same bare file stem.
    /// </summary>
    internal sealed class PerFileIsolationMz
    {
        public IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> Value { get; }
        public PerFileIsolationMz(IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> value) { Value = value; }
    }

    /// <summary>Map of file name to its on-disk <c>.scores.parquet</c> path.</summary>
    internal sealed class PerFileParquetPaths
    {
        public IReadOnlyDictionary<string, string> Value { get; }
        public PerFileParquetPaths(IReadOnlyDictionary<string, string> value) { Value = value; }
    }

    /// <summary>
    /// The probe-the-disk reconciliation bundle PerFileScoring hydrates from
    /// sibling sidecars in worker mode, or <c>null</c> at a Stage-5 entry / any
    /// straight-through run that wrote no bundle. The wrapper is always
    /// published once (presence == "PerFileScoring has been materialized"); its
    /// <see cref="Value"/> is the nullable bundle, so a consumer distinguishes
    /// "no bundle" (Value == null) from "producer not yet run" (cache miss).
    /// </summary>
    internal sealed class RescoreBundle
    {
        public RescoreInputs Value { get; }
        public RescoreBundle(RescoreInputs value) { Value = value; }
    }

    /// <summary>
    /// Stage 6 multi-charge consensus rescore targets per file (post-compaction
    /// apex/start/end by stub index), produced by FirstJoin's planning step.
    /// </summary>
    internal sealed class PerFileConsensusTargets
    {
        public IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> Value { get; }
        public PerFileConsensusTargets(
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Whether FirstJoin's Stage 6 planning block actually ran (<c>true</c>) vs
    /// was skipped (single-file / reconciliation off) or rehydrated from disk
    /// (<c>false</c>). This is the gate PerFileRescore's self-gate checks to tell
    /// "planning ran" from "planning was skipped." Routing it through the typed
    /// byproduct registry replaces PerFileRescore's former concrete-type reach
    /// (<c>ctx.Demand&lt;FirstJoinTask&gt;().DidPlan(ctx)</c>) -- the last
    /// compile-time edge to a sibling task in the otherwise uniform
    /// <c>ctx.Get&lt;T&gt;()</c> spine.
    /// </summary>
    internal sealed class PlanningPerformed
    {
        public bool Value { get; }
        public PlanningPerformed(bool value) { Value = value; }
    }

    /// <summary>Stage 6 reconciliation actions keyed by (file, post-compaction index).</summary>
    internal sealed class ReconciliationActions
    {
        public IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> Value { get; }
        public ReconciliationActions(IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> value) { Value = value; }
    }

    /// <summary>Per-file refined RT calibrations from the Stage 6 calibration refit.</summary>
    internal sealed class RefinedCalibrations
    {
        public IReadOnlyDictionary<string, RTCalibration> Value { get; }
        public RefinedCalibrations(IReadOnlyDictionary<string, RTCalibration> value) { Value = value; }
    }

    /// <summary>Per-file gap-fill targets for the Stage 6 rescore.</summary>
    internal sealed class PerFileGapFillForRescore
    {
        public IReadOnlyDictionary<string, List<GapFillTarget>> Value { get; }
        public PerFileGapFillForRescore(IReadOnlyDictionary<string, List<GapFillTarget>> value) { Value = value; }
    }

    /// <summary>
    /// The pipeline's working per-file FDR entry buffer. UNLIKE every other
    /// byproduct here, this is a deliberately MUTABLE shared buffer: the same
    /// inner <see cref="Value"/> list reference is created once by PerFileScoring,
    /// compacted in place by FirstJoin, then overlaid in place by PerFileRescore
    /// (the no-copy hand-off is load-bearing at Astral scale).
    ///
    /// The three in-place mutation milestones are modeled as the distinct
    /// subtypes below (<see cref="ScoredEntries"/> -> <see cref="CompactedEntries"/>
    /// -> <see cref="RescoredEntries"/>), each published once by its single
    /// producing task, so the buffer resolves through the byproduct->producer
    /// registry like every other byproduct: a consumer asks for the milestone it
    /// needs (e.g. MergeNode wants <see cref="RescoredEntries"/>) and a cache
    /// miss lazily materializes the producer that reaches that state.
    ///
    /// IMPORTANT: these subtypes are MILESTONE TOKENS over a shared backing
    /// store, NOT immutable snapshots. Because all three wrap the SAME list (no
    /// copy), reading <see cref="ScoredEntries"/> after PerFileRescore has run
    /// returns the now-rescored list -- the type asserts "the buffer reached at
    /// least this state," not "the buffer as it was at this state." In the
    /// pipeline DAG each milestone is consumed before the next in-place mutation,
    /// so a stale read is never observable. Registry keys are the concrete
    /// subtypes (each single-producer); this base is only for shared accessor
    /// code -- never publish or Get the base type itself.
    /// </summary>
    internal abstract class PerFileEntries
    {
        public List<KeyValuePair<string, List<FdrEntry>>> Value { get; }
        protected PerFileEntries(List<KeyValuePair<string, List<FdrEntry>>> value) { Value = value; }
    }

    /// <summary>The buffer as produced by PerFileScoring (per-file scored stubs).</summary>
    internal sealed class ScoredEntries : PerFileEntries
    {
        public ScoredEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }

    /// <summary>
    /// The lean first-pass projection built straight from each file's .scores.parquet,
    /// bypassing the fat <see cref="FdrEntry"/> stub buffer entirely (issue #4397:
    /// rematerializing 191M stubs to convert them into 32 B rows cost ~53 GB).
    /// <c>Value</c> is null when the run needs the resident stub pool instead
    /// (--model-diagnostics / FDRBench pass 1) or on the rehydrate/merge paths, which
    /// still publish fat stubs via <see cref="ScoredEntries"/>.
    /// </summary>
    internal sealed class FdrProjections
    {
        public FdrProjectionSet Value { get; }
        public FdrProjections(FdrProjectionSet value) { Value = value; }
    }

    /// <summary>The buffer after FirstJoin's first-pass FDR + compaction.</summary>
    internal sealed class CompactedEntries : PerFileEntries
    {
        public CompactedEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }

    /// <summary>The buffer after PerFileRescore's Stage 6 rescore / reconciliation overlay.</summary>
    internal sealed class RescoredEntries : PerFileEntries
    {
        public RescoredEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }
}
