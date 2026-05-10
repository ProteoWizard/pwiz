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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.PerFileScoring
{
    /// <summary>
    /// Per-file first-pass scoring phase: load the spectral library
    /// and generate decoys (Stage 1), then for each input mzML run
    /// the parse + RT calibration + main-search scoring pipeline
    /// (Stages 2-4) to produce a per-file
    /// <c>.scores.parquet</c>. The HPC "first per-file fan-out"
    /// boundary in the <c>Osprey-workflow.html</c> view -- each input
    /// file's Stage 1-4 work is independent of every other file's,
    /// so an HPC scheduler can fan this task out across N nodes and
    /// the merge node only needs the resulting parquet sidecars.
    ///
    /// Phase A scope: this task is a thin orchestration wrapper that
    /// delegates to AnalysisPipeline's existing private (now
    /// <c>internal</c>) methods (LoadLibrary, GenerateDecoys,
    /// ProcessFile) plus the --join-only / --join-at-pass=2 input
    /// loading paths that share the same per-file collection layout.
    /// The inline Stage 1-4 block from <c>AnalysisPipeline.Run</c>
    /// moved here verbatim; the only changes are LogInfo / LogWarning
    /// / LogError -> ctx.LogInfo etc. and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Outputs (FullLibrary, LibraryById, PerFileEntries,
    /// PerFileCalibrations, PerFileParquetPaths) are exposed as
    /// instance properties for FirstJoinTask + downstream tasks to
    /// consume after this one completes successfully.
    /// </summary>
    internal sealed class PerFileScoringTask : OspreyTask
    {
        private readonly AnalysisPipeline _pipeline;

        public PerFileScoringTask(AnalysisPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public override string Name => @"PerFileScoring";

        // Outputs read by AnalysisPipeline.Run after Run completes.
        // Defaults match the "library load failed before any other
        // state existed" case so the caller can still safely access
        // the collections without further null guards.
        public List<LibraryEntry> FullLibrary { get; private set; } = new List<LibraryEntry>();
        public Dictionary<uint, LibraryEntry> LibraryById { get; private set; } = new Dictionary<uint, LibraryEntry>();
        public List<KeyValuePair<string, List<FdrEntry>>> PerFileEntries { get; private set; }
            = new List<KeyValuePair<string, List<FdrEntry>>>();
        public ConcurrentDictionary<string, RTCalibration> PerFileCalibrations { get; private set; }
            = new ConcurrentDictionary<string, RTCalibration>();
        public Dictionary<string, string> PerFileParquetPaths { get; private set; }
            = new Dictionary<string, string>();

        public override bool Run(PipelineContext ctx)
        {
            var config = ctx.Config;

            // Stage 1: Load library + generate decoys
            var swLibrary = Stopwatch.StartNew();
            var library = _pipeline.LoadLibrary(config);
            if (library == null || library.Count == 0)
            {
                ctx.LogError(@"Library is empty after loading");
                ctx.ExitCode = 1;
                return false;
            }

            int nLibraryTargets = 0;
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    nLibraryTargets++;
            }
            double libLoadSec = swLibrary.Elapsed.TotalSeconds;
            ctx.LogInfo(string.Format(@"[COUNT] Library targets loaded: {0}", nLibraryTargets));

            List<LibraryEntry> decoys;
            if (config.ExpectReconciledInput)
            {
                // --join-at-pass=2: decoy LibraryEntries are unused
                // downstream. The reconciled parquet already carries
                // both target and decoy FDR rows with their stage-1-4
                // scores; Stage 5 is skipped, Stage 6 is skipped, and
                // the protein-parsimony / blib write paths both filter
                // on `entry.IsDecoy` (only target LibraryEntries get
                // looked up by entry_id). Skipping the rebuild saves
                // ~45s on Astral 1-file (BuildDecoyFromSequence +
                // RecalculateFragments dominated the Stage 7+blib
                // hotspot list). dotTrace OWN-time on Astral 1-file
                // Stage 7 cs run before this fix:
                //   BuildDecoyFromSequence  total=45665 ms (89% wall)
                //   GenerateDecoys.<>b__0   total=46792 ms
                decoys = new List<LibraryEntry>();
            }
            else if (!config.DecoysInLibrary)
            {
                decoys = _pipeline.GenerateDecoys(library, config, out List<LibraryEntry> validTargets);
                library = validTargets;
            }
            else
            {
                decoys = new List<LibraryEntry>();
            }
            swLibrary.Stop();
            double totalSec = swLibrary.Elapsed.TotalSeconds;
            ctx.LogInfo(string.Format(@"[TIMING] Library loading + decoys: {0:F1}s (load: {1:F1}s, decoys: {2:F1}s)",
                totalSec, libLoadSec, totalSec - libLoadSec));

            ctx.LogInfo(string.Format(@"[COUNT] Library decoys generated: {0}", decoys.Count));

            var fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
            fullLibrary.AddRange(library);
            fullLibrary.AddRange(decoys);

            ctx.LogInfo(string.Format(@"Full library: {0} entries ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));
            ctx.LogInfo(string.Format(@"[COUNT] Full library: {0} ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));

            // Count entries with few fragments (diagnostic for entry count parity)
            int nZeroFrag = 0, nOneFrag = 0, nTwoFrag = 0;
            foreach (var entry in fullLibrary)
            {
                int fc = entry.Fragments != null ? entry.Fragments.Count : 0;
                if (fc == 0)
                    nZeroFrag++;
                else if (fc == 1)
                    nOneFrag++;
                else if (fc == 2)
                    nTwoFrag++;
            }
            if (nZeroFrag + nOneFrag + nTwoFrag > 0)
                ctx.LogInfo(string.Format(@"[COUNT] Entries with <3 fragments: {0} (0={1}, 1={2}, 2={3})",
                    nZeroFrag + nOneFrag + nTwoFrag, nZeroFrag, nOneFrag, nTwoFrag));

            // Build library lookup by ID for fast access
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var entry in fullLibrary)
                libraryById[entry.Id] = entry;

            FullLibrary = fullLibrary;
            LibraryById = libraryById;

            // Stage 2-4: Per-file calibration + coelution scoring
            // Process files in parallel when multiple files are provided.
            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            // Per-file RT calibration handles harvested by ProcessFile so
            // Stage 6 reconciliation has the live RTCalibration objects.
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // fileName -> .scores.parquet path. Populated by --join-only
            // (which already knows the paths) so Stage 6 reconciliation
            // can lazily load CWT candidates per file via
            // ParquetScoreCache.LoadCwtCandidatesFromParquet.
            var perFileParquetPaths = new Dictionary<string, string>();

            bool joinOnly = config.InputScores != null && config.InputScores.Count > 0;
            int nFiles = joinOnly ? config.InputScores.Count : config.InputFiles.Count;

            // In --input-scores mode without explicit -i, synthesize
            // InputFiles from the parquet stems so downstream code
            // (Stage 6 rescore's fileNameToIdx in particular) can map
            // each file_name back to a real (synthetic) input path.
            if (joinOnly && (config.InputFiles == null || config.InputFiles.Count == 0))
            {
                var synthetic = new List<string>(config.InputScores.Count);
                foreach (var p in config.InputScores)
                    synthetic.Add(RescoreHydration.SyntheticInputFromParquet(p));
                config.InputFiles = synthetic;
            }

            // Pre-compute the parquet footer metadata ONCE, against the
            // unmutated outer config. ProcessFile clones the config per
            // file and mutates FragmentTolerance during MS2 calibration,
            // so reading config.SearchParameterHash() inside ProcessFile
            // would produce a hash that the join-only validator would
            // not recognize. Built unconditionally (in any non-joinOnly
            // mode) because Stage 6 reconciliation needs the per-file
            // .scores.parquet on disk to lazily load CWT candidates --
            // matches Rust's end-to-end behavior, which always writes
            // the parquet sidecar regardless of --no-join.
            Dictionary<string, string> parquetFooterMetadata = null;
            if (!joinOnly)
            {
                parquetFooterMetadata = new Dictionary<string, string>
                {
                    { @"osprey.version", Program.VERSION },
                    { @"osprey.search_hash", config.SearchParameterHash() },
                    { @"osprey.library_hash", config.LibraryIdentityHash() },
                    { @"osprey.reconciled", @"false" },
                };
            }

            // File-level parallelism is configurable via
            // OSPREY_MAX_PARALLEL_FILES. See AnalysisPipeline (and
            // Osprey-workflow.html) for the policy.
            int maxParallelFiles = OspreyEnvironment.MaxParallelFiles;

            // Determine how many files will actually run concurrently so
            // ProcessFile can divide the inner main-search thread budget
            // and avoid oversubscription. Stored on the config so the
            // per-file clones inherit it.
            if (nFiles == 1 || maxParallelFiles == 1)
                config.EffectiveFileParallelism = 1;
            else if (maxParallelFiles > 1)
                config.EffectiveFileParallelism = Math.Min(maxParallelFiles, nFiles);
            else
                config.EffectiveFileParallelism = Math.Min(nFiles, Environment.ProcessorCount);

            var swAllFiles = Stopwatch.StartNew();
            if (joinOnly)
            {
                // --join-only: load per-file FdrEntry stubs directly from
                // each .scores.parquet listed via --input-scores. Skips
                // Stages 1-4. Side data (calibration, sidecars) is not
                // loaded on the C# side yet -- the simple Stage 5 path
                // doesn't need it. When reconciliation lands, this branch
                // will need to load the calibration JSON sibling files
                // (best-effort, like the Rust impl).
                // Guard: hash check against current --library and search params.
                // Aborts with a clear, file-named error if the operator points
                // the merge node at parquets from a different scoring run.
                string validationError = ParquetScoreCache.ValidateScoresParquetGroup(
                    config.InputScores, config, Program.VERSION, ctx.LogWarning);
                if (validationError != null)
                    throw new InvalidDataException(validationError);

                ctx.LogInfo(string.Format(
                    @"--join-only: loading {0} per-file score parquet(s)",
                    config.InputScores.Count));
                for (int fileIdx = 0; fileIdx < config.InputScores.Count; fileIdx++)
                {
                    string parquetPath = config.InputScores[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(parquetPath);
                    if (fileName.EndsWith(@".scores", StringComparison.Ordinal))
                        fileName = fileName.Substring(0, fileName.Length - @".scores".Length);
                    ctx.LogInfo(string.Format(@"===== Loading file {0}/{1}: {2} (from {3}) =====",
                        fileIdx + 1, config.InputScores.Count, fileName, parquetPath));
                    var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                    // Stage 5+ (Percolator SVM) requires the 21 PIN features
                    // on each FdrEntry. Load them in lockstep with the stubs
                    // and bind by row index (parquet rows are stable).
                    var features = ParquetScoreCache.LoadPinFeaturesFromParquet(parquetPath);
                    if (features.Count != stubs.Count)
                    {
                        throw new InvalidDataException(string.Format(
                            @"--join-only: parquet {0} has {1} stubs but {2} feature rows",
                            parquetPath, stubs.Count, features.Count));
                    }
                    for (int j = 0; j < stubs.Count; j++)
                        stubs[j].Features = features[j];
                    ctx.LogInfo(string.Format(@"  Loaded {0} FDR stubs + features", stubs.Count));
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                    perFileParquetPaths[fileName] = parquetPath;

                    // Best-effort calibration JSON load for Stage 6
                    // reconciliation. Mirrors osprey/src/pipeline.rs:2573-2588.
                    try
                    {
                        string parquetDir = Path.GetDirectoryName(Path.GetFullPath(parquetPath));
                        if (parquetDir != null)
                        {
                            // fileName is the bare input stem (the trailing
                            // ".scores" was stripped above), so combining it
                            // with parquetDir yields the same input-stem path
                            // ProcessFile passes to CalibrationPathForInput.
                            string calStemPath = Path.Combine(parquetDir, fileName);
                            string calPath = CalibrationIO.CalibrationPathForInput(calStemPath, parquetDir);
                            if (File.Exists(calPath))
                            {
                                var calParams = CalibrationIO.LoadCalibration(calPath);
                                if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                                {
                                    var mp = calParams.RtCalibration.ModelParams;
                                    if (OspreyDiagnostics.DumpCalibration)
                                    {
                                        OspreyDiagnostics.WriteStage6CalibrationDump(
                                            fileName, mp.LibraryRts, mp.FittedRts);
                                    }
                                    var rtCal = RTCalibration.FromModelParams(
                                        mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                                        calParams.RtCalibration.ResidualSD);
                                    perFileCalibrations[fileName] = rtCal;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(@"  Failed to load calibration for {0}: {1}", fileName, ex.Message));
                    }
                }
                if (OspreyDiagnostics.CalibrationOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_CALIBRATION_ONLY");
            }
            else if (config.InputFiles.Count == 1)
            {
                // Single file: process directly (no parallel overhead)
                string inputFile = config.InputFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                ctx.LogInfo(string.Empty);
                ctx.LogInfo(string.Format(@"===== Processing file 1/1: {0} =====", inputFile));
                var fileResult = _pipeline.ProcessFile(inputFile, fileName, fullLibrary, config, parquetFooterMetadata, perFileCalibrations);
                if (fileResult != null)
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
            }
            else if (maxParallelFiles == 1)
            {
                // Strictly sequential: one file at a time. Matches the
                // memory envelope of the single-file path while still
                // sharing the library load. Useful for 3-file Astral
                // runs that would OOM in parallel.
                ctx.LogInfo(string.Format(
                    @"[BENCH] OSPREY_MAX_PARALLEL_FILES=1 - processing {0} files sequentially",
                    config.InputFiles.Count));
                for (int fileIdx = 0; fileIdx < config.InputFiles.Count; fileIdx++)
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    ctx.LogInfo(string.Format(@"===== Processing file {0}/{1}: {2} =====",
                        fileIdx + 1, config.InputFiles.Count, inputFile));
                    var fileResult = _pipeline.ProcessFile(inputFile, fileName, fullLibrary, config, parquetFooterMetadata, perFileCalibrations);
                    if (fileResult != null)
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
                }
            }
            else
            {
                // Multiple files: process in parallel, optionally
                // capped via OSPREY_MAX_PARALLEL_FILES.
                var parallelOpts = new ParallelOptions();
                if (maxParallelFiles > 1)
                {
                    parallelOpts.MaxDegreeOfParallelism = maxParallelFiles;
                    ctx.LogInfo(string.Format(
                        @"[BENCH] OSPREY_MAX_PARALLEL_FILES={0} - capping parallel file count",
                        maxParallelFiles));
                }
                var fileResults = new ConcurrentDictionary<int, KeyValuePair<string, List<FdrEntry>>>();
                Parallel.For(0, config.InputFiles.Count, parallelOpts, fileIdx =>
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    ctx.LogInfo(string.Format(@"===== Processing file {0}/{1}: {2} =====",
                        fileIdx + 1, config.InputFiles.Count, inputFile));
                    var fileResult = _pipeline.ProcessFile(inputFile, fileName, fullLibrary, config, parquetFooterMetadata, perFileCalibrations);
                    if (fileResult != null)
                        fileResults[fileIdx] = new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult);
                });
                // Collect in original order
                for (int i = 0; i < config.InputFiles.Count; i++)
                {
                    if (fileResults.TryGetValue(i, out KeyValuePair<string, List<FdrEntry>> result))
                        perFileEntries.Add(result);
                }
            }
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            // End-to-end (non-joinOnly) modes: populate perFileParquetPaths
            // from config.InputFiles so Stage 6 reconciliation can locate
            // each file's freshly-written .scores.parquet to lazy-load CWT
            // candidates from. ProcessFile writes the parquet whenever
            // parquetFooterMetadata != null (now always set in non-joinOnly mode).
            if (!joinOnly)
            {
                foreach (string inputFile in config.InputFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    perFileParquetPaths[fileName] = ParquetScoreCache.GetScoresPath(inputFile);
                }
            }

            int totalScored = 0;
            foreach (var kvp in perFileEntries)
                totalScored += kvp.Value.Count;

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            // --join-at-pass=2: load the 1st-pass FDR scores sidecar for
            // each file onto the freshly-loaded stubs. The sidecar
            // carries the persisted SVM scores + q-values from the
            // straight-through pipeline run that produced these
            // reconciled parquets. Without this load, RunFirstPassProteinFdr
            // and the compaction step (next) would see uninitialized
            // entry.Score = 0 / q = 1 for every entry -- every protein
            // group would tie at score 0 and the picked-protein FDR
            // would collapse. Mirrors Rust pipeline.rs:3823 sidecar
            // load order (1st-pass first, then 2nd-pass after
            // compaction).
            if (config.ExpectReconciledInput)
            {
                // Build a fileName -> synthetic input path map so we
                // can resolve sidecar paths via FdrScoresSidecar.
                var inputByFileName = new Dictionary<string, string>();
                foreach (var inputFile in config.InputFiles)
                    inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

                foreach (var kvp in perFileEntries)
                {
                    string fileName = kvp.Key;
                    var entries = kvp.Value;
                    string sidecarPath = inputByFileName.TryGetValue(fileName, out string inputFile)
                        ? FdrScoresSidecar.Pass1Path(inputFile)
                        : null;
                    if (sidecarPath == null || !File.Exists(sidecarPath))
                    {
                        ctx.LogError(string.Format(
                            @"--join-at-pass=2: missing 1st-pass FDR sidecar for {0} " +
                            @"(expected at {1}). Re-run a straight-through pipeline to " +
                            @"produce the sidecar.",
                            fileName, sidecarPath ?? @"<unresolved>"));
                        ctx.ExitCode = 1;
                        return false;
                    }
                    if (!FdrScoresSidecar.TryRead(sidecarPath, entries,
                            FdrScoresSidecar.Pass.FirstPass))
                    {
                        ctx.LogError(string.Format(
                            @"--join-at-pass=2: 1st-pass sidecar at {0} failed to load " +
                            @"(magic / version / pass-byte / count / size mismatch).",
                            sidecarPath));
                        ctx.ExitCode = 1;
                        return false;
                    }
                }
                ctx.LogInfo(string.Format(
                    @"--join-at-pass=2: loaded 1st-pass FDR sidecars for {0} file(s)",
                    perFileEntries.Count));
            }

            // Surface per-file outputs for downstream tasks before any
            // early-exit so a partial-success caller still sees the
            // populated collections.
            PerFileEntries = perFileEntries;
            PerFileCalibrations = perFileCalibrations;
            PerFileParquetPaths = perFileParquetPaths;

            if (perFileEntries.Count == 0 || totalScored == 0)
            {
                ctx.LogWarning(@"No scored entries found. Cannot perform FDR control.");
                ctx.ExitCode = 0;
                return false;
            }

            // --no-join: stop here. Per-file `.scores.parquet` files are
            // now on disk; a separate `--join-only` invocation (typically
            // on a merge node) will pick them up and run Stage 5+.
            if (config.NoJoin)
            {
                ctx.LogInfo(string.Format(
                    @"--no-join: Stage 1-4 complete. {0} entries scored across {1} file(s). " +
                    @"Per-file `.scores.parquet` written next to each input mzML. " +
                    @"Skipping FDR and blib output.",
                    totalScored, nFiles));
                ctx.ExitCode = 0;
                return false;
            }

            return true;
        }
    }
}
