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
    /// Two entry methods on a single (parameterless-constructed) instance:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="Run"/> — invoked by <see cref="AnalysisPipeline"/>'s
    ///     task driver during a straight-through pipeline run. Reads the
    ///     upstream state from sibling tasks through
    ///     <c>ctx.GetTask&lt;PerFileScoringTask&gt;()</c> and
    ///     <c>ctx.GetTask&lt;FirstJoinTask&gt;()</c>, dispatches into
    ///     <see cref="ExecuteRescore"/>, then runs the per-process
    ///     diagnostic-writer close + cross-impl bisection dump.
    ///   </item>
    ///   <item>
    ///     <see cref="RunWorker"/> — entry point for the
    ///     <c>--join-at-pass=1 --no-join</c> worker mode. Loads the
    ///     library, hydrates the boundary-file pair from sidecars,
    ///     applies worker compaction, computes per-file consensus
    ///     targets in-method, then dispatches into
    ///     <see cref="ExecuteRescore"/>. Pass 2 will fold this into
    ///     the registry path by moving the hydration onto the
    ///     upstream producers' lazy-rehydrate accessors.
    ///   </item>
    /// </list>
    /// Inherits the scoring engine (RunCoelutionScoring, LoadLibrary,
    /// GenerateDecoys, ExtractIsolationWindows, ...) from
    /// <see cref="AbstractScoringTask"/>.
    /// </summary>
    internal sealed class PerFileRescoreTask : AbstractScoringTask
    {
        // Captured during Run / RunWorker so MergeNodeTask (downstream)
        // can reach the post-rescore version. Per the ownership-transfer
        // semantics of the pipeline: this task is the producer of the
        // post-rescore perFileEntries; consumers query us rather than
        // PerFileScoringTask. When Run is a no-op (DidPlan = false) the
        // list reference falls through unchanged from PerFileScoringTask.
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
                    @"PerFileRescoreTask.GetPerFileEntries called before Run / RunWorker populated the field.");
            return _perFileEntries;
        }

        // Phase B resume surface. The reconciled parquet overwrites the
        // upstream PerFileScoringTask parquet at the same path, but
        // the per-task sidecar naming
        // (<output>.PerFileRescore.osprey.task) keeps the two tasks'
        // validity records distinct. ValidityKey adds the
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
                yield return ParquetScoreCache.GetScoresPath(input);
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
            // Mid-Run crash safety: see FirstJoinTask.Run for rationale.
            foreach (var output in Outputs(ctx))
                TaskValiditySidecar.Delete(output, Name);
            var perFileScoring = ctx.GetTask<PerFileScoringTask>();
            _perFileEntries = perFileScoring.GetPerFileEntries(ctx);

            // Self-gate on FirstJoinTask: rescore + reconciliation only
            // run when planning actually produced state (multi-file with
            // Reconciliation.Enabled, not --join-at-pass=2). When
            // planning was skipped, this task is a no-op; downstream
            // MergeNodeTask still gets _perFileEntries via our accessor
            // (falls through to the upstream reference).
            var firstJoin = ctx.GetTask<FirstJoinTask>();
            if (!firstJoin.DidPlan(ctx))
                return true;

            var rescoreStats = ExecuteRescore(
                _perFileEntries,
                firstJoin.GetPerFileConsensusTargets(ctx),
                firstJoin.GetReconciliationActions(ctx),
                firstJoin.GetRefinedCalibrations(ctx),
                perFileScoring.GetPerFileCalibrations(ctx),
                firstJoin.GetPerFileGapFillForRescore(ctx),
                perFileScoring.GetPerFileParquetPaths(ctx),
                perFileScoring.GetFullLibrary(ctx),
                ctx.Config);
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

        /// <summary>
        /// Top-level entry point for the <c>--join-at-pass=1 --no-join</c>
        /// per-file rescore worker. Mirrors <c>run_rescore</c> in
        /// <c>osprey/crates/osprey/src/rescore.rs</c>.
        ///
        /// Synthesizes <c>config.InputFiles</c> from <c>config.InputScores</c>
        /// (mzML stems derived from parquet stems), loads the spectral
        /// library, hydrates the boundary file pair via
        /// <see cref="RescoreHydration.HydrateForRescore"/>, applies worker
        /// compaction via <see cref="RescoreCompaction.Apply"/>, computes
        /// per-file multi-charge consensus targets from the compacted
        /// stubs, builds the per-file original RT calibration map by
        /// loading each sibling <c>.calibration.json</c>, then dispatches
        /// to <see cref="ExecuteRescore"/>.
        /// </summary>
        internal int RunWorker(OspreyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            // Init _ctx so the inherited engine methods (RunCoelutionScoring,
            // etc.) can log via the same callbacks the in-process pipeline uses.
            // Worker mode's "pipeline" is just this task; the upstream-task
            // hydration that the in-process flow gets through ctx.GetTask
            // happens inside this method (see below) until Pass 2 moves it
            // onto the producer tasks' accessors.
            _ctx = new PipelineContext(config, new OspreyTask[] { this },
                Program.LogInfo, Program.LogWarning, Program.LogError);
            if (config.InputScores == null || config.InputScores.Count == 0)
            {
                _ctx.LogError(
                    "--join-at-pass=1 --no-join requires --input-scores <path...>.");
                return 1;
            }

            // Synthesize config.InputFiles from --input-scores so
            // ExecuteRescore's file_name_to_idx can map file_name
            // back to a real (synthetic) input path. Mirrors Rust's
            // run_analysis idempotent synthesis at pipeline.rs ~line 3144.
            if (config.InputFiles == null || config.InputFiles.Count == 0)
            {
                var synthetic = new List<string>(config.InputScores.Count);
                foreach (var p in config.InputScores)
                    synthetic.Add(RescoreHydration.SyntheticInputFromParquet(p));
                config.InputFiles = synthetic;
            }

            _ctx.LogInfo(string.Format(
                "--join-at-pass=1 --no-join: per-file rescore worker starting on {0} parquet(s)",
                config.InputScores.Count));

            // Library loading uses the same path the in-process pipeline does,
            // including the .libcache fast-path.
            List<LibraryEntry> fullLibrary;
            try
            {
                fullLibrary = LoadLibrary(config);
            }
            catch (Exception ex)
            {
                _ctx.LogError(string.Format(
                    "--join-at-pass=1 --no-join: library load failed: {0}", ex.Message));
                return 1;
            }
            // Decoy generation (mirror the in-process flow's call site at
            // AnalysisPipeline.Run line 132). Worker needs the decoys in
            // fullLibrary so subset_library produces the full target+decoy
            // set ScoreCandidate expects.
            if (!config.DecoysInLibrary)
            {
                List<LibraryEntry> validTargets;
                var decoys = GenerateDecoys(fullLibrary, config, out validTargets);
                fullLibrary = new List<LibraryEntry>(validTargets.Count + decoys.Count);
                fullLibrary.AddRange(validTargets);
                fullLibrary.AddRange(decoys);
            }

            // Hydrate boundary file pair -> RescoreInputs.
            RescoreInputs inputs;
            try
            {
                inputs = RescoreHydration.HydrateForRescore(config.InputScores);
            }
            catch (Exception ex)
            {
                _ctx.LogError(string.Format(
                    "--join-at-pass=1 --no-join: hydration failed: {0}", ex.Message));
                return 1;
            }
            _ctx.LogInfo(string.Format(
                "Hydrated {0} file(s); {1} pre-compaction stubs, {2} reconciliation actions, " +
                "{3} gap-fill candidates, {4} refined RT calibration(s)",
                inputs.PerFileEntries.Count,
                inputs.TotalStubs,
                inputs.TotalActions,
                inputs.TotalGapFillTargets,
                inputs.RefinedCalibrations.Count));

            // Cross-impl bisection seam: dump the per-precursor q-values
            // so the result can be diffed against Rust's
            // rust_stage5_percolator.tsv via Compare-Percolator.ps1.
            if (OspreyDiagnostics.DumpPercolator)
                OspreyDiagnostics.WriteStage5PercolatorDump(inputs.PerFileEntries);

            // Worker compaction (mirror in-process first-pass FDR drop).
            RescoreCompaction.Stats compactStats;
            try
            {
                compactStats = RescoreCompaction.Apply(inputs, config);
            }
            catch (Exception ex)
            {
                _ctx.LogError(string.Format(
                    "--join-at-pass=1 --no-join: compaction failed: {0}", ex.Message));
                return 1;
            }
            _ctx.LogInfo(string.Format(
                "Worker compaction: {0} -> {1} entries ({2} surviving base_ids), " +
                "{3} reconciliation actions retained ({4} dropped)",
                compactStats.EntriesBefore,
                compactStats.EntriesAfter,
                compactStats.FirstPassBaseIds,
                inputs.ReconciliationActions.Count,
                compactStats.DroppedActions));

            // Compute per-file multi-charge consensus targets from the
            // compacted stubs. This is fresh per-file work -- the planner's
            // reconciliation actions cover cross-run targets, but
            // multi-charge consensus is a within-file decision and the
            // worker recomputes it from the same FDR threshold the
            // in-process flow uses.
            var perFileConsensusTargets =
                new Dictionary<string,
                    IReadOnlyList<(int Index, double Apex, double Start, double End)>>();
            int totalConsensusTargets = 0;
            foreach (var kvp in inputs.PerFileEntries)
            {
                var targets = MultiChargeConsensus.SelectRescoreTargets(kvp.Value, config.RunFdr);
                perFileConsensusTargets[kvp.Key] = targets;
                totalConsensusTargets += targets.Count;
            }
            _ctx.LogInfo(string.Format(
                "Worker multi-charge consensus: {0} entries to re-score across {1} files",
                totalConsensusTargets, inputs.PerFileEntries.Count));

            // Build per-file original RT calibration map from sibling
            // .calibration.json. The refinedCalibrations dict (from the
            // reconciliation envelope) is the preferred source inside the
            // rescore loop, but the original cal is the fallback when no
            // refined cal was persisted for a file (Stage 5 LOESS refit
            // failed). Mirrors the per-file calibration load in
            // rescore::run_rescore at lines 367-413.
            var perFileCalibrations = new Dictionary<string, RTCalibration>();
            foreach (var kvp in inputs.PerFileEntries)
            {
                string fileName = kvp.Key;
                int inputIdx;
                bool found = false;
                for (int i = 0; i < config.InputFiles.Count; i++)
                {
                    if (Path.GetFileNameWithoutExtension(config.InputFiles[i]) == fileName)
                    {
                        inputIdx = i;
                        found = true;
                        AddIfNotNull(perFileCalibrations, fileName,
                            LoadOriginalRtCalibration(config.InputFiles[inputIdx]));
                        break;
                    }
                }
                if (!found)
                {
                    _ctx.LogWarning(string.Format(
                        "Worker: no input_files entry for {0} (no original cal loaded)", fileName));
                }
            }
            _ctx.LogInfo(string.Format(
                "Worker original calibrations: {0}/{1} files loaded",
                perFileCalibrations.Count, inputs.PerFileEntries.Count));

            // Per-file parquet paths from --input-scores. Stage 6
            // write-back rewrites these in place with reconciliation
            // metadata, mirroring how Rust's worker uses
            // per_file_cache_paths.
            var perFileParquetPaths = new Dictionary<string, string>();
            for (int i = 0; i < config.InputScores.Count; i++)
            {
                string parquetPath = config.InputScores[i];
                string fileName = RescoreHydration.SyntheticInputFromParquet(parquetPath);
                fileName = Path.GetFileNameWithoutExtension(fileName);
                if (!string.IsNullOrEmpty(fileName))
                    perFileParquetPaths[fileName] = parquetPath;
            }

            // Phases 1 + 2 + 3 of the rescore engine. Phase 3 rewrites
            // each per-file .scores.parquet with reconciliation metadata.
            RescoreStats stats;
            try
            {
                stats = ExecuteRescore(
                    inputs.PerFileEntries,
                    perFileConsensusTargets,
                    inputs.ReconciliationActions,
                    inputs.RefinedCalibrations,
                    perFileCalibrations,
                    inputs.PerFileGapFill,
                    perFileParquetPaths,
                    fullLibrary,
                    config);
            }
            catch (Exception ex)
            {
                _ctx.LogError(string.Format(
                    "--join-at-pass=1 --no-join: rescore failed: {0}", ex.Message));
                _ctx.LogError(ex.StackTrace);
                return 1;
            }

            _ctx.LogInfo(string.Format(
                "Stage 6 rescore: {0} entries re-scored ({1} reconciliation actions, " +
                "{2} gap-fill via CWT, {3} gap-fill via forced)",
                stats.TotalRescored, stats.TotalReconciliation,
                stats.TotalGapCwt, stats.TotalGapForced));

            // Cross-impl bisection seam: dump per-precursor state
            // immediately after the rescore loop. Mirrors Rust's
            // dump_stage6_rescored call from rescore::run_rescore.
            if (OspreyDiagnostics.DumpRescored)
            {
                OspreyDiagnostics.WriteStage6RescoredDump(inputs.PerFileEntries);
                if (OspreyDiagnostics.RescoredOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_RESCORED_ONLY");
            }

            // Flush the median-polish inputs dump (no-op when
            // OSPREY_DUMP_MP_INPUTS is unset).
            OspreyDiagnostics.CloseMpInputsDump();

            // Flush the predict() inputs/outputs dump (no-op when
            // OSPREY_DUMP_PREDICT_RT is unset).
            OspreyDiagnostics.ClosePredictRtDump();

            // Flush the CWT path summary dump (no-op when
            // OSPREY_DUMP_CWT_PATH is unset).
            OspreyDiagnostics.CloseCwtPathDump();

            _ctx.LogInfo(
                "--join-at-pass=1 --no-join: rescore complete. Reconciled .scores.parquet " +
                "files written. (C# parity gap: fragment_mzs / fragment_intensities / " +
                "ref_xic_* / bounds_area / bounds_snr columns null, tracked for follow-up.)");
            return 0;
        }

        /// <summary>
        /// Insert <paramref name="value"/> into <paramref name="dict"/> at
        /// <paramref name="key"/> only when <paramref name="value"/> is
        /// non-null. Lifts the null-check out of the call site so
        /// ReSharper's null-flow analysis doesn't flag the indexer
        /// assignment for an unannotated possibly-null source.
        /// </summary>
        private static void AddIfNotNull(Dictionary<string, RTCalibration> dict,
            string key, RTCalibration value)
        {
            if (value != null)
                dict[key] = value;
        }

        /// <summary>
        /// Phase 3 -- write the reconciled per-file <c>.scores.parquet</c>.
        ///
        /// Reload the original Stage 4 parquet's full per-row data
        /// (identity, boundaries, 21 PIN features, CWT candidate lists),
        /// replace re-scored rows in place by <see cref="FdrEntry.ParquetIndex"/>
        /// (NOT by post-compaction Vec position; the two diverge after
        /// first-pass FDR drops non-passing entries), append gap-fill
        /// rows at the end, reassign each gap-fill stub's
        /// <see cref="FdrEntry.ParquetIndex"/> to the actual row it now
        /// occupies, then write back via
        /// <see cref="ParquetScoreCache.WriteScoresParquet(string, List{FdrEntry}, Dictionary{string, string}, Dictionary{uint, LibraryEntry}, string)"/>
        /// with reconciliation metadata
        /// (<c>osprey.reconciled = "true"</c> +
        /// <c>osprey.reconciliation_hash = config.ReconciliationParameterHash()</c>).
        /// Mirrors Rust pipeline.rs:3050-3110.
        /// </summary>
        private void WriteReconciledParquet(string parquetPath, List<FdrEntry> fdrEntries,
            string fileName, List<LibraryEntry> fullLibrary, OspreyConfig config)
        {
            // 1. Reload the original parquet's per-row state.
            List<FdrEntry> fullEntries;
            try
            {
                fullEntries = ParquetScoreCache.LoadFullFdrEntries(parquetPath);
            }
            catch (Exception ex)
            {
                _ctx.LogWarning(string.Format(
                    "Stage 6 write-back: failed to reload {0}: {1} (skipping)",
                    parquetPath, ex.Message));
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
            var metadata = new Dictionary<string, string>
            {
                { @"osprey.version", Program.VERSION },
                { @"osprey.search_hash", config.SearchParameterHash() },
                { @"osprey.library_hash", config.LibraryIdentityHash() },
                { @"osprey.reconciled", @"true" },
                { @"osprey.reconciliation_hash", config.ReconciliationParameterHash() },
            };

            try
            {
                ParquetScoreCache.WriteScoresParquet(parquetPath, fullEntries,
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
        /// Load the original (Stage 1-2) RT calibration for a file from its
        /// sibling .calibration.json. Returns null if the JSON is missing,
        /// has no model_params, or fails to parse.
        /// </summary>
        private RTCalibration LoadOriginalRtCalibration(string inputFile)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(inputFile));
            if (string.IsNullOrEmpty(parent))
                return null;
            string calPath = CalibrationIO.CalibrationPathForInput(inputFile, parent);
            if (!File.Exists(calPath))
                return null;
            try
            {
                var calParams = CalibrationIO.LoadCalibration(calPath);
                if (calParams.RtCalibration?.ModelParams == null)
                    return null;
                var mp = calParams.RtCalibration.ModelParams;
                return RTCalibration.FromModelParams(
                    mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                    calParams.RtCalibration.ResidualSD);
            }
            catch
            {
                return null;
            }
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
            OspreyConfig config)
        {
            // Pre-group reconciliation actions by file. Mirrors the Rust
            // pre-grouping at pipeline.rs:2719-2744 -- a single pass over
            // the action map produces (file -> [(idx, apex, start, end)])
            // so the per-file loop below just looks up its slice.
            var perFileReconTargets =
                new Dictionary<string, List<(int Index, double Apex, double Start, double End)>>();
            int totalReconciliation = 0;
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

            // file_name -> input_files index. Used to pick the right mzML
            // path for spectra cache load + sibling .calibration.json.
            // For the worker, config.InputFiles was synthesized from
            // --input-scores parquet stems by Program.Main; for in-process,
            // it's the user's -i mzML list. Either way the stem matches
            // the file_name keys in perFileEntries.
            var fileNameToIdx = new Dictionary<string, int>();
            for (int i = 0; i < config.InputFiles.Count; i++)
                fileNameToIdx[Path.GetFileNameWithoutExtension(config.InputFiles[i])] = i;

            int totalRescored = 0;
            int totalGapCwt = 0;
            int totalGapForced = 0;
            int nTotalFiles = perFileEntries.Count;

            for (int fileNum = 0; fileNum < nTotalFiles; fileNum++)
            {
                var fileName = perFileEntries[fileNum].Key;
                var fdrEntries = perFileEntries[fileNum].Value;

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

                // Build boundary_overrides keyed by entry_id + entry_id->idx
                // map for the post-scoring overlay step. Also collect the
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

                // Build the subset library for re-scoring. The same library
                // entries the original Stage 1-4 scoring used; we just hand
                // RunCoelutionScoring a smaller list so it doesn't waste
                // work on entries we're not re-scoring.
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

                // Overlay re-scored entries back onto fdr_entries by
                // entry_id. Preserve the original ParquetIndex so the
                // future write-back step can target the right Parquet row
                // (post-compaction Vec position != Parquet row index).
                //
                // Mirror Rust's to_fdr_entry semantics: post-rescore stubs
                // carry default Score (0.0), q-values (1.0), and Pep
                // (1.0). Percolator (Stage 7, second-pass FDR) recomputes
                // these from the new Features. Without this reset the
                // OspreySharp ScoreCandidate's `Score = coelutionSum`
                // initializer (AnalysisPipeline.cs ~line 4088) bleeds
                // through, producing 173k rows of post-rescore divergence
                // vs the Rust worker's rust_stage6_rescored.tsv.
                //
                // Pass 1: index the rescored results by entry_id so we
                // can look up successful re-scores in the second pass.
                var rescoredByEntryId = new Dictionary<uint, FdrEntry>();
                foreach (var entry in rescored)
                {
                    rescoredByEntryId[entry.EntryId] = entry;
                }

                // Pass 2: iterate every combined target. Successful
                // re-scores get the new entry overlaid with reset
                // discriminant fields. Targets where RunCoelutionScoring
                // returned no entry (no peak found at the override
                // boundary) STILL get their existing fdrEntries[idx]
                // reset to default discriminant values. Without this,
                // ~9956 multi-charge consensus targets on Stellar 1-file
                // retain their first-pass Percolator scores in the
                // post-rescore dump while Rust's writes score=0/q=1
                // because Rust's worker emits zeroed stubs for every
                // override regardless of peak success.
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
                int nGapCwt = 0;
                int nGapForced = 0;
                if (gapFillTargets.Count > 0)
                {
                    // Build target+decoy id set from gap_fill_targets.
                    var gapFillIds = new HashSet<uint>();
                    foreach (var gf in gapFillTargets)
                    {
                        gapFillIds.Add(gf.TargetEntryId);
                        gapFillIds.Add(gf.DecoyEntryId);
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

                    // Pass 2: Forced integration for entries CWT missed.
                    // For each gap-fill target, check both the target_id
                    // and decoy_id; either or both may have missed the CWT
                    // pass.
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
                        if (!cwtHitIds.Contains(gf.DecoyEntryId))
                        {
                            forcedOverrides[gf.DecoyEntryId] = (gf.ExpectedRt, start, end);
                            forcedIds.Add(gf.DecoyEntryId);
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

                    totalGapCwt += nGapCwt;
                    totalGapForced += nGapForced;
                    totalRescored += nGapCwt + nGapForced;
                }

                // PHASE 3 -- reconciled parquet write-back.
                if (perFileParquetPaths != null &&
                    perFileParquetPaths.TryGetValue(fileName, out string parquetPath) &&
                    File.Exists(parquetPath))
                {
                    WriteReconciledParquet(parquetPath, fdrEntries, fileName,
                        fullLibrary, config);
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
        /// Stage 2 wrote. Returns uncalibrated results / null MAD if the
        /// file is missing or the relevant section is absent.
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
                return;
            string calPath = CalibrationIO.CalibrationPathForInput(inputFile, parent);
            if (!File.Exists(calPath))
                return;

            CalibrationParams calParams;
            try
            {
                calParams = CalibrationIO.LoadCalibration(calPath);
            }
            catch (Exception ex)
            {
                _ctx.LogWarning(string.Format(
                    "Failed to load calibration JSON {0}: {1}", calPath, ex.Message));
                return;
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
