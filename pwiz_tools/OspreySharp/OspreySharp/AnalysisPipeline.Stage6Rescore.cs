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
using System.Diagnostics;
using System.IO;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Aggregate counts returned by <see cref="AnalysisPipeline.ExecuteStage6Rescore"/>.
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

    public partial class AnalysisPipeline
    {
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
        /// to <see cref="ExecuteStage6Rescore"/>.
        ///
        /// PHASE 1 of the C# port: existing entries (consensus +
        /// reconciliation overlay) re-scored from the v3 sidecar inputs.
        /// Gap-fill two-pass and reconciled .scores.parquet write-back are
        /// the next porting phases; today the worker exits with a clear
        /// pointer message after the rescore loop completes.
        /// </summary>
        internal int RunWorker(OspreyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.InputScores == null || config.InputScores.Count == 0)
            {
                Program.LogError(
                    "--join-at-pass=1 --no-join requires --input-scores <path...>.");
                return 1;
            }

            // Synthesize config.InputFiles from --input-scores so
            // ExecuteStage6Rescore's file_name_to_idx can map file_name
            // back to a real (synthetic) input path. Mirrors Rust's
            // run_analysis idempotent synthesis at pipeline.rs ~line 3144.
            if (config.InputFiles == null || config.InputFiles.Count == 0)
            {
                var synthetic = new List<string>(config.InputScores.Count);
                foreach (var p in config.InputScores)
                    synthetic.Add(RescoreHydration.SyntheticInputFromParquet(p));
                config.InputFiles = synthetic;
            }

            Program.LogInfo(string.Format(
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
                Program.LogError(string.Format(
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
                Program.LogError(string.Format(
                    "--join-at-pass=1 --no-join: hydration failed: {0}", ex.Message));
                return 1;
            }
            Program.LogInfo(string.Format(
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
                Program.LogError(string.Format(
                    "--join-at-pass=1 --no-join: compaction failed: {0}", ex.Message));
                return 1;
            }
            Program.LogInfo(string.Format(
                "Worker compaction: {0} -> {1} entries ({2} surviving base_ids), " +
                "{3} reconciliation actions retained ({4} dropped)",
                compactStats.EntriesBefore,
                compactStats.EntriesAfter,
                compactStats.FirstPassBaseIds,
                inputs.ReconciliationActions.Count,
                compactStats.DroppedActions));

            // Compute per-file multi-charge consensus targets from the
            // compacted stubs. This is fresh per-file work — the planner's
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
            Program.LogInfo(string.Format(
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
                    Program.LogWarning(string.Format(
                        "Worker: no input_files entry for {0} (no original cal loaded)", fileName));
                }
            }
            Program.LogInfo(string.Format(
                "Worker original calibrations: {0}/{1} files loaded",
                perFileCalibrations.Count, inputs.PerFileEntries.Count));

            // PHASE 1 dispatch: existing-entry rescore (consensus +
            // reconciliation overlay) only. Phases 2 (gap-fill) and 3
            // (parquet write-back) land in subsequent commits.
            RescoreStats stats;
            try
            {
                stats = ExecuteStage6Rescore(
                    inputs.PerFileEntries,
                    perFileConsensusTargets,
                    inputs.ReconciliationActions,
                    inputs.RefinedCalibrations,
                    perFileCalibrations,
                    inputs.PerFileGapFill,
                    fullLibrary,
                    config);
            }
            catch (Exception ex)
            {
                Program.LogError(string.Format(
                    "--join-at-pass=1 --no-join: rescore failed: {0}", ex.Message));
                Program.LogError(ex.StackTrace);
                return 1;
            }

            Program.LogInfo(string.Format(
                "Stage 6 rescore: {0} entries re-scored ({1} reconciliation actions executed)",
                stats.TotalRescored, stats.TotalReconciliation));

            // Cross-impl bisection seam: dump per-precursor state
            // immediately after the rescore loop. Mirrors Rust's
            // dump_stage6_rescored call from rescore::run_rescore.
            if (OspreyDiagnostics.DumpRescored)
            {
                OspreyDiagnostics.WriteStage6RescoredDump(inputs.PerFileEntries);
                if (OspreyDiagnostics.RescoredOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_RESCORED_ONLY");
            }

            // Phase 1 success: in-memory state matches what the in-process
            // pipeline holds at the same seam. Phases 2 (gap-fill) and
            // 3 (parquet write-back) are the next steps; until then, the
            // worker can't produce reconciled .scores.parquet output, so
            // exit non-zero with a pointer rather than letting downstream
            // consumers see stale Stage 4 parquets.
            Program.LogError(
                "--join-at-pass=1 --no-join: PHASE 1 (consensus + reconciliation rescore) " +
                "completed cleanly. Phases 2 (gap-fill) and 3 (reconciled parquet write-back) " +
                "are not yet ported; the worker has not written reconciled .scores.parquet " +
                "output. Both will land in subsequent commits.");
            return 2;
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
        /// Today this method covers PHASE 1 of the port: existing
        /// re-scoring (multi-charge consensus + cross-run reconciliation
        /// targets, merged with reconciliation winning on conflict).
        /// The (target, decoy) gap-fill two-pass and reconciled
        /// .scores.parquet write-back are the next porting phases.
        ///
        /// For each file with at least one re-scoring target:
        /// <list type="number">
        ///   <item>Build boundary_overrides keyed by entry_id.</item>
        ///   <item>Subset the library to the entries that need re-scoring.</item>
        ///   <item>Reload spectra from the .spectra.bin cache or the mzML.</item>
        ///   <item>Reload MS2/MS1 mass calibration from the sibling .calibration.json.</item>
        ///   <item>Pick the refined RT calibration when present, else fall back to
        ///       the original first-pass calibration.</item>
        ///   <item>Call <see cref="RunCoelutionScoring"/> with the override-aware
        ///       <see cref="ScoringContext"/>.</item>
        ///   <item>Overlay the re-scored entries back onto the per-file
        ///       FdrEntry stubs by entry_id, preserving ParquetIndex.</item>
        /// </list>
        ///
        /// The mutable <paramref name="perFileEntries"/> is updated in place
        /// (Score, Pep, q-values, Features, ApexRt/StartRt/EndRt, etc.).
        /// Returns <see cref="RescoreStats"/> with the per-stage counts.
        /// </summary>
        internal RescoreStats ExecuteStage6Rescore(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileConsensusTargets,
            IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, List<GapFillTarget>> perFileGapFill,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            // Pre-group reconciliation actions by file. Mirrors the Rust
            // pre-grouping at pipeline.rs:2719-2744 — a single pass over
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
                    // but stay defensive — skip rather than crash.
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
                // map. Reconciliation wins on conflict — the inter-replicate
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
                    LogWarning(string.Format(
                        "Stage 6 rescore: no input_files entry for {0} (skipping)", fileName));
                    continue;
                }
                string inputFile = config.InputFiles[inputIdx];

                LogInfo(string.Format(
                    "Re-scoring file {0}/{1}: {2}", fileNum + 1, nTotalFiles, fileName));
                LogInfo(string.Format(
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
                var entryIdToIdx = new Dictionary<uint, int>();
                var subsetIds = new HashSet<uint>();
                foreach (var kvp in combinedTargets)
                {
                    int idx = kvp.Key;
                    uint entryId = fdrEntries[idx].EntryId;
                    boundaryOverrides[entryId] = kvp.Value;
                    entryIdToIdx[entryId] = idx;
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
                // and read here — same disk-roundtrip path the worker uses.
                LoadMassCalibrations(inputFile,
                    out MzCalibrationResult ms2Cal,
                    out MzCalibrationResult ms1Cal);

                // Pick the RT calibration: refined (from Stage 6 planning's
                // calibration refit) wins; original first-pass falls back.
                if (!refinedCalibrations.TryGetValue(fileName, out RTCalibration rtCal))
                    perFileCalibrations.TryGetValue(fileName, out rtCal);

                // Build the scoring context with the boundary overrides.
                // RunCoelutionScoring inspects context.BoundaryOverrides
                // inside ScoreCandidate and routes through the override
                // peak-construction path.
                var context = new ScoringContext(config, fileName);
                context.BoundaryOverrides = boundaryOverrides;

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
                int nOverlay = 0;
                foreach (var entry in rescored)
                {
                    if (entryIdToIdx.TryGetValue(entry.EntryId, out int idx))
                    {
                        entry.Score = 0.0;
                        entry.RunPrecursorQvalue = 1.0;
                        entry.RunPeptideQvalue = 1.0;
                        entry.RunProteinQvalue = 1.0;
                        entry.ExperimentPrecursorQvalue = 1.0;
                        entry.ExperimentPeptideQvalue = 1.0;
                        entry.ExperimentProteinQvalue = 1.0;
                        entry.Pep = 1.0;
                        entry.ParquetIndex = fdrEntries[idx].ParquetIndex;
                        fdrEntries[idx] = entry;
                        nOverlay++;
                    }
                }
                totalRescored += nOverlay;

                LogInfo(string.Format(
                    "  {0} of {1} existing entries re-scored ({2:F1}s)",
                    nOverlay, combinedTargets.Count, swRescore.Elapsed.TotalSeconds));

                // PHASE 2 — gap-fill two-pass.
                //
                // For each gap-fill target the planner identified (peptides
                // confidently identified in sibling replicates but missing
                // here), score both the TARGET and the paired DECOY against
                // the spectra at the consensus RT. Two-pass strategy:
                //
                //   Pass 1 — CWT: PrefilterEnabled=false, no boundary
                //   overrides. Lets CWT peak detection find a natural peak
                //   inside the rt-tolerance window around the consensus RT.
                //   Catches the easy gap-fills where there's a real peak we
                //   just missed in Stage 4.
                //
                //   Pass 2 — Forced: for entries CWT didn't find, force an
                //   integration window at expected_rt +- half_width via
                //   boundary overrides. Catches the hard gap-fills where
                //   no peak rises above CWT's threshold but we want to
                //   integrate at the expected RT for quantification.
                //
                // Both passes append new FdrEntry stubs to fdr_entries with
                // ParquetIndex = uint.MaxValue (the gap-fill sentinel).
                // Phase 3 (parquet write-back) reassigns the sentinel to a
                // real row index as the entries are appended to the per-file
                // .scores.parquet. Mirrors the Rust gap-fill block at
                // pipeline.rs:2924-3014.
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
                        // config so the disable doesn't bleed into other
                        // files (OspreyConfig.ShallowClone gives us a new
                        // instance whose mutations are local to this file).
                        var cwtConfig = config.ShallowClone();
                        cwtConfig.PrefilterEnabled = false;
                        var cwtContext = new ScoringContext(cwtConfig, fileName);
                        // No BoundaryOverrides — CWT picks peaks freely.

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

                        LogInfo(string.Format(
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

                        var forcedContext = new ScoringContext(config, fileName);
                        forcedContext.BoundaryOverrides = forcedOverrides;

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

                        LogInfo(string.Format(
                            "  Gap-fill forced: {0} integrated ({1:F1}s)",
                            nGapForced, swForced.Elapsed.TotalSeconds));
                    }

                    totalGapCwt += nGapCwt;
                    totalGapForced += nGapForced;
                    totalRescored += nGapCwt + nGapForced;
                }

                // PHASE 3 (reconciled parquet write-back) is the next step.
                // Rust does this at pipeline.rs:3050-3110.
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
                    LogInfo(string.Format(
                        "  Loaded {0} MS2 + {1} MS1 spectra from cache for {2}",
                        spectra.Count, ms1Spectra.Count, fileName));
                    return;
                }
                catch (Exception ex)
                {
                    LogWarning(string.Format(
                        "Failed to load spectra cache {0}: {1}; falling back to mzML",
                        cachePath, ex.Message));
                }
            }
            var fresh = MzmlReader.LoadAllSpectra(inputFile);
            spectra = fresh.Ms2Spectra;
            ms1Spectra = fresh.Ms1Spectra;
            LogInfo(string.Format(
                "  Loaded {0} MS2 + {1} MS1 spectra from mzML for {2}",
                spectra.Count, ms1Spectra.Count, fileName));
        }

        /// <summary>
        /// Load MS2 + MS1 mass calibrations from the sibling
        /// .calibration.json that the original Stage 2 wrote. Returns
        /// uncalibrated results if the file is missing or the relevant
        /// section is absent. Mirrors the Rust load_calibration block at
        /// pipeline.rs:2893-2900 + the ms2/ms1 unpacking the in-process
        /// AnalysisPipeline does at lines 1154-1183.
        /// </summary>
        private void LoadMassCalibrations(string inputFile,
            out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal)
        {
            ms2Cal = MzCalibrationResult.Uncalibrated();
            ms1Cal = MzCalibrationResult.Uncalibrated();

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
                LogWarning(string.Format(
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
        }
    }
}
