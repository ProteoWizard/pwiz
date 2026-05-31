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
using System.Diagnostics;
using System.IO;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Aggregate counts returned by <see cref="PerFileRescoreTask.ExecuteRescore"/>.
    /// Mirrors <c>RescoreStats</c> in
    /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
    /// </summary>
    public class RescoreStats
    {
        /// <summary>
        /// Total entries re-scored across all files: existing
        /// (consensus + reconciliation) plus gap-fill (CWT + forced).
        /// </summary>
        public int TotalRescored { get; set; }

        /// <summary>
        /// Number of non-Keep reconciliation actions executed across all files.
        /// </summary>
        public int TotalReconciliation { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the CWT-detected pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapCwt { get; set; }

        /// <summary>
        /// Gap-fill targets that landed via the forced-integration pass.
        /// Phase 2 of the port; zero today.
        /// </summary>
        public int TotalGapForced { get; set; }
    }

    /// <summary>
    /// Stage 6 per-file rescore phase: re-scores each input file's
    /// previously-scored entries against the consensus + reconciliation
    /// boundaries produced by the first-join phase, runs the gap-fill
    /// two-pass for missing precursors, and writes the reconciled
    /// results back into the per-file <c>.scores.parquet</c>. The HPC
    /// "second per-file fan-out" boundary in the
    /// <c>Osprey-workflow.html</c> view -- each input file's rescore is
    /// independent of the others.
    ///
    /// Single entry point: <see cref="Run"/> is invoked by
    /// <see cref="AnalysisPipeline"/>'s task driver during both
    /// straight-through pipeline runs and the stage6 worker mode
    /// (<c>--join-at-pass=1 --no-join --input-scores</c>). The worker
    /// mode previously had a separate <c>RunWorker</c> entry that
    /// hand-assembled the upstream hydration; Phase C collapsed that
    /// path so the canonical pipeline's StartAt/StopAfter + the
    /// upstream tasks' lazy-rehydrate accessors handle it. Run reads
    /// upstream state from sibling tasks through
    /// <c>ctx.GetTask&lt;PerFileScoringTask&gt;()</c> and
    /// <c>ctx.GetTask&lt;FirstJoinTask&gt;()</c>, dispatches into
    /// <see cref="ExecuteRescore"/>, then runs the per-process
    /// diagnostic-writer close + cross-impl bisection dump. Inherits
    /// the scoring engine (RunCoelutionScoring, LoadLibrary,
    /// GenerateDecoys, ExtractIsolationWindows, ...) from
    /// <see cref="AbstractScoringTask"/>.
    /// </summary>
    internal sealed class PerFileRescoreTask : AbstractScoringTask
    {
        // Captured during Run so MergeNodeTask (downstream) can reach
        // the post-rescore version. Per the ownership-transfer semantics
        // of the pipeline: this task is the producer of the post-rescore
        // perFileEntries; consumers query us rather than
        // PerFileScoringTask. When Run is a no-op (no planning state)
        // the list reference falls through unchanged from
        // PerFileScoringTask.
        private List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;

        // Phase B lazy-rehydrate gate. See PerFileScoringTask for the
        // mechanism.
        private bool _runOrHydrated;

        public override string Name => @"PerFileRescore";

        /// <summary>
        /// The post-rescore per-file entries. Mutated in place by
        /// <see cref="ExecuteRescore"/>; when this task short-circuits
        /// (no FirstJoinTask plan) the list is the unchanged upstream
        /// reference. Lazy-rehydrates by invoking <see cref="Run"/> on
        /// the first call when this task was skipped by the driver
        /// (i.e. <see cref="PipelineContext.StartAtTask"/> is downstream),
        /// so consumers always see populated state.
        /// </summary>
        public List<KeyValuePair<string, List<FdrEntry>>> GetPerFileEntries(PipelineContext ctx)
        {
            if (!_runOrHydrated) Run(ctx);
            if (_perFileEntries == null)
                throw new InvalidOperationException(
                    @"PerFileRescoreTask.GetPerFileEntries called before Run populated the field.");
            return _perFileEntries;
        }

        // Phase B resume surface. The reconciled parquet is written to a
        // SEPARATE <stem>.reconciled.scores.parquet sibling, leaving the
        // upstream PerFileScoringTask <stem>.scores.parquet intact (so a
        // partial Stage 6 crash can no longer half-rewrite the Stage 4
        // output, and downstream readers can fall back to the original
        // for files that had no reconciliation work). ValidityKey adds the
        // reconciliation parameter hash because the rescored content
        // depends on it.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
            {
                yield return FdrScoresSidecar.Pass1Path(input);
                if (ctx.Config.Reconciliation != null && ctx.Config.Reconciliation.Enabled)
                    yield return ReconciliationFile.PathForInput(input);
            }
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
                yield return ParquetScoreCache.GetReconciledScoresPath(input);
        }

        public override string ValidityKey(PipelineContext ctx)
        {
            return base.ValidityKey(ctx)
                + @";reconciliation=" + ctx.Config.ReconciliationParameterHash();
        }

        public override bool Run(PipelineContext ctx)
        {
            if (_runOrHydrated) return true;
            _runOrHydrated = true;
            _ctx = ctx;
            var perFileScoring = ctx.GetTask<PerFileScoringTask>();
            _perFileEntries = perFileScoring.GetPerFileEntries(ctx);

            // Hard short-circuit for --join-at-pass=2: every input parquet
            // already has osprey.reconciled = "true" (asserted by
            // ParquetScoreCache.CheckParquetMetadata when ExpectReconciledInput
            // is set), so Stage 5 first-pass Percolator AND Stage 6
            // planning / rescore have ALREADY been performed upstream by
            // the worker nodes that wrote those parquets. We must NOT
            // touch FirstJoinTask here -- doing so transitively triggers
            // FirstJoinTask.Run via EnsureHydrated, which re-runs Stage 5
            // first-pass Percolator from scratch on the reconciled parquets
            // (producing wildly different action counts than the planner
            // saw on the raw Stage 4 inputs) and then attempts a Stage 6
            // rescore that needs mzML files the merge node does not have
            // (in production HPC, the merge node ships only sidecars +
            // reconciled parquets, no mzMLs). MergeNodeTask is responsible
            // for 2nd-pass Percolator (Bug C) and protein FDR + blib
            // output starting from this hydrated, reconciled state.
            // Mirrors Rust pipeline.rs:3313-3344 which gates the entire
            // Stage 5+6 block on `!config.expect_reconciled_input`.
            //
            // Compaction still needs to run though: PerFileScoringTask's
            // bundle-hydration path loads ALL entries from the parquet,
            // including ones that failed first-pass FDR. FirstJoinTask's
            // normal flow would run this compaction inline after first-pass
            // Percolator (and we're skipping FirstJoinTask entirely here).
            // Without it, MergeNodeTask's 2nd-pass Percolator would train
            // on ~3x too many entries -- specifically the non-passing
            // first-pass entries whose 1st-pass q-values are 1.0 -- and
            // the SVM would learn a much worse decision boundary than the
            // in-memory pipeline's, producing different per-precursor
            // scores and different protein-FDR results. The compaction
            // reads first-pass q-values that are already overlaid onto
            // each entry from the .1st-pass.fdr_scores.bin sidecar by
            // PerFileScoringTask's bundle hydration; no fresh FDR
            // computation needed.
            if (ctx.Config.ExpectReconciledInput)
            {
                var bundle = perFileScoring.GetRescoreInputs(ctx);
                if (bundle != null)
                {
                    // First-pass protein FDR BEFORE compaction. The 1st-pass FDR
                    // sidecar v3 already carries RunProteinQvalue from the original
                    // straight-through pipeline, but Rust pipeline.rs:4292 (gated by
                    // `!can_skip_fdr || config.expect_reconciled_input`) recomputes
                    // it inline in the --join-at-pass=2 path. The recompute uses the
                    // post-rehydration detected_peptides set + best_peptide_scores
                    // (which differ from the original write-time inputs whenever any
                    // upstream rebuild has nudged peptide q-values or score values
                    // even at the ULP level). Without this matching recompute on the
                    // C# side, the protein-rescue branch of compaction below sees
                    // slightly stale RunProteinQvalue values and the post-compaction
                    // detected_peptides set diverges from Rust by ~19 peptides on
                    // Stellar Single (1 protein delta at Stage 7). Only runs when
                    // protein FDR is enabled — the recompute is the protein-rescue
                    // input and is meaningless otherwise. Mirrors Rust pipeline.rs:
                    // 4292-4358.
                    if (ctx.Config.ProteinFdr.HasValue && bundle.PerFileEntries.Count > 0)
                    {
                        var fullLibrary = perFileScoring.GetFullLibrary(ctx);
                        ProteinFdr.RunFirstPassProteinFdr(
                            bundle.PerFileEntries, fullLibrary, ctx.Config);
                    }
                    var stats = RescoreCompaction.Apply(bundle, ctx.Config);
                    ctx.LogInfo(string.Format(
                        @"--join-at-pass=2 compaction: {0} -> {1} entries ({2} passing base_ids; {3} action(s) dropped)",
                        stats.EntriesBefore, stats.EntriesAfter,
                        stats.FirstPassBaseIds, stats.DroppedActions));
                }
                return true;
            }

            // Self-gate: rescore + reconciliation only run when there is
            // planning state to act on AND the rescore hasn't already been
            // done upstream. State comes from either FirstJoinTask's
            // planning block (in-process pipeline, DidPlan=true) or
            // PerFileScoringTask's probe-the-disk bundle (collapsed worker
            // path, DidPlan=false but bundle != null). A 2nd-pass FDR
            // sidecar already on disk for any file is the signal that the
            // rescore engine has already produced the reconciled output;
            // re-running it would re-apply reconciliation actions on top
            // of already-reconciled values, so this branch falls back to
            // the no-op alongside the no-state case. Probe-the-disk on
            // 2nd-pass sidecar presence replaces the prior
            // ExpectReconciledInput gate (Phase C: mechanism-driven, not
            // flag-driven) for the worker self-gate cases below;
            // ExpectReconciledInput keeps the hard short-circuit above for
            // the strict --join-at-pass=2 merge path. Downstream
            // MergeNodeTask still gets _perFileEntries via our accessor
            // (falls through to the upstream reference).
            var firstJoin = ctx.GetTask<FirstJoinTask>();
            bool didPlan = firstJoin.DidPlan(ctx);
            var rescoreBundle = perFileScoring.GetRescoreInputs(ctx);
            bool anyPass2Present = false;
            if (ctx.Config.InputFiles != null)
            {
                foreach (var inputFile in ctx.Config.InputFiles)
                {
                    if (File.Exists(FdrScoresSidecar.Pass2Path(inputFile)))
                    {
                        anyPass2Present = true;
                        break;
                    }
                }
            }
            if (!didPlan && (rescoreBundle == null || anyPass2Present))
                return true;

            // Per-file sidecar lifecycle (delete-before / write-after) is
            // handled inside ExecuteRescore's loop so a per-file skip can
            // preserve the valid sidecars for already-rescored files and
            // only invalidate the file(s) about to be re-rescored.

            // Join file stems for the reconciled parquet metadata hash.
            // In the in-process pipeline _perFileEntries has every file in
            // the run; in worker mode (--join-at-pass=1 --no-join) it has
            // a single file and the planner's full set comes from
            // RescoreInputs.JoinFileStems (read from reconciliation.json
            // v2+). Pass _perFileEntries keys when there's more than one;
            // else fall through to the bundle's JoinFileStems. Null /
            // empty means "let ExecuteRescore fall back to the
            // InputFiles-derived hash" (preserves v1 behavior).
            IReadOnlyList<string> joinFileStems = null;
            if (_perFileEntries != null && _perFileEntries.Count > 1)
            {
                var stems = new List<string>(_perFileEntries.Count);
                foreach (var kv in _perFileEntries)
                    stems.Add(kv.Key);
                joinFileStems = stems;
            }
            else if (rescoreBundle != null
                     && rescoreBundle.JoinFileStems != null
                     && rescoreBundle.JoinFileStems.Count > 0)
            {
                joinFileStems = rescoreBundle.JoinFileStems;
            }

            var rescoreStats = ExecuteRescore(
                _perFileEntries,
                firstJoin.GetPerFileConsensusTargets(ctx),
                firstJoin.GetReconciliationActions(ctx),
                firstJoin.GetRefinedCalibrations(ctx),
                perFileScoring.GetPerFileCalibrations(ctx),
                firstJoin.GetPerFileGapFillForRescore(ctx),
                perFileScoring.GetPerFileParquetPaths(ctx),
                perFileScoring.GetFullLibrary(ctx),
                ctx.Config,
                joinFileStems);
            ctx.LogInfo(string.Format(
                @"Stage 6 rescore: {0} entries re-scored ({1} reconciliation actions executed)",
                rescoreStats.TotalRescored, rescoreStats.TotalReconciliation));

            // Cross-impl bisection seam: dump per-precursor state
            // immediately after the rescore loop. Mirrors Rust's
            // dump_stage6_rescored call from pipeline.rs.
            if (OspreyDiagnostics.DumpRescored)
            {
                OspreyDiagnostics.WriteStage6RescoredDump(_perFileEntries);
                if (OspreyDiagnostics.RescoredOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_RESCORED_ONLY");
            }

            // Flush + close the persistent per-process diagnostic
            // dump writers (no-ops when their env vars are unset).
            // Mirrors the worker-mode close calls in RunWorker; without
            // these, the in-process pipeline path can leave the writers
            // unflushed and produce truncated bisection dumps.
            OspreyDiagnostics.CloseMpInputsDump();
            OspreyDiagnostics.ClosePredictRtDump();
            OspreyDiagnostics.CloseCwtPathDump();
            return true;
        }

        // RunWorker + its helpers (AddIfNotNull, LoadOriginalRtCalibration)
        // were removed in Phase C. The stage6 worker mode
        // (--join-at-pass=1 --no-join --input-scores) now routes through
        // AnalysisPipeline.Run with StartAt = StopAfter =
        // PerFileRescoreTask. Upstream state previously assembled in
        // RunWorker (library load, hydration, compaction, consensus,
        // calibration) is produced by PerFileScoringTask's joinOnly
        // probe-the-disk path and consumed through the lazy-rehydrate
        // accessors. Run() above is the only entry point.

        /// <summary>
        /// Phase 3 -- write the reconciled per-file
        /// <c>.reconciled.scores.parquet</c>.
        ///
        /// Reload the original Stage 4 parquet's full per-row data
        /// (identity, boundaries, 21 PIN features, CWT candidate lists) from
        /// <paramref name="originalPath"/>, replace re-scored rows in place by
        /// <see cref="FdrEntry.ParquetIndex"/> (NOT by post-compaction Vec
        /// position; the two diverge after first-pass FDR drops non-passing
        /// entries), append gap-fill rows at the end, reassign each gap-fill
        /// stub's <see cref="FdrEntry.ParquetIndex"/> to the actual row it now
        /// occupies, then write to the SEPARATE
        /// <paramref name="reconciledPath"/> via
        /// <see cref="ParquetScoreCache.WriteScoresParquet(string, List{FdrEntry}, Dictionary{string, string}, Dictionary{uint, LibraryEntry}, string)"/>
        /// with reconciliation metadata
        /// (<c>osprey.reconciled = "true"</c> +
        /// <c>osprey.reconciliation_hash = config.ReconciliationParameterHash()</c>).
        /// The original parquet is read-only here -- it is never overwritten,
        /// so it survives intact for files whose reconciliation is a no-op and
        /// as a crash-safe Stage 4 record. Mirrors Rust pipeline.rs:3050-3110.
        /// </summary>
        private void WriteReconciledParquet(string originalPath, string reconciledPath,
            List<FdrEntry> fdrEntries,
            string fileName, List<LibraryEntry> fullLibrary, OspreyConfig config,
            IReadOnlyList<string> joinFileStems)
        {
            // 1. Reload the original parquet's per-row state (read-only).
            List<FdrEntry> fullEntries;
            try
            {
                fullEntries = ParquetScoreCache.LoadFullFdrEntries(originalPath);
            }
            catch (Exception ex)
            {
                _ctx.LogWarning(string.Format(
                    "Stage 6 write-back: failed to reload {0}: {1} (skipping)",
                    originalPath, ex.Message));
                return;
            }
            int origRowCount = fullEntries.Count;

            // 2. Replace re-scored rows (Phase 1 + Phase 2 existing-entry
            //    overlay) by ParquetIndex. Detect rescored entries by
            //    Features != null -- hydration's LoadFdrStubsFromParquet
            //    does NOT populate Features, so unchanged post-compaction
            //    stubs have Features=null and we leave their corresponding
            //    fullEntries row alone (preserving Features + CwtCandidates
            //    + the binary blob columns loaded from the original
            //    parquet). Rescored entries have Features populated by
            //    RunCoelutionScoring. Gap-fill stubs have ParquetIndex =
            //    uint.MaxValue and are appended next.
            int nReplaced = 0;
            foreach (var entry in fdrEntries)
            {
                if (entry.ParquetIndex == uint.MaxValue)
                    continue;
                if (entry.Features == null)
                    continue;  // hydrated stub, never re-scored
                int pqIdx = (int)entry.ParquetIndex;
                if (pqIdx < 0 || pqIdx >= fullEntries.Count)
                {
                    _ctx.LogWarning(string.Format(
                        "Stage 6 write-back: ParquetIndex {0} out of range for {1} ({2} rows)",
                        pqIdx, fileName, fullEntries.Count));
                    continue;
                }
                fullEntries[pqIdx] = entry;
                nReplaced++;
            }

            // 3. Append gap-fill rows at the end. Reassign each gap-fill
            //    stub's ParquetIndex to its new row position so a
            //    downstream --join-at-pass=2 worker can locate its
            //    features.
            int nAppended = 0;
            foreach (var entry in fdrEntries)
            {
                if (entry.ParquetIndex != uint.MaxValue)
                    continue;
                entry.ParquetIndex = (uint)fullEntries.Count;
                fullEntries.Add(entry);
                nAppended++;
            }

            // 4. Build libraryById for the WriteScoresParquet sequence /
            //    precursor_mz / protein_ids columns.
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var libEntry in fullLibrary)
                libraryById[libEntry.Id] = libEntry;

            // 5. Reconciliation metadata (mirrors Rust
            //    build_reconciled_metadata). osprey.version is what the
            //    next reload's CacheValidity check compares against.
            //    The reconciliation_hash must be the JOIN-wide hash
            //    (over every file in the planner step), not the worker's
            //    single-file InputFiles hash; without that, a worker
            //    rescoring a single parquet stamps a single-file hash
            //    that the downstream --join-at-pass=2 merge node rejects
            //    on hash mismatch. The join file stems come from the
            //    planner's reconciliation.json (v2+) via
            //    RescoreInputs.JoinFileStems; fall back to config-derived
            //    stems when the caller didn't pass any (in-process
            //    pipeline where config.InputFiles already has all files,
            //    or v1 backward compat).
            string reconciliationHash = (joinFileStems != null && joinFileStems.Count > 0)
                ? config.ReconciliationParameterHashForStems(joinFileStems)
                : config.ReconciliationParameterHash();
            var metadata = new Dictionary<string, string>
            {
                { @"osprey.version", Program.VERSION },
                { @"osprey.search_hash", config.SearchParameterHash() },
                { @"osprey.library_hash", config.LibraryIdentityHash() },
                { @"osprey.reconciled", @"true" },
                { @"osprey.reconciliation_hash", reconciliationHash },
            };

            try
            {
                ParquetScoreCache.WriteScoresParquet(reconciledPath, fullEntries,
                    metadata, libraryById, fileName);
            }
            catch (Exception ex)
            {
                _ctx.LogWarning(string.Format(
                    "Stage 6 write-back: failed to write reconciled scores for {0}: {1}",
                    fileName, ex.Message));
                return;
            }

            _ctx.LogInfo(string.Format(
                "  Wrote reconciled parquet for {0}: {1} rows ({2} replaced + {3} appended; original {4} rows)",
                fileName, fullEntries.Count, nReplaced, nAppended, origRowCount));
        }

        /// <summary>
        /// Execute the per-file Stage 6 rescore loop. Mirrors
        /// <c>rescore_per_file_loop</c> in
        /// <c>osprey/crates/osprey/src/pipeline.rs</c>.
        ///
        /// For each file with at least one re-scoring target:
        /// <list type="number">
        ///   <item>Build boundary_overrides keyed by entry_id.</item>
        ///   <item>Subset the library to the entries that need re-scoring.</item>
        ///   <item>Reload spectra from the .spectra.bin cache or the mzML.</item>
        ///   <item>Reload MS2/MS1 mass calibration from the sibling .calibration.json.</item>
        ///   <item>Pick the refined RT calibration when present, else fall back to
        ///       the original first-pass calibration.</item>
        ///   <item>Call <see cref="AbstractScoringTask.RunCoelutionScoring"/> with the override-aware
        ///       <see cref="ScoringContext"/>.</item>
        ///   <item>Overlay the re-scored entries back onto the per-file
        ///       FdrEntry stubs by entry_id, preserving ParquetIndex.</item>
        /// </list>
        ///
        /// The mutable <paramref name="perFileEntries"/> is updated in place
        /// (Score, Pep, q-values, Features, ApexRt/StartRt/EndRt, etc.).
        /// Returns <see cref="RescoreStats"/> with the per-stage counts.
        /// </summary>
        internal RescoreStats ExecuteRescore(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileConsensusTargets,
            IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, List<GapFillTarget>> perFileGapFill,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            IReadOnlyList<string> joinFileStems = null)
        {
            // Pre-group reconciliation actions by file so the per-file loop
            // below just looks up its slice.
            var perFileReconTargets =
                GroupReconciliationActionsByFile(reconciliationActions, out int totalReconciliation);

            // file_name -> input_files index, used to pick the right mzML
            // path for spectra cache load + sibling .calibration.json.
            var fileNameToIdx = BuildFileNameToIndex(config.InputFiles);

            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;
            int nTotalFiles = perFileEntries.Count;
            string taskValidityKey = ValidityKey(_ctx);

            for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
            {
                var fileName = perFileEntries[fileNum].Key;
                var fdrEntries = perFileEntries[fileNum].Value;

                // Per-file resume: if the file's reconciled parquet is
                // already on disk with a matching <output>.PerFileRescore.osprey.task
                // sidecar, skip the rescore for that file. Pairs with the
                // worker (stage6) crash-resume contract: re-invoking the
                // same CLI on the same inputs is a no-op for files whose
                // rescore completed; only files missing a valid sidecar
                // get re-rescored. The skipped file's in-memory entries
                // remain at the pre-rescore state (1st-pass overlay)
                // because the worker's StopAfter terminates the pipeline
                // here -- no downstream consumer reads them.
                // The rescore READS the original Stage 4 parquet and WRITES a
                // separate <stem>.reconciled.scores.parquet. Resume validity is
                // keyed on the reconciled output (the task's declared Output),
                // not on the original read source.
                bool hasParquetPath = perFileParquetPaths.TryGetValue(fileName, out string perFileParquetPath);
                string reconciledPath = hasParquetPath
                    ? ParquetScoreCache.ReconciledPathFromScoresPath(perFileParquetPath)
                    : null;
                if (hasParquetPath
                    && File.Exists(reconciledPath)
                    && TaskValiditySidecar.IsValid(reconciledPath, Name, taskValidityKey))
                {
                    _ctx.LogInfo(string.Format(
                        @"[file] {0}/{1} {2}: skipping (outputs valid)",
                        fileNum + 1, nTotalFiles, fileName));
                    continue;
                }
                // About to (re-)rescore this file: clear any stale sidecar
                // so a mid-Run crash leaves no false-positive pointing at
                // the partially-written reconciled parquet.
                if (hasParquetPath)
                    TaskValiditySidecar.Delete(reconciledPath, Name);

                IReadOnlyList<(int Index, double Apex, double Start, double End)> consensusTargets;
                if (!perFileConsensusTargets.TryGetValue(fileName, out consensusTargets))
                    consensusTargets = new List<(int, double, double, double)>();

                List<(int Index, double Apex, double Start, double End)> reconTargets;
                if (!perFileReconTargets.TryGetValue(fileName, out reconTargets))
                    reconTargets = new List<(int, double, double, double)>();

                // PHASE 2 (gap-fill): per-file gap-fill targets land here.
                List<GapFillTarget> gapFillTargets;
                if (perFileGapFill == null ||
                    !perFileGapFill.TryGetValue(fileName, out gapFillTargets))
                {
                    gapFillTargets = new List<GapFillTarget>();
                }

                // Merge consensus + reconciliation into a per-(idx, override)
                // map. Reconciliation wins on conflict -- the inter-replicate
                // peak boundary is more authoritative than the multi-charge
                // consensus boundary.
                var combinedTargets =
                    new Dictionary<int, (double Apex, double Start, double End)>();
                foreach (var t in consensusTargets)
                    combinedTargets[t.Index] = (t.Apex, t.Start, t.End);
                foreach (var t in reconTargets)
                    combinedTargets[t.Index] = (t.Apex, t.Start, t.End);

                // Skip files with no work to do.
                if (combinedTargets.Count == 0 && gapFillTargets.Count == 0)
                    continue;

                if (!fileNameToIdx.TryGetValue(fileName, out int inputIdx))
                {
                    _ctx.LogWarning(string.Format(
                        "Stage 6 rescore: no input_files entry for {0} (skipping)", fileName));
                    continue;
                }
                string inputFile = config.InputFiles[inputIdx];

                // Clone the outer config for this file's ScoringContexts.
                // RunCoelutionScoring reassigns config.FragmentTolerance to
                // the MS2-calibrated tolerance (AnalysisPipeline.cs ~line 3552);
                // without a per-file clone the mutation persists on the outer
                // config, leaks into subsequent files, AND poisons the
                // WriteReconciledParquet hash stamp (config.SearchParameterHash()
                // would then reflect the calibrated tolerance, not the value
                // a fresh --join-at-pass=2 invocation recomputes from CLI
                // defaults -- causing search_hash mismatch errors). Mirrors
                // the per-file clone pattern in ProcessFile.
                var fileConfig = config.ShallowClone();

                _ctx.LogInfo(string.Format(
                    "Re-scoring file {0}/{1}: {2}", fileNum + 1, nTotalFiles, fileName));
                _ctx.LogInfo(string.Format(
                    "  {0} entries ({1} consensus, {2} reconciliation, {3} gap-fill, {4} unique after dedup)",
                    combinedTargets.Count + gapFillTargets.Count * 2,
                    consensusTargets.Count,
                    reconTargets.Count,
                    gapFillTargets.Count,
                    combinedTargets.Count));

                // Build the per-file scoring subset: boundary_overrides keyed
                // by entry_id + the subset library RunCoelutionScoring scores.
                var (boundaryOverrides, subsetLibrary) =
                    BuildScoringSubset(combinedTargets, fdrEntries, fullLibrary);

                // Load spectra: prefer the .spectra.bin cache the original
                // Stage 1 wrote; fall back to mzML if the cache is missing
                // or unreadable.
                List<Spectrum> spectra;
                List<MS1Spectrum> ms1Spectra;
                LoadSpectraForRescore(inputFile, fileName, out spectra, out ms1Spectra);

                // Load the sibling .calibration.json so the search uses the
                // same MS2/MS1 mass calibrations the original Stage 1-4 run
                // used. The file is written by the original ProcessFile call
                // and read here -- same disk-roundtrip path the worker uses.
                LoadMassCalibrations(inputFile,
                    out MzCalibrationResult ms2Cal,
                    out MzCalibrationResult ms1Cal,
                    out double? rtMadFromCalJson);

                // Pick the RT calibration: refined (from Stage 6 planning's
                // calibration refit) wins; original first-pass falls back.
                if (!refinedCalibrations.TryGetValue(fileName, out RTCalibration rtCal))
                    perFileCalibrations.TryGetValue(fileName, out rtCal);

                // Bisection seam: dump the cal's library_rts +
                // fitted_values once per file. Mirrors Rust's
                // dump_predict_rt_arrays at pipeline.rs ~2886.
                if (rtCal != null)
                {
                    OspreyDiagnostics.WritePredictRtArrays(
                        fileName, rtCal.LibraryRts, rtCal.FittedValues);
                }

                // Build the scoring context with the boundary overrides.
                // RunCoelutionScoring inspects context.BoundaryOverrides
                // inside ScoreCandidate and routes through the override
                // peak-construction path.
                var context = new ScoringContext(fileConfig, fileName);
                context.BoundaryOverrides = boundaryOverrides;
                context.OriginalRtMad = rtMadFromCalJson;

                // Build isolation windows from the loaded spectra (same as
                // the first-pass ProcessFile path).
                var isolationWindows = ExtractIsolationWindows(spectra);

                // Re-score the subset.
                var swRescore = Stopwatch.StartNew();
                List<FdrEntry> rescored;
                if (subsetLibrary.Count > 0)
                {
                    rescored = RunCoelutionScoring(
                        subsetLibrary, spectra, ms1Spectra,
                        isolationWindows, rtCal,
                        ms2Cal, ms1Cal,
                        context);
                }
                else
                {
                    rescored = new List<FdrEntry>();
                }
                swRescore.Stop();

                // Overlay the re-scored subset back onto the per-file stubs,
                // resetting discriminant fields to Rust to_fdr_entry defaults.
                var (nOverlay, nNoPeak) =
                    OverlayRescoredEntries(fdrEntries, combinedTargets, rescored);
                totalRescored += nOverlay;
                if (nNoPeak > 0)
                {
                    _ctx.LogInfo(string.Format(
                        "  {0} targets had no peak at override boundary (reset to defaults)",
                        nNoPeak));
                }

                _ctx.LogInfo(string.Format(
                    "  {0} of {1} existing entries re-scored ({2:F1}s)",
                    nOverlay, combinedTargets.Count, swRescore.Elapsed.TotalSeconds));

                // PHASE 2 -- gap-fill two-pass.
                if (gapFillTargets.Count > 0)
                {
                    var (nGapCwt, nGapForced) = RunGapFillTwoPass(
                        gapFillTargets, fullLibrary, spectra, ms1Spectra,
                        isolationWindows, rtCal, ms2Cal, ms1Cal,
                        fileConfig, fileName, rtMadFromCalJson, fdrEntries);
                    totalGapCwt += nGapCwt;
                    totalGapForced += nGapForced;
                    totalRescored += nGapCwt + nGapForced;
                }

                // PHASE 3 -- reconciled parquet write-back. Read the original
                // Stage 4 parquet, write a separate .reconciled.scores.parquet
                // sibling (leaving the original intact).
                if (perFileParquetPaths != null &&
                    perFileParquetPaths.TryGetValue(fileName, out string parquetPath) &&
                    File.Exists(parquetPath))
                {
                    string reconciledOutPath = ParquetScoreCache.ReconciledPathFromScoresPath(parquetPath);
                    WriteReconciledParquet(parquetPath, reconciledOutPath, fdrEntries, fileName,
                        fullLibrary, config, joinFileStems);

                    // Per-file resume sidecar: write next to the
                    // reconciled parquet so a subsequent invocation with
                    // the same validity key can skip this file. The
                    // pre-write delete above guarantees the sidecar
                    // appears only after WriteReconciledParquet finished.
                    var perFileInputs = new List<string>
                    {
                        FdrScoresSidecar.Pass1Path(inputFile),
                    };
                    if (config.Reconciliation != null && config.Reconciliation.Enabled)
                        perFileInputs.Add(ReconciliationFile.PathForInput(inputFile));
                    try
                    {
                        TaskValiditySidecar.Write(reconciledOutPath, Name, Program.VERSION,
                            taskValidityKey, perFileInputs);
                    }
                    catch (Exception ex)
                    {
                        _ctx.LogWarning(string.Format(
                            @"  Failed to write {0} sidecar for {1}: {2}",
                            Name, reconciledOutPath, ex.Message));
                    }
                }
            }

            return new RescoreStats
            {
                TotalRescored = totalRescored,
                TotalReconciliation = totalReconciliation,
                TotalGapCwt = totalGapCwt,
                TotalGapForced = totalGapForced,
            };
        }

        /// <summary>
        /// Pre-group reconciliation actions by file. Mirrors the Rust
        /// pre-grouping at pipeline.rs:2719-2744 -- a single pass over
        /// the action map produces (file -> [(idx, apex, start, end)])
        /// so the per-file loop just looks up its slice. Returns the
        /// per-file map; <paramref name="totalReconciliation"/> receives
        /// the count of non-Keep actions grouped.
        /// </summary>
        private static Dictionary<string, List<(int Index, double Apex, double Start, double End)>>
            GroupReconciliationActionsByFile(
                IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
                out int totalReconciliation)
        {
            var perFileReconTargets =
                new Dictionary<string, List<(int Index, double Apex, double Start, double End)>>();
            totalReconciliation = 0;
            foreach (var kvp in reconciliationActions)
            {
                var fileName = kvp.Key.FileName;
                var idx = kvp.Key.Index;
                double apex, start, end;
                if (kvp.Value is ReconcileAction.UseCwtPeak useCwt)
                {
                    apex = useCwt.ApexRt;
                    start = useCwt.StartRt;
                    end = useCwt.EndRt;
                }
                else if (kvp.Value is ReconcileAction.ForcedIntegration forced)
                {
                    apex = forced.ExpectedRt;
                    start = forced.ExpectedRt - forced.HalfWidth;
                    end = forced.ExpectedRt + forced.HalfWidth;
                }
                else
                {
                    // Keep: planner omits these from the map by design,
                    // but stay defensive -- skip rather than crash.
                    continue;
                }
                if (!perFileReconTargets.TryGetValue(fileName, out var list))
                {
                    list = new List<(int, double, double, double)>();
                    perFileReconTargets[fileName] = list;
                }
                list.Add((idx, apex, start, end));
                totalReconciliation++;
            }
            return perFileReconTargets;
        }

        /// <summary>
        /// Build the file_name -> input_files index map used to pick the
        /// right mzML path for the spectra-cache load + sibling
        /// .calibration.json. For the worker, config.InputFiles was
        /// synthesized from --input-scores parquet stems by Program.Main;
        /// for in-process it's the user's -i mzML list. Either way the
        /// stem matches the file_name keys in perFileEntries.
        /// </summary>
        private static Dictionary<string, int> BuildFileNameToIndex(IReadOnlyList<string> inputFiles)
        {
            var fileNameToIdx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < inputFiles.Count; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(inputFiles[i]) ?? string.Empty;
                fileNameToIdx[stem] = i;
            }
            return fileNameToIdx;
        }

        /// <summary>
        /// Build the per-file scoring subset: the boundary_overrides map
        /// keyed by entry_id, and the subset library handed to
        /// <see cref="AbstractScoringTask.RunCoelutionScoring"/> so it
        /// doesn't waste work on entries we're not re-scoring. The subset
        /// is the same library entries the original Stage 1-4 scoring used,
        /// just a smaller list.
        /// </summary>
        private static (Dictionary<uint, (double Apex, double Start, double End)> BoundaryOverrides,
            List<LibraryEntry> SubsetLibrary) BuildScoringSubset(
                Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
                List<FdrEntry> fdrEntries,
                List<LibraryEntry> fullLibrary)
        {
            // Build boundary_overrides keyed by entry_id. Also collect the
            // subset of library ids the search engine needs to score.
            var boundaryOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var subsetIds = new HashSet<uint>();
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                boundaryOverrides[entryId] = kvp.Value;
                subsetIds.Add(entryId);
            }

            List<LibraryEntry> subsetLibrary;
            if (subsetIds.Count == 0)
            {
                subsetLibrary = new List<LibraryEntry>();
            }
            else
            {
                subsetLibrary = new List<LibraryEntry>(subsetIds.Count);
                foreach (var libEntry in fullLibrary)
                {
                    if (subsetIds.Contains(libEntry.Id))
                        subsetLibrary.Add(libEntry);
                }
            }
            return (boundaryOverrides, subsetLibrary);
        }

        /// <summary>
        /// Overlay re-scored entries back onto the per-file FdrEntry stubs
        /// by entry_id, preserving the original ParquetIndex so the
        /// write-back step can target the right Parquet row (post-compaction
        /// Vec position != Parquet row index).
        ///
        /// Mirror Rust's to_fdr_entry semantics: post-rescore stubs carry
        /// default Score (0.0), q-values (1.0), and Pep (1.0). Percolator
        /// (Stage 7, second-pass FDR) recomputes these from the new
        /// Features. Without this reset the OspreySharp ScoreCandidate's
        /// <c>Score = coelutionSum</c> initializer bleeds through, producing
        /// 173k rows of post-rescore divergence vs the Rust worker's
        /// rust_stage6_rescored.tsv. Targets where RunCoelutionScoring
        /// returned no entry (no peak at the override boundary) STILL get
        /// their existing stub reset in place -- Rust's worker emits zeroed
        /// stubs for every override regardless of peak success. Returns
        /// (entries overlaid, no-peak resets).
        /// </summary>
        private static (int NOverlay, int NNoPeak) OverlayRescoredEntries(
            List<FdrEntry> fdrEntries,
            Dictionary<int, (double Apex, double Start, double End)> combinedTargets,
            List<FdrEntry> rescored)
        {
            // Pass 1: index the rescored results by entry_id so we
            // can look up successful re-scores in the second pass.
            var rescoredByEntryId = new Dictionary<uint, FdrEntry>();
            foreach (var entry in rescored)
            {
                rescoredByEntryId[entry.EntryId] = entry;
            }

            // Pass 2: iterate every combined target.
            int nOverlay = 0;
            int nNoPeak = 0;
            foreach (var kvp in combinedTargets)
            {
                int idx = kvp.Key;
                uint entryId = fdrEntries[idx].EntryId;
                if (rescoredByEntryId.TryGetValue(entryId, out FdrEntry rescoredEntry))
                {
                    rescoredEntry.Score = 0.0;
                    rescoredEntry.RunPrecursorQvalue = 1.0;
                    rescoredEntry.RunPeptideQvalue = 1.0;
                    rescoredEntry.RunProteinQvalue = 1.0;
                    rescoredEntry.ExperimentPrecursorQvalue = 1.0;
                    rescoredEntry.ExperimentPeptideQvalue = 1.0;
                    rescoredEntry.ExperimentProteinQvalue = 1.0;
                    rescoredEntry.Pep = 1.0;
                    rescoredEntry.ParquetIndex = fdrEntries[idx].ParquetIndex;
                    fdrEntries[idx] = rescoredEntry;
                    nOverlay++;
                }
                else
                {
                    // No peak at the override boundary -- reset to
                    // defaults in place to match Rust's behavior.
                    var existing = fdrEntries[idx];
                    existing.Score = 0.0;
                    existing.RunPrecursorQvalue = 1.0;
                    existing.RunPeptideQvalue = 1.0;
                    existing.RunProteinQvalue = 1.0;
                    existing.ExperimentPrecursorQvalue = 1.0;
                    existing.ExperimentPeptideQvalue = 1.0;
                    existing.ExperimentProteinQvalue = 1.0;
                    existing.Pep = 1.0;
                    nNoPeak++;
                }
            }
            return (nOverlay, nNoPeak);
        }

        /// <summary>
        /// PHASE 2 gap-fill two-pass for a single file: a CWT pass (prefilter
        /// disabled, peaks picked freely) followed by a forced-integration
        /// pass for the targets CWT missed. CWT + forced results are appended
        /// to <paramref name="fdrEntries"/> as new gap-fill stubs (ParquetIndex
        /// sentinel + score-reset, mirroring Rust to_fdr_entry semantics).
        /// Decoys are intentionally excluded from gap-fill: forcing a random
        /// decoy sequence to be scored at the target's consensus RT has no
        /// biological basis (decoys are not expected to co-elute with their
        /// paired target), and the 1st-pass parquet already has a score for
        /// every decoy at its own natural-but-best peak. Gap-filling decoys
        /// also re-scored them at consensus RT and APPENDED a second parquet
        /// row alongside the existing 1st-pass row, producing exact-duplicate
        /// rows in the reconciled parquet. Those duplicates cascaded into
        /// different max-per-modseq aggregations cross-impl and a 1.1e-4
        /// group_qvalue drift on Astral 3-file. Targets are still gap-filled
        /// because they were missing from this file by definition. Returns
        /// (CWT hits, forced integrations).
        /// </summary>
        private (int NGapCwt, int NGapForced) RunGapFillTwoPass(
            List<GapFillTarget> gapFillTargets,
            List<LibraryEntry> fullLibrary,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCal,
            MzCalibrationResult ms2Cal,
            MzCalibrationResult ms1Cal,
            OspreyConfig fileConfig,
            string fileName,
            double? rtMadFromCalJson,
            List<FdrEntry> fdrEntries)
        {
            int nGapCwt = 0;
            int nGapForced = 0;

            // Build gap-fill library subset (targets only).
            var gapFillIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                gapFillIds.Add(gf.TargetEntryId);
            }
            var gapFillLibrary = new List<LibraryEntry>(gapFillIds.Count);
            foreach (var libEntry in fullLibrary)
            {
                if (gapFillIds.Contains(libEntry.Id))
                    gapFillLibrary.Add(libEntry);
            }

            HashSet<uint> cwtHitIds;
            if (gapFillLibrary.Count > 0)
            {
                // Pass 1: CWT pass with prefilter disabled. Clone
                // fileConfig (already a per-file clone) so the
                // disable is scoped to this CWT pass and doesn't
                // affect the forced-integration pass below.
                var cwtConfig = fileConfig.ShallowClone();
                cwtConfig.PrefilterEnabled = false;
                var cwtContext = new ScoringContext(cwtConfig, fileName);
                cwtContext.OriginalRtMad = rtMadFromCalJson;
                // No BoundaryOverrides -- CWT picks peaks freely.

                var swCwt = Stopwatch.StartNew();
                var cwtResults = RunCoelutionScoring(
                    gapFillLibrary, spectra, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    cwtContext);
                swCwt.Stop();

                cwtHitIds = new HashSet<uint>();
                foreach (var entry in cwtResults)
                    cwtHitIds.Add(entry.EntryId);
                nGapCwt = cwtResults.Count;

                // Append CWT results as new FdrEntry stubs with the
                // gap-fill sentinel + score-reset (mirroring Rust
                // to_fdr_entry semantics for new stubs).
                foreach (var entry in cwtResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.Score = 0.0;
                    entry.RunPrecursorQvalue = 1.0;
                    entry.RunPeptideQvalue = 1.0;
                    entry.RunProteinQvalue = 1.0;
                    entry.ExperimentPrecursorQvalue = 1.0;
                    entry.ExperimentPeptideQvalue = 1.0;
                    entry.ExperimentProteinQvalue = 1.0;
                    entry.Pep = 1.0;
                    fdrEntries.Add(entry);
                }

                _ctx.LogInfo(string.Format(
                    "  Gap-fill CWT: {0} hits ({1:F1}s)",
                    nGapCwt, swCwt.Elapsed.TotalSeconds));
            }
            else
            {
                cwtHitIds = new HashSet<uint>();
            }

            // Pass 2: Forced integration for targets CWT missed.
            // Decoys are intentionally excluded from gap-fill (see
            // gapFillIds build above).
            var forcedOverrides = new Dictionary<uint, (double Apex, double Start, double End)>();
            var forcedIds = new HashSet<uint>();
            foreach (var gf in gapFillTargets)
            {
                double start = gf.ExpectedRt - gf.HalfWidth;
                double end = gf.ExpectedRt + gf.HalfWidth;
                if (!cwtHitIds.Contains(gf.TargetEntryId))
                {
                    forcedOverrides[gf.TargetEntryId] = (gf.ExpectedRt, start, end);
                    forcedIds.Add(gf.TargetEntryId);
                }
            }

            if (forcedOverrides.Count > 0)
            {
                var forcedLibrary = new List<LibraryEntry>(forcedIds.Count);
                foreach (var libEntry in gapFillLibrary)
                {
                    if (forcedIds.Contains(libEntry.Id))
                        forcedLibrary.Add(libEntry);
                }

                var forcedContext = new ScoringContext(fileConfig, fileName);
                forcedContext.BoundaryOverrides = forcedOverrides;
                forcedContext.OriginalRtMad = rtMadFromCalJson;

                var swForced = Stopwatch.StartNew();
                var forcedResults = RunCoelutionScoring(
                    forcedLibrary, spectra, ms1Spectra,
                    isolationWindows, rtCal,
                    ms2Cal, ms1Cal,
                    forcedContext);
                swForced.Stop();
                nGapForced = forcedResults.Count;

                foreach (var entry in forcedResults)
                {
                    entry.ParquetIndex = uint.MaxValue;
                    entry.Score = 0.0;
                    entry.RunPrecursorQvalue = 1.0;
                    entry.RunPeptideQvalue = 1.0;
                    entry.RunProteinQvalue = 1.0;
                    entry.ExperimentPrecursorQvalue = 1.0;
                    entry.ExperimentPeptideQvalue = 1.0;
                    entry.ExperimentProteinQvalue = 1.0;
                    entry.Pep = 1.0;
                    fdrEntries.Add(entry);
                }

                _ctx.LogInfo(string.Format(
                    "  Gap-fill forced: {0} integrated ({1:F1}s)",
                    nGapForced, swForced.Elapsed.TotalSeconds));
            }

            return (nGapCwt, nGapForced);
        }

        /// <summary>
        /// Load MS2 spectra + MS1 spectra for the rescore loop. Prefers the
        /// .spectra.bin cache the original Stage 1 wrote; falls back to
        /// re-parsing the mzML on cache miss / read error. Mirrors the
        /// Rust spectra-load block at pipeline.rs:2851-2872.
        /// </summary>
        private void LoadSpectraForRescore(string inputFile, string fileName,
            out List<Spectrum> spectra, out List<MS1Spectrum> ms1Spectra)
        {
            string cachePath = SpectraCache.GetCachePath(inputFile);
            if (File.Exists(cachePath))
            {
                try
                {
                    var result = SpectraCache.LoadSpectraCache(cachePath);
                    spectra = result.Ms2Spectra;
                    ms1Spectra = result.Ms1Spectra;
                    _ctx.LogInfo(string.Format(
                        "  Loaded {0} MS2 + {1} MS1 spectra from cache for {2}",
                        spectra.Count, ms1Spectra.Count, fileName));
                    return;
                }
                catch (Exception ex)
                {
                    _ctx.LogWarning(string.Format(
                        "Failed to load spectra cache {0}: {1}; falling back to mzML",
                        cachePath, ex.Message));
                }
            }
            var fresh = MzmlReader.LoadAllSpectra(inputFile);
            spectra = fresh.Ms2Spectra;
            ms1Spectra = fresh.Ms1Spectra;
            _ctx.LogInfo(string.Format(
                "  Loaded {0} MS2 + {1} MS1 spectra from mzML for {2}",
                spectra.Count, ms1Spectra.Count, fileName));
        }

        /// <summary>
        /// Load MS2 + MS1 mass calibrations and the original Stage-4 RT
        /// calibration MAD from the sibling .calibration.json that
        /// Stage 2 wrote. Throws <see cref="InvalidDataException"/> if the
        /// calibration sidecar is missing or unreadable -- Stage 6
        /// requires the Stage 1-4 calibration to rescore, and silently
        /// falling back to uncalibrated would mask a real configuration
        /// error (the worker's output would diverge from the
        /// straight-through pipeline's output). Mirrors the hard-error
        /// behavior in Rust <c>run_rescore</c> at
        /// <c>osprey/crates/osprey/src/rescore.rs</c>. Individual calibration
        /// sections (Ms1Calibration / Ms2Calibration / RtMad) may still
        /// be absent within the file; those leave the corresponding
        /// out-param at its uncalibrated / null default.
        /// </summary>
        private void LoadMassCalibrations(string inputFile,
            out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal,
            out double? rtMadFromCalJson)
        {
            ms2Cal = MzCalibrationResult.Uncalibrated();
            ms1Cal = MzCalibrationResult.Uncalibrated();
            rtMadFromCalJson = null;

            string parent = Path.GetDirectoryName(Path.GetFullPath(inputFile));
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: cannot derive sidecar directory from input path `{0}`. " +
                    "Stage 6 needs to read the Stage 1-4 calibration sidecar; without it the " +
                    "worker would silently produce uncalibrated rescore output.", inputFile));
            }
            string calPath = CalibrationIO.CalibrationPathForInput(inputFile, parent);
            if (!File.Exists(calPath))
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: required calibration JSON not found at `{0}` " +
                    "(input file: `{1}`). Stage 6 needs the Stage 1-4 calibration sidecar to " +
                    "rescore. Run Stages 1-4 first or fix the path.", calPath, inputFile));
            }

            CalibrationParams calParams;
            try
            {
                calParams = CalibrationIO.LoadCalibration(calPath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    "LoadMassCalibrations: failed to read calibration JSON `{0}`: {1}. The file " +
                    "exists but could not be parsed -- check that it was written by a matching " +
                    "OspreySharp version.", calPath, ex.Message), ex);
            }

            if (calParams.Ms2Calibration != null && calParams.Ms2Calibration.Calibrated)
            {
                ms2Cal = new MzCalibrationResult
                {
                    Mean = calParams.Ms2Calibration.Mean,
                    Median = calParams.Ms2Calibration.Median,
                    SD = calParams.Ms2Calibration.SD,
                    Count = calParams.Ms2Calibration.Count,
                    Unit = calParams.Ms2Calibration.Unit,
                    AdjustedTolerance = calParams.Ms2Calibration.AdjustedTolerance,
                    Calibrated = true
                };
            }
            if (calParams.Ms1Calibration != null && calParams.Ms1Calibration.Calibrated)
            {
                ms1Cal = new MzCalibrationResult
                {
                    Mean = calParams.Ms1Calibration.Mean,
                    Median = calParams.Ms1Calibration.Median,
                    SD = calParams.Ms1Calibration.SD,
                    Count = calParams.Ms1Calibration.Count,
                    Unit = calParams.Ms1Calibration.Unit,
                    AdjustedTolerance = calParams.Ms1Calibration.AdjustedTolerance,
                    Calibrated = true
                };
            }
            // The MAD is what Rust's run_search uses for rt_tolerance
            // derivation; emit it from here (not from the refined cal's
            // abs_residuals) so the C# rescore matches Rust's window
            // size byte-for-byte.
            if (calParams.RtCalibration != null && calParams.RtCalibration.MAD.HasValue)
            {
                rtMadFromCalJson = calParams.RtCalibration.MAD.Value;
            }
        }
    }
}
