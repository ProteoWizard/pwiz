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
using System.Threading;
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
    // too, resolves through the byproduct->producer registry uniformly, and it
    // is the one exception to "no behavior": RescoredEntries may be published
    // DEFERRED, so that reading it is what brings the buffer to that milestone.

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

    /// <summary>
    /// The run's ONE modified-sequence pool, seeded from the library so that every sidecar
    /// reader canonicalizes onto the library's own string instances.
    ///
    /// <para>A parquet reader hands out a fresh string per row, so the FDR pool would hold one
    /// string object per observation - ~72 B of a survivor's measured 274 B, about 9.9 GB at
    /// 137 M survivors (issue #4486). Interning against a pool of its OWN would elect the first
    /// parquet instance as canonical, leaving the run holding the library's set AND the
    /// sidecars', which costs more than it saves. Seeding from the library first is what makes
    /// this a collapse rather than a duplication, and it is why there is exactly one of these
    /// per run rather than one per loader.</para>
    ///
    /// <para>Seeded LAZILY: a run that never loads stubs (Stage 1-4 only) should not pay a walk
    /// of six million library entries. The seed is guarded because the byproduct is reachable
    /// from more than one task; <see cref="LibraryStringInterner"/> itself is not synchronized,
    /// so its CALLERS must stay single-threaded loads - which every stub loader is.</para>
    /// </summary>
    internal sealed class SequencePool
    {
        private readonly IReadOnlyDictionary<uint, LibraryEntry> _libraryById;
        private readonly object _seedLock = new object();
        private LibraryStringInterner _interner;

        public SequencePool(IReadOnlyDictionary<uint, LibraryEntry> libraryById)
        {
            _libraryById = libraryById;
        }

        public LibraryStringInterner Value
        {
            get
            {
                lock (_seedLock)
                {
                    if (_interner == null)
                        _interner = Seed();
                    return _interner;
                }
            }
        }

        /// <summary>
        /// Distinct sequences the LIBRARY contributed, captured before any sidecar was read.
        /// Compared against the pool's distinct count afterwards it answers the only question
        /// worth asking of this design: a count that has not moved means every sidecar value
        /// landed on a library instance and the readers allocated no sequences at all.
        /// </summary>
        public int SeedCount { get; private set; }

        /// <summary>
        /// Log what the pool holds and how much of it the library supplied. No-op when the pool
        /// was never seeded - a run that read no sidecar has nothing to report.
        /// </summary>
        public void LogSummary(Action<string> logInfo)
        {
            if (logInfo == null)
                return;
            LibraryStringInterner interner;
            lock (_seedLock)
                interner = _interner;
            if (interner == null)
                return;
            logInfo(string.Format(
                "Sequence pool: {0} distinct seeded from the library, {1} sidecar lookup(s) missed it",
                SeedCount, interner.FrozenMisses));
        }

        /// <summary>
        /// Prime the pool with every library modified sequence, so a later sidecar value equal
        /// to one of them is answered with the LIBRARY's instance and costs no string at all.
        /// </summary>
        private LibraryStringInterner Seed()
        {
            var interner = new LibraryStringInterner();
            if (_libraryById == null)
                return interner;
            foreach (var entry in _libraryById.Values)
            {
                if (entry != null)
                    interner.Intern(entry.ModifiedSequence);
            }
            SeedCount = interner.DistinctCount;
            // Frozen from here on. Stage 6 reads this pool from inside a Parallel.For over
            // files, and a Dictionary tolerates concurrent readers but not a writer among
            // them - so the seeding walk is the only write it ever takes.
            interner.Freeze();
            return interner;
        }
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
        private readonly List<KeyValuePair<string, List<FdrEntry>>> _buffer;

        protected PerFileEntries(List<KeyValuePair<string, List<FdrEntry>>> value) { _buffer = value; }

        /// <summary>
        /// The shared buffer, at this milestone's state. Reading it is the PULL:
        /// a DEFERRED milestone (see <see cref="RescoredEntries"/>) does the work that
        /// reaches its state here, on the first read, so a process where nobody reads
        /// it never pays for it.
        /// </summary>
        public virtual List<KeyValuePair<string, List<FdrEntry>>> Value => _buffer;

        /// <summary>
        /// The backing list as an OPAQUE reference, for identity comparison only - the DEBUG
        /// milestone-ordering guard in <see cref="PipelineContext"/> keys on which milestone
        /// was last published over a given buffer, and reading <see cref="Value"/> to get it
        /// would make the guard itself the thing that pulls.
        ///
        /// <para>Typed as <see cref="object"/> on purpose. The same accessor typed as the
        /// list would sit one keystroke from <c>Value</c> in every task in this assembly, and
        /// on a deferred milestone it hands back 82 empty per-file lists with no exception and
        /// no warning - a blib with no precursors. Nothing can read entries through this.</para>
        /// </summary>
        internal object BufferIdentity => _buffer;
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
        // DEFERRED, because on a resume this is usually built and never read. Its ONLY consumer
        // is FirstPassFdrTask.Run (ctx.Consume<FdrProjections>()), and a resume whose 1st-pass
        // outputs are already valid SKIPS that Run entirely - so PerFileScoring's rehydrate was
        // streaming every row of every parquet to build a set nobody would ask for. Measured
        // 2026-09-03 on the 446-file CHS cohort: 9m46s scanning 1,342,686,095 rows, discarded.
        //
        // Same shape as the RescoredEntries milestone, and for the same reason: the work belongs
        // to whoever needs the product, at the moment they need it. Building it eagerly in a
        // fan-out task is a pre-processing pass over all runs, which the target shape forbids.
        private readonly Func<FdrProjectionSet> _build;
        private FdrProjectionSet _value;
        private bool _built;

        public FdrProjections(FdrProjectionSet value)
        {
            _value = value;
            _built = true;
        }

        /// <summary>Build on first read. A null factory means "no projection".</summary>
        public FdrProjections(Func<FdrProjectionSet> build)
        {
            _build = build;
        }

        public FdrProjectionSet Value
        {
            get
            {
                if (!_built)
                {
                    _value = _build?.Invoke();
                    _built = true;
                }
                return _value;
            }
        }
    }

    /// <summary>The buffer after FirstPassFDR's first-pass FDR + compaction.</summary>
    internal sealed class CompactedEntries : PerFileEntries
    {
        public CompactedEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }
    }

    /// <summary>
    /// The buffer after PerFileRescore's Stage 6 rescore / reconciliation overlay.
    ///
    /// <para>May be published DEFERRED. The streamed Stage 6 rescore drops each file's
    /// entries as it goes (issue #4526), so reaching this milestone means re-reading every
    /// file's artifacts - 16 minutes and 27 GB at 82 SEA-AD files. That is whole-run join
    /// work, and PerFileRescoring is a per-file HPC task whose process exits at its end, so
    /// it must not be the one to pay it: a <c>--task PerFileRescoring</c> worker has no
    /// SecondPassFDR to serve. Deferring it to the first <see cref="Value"/> read moves the
    /// cost to the consumer that needs the global pool, and a worker skips the work because
    /// nothing pulled it rather than because a predicate asked whether its own consumer was
    /// going to run (issue #4597).</para>
    ///
    /// <para>Run-once, and a FAILED build stays failed: the build overlays reconciled
    /// parquets and appends gap-fill rows, so running it a second time over one buffer
    /// duplicates them, and resuming from a half-filled buffer reports a plausible wrong
    /// number rather than an error. <see cref="Lazy{T}"/> in
    /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> is exactly those two
    /// semantics - one execution however many readers arrive, and a cached exception
    /// rethrown to every later reader instead of a silently partial pool.</para>
    /// </summary>
    internal sealed class RescoredEntries : PerFileEntries
    {
        private readonly Lazy<bool> _materialize;

        /// <summary>The buffer already at its post-rescore state - nothing deferred.</summary>
        public RescoredEntries(List<KeyValuePair<string, List<FdrEntry>>> value) : base(value) { }

        /// <summary>
        /// The run's file names, in buffer order. Reading them builds the buffer when the
        /// build is still deferred, exactly as <see cref="PerFileEntries.Value"/> does -
        /// every consumer of this milestone runs after Stage 7's own pool build, so by the
        /// time anyone asks there is nothing left to defer.
        /// </summary>
        public IReadOnlyList<string> FileNames
        {
            get { return Value.ConvertAll(kv => kv.Key); }
        }

        /// <summary>
        /// The run's files, one at a time, for a consumer that ITERATES and does not retain.
        ///
        /// <para>Yields from the resident buffer. The enumeration shape is the point: every
        /// Stage 7 consumer folds to an O(distinct) aggregate through this seam rather than
        /// indexing into the pool, which is what lets a per-file source replace the buffer
        /// behind it (#4486) without those consumers changing. A per-file streamed source
        /// stood here once and was removed as unreachable: Stage 7 builds the pool before
        /// any consumer runs, and the source's rebuild-from-disk overlaid only the 1st-pass
        /// sidecar, so the second-pass gates would have read 1st-pass q-values had it ever
        /// run. The lean-row work is what retires the pool build and puts a streamed source
        /// here for real.</para>
        /// </summary>
        public IEnumerable<KeyValuePair<string, List<FdrEntry>>> Files()
        {
            foreach (var kv in Value)
                yield return kv;
        }

        /// <param name="value">The shared backing buffer, filled in place by
        /// <paramref name="materialize"/>.</param>
        /// <param name="materialize">Brings <paramref name="value"/> to its post-rescore
        /// state on the first <see cref="Value"/> read. Throws on failure - a deferred build
        /// has no return channel to the driver loop - and the throw is cached, so a second
        /// reader sees the same failure rather than a partially built pool.</param>
        public RescoredEntries(List<KeyValuePair<string, List<FdrEntry>>> value, Action materialize)
            : base(value)
        {
            _materialize = new Lazy<bool>(() => { materialize(); return true; },
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public override List<KeyValuePair<string, List<FdrEntry>>> Value
        {
            get
            {
                // Reading Lazy.Value IS the build - once however many readers arrive, and a
                // failure cached and rethrown rather than retried. The bool it yields only
                // exists because Lazy needs a value type to hand back; discard it.
                _ = _materialize?.Value;
                return base.Value;
            }
        }
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
