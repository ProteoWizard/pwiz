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
        /// + PEP + <c>RunProteinQvalue</c> overlaid from the
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar. File order
        /// matches the order of <c>parquetPaths</c> passed to
        /// <see cref="RescoreHydration.HydrateForRescore"/>.
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
    /// Hydrate the Stage 5 → Stage 6 boundary file pair into the
    /// in-memory state needed to drive a per-file rescore. Mirrors
    /// <c>hydrate_for_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// </summary>
    public static class RescoreHydration
    {
        /// <summary>
        /// Read each <c>&lt;stem&gt;.scores.parquet</c> in
        /// <paramref name="parquetPaths"/>, overlay the matching
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> sidecar (v3 format,
        /// pass = FirstPass), and parse the matching
        /// <c>&lt;stem&gt;.reconciliation.json</c> envelope into the
        /// per-file action map + refined calibration + gap-fill list.
        ///
        /// File names are extracted from the parquet stem with the
        /// <c>.scores</c> suffix stripped, mirroring Rust's
        /// <c>synthetic_input_from_parquet</c>. The output preserves the
        /// input ordering.
        ///
        /// Throws <see cref="InvalidDataException"/> on any per-file boundary
        /// file that is missing, unreadable, or fails its format-version /
        /// count checks. Does not silently fall back to partial state — a
        /// Stage 6 worker that proceeded with one file's planner output
        /// missing would scramble gap-fill results across files.
        /// </summary>
        public static RescoreInputs HydrateForRescore(IList<string> parquetPaths)
        {
            if (parquetPaths == null) throw new ArgumentNullException(nameof(parquetPaths));
            if (parquetPaths.Count == 0)
                throw new InvalidDataException("HydrateForRescore: parquetPaths is empty");

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>(parquetPaths.Count);
            foreach (var parquetPath in parquetPaths)
            {
                string syntheticInput = SyntheticInputFromParquet(parquetPath);
                string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                if (string.IsNullOrEmpty(fileName))
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateForRescore: could not derive file_name from parquet path {0}",
                        parquetPath));
                }

                // Stubs from parquet (entry_id, charge, modseq, RTs,
                // parquet_index assigned by LoadFdrStubsFromParquet).
                List<FdrEntry> stubs;
                try
                {
                    stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format(
                        "HydrateForRescore: failed to load stubs from {0}: {1}",
                        parquetPath, ex.Message), ex);
                }
                perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
            }

            return HydrateReconciliationOverlay(perFileEntries, parquetPaths);
        }

        /// <summary>
        /// Overlay the per-file 1st-pass FDR sidecars and parse the per-file
        /// <c>reconciliation.json</c> envelopes onto an already-loaded
        /// <paramref name="perFileEntries"/> list. The per-file element at
        /// index <c>i</c> in <paramref name="perFileEntries"/> must
        /// correspond to <paramref name="parquetPaths"/>[<c>i</c>] — the
        /// fileName key is rederived from the parquet path for the sidecar
        /// path computation.
        ///
        /// Used by both the worker-mode <see cref="HydrateForRescore"/>
        /// wrapper (which loads stubs first) and the in-pipeline joinOnly
        /// dispatch (which already has stubs loaded with PIN features +
        /// calibration siblings, and just needs the rescore overlay added
        /// to share state with FirstPassFDR / PerFileRescore).
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
            IList<string> parquetPaths)
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
            // HEARTBEAT_SECONDS (30 s) tick, so one slow file cannot reopen the gap.
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
                        nameof(HydrateReconciliationOverlay));

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
                    CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill);
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
        /// <c>first_pass_base_ids</c> the v3 envelope carries and (b) the base_ids of every
        /// entry the planner emitted an action for, whose <c>entry_id</c>s the same envelope
        /// carries. Both come off the SMALL on-disk envelopes, so a pre-pass over the
        /// envelopes alone fixes the retained set before a single parquet row is read:
        ///
        ///   pass 1 - read every <c>reconciliation.json</c> (small), run the sibling
        ///            consistency checks, and union the retain set;
        ///   pass 2 - per file, in <paramref name="parquetPaths"/> order: load that file's
        ///            stubs, overlay its <c>.1st-pass.fdr_scores.bin</c>, compact to the
        ///            retain set, keep ONLY the survivors, move on.
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
            Action<int, string, List<FdrEntry>, PreCompactionTally> onStubsHydrated)
        {
            if (perFileEntries == null)
                throw new ArgumentNullException(nameof(perFileEntries));
            if (parquetPaths == null)
                throw new ArgumentNullException(nameof(parquetPaths));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));
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

            // Pass 1: envelopes only. Everything kept here is small and is retained by the
            // returned bundle anyway (actions, gap-fill, refined calibrations) EXCEPT the
            // per-file first_pass_base_ids array, which is dropped with its envelope as soon
            // as the sibling check has consumed it - only the single shared set survives.
            var fileNames = new List<string>(nFiles);
            var syntheticInputs = new List<string>(nFiles);
            var reconPaths = new List<string>(nFiles);
            var plannedByFile = new List<List<PlannedAction>>(nFiles);
            // The retained set: the join-wide first-pass base_ids UNION the base_ids of every
            // planner action target, across ALL files. Both terms are what
            // RescoreCompaction.Apply unions, and the union has to be complete before ANY
            // file is filtered - file A can retain a base_id only because file B has an
            // action on it.
            var retainBaseIds = new HashSet<uint>();
            // One reporter across BOTH passes, 2 * nFiles units. Pass 1 reads only the small
            // on-disk envelopes and finishes in seconds, so a reporter scoped to it alone
            // printed 100% before a single parquet row was read and then left pass 2 - the
            // ~1.19 GB per-file load that runs for minutes a file - completely unreported.
            // Spanning both keeps the heartbeat on the pass that actually takes the time.
            using (var hydrateProgress = new ProgressReporter(
                       @"Hydrating reconciliation bundle", 2L * nFiles))
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
                    foreach (var action in planned)
                        retainBaseIds.Add(action.EntryId & ScoringTaskShared.BASE_ID_MASK);
                    CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill);

                    fileNames.Add(fileName);
                    syntheticInputs.Add(syntheticInput);
                    reconPaths.Add(reconPath);
                    plannedByFile.Add(planned);
                }
                retainBaseIds.UnionWith(consistency.GlobalBaseIds);

                // Pass 2: one file's pre-compaction pool resident at a time.
                for (int i = 0; i < nFiles; i++)
                {
                    hydrateProgress.Report(nFiles + i);
                    string fileName = fileNames[i];
                    var stubs = loadStubs(i, fileName, parquetPaths[i]);
                    if (stubs == null)
                    {
                        throw new InvalidDataException(string.Format(
                            "HydrateCompactedStreaming: no stubs loaded for {0}", fileName));
                    }
                    // The sidecar overlay needs the FULL pre-compaction list: it was written
                    // pre-compaction and its reader requires the stub list to be a superset of
                    // its records. Same order as the batch twin - overlay, then compact.
                    OverlayFirstPassSidecar(syntheticInputs[i], fileName, stubs,
                        nameof(HydrateCompactedStreaming));

                    // The caller's one look at this file's full pre-compaction pool: it fills
                    // in whatever it used to reduce off the resident all-files pool.
                    var tally = new PreCompactionTally { Stubs = stubs.Count };
                    onStubsHydrated?.Invoke(i, fileName, stubs, tally);
                    tallies.Add(tally);

                    stubs.RemoveAll(e => !retainBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
                    stubs.TrimExcess();

                    // Map the planner's actions onto POST-compaction vec_idx. Every action's
                    // base_id is in the retain set by construction above, so an action entry
                    // present in the parquet is guaranteed to have survived; a miss here means
                    // the same parquet drift the batch twin rejects, with the same message.
                    var idToIdx = new Dictionary<uint, int>(stubs.Count);
                    for (int idx = 0; idx < stubs.Count; idx++)
                        idToIdx[stubs[idx].EntryId] = idx;
                    MapPlannedActions(plannedByFile[i], fileName, reconPaths[i], idToIdx,
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
                PreCompactionTallies = tallies,
            };
        }

        /// <summary>
        /// Overlay SVM scores + 4 q-values + PEP + RunProteinQvalue from
        /// <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c> v3 onto <paramref name="stubs"/>.
        /// <c>expected_pass = FirstPass</c>: the planner's actions were computed against
        /// first-pass FDR, and the compaction predicate uses first-pass q-values. The stub
        /// list must be the FULL pre-compaction set - the sidecar was written before
        /// compaction and its reader requires a superset of its records.
        /// </summary>
        private static void OverlayFirstPassSidecar(
            string syntheticInput, string fileName, List<FdrEntry> stubs, string context)
        {
            string sidecarPath = FdrScoresSidecar.Pass1Path(syntheticInput);
            if (!FdrScoresSidecar.TryRead(sidecarPath, stubs, FdrScoresSidecar.Pass.FirstPass))
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
        /// </summary>
        private static void CaptureCalibrationAndGapFill(
            ReconciliationFile envelope,
            string fileName,
            Dictionary<string, RTCalibration> refinedCalibrations,
            Dictionary<string, List<GapFillTarget>> perFileGapFill)
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
                        ModifiedSequence = g.ModifiedSequence,
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

            public void Check(ReconciliationFile envelope, string reconPath, string context)
            {
                var envelopeBaseIds = new HashSet<uint>(envelope.FirstPassBaseIds);
                if (GlobalBaseIds == null)
                {
                    GlobalBaseIds = envelopeBaseIds;
                }
                else if (!GlobalBaseIds.SetEquals(envelopeBaseIds))
                {
                    throw new InvalidDataException(string.Format(
                        "{0}: reconciliation.json {1} carries a different first_pass_base_ids " +
                        "set than its siblings (planner inconsistency): {2} vs {3} base_ids.",
                        context, reconPath, GlobalBaseIds.Count, envelopeBaseIds.Count));
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
