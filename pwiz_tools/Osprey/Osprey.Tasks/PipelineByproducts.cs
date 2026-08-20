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
using pwiz.Osprey.FDR.ModelDiagnostics;
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
    /// Per-file CAL-view calibration diagnostics for the <c>--model-diagnostics</c>
    /// HTML report, captured during Stage 3 calibration and keyed by file name in
    /// input order (parallels <see cref="PerFileCalibrations"/>). Empty on a normal
    /// run and on the rehydrate / resume / HPC-worker paths, where the per-file
    /// calibration MATCHES are not available (only the small calibration.json is
    /// reloaded), so the rows cannot be reconstructed -- FirstPassFdrTask reads this
    /// only under <c>config.ModelDiagnostics</c> and tolerates an empty map.
    ///
    /// <see cref="MassUnit"/> is the per-run mass-error unit ("ppm" or "Th") the CAL
    /// view labels its MS1/MS2 axes with. It is captured alongside the rows because
    /// <see cref="ModelDiagnosticsData.CalFileRow"/> deliberately does not carry it (it
    /// is a per-run scalar on <see cref="ModelDiagnosticsData.CalibrationData"/>, and
    /// the resolution mode that fixes it is resolved per-file at scoring time, not
    /// derivable from config at FirstPassFDR). Null until the first calibrated file records
    /// it; defaults to "ppm" downstream.
    /// </summary>
    internal sealed class PerFileCalibrationDiagnostics
    {
        public IReadOnlyDictionary<string, ModelDiagnosticsData.CalFileRow> Value { get; }
        public string MassUnit { get; }
        public PerFileCalibrationDiagnostics(
            IReadOnlyDictionary<string, ModelDiagnosticsData.CalFileRow> value, string massUnit)
        {
            Value = value;
            MassUnit = massUnit;
        }
    }

    /// <summary>
    /// Per-file isolation-window m/z intervals (half-open <c>[Lo, Hi)</c>) from
    /// Stages 2-4 -- the gap-fill m/z filter's per-file coverage map. Straight
    /// through, each file's list is built from its extracted isolation windows
    /// (<c>center +/- width/2</c>); on an HPC SecondPassFDR node (no mzML) it is
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
    /// apex/start/end by stub index), produced by FirstPassFDR's planning step.
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
    /// Whether FirstPassFDR's Stage 6 planning block actually ran (<c>true</c>) vs
    /// was skipped (single-file / reconciliation off) or rehydrated from disk
    /// (<c>false</c>). This is the gate PerFileRescore's self-gate checks to tell
    /// "planning ran" from "planning was skipped." Routing it through the typed
    /// byproduct registry replaces PerFileRescore's former concrete-type reach
    /// (<c>ctx.Demand&lt;FirstPassFdrTask&gt;().DidPlan(ctx)</c>) -- the last
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
    /// Base_ids of the protein-compact stratum (OSPREY_PASS2_QVALUE=protein-compact):
    /// every library precursor whose peptide maps to a protein detected in the 1st pass
    /// by &gt;=2 DISTINCT peptides (the honest anchor -- single-hit proteins break the
    /// independent-filtering assumption; the entrapment prototype showed &gt;=2 restores
    /// FDP control at full gain). Built in FirstPassFDR (which has the full library + the
    /// 1st-pass detected-peptide set) and consumed by the pass-2 stratified competition.
    /// Bounded by the library (not the observation count) -> flat in file count. Only
    /// published when the mode is set.
    /// </summary>
    internal sealed class ProteinCompactStratum
    {
        public HashSet<uint> BaseIds { get; }
        public ProteinCompactStratum(HashSet<uint> baseIds) { BaseIds = baseIds; }
    }

    /// <summary>
    /// The pipeline's working per-file FDR entry buffer. UNLIKE every other
    /// byproduct here, this is a deliberately MUTABLE shared buffer: the same
    /// inner <see cref="Value"/> list reference is created once by PerFileScoring,
    /// compacted in place by FirstPassFDR, then overlaid in place by PerFileRescore
    /// (the no-copy hand-off is load-bearing at Astral scale).
    ///
    /// The three in-place mutation milestones are modeled as the distinct
    /// subtypes below (<see cref="ScoredEntries"/> -> <see cref="CompactedEntries"/>
    /// -> <see cref="RescoredEntries"/>), each published once by its single
    /// producing task, so the buffer resolves through the byproduct->producer
    /// registry like every other byproduct: a consumer asks for the milestone it
    /// needs (e.g. SecondPassFDR wants <see cref="RescoredEntries"/>) and a cache
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
    /// (--model-diagnostics / FDRBench pass 1) or on the rehydrate / reconciled-input paths, which
    /// still publish fat stubs via <see cref="ScoredEntries"/>.
    /// </summary>
    internal sealed class FdrProjections
    {
        public FdrProjectionSet Value { get; }
        public FdrProjections(FdrProjectionSet value) { Value = value; }
    }

    /// <summary>The buffer after FirstPassFDR's first-pass FDR + compaction.</summary>
    internal sealed class CompactedEntries : PerFileEntries
    {
        public CompactedEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }

    /// <summary>The buffer after PerFileRescore's Stage 6 rescore / reconciliation overlay.</summary>
    internal sealed class RescoredEntries : PerFileEntries
    {
        public RescoredEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }

    /// <summary>
    /// The means to REBUILD any one file's post-compaction survivors from disk,
    /// published by FirstPassFDR alongside <see cref="CompactedEntries"/>.
    ///
    /// <para>Every artifact the rebuild needs - the original <c>.scores.parquet</c>
    /// and the finalized <c>.1st-pass.fdr_scores.bin</c> - is on disk by the time
    /// Stage 5 compacts, so holding the survivors is a choice rather than a
    /// requirement. It is an expensive one: the all-files survivor buffer is
    /// 88.9 M entries / 28 GB at 163 files, live for the whole Stage 6 rescore
    /// (issue #4526). A consumer that works one file at a time takes this instead
    /// and the buffer never has to exist.</para>
    ///
    /// <para><c>Value</c> is null when the run kept the resident buffer (the
    /// token-gated parity oracle), so a consumer must fall back to
    /// <see cref="CompactedEntries"/> when it is absent.</para>
    /// </summary>
    internal sealed class FirstPassSurvivorSource
    {
        public FirstPassSurvivorLoader Value { get; }
        public FirstPassSurvivorSource(FirstPassSurvivorLoader value) { Value = value; }
    }

    /// <summary>
    /// The FROZEN 1st-pass Percolator model (fold weights + biases + feature
    /// standardizer, carried on <see cref="PercolatorResults"/>), captured at
    /// first-pass FDR time. Published only under the OSPREY_PASS2_QVALUE=transfer
    /// path so the SecondPassFDR 2nd-pass step can re-score reconciled features with
    /// this frozen model (TRIC-style confidence transfer) instead of retraining a
    /// decoy-depleted 2nd-pass SVM. Absent (never published) on the default
    /// percolator path. See ai/todos/active/TODO-20260710_osprey_pass2_recalibration_fix.md.
    /// </summary>
    internal sealed class FirstPassPercolatorModel
    {
        public PercolatorResults Results { get; set; }

        /// <summary>
        /// The normalized OSPREY_EXPERIMENT_AGG arm that the FIRST pass actually ran under
        /// (<see cref="OspreyEnvironment.ExperimentAgg"/> as of that process), or null when the
        /// model came from a sidecar written before this was recorded.
        ///
        /// Recorded rather than re-read, because the 2nd pass may not be the same process: a
        /// distributed <c>--task SecondPassFDR</c> node reloads this model from disk
        /// (<see cref="FirstPassModelIO"/>) and never trained pass 1, so ITS environment says
        /// nothing about which aggregation produced the q-values it is about to rewrite.
        /// Inferring from the live process was wrong in both directions - a SecondPassFDR node with the
        /// variable unset would emit a mixed q column with no refusal, and a consistent run
        /// could be aborted by a stale exported variable. Provenance travels with the artifact.
        /// </summary>
        public string ExperimentAgg { get; set; }
    }
}
