/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// In-memory state needed to drive a per-file Stage 6 rescore from
    /// the Stage 5 → Stage 6 boundary files on disk. Mirrors
    /// <c>RescoreInputs</c> in <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// The same shape the in-process pipeline holds at the boundary, so
    /// the rescore engine can be written once and used from both the
    /// in-process path and the worker path.
    /// </summary>
    public class RescoreInputs
    {
        /// <summary>
        /// Per-file <see cref="FdrEntry"/> stubs from
        /// <c>&lt;stem&gt;.scores.parquet</c>, with SVM scores + 4 q-values
        /// + PEP + <c>ExperimentProteinQvalue</c> overlaid from the
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar. File order
        /// matches the order of <c>parquetPaths</c> passed to
        /// <see cref="RescoreHydration.HydrateReconciliationOverlay"/>.
        /// </summary>
        public List<KeyValuePair<string, List<FdrEntry>>> PerFileEntries { get; set; }

        /// <summary>
        /// Reconciliation actions keyed by <c>(file_name, vec_idx)</c>.
        /// Built from the homogeneous <c>use_cwt_peak_actions</c> +
        /// <c>forced_integration_actions</c> arrays in
        /// <c>reconciliation.json</c> by joining each action's
        /// <c>entry_id</c> against the loaded stub list. Keep actions
        /// are implicitly absent (the planner never persists them).
        /// </summary>
        public Dictionary<(string FileName, int Index), ReconcileAction> ReconciliationActions { get; set; }

        /// <summary>
        /// Refined per-file RT calibrations reconstructed from
        /// <c>reconciliation.json</c>'s <c>refined_rt_calibration</c>
        /// field via <see cref="RTCalibration.FromModelParams"/>. Files
        /// whose envelope had a null calibration (e.g., refined fit
        /// failed during Stage 5) are absent from the dictionary.
        /// </summary>
        public Dictionary<string, RTCalibration> RefinedCalibrations { get; set; }

        /// <summary>
        /// Per-file gap-fill targets parsed from
        /// <c>reconciliation.json</c>'s <c>gap_fill_targets</c> array.
        /// </summary>
        public Dictionary<string, List<GapFillTarget>> PerFileGapFill { get; set; }

        /// <summary>
        /// Per-file multi-charge consensus rescore targets. Populated
        /// only by callers that compute it post-compaction (consensus
        /// is meaningful only against the surviving entry set). Left
        /// null when the bundle is built ahead of compaction; the
        /// downstream task computes it on demand in that case.
        /// </summary>
        public Dictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> PerFileConsensusTargets { get; set; }

        /// <summary>
        /// Full set of file stems participating in the planner's join, as
        /// read from <c>reconciliation.json</c>'s <c>file_stems</c> field
        /// (v2+). Per-file Stage 6 rescore workers carry this through so
        /// they can compute the join-wide reconciliation parameter hash —
        /// the worker's <c>OspreyConfig.InputFiles</c> only has its single
        /// parquet, but the hash that the downstream
        /// <c>--task SecondPassFDR</c> node validates is computed over
        /// all files. Empty list when reading a v1 envelope (the worker
        /// falls back to its <c>InputFiles</c> stems in that case).
        /// </summary>
        public List<string> JoinFileStems { get; set; }

        /// <summary>
        /// Join-wide set of base_ids that survived first-pass compaction, read
        /// from the <c>reconciliation.json</c> envelope's <c>first_pass_base_ids</c>
        /// (v3 required field) that FirstPassFDR wrote with every file in memory.
        /// <see cref="RescoreCompaction"/> compacts to exactly this set so an HPC
        /// per-file worker keeps the same cross-file entries the in-memory
        /// straight-through pipeline keeps.
        /// </summary>
        public HashSet<uint> GlobalFirstPassBaseIds { get; set; }

        /// <summary>
        /// The set of base_ids the compaction actually RETAINED:
        /// <see cref="GlobalFirstPassBaseIds"/> unioned with the base_ids of every entry
        /// the planner emitted a reconciliation action for. Set by
        /// <see cref="RescoreCompaction.Apply"/>, which is the single authority on the
        /// retained set across every bundle arm (worker-supplied, own-sidecar batch,
        /// own-sidecar streaming), so a consumer that has to reproduce the survivor list
        /// takes it from here rather than re-deriving one of the two terms and silently
        /// dropping the other.
        ///
        /// <para>Its one consumer is <c>FirstPassFdrTask.Rehydrate</c>, which uses it to build
        /// the per-file <see cref="FirstPassSurvivorLoader"/> a resume publishes so Stage 6
        /// streams instead of holding the all-files survivor buffer (issue #4536). Null
        /// before <c>Apply</c> has run.</para>
        /// </summary>
        public HashSet<uint> RetainedBaseIds { get; set; }

        /// <summary>
        /// Per-file PRE-compaction tallies captured by
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/>. That hydrate
        /// compacts each file as it loads and therefore never holds more than ONE
        /// file's pre-compaction pool, so the counts a caller used to take off the
        /// resident all-files pool have to be reduced during the load instead.
        /// Null on the batch <see cref="RescoreHydration.HydrateReconciliationOverlay"/>,
        /// whose caller still has that pool and counts it directly.
        ///
        /// Indexed by FILE INDEX, positionally aligned with <see cref="PerFileEntries"/>,
        /// not keyed by file name: two <c>--input-scores</c> paths in different
        /// directories can share a stem, and a name-keyed map silently let the second
        /// overwrite the first's tally. When non-null this list is COMPLETE - one entry
        /// per file, in load order - so a consumer that cannot find a file's tally has a
        /// real inconsistency, not a fallback case.
        /// </summary>
        public List<PreCompactionTally> PreCompactionTallies { get; set; }

        /// <summary>
        /// The <c>--model-diagnostics</c> pass-1 report reduction, folded row by row off the
        /// same PRE-compaction pools <see cref="PreCompactionTallies"/> is reduced from, while
        /// each file was briefly resident during
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/>. The report needs the
        /// pre-compaction entries (compaction discards ~52x of them, mostly the decoys and
        /// entrapment the FDP and calibration views are built from), so on the streaming
        /// hydrate it has to be accumulated during the load rather than built afterwards off a
        /// pool that no longer exists. Null unless <c>--model-diagnostics</c> is set, so the
        /// default path allocates nothing; the batch
        /// <see cref="RescoreHydration.HydrateReconciliationOverlay"/> also leaves it null -
        /// its caller still holds the all-files pre-compaction pool and builds the report from
        /// it directly.
        ///
        /// Has exactly ONE reader, <c>FirstPassFdrTask</c>'s rehydrate, which nulls this property
        /// as soon as it has written the report. The bundle travels on a published byproduct
        /// slot that lives for the whole process, so an accumulator left set here would pin
        /// its ~1-2 GB through Stage 6 and SecondPassFDR with nothing left to read it.
        /// </summary>
        public ModelDiagnosticsData.Accumulator ModelDiagnosticsAccumulator { get; set; }

        /// <summary>Total non-Keep reconciliation actions across all files.</summary>
        public int TotalActions => ReconciliationActions.Count;

        /// <summary>
        /// Total PRE-compaction stubs across all files, or 0 when
        /// <see cref="PreCompactionTallies"/> is null (batch hydrate). Accumulated as
        /// <c>long</c>: a file carries ~4.2 M pre-compaction stubs, so an <c>int</c> sum
        /// overflows past ~505 files and would report a negative total.
        /// </summary>
        public long TotalPreCompactionStubs
        {
            get
            {
                if (PreCompactionTallies == null)
                    return 0;
                long n = 0;
                foreach (var tally in PreCompactionTallies)
                    n += tally.Stubs;
                return n;
            }
        }

        /// <summary>Total stubs across all files.</summary>
        public int TotalStubs
        {
            get
            {
                int n = 0;
                foreach (var kv in PerFileEntries)
                    n += kv.Value.Count;
                return n;
            }
        }

        /// <summary>Total gap-fill targets across all files.</summary>
        public int TotalGapFillTargets
        {
            get
            {
                int n = 0;
                foreach (var kv in PerFileGapFill)
                    n += kv.Value.Count;
                return n;
            }
        }
    }

    /// <summary>
    /// One file's PRE-compaction reductions, captured while that file's full stub
    /// list is briefly resident during
    /// <see cref="RescoreHydration.HydrateCompactedStreaming"/>. These are the only
    /// two quantities the rehydrate path used to read off the all-files
    /// pre-compaction pool: the stub count (the "total scored entries" figure and
    /// its zero guard) and the run-level FDR passing-target count (the per-file
    /// Stage 5 result line). Reducing them per file keeps both exact without
    /// holding more than one file's pool.
    /// </summary>
    public class PreCompactionTally
    {
        /// <summary>Stubs loaded from this file's parquet, before compaction.</summary>
        public int Stubs { get; set; }

        /// <summary>
        /// Non-decoy stubs in this file passing run-level FDR, before compaction.
        /// The caller owns the predicate (it needs <c>OspreyConfig</c>); this type
        /// only carries the result.
        /// </summary>
        public int PassingTargets { get; set; }
    }

    /// <summary>
    /// ONE run's Stage-6 rescore inputs, produced by
    /// <see cref="RescoreHydration.HydrateOneRun"/> from that run's own artifacts plus the
    /// analysis-wide summaries. The per-run counterpart of <see cref="RescoreInputs"/>, which
    /// holds the same things keyed by file for every run at once.
    ///
    /// <para>Every field here is a per-run quantity that <see cref="RescoreInputs"/> stores in a
    /// file-keyed dictionary. That is the whole difference, and it is the difference between a
    /// fan-out task and a join: an 86-run rescore built those dictionaries for all 86 runs
    /// before touching one, and the rescore loop then read exactly one slice per iteration.</para>
    /// </summary>
    public sealed class RunRescoreInputs
    {
        /// <summary>The run's stem, as every per-file artifact path is derived from it.</summary>
        public string FileName { get; set; }

        /// <summary>
        /// This run's POST-compaction survivors, already overlaid with its
        /// <c>.1st-pass.fdr_scores.bin</c> and filtered to the analysis-wide retained set.
        /// </summary>
        public List<FdrEntry> Survivors { get; set; }

        /// <summary>
        /// This run's reconciliation actions, keyed <c>(file_name, vec_idx)</c> against
        /// <see cref="Survivors"/>. Keyed the same way as
        /// <see cref="RescoreInputs.ReconciliationActions"/> - with one file's entries in it -
        /// so a consumer written for the all-runs map reads it unchanged.
        /// </summary>
        public Dictionary<(string FileName, int Index), ReconcileAction> ReconciliationActions { get; set; }

        /// <summary>This run's gap-fill targets; null when its envelope carried none.</summary>
        public List<GapFillTarget> GapFill { get; set; }

        /// <summary>This run's refined RT calibration; null when its envelope carried none.</summary>
        public RTCalibration RefinedCalibration { get; set; }

        /// <summary>
        /// The planner's full join file-stem set, carried in every run's envelope, for the
        /// reconciled parquet's join-wide metadata hash. Analysis-wide by content, per-run by
        /// storage - so reading it here costs no other run's file.
        /// </summary>
        public IReadOnlyList<string> JoinFileStems { get; set; }

        /// <summary>
        /// The join-wide first-pass passing base_ids off this run's envelope. Also analysis-wide
        /// by content and identical in every envelope.
        /// </summary>
        public HashSet<uint> GlobalFirstPassBaseIds { get; set; }

        /// <summary>This run's PRE-compaction tally, reduced while its full pool was resident.</summary>
        public PreCompactionTally Tally { get; set; }
    }

    /// <summary>
    /// Hydrate the Stage 5 → Stage 6 boundary file pair into the
    /// in-memory state needed to drive a per-file rescore. Mirrors
    /// <c>hydrate_for_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// </summary>
    public static class RescoreHydration
    {
        /// <summary>
        /// Overlay the per-file 1st-pass FDR sidecars and parse the per-file
        /// <c>reconciliation.json</c> envelopes onto an already-loaded
        /// <paramref name="perFileEntries"/> list. The per-file element at
        /// index <c>i</c> in <paramref name="perFileEntries"/> must
        /// correspond to <paramref name="parquetPaths"/>[<c>i</c>] — the
        /// fileName key is rederived from the parquet path for the sidecar
        /// path computation.
        ///
        /// Used by the in-pipeline joinOnly dispatch, which already has stubs
        /// loaded with PIN features + calibration siblings and just needs the
        /// rescore overlay added to share state with FirstPassFDR /
        /// PerFileRescore.
        ///
        /// The returned <see cref="RescoreInputs"/> references the SAME
        /// <see cref="FdrEntry"/> list objects passed in
        /// <paramref name="perFileEntries"/>; this method mutates those
        /// lists' element fields via the FDR sidecar overlay.
        /// <see cref="RescoreInputs.PerFileConsensusTargets"/> is left
        /// null; callers that need it compute it post-compaction.
        /// </summary>
        public static RescoreInputs HydrateReconciliationOverlay(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<string> parquetPaths,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            LibraryStringInterner sequencePool = null)
        {
            if (perFileEntries == null) throw new ArgumentNullException(nameof(perFileEntries));
            if (parquetPaths == null) throw new ArgumentNullException(nameof(parquetPaths));
            if (perFileEntries.Count != parquetPaths.Count)
            {
                throw new InvalidDataException(string.Format(
                    "HydrateReconciliationOverlay: perFileEntries.Count ({0}) != parquetPaths.Count ({1})",
                    perFileEntries.Count, parquetPaths.Count));
            }

            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            var reconciliationActions = new Dictionary<(string, int), ReconcileAction>();
            // Cross-envelope agreement on file_stems + first_pass_base_ids, captured
            // from the first envelope and checked against every sibling.
            var consistency = new EnvelopeConsistency();

            // Per-file progress. This loop reads a sidecar + a reconciliation envelope for
            // every file and was silent throughout: on a 20-file resume it produced a 35 s
            // gap in the log, the kind that reads as a hang. ProgressReporter also emits a
            // HEARTBEAT_SECONDS tick. That bounds the gap only while Report keeps being
            // called - it fires from inside Report - so a single slow file still reopens it.
            using (var hydrateProgress = new ProgressReporter(
                       @"Hydrating reconciliation bundle", perFileEntries.Count))
            {
                for (int i = 0; i < perFileEntries.Count; i++)
                {
                    hydrateProgress.Report(i);
                    string parquetPath = parquetPaths[i];
                    string syntheticInput = SyntheticInputFromParquet(parquetPath);
                    string fileName = perFileEntries[i].Key;
                    var stubs = perFileEntries[i].Value;

                    OverlayFirstPassSidecar(syntheticInput, fileName, stubs,
                        nameof(HydrateReconciliationOverlay), experimentRecords);

                    string reconPath = ReconciliationFile.PathForInput(syntheticInput);
                    var envelope = LoadEnvelope(reconPath, nameof(HydrateReconciliationOverlay));
                    consistency.Check(envelope, reconPath, nameof(HydrateReconciliationOverlay));

                    // Build entry_id -> vec_idx map from the loaded stubs so the
                    // planner's entry_id-keyed actions can be rehomed onto
                    // (file_name, vec_idx) keys the rescore engine consumes.
                    var idToIdx = new Dictionary<uint, int>(stubs.Count);
                    for (int idx = 0; idx < stubs.Count; idx++)
                        idToIdx[stubs[idx].EntryId] = idx;

                    MapPlannedActions(PlanActions(envelope), fileName, reconPath, idToIdx,
                        reconciliationActions, nameof(HydrateReconciliationOverlay));
                    CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                        sequencePool);
                }
                hydrateProgress.Report(perFileEntries.Count);
            }

            return new RescoreInputs
            {
                PerFileEntries = perFileEntries,
                ReconciliationActions = reconciliationActions,
                RefinedCalibrations = refinedCalibrations,
                PerFileGapFill = perFileGapFill,
                PerFileConsensusTargets = null,
                JoinFileStems = consistency.JoinFileStems ?? new List<string>(),
                GlobalFirstPassBaseIds = consistency.GlobalBaseIds,
            };
        }

        /// <summary>
        /// File-count-bounded twin of <see cref="HydrateReconciliationOverlay"/>: produces
        /// the SAME post-compaction bundle without ever holding more than ONE file's
        /// pre-compaction stub pool. The batch twin materializes every file's full Stage-4
        /// stub list (~4.25 M rows, ~1.19 GB per file) and only lets
        /// <see cref="RescoreCompaction"/> discard the ~52x non-survivors afterwards, so its
        /// peak is O(files) - ~104 GB projected at 82 files.
        ///
        /// This works because the compaction predicate does not depend on the loaded pool.
        /// <see cref="RescoreCompaction.Apply"/> retains (a) the join-wide
        /// <c>first_pass_base_ids</c> and (b) the base_ids of every entry the planner emitted
        /// an action for. <paramref name="retainedBaseIds"/> is that union, already complete:
        /// the planner computed it with the whole analysis in hand and left it in the
        /// analysis-wide <c>RetainedBaseIdSidecar</c>, so this method fixes its retain set
        /// before reading anything and needs ONE pass:
        ///
        ///   per run, in <paramref name="parquetPaths"/> order: read that run's
        ///   <c>reconciliation.json</c>, load its stubs, overlay its
        ///   <c>.1st-pass.fdr_scores.bin</c>, compact to the retain set, keep ONLY the
        ///   survivors, map its actions, move on.
        ///
        /// <para>It used to take TWO passes, the first over every envelope, because term (b)
        /// had to be unioned across all runs before any run could be filtered - run A can
        /// retain a base_id only because run B has an action on it. That made this a join
        /// wherever it was called with more than one run: on a 446-run cohort, 10.7 GB of
        /// envelope JSON parsed and 30.8 M planned actions held before the first parquet row
        /// was read. Supplying the union instead of rebuilding it is what removes the pre-pass;
        /// see <c>RetainedBaseIdSidecar</c> for why the planner is the only component that can
        /// compute it.</para>
        ///
        /// The result is the state <see cref="RescoreCompaction.Apply"/> would have produced,
        /// so the caller still runs <c>Apply</c> afterwards: it re-derives the identical
        /// retain set, finds nothing left to remove, and rebuilds the action map exactly as
        /// on the batch path. <c>Apply</c> therefore stays the single authority on both the
        /// survivor set and the <c>vec_idx</c> remap, and this method is a provably
        /// conservative pre-filter in front of it.
        ///
        /// <paramref name="perFileEntries"/> must be EMPTY on entry; survivors are appended
        /// to it in <paramref name="parquetPaths"/> order and the returned bundle references
        /// that same list object (the shared mutable buffer contract the batch twin has).
        /// <paramref name="loadStubs"/> is called once per file with
        /// <c>(fileIndex, fileName, parquetPath)</c> and must return that file's FULL
        /// pre-compaction stub list. <paramref name="onStubsHydrated"/> is called with
        /// <c>(fileIndex, fileName, fullStubList, tally)</c> just before that list is
        /// compacted - the caller's one look at a file's pre-compaction pool, where it fills
        /// in <see cref="PreCompactionTally.PassingTargets"/> and anything else it used to
        /// reduce off the resident all-files pool (the <c>--model-diagnostics</c> report
        /// accumulator is fed there too, which is why the file index is passed); it may be null.
        /// </summary>
        public static RescoreInputs HydrateCompactedStreaming(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<string> parquetPaths,
            Func<int, string, string, List<FdrEntry>> loadStubs,
            Action<int, string, List<FdrEntry>, PreCompactionTally> onStubsHydrated,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            HashSet<uint> retainedBaseIds,
            LibraryStringInterner sequencePool = null)
        {
            if (perFileEntries == null)
                throw new ArgumentNullException(nameof(perFileEntries));
            if (parquetPaths == null)
                throw new ArgumentNullException(nameof(parquetPaths));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));
            if (retainedBaseIds == null)
                throw new ArgumentNullException(nameof(retainedBaseIds));
            if (perFileEntries.Count != 0)
            {
                throw new InvalidDataException(string.Format(
                    "HydrateCompactedStreaming: perFileEntries must be empty on entry (got {0})",
                    perFileEntries.Count));
            }
            if (parquetPaths.Count == 0)
                throw new InvalidDataException("HydrateCompactedStreaming: parquetPaths is empty");

            int nFiles = parquetPaths.Count;
            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            var reconciliationActions = new Dictionary<(string, int), ReconcileAction>();
            // Positionally aligned with perFileEntries (both are appended in parquetPaths
            // order), so a consumer indexes rather than looks a file up by stem - two
            // --input-scores paths in different directories can share a stem.
            var tallies = new List<PreCompactionTally>(nFiles);
            var consistency = new EnvelopeConsistency();

            // ONE pass over the runs, each iteration self-contained: read that run's envelope,
            // load its stubs, overlay, compact, map its actions, hand it on. Nothing is carried
            // between iterations except products the returned bundle owns anyway.
            //
            // There used to be a pass 1 over every envelope first, for one reason: the retained
            // set is the join-wide first-pass base_ids UNION every run's action targets, and the
            // union had to be complete before ANY run could be filtered - run A can retain a
            // base_id only because run B has an action on it. That made this a join: 446
            // envelopes at 24.6 MB each, 10.7 GB of JSON parsed and 30.8 M planned actions held,
            // before a single parquet row was read.
            //
            // <paramref name="retainedBaseIds"/> is that union, computed once by the planner -
            // the one component that legitimately holds the whole analysis - and read back from
            // the analysis-wide RetainedBaseIdSidecar. With it supplied, a run's envelope is
            // needed only by the run it belongs to, and is released with it.
            using (var hydrateProgress = new ProgressReporter(
                       @"Hydrating reconciliation bundle", nFiles))
            {
                for (int i = 0; i < nFiles; i++)
                {
                    hydrateProgress.Report(i);
                    string syntheticInput = SyntheticInputFromParquet(parquetPaths[i]);
                    string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        throw new InvalidDataException(string.Format(
                            "HydrateCompactedStreaming: could not derive file_name from parquet path {0}",
                            parquetPaths[i]));
                    }
                    string reconPath = ReconciliationFile.PathForInput(syntheticInput);
                    var envelope = LoadEnvelope(reconPath, nameof(HydrateCompactedStreaming));
                    consistency.Check(envelope, reconPath, nameof(HydrateCompactedStreaming));

                    var planned = PlanActions(envelope);
                    CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                        sequencePool);

                    var stubs = loadStubs(i, fileName, parquetPaths[i]);
                    // NOTE: this loop is the ALL-RUNS builder, kept for the straight-through
                    // pipeline whose Stage 7 consumes the whole-run pool in process. A caller
                    // that rescores one run at a time must use HydrateOneRun instead - its
                    // signature takes a single parquet path, so it cannot reach another run's
                    // artifacts even by accident. See that method for why the distinction is
                    // enforced by the type rather than by discipline.
                    if (stubs == null)
                    {
                        throw new InvalidDataException(string.Format(
                            "HydrateCompactedStreaming: no stubs loaded for {0}", fileName));
                    }
                    // The 1st-pass sidecar is written over the WHOLE pre-compaction row set,
                    // but these stubs come from the reconciled parquet, which now holds only
                    // the Stage 5 survivors (issue #4486) - so most of its records have no
                    // entry to land on. Any OTHER missing entry_id is still the parquet drift
                    // the reader rejects. Same order as the batch twin: overlay, then compact.
                    //
                    // The predicate states the FILTER, not what happened to load. Asking
                    // "is this id absent from the stubs I loaded?" is tautological here -
                    // FdrScoresSidecar.TryRead only consults it after its own stub lookup has
                    // already missed - so it answered yes to every record and disabled the
                    // drift check outright rather than narrowing it. A sidecar written from a
                    // different parquet, a different library build (different entry_id
                    // assignment) or a different binary would then be accepted record for
                    // record, every survivor would keep Score = 0.0, and the un-q-gated decoy
                    // zeros would compete in the picked-protein null. Same shape as
                    // FirstPassSurvivorLoader's predicate, which asks the survivor test.
                    OverlayFirstPassSidecar(syntheticInput, fileName, stubs,
                        nameof(HydrateCompactedStreaming), experimentRecords,
                        id => !retainedBaseIds.Contains(id & ScoringTaskShared.BASE_ID_MASK));

                    // The caller's one look at this file's full pre-compaction pool: it fills
                    // in whatever it used to reduce off the resident all-files pool.
                    var tally = new PreCompactionTally { Stubs = stubs.Count };
                    onStubsHydrated?.Invoke(i, fileName, stubs, tally);
                    tallies.Add(tally);

                    stubs.RemoveAll(e => !retainedBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
                    stubs.TrimExcess();

                    // Map the planner's actions onto POST-compaction vec_idx. Every action's
                    // base_id is in the retain set by construction above, so an action entry
                    // present in the parquet is guaranteed to have survived; a miss here means
                    // the same parquet drift the batch twin rejects, with the same message.
                    var idToIdx = new Dictionary<uint, int>(stubs.Count);
                    for (int idx = 0; idx < stubs.Count; idx++)
                        idToIdx[stubs[idx].EntryId] = idx;
                    MapPlannedActions(planned, fileName, reconPath, idToIdx,
                        reconciliationActions, nameof(HydrateCompactedStreaming));

                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                }
            }

            return new RescoreInputs
            {
                PerFileEntries = perFileEntries,
                ReconciliationActions = reconciliationActions,
                RefinedCalibrations = refinedCalibrations,
                PerFileGapFill = perFileGapFill,
                PerFileConsensusTargets = null,
                JoinFileStems = consistency.JoinFileStems ?? new List<string>(),
                GlobalFirstPassBaseIds = consistency.GlobalBaseIds,
                // Already the completed union, so publish it rather than leaving
                // RescoreCompaction.Apply to re-derive it. Apply stays the authority - it
                // recomputes the same set and finds nothing to remove - but a consumer that
                // needs the retained set before Apply runs now has it.
                RetainedBaseIds = retainedBaseIds,
                PreCompactionTallies = tallies,
            };
        }

        /// <summary>
        /// Read ONLY the gap-fill targets and refined calibrations out of each run's envelope -
        /// no parquet rows, no stubs, no action mapping.
        ///
        /// <para>This exists for the one consumer that genuinely spans runs: the Stage 7 pool
        /// rebuild in <c>PerFileRescoreTask.Rehydrate</c>, which overlays every run's reconciled
        /// parquet and needs the gap-fill targets to restore the detections gap-fill transferred
        /// into runs that did not find them independently. Publishing that map empty costs
        /// exactly those detections - measured on Stellar as 94 missing <c>RetentionTimes</c>
        /// rows and <c>NRunsDetected</c> falling 3 -> 2.</para>
        ///
        /// <para>It IS an all-runs read, and it is the honest scope of the remaining coupling:
        /// gap-fill is a cross-run product, so a consumer that rebuilds a cross-run pool needs
        /// all of it. It is far smaller than what it replaces - envelope JSON only, no parquet
        /// pass and no stub materialisation - and a per-run rescore never calls it, because
        /// nothing on that path rebuilds the pool.</para>
        /// </summary>
        public static void ReadGapFillAndCalibrations(
            IEnumerable<string> parquetPaths,
            Dictionary<string, List<GapFillTarget>> perFileGapFill,
            Dictionary<string, RTCalibration> refinedCalibrations,
            LibraryStringInterner sequencePool = null)
        {
            foreach (string parquetPath in parquetPaths)
            {
                string syntheticInput = SyntheticInputFromParquet(parquetPath);
                string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                if (string.IsNullOrEmpty(fileName))
                    continue;
                string reconPath = ReconciliationFile.PathForInput(syntheticInput);
                if (!File.Exists(reconPath))
                    continue;
                var envelope = LoadEnvelope(reconPath, nameof(ReadGapFillAndCalibrations));
                CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                    sequencePool);
            }
        }

        /// <summary>
        /// Hydrate the Stage-6 rescore inputs for ONE run, from that run's own artifacts and the
        /// analysis-wide summaries - never from another run's.
        ///
        /// <para><b>The single <paramref name="parquetPath"/> is the point.</b> Its all-runs
        /// sibling <see cref="HydrateCompactedStreaming"/> takes an <c>IList&lt;string&gt;</c> of
        /// every run's parquet, and that signature is what let a per-run task become a join: on
        /// an 86-run plate the caller spent 8m42s and 17.2 GB loading every run's stubs before
        /// rescoring one, and the rescore loop then RE-READ each run's parquet through
        /// <c>FirstPassSurvivorLoader</c> anyway and overwrote what had been loaded. The work
        /// was discarded, not used. Only the loop's discipline had ever bounded it, and
        /// discipline is what failed - so the fix is a signature that cannot express the
        /// mistake, per CRITICAL-RULES' "strengthen the verifier rather than the wording".</para>
        ///
        /// <para>What makes this correct rather than merely cheaper: every input a rescore needs
        /// is a per-run slice. <c>MultiChargeConsensus.SelectRescoreTargets</c> reads one run's
        /// entries; the reconciliation actions, gap-fill targets and refined calibration all come
        /// out of that run's own <c>reconciliation.json</c>; and the compaction predicate is the
        /// analysis-wide retained base_id set, which is library-bounded and passed in. The
        /// regression gate's mode 3 has always proved this by handing a node ONE run and getting
        /// byte-identical output - this method is that path, used as the loop body.</para>
        ///
        /// <para><paramref name="loadStubs"/> must return the run's FULL pre-compaction stub
        /// list; <paramref name="onStubsHydrated"/> is the caller's one look at it (per-run
        /// tallies, the <c>--model-diagnostics</c> fold) before compaction drops the
        /// non-survivors. Cross-run envelope agreement is deliberately NOT checked here: a node
        /// holding one run has no sibling to compare against, so that check belongs to a caller
        /// that legitimately has several.</para>
        /// </summary>
        public static RunRescoreInputs HydrateOneRun(
            string parquetPath,
            HashSet<uint> retainedBaseIds,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<string, string, List<FdrEntry>> loadStubs,
            Action<string, List<FdrEntry>, PreCompactionTally> onStubsHydrated = null,
            LibraryStringInterner sequencePool = null)
        {
            if (parquetPath == null)
                throw new ArgumentNullException(nameof(parquetPath));
            if (retainedBaseIds == null)
                throw new ArgumentNullException(nameof(retainedBaseIds));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));

            string syntheticInput = SyntheticInputFromParquet(parquetPath);
            string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
            if (string.IsNullOrEmpty(fileName))
            {
                throw new InvalidDataException(string.Format(
                    "HydrateOneRun: could not derive file_name from parquet path {0}", parquetPath));
            }

            string reconPath = ReconciliationFile.PathForInput(syntheticInput);
            var envelope = LoadEnvelope(reconPath, nameof(HydrateOneRun));
            var planned = PlanActions(envelope);

            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                sequencePool);

            var stubs = loadStubs(fileName, parquetPath);
            if (stubs == null)
            {
                throw new InvalidDataException(string.Format(
                    "HydrateOneRun: no stubs loaded for {0}", fileName));
            }

            // Same order as the all-runs sibling: overlay, look, then compact. The
            // expectedAbsent predicate states the FILTER for the same reason it does there -
            // the sidecar was written over the whole pre-compaction row set, and a reconciled
            // parquet holds only survivors, so most records legitimately have no entry to land
            // on while any OTHER miss is real parquet drift.
            OverlayFirstPassSidecar(syntheticInput, fileName, stubs, nameof(HydrateOneRun),
                experimentRecords,
                id => !retainedBaseIds.Contains(id & ScoringTaskShared.BASE_ID_MASK));

            var tally = new PreCompactionTally { Stubs = stubs.Count };
            onStubsHydrated?.Invoke(fileName, stubs, tally);

            stubs.RemoveAll(e => !retainedBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
            stubs.TrimExcess();

            var idToIdx = new Dictionary<uint, int>(stubs.Count);
            for (int idx = 0; idx < stubs.Count; idx++)
                idToIdx[stubs[idx].EntryId] = idx;
            var actions = new Dictionary<(string, int), ReconcileAction>();
            MapPlannedActions(planned, fileName, reconPath, idToIdx, actions, nameof(HydrateOneRun));

            perFileGapFill.TryGetValue(fileName, out var gapFill);
            refinedCalibrations.TryGetValue(fileName, out var refinedCalibration);
            return new RunRescoreInputs
            {
                FileName = fileName,
                Survivors = stubs,
                ReconciliationActions = actions,
                GapFill = gapFill,
                RefinedCalibration = refinedCalibration,
                JoinFileStems = NormalizeStems(envelope.FileStems),
                GlobalFirstPassBaseIds = new HashSet<uint>(envelope.FirstPassBaseIds),
                Tally = tally,
            };
        }

        /// <summary>
        /// Overlay the first-pass FDR statistics onto <paramref name="stubs"/>: the RUN-scope
        /// SVM score, run q-values and PEP from <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c>,
        /// and the EXPERIMENT-scope q-values from <paramref name="experimentRecords"/>, which
        /// the caller read once from the analysis-wide
        /// <c>&lt;blib-stem&gt;.1st-pass.fdr_experiment.bin</c> (format v5, issue #4486).
        /// <c>expected_pass = FirstPass</c>: the planner's actions were computed against
        /// first-pass FDR, and the compaction predicate uses first-pass q-values. The stub
        /// list must be the FULL pre-compaction set - the sidecar was written before
        /// compaction and its reader requires a superset of its records.
        ///
        /// <para>Both halves are restored because a hydrated stub has to be indistinguishable
        /// from the resident one it stands in for. The experiment q-values in particular reach
        /// the <c>--model-diagnostics</c> report through
        /// <c>ScoringTaskShared.FeedModelDiagnostics</c>, and that report is compared against a
        /// committed golden - so a stub rehydrated without them would not fail here, it would
        /// fail as a wrong number in a report two stages later.</para>
        ///
        /// <para><c>expectedAbsent</c> is non-null ONLY for a caller whose list is
        /// legitimately a subset of that: today just <see cref="HydrateCompactedStreaming"/>,
        /// which loads from the reconciled parquet, and that parquet now carries only the
        /// Stage 5 survivors (issue #4486). The batch path stays strict, because a lean list
        /// THERE means the caller was handed the wrong pool - which is exactly what
        /// <c>AssertBatchOverlayRejectsLeanStubs</c> pins, and what caught this when the
        /// tolerance was first applied to both paths at once.</para>
        /// </summary>
        private static void OverlayFirstPassSidecar(
            string syntheticInput, string fileName, List<FdrEntry> stubs, string context,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<uint, bool> expectedAbsent = null)
        {
            string sidecarPath = FdrScoresSidecar.Pass1Path(syntheticInput);
            // The experiment records go THROUGH the reader, not over the stub list afterwards:
            // the reader applies them to the entries it binds a record to, which is the set a
            // by-entry_id loop would wrongly widen to include gap-fill stubs.
            if (!FdrScoresSidecar.TryRead(sidecarPath, stubs, FdrScoresSidecar.Pass.FirstPass,
                    expectedAbsent, experimentRecords))
            {
                throw new InvalidDataException(string.Format(
                    "{0}: failed to overlay .1st-pass.fdr_scores.bin for {1} (expected at {2})",
                    context, fileName, sidecarPath));
            }
        }

        /// <summary>
        /// Read one <c>reconciliation.json</c> envelope, wrapping any reader failure in
        /// <see cref="InvalidDataException"/> with the offending path named.
        /// </summary>
        private static ReconciliationFile LoadEnvelope(string reconPath, string context)
        {
            try
            {
                return ReconciliationFile.Load(reconPath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    "{0}: failed to read {1}: {2}", context, reconPath, ex.Message), ex);
            }
        }
        /// <summary>
        /// The planner's non-Keep actions for one file, flattened out of the envelope's two
        /// homogeneous arrays in the order the hydrators consume them (<c>use_cwt_peak</c>
        /// first, then <c>forced_integration</c>, each in envelope order). Both hydrate paths
        /// go through this so they agree on action content, on which action wins when the
        /// planner emitted two for one <c>entry_id</c> (the later one, as a
        /// <c>vec_idx</c>-keyed assignment would), and on which missing <c>entry_id</c>
        /// raises the drift error first.
        /// </summary>
        private static List<PlannedAction> PlanActions(ReconciliationFile envelope)
        {
            var planned = new List<PlannedAction>(
                (envelope.UseCwtPeakActions?.Count ?? 0) + (envelope.ForcedIntegrationActions?.Count ?? 0));
            if (envelope.UseCwtPeakActions != null)
            {
                foreach (var entry in envelope.UseCwtPeakActions)
                {
                    planned.Add(new PlannedAction(entry.EntryId, @"use_cwt_peak",
                        new ReconcileAction.UseCwtPeak(
                            (int)entry.CandidateIdx, entry.StartRt, entry.ApexRt, entry.EndRt)));
                }
            }
            if (envelope.ForcedIntegrationActions != null)
            {
                foreach (var entry in envelope.ForcedIntegrationActions)
                {
                    planned.Add(new PlannedAction(entry.EntryId, @"forced_integration",
                        new ReconcileAction.ForcedIntegration(entry.ExpectedRt, entry.HalfWidth)));
                }
            }
            return planned;
        }

        /// <summary>
        /// Rehome one file's <see cref="PlanActions"/> output from <c>entry_id</c> onto the
        /// <c>(file_name, vec_idx)</c> keys the rescore engine consumes, using
        /// <paramref name="idToIdx"/> over whichever stub list the caller holds (the full
        /// pre-compaction list on the batch path, the survivors on the streaming path). An
        /// <c>entry_id</c> the stub list does not carry is parquet/boundary drift and stops
        /// the run: a Stage 6 worker proceeding with missing actions would scramble gap-fill.
        /// </summary>
        private static void MapPlannedActions(
            List<PlannedAction> planned,
            string fileName,
            string reconPath,
            Dictionary<uint, int> idToIdx,
            Dictionary<(string, int), ReconcileAction> reconciliationActions,
            string context)
        {
            foreach (var action in planned)
            {
                if (!idToIdx.TryGetValue(action.EntryId, out int vecIdx))
                {
                    throw new InvalidDataException(string.Format(
                        "{0}: {1} entry_id {2} in {3} not found in stubs (parquet drift?)",
                        context, action.Kind, action.EntryId, reconPath));
                }
                reconciliationActions[(fileName, vecIdx)] = action.Action;
            }
        }

        /// <summary>
        /// Capture one envelope's refined RT calibration and gap-fill targets into the
        /// per-file dictionaries. A null <c>refined_rt_calibration</c> (Stage 5 refit failed)
        /// and an empty gap-fill list both leave the file absent, which is how the rescore
        /// engine reads "nothing to do here".
        ///
        /// <para>The gap-fill sequences go through <paramref name="sequencePool"/> for the same
        /// reason the stubs do: Json.NET hands out a fresh string per property, and these
        /// targets are retained for EVERY file at once (5.5 K - 12.3 K per file, ~2 M at 257
        /// CHS files). Null pool leaves them as read.</para>
        /// </summary>
        private static void CaptureCalibrationAndGapFill(
            ReconciliationFile envelope,
            string fileName,
            Dictionary<string, RTCalibration> refinedCalibrations,
            Dictionary<string, List<GapFillTarget>> perFileGapFill,
            LibraryStringInterner sequencePool)
        {
            if (envelope.RefinedRtCalibration != null)
            {
                refinedCalibrations[fileName] = RTCalibration.FromModelParams(
                    envelope.RefinedRtCalibration.LibraryRts,
                    envelope.RefinedRtCalibration.FittedRts,
                    envelope.RefinedRtCalibration.AbsResiduals,
                    envelope.RefinedRtCalibration.ResidualSd);
            }

            if (envelope.GapFillTargets != null && envelope.GapFillTargets.Count > 0)
            {
                var gapFill = new List<GapFillTarget>(envelope.GapFillTargets.Count);
                foreach (var g in envelope.GapFillTargets)
                {
                    gapFill.Add(new GapFillTarget
                    {
                        TargetEntryId = g.TargetEntryId,
                        DecoyEntryId = g.DecoyEntryId,
                        ExpectedRt = g.ExpectedRt,
                        HalfWidth = g.HalfWidth,
                        ModifiedSequence = sequencePool != null
                            ? sequencePool.Intern(g.ModifiedSequence)
                            : g.ModifiedSequence,
                        Charge = g.Charge,
                    });
                }
                perFileGapFill[fileName] = gapFill;
            }
        }

        /// <summary>
        /// One planner action paired with the <c>entry_id</c> it targets and the envelope
        /// array it came from (used only to name the array in the drift error).
        /// </summary>
        private sealed class PlannedAction
        {
            public uint EntryId { get; }
            public string Kind { get; }
            public ReconcileAction Action { get; }

            public PlannedAction(uint entryId, string kind, ReconcileAction action)
            {
                EntryId = entryId;
                Kind = kind;
                Action = action;
            }
        }

        /// <summary>
        /// The two fields every sibling <c>reconciliation.json</c> in a join must agree on,
        /// captured from the first envelope seen and checked against the rest. A disagreement
        /// means the on-disk envelopes came from different planner steps (corrupted
        /// hand-off): silently taking the first file's values would compact its siblings
        /// against the wrong base_ids and compute a join-wide reconciliation hash the SecondPassFDR
        /// node rejects. Mirrors the consistency checks in Rust's <c>hydrate_for_rescore</c>.
        /// </summary>
        private sealed class EnvelopeConsistency
        {
            /// <summary>
            /// Join-wide first-pass passing base_ids (v3 required field), identical in every
            /// file's envelope by construction. <see cref="RescoreCompaction"/> compacts to
            /// exactly this set (unioned with the action targets) so a per-file worker keeps
            /// the same entries the in-memory straight-through pipeline keeps.
            /// </summary>
            public HashSet<uint> GlobalBaseIds { get; private set; }

            /// <summary>Full set of file stems participating in the planner's join (v2+).</summary>
            public List<string> JoinFileStems { get; private set; }

            /// <summary>
            /// The library and search hashes of the first envelope seen, against which every
            /// sibling is checked. These are the O(1) provenance identity of the planner step
            /// that wrote the envelope, and they replaced the whole-set first_pass_base_ids
            /// comparison - see <see cref="Check"/>.
            /// </summary>
            private string LibraryHash { get; set; }
            private string SearchHash { get; set; }

            public void Check(ReconciliationFile envelope, string reconPath, string context)
            {
                // Materialize the join-wide set from the FIRST envelope only, and do not compare
                // the siblings' copies against it. The comparison used to be this class's main
                // job; what retired it is that the set is no longer what compaction consumes -
                // RetainedBaseIdSidecar carries the analysis-wide retained set, which is a
                // superset of this one, and every arm compacts to THAT. Re-deriving a HashSet
                // per envelope only to SetEquals it cost 446 x 744,943 inserts and probes on the
                // CHS cohort to re-confirm a field nothing reads any more.
                //
                // The provenance guard it provided is kept below, moved onto library_hash /
                // search_hash / file_stems: those are O(1) and O(stems) per envelope, they are
                // what actually identify the planner step, and unlike the base_id array they
                // stay meaningful for a worker holding a single run.
                if (GlobalBaseIds == null)
                    GlobalBaseIds = new HashSet<uint>(envelope.FirstPassBaseIds);

                if (LibraryHash == null)
                {
                    LibraryHash = envelope.LibraryHash;
                    SearchHash = envelope.SearchHash;
                }
                else if (!string.Equals(LibraryHash, envelope.LibraryHash, StringComparison.Ordinal) ||
                         !string.Equals(SearchHash, envelope.SearchHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(string.Format(
                        "{0}: reconciliation.json {1} was written against a different library or " +
                        "search configuration than its siblings (planner inconsistency): " +
                        "library {2} vs {3}, search {4} vs {5}.",
                        context, reconPath, LibraryHash, envelope.LibraryHash,
                        SearchHash, envelope.SearchHash));
                }

                // ReconciliationFile.Load already rejects any envelope whose format_version
                // != CurrentFormatVersion (currently 3), so FileStems here must be the
                // planner's full join file set -- a non-empty list, identical across every
                // envelope produced by a single planner step. Any disagreement (including
                // unexpected empty stems) indicates a corrupted hand-off.
                var envelopeStems = NormalizeStems(envelope.FileStems);
                if (JoinFileStems == null)
                {
                    JoinFileStems = envelopeStems;
                }
                else if (!StemsEqual(JoinFileStems, envelopeStems))
                {
                    throw new InvalidDataException(string.Format(
                        "{0}: reconciliation.json {1} carries a different file_stems set than " +
                        "its siblings (planner inconsistency). Expected: [{2}]; got: [{3}]",
                        context, reconPath,
                        string.Join(", ", JoinFileStems),
                        string.Join(", ", envelopeStems)));
                }
            }
        }

        /// <summary>
        /// Sort + dedup a list of file stems (Ordinal). Returns a new list;
        /// the input is not mutated. Empty / null input becomes an empty
        /// list. Used to canonicalize the <c>file_stems</c> field from
        /// each <c>reconciliation.json</c> envelope before consistency
        /// checks across siblings.
        /// </summary>
        private static List<string> NormalizeStems(IList<string> stems)
        {
            if (stems == null || stems.Count == 0)
                return new List<string>();
            var result = new List<string>(stems.Count);
            foreach (var s in stems)
            {
                if (!string.IsNullOrEmpty(s))
                    result.Add(s);
            }
            result.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to dedup adjacent identical stems immediately below; equal keys are byte-identical so tie order is irrelevant
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (string.Equals(result[i], result[i - 1], StringComparison.Ordinal))
                    result.RemoveAt(i);
            }
            return result;
        }

        /// <summary>
        /// Ordinal element-wise equality on two pre-normalized stem lists.
        /// </summary>
        private static bool StemsEqual(IList<string> a, IList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Inverse of <c>scores_path_for_input</c>: given
        /// <c>/data/sample1.scores.parquet</c> (or its reconciled sibling
        /// <c>/data/sample1.scores-reconciled.parquet</c>), produce a synthetic
        /// input path <c>/data/sample1.mzML</c> whose stem matches what the
        /// worker used. This lets the worker reuse the existing
        /// path-derivation helpers (FDR sidecars, calibration JSON,
        /// reconciliation JSON) without duplicating them. The synthetic
        /// path is never opened — only its components are inspected.
        /// Mirrors Rust's <c>synthetic_input_from_parquet</c>.
        /// </summary>
        public static string SyntheticInputFromParquet(string parquetPath)
        {
            if (parquetPath == null) throw new ArgumentNullException(nameof(parquetPath));
            // GetFileNameWithoutExtension returns "" not null for valid paths
            // and throws on invalid input, so the result is never null here.
            string stem = Path.GetFileNameWithoutExtension(parquetPath);
            // Strip the trailing ".scores-reconciled" (Stage 6 reconciled output)
            // or ".scores" (Stage 4 output). These two tokens never collide with
            // an input stem because Stage 4 always appends exactly ".scores"
            // (so the only way a name ends in ".scores-reconciled" is Stage 6).
            // GetFileNameWithoutExtension of "x.scores-reconciled.parquet" is
            // "x.scores-reconciled"; check the longer token first.
            const string ReconciledScoresSuffix = ".scores-reconciled";
            const string ScoresSuffix = ".scores";
            if (stem.EndsWith(ReconciledScoresSuffix, StringComparison.Ordinal))
                stem = stem.Substring(0, stem.Length - ReconciledScoresSuffix.Length);
            else if (stem.EndsWith(ScoresSuffix, StringComparison.Ordinal))
                stem = stem.Substring(0, stem.Length - ScoresSuffix.Length);
            string parent = Path.GetDirectoryName(parquetPath);
            string filename = stem + ".mzML";
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }
    }
}
