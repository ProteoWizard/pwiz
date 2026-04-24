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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Main analysis pipeline orchestrating the end-to-end Osprey workflow.
    /// Port of osprey/src/pipeline.rs run_analysis().
    ///
    /// Stages:
    /// 1. Load library + generate decoys
    /// 2. Per-file: load spectra, calibrate RT, run coelution scoring
    /// 3. First-pass FDR (Percolator or simple)
    /// 4. Protein FDR (optional)
    /// 5. Write blib output
    /// </summary>
    public class AnalysisPipeline
    {
        private const int NUM_PIN_FEATURES = 21;

        // Serializes mzML reads across concurrent ProcessFile() calls.
        // The producer inside MzmlReader.LoadAllSpectra is a sequential
        // XmlReader over a FileStream, so 3 files parsing in parallel
        // means 3 sequential disk scans fighting for the same head/cache.
        // Gating the parse step funnels the disk-bound work into one
        // stream at a time while leaving the subsequent main-search
        // phase free to run in parallel across files.
        private static readonly SemaphoreSlim s_mzmlReadGate = new SemaphoreSlim(1, 1);

        // Savitzky-Golay quadratic filter weights for length 5, center offset.
        // Matches Rust pipeline.rs sg_weights: [-3/35, 12/35, 17/35, 12/35, -3/35].
        private static readonly double[] SG_WEIGHTS =
        {
            -3.0 / 35.0,
            12.0 / 35.0,
            17.0 / 35.0,
            12.0 / 35.0,
            -3.0 / 35.0,
        };

        // Calibration XCorr always uses unit-resolution bins (~2K) regardless of
        // instrument resolution mode. Matches the spec in Rust osprey
        // docs/02-calibration.md ("Comet-style XCorr (unit resolution, BLAS
        // sdot)") and the calibration_xcorr_scorer helper in
        // osprey/crates/osprey/src/pipeline.rs, and avoids the LOH allocation
        // pressure that 100K-bin arrays cause on .NET Framework's large-object
        // heap. Main search XCorr still uses the resolution-mode bins via the
        // IResolutionStrategy abstraction. Exposed as internal so
        // OspreySharp.Test can assert the bin-config invariant.
        internal static readonly SpectralScorer s_calXcorrScorer =
            new SpectralScorer(BinConfig.UnitResolution());

        // Local alias so existing dump code using F10 continues to read cleanly.
        // The actual formatter lives in OspreyDiagnostics now.
        private static string F10(double v)
        {
            return OspreyDiagnostics.F10(v);
        }

        /// <summary>
        /// Run the complete analysis pipeline.
        /// </summary>
        /// <param name="config">Analysis configuration.</param>
        /// <returns>0 on success, non-zero on failure.</returns>
        public int Run(OspreyConfig config)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Stage 1: Load library + generate decoys
                var swLibrary = Stopwatch.StartNew();
                var library = LoadLibrary(config);
                if (library == null || library.Count == 0)
                {
                    LogError("Library is empty after loading");
                    return 1;
                }

                int nLibraryTargets = 0;
                foreach (var entry in library)
                {
                    if (!entry.IsDecoy)
                        nLibraryTargets++;
                }
                double libLoadSec = swLibrary.Elapsed.TotalSeconds;
                LogInfo(string.Format("[COUNT] Library targets loaded: {0}", nLibraryTargets));

                List<LibraryEntry> decoys;
                if (!config.DecoysInLibrary)
                {
                    List<LibraryEntry> validTargets;
                    decoys = GenerateDecoys(library, config, out validTargets);
                    library = validTargets;
                }
                else
                {
                    decoys = new List<LibraryEntry>();
                }
                swLibrary.Stop();
                double totalSec = swLibrary.Elapsed.TotalSeconds;
                LogInfo(string.Format("[TIMING] Library loading + decoys: {0:F1}s (load: {1:F1}s, decoys: {2:F1}s)",
                    totalSec, libLoadSec, totalSec - libLoadSec));

                LogInfo(string.Format("[COUNT] Library decoys generated: {0}", decoys.Count));

                var fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
                fullLibrary.AddRange(library);
                fullLibrary.AddRange(decoys);

                LogInfo(string.Format("Full library: {0} entries ({1} targets + {2} decoys)",
                    fullLibrary.Count, library.Count, decoys.Count));
                LogInfo(string.Format("[COUNT] Full library: {0} ({1} targets + {2} decoys)",
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
                    LogInfo(string.Format("[COUNT] Entries with <3 fragments: {0} (0={1}, 1={2}, 2={3})",
                        nZeroFrag + nOneFrag + nTwoFrag, nZeroFrag, nOneFrag, nTwoFrag));

                // Build library lookup by ID for fast access
                var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
                foreach (var entry in fullLibrary)
                    libraryById[entry.Id] = entry;

                // Stage 2-4: Per-file calibration + coelution scoring
                // Process files in parallel when multiple files are provided.
                var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();

                bool joinOnly = config.InputScores != null && config.InputScores.Count > 0;
                int nFiles = joinOnly ? config.InputScores.Count : config.InputFiles.Count;

                // Pre-compute the parquet footer metadata ONCE, against the
                // unmutated outer config. ProcessFile clones the config per
                // file and mutates FragmentTolerance during MS2 calibration,
                // so reading config.SearchParameterHash() inside ProcessFile
                // would produce a hash that the join-only validator would
                // not recognize. Dictionary stays null in non-NoJoin runs
                // (no parquet writing happens then).
                Dictionary<string, string> noJoinMetadata = null;
                if (config.NoJoin)
                {
                    noJoinMetadata = new Dictionary<string, string>
                    {
                        { "osprey.version", Program.VERSION },
                        { "osprey.search_hash", config.SearchParameterHash() },
                        { "osprey.library_hash", config.LibraryIdentityHash() },
                        { "osprey.reconciled", "false" },
                    };
                }

                // File-level parallelism is configurable via
                // OSPREY_MAX_PARALLEL_FILES:
                //   1      = strictly sequential (one file at a time)
                //   N > 1  = up to N files concurrently via Parallel.For
                //            with MaxDegreeOfParallelism = N
                //   unset/<=0 = Parallel.For default (all files at once,
                //            bounded by the TPL thread pool)
                // Memory-critical 3-file Astral runs on a 64 GB machine
                // can cap this at 1 or 2 to keep the per-file pool +
                // spectra under budget; Stellar stays on default for
                // max throughput.
                int maxParallelFiles = OspreyEnvironment.MaxParallelFiles;

                // Determine how many files will actually run concurrently so
                // ProcessFile can divide the inner main-search thread budget
                // and avoid oversubscription (see notes there). Stored on
                // the config so the per-file clones inherit it.
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
                        config.InputScores, config, Program.VERSION, LogWarning);
                    if (validationError != null)
                        throw new InvalidDataException(validationError);

                    LogInfo(string.Format(
                        "--join-only: loading {0} per-file score parquet(s)",
                        config.InputScores.Count));
                    for (int fileIdx = 0; fileIdx < config.InputScores.Count; fileIdx++)
                    {
                        string parquetPath = config.InputScores[fileIdx];
                        string fileName = Path.GetFileNameWithoutExtension(parquetPath);
                        if (fileName.EndsWith(".scores", StringComparison.Ordinal))
                            fileName = fileName.Substring(0, fileName.Length - ".scores".Length);
                        LogInfo(string.Format("===== Loading file {0}/{1}: {2} (from {3}) =====",
                            fileIdx + 1, config.InputScores.Count, fileName, parquetPath));
                        var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                        // Stage 5+ (Percolator SVM) requires the 21 PIN features
                        // on each FdrEntry. Load them in lockstep with the stubs
                        // and bind by row index (parquet rows are stable).
                        var features = ParquetScoreCache.LoadPinFeaturesFromParquet(parquetPath);
                        if (features.Count != stubs.Count)
                        {
                            throw new InvalidDataException(string.Format(
                                "--join-only: parquet {0} has {1} stubs but {2} feature rows",
                                parquetPath, stubs.Count, features.Count));
                        }
                        for (int j = 0; j < stubs.Count; j++)
                            stubs[j].Features = features[j];
                        LogInfo(string.Format("  Loaded {0} FDR stubs + features", stubs.Count));
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                    }
                }
                else if (config.InputFiles.Count == 1)
                {
                    // Single file: process directly (no parallel overhead)
                    string inputFile = config.InputFiles[0];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    LogInfo("");
                    LogInfo(string.Format("===== Processing file 1/1: {0} =====", inputFile));
                    var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config, noJoinMetadata);
                    if (fileResult != null)
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
                }
                else if (maxParallelFiles == 1)
                {
                    // Strictly sequential: one file at a time. Matches the
                    // memory envelope of the single-file path while still
                    // sharing the library load. Useful for 3-file Astral
                    // runs that would OOM in parallel.
                    LogInfo(string.Format(
                        "[BENCH] OSPREY_MAX_PARALLEL_FILES=1 - processing {0} files sequentially",
                        config.InputFiles.Count));
                    for (int fileIdx = 0; fileIdx < config.InputFiles.Count; fileIdx++)
                    {
                        string inputFile = config.InputFiles[fileIdx];
                        string fileName = Path.GetFileNameWithoutExtension(inputFile);
                        LogInfo(string.Format("===== Processing file {0}/{1}: {2} =====",
                            fileIdx + 1, config.InputFiles.Count, inputFile));
                        var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config, noJoinMetadata);
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
                        LogInfo(string.Format(
                            "[BENCH] OSPREY_MAX_PARALLEL_FILES={0} - capping parallel file count",
                            maxParallelFiles));
                    }
                    var fileResults = new ConcurrentDictionary<int, KeyValuePair<string, List<FdrEntry>>>();
                    Parallel.For(0, config.InputFiles.Count, parallelOpts, fileIdx =>
                    {
                        string inputFile = config.InputFiles[fileIdx];
                        string fileName = Path.GetFileNameWithoutExtension(inputFile);
                        LogInfo(string.Format("===== Processing file {0}/{1}: {2} =====",
                            fileIdx + 1, config.InputFiles.Count, inputFile));
                        var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config, noJoinMetadata);
                        if (fileResult != null)
                            fileResults[fileIdx] = new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult);
                    });
                    // Collect in original order
                    for (int i = 0; i < config.InputFiles.Count; i++)
                    {
                        KeyValuePair<string, List<FdrEntry>> result;
                        if (fileResults.TryGetValue(i, out result))
                            perFileEntries.Add(result);
                    }
                }
                swAllFiles.Stop();
                LogInfo(string.Format("[TIMING] All files processed: {0:F1}s",
                    swAllFiles.Elapsed.TotalSeconds));

                int totalScored = 0;
                foreach (var kvp in perFileEntries)
                    totalScored += kvp.Value.Count;

                LogInfo("");
                LogInfo(string.Format(
                    "Coelution analysis complete. {0} total scored entries across {1} files",
                    totalScored, nFiles));

                if (perFileEntries.Count == 0 || totalScored == 0)
                {
                    LogWarning("No scored entries found. Cannot perform FDR control.");
                    return 0;
                }

                // --no-join: stop here. Per-file `.scores.parquet` files are
                // now on disk; a separate `--join-only` invocation (typically
                // on a merge node) will pick them up and run Stage 5+.
                if (config.NoJoin)
                {
                    LogInfo(string.Format(
                        "--no-join: Stage 1-4 complete. {0} entries scored across {1} file(s). " +
                        "Per-file `.scores.parquet` written next to each input mzML. " +
                        "Skipping FDR and blib output.",
                        totalScored, nFiles));
                    return 0;
                }

                // Stage 5: First-pass FDR
                LogInfo("");
                LogInfo(string.Format("Running {0} FDR control on coelution results...",
                    config.FdrMethod));

                var swFdr = Stopwatch.StartNew();
                RunFdr(perFileEntries, fullLibrary, config);
                swFdr.Stop();
                LogInfo(string.Format("[TIMING] Percolator/Simple FDR: {0:F1}s",
                    swFdr.Elapsed.TotalSeconds));

                // Log first-pass results
                int passingTargets = 0;
                foreach (var kvp in perFileEntries)
                {
                    int fileTargets = 0;
                    foreach (var entry in kvp.Value)
                    {
                        if (!entry.IsDecoy &&
                            entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                        {
                            fileTargets++;
                        }
                    }
                    LogInfo(string.Format("  {0}: {1} precursors at {2:P1} run-level FDR",
                        kvp.Key, fileTargets, config.RunFdr));
                    passingTargets += fileTargets;
                }
                LogInfo(string.Format("Total: {0} precursors pass run-level FDR across all files",
                    passingTargets));

                // Stage 5 diagnostic dump. Gated by OSPREY_DUMP_PERCOLATOR=1;
                // exits when OSPREY_PERCOLATOR_ONLY=1 is also set. Writes all
                // four q-values plus SVM score and PEP for every FdrEntry,
                // before compaction drops any rows, so the cross-impl diff
                // sees both targets and decoys.
                if (OspreyDiagnostics.DumpPercolator)
                {
                    OspreyDiagnostics.WriteStage5PercolatorDump(perFileEntries);
                    if (OspreyDiagnostics.PercolatorOnly)
                        OspreyDiagnostics.ExitAfterDump("OSPREY_PERCOLATOR_ONLY");
                }

                // Stage 6-7: Reconciliation (TODO for multi-file)
                if (config.InputFiles.Count > 1)
                {
                    LogInfo("");
                    LogInfo("TODO: Inter-replicate reconciliation not yet implemented");
                    LogInfo("      First-pass results are still usable for single-run analysis");
                }

                // Stage 8: Protein FDR (optional)
                if (config.ProteinFdr.HasValue)
                {
                    LogInfo("");
                    LogInfo(string.Format("Running protein-level FDR at {0:P1}...",
                        config.ProteinFdr.Value));
                    var swProtein = Stopwatch.StartNew();
                    RunProteinFdr(perFileEntries, fullLibrary, config);
                    swProtein.Stop();
                    LogInfo(string.Format("[TIMING] Protein FDR: {0:F1}s",
                        swProtein.Elapsed.TotalSeconds));
                }

                // Stage 9: Write output blib
                LogInfo("");
                LogInfo(string.Format("Writing output to {0}...", config.OutputBlib));
                var swBlib = Stopwatch.StartNew();
                WriteBlibOutput(perFileEntries, fullLibrary, libraryById, config);
                swBlib.Stop();
                LogInfo(string.Format("[TIMING] Blib output: {0:F1}s",
                    swBlib.Elapsed.TotalSeconds));

                stopwatch.Stop();
                LogInfo("");
                LogInfo(string.Format("[TIMING] Total pipeline: {0:F1}s",
                    stopwatch.Elapsed.TotalSeconds));
                LogInfo(string.Format("Analysis complete in {0}", FormatDuration(stopwatch.Elapsed)));
                return 0;
            }
            catch (Exception ex)
            {
                LogError(string.Format("Pipeline failed: {0}", ex.Message));
                LogError(ex.StackTrace);
                return 1;
            }
        }

        #region Stage 1: Library Loading

        /// <summary>
        /// Load spectral library from the configured source, using binary cache
        /// when available. Matches Rust's .libcache mechanism for fast reload.
        /// </summary>
        private List<LibraryEntry> LoadLibrary(OspreyConfig config)
        {
            string path = config.LibrarySource.Path;
            string cachePath = path + ".libcache";

            // Try loading from binary cache first
            if (File.Exists(cachePath))
            {
                try
                {
                    var cached = LibraryCache.LoadCache(cachePath);
                    if (cached != null && cached.Count > 0)
                    {
                        LogInfo(string.Format(
                            "Loaded {0} library entries from cache '{1}'",
                            cached.Count, cachePath));
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning(string.Format(
                        "Failed to load library cache: {0}. Falling back to source.", ex.Message));
                }
            }

            // Parse from source
            LogInfo(string.Format("Loading spectral library from {0}...", path));

            List<LibraryEntry> entries;

            switch (config.LibrarySource.Format)
            {
                case LibraryFormat.DiannTsv:
                    var tsvLoader = new DiannTsvLoader();
                    entries = tsvLoader.Load(path);
                    break;

                case LibraryFormat.Blib:
                    var blibLoader = new BlibLoader();
                    entries = blibLoader.Load(path);
                    break;

                case LibraryFormat.Elib:
                    var elibLoader = new ElibLoader();
                    entries = elibLoader.Load(path);
                    break;

                default:
                    throw new NotSupportedException(string.Format(
                        "Unsupported library format: {0}", config.LibrarySource.Format));
            }

            // Deduplicate library entries
            entries = LibraryDeduplicator.DeduplicateLibrary(entries);

            LogInfo(string.Format("Loaded {0} library entries", entries.Count));

            // Save binary cache for next run
            try
            {
                LibraryCache.SaveCache(cachePath, entries);
                LogInfo(string.Format(
                    "Saved library cache ({0} entries) to '{1}'",
                    entries.Count, cachePath));
            }
            catch (Exception ex)
            {
                LogWarning(string.Format("Failed to save library cache: {0}", ex.Message));
            }

            return entries;
        }

        /// <summary>
        /// Generate decoy entries from the target library with collision detection.
        /// Matches Rust DecoyGenerator.generate_all_with_collision_detection:
        ///   1. Build set of target sequences (stripped) for collision detection
        ///   2. For each target, try reversing
        ///   3. If reversed collides or is palindromic, try cycling with lengths 1..10
        ///   4. If all methods fail, exclude the target-decoy pair
        /// Modifies <paramref name="validTargets"/> to contain only targets that
        /// produced valid decoys (Rust: library = valid_targets; library.extend(decoys)).
        /// </summary>
        private List<LibraryEntry> GenerateDecoys(
            List<LibraryEntry> targets, OspreyConfig config,
            out List<LibraryEntry> validTargets)
        {
            LogInfo(string.Format("Generating decoys using {0} method...", config.DecoyMethod));

            // Build set of all target (stripped) sequences for collision detection.
            var targetSequences = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in targets)
            {
                if (!t.IsDecoy)
                    targetSequences.Add(t.Sequence);
            }

            // Generate decoys in parallel (matches Rust's par_iter approach).
            // Each target produces a (target, decoy) pair or is excluded.
            int nReversed = 0, nCycled = 0, nExcluded = 0, nSkipped = 0;
            var results = new (LibraryEntry target, LibraryEntry decoy, int kind)[targets.Count];
            // kind: 0=skip, 1=reversed, 2=cycled, 3=excluded

            Parallel.For(0, targets.Count, i =>
            {
                var target = targets[i];
                if (target.IsDecoy || target.Fragments == null || target.Fragments.Count == 0)
                {
                    results[i] = (null, null, 0);
                    return;
                }

                // Each thread gets its own generator (DecoyGenerator is lightweight)
                var gen = new DecoyGenerator();
                int[] mapping;
                string reversedSeq = gen.ReverseSequence(target.Sequence, out mapping);

                if (reversedSeq != target.Sequence && !targetSequences.Contains(reversedSeq))
                {
                    var decoy = BuildDecoyFromSequence(target, reversedSeq, mapping);
                    if (decoy != null)
                    {
                        results[i] = (target, decoy, 1);
                        return;
                    }
                }

                // Fallback: cycling with lengths 1..min(len, 10)
                int maxRetries = Math.Min(target.Sequence.Length, 10);
                for (int cycleLength = 1; cycleLength <= maxRetries; cycleLength++)
                {
                    string cycledSeq = gen.CycleSequence(target.Sequence, cycleLength, out mapping);
                    if (cycledSeq != target.Sequence && !targetSequences.Contains(cycledSeq))
                    {
                        var decoy = BuildDecoyFromSequence(target, cycledSeq, mapping);
                        if (decoy != null)
                        {
                            results[i] = (target, decoy, 2);
                            return;
                        }
                    }
                }

                results[i] = (null, null, 3);
            });

            // Collect results (sequential, preserves order)
            validTargets = new List<LibraryEntry>(targets.Count);
            var decoys = new List<LibraryEntry>(targets.Count);
            foreach (var r in results)
            {
                switch (r.kind)
                {
                    case 0: nSkipped++; break;
                    case 1: nReversed++; validTargets.Add(r.target); decoys.Add(r.decoy); break;
                    case 2: nCycled++; validTargets.Add(r.target); decoys.Add(r.decoy); break;
                    case 3: nExcluded++; break;
                }
            }

            LogInfo(string.Format(
                "Generated {0} decoys from {1} targets ({2} excluded due to collisions)",
                decoys.Count, targets.Count, nExcluded));
            return decoys;
        }

        /// <summary>
        /// Build a decoy LibraryEntry from a decoy sequence and position mapping.
        /// Mirrors DecoyGenerator.Generate's construction but takes an already-chosen sequence.
        /// </summary>
        private static LibraryEntry BuildDecoyFromSequence(
            LibraryEntry target, string decoySequence, int[] positionMapping)
        {
            var decoy = new LibraryEntry(
                target.Id | 0x80000000u,
                decoySequence,
                "DECOY_" + target.ModifiedSequence,
                target.Charge,
                target.PrecursorMz,
                target.RetentionTime);
            decoy.RtCalibrated = target.RtCalibrated;
            decoy.IsDecoy = true;
            decoy.Modifications = DecoyGenerator.RemapModificationsStatic(
                target.Modifications, positionMapping);
            decoy.Fragments = DecoyGenerator.RecalculateFragmentsStatic(
                target, positionMapping, decoySequence);
            decoy.ProteinIds = new List<string>();
            foreach (string p in target.ProteinIds)
                decoy.ProteinIds.Add("DECOY_" + p);
            decoy.GeneNames = new List<string>(target.GeneNames);
            return decoy;
        }

        #endregion

        #region Stage 2-4: Per-File Processing

        /// <summary>
        /// Process a single mzML file: load spectra, calibrate RT, score coelution.
        /// </summary>
        private List<FdrEntry> ProcessFile(
            string inputFile, string fileName,
            List<LibraryEntry> fullLibrary, OspreyConfig config,
            Dictionary<string, string> noJoinMetadata)
        {
            if (inputFile == null)
                throw new ArgumentNullException(nameof(inputFile));

            // Per-file shallow clone so MS2 calibration's mutation of
            // FragmentTolerance (and any future per-file overrides) does
            // not leak between parallel ProcessFile() calls. Without this
            // the shared OspreyConfig.FragmentTolerance gets clobbered
            // by whichever calibration completes second, causing
            // ~1000-2000 entries per file to score with the wrong
            // (foreign-file-calibrated) tolerance under -MaxParallelFiles>1.
            config = config.ShallowClone();

            // Divide the inner main-search thread budget across concurrent
            // files so total demand stays near core count. With 3 files x
            // 32 threads on a 32-core box, the prior 96-way oversubscription
            // produced 45-95s wall-time variance on Stellar; a fair share
            // (10 threads each) holds the run near steady-state.
            if (config.EffectiveFileParallelism > 1)
            {
                int perFileThreads = Math.Max(1, config.NThreads / config.EffectiveFileParallelism);
                LogInfo(string.Format(
                    "[BENCH] Per-file thread cap: {0} ({1} total / {2} files in parallel)",
                    perFileThreads, config.NThreads, config.EffectiveFileParallelism));
                config.NThreads = perFileThreads;
            }

            var context = new ScoringContext(config, fileName);

            // Load spectra (from mzML or .spectra.bin cache)
            List<Spectrum> spectra;
            List<MS1Spectrum> ms1Spectra;
            var swParse = Stopwatch.StartNew();
            LoadSpectra(inputFile, config.EffectiveFileParallelism > 1,
                out spectra, out ms1Spectra);
            swParse.Stop();

            long inputBytes = 0;
            try
            {
                if (File.Exists(inputFile))
                    inputBytes = new FileInfo(inputFile).Length;
            }
            catch
            {
                inputBytes = 0;
            }

            double parseSeconds = swParse.Elapsed.TotalSeconds;
            if (inputBytes > 0 && parseSeconds > 0.001)
            {
                double mbPerSec = (inputBytes / 1024.0 / 1024.0) / parseSeconds;
                LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s ({1:F1} MB/s)",
                    parseSeconds, mbPerSec));
            }
            else
            {
                LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s", parseSeconds));
            }

            if (spectra == null || spectra.Count == 0)
            {
                LogWarning(string.Format("No spectra found in {0}", inputFile));
                return null;
            }

            LogInfo(string.Format("Loaded {0} MS2 spectra and {1} MS1 spectra",
                spectra.Count, ms1Spectra != null ? ms1Spectra.Count : 0));
            LogInfo(string.Format("[COUNT] mzML spectra loaded [{0}]: {1} MS2 + {2} MS1",
                fileName, spectra.Count, ms1Spectra != null ? ms1Spectra.Count : 0));

            // Extract isolation windows from spectra
            var isolationWindows = ExtractIsolationWindows(spectra);
            LogInfo(string.Format("Found {0} unique isolation windows", isolationWindows.Count));
            LogInfo(string.Format("[COUNT] Isolation windows [{0}]: {1}",
                fileName, isolationWindows.Count));

            // RT calibration
            RTCalibration rtCalibration = null;
            MzCalibrationResult ms2Cal = MzCalibrationResult.Uncalibrated();
            MzCalibrationResult ms1Cal = MzCalibrationResult.Uncalibrated();

            // BISECT: load Rust's calibration JSON instead of computing our own.
            // This eliminates calibration noise from the feature comparison.
            // Usage: OSPREY_LOAD_CALIBRATION=Ste-...20.calibration.json
            string loadCalPath = OspreyEnvironment.LoadCalibrationPath;
            if (!string.IsNullOrEmpty(loadCalPath) && File.Exists(loadCalPath))
            {
                LogInfo(string.Format("[BISECT] Loading calibration from: {0}", loadCalPath));
                var calParams = CalibrationIO.LoadCalibration(loadCalPath);
                if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                {
                    var mp = calParams.RtCalibration.ModelParams;
                    rtCalibration = RTCalibration.FromModelParams(
                        mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                        calParams.RtCalibration.ResidualSD);
                    LogInfo(string.Format("Loaded RT calibration: {0} points, R2={1:F4}",
                        calParams.RtCalibration.NPoints, calParams.RtCalibration.RSquared));
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
                    LogInfo(string.Format("Loaded MS2 calibration: mean={0:F4} {1}, SD={2:F4}",
                        ms2Cal.Mean, ms2Cal.Unit, ms2Cal.SD));
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
                    LogInfo(string.Format("Loaded MS1 calibration: mean={0:F4} {1}, SD={2:F4}",
                        ms1Cal.Mean, ms1Cal.Unit, ms1Cal.SD));
                }
            }
            else if (config.RtCalibration.Enabled)
            {
                var swCal = Stopwatch.StartNew();
                rtCalibration = RunCalibration(
                    fullLibrary, spectra, ms1Spectra, context,
                    out ms1Cal, out ms2Cal);
                swCal.Stop();
                int nPoints = rtCalibration != null ? rtCalibration.Stats().NPoints : 0;
                LogInfo(string.Format(
                    "[TIMING] RT calibration: {0:F1}s ({1} calibration points)",
                    swCal.Elapsed.TotalSeconds, nPoints));
            }

            // Dump 11 calibration summary scalars (MS1/MS2 mean/sd/count/
            // tolerance + RT n_points/r_squared/residual_sd) so the final
            // calibration state can be diff'd against Rust's cal JSON.
            OspreyDiagnostics.WriteCalibrationSummary(rtCalibration, ms1Cal, ms2Cal);

            // Save the full calibration state to {inputStem}.calibration.json
            // in the same directory as the mzML input. Same schema as Rust
            // so the file is round-trippable via OSPREY_LOAD_CALIBRATION in
            // either tool. Enables HPC "compute cal once, reuse on another
            // node" and the cross-runtime cal-swap bisection.
            try
            {
                var calParams = new CalibrationParams
                {
                    Metadata = new CalibrationMetadata
                    {
                        CalibrationSuccessful = rtCalibration != null,
                        NumConfidentPeptides = 0,
                        NumSampledPrecursors = 0,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    },
                    Ms1Calibration = MzCalibrationJson.FromResult(ms1Cal),
                    Ms2Calibration = MzCalibrationJson.FromResult(ms2Cal),
                    RtCalibration = RTCalibrationJson.FromRTCalibration(rtCalibration),
                    SecondPassRt = null
                };
                // Path.GetDirectoryName can return null for a root path; default
                // to the current dir so the calibration JSON still has a home.
                string calDir = Path.GetDirectoryName(Path.GetFullPath(inputFile)) ?? ".";
                string calPath = CalibrationIO.CalibrationPathForInput(inputFile, calDir);
                CalibrationIO.SaveCalibration(calParams, calPath);
                LogInfo(string.Format("Saved calibration to {0}", calPath));
            }
            catch (Exception ex)
            {
                LogInfo("Warning: failed to save calibration JSON: " + ex.Message);
            }

            // Optional early exit after Stage 3 (calibration only, no main search).
            // Used for Stage 1-3 perf benchmarking and walking up to the main
            // search incrementally without paying the Stage 4 cost.
            if (OspreyEnvironment.ExitAfterCalibration)
            {
                LogInfo("[BENCH] OSPREY_EXIT_AFTER_CALIBRATION set - exiting after Stage 3 (calibration done)");
                return new List<FdrEntry>();
            }

            // Run coelution scoring across all isolation windows
            var swScoring = Stopwatch.StartNew();
            var scoredEntries = RunCoelutionScoring(
                fullLibrary, spectra, ms1Spectra,
                isolationWindows, rtCalibration,
                ms2Cal, ms1Cal,
                context);
            swScoring.Stop();
            double scoringSeconds = swScoring.Elapsed.TotalSeconds;
            double ratePerSec = scoringSeconds > 0.001
                ? scoredEntries.Count / scoringSeconds
                : 0.0;
            LogInfo(string.Format(
                "[TIMING] Coelution scoring: {0:F1}s ({1} candidates, {2:F0} cand/s)",
                scoringSeconds, scoredEntries.Count, ratePerSec));

            int nScoredTargets = scoredEntries.Count(e => !e.IsDecoy);
            int nScoredDecoys = scoredEntries.Count(e => e.IsDecoy);
            LogInfo(string.Format("Scored {0} entries ({1} targets, {2} decoys) for {3}",
                scoredEntries.Count,
                nScoredTargets,
                nScoredDecoys,
                fileName));
            LogInfo(string.Format(
                "[COUNT] Coelution scored [{0}]: {1} entries ({2} targets, {3} decoys)",
                fileName, scoredEntries.Count, nScoredTargets, nScoredDecoys));

            // Deduplicate: keep best target and best decoy per base_id
            int nBeforeDedup = scoredEntries.Count;
            scoredEntries = DeduplicatePairs(scoredEntries);
            int nAfterDedup = scoredEntries.Count;
            LogInfo(string.Format(
                "[COUNT] Deduplication [{0}]: {1} -> {2} ({3} removed)",
                fileName, nBeforeDedup, nAfterDedup, nBeforeDedup - nAfterDedup));

            // Optional: write per-entry feature TSV for comparison against Rust's PIN output
            if (config.WritePin)
            {
                WriteFeatureDump(inputFile, fileName, scoredEntries);
            }

            // --no-join: persist the full FdrEntry results (with features) to
            // {stem}.scores.parquet so a subsequent --join-only invocation can
            // pick them up without re-running Stages 1-4. Same path convention
            // as Rust (`scores_path_for_input`). Snappy-compressed; cross-impl
            // ZSTD/Snappy compatibility tracked as a Phase 4 follow-up.
            // The metadata dictionary is precomputed in Run() against the
            // original (un-mutated) outer config -- see Run() for why.
            if (noJoinMetadata != null)
            {
                string parquetPath = ParquetScoreCache.GetScoresPath(inputFile);
                // Build a per-file lookup so the writer can pull
                // sequence/precursor_mz/protein_ids from the library by
                // entry_id (FdrEntry doesn't carry these). One pass over
                // ~485K library entries is well under a second.
                var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
                foreach (var entry in fullLibrary)
                    libraryById[entry.Id] = entry;
                var swParquet = Stopwatch.StartNew();
                ParquetScoreCache.WriteScoresParquet(
                    parquetPath, scoredEntries, noJoinMetadata, libraryById, fileName);
                swParquet.Stop();
                LogInfo(string.Format(
                    "[--no-join] Wrote {0} scored entries to {1} ({2:F1}s)",
                    scoredEntries.Count, parquetPath, swParquet.Elapsed.TotalSeconds));
            }

            return scoredEntries;
        }

        /// <summary>
        /// Write a TSV dump of per-entry feature values for direct comparison with
        /// the Rust implementation's PIN output. Format matches Rust's PIN columns:
        /// psm_id, label, scan, + 21 features. Sorted by (modified_sequence, charge,
        /// scan_number) for stable diffing against Rust's output.
        /// </summary>
        private void WriteFeatureDump(
            string inputFile, string fileName,
            List<FdrEntry> scoredEntries)
        {
            string dumpPath = Path.Combine(
                Path.GetDirectoryName(inputFile) ?? ".",
                fileName + ".cs_features.tsv");

            var header = new[]
            {
                "SpecId", "Label", "ScanNr", "Charge",
                "fragment_coelution_sum", "fragment_coelution_max", "n_coeluting_fragments",
                "peak_apex", "peak_area", "peak_sharpness",
                "xcorr", "consecutive_ions", "explained_intensity",
                "mass_accuracy_deviation_mean", "abs_mass_accuracy_deviation_mean",
                "rt_deviation", "abs_rt_deviation",
                "ms1_precursor_coelution", "ms1_isotope_cosine",
                "median_polish_cosine", "median_polish_residual_ratio",
                "sg_weighted_xcorr", "sg_weighted_cosine",
                "median_polish_min_fragment_r2", "median_polish_residual_correlation",
                "Peptide"
            };

            var sorted = scoredEntries
                .Where(e => e.Features != null && e.Features.Length == NUM_PIN_FEATURES)
                .OrderBy(e => e.ModifiedSequence, StringComparer.Ordinal)
                .ThenBy(e => e.Charge)
                .ThenBy(e => e.ScanNumber)
                .ToList();

            using (var writer = new StreamWriter(dumpPath))
            {
                writer.WriteLine(string.Join("\t", header));
                foreach (var e in sorted)
                {
                    string psmId = string.Format("{0}_{1}_{2}_{3}",
                        fileName, e.ModifiedSequence, e.Charge, e.ScanNumber);
                    int label = e.IsDecoy ? -1 : 1;
                    var cols = new List<string>(26)
                    {
                        psmId,
                        label.ToString(),
                        e.ScanNumber.ToString(),
                        e.Charge.ToString()
                    };
                    for (int i = 0; i < NUM_PIN_FEATURES; i++)
                        cols.Add(e.Features[i].ToString("G17"));
                    cols.Add(e.ModifiedSequence ?? "");
                    writer.WriteLine(string.Join("\t", cols));
                }
            }

            LogInfo(string.Format("[COUNT] Wrote feature dump: {0} ({1} entries)",
                dumpPath, sorted.Count));
        }

        /// <summary>
        /// Load spectra from mzML file or spectra cache. When multiple
        /// files are processed in parallel, the mzML parse is gated so
        /// only one disk scan runs at a time (see s_mzmlReadGate).
        /// </summary>
        private void LoadSpectra(string inputFile, bool serializeMzmlRead,
            out List<Spectrum> ms2Spectra, out List<MS1Spectrum> ms1Spectra)
        {
            // Check for binary spectra cache
            string cachePath = inputFile + ".spectra.bin";
            if (File.Exists(cachePath))
            {
                LogInfo(string.Format("Loading spectra from cache: {0}", cachePath));
                try
                {
                    var cacheResult = SpectraCache.LoadSpectraCache(cachePath);
                    ms2Spectra = cacheResult.Ms2Spectra;
                    ms1Spectra = cacheResult.Ms1Spectra;
                    return;
                }
                catch (Exception ex)
                {
                    LogWarning(string.Format(
                        "Failed to load spectra cache: {0}. Falling back to mzML.", ex.Message));
                }
            }

            // Parse mzML directly, optionally serialized across files.
            LogInfo(string.Format("Parsing mzML: {0}", inputFile));
            MzmlResult mzmlResult;
            if (serializeMzmlRead)
                s_mzmlReadGate.Wait();
            try
            {
                mzmlResult = MzmlReader.LoadAllSpectra(inputFile);
            }
            finally
            {
                if (serializeMzmlRead)
                    s_mzmlReadGate.Release();
            }
            ms2Spectra = mzmlResult.Ms2Spectra;
            ms1Spectra = mzmlResult.Ms1Spectra;
            LogInfo(string.Format("Loaded {0} MS2 + {1} MS1 spectra",
                ms2Spectra.Count, ms1Spectra.Count));

            // Save to cache for next run
            try
            {
                SpectraCache.SaveSpectraCache(cachePath, ms2Spectra, ms1Spectra);
            }
            catch (Exception ex)
            {
                LogWarning(string.Format("Failed to save spectra cache: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Extract unique isolation windows from the first cycle of MS2 spectra.
        /// </summary>
        private List<IsolationWindow> ExtractIsolationWindows(List<Spectrum> spectra)
        {
            var windows = new List<IsolationWindow>();
            var seenCenters = new HashSet<int>();

            foreach (var spectrum in spectra)
            {
                int centerKey = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                if (seenCenters.Contains(centerKey))
                    break;
                seenCenters.Add(centerKey);
                windows.Add(spectrum.IsolationWindow);
            }

            // Sort by center m/z
            windows.Sort((a, b) => a.Center.CompareTo(b.Center));
            return windows;
        }

        private const double MIN_SNR_FOR_RT_CAL = 5.0;
        private const double MIN_COELUTION_CORR_SCORE = 0.5;
        private const int CAL_TOP_N_FRAGMENTS = 6;
        private const int MIN_COELUTION_SPECTRA = 3;
        private const double CAL_FDR_THRESHOLD = 0.01;
        // Hard floor for LOESS refit in the two-pass calibration refinement.
        // Matches Rust's ABSOLUTE_MIN_CALIBRATION_POINTS in pipeline.rs:652.
        private const int ABSOLUTE_MIN_CALIBRATION_POINTS = 50;

        /// <summary>
        /// Run RT calibration using calibration discovery scoring.
        /// Ports osprey/src/pipeline.rs run_calibration_discovery_windowed:
        ///   1. Sample target + paired decoy library entries (stratified by RT/m/z).
        ///   2. For each sample, extract fragment XICs, detect the best co-eluting
        ///      peak, then compute 4 features at the apex: mean pairwise correlation,
        ///      LibCosine, top-6 matched, and XCorr.
        ///   3. Train LDA with non-negative weights + 1% FDR target-decoy competition.
        ///   4. Apply S/N >= 5.0 quality filter on surviving targets.
        ///   5. Fit LOESS on the (libRt, measuredRt) pairs.
        /// </summary>
        private RTCalibration RunCalibration(
            List<LibraryEntry> library,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            out MzCalibrationResult ms1Calibration,
            out MzCalibrationResult ms2Calibration)
        {
            var config = context.Config;
            LogInfo("Running RT calibration...");

            // Calculate library and mzML RT ranges
            double libMinRt = double.MaxValue, libMaxRt = double.MinValue;
            double mzmlMinRt = double.MaxValue, mzmlMaxRt = double.MinValue;

            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    if (entry.RetentionTime < libMinRt)
                        libMinRt = entry.RetentionTime;
                    if (entry.RetentionTime > libMaxRt)
                        libMaxRt = entry.RetentionTime;
                }
            }

            foreach (var spectrum in spectra)
            {
                if (spectrum.RetentionTime < mzmlMinRt)
                    mzmlMinRt = spectrum.RetentionTime;
                if (spectrum.RetentionTime > mzmlMaxRt)
                    mzmlMaxRt = spectrum.RetentionTime;
            }

            double libRtRange = libMaxRt - libMinRt;
            double mzmlRtRange = mzmlMaxRt - mzmlMinRt;

            LogInfo(string.Format(
                "Library RT range: {0:F1}-{1:F1}, mzML RT range: {2:F1}-{3:F1} min",
                libMinRt, libMaxRt, mzmlMinRt, mzmlMaxRt));

            // Linear RT mapping when library and mzML scales differ significantly.
            bool rangesSimilar = libRtRange > 0 && mzmlRtRange > 0 &&
                Math.Max(libRtRange / mzmlRtRange, mzmlRtRange / libRtRange) < 2.0 &&
                Math.Abs(libMinRt - mzmlMinRt) < libRtRange * 0.5;

            double rtSlope = 1.0;
            double rtIntercept = 0.0;
            if (!rangesSimilar && libRtRange > 0)
            {
                rtSlope = mzmlRtRange / libRtRange;
                rtIntercept = mzmlMinRt - rtSlope * libMinRt;
                LogInfo(string.Format("RT mapping: slope={0:F4}, intercept={1:F4}",
                    rtSlope, rtIntercept));
            }

            double toleranceFraction = rangesSimilar ? 0.2 : 0.5;
            double initialTolerance = mzmlRtRange * toleranceFraction;

            LogInfo(string.Format("Initial RT tolerance: {0:F1} min", initialTolerance));

            // Sample library entries (paired target+decoy). Use seed 43 to match
            // Rust's sample_library_for_calibration(..., 42 + attempt=1) on the
            // first calibration attempt.
            var swSample = Stopwatch.StartNew();
            var sampledEntries = SampleLibraryForCalibration(
                library, config.RtCalibration.CalibrationSampleSize, 43UL);
            swSample.Stop();

            // Diagnostic: dump sorted sampled entry IDs + (modseq, charge) for
            // direct comparison with Rust. Abort after dump if CAL_SAMPLE_ONLY
            // env var is set (bisection mode - stop once we agree here).
            if (config.WritePin || OspreyDiagnostics.DumpCalSample)
            {
                OspreyDiagnostics.WriteCalSampleDump(context.FileName, sampledEntries);
                if (OspreyDiagnostics.CalSampleOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_SAMPLE_ONLY");
            }
            int nSampledTargets = 0;
            int nSampledDecoys = 0;
            foreach (var e in sampledEntries)
            {
                if (e.IsDecoy)
                    nSampledDecoys++;
                else nSampledTargets++;
            }
            LogInfo(string.Format(
                "[TIMING] Calibration sampling: {0:F2}s ({1} targets + {2} decoys)",
                swSample.Elapsed.TotalSeconds, nSampledTargets, nSampledDecoys));

            if (nSampledTargets == 0)
            {
                LogWarning("No target entries available for calibration sampling.");
                ms1Calibration = MzCalibrationResult.Uncalibrated();
                ms2Calibration = MzCalibrationResult.Uncalibrated();
                return null;
            }

            // Group spectra by isolation window center for O(1) window lookup per candidate.
            var spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in spectra)
            {
                int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                List<Spectrum> list;
                if (!spectraByWindowKey.TryGetValue(key, out list))
                {
                    list = new List<Spectrum>();
                    spectraByWindowKey[key] = list;
                }
                list.Add(spectrum);
            }
            // Sort each window's spectra by RT for deterministic XIC extraction.
            foreach (var list in spectraByWindowKey.Values)
                list.Sort((a, b) => a.RetentionTime.CompareTo(b.RetentionTime));

            // Pass 1: score all sampled entries with the linear pre-fit RT mapping
            // and the wide initial tolerance. Fits a LOESS RTCalibration from the
            // LDA + S/N surviving targets.
            var pass1 = RunCalibrationScoringPass(
                1,
                sampledEntries, spectraByWindowKey, ms1Spectra, context,
                rtSlope, rtIntercept, initialTolerance,
                null /* calibrationModel: pass 1 uses linear mapping */,
                config.RtCalibration.MinCalibrationPoints);

            if (pass1 == null)
            {
                LogWarning("Calibration pass 1 failed. Using fallback tolerance.");
                ms1Calibration = MzCalibrationResult.Uncalibrated();
                ms2Calibration = MzCalibrationResult.Uncalibrated();
                return null;
            }

            // === Iterative calibration refinement (2-pass) ===
            // Mirrors Rust pipeline.rs:714-839.
            // MAD * 1.4826 ~ SD for a normal distribution; 3* that covers ~99.7%.
            double madTolerance = pass1.Stats.MAD * 1.4826 * 3.0;
            double pass1Tolerance = Math.Max(
                config.RtCalibration.MinRtTolerance,
                Math.Min(config.RtCalibration.MaxRtTolerance, madTolerance));

            LogInfo(string.Format(
                "First-pass RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                pass1Tolerance,
                pass1.Stats.MAD,
                pass1.Stats.MAD * 1.4826,
                pass1.Stats.ResidualSD,
                pass1.Stats.NPoints,
                pass1.Stats.RSquared));

            // Only refine if the tolerance narrowed at least 2* tighter than the
            // initial wide window.
            if (pass1Tolerance < initialTolerance * 0.5)
            {
                LogInfo(string.Format(
                    "Calibration refinement: re-scoring with {0:F2} min tolerance (was {1:F1} min)",
                    pass1Tolerance, initialTolerance));

                var pass2 = RunCalibrationScoringPass(
                    2,
                    sampledEntries, spectraByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, pass1Tolerance,
                    pass1.Calibration /* pass 2 predicts RT via the LOESS fit */,
                    ABSOLUTE_MIN_CALIBRATION_POINTS);

                if (pass2 != null)
                {
                    double refinedMadTolerance = pass2.Stats.MAD * 1.4826 * 3.0;
                    double refinedTolerance = Math.Max(
                        config.RtCalibration.MinRtTolerance,
                        Math.Min(config.RtCalibration.MaxRtTolerance, refinedMadTolerance));

                    LogInfo(string.Format(
                        "Refined RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                        refinedTolerance,
                        pass2.Stats.MAD,
                        pass2.Stats.MAD * 1.4826,
                        pass2.Stats.ResidualSD,
                        pass2.Stats.NPoints,
                        pass2.Stats.RSquared));

                    // Accept the refined calibration only if R^2 didn't degrade
                    // by more than 1% (matches Rust pipeline.rs:811).
                    if (pass2.Stats.RSquared >= pass1.Stats.RSquared * 0.99)
                    {
                        ms1Calibration = pass2.Ms1Calibration;
                        ms2Calibration = pass2.Ms2Calibration;
                        return pass2.Calibration;
                    }
                    LogInfo(string.Format(
                        "Refined calibration not better (R^2 {0:F4} vs {1:F4}), keeping original",
                        pass2.Stats.RSquared, pass1.Stats.RSquared));
                }
                else
                {
                    LogInfo(string.Format(
                        "Refinement pass: insufficient points (need {0}), keeping original calibration",
                        ABSOLUTE_MIN_CALIBRATION_POINTS));
                }
            }

            ms1Calibration = pass1.Ms1Calibration;
            ms2Calibration = pass1.Ms2Calibration;
            return pass1.Calibration;
        }

        /// <summary>
        /// Result of one calibration scoring pass (scoring + LDA + S/N filter + LOESS fit).
        /// </summary>
        private class CalibrationPassResult
        {
            public RTCalibration Calibration;
            public RTCalibrationStats Stats;
            public MzCalibrationResult Ms1Calibration;
            public MzCalibrationResult Ms2Calibration;
        }

        /// <summary>
        /// Run one calibration scoring pass: score each sampled entry, train LDA,
        /// apply S/N filter, and fit LOESS on the surviving (libRt, measuredRt) pairs.
        /// Returns null if the pass has fewer than minLoessPoints survivors or the
        /// LOESS fit fails. This helper is called twice by RunCalibration to
        /// implement the two-pass refinement (pipeline.rs:714-839).
        /// </summary>
        private CalibrationPassResult RunCalibrationScoringPass(
            int passNumber,
            List<LibraryEntry> sampledEntries,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double tolerance,
            RTCalibration calibrationModel,
            int minLoessPoints)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            var fileName = context.FileName;
            // Activate per-entry window dump if requested. Cleared after the
            // matching loop completes (file written below).
            OspreyDiagnostics.StartCalWindowCollection();

            // Pre-preprocess all window spectra for XCorr using the calibration
            // unit-bin scorer (~2K bins per spectrum, ~16 KB per array).
            // Independent of resolution mode -- HRAM 100K-bin arrays would
            // consume ~160 GB of LOH for 204K Astral spectra, so we use the
            // small unit-bin form for calibration regardless. Main search
            // still uses the resolution-mode bins.
            // Calibration preprocess runs in pure f32 to match Rust upstream
            // maccoss/osprey's native f32 XCorr path (cross-impl parity at
            // F10 rounding noise, vs ~4e-6 drift under f64). f32 values are
            // widened to double[] here so the downstream XcorrFromPreprocessed
            // path is unchanged; the widening is lossless (f32 is a subset of
            // f64) and preserves the f32 bit pattern for the final sum.
            var preprocessedByWindowKey = new Dictionary<int, double[][]>();
            foreach (var kvp in spectraByWindowKey)
            {
                var pp = new double[kvp.Value.Count][];
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    float[] f32pp = s_calXcorrScorer.PreprocessSpectrumForXcorrF32(kvp.Value[i]);
                    var widened = new double[f32pp.Length];
                    for (int k = 0; k < f32pp.Length; k++)
                        widened[k] = f32pp[k];
                    pp[i] = widened;
                }
                preprocessedByWindowKey[kvp.Key] = pp;
            }

            // Parallel score each sampled entry.
            var swScoring = Stopwatch.StartNew();
            var matches = new ConcurrentBag<CalibrationMatch>();
            var snrByEntryId = new ConcurrentDictionary<uint, double>();
            var matchRts = new ConcurrentDictionary<uint, KeyValuePair<double, double>>();

            Parallel.ForEach(sampledEntries, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            () => resolution.CreateScorer(),
            (entry, loopState, localScorer) =>
            {
                double entrySnr;
                double entryLibRt;
                double entryMeasuredRt;
                var match = ScoreCalibrationEntry(
                    entry, spectraByWindowKey, preprocessedByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, tolerance,
                    calibrationModel,
                    localScorer,
                    out entrySnr, out entryLibRt, out entryMeasuredRt);
                if (match != null)
                {
                    matches.Add(match);
                    snrByEntryId[entry.Id] = entrySnr;
                    matchRts[entry.Id] = new KeyValuePair<double, double>(
                        entryLibRt, entryMeasuredRt);
                }
                return localScorer;
            },
            localScorer => { });
            swScoring.Stop();
            LogInfo(string.Format(
                "[TIMING] Calibration pass {0} scoring: {1:F2}s ({2} matches)",
                passNumber, swScoring.Elapsed.TotalSeconds, matches.Count));
            LogInfo(string.Format(
                "[COUNT] Calibration pass {0} matches scored [{1}]: {2}",
                passNumber, fileName, matches.Count));

            // Write per-entry window dump if requested. When two passes run, the
            // pass 2 dump overwrites pass 1 - same behaviour as Rust's
            // run_coelution_calibration_scoring dumping on every invocation.
            if (OspreyDiagnostics.CalWindowsCollecting)
            {
                OspreyDiagnostics.WriteCalWindowsDump(passNumber);
                if (OspreyDiagnostics.CalWindowsOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_WINDOWS_ONLY");
            }

            // Cross-implementation diagnostic: dump per-entry calibration match info
            // for direct diff with Rust. Writes a row for EVERY sampled entry
            // (matched or not), sorted by entry_id for stable diff.
            if (OspreyDiagnostics.DumpCalMatch)
            {
                OspreyDiagnostics.WriteCalMatchDump(passNumber, matches, sampledEntries, matchRts, snrByEntryId);
                if (OspreyDiagnostics.CalMatchOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_MATCH_ONLY");
            }

            if (matches.Count == 0)
            {
                LogWarning(string.Format(
                    "No calibration matches could be scored in pass {0}.", passNumber));
                return null;
            }

            // Train LDA + 1% FDR target-decoy competition.
            var swLda = Stopwatch.StartNew();
            var matchArray = matches.ToArray();
            // Sort deterministically by (base_id, entry_id) so LDA sees a stable order.
            Array.Sort(matchArray, (a, b) =>
            {
                uint baseA = a.EntryId & 0x7FFFFFFF;
                uint baseB = b.EntryId & 0x7FFFFFFF;
                int cmp = baseA.CompareTo(baseB);
                if (cmp != 0)
                    return cmp;
                return a.EntryId.CompareTo(b.EntryId);
            });
            int nPassing = CalibrationScorer.TrainAndScoreCalibration(matchArray, false);
            swLda.Stop();

            int nTargetWins = 0;
            int nDecoyWins = 0;
            foreach (var m in matchArray)
            {
                if (m.QValue <= CAL_FDR_THRESHOLD)
                {
                    if (m.IsDecoy)
                        nDecoyWins++;
                    else nTargetWins++;
                }
            }
            LogInfo(string.Format(
                "[TIMING] Calibration pass {0} LDA: {1:F2}s ({2} target wins, {3} decoy wins at 1% FDR)",
                passNumber, swLda.Elapsed.TotalSeconds, nTargetWins, nDecoyWins));
            LogInfo(string.Format(
                "[COUNT] Calibration pass {0} LDA winners [{1}]: {2} target wins, {3} decoy wins at 1% FDR",
                passNumber, fileName, nTargetWins, nDecoyWins));
            LogInfo(string.Format(
                "Calibration pass {0} LDA passing count: {1} (returned by TrainAndScoreCalibration)",
                passNumber, nPassing));

            // Cross-implementation diagnostic: dump per-entry LDA discriminant + q-value
            // sorted by entry_id for stable diff with rust_lda_scores.txt. Gated by
            // OSPREY_DUMP_LDA_SCORES; exits after write when OSPREY_LDA_SCORES_ONLY is set.
            if (OspreyDiagnostics.DumpLdaScores)
            {
                OspreyDiagnostics.WriteLdaScoresDump(passNumber, matchArray);
                if (OspreyDiagnostics.LdaScoresOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_LDA_SCORES_ONLY");
            }

            // Collect high-confidence target matches that also meet the S/N quality gate.
            var libRtsDetected = new List<double>();
            var measuredRtsDetected = new List<double>();
            int nSnrFiltered = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy)
                    continue;
                if (m.QValue > CAL_FDR_THRESHOLD)
                    continue;

                KeyValuePair<double, double> rtPair;
                if (!matchRts.TryGetValue(m.EntryId, out rtPair))
                    continue;

                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr))
                    snr = 0.0;

                if (snr < MIN_SNR_FOR_RT_CAL)
                {
                    nSnrFiltered++;
                    continue;
                }

                libRtsDetected.Add(rtPair.Key);
                measuredRtsDetected.Add(rtPair.Value);
            }

            if (nSnrFiltered > 0)
            {
                LogInfo(string.Format(
                    "  RT quality filter (pass {0}): {1} -> {2} peptides (removed {3} with S/N < {4:F1})",
                    passNumber, nTargetWins, libRtsDetected.Count, nSnrFiltered, MIN_SNR_FOR_RT_CAL));
            }

            LogInfo(string.Format(
                "[COUNT] Calibration pass {0} high-quality (S/N>=5) [{1}]: {2}",
                passNumber, fileName, libRtsDetected.Count));
            LogInfo(string.Format("Pass {0} found {1} calibration points",
                passNumber, libRtsDetected.Count));

            if (libRtsDetected.Count < minLoessPoints)
            {
                LogWarning(string.Format(
                    "Insufficient calibration points in pass {0} ({1} < {2}).",
                    passNumber, libRtsDetected.Count, minLoessPoints));
                return null;
            }

            // Aggregate MS1 + MS2 mass errors from passing targets only.
            // Rust pipeline.rs:610-619 collects both MS1 and MS2 errors from
            // passing_targets (those surviving LDA + competition + S/N >= 5.0
            // filter). This is the same set used for RT calibration points.
            var allMs1Errors = new List<double>();
            var allMs2Errors = new List<double>();
            foreach (var m in matchArray)
            {
                if (m.Ms2MassErrors == null || m.IsDecoy || m.QValue > CAL_FDR_THRESHOLD)
                    continue;
                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr) || snr < MIN_SNR_FOR_RT_CAL)
                    continue;
                if (m.Ms1Error.HasValue)
                    allMs1Errors.Add(m.Ms1Error.Value);
                allMs2Errors.AddRange(m.Ms2MassErrors);
            }
            string unitStr = config.FragmentTolerance.Unit == ToleranceUnit.Ppm ? "ppm" : "Th";
            var ms1Cal = MzCalibration.CalculateSingleLevel(allMs1Errors.ToArray(), unitStr);
            var ms2Cal = MzCalibration.CalculateSingleLevel(allMs2Errors.ToArray(), unitStr);
            LogInfo(string.Format(
                "MS1 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
                passNumber, ms1Cal.Mean, unitStr, ms1Cal.SD, 3.0 * ms1Cal.SD, allMs1Errors.Count));
            LogInfo(string.Format(
                "MS2 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
                passNumber, ms2Cal.Mean, unitStr, ms2Cal.SD, 3.0 * ms2Cal.SD, allMs2Errors.Count));

            // Fit LOESS calibration.
            var swLoess = Stopwatch.StartNew();
            try
            {
                double[] libRts = libRtsDetected.ToArray();
                double[] measuredRts = measuredRtsDetected.ToArray();

                var calibratorConfig = new RTCalibratorConfig
                {
                    Bandwidth = config.RtCalibration.LoessBandwidth,
                    Degree = 1,
                    MinPoints = Math.Min(20, libRts.Length),
                    RobustnessIterations = 2,
                    OutlierRetention = 1.0, // LDA + S/N already filtered
                    ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust
                };

                // Cross-implementation diagnostic: dump the (lib_rt, measured_rt) pairs
                // fed to LOESS. Used to verify Rust and C# see identical inputs
                // before LOESS fitting.
                if (OspreyDiagnostics.DumpLoessInput)
                {
                    OspreyDiagnostics.WriteLoessInputDump(passNumber, libRts, measuredRts);
                    if (OspreyDiagnostics.LoessInputOnly)
                        OspreyDiagnostics.ExitAfterDump("OSPREY_LOESS_INPUT_ONLY");
                }

                var calibrator = new RTCalibrator(calibratorConfig);
                var rtCal = calibrator.Fit(libRts, measuredRts);
                swLoess.Stop();

                var stats = rtCal.Stats();
                LogInfo(string.Format("[TIMING] Calibration pass {0} LOESS fit: {1:F2}s",
                    passNumber, swLoess.Elapsed.TotalSeconds));
                LogInfo(string.Format(
                    "RT calibration pass {0}: {1} points, R2={2:F4}, residual SD={3:F3} min, MAD={4:F3}",
                    passNumber, stats.NPoints, stats.RSquared, stats.ResidualSD, stats.MAD));

                return new CalibrationPassResult
                {
                    Calibration = rtCal,
                    Stats = stats,
                    Ms1Calibration = ms1Cal,
                    Ms2Calibration = ms2Cal,
                };
            }
            catch (Exception ex)
            {
                swLoess.Stop();
                LogWarning(string.Format("RT calibration pass {0} failed: {1}",
                    passNumber, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Sample library entries for calibration discovery, keeping paired target-decoy
        /// pairs together (matched by base_id = entry_id &amp; 0x7FFFFFFF).
        /// Direct port of Rust sample_library_for_calibration (osprey-scoring/src/batch.rs:1450).
        /// Uses a 2D (RT x m/z) stratified grid with deterministic stride sampling.
        /// This is the first randomized/selected step in the pipeline, so it must match
        /// Rust exactly for the two tools to process the same calibration peptides.
        /// </summary>
        private static List<LibraryEntry> SampleLibraryForCalibration(
            List<LibraryEntry> library, int sampleSize, ulong seed)
        {
            if (sampleSize == 0)
                return new List<LibraryEntry>(library);

            var targets = new List<LibraryEntry>();
            var decoys = new List<LibraryEntry>();
            foreach (var entry in library)
            {
                if (entry.IsDecoy)
                    decoys.Add(entry);
                else targets.Add(entry);
            }

            if (targets.Count <= sampleSize)
                return new List<LibraryEntry>(library);

            // Build target_id -> decoy map (decoy_id = target_id | 0x80000000)
            var decoyMap = new Dictionary<uint, LibraryEntry>(decoys.Count);
            foreach (var d in decoys)
                decoyMap[d.Id & 0x7FFFFFFF] = d;

            // 2D stratified sampling: divide RT x m/z space into a grid.
            // ~sqrt(sample_size)/2 bins per axis for good 2D coverage.
            int binsPerAxis = (int)Math.Max(5, Math.Ceiling(Math.Sqrt(sampleSize) / 2.0));

            double rtMin = double.MaxValue, rtMax = double.MinValue;
            double mzMin = double.MaxValue, mzMax = double.MinValue;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.RetentionTime < rtMin)
                    rtMin = t.RetentionTime;
                if (t.RetentionTime > rtMax)
                    rtMax = t.RetentionTime;
                if (t.PrecursorMz < mzMin)
                    mzMin = t.PrecursorMz;
                if (t.PrecursorMz > mzMax)
                    mzMax = t.PrecursorMz;
            }
            double rtRange = Math.Max(1e-6, rtMax - rtMin);
            double mzRange = Math.Max(1e-6, mzMax - mzMin);
            double rtBinWidth = rtRange / binsPerAxis;
            double mzBinWidth = mzRange / binsPerAxis;

            // Assign each target to a 2D grid cell
            var grid = new List<int>[binsPerAxis, binsPerAxis];
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    grid[i, j] = new List<int>();

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                int rtBin = (int)Math.Floor((t.RetentionTime - rtMin) / rtBinWidth);
                int mzBin = (int)Math.Floor((t.PrecursorMz - mzMin) / mzBinWidth);
                if (rtBin >= binsPerAxis)
                    rtBin = binsPerAxis - 1;
                if (mzBin >= binsPerAxis)
                    mzBin = binsPerAxis - 1;
                grid[rtBin, mzBin].Add(i);
            }

            // Count non-empty cells and compute per-cell quota
            int nOccupied = 0;
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    if (grid[i, j].Count > 0)
                        nOccupied++;

            int perCell = nOccupied > 0 ? sampleSize / nOccupied : 1;
            if (perCell < 1)
                perCell = 1;

            // Diagnostic dump: scalar parameters + full grid contents,
            // matching Rust's dump format for direct diff.
            if (OspreyDiagnostics.DumpCalSample)
            {
                OspreyDiagnostics.WriteCalScalarsAndGridDump(
                    targets, decoys, binsPerAxis,
                    rtMin, rtMax, mzMin, mzMax,
                    rtRange, mzRange, rtBinWidth, mzBinWidth,
                    nOccupied, perCell, seed, grid);
            }

            // Deterministic stride sampling from each cell.
            int offset = (int)(seed & 0x7FFFFFFF);
            var sampledIds = new HashSet<uint>();
            var sampled = new List<LibraryEntry>(sampleSize * 2);

            for (int ri = 0; ri < binsPerAxis; ri++)
            {
                for (int ci = 0; ci < binsPerAxis; ci++)
                {
                    var cell = grid[ri, ci];
                    if (cell.Count == 0)
                        continue;

                    int nTake = Math.Min(cell.Count, perCell);
                    int stride = Math.Max(1, cell.Count / nTake);
                    int cellOffset = offset % Math.Max(1, cell.Count);

                    for (int j = 0; j < nTake; j++)
                    {
                        int idx = (cellOffset + j * stride) % cell.Count;
                        var target = targets[cell[idx]];

                        if (sampledIds.Contains(target.Id))
                            continue;
                        sampledIds.Add(target.Id);
                        sampled.Add(target);

                        LibraryEntry decoy;
                        if (decoyMap.TryGetValue(target.Id, out decoy))
                            sampled.Add(decoy);
                    }
                }
            }

            // Second pass: if under-sampled, add more from occupied cells.
            if (sampledIds.Count < sampleSize)
            {
                int remaining = sampleSize - sampledIds.Count;
                int extraPerCell = Math.Max(1, remaining / Math.Max(1, nOccupied));

                bool done = false;
                for (int ri = 0; ri < binsPerAxis && !done; ri++)
                {
                    for (int ci = 0; ci < binsPerAxis; ci++)
                    {
                        var cell = grid[ri, ci];
                        if (cell.Count == 0)
                            continue;

                        int added = 0;
                        foreach (int targetIdx in cell)
                        {
                            if (added >= extraPerCell)
                                break;
                            var target = targets[targetIdx];
                            if (sampledIds.Contains(target.Id))
                                continue;
                            sampledIds.Add(target.Id);
                            sampled.Add(target);
                            LibraryEntry decoy;
                            if (decoyMap.TryGetValue(target.Id, out decoy))
                                sampled.Add(decoy);
                            added++;
                        }

                        if (sampledIds.Count >= sampleSize) { done = true; break; }
                    }
                }
            }

            return sampled;
        }

        /// <summary>
        /// Score a single library entry for calibration: extract fragment XICs across
        /// spectra in the entry's isolation window that fall within the initial RT
        /// tolerance, detect the best co-eluting peak, and compute the four LDA
        /// features at the apex (correlation, LibCosine, top-6 matched, XCorr).
        /// Returns null if the entry has no viable peak.
        ///
        /// On pass 1 (calibrationModel == null), expectedRt is computed from the
        /// linear (rtSlope * library_rt + rtIntercept) mapping. On pass 2, the
        /// LOESS-fitted RTCalibration is used to predict expected_rt and the
        /// (refined) tolerance is much tighter.
        /// </summary>
        private CalibrationMatch ScoreCalibrationEntry(
            LibraryEntry entry,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            Dictionary<int, double[][]> preprocessedByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double initialTolerance,
            RTCalibration calibrationModel,
            SpectralScorer scorer,
            out double signalToNoise,
            out double libraryRt,
            out double measuredRt)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            signalToNoise = 0.0;
            libraryRt = entry.RetentionTime;
            measuredRt = 0.0;

            if (entry.Fragments == null || entry.Fragments.Count < 2)
                return null;

            // Find spectra in the entry's isolation window.
            int windowKey = (int)Math.Round(entry.PrecursorMz * 10.0);
            List<Spectrum> windowSpectra;
            if (!spectraByWindowKey.TryGetValue(windowKey, out windowSpectra))
            {
                // Try neighbouring window keys (handles off-by-one due to rounding).
                if (!spectraByWindowKey.TryGetValue(windowKey - 1, out windowSpectra) &&
                    !spectraByWindowKey.TryGetValue(windowKey + 1, out windowSpectra))
                {
                    // Fall back to linear scan across windows that contain this precursor.
                    windowSpectra = null;
                    foreach (var kvp in spectraByWindowKey)
                    {
                        var first = kvp.Value[0];
                        if (first.IsolationWindow.Contains(entry.PrecursorMz))
                        {
                            windowSpectra = kvp.Value;
                            break;
                        }
                    }
                    if (windowSpectra == null)
                        return null;
                }
            }
            else if (!windowSpectra[0].IsolationWindow.Contains(entry.PrecursorMz))
            {
                // Key collision where the actual isolation window doesn't contain this precursor.
                return null;
            }

            // Compute expected RT for this library entry.
            // Pass 1 (calibrationModel == null): use the linear pre-fit mapping
            //     rtSlope * library_rt + rtIntercept
            // Pass 2 (calibrationModel != null): use the LOESS-fitted prediction
            //     rtCalibration.Predict(library_rt)
            // Matches Rust's predict_fn pattern in pipeline.rs:740.
            double expectedRt = calibrationModel != null
                ? calibrationModel.Predict(entry.RetentionTime)
                : entry.RetentionTime * rtSlope + rtIntercept;

            // Diagnostic: record per-entry m/z + RT window selection.
            // C# selects ONE window per entry (the first match in dictionary order),
            // unlike Rust which scores in ALL matching windows. Capturing this here
            // before the RT/2-of-6 filter so it matches Rust's pre-filter dump.
            if (OspreyDiagnostics.CalWindowsCollecting)
            {
                OspreyDiagnostics.AddCalWindowRow(
                    entry, windowSpectra[0].IsolationWindow,
                    expectedRt,
                    expectedRt - initialTolerance,
                    expectedRt + initialTolerance);
            }

            // Resolve the actual window key that was used (may differ from primary
            // due to neighbour-key or linear-scan fallback).
            int resolvedWindowKey = windowKey;
            if (!spectraByWindowKey.ContainsKey(windowKey))
            {
                if (spectraByWindowKey.ContainsKey(windowKey - 1))
                    resolvedWindowKey = windowKey - 1;
                else if (spectraByWindowKey.ContainsKey(windowKey + 1))
                    resolvedWindowKey = windowKey + 1;
                else
                {
                    // Linear scan fallback - find the key that matched
                    foreach (var kvp in spectraByWindowKey)
                    {
                        if (kvp.Value == windowSpectra)
                        {
                            resolvedWindowKey = kvp.Key;
                            break;
                        }
                    }
                }
            }

            // Filter by RT tolerance and top-6 fragment prefilter.
            // Track window indices for preprocessed XCorr lookup.
            var candidateSpectra = new List<Spectrum>();
            var candidateWindowIndices = new List<int>();
            double[][] windowPreprocessed = null;
            preprocessedByWindowKey.TryGetValue(resolvedWindowKey, out windowPreprocessed);
            for (int si = 0; si < windowSpectra.Count; si++)
            {
                var spec = windowSpectra[si];
                if (Math.Abs(spec.RetentionTime - expectedRt) > initialTolerance)
                    continue;
                if (!HasTopNFragmentMatch(entry, spec.Mzs, config.FragmentTolerance))
                    continue;
                candidateSpectra.Add(spec);
                candidateWindowIndices.Add(si);
            }

            if (candidateSpectra.Count < MIN_COELUTION_SPECTRA)
                return null;

            // Build shared RT axis for XIC extraction.
            int nScans = candidateSpectra.Count;
            double[] rts = new double[nScans];
            for (int i = 0; i < nScans; i++)
                rts[i] = candidateSpectra[i].RetentionTime;

            // Per-entry chromatogram diagnostic. Dump candidates + extracted XICs.
            // We default to pass 1 in OspreyDiagnostics because the cross-tool
            // bisection walks downstream: until pass 1 chromatograms match,
            // there's no point comparing pass 2 (which depends on pass 1's
            // LOESS fit).
            int currentPass = calibrationModel != null ? 2 : 1;

            // Extract XICs for the top-N most intense library fragments.
            var xics = ExtractTopNFragmentXics(
                entry, candidateSpectra, rts, CAL_TOP_N_FRAGMENTS, config);

            if (OspreyDiagnostics.ShouldDumpCalXicFor(entry.Id, currentPass))
            {
                OspreyDiagnostics.WriteCalXicEntryDumpAndExit(
                    entry, currentPass, calibrationModel,
                    expectedRt, initialTolerance, rtSlope, rtIntercept,
                    candidateSpectra, xics);
            }

            if (xics.Count < 2)
                return null;

            // Detect consensus CWT peaks and score by pairwise correlation sum.
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            // Fallback: when CWT returns no consensus peaks, run DetectAllXicPeaks
            // on the reference fragment XIC alone. This rescues entries where
            // cross-fragment consensus is weak (one dominant fragment, noisy
            // others) but the reference has a clean peak shape. Matches Rust
            // batch.rs:2744-2751: the `cwt_candidates.is_empty()` branch.
            if (peaks.Count == 0)
            {
                // Pick the reference fragment (highest total intensity). Uses `>=`
                // so ties resolve to the LAST fragment, matching Rust's max_by.
                int refFallbackIdx = 0;
                double refTotal = -1.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double total = 0.0;
                    double[] inten = xics[i].Intensities;
                    for (int k = 0; k < inten.Length; k++) total += inten[k];
                    if (total >= refTotal)
                    {
                        refTotal = total;
                        refFallbackIdx = i;
                    }
                }
                var fallbackXic = xics[refFallbackIdx];
                peaks = PeakDetector.DetectAllXicPeaks(
                    fallbackXic.RetentionTimes,
                    fallbackXic.Intensities,
                    0.01, // min_height
                    5.0); // peak_boundary
            }

            if (peaks.Count == 0)
                return null;

            XICPeakBounds bestPeak = null;
            double bestCorrSum = double.NegativeInfinity;

            foreach (var peak in peaks)
            {
                int si = peak.StartIndex;
                int ei = peak.EndIndex;
                if (ei - si + 1 < 3)
                    continue;

                double corrSum = 0.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double[] inti = xics[i].Intensities;
                    for (int j = i + 1; j < xics.Count; j++)
                    {
                        double[] intj = xics[j].Intensities;
                        double corr = PearsonOverRange(inti, intj, si, ei);
                        if (!double.IsNaN(corr))
                            corrSum += corr;
                    }
                }

                // Use >= to match Rust's max_by tie-break (last wins on ties).
                if (corrSum >= bestCorrSum)
                {
                    bestCorrSum = corrSum;
                    bestPeak = peak;
                }
            }

            if (bestPeak == null || bestCorrSum < MIN_COELUTION_CORR_SCORE)
                return null;

            // Identify the reference XIC - the single fragment with the highest
            // total intensity across the extracted XICs. This is the signal that
            // feeds SNR computation and the apex selection. Direct port of
            // Rust's `ref_idx = xics.max_by(total intensity)` in batch.rs:~2718.
            //
            // Note: Rust's `Iterator::max_by` returns the LAST element on ties,
            // so we use `>=` (not `>`) here to match. Without this, fragments
            // with identical total intensities would select different reference
            // XICs between the two tools, causing downstream apex divergence.
            int refIdx = 0;
            double bestTotalIntensity = -1.0;
            for (int i = 0; i < xics.Count; i++)
            {
                double total = 0.0;
                double[] inten = xics[i].Intensities;
                for (int k = 0; k < inten.Length; k++)
                    total += inten[k];
                if (total >= bestTotalIntensity)
                {
                    bestTotalIntensity = total;
                    refIdx = i;
                }
            }
            var refXic = xics[refIdx];
            double[] refIntensities = refXic.Intensities;

            // Apex is the highest-intensity point within the peak boundaries of
            // the reference XIC. No top-6 constraint: Rust's batch.rs:2797-2802
            // is a straight argmax over `ref_xic[ref_start..=ref_end]`. Uses `>=`
            // so ties resolve to the LAST index, matching Rust `max_by`.
            int apexLocalIdx = bestPeak.StartIndex;
            double apexVal = refIntensities[Math.Min(apexLocalIdx, refIntensities.Length - 1)];
            for (int scan = bestPeak.StartIndex; scan <= bestPeak.EndIndex; scan++)
            {
                if (scan >= refIntensities.Length)
                    break;
                if (refIntensities[scan] >= apexVal)
                {
                    apexVal = refIntensities[scan];
                    apexLocalIdx = scan;
                }
            }

            // Apex RT from the reference XIC (shared time axis across fragments).
            double apexRt = refXic.RetentionTimes[apexLocalIdx];
            measuredRt = apexRt;

            // SNR is computed on the reference fragment's raw intensities
            // (NOT the composite sum). Direct port of Rust batch.rs:2803-2806.
            signalToNoise = PeakDetector.ComputeSnr(
                refIntensities, apexLocalIdx, bestPeak.StartIndex, bestPeak.EndIndex);

            // Map the apex RT to the candidate spectrum with the closest RT
            // for feature computation. In practice this returns apexLocalIdx
            // since the XIC time axis is built directly from candidate spectrum
            // RTs, but we port Rust's lookup verbatim for parity.
            int apexSpecLocalIdx = 0;
            double bestDt = Math.Abs(candidateSpectra[0].RetentionTime - apexRt);
            for (int i = 1; i < candidateSpectra.Count; i++)
            {
                double dt = Math.Abs(candidateSpectra[i].RetentionTime - apexRt);
                if (dt < bestDt)
                {
                    bestDt = dt;
                    apexSpecLocalIdx = i;
                }
            }
            var apexSpectrum = candidateSpectra[apexSpecLocalIdx];

            // Compute the four LDA features at the apex.
            double libCosineApex = scorer.LibCosine(
                apexSpectrum, entry, config.FragmentTolerance);
            // XCorr at apex always uses the calibration unit-bin scorer
            // (matches the pre-preprocessed arrays built with s_calXcorrScorer).
            int apexWindowIdx = candidateWindowIndices[apexSpecLocalIdx];
            double xcorrApex = (windowPreprocessed != null && apexWindowIdx < windowPreprocessed.Length)
                ? s_calXcorrScorer.XcorrFromPreprocessed(windowPreprocessed[apexWindowIdx], entry)
                : s_calXcorrScorer.XcorrAtScan(apexSpectrum, entry);
            byte top6Matched = CountTop6Matches(entry, apexSpectrum, config);

            // Collect MS2 fragment mass errors at apex for m/z calibration.
            // Matches Rust topn_fragment_match_with_errors: uses TOP-6 fragments
            // by intensity, not all fragments.
            var ms2Errors = new List<double>();
            {
                // Select top-6 fragment indices by descending intensity
                int nFragsForErrors = entry.Fragments.Count;
                int nTopForErrors = Math.Min(nFragsForErrors, CAL_TOP_N_FRAGMENTS);
                int[] topErrorIndices;
                if (nFragsForErrors <= CAL_TOP_N_FRAGMENTS)
                {
                    topErrorIndices = new int[nFragsForErrors];
                    for (int ti = 0; ti < nFragsForErrors; ti++)
                        topErrorIndices[ti] = ti;
                }
                else
                {
                    var indexed = new List<KeyValuePair<int, float>>(nFragsForErrors);
                    for (int ti = 0; ti < nFragsForErrors; ti++)
                        indexed.Add(new KeyValuePair<int, float>(ti, entry.Fragments[ti].RelativeIntensity));
                    indexed.Sort((a, b) => b.Value.CompareTo(a.Value));
                    topErrorIndices = new int[nTopForErrors];
                    for (int ti = 0; ti < nTopForErrors; ti++)
                        topErrorIndices[ti] = indexed[ti].Key;
                }

                foreach (int fragIdx in topErrorIndices)
                {
                    var frag = entry.Fragments[fragIdx];
                    double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                    double lower = frag.Mz - tolDa;
                    double upper = frag.Mz + tolDa;

                    int lo = BinarySearchLowerBound(apexSpectrum.Mzs, lower);
                    if (lo < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[lo] <= upper)
                    {
                        double bestMz = apexSpectrum.Mzs[lo];
                        double bestDiff = Math.Abs(bestMz - frag.Mz);
                        for (int k = lo + 1; k < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[k] <= upper; k++)
                        {
                            double diff = Math.Abs(apexSpectrum.Mzs[k] - frag.Mz);
                            if (diff < bestDiff)
                            {
                                bestDiff = diff;
                                bestMz = apexSpectrum.Mzs[k];
                            }
                        }
                        ms2Errors.Add(config.FragmentTolerance.MassError(frag.Mz, bestMz));
                    }
                }
            }

            // MS1 precursor mass error at apex for MS1 mass calibration.
            // Port of osprey-scoring/src/batch.rs:2912-2940 -- extract M+0 from the
            // MS1 scan closest to the apex RT using the config precursor tolerance;
            // report the error in fragment tolerance units so MS1 + MS2 errors
            // share the same unit for the MzQCData aggregator.
            double? ms1Error = null;
            if (ms1Spectra != null && ms1Spectra.Count > 0)
            {
                int charge = entry.Charge > 0 ? entry.Charge : 1;
                double precursorTolPpm = config.PrecursorTolerance != null
                    && config.PrecursorTolerance.Unit == ToleranceUnit.Ppm
                    ? config.PrecursorTolerance.Tolerance
                    : 10.0;
                var apexMs1 = FindNearestMs1(ms1Spectra, apexRt);
                if (apexMs1 != null)
                {
                    var envelope = IsotopeEnvelope.Extract(
                        apexMs1, entry.PrecursorMz, charge, precursorTolPpm);
                    if (envelope.HasM0 && envelope.M0ObservedMz.HasValue)
                    {
                        ms1Error = config.FragmentTolerance.MassError(
                            entry.PrecursorMz, envelope.M0ObservedMz.Value);
                    }
                }
            }

            return new CalibrationMatch
            {
                EntryId = entry.Id,
                IsDecoy = entry.IsDecoy,
                Sequence = entry.Sequence,
                ScanNumber = apexSpectrum.ScanNumber,
                CorrelationScore = bestCorrSum,
                LibcosineApex = libCosineApex,
                Top6MatchedApex = top6Matched,
                XcorrScore = xcorrApex,
                IsotopeCosine = 0.0,
                DiscriminantScore = bestCorrSum,
                QValue = 1.0,
                Ms2MassErrors = ms2Errors.ToArray(),
                Ms1Error = ms1Error
            };
        }

        /// <summary>
        /// Extract XICs for the top N most intense library fragments across the
        /// supplied (pre-filtered) spectra list. Returns only fragments that have
        /// at least one non-zero intensity point.
        /// </summary>
        private List<XicData> ExtractTopNFragmentXics(
            LibraryEntry entry,
            List<Spectrum> candidateSpectra,
            double[] rts,
            int maxFragments,
            OspreyConfig config)
        {
            var xics = new List<XicData>();
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return xics;

            // Select top N fragment indices by descending relative intensity.
            int nFrags = entry.Fragments.Count;
            int nTop = Math.Min(nFrags, maxFragments);
            int[] topIndices;
            if (nFrags <= maxFragments)
            {
                topIndices = new int[nFrags];
                for (int i = 0; i < nFrags; i++)
                    topIndices[i] = i;
            }
            else
            {
                var indexed = new List<KeyValuePair<int, float>>(nFrags);
                for (int i = 0; i < nFrags; i++)
                    indexed.Add(new KeyValuePair<int, float>(i, entry.Fragments[i].RelativeIntensity));
                indexed.Sort((a, b) => b.Value.CompareTo(a.Value));
                topIndices = new int[nTop];
                for (int i = 0; i < nTop; i++)
                    topIndices[i] = indexed[i].Key;
            }

            int nScans = candidateSpectra.Count;
            foreach (int fragIdx in topIndices)
            {
                var fragment = entry.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(fragment.Mz);
                double lower = fragment.Mz - tolDa;
                double upper = fragment.Mz + tolDa;

                double[] intensities = new double[nScans];

                for (int scanIdx = 0; scanIdx < nScans; scanIdx++)
                {
                    var spectrum = candidateSpectra[scanIdx];
                    if (spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                        continue;

                    int lo = BinarySearchLowerBound(spectrum.Mzs, lower);
                    if (lo >= spectrum.Mzs.Length || spectrum.Mzs[lo] > upper)
                        continue;

                    // Pick CLOSEST peak by m/z (not most intense). Matches
                    // Rust extract_fragment_xics in osprey-scoring/src/batch.rs.
                    double bestDiff = Math.Abs(spectrum.Mzs[lo] - fragment.Mz);
                    double bestIntensity = spectrum.Intensities[lo];
                    for (int k = lo + 1; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                    {
                        double diff = Math.Abs(spectrum.Mzs[k] - fragment.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIntensity = spectrum.Intensities[k];
                        }
                    }
                    intensities[scanIdx] = bestIntensity;
                }

                // Always include the fragment XIC, even all-zero. Rust:
                // "Dropping all-zero fragments biases decoys to higher R^2".
                xics.Add(new XicData(fragIdx, rts, intensities));
            }

            return xics;
        }

        /// <summary>
        /// Pearson correlation over an inclusive index range. Returns NaN if either
        /// subrange has no variance or the range is too short.
        /// </summary>
        private static double PearsonOverRange(double[] x, double[] y, int start, int end)
        {
            int n = end - start + 1;
            if (n < 3)
                return double.NaN;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = start; i <= end; i++)
            {
                double xi = x[i];
                double yi = y[i];
                sumX += xi;
                sumY += yi;
                sumXY += xi * yi;
                sumX2 += xi * xi;
                sumY2 += yi * yi;
            }

            double dn = n;
            double denom = (dn * sumX2 - sumX * sumX) * (dn * sumY2 - sumY * sumY);
            if (denom < 1e-30)
                return 0.0;

            return (dn * sumXY - sumX * sumY) / Math.Sqrt(denom);
        }

        /// <summary>
        /// Run coelution scoring for all library entries across all isolation windows.
        /// For each window, finds candidate entries whose precursor falls in the window,
        /// extracts fragment XICs, detects CWT peaks, and scores at each peak.
        /// </summary>
        private List<FdrEntry> RunCoelutionScoring(
            List<LibraryEntry> fullLibrary,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCalibration,
            MzCalibrationResult ms2Calibration,
            MzCalibrationResult ms1Calibration,
            ScoringContext context)
        {
            var config = context.Config;
            var allEntries = new List<FdrEntry>();
            var scorer = context.Resolution.CreateScorer();
            int windowsProcessed = 0;

            // Pre-size the pool once per scoring run. Matters most for HRAM
            // where each scratch set carries four 100K-bin LOH arrays; Unit
            // resolution scratch is only ~16 KB per set so the pool overhead
            // is negligible either way. The pool grows organically to its
            // natural high-water mark (approximately NThreads sets) and
            // never shrinks, so gen-2 keeps the arrays for the full run.
            context.EnsureXcorrScratchPool(scorer.BinConfig.NBins);

            // Group spectra by isolation window center (rounded key) for efficient lookup
            var spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in spectra)
            {
                int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                List<Spectrum> list;
                if (!spectraByWindowKey.TryGetValue(key, out list))
                {
                    list = new List<Spectrum>();
                    spectraByWindowKey[key] = list;
                }
                list.Add(spectrum);
            }

            // Determine RT tolerance - global, matching Rust's run_search.
            // Rust computes one tolerance for ALL entries: 3 * MAD * 1.4826,
            // clamped to [min, max]. C# was using per-entry LocalTolerance
            // (interpolated residuals), which produces different scan ranges.
            double rtToleranceGlobal;
            // Sigma for the Gaussian RT penalty applied during CWT peak
            // ranking inside ScoreCandidate. Rust pipeline.rs:6690 uses
            // UNCLAMPED 5*MAD*1.4826 with a 0.1 min floor (widened from 3x
            // in v26.3.1 so peaks with small RT deviations from the LOESS
            // prediction aren't over-penalized; see maccoss/osprey commit
            // 2db5f1c). The scan-window tolerance above remains at 3x and
            // is clamped to [MinRt, MaxRt]. Two separate values keep peak
            // ranking bit-identical to Rust regardless of config clamping.
            double rtSigmaGlobal;
            if (rtCalibration != null)
            {
                var stats = rtCalibration.Stats();
                double robustSd = stats.MAD * 1.4826;
                double rtToleranceMad = robustSd * 3.0;
                rtToleranceGlobal = Math.Max(
                    config.RtCalibration.MinRtTolerance,
                    Math.Min(config.RtCalibration.MaxRtTolerance, rtToleranceMad));
                rtSigmaGlobal = Math.Max(robustSd * 5.0, 0.1);
                LogInfo(string.Format(
                    "Coelution search RT tolerance: {0:F2} min (3*MAD*1.4826, MAD={1:F3})",
                    rtToleranceGlobal, stats.MAD));
            }
            else
            {
                rtToleranceGlobal = config.RtCalibration.FallbackRtTolerance;
                rtSigmaGlobal = rtToleranceGlobal;
            }

            // Apply MS2 calibration: calibrated fragment tolerance + m/z offset.
            // Matches Rust run_search which applies calibrated_tolerance() and
            // apply_spectrum_calibration() before scoring.
            FragmentToleranceConfig searchFragTol = config.FragmentTolerance;
            if (ms2Calibration.Calibrated)
            {
                double calTol;
                ToleranceUnit calUnit;
                MzCalibration.CalibratedTolerance(ms2Calibration,
                    config.FragmentTolerance.Tolerance, config.FragmentTolerance.Unit,
                    out calTol, out calUnit);
                searchFragTol = new FragmentToleranceConfig
                {
                    Tolerance = calTol,
                    Unit = calUnit
                };
                string unitStr = calUnit == ToleranceUnit.Ppm ? "ppm" : "Th";
                LogInfo(string.Format(
                    "Coelution search using calibrated fragment tolerance: {0:F4} {1}",
                    calTol, unitStr));

                // Use calibrated tolerance for all downstream scoring
                config.FragmentTolerance = searchFragTol;

                // Apply m/z offset correction to all spectra
                LogInfo(string.Format(
                    "Applying MS2 calibration: mean error = {0:F4} {1} -> correcting by {2:+F4;-F4;0} {1}",
                    ms2Calibration.Mean, ms2Calibration.Unit, -ms2Calibration.Mean));
                for (int si = 0; si < spectra.Count; si++)
                {
                    var s = spectra[si];
                    double[] correctedMzs = new double[s.Mzs.Length];
                    for (int mi = 0; mi < s.Mzs.Length; mi++)
                        correctedMzs[mi] = MzCalibration.ApplyCalibration(s.Mzs[mi], ms2Calibration);
                    spectra[si] = new Spectrum
                    {
                        ScanNumber = s.ScanNumber,
                        RetentionTime = s.RetentionTime,
                        PrecursorMz = s.PrecursorMz,
                        IsolationWindow = s.IsolationWindow,
                        Mzs = correctedMzs,
                        Intensities = s.Intensities
                    };
                }

                // Rebuild the window lookup after m/z correction
                spectraByWindowKey.Clear();
                foreach (var spectrum in spectra)
                {
                    int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                    List<Spectrum> list;
                    if (!spectraByWindowKey.TryGetValue(key, out list))
                    {
                        list = new List<Spectrum>();
                        spectraByWindowKey[key] = list;
                    }
                    list.Add(spectrum);
                }
            }

            // Per-entry search XIC diagnostic: log the intent once at start.
            // The per-entry dump check happens inline in ScoreCandidate via
            // OspreyDiagnostics.ShouldDumpSearchXicFor(entry.Id).
            if (OspreyDiagnostics.DiagSearchEntryIds != null)
            {
                LogInfo(string.Format(
                    "[BISECT] OSPREY_DIAG_SEARCH_ENTRY_IDS: will dump {0} entries",
                    OspreyDiagnostics.DiagSearchEntryIds.Count));
            }

            // Per-window timings collected thread-safely for post-summary.
            var windowTimings = new ConcurrentBag<WindowTiming>();

            // Short-circuit gate for profiling / fast iteration. Caps the
            // number of windows actually scored. Set OSPREY_MAX_SCORING_
            // WINDOWS=2 to capture a few representative windows under
            // dotTrace without paying the full ~15 min Astral wall-clock.
            var windowsToScore = isolationWindows;
            int maxWindows = OspreyEnvironment.MaxScoringWindows;
            if (maxWindows > 0 && maxWindows < isolationWindows.Count)
            {
                windowsToScore = isolationWindows.Take(maxWindows).ToList();
                LogInfo(string.Format(
                    "[BENCH] OSPREY_MAX_SCORING_WINDOWS={0} - capping {1} windows to first {0}",
                    maxWindows, isolationWindows.Count));
            }

            // Bracket the main-search parallel loop with the dotTrace Measure
            // API and peak-memory logging. When no profiler is attached the
            // ProfilerHooks calls are inexpensive no-ops.
            ProfilerHooks.LogMemoryStats(LogInfo, "pre-main-search");
            ProfilerHooks.StartMeasure();

            // Process each isolation window (parallelizable)
            object lockObj = new object();

            Parallel.ForEach(windowsToScore, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            window =>
            {
                var swWindow = Stopwatch.StartNew();
                var windowEntries = ScoreWindow(
                    window, fullLibrary, spectraByWindowKey, ms1Spectra,
                    rtCalibration, ms1Calibration, rtToleranceGlobal, rtSigmaGlobal,
                    scorer, context);
                swWindow.Stop();

                windowTimings.Add(new WindowTiming
                {
                    CenterMz = window.Center,
                    Seconds = swWindow.Elapsed.TotalSeconds,
                    CandidateCount = windowEntries.Count
                });

                lock (lockObj)
                {
                    allEntries.AddRange(windowEntries);
                    windowsProcessed++;
                    if (windowsProcessed % 10 == 0 || windowsProcessed == windowsToScore.Count)
                    {
                        LogInfo(string.Format("  Scored {0}/{1} isolation windows ({2} entries so far)",
                            windowsProcessed, windowsToScore.Count, allEntries.Count));
                    }
                }
            });

            ProfilerHooks.SaveAndStopMeasure();
            ProfilerHooks.LogMemoryStats(LogInfo, "post-main-search");

            if (context.XcorrScratchPool != null)
            {
                LogInfo(string.Format(
                    "[POOL] scratch_allocs={0}, bins_allocs={1}",
                    context.XcorrScratchPool.ScratchAllocCount,
                    context.XcorrScratchPool.BinsAllocCount));
            }

            // Summarize per-window timings.
            LogWindowTimingSummary(windowTimings);

            return allEntries;
        }

        /// <summary>
        /// Per-window timing record for diagnostic summarization.
        /// </summary>
        private class WindowTiming
        {
            public double CenterMz { get; set; }
            public double Seconds { get; set; }
            public int CandidateCount { get; set; }
        }

        /// <summary>
        /// Log min/median/max per-window scoring times and the slowest window's candidate count.
        /// </summary>
        private static void LogWindowTimingSummary(ConcurrentBag<WindowTiming> timings)
        {
            if (timings == null || timings.Count == 0)
                return;

            var sorted = timings.OrderBy(t => t.Seconds).ToList();
            int n = sorted.Count;
            double minS = sorted[0].Seconds;
            double maxS = sorted[n - 1].Seconds;
            double medS = sorted[n / 2].Seconds;
            var slowest = sorted[n - 1];
            LogInfo(string.Format(
                "[TIMING] Per-window: min={0:F2}s, median={1:F2}s, max={2:F2}s (slowest m/z={3:F1} had {4} candidates)",
                minS, medS, maxS, slowest.CenterMz, slowest.CandidateCount));
        }

        /// <summary>
        /// Score all candidate library entries within a single isolation window.
        /// For each candidate:
        /// 1. Extract fragment XICs from spectra in this window
        /// 2. Detect consensus CWT peaks
        /// 3. Score XCorr and LibCosine at the best peak apex
        /// 4. Build feature set and create FdrEntry
        /// </summary>
        private List<FdrEntry> ScoreWindow(
            IsolationWindow window,
            List<LibraryEntry> fullLibrary,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            RTCalibration rtCalibration,
            MzCalibrationResult ms1Calibration,
            double globalRtTolerance,
            double rtSigma,
            SpectralScorer scorer,
            ScoringContext context)
        {
            var config = context.Config;
            var entries = new List<FdrEntry>();

            int windowKey = (int)Math.Round(window.Center * 10.0);
            List<Spectrum> windowSpectra;
            if (!spectraByWindowKey.TryGetValue(windowKey, out windowSpectra) ||
                windowSpectra.Count == 0)
            {
                return entries;
            }

            // Sort spectra by RT for XIC extraction
            windowSpectra.Sort((a, b) => a.RetentionTime.CompareTo(b.RetentionTime));

            // Find candidate library entries whose precursor m/z falls in this window.
            // No minimum fragment count filter - matches Rust which scores all entries.
            var candidates = new List<LibraryEntry>();
            foreach (var entry in fullLibrary)
            {
                if (entry.Fragments == null || entry.Fragments.Count == 0)
                    continue;
                if (window.Contains(entry.PrecursorMz))
                    candidates.Add(entry);
            }

            if (candidates.Count == 0)
                return entries;

            // Build RT array for this window
            double[] windowRts = new double[windowSpectra.Count];
            for (int i = 0; i < windowSpectra.Count; i++)
                windowRts[i] = windowSpectra[i].RetentionTime;

            // Pre-preprocess all window spectra for XCorr via the resolution
            // strategy. Both Unit-res and HRAM now produce a dense
            // double[NSpectra][NBins] cache by renting from the scratch
            // pool; per-candidate scoring then hits the O(n_fragments)
            // XcorrFromPreprocessed fast path. Matches Rust pipeline.rs:
            // 5954-5957 (preprocessed_xcorr per window). Release the rented
            // bins arrays back to the pool once all candidates for this
            // window are scored.
            var preprocessedXcorr = context.Resolution.PreprocessWindowSpectra(
                windowSpectra, scorer, context.XcorrScratchPool);

            try
            {
                // Score each candidate
                foreach (var candidate in candidates)
                {
                    var fdrEntry = ScoreCandidate(
                        candidate, windowSpectra, windowRts,
                        preprocessedXcorr,
                        ms1Spectra, rtCalibration, ms1Calibration,
                        globalRtTolerance, rtSigma,
                        scorer, context);

                    if (fdrEntry != null)
                        entries.Add(fdrEntry);
                }

                return entries;
            }
            finally
            {
                context.Resolution.ReleaseWindowCache(preprocessedXcorr, context.XcorrScratchPool);
            }
        }

        // Diagnostic: log detailed trace for a specific peptide. Set this to a
        // peptide modified sequence to dump its RT window, XICs, CWT peaks, and
        // winning peak selection. Used for bisecting divergences with Rust.
        private const string DIAG_PEPTIDE = "AAAAAAAAAAAAAAAGAGAGAK";

        /// <summary>
        /// Score a single library entry candidate against spectra in its isolation window.
        /// Extracts fragment XICs, detects CWT peaks, and scores at the best apex.
        /// </summary>
        // IEEE 754-2008 §5.10 total order on doubles: matches Rust
        // f64::total_cmp so -0.0 < +0.0 and NaNs sort consistently.
        // Used by the main-search peak-ranking tie-break.
        private static bool TotalOrderGreater(double a, double b)
        {
            long la = BitConverter.DoubleToInt64Bits(a);
            long lb = BitConverter.DoubleToInt64Bits(b);
            if (la < 0) la ^= 0x7FFFFFFFFFFFFFFFL;
            if (lb < 0) lb ^= 0x7FFFFFFFFFFFFFFFL;
            return la > lb;
        }

        private FdrEntry ScoreCandidate(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            WindowXcorrCache preprocessedXcorr,
            List<MS1Spectrum> ms1Spectra,
            RTCalibration rtCalibration,
            MzCalibrationResult ms1Calibration,
            double globalRtTolerance,
            double rtSigma,
            SpectralScorer scorer,
            ScoringContext context)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            bool diag = !candidate.IsDecoy && candidate.ModifiedSequence == DIAG_PEPTIDE
                && candidate.Charge == 2;
            int nScans = windowSpectra.Count;
            if (nScans < 5)
                return null;

            // Determine RT search window.
            // Use the global tolerance passed from RunCoelutionScoring (matches
            // Rust's single rt_tolerance for all entries in run_search).
            double expectedRt = rtCalibration != null
                ? rtCalibration.Predict(candidate.RetentionTime)
                : candidate.RetentionTime;
            double rtTolerance = globalRtTolerance;

            if (diag)
            {
                LogInfo(string.Format(
                    "[DIAG] {0} charge {1}: library_rt={2:F3}, expected_rt={3:F3}, tolerance={4:F3}",
                    candidate.ModifiedSequence, candidate.Charge,
                    candidate.RetentionTime, expectedRt, rtTolerance));
                LogInfo(string.Format(
                    "[DIAG] {0}: window m/z={1:F3}, fragments={2}, window_spectra={3}",
                    candidate.ModifiedSequence, candidate.PrecursorMz,
                    candidate.Fragments.Count, nScans));
            }

            // Find scan range for XIC extraction. Matches Rust pipeline.rs
            // commit 885339b: extract over a window wider than rtTolerance so
            // CWT has context on both sides of any in-tolerance apex to
            // determine full peak boundaries. The apex itself is still
            // required to be within rtTolerance (enforced in the
            // candidate-scoring loop below). Half-width is rtTolerance plus
            // max(rtTolerance, 0.1) — tight-calibration runs get a 0.1 min
            // floor of extra context; wider runs scale with rtTolerance.
            double xicHalfWidth = rtTolerance + Math.Max(rtTolerance, 0.1);
            int startScan = -1, endScan = -1;
            for (int i = 0; i < nScans; i++)
            {
                if (windowRts[i] > expectedRt + xicHalfWidth)
                    break;
                if (windowRts[i] >= expectedRt - xicHalfWidth)
                {
                    if (startScan < 0)
                        startScan = i;
                    endScan = i;
                }
            }

            if (diag)
            {
                if (startScan >= 0 && endScan >= 0)
                {
                    LogInfo(string.Format(
                        "[DIAG] {0}: scan range [{1}..{2}] RT [{3:F3}..{4:F3}] ({5} scans)",
                        candidate.ModifiedSequence, startScan, endScan,
                        windowRts[startScan], windowRts[endScan],
                        endScan - startScan + 1));
                    LogInfo(string.Format(
                        "[DIAG] {0}: spectrum scan_numbers in range: first={1}, last={2}",
                        candidate.ModifiedSequence,
                        windowSpectra[startScan].ScanNumber,
                        windowSpectra[endScan].ScanNumber));
                }
                else
                {
                    LogInfo(string.Format(
                        "[DIAG] {0}: no scans in RT window [{1:F3}..{2:F3}]",
                        candidate.ModifiedSequence, expectedRt - xicHalfWidth,
                        expectedRt + xicHalfWidth));
                }
            }

            if (startScan < 0 || endScan < 0 || endScan - startScan + 1 < 5)
                return null;

            int rangeLen = endScan - startScan + 1;

            // Signal pre-filter: require at least 2 of top 6 fragments present
            // in at least 3 of 4 consecutive scans. Matches Rust pipeline.rs:6032-6066.
            // Skips noise-only candidates before the expensive XIC extraction.
            if (config.PrefilterEnabled)
            {
                const int WIN = 4;
                const int MIN_PASS = 3;
                bool[] window = new bool[WIN];
                int winSum = 0;
                bool hasSignal = false;

                for (int i = startScan; i <= endScan; i++)
                {
                    bool passes = HasTopNFragmentMatch(
                        candidate, windowSpectra[i].Mzs, config.FragmentTolerance);
                    int slot = (i - startScan) % WIN;
                    if (window[slot])
                        winSum--;
                    window[slot] = passes;
                    if (passes)
                        winSum++;
                    if (i - startScan + 1 >= WIN && winSum >= MIN_PASS)
                    {
                        hasSignal = true;
                        break;
                    }
                }
                if (!hasSignal)
                    return null;
            }

            // Extract fragment XICs within the RT range
            var xics = ExtractFragmentXics(
                candidate, windowSpectra, windowRts, startScan, endScan, config);

            // Per-entry search XIC diagnostic (thread-safe: unique file per entry_id)
            if (OspreyDiagnostics.ShouldDumpSearchXicFor(candidate.Id))
            {
                OspreyDiagnostics.WriteSearchXicDump(
                    candidate, expectedRt, rtTolerance,
                    startScan, endScan, rangeLen,
                    windowSpectra, xics);
            }

            if (xics.Count < 2)
                return null;

            // Detect candidate peaks with three-tier fallback matching Rust pipeline.rs:6244-6259.
            //   1. CWT consensus (primary)
            //   2. Peak detection on median polish elution profile (fallback 1)
            //   3. Peak detection on reference XIC (fallback 2)
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            if (peaks.Count == 0)
            {
                // Fallback 1: detect peaks on the median polish elution profile.
                // Rust: detect_all_xic_peaks(&mp.elution_profile, 0.01, 5.0)
                var polishXics = new List<KeyValuePair<int, double[]>>();
                for (int f = 0; f < xics.Count; f++)
                    polishXics.Add(new KeyValuePair<int, double[]>(
                        xics[f].FragmentIndex, xics[f].Intensities));
                double[] polishRts = xics[0].RetentionTimes;

                var fullPolish = TukeyMedianPolish.Compute(polishXics, polishRts, 10, 0.01);
                if (fullPolish != null && fullPolish.ElutionProfileRts != null &&
                    fullPolish.ElutionProfileIntensities != null)
                {
                    peaks = PeakDetector.DetectAllXicPeaks(
                        fullPolish.ElutionProfileRts,
                        fullPolish.ElutionProfileIntensities,
                        0.01, 5.0);
                }
            }

            if (peaks.Count == 0)
            {
                // Fallback 2: detect peaks on the reference XIC (highest total intensity).
                // Rust: detect_all_xic_peaks(ref_xic, 0.01, 5.0)
                int refIdx = 0;
                double bestTotal = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double total = 0.0;
                    for (int i = 0; i < xics[f].Intensities.Length; i++)
                        total += xics[f].Intensities[i];
                    if (total > bestTotal) { bestTotal = total; refIdx = f; }
                }
                peaks = PeakDetector.DetectAllXicPeaks(
                    xics[refIdx].RetentionTimes,
                    xics[refIdx].Intensities,
                    0.01, 5.0);
            }

            if (diag)
            {
                LogInfo(string.Format(
                    "[DIAG] {0}: xics extracted={1}, peaks={2}",
                    candidate.ModifiedSequence, xics.Count, peaks.Count));
                for (int i = 0; i < peaks.Count; i++)
                {
                    var p = peaks[i];
                    int apexAbsIdx = startScan + p.ApexIndex;
                    double apexRt = windowRts[apexAbsIdx];
                    uint apexScanNum = windowSpectra[apexAbsIdx].ScanNumber;
                    LogInfo(string.Format(
                        "[DIAG] {0}: peak[{1}] apex_local={2} apex_rt={3:F3} scan#={4} range=[{5}..{6}]",
                        candidate.ModifiedSequence, i, p.ApexIndex,
                        apexRt, apexScanNum, p.StartIndex, p.EndIndex));
                }
            }
            if (peaks.Count == 0)
                return null;

            // Rust scores each candidate peak by mean pairwise fragment
            // correlation weighted by a Gaussian RT penalty and an intensity
            // tiebreaker, then picks the highest-ranked peak. The RT penalty
            // prevents strong interferers at the wrong RT from beating the
            // correct peak on coelution alone; the intensity tiebreaker
            // ensures the main peak wins over its own shoulder when coelution
            // scores are nearly identical. Matches Rust pipeline.rs:6685-6760.
            //   rank_score = coelution * exp(-dt^2 / (2 * sigma^2)) * ln(1 + apex_intensity)
            // where dt = |peak_apex_rt - expected_rt|. Peak at expected
            // position gets RT penalty=1.0; 5-sigma away gets ~0.01.

            // Reference XIC = highest total-intensity fragment. Matches Rust
            // run_search which selects ref_xic before CWT and reuses it for
            // apex intensity lookup.
            int refXicIdx = 0;
            double refXicBestTotal = -1.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                for (int i = 0; i < xics[f].Intensities.Length; i++)
                    total += xics[f].Intensities[i];
                if (total > refXicBestTotal) { refXicBestTotal = total; refXicIdx = f; }
            }
            double[] refXicIntensities = xics[refXicIdx].Intensities;

            XICPeakBounds bestPeak = null;
            double bestRankScore = double.MinValue;
            int bestPeakIdx = -1;
            double twoSigmaSq = 2.0 * rtSigma * rtSigma;
            for (int pi = 0; pi < peaks.Count; pi++)
            {
                var p = peaks[pi];
                int pLen = p.EndIndex - p.StartIndex + 1;
                if (pLen < 3)
                    continue;

                // Apex-acceptance filter (Rust pipeline.rs commit 885339b):
                // the XIC extraction window above is wider than rtTolerance so
                // CWT can extend peak boundaries past the acceptance edge, but
                // the detected apex itself must fall within rtTolerance of
                // expectedRt. Preserves first-pass selectivity -- only
                // boundaries are allowed to extend past rtTolerance; apex
                // locations still have to be within it.
                double peakApexRt = windowRts[startScan + p.ApexIndex];
                double rtResidual = Math.Abs(peakApexRt - expectedRt);
                if (rtResidual > rtTolerance)
                    continue;

                double sum = 0.0;
                int count = 0;
                for (int ii = 0; ii < xics.Count; ii++)
                {
                    for (int jj = ii + 1; jj < xics.Count; jj++)
                    {
                        double corr = PearsonCorrelation(
                            xics[ii].Intensities, xics[jj].Intensities,
                            p.StartIndex, p.EndIndex);
                        if (!double.IsNaN(corr))
                        {
                            sum += corr;
                            count++;
                        }
                    }
                }
                double coelutionScore = count > 0 ? sum / count : 0.0;

                double rtPenalty = Math.Exp(-(rtResidual * rtResidual) / twoSigmaSq);

                // Intensity tiebreaker (Rust pipeline.rs:6753-6758, added in
                // v26.3.1 commit 4d0119d): log(1 + apex_intensity) breaks ties
                // between main peak and shoulder without dominating the
                // coelution ranking.
                double apexIntensity = refXicIntensities[p.ApexIndex];
                double intensityWeight = Math.Log(1.0 + apexIntensity);

                double rankScore = coelutionScore * rtPenalty * intensityWeight;

                if (diag)
                {
                    LogInfo(string.Format(
                        "[DIAG] {0}: peak[{1}] pairwise_corr_mean={2:F4} rt_penalty={3:F4} int_weight={4:F2} rank={5:F4}",
                        candidate.ModifiedSequence, pi, coelutionScore, rtPenalty, intensityWeight, rankScore));
                }
                // Tie-break via IEEE 754-2008 total order (matches Rust's
                // f64::total_cmp used in run_search's scored_candidates
                // sort). When intensityWeight is 0 (ref_xic intensity at
                // apex is 0), rankScore is -0.0 or +0.0 depending on
                // coelutionScore sign. Standard '>' treats -0.0 == +0.0, so
                // without total-order compare the tie falls back to
                // iteration order, producing divergent peak picks vs Rust
                // for the handful of entries where all in-tolerance peaks
                // have zero reference intensity.
                if (TotalOrderGreater(rankScore, bestRankScore))
                {
                    bestRankScore = rankScore;
                    bestPeak = p;
                    bestPeakIdx = pi;
                }
            }

            if (diag && bestPeak != null)
            {
                int apexAbsIdx = startScan + bestPeak.ApexIndex;
                LogInfo(string.Format(
                    "[DIAG] {0}: WINNER peak[{1}] apex_rt={2:F3} scan#={3}",
                    candidate.ModifiedSequence, bestPeakIdx,
                    windowRts[apexAbsIdx], windowSpectra[apexAbsIdx].ScanNumber));
            }

            // Append peak boundaries to search XIC diagnostic dump
            if (OspreyDiagnostics.ShouldDumpSearchXicFor(candidate.Id))
            {
                string peakDumpPath = "cs_search_xic_entry_" + candidate.Id + ".txt";
                using (var dw = new StreamWriter(peakDumpPath, true))
                {
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# CWT PEAKS: {0} candidates", peaks.Count));
                    dw.WriteLine("peak\tidx\tstart\tapex\tend\tcorr_score");
                    for (int pi = 0; pi < peaks.Count; pi++)
                    {
                        var p = peaks[pi];
                        int pLen = p.EndIndex - p.StartIndex + 1;
                        double corrScore = 0.0;
                        if (pLen >= 3)
                        {
                            double psum = 0.0; int pcnt = 0;
                            for (int ii = 0; ii < xics.Count; ii++)
                                for (int jj = ii + 1; jj < xics.Count; jj++)
                                {
                                    double c = PearsonCorrelation(xics[ii].Intensities, xics[jj].Intensities,
                                        p.StartIndex, p.EndIndex);
                                    if (!double.IsNaN(c)) { psum += c; pcnt++; }
                                }
                            corrScore = pcnt > 0 ? psum / pcnt : 0.0;
                        }
                        dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "peak\t{0}\t{1}\t{2}\t{3}\t{4:F10}",
                            pi, p.StartIndex, p.ApexIndex, p.EndIndex, corrScore));
                    }
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# BEST PEAK: idx={0} start={1} apex={2} end={3}",
                        bestPeakIdx,
                        bestPeak != null ? bestPeak.StartIndex : -1,
                        bestPeak != null ? bestPeak.ApexIndex : -1,
                        bestPeak != null ? bestPeak.EndIndex : -1));
                }
            }

            if (bestPeak == null)
                return null;

            int apexGlobalIdx = startScan + bestPeak.ApexIndex;

            // Score at the apex spectrum
            if (apexGlobalIdx < 0 || apexGlobalIdx >= windowSpectra.Count)
                return null;

            var apexSpectrum = windowSpectra[apexGlobalIdx];

            // LibCosine at apex
            double libCosine = scorer.LibCosine(apexSpectrum, candidate, config.FragmentTolerance);

            // XCorr at apex via the resolution strategy. Pool avoids per-
            // call 100K-bin LOH allocation on HRAM (no-op on Unit-res).
            double xcorr = resolution.ScoreXcorr(
                preprocessedXcorr, apexGlobalIdx, apexSpectrum, candidate, scorer,
                context.XcorrScratchPool);



            // Compute pairwise coelution features (sum, max, n_positive).
            double coelutionSum, coelutionMax;
            int nCoelutingFragments;
            ComputeCoelutionStats(xics, bestPeak,
                out coelutionSum, out coelutionMax, out nCoelutingFragments);

            // Peak shape features: apex, area, sharpness
            double peakApex, peakArea, peakSharpness;
            ComputePeakShapeFeatures(xics, bestPeak,
                out peakApex, out peakArea, out peakSharpness);

            // RT deviation (absolute even if calibration disabled - measured vs library RT)
            double rtDeviation = apexSpectrum.RetentionTime - expectedRt;
            double absRtDeviation = Math.Abs(rtDeviation);

            // Count consecutive ions
            byte consecutiveIons = CountConsecutiveIons(candidate, apexSpectrum, config);

            // Count fragment matches
            byte top6Matches = CountTop6Matches(candidate, apexSpectrum, config);

            // Explained intensity, mass accuracy at apex
            double explainedIntensity, massAccuracyMean, absMassAccuracyMean;
            ComputeApexMatchFeatures(candidate, apexSpectrum, config,
                out explainedIntensity, out massAccuracyMean, out absMassAccuracyMean);

            // MS1 features: precursor coelution, isotope cosine.
            // Rust pipeline.rs:5362 gates on is_hram - unit resolution skips MS1.
            double ms1PrecursorCoelution = 0.0;
            double ms1IsotopeCosine = 0.0;
            if (resolution.HasMs1Features && ms1Spectra != null && ms1Spectra.Count > 0)
            {
                // Find reference XIC (highest total intensity) for MS1 coelution.
                // Same selection as ComputePeakShapeFeatures and Rust pipeline.rs.
                int ms1RefIdx = 0;
                double ms1BestTotal = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double total = 0.0;
                    double[] inten = xics[f].Intensities;
                    for (int k = 0; k < inten.Length; k++)
                        total += inten[k];
                    if (total >= ms1BestTotal) { ms1BestTotal = total; ms1RefIdx = f; }
                }
                ComputeMs1Features(
                    candidate, xics, ms1RefIdx, bestPeak,
                    windowRts, startScan,
                    ms1Spectra, ms1Calibration, config,
                    out ms1PrecursorCoelution, out ms1IsotopeCosine);
            }

            // Savitzky-Golay weighted spectral scores at apex +/- 2 scans.
            // Matches Rust pipeline.rs sg_xcorr / sg_cosine. Uses candidate-local
            // indices (within startScan..endScan) not global window indices, matching
            // Rust's cand_spectra bounds. Cosine uses mass-range-filtered matching
            // (compute_cosine_at_scan) not LibCosine.
            double sgXcorr = 0.0;
            double sgCosine = 0.0;
            for (int offset = -2; offset <= 2; offset++)
            {
                double weight = SG_WEIGHTS[offset + 2];
                int candIdx = bestPeak.ApexIndex + offset;
                if (candIdx < 0 || candIdx >= rangeLen)
                    continue;
                int globalIdx = startScan + candIdx;
                var s = windowSpectra[globalIdx];
                sgXcorr += resolution.ScoreXcorr(preprocessedXcorr, globalIdx, s, candidate, scorer,
                    context.XcorrScratchPool) * weight;
                sgCosine += ComputeCosineAtScan(candidate, s, config) * weight;
            }

            // Tukey median polish features (15, 16, 19, 20).
            // Crop XICs to the peak range so the polish operates only on signal,
            // not the wider RT search window. Matches Rust pipeline.rs:5198-5212.
            double mpCosine = 0.0;
            double mpResidualRatio = 1.0;
            double mpMinFragmentR2 = 0.0;
            double mpResidualCorr = 0.0;
            int peakLen = bestPeak.EndIndex - bestPeak.StartIndex + 1;
            if (peakLen >= 3)
            {
                var peakXics = new List<KeyValuePair<int, double[]>>(xics.Count);
                var peakRts = new double[peakLen];
                for (int s = 0; s < peakLen; s++)
                    peakRts[s] = xics[0].RetentionTimes[bestPeak.StartIndex + s];
                for (int xi = 0; xi < xics.Count; xi++)
                {
                    var src = xics[xi].Intensities;
                    var slice = new double[peakLen];
                    for (int s = 0; s < peakLen; s++)
                        slice[s] = src[bestPeak.StartIndex + s];
                    peakXics.Add(new KeyValuePair<int, double[]>(xics[xi].FragmentIndex, slice));
                }

                var polish = TukeyMedianPolish.Compute(peakXics, peakRts, 10, 0.01);
                if (polish != null)
                {
                    mpCosine = TukeyMedianPolish.LibCosine(polish, candidate.Fragments);
                    mpResidualRatio = TukeyMedianPolish.ResidualRatio(polish);
                    mpMinFragmentR2 = TukeyMedianPolish.MinFragmentR2(polish);
                    mpResidualCorr = TukeyMedianPolish.ResidualCorrelation(polish);

                    // Median polish diagnostic for bisection
                    if (OspreyDiagnostics.ShouldDumpMpFor(apexSpectrum.ScanNumber, candidate.ModifiedSequence))
                    {
                        OspreyDiagnostics.WriteMpDump(
                            candidate, apexSpectrum.ScanNumber,
                            bestPeak, peakLen,
                            mpCosine, mpResidualRatio, mpMinFragmentR2, mpResidualCorr,
                            polish, peakXics);
                    }
                }
            }

            // Build full 21-element PIN feature vector
            double[] features = new double[NUM_PIN_FEATURES];
            features[0] = coelutionSum;
            features[1] = coelutionMax;
            features[2] = nCoelutingFragments;
            features[3] = peakApex;
            features[4] = peakArea;
            features[5] = peakSharpness;
            features[6] = xcorr;
            features[7] = consecutiveIons;
            features[8] = explainedIntensity;
            features[9] = massAccuracyMean;
            features[10] = absMassAccuracyMean;
            features[11] = rtDeviation;
            features[12] = absRtDeviation;
            features[13] = ms1PrecursorCoelution;
            features[14] = ms1IsotopeCosine;
            features[15] = mpCosine;
            features[16] = mpResidualRatio;
            features[17] = sgXcorr;
            features[18] = sgCosine;
            features[19] = mpMinFragmentR2;
            features[20] = mpResidualCorr;

            // Build FdrEntry
            var entry = new FdrEntry
            {
                EntryId = candidate.Id,
                IsDecoy = candidate.IsDecoy,
                Charge = candidate.Charge,
                ScanNumber = apexSpectrum.ScanNumber,
                ApexRt = apexSpectrum.RetentionTime,
                StartRt = windowRts[startScan + bestPeak.StartIndex],
                EndRt = windowRts[startScan + bestPeak.EndIndex],
                CoelutionSum = coelutionSum,
                Score = coelutionSum,
                ModifiedSequence = candidate.ModifiedSequence,
                Features = features
            };

            return entry;
        }

        /// <summary>
        /// Get cached top-6 fragment m/z values for an entry. Computed once,
        /// reused across all prefilter calls for the same entry. Thread-safe
        /// via ConcurrentDictionary.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, double[]> _top6MzCache =
            new ConcurrentDictionary<uint, double[]>();

        private static double[] GetTop6FragmentMzs(LibraryEntry entry)
        {
            return _top6MzCache.GetOrAdd(entry.Id, _ =>
            {
                var frags = entry.Fragments;
                if (frags == null || frags.Count == 0)
                    return new double[0];

                int nTop = Math.Min(frags.Count, 6);
                if (frags.Count <= 6)
                {
                    var mzs = new double[frags.Count];
                    for (int i = 0; i < frags.Count; i++)
                        mzs[i] = frags[i].Mz;
                    return mzs;
                }

                // Find top 6 by intensity
                var indices = new int[frags.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                Array.Sort(indices, (a, b) =>
                    frags[b].RelativeIntensity.CompareTo(frags[a].RelativeIntensity));
                var result = new double[nTop];
                for (int i = 0; i < nTop; i++)
                    result[i] = frags[indices[i]].Mz;
                return result;
            });
        }

        /// <summary>
        /// Check if at least 2 of the top 6 library fragments have matching peaks
        /// in the spectrum. Uses cached top-6 m/z values (no allocation per call).
        /// Port of has_topn_fragment_match in osprey-scoring/src/lib.rs:112.
        /// </summary>
        private static bool HasTopNFragmentMatch(
            LibraryEntry entry, double[] spectrumMzs, FragmentToleranceConfig fragTol)
        {
            var frags = entry.Fragments;
            if (frags == null || frags.Count == 0 || spectrumMzs == null || spectrumMzs.Length == 0)
                return true;

            double[] top6Mzs = GetTop6FragmentMzs(entry);
            int nTop = top6Mzs.Length;
            int requiredMatches = nTop <= 1 ? 1 : 2;
            int matchCount = 0;

            // Per-fragment tolerance: in ppm mode, each fragment's Da window
            // depends on its own m/z. Matches Rust has_topn_fragment_match.
            for (int t = 0; t < nTop; t++)
            {
                double mz = top6Mzs[t];
                double tolDa = fragTol.ToleranceDa(mz);
                double lower = mz - tolDa;
                double upper = mz + tolDa;
                int lo = 0, hi = spectrumMzs.Length;
                while (lo < hi) { int mid = (lo + hi) / 2; if (spectrumMzs[mid] < lower) lo = mid + 1; else hi = mid; }
                if (lo < spectrumMzs.Length && spectrumMzs[lo] <= upper)
                {
                    matchCount++;
                    if (matchCount >= requiredMatches)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Extract fragment XICs for a candidate across the scan range.
        /// </summary>
        private List<XicData> ExtractFragmentXics(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            int startScan, int endScan,
            OspreyConfig config)
        {
            // Port of Rust extract_fragment_xics (osprey-scoring/src/lib.rs:505).
            // Differences from the previous C# implementation:
            //   1. Use top-6 fragments by relative intensity (not all fragments)
            //   2. Pick the closest peak by m/z within tolerance (not most intense)
            //   3. Always include all selected fragments, even all-zero XICs
            //      (dropping all-zero fragments biases decoys to higher R^2)
            int rangeLen = endScan - startScan + 1;
            var xics = new List<XicData>();
            if (candidate.Fragments == null || candidate.Fragments.Count == 0)
                return xics;

            int nFrags = candidate.Fragments.Count;
            int nTop = Math.Min(nFrags, CAL_TOP_N_FRAGMENTS);
            int[] topIndices;
            if (nFrags <= CAL_TOP_N_FRAGMENTS)
            {
                topIndices = new int[nFrags];
                for (int i = 0; i < nFrags; i++)
                    topIndices[i] = i;
            }
            else
            {
                var indexed = new List<KeyValuePair<int, float>>(nFrags);
                for (int i = 0; i < nFrags; i++)
                    indexed.Add(new KeyValuePair<int, float>(i, candidate.Fragments[i].RelativeIntensity));
                indexed.Sort((a, b) => b.Value.CompareTo(a.Value));
                topIndices = new int[nTop];
                for (int i = 0; i < nTop; i++)
                    topIndices[i] = indexed[i].Key;
            }

            // Build shared RT array for this range
            double[] rangeRts = new double[rangeLen];
            for (int i = 0; i < rangeLen; i++)
                rangeRts[i] = windowRts[startScan + i];

            foreach (int fragIdx in topIndices)
            {
                var fragment = candidate.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(fragment.Mz);
                double lower = fragment.Mz - tolDa;
                double upper = fragment.Mz + tolDa;

                double[] intensities = new double[rangeLen];

                for (int scanIdx = 0; scanIdx < rangeLen; scanIdx++)
                {
                    var spectrum = windowSpectra[startScan + scanIdx];
                    if (spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                        continue;

                    int lo = BinarySearchLowerBound(spectrum.Mzs, lower);
                    if (lo >= spectrum.Mzs.Length || spectrum.Mzs[lo] > upper)
                        continue;

                    // Find closest peak by m/z within tolerance (matches Rust).
                    double bestDiff = Math.Abs(spectrum.Mzs[lo] - fragment.Mz);
                    double bestIntensity = spectrum.Intensities[lo];
                    for (int k = lo + 1; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                    {
                        double diff = Math.Abs(spectrum.Mzs[k] - fragment.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIntensity = spectrum.Intensities[k];
                        }
                    }
                    intensities[scanIdx] = bestIntensity;
                }

                // Always include the fragment XIC, even if all zero. Zero intensities
                // are valid data (no centroided peak found) and dropping all-zero
                // fragments biases decoys to higher R^2. Matches Rust behavior.
                xics.Add(new XicData(fragIdx, rangeRts, intensities));
            }

            return xics;
        }

        /// <summary>
        /// Compute coelution sum/max and count of positively-correlated fragments
        /// from pairwise fragment correlations.
        /// </summary>
        private void ComputeCoelutionStats(
            List<XicData> xics, XICPeakBounds peak,
            out double sum, out double max, out int nCoeluting)
        {
            sum = 0.0;
            max = 0.0;
            nCoeluting = 0;

            if (xics.Count < 2)
                return;

            // Per-fragment mean pairwise correlation. A fragment is "coeluting" if
            // its mean pairwise correlation is > 0. Matches Rust pipeline.rs:5049-5058
            // which averages per_frag_corr_sum[i]/count and checks > 0.
            double[] fragCorrSum = new double[xics.Count];
            int[] fragCorrCount = new int[xics.Count];
            bool haveAny = false;
            double maxCorr = double.NegativeInfinity;

            for (int i = 0; i < xics.Count; i++)
            {
                for (int j = i + 1; j < xics.Count; j++)
                {
                    double corr = PearsonCorrelation(
                        xics[i].Intensities, xics[j].Intensities,
                        peak.StartIndex, peak.EndIndex);
                    if (double.IsNaN(corr))
                        continue;

                    sum += corr;
                    if (corr > maxCorr)
                        maxCorr = corr;
                    haveAny = true;

                    fragCorrSum[i] += corr;
                    fragCorrCount[i]++;
                    fragCorrSum[j] += corr;
                    fragCorrCount[j]++;
                }
            }

            if (haveAny)
                max = maxCorr;

            for (int i = 0; i < xics.Count; i++)
            {
                if (fragCorrCount[i] > 0 && fragCorrSum[i] / fragCorrCount[i] > 0.0)
                    nCoeluting++;
            }
        }

        /// <summary>
        /// Compute peak shape features at the detected peak boundaries: summed XIC
        /// apex intensity, area under the summed XIC, and sharpness (apex / mean edge).
        /// </summary>
        private void ComputePeakShapeFeatures(
            List<XicData> xics, XICPeakBounds peak,
            out double peakApex, out double peakArea, out double peakSharpness)
        {
            peakApex = 0.0;
            peakArea = 0.0;
            peakSharpness = 0.0;

            if (xics.Count == 0)
                return;

            int len = xics[0].Intensities.Length;
            if (len == 0)
                return;

            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(len - 1, peak.EndIndex);
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));

            if (start > end)
                return;

            // Use reference XIC (highest total intensity), matching Rust pipeline.rs.
            // Rust computes peak_apex, peak_area, peak_sharpness from ref_xic only.
            int refIdx = 0;
            double bestTotal = 0.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                double[] inten = xics[f].Intensities;
                for (int i = 0; i < inten.Length; i++)
                    total += inten[i];
                if (total > bestTotal)
                {
                    bestTotal = total;
                    refIdx = f;
                }
            }

            double[] refInten = xics[refIdx].Intensities;
            double[] refRts = xics[refIdx].RetentionTimes;

            // Apex: max intensity in the reference XIC over the peak range.
            // Matches Rust: ref_xic[si..=ei].max_by(intensity). On ties
            // Rust's Iterator::max_by keeps the LAST equal element (see
            // std::cmp::max_by returning v2 on Ordering::Equal); use `>=`
            // here to match so flat-top peaks pick the same apex scan.
            double apexVal = 0.0;
            int apexIdx = start;
            for (int i = start; i <= end; i++)
            {
                if (refInten[i] >= apexVal)
                {
                    apexVal = refInten[i];
                    apexIdx = i;
                }
            }
            peakApex = apexVal;

            // Area: trapezoidal integration on the reference XIC.
            // Matches Rust trapezoidal_area(ref_xic[si..=ei]).
            double area = 0.0;
            for (int i = start; i < end; i++)
            {
                double dt = refRts[i + 1] - refRts[i];
                double avgHeight = (refInten[i] + refInten[i + 1]) * 0.5;
                area += avgHeight * dt;
            }
            peakArea = area;

            // Sharpness: mean of left and right slopes on the reference XIC.
            // Matches Rust pipeline.rs:5212-5234.
            double leftSlope = 0.0;
            if (apexIdx > start)
            {
                double dt = refRts[apexIdx] - refRts[start];
                if (dt > 1e-10)
                    leftSlope = (apexVal - refInten[start]) / dt;
            }
            double rightSlope = 0.0;
            if (end > apexIdx)
            {
                double dt = refRts[end] - refRts[apexIdx];
                if (dt > 1e-10)
                    rightSlope = (apexVal - refInten[end]) / dt;
            }
            peakSharpness = (leftSlope + rightSlope) * 0.5;
        }

        /// <summary>
        /// Compute cosine similarity at a single scan, filtering fragments to the
        /// spectrum's observed m/z range. Matches Rust compute_cosine_at_scan in
        /// osprey-scoring/src/lib.rs:382. Unlike LibCosine, fragments outside the
        /// spectrum's mass range are excluded (not treated as zero-intensity pairs).
        /// </summary>
        private static double ComputeCosineAtScan(
            LibraryEntry candidate, Spectrum spectrum, OspreyConfig config)
        {
            if (candidate.Fragments == null || candidate.Fragments.Count == 0 ||
                spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                return 0.0;

            double specMzMin = spectrum.Mzs[0];
            double specMzMax = spectrum.Mzs[spectrum.Mzs.Length - 1];

            var libPre = new List<double>();
            var obsPre = new List<double>();

            foreach (var frag in candidate.Fragments)
            {
                // Skip fragments outside the spectrum's mass range
                if (frag.Mz < specMzMin || frag.Mz > specMzMax)
                    continue;

                double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                double lower = frag.Mz - tolDa;
                double upper = frag.Mz + tolDa;

                int lo = BinarySearchLowerBound(spectrum.Mzs, lower);
                double bestIntensity = 0.0;
                double bestDiff = double.MaxValue;

                for (int k = lo; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                {
                    double diff = Math.Abs(spectrum.Mzs[k] - frag.Mz);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIntensity = spectrum.Intensities[k];
                    }
                }

                libPre.Add(Math.Sqrt(frag.RelativeIntensity));
                obsPre.Add(Math.Sqrt(bestIntensity));
            }

            if (libPre.Count == 0)
                return 0.0;

            // L2 normalize and dot product
            double libNorm = 0, obsNorm = 0, dot = 0;
            for (int i = 0; i < libPre.Count; i++)
            {
                libNorm += libPre[i] * libPre[i];
                obsNorm += obsPre[i] * obsPre[i];
                dot += libPre[i] * obsPre[i];
            }
            libNorm = Math.Sqrt(libNorm);
            obsNorm = Math.Sqrt(obsNorm);
            if (libNorm < 1e-12 || obsNorm < 1e-12)
                return 0.0;
            return dot / (libNorm * obsNorm);
        }

        /// <summary>
        /// Compute apex-level match features: explained intensity fraction, and mean
        /// signed / absolute mass error (in the tolerance unit, typically ppm).
        /// </summary>
        private void ComputeApexMatchFeatures(
            LibraryEntry candidate, Spectrum apexSpectrum, OspreyConfig config,
            out double explainedIntensity,
            out double massAccuracyMean,
            out double absMassAccuracyMean)
        {
            explainedIntensity = 0.0;
            massAccuracyMean = 0.0;
            absMassAccuracyMean = 0.0;

            if (apexSpectrum.Mzs == null || apexSpectrum.Mzs.Length == 0 ||
                candidate.Fragments == null || candidate.Fragments.Count == 0)
                return;

            double totalIntensity = 0.0;
            for (int i = 0; i < apexSpectrum.Intensities.Length; i++)
                totalIntensity += apexSpectrum.Intensities[i];

            double matchedIntensity = 0.0;
            double massErrSum = 0.0;
            double absMassErrSum = 0.0;
            int nMatched = 0;

            foreach (var frag in candidate.Fragments)
            {
                double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                double lower = frag.Mz - tolDa;
                double upper = frag.Mz + tolDa;

                int lo = BinarySearchLowerBound(apexSpectrum.Mzs, lower);
                double bestError = double.MaxValue;
                double bestIntensity = 0.0;
                double bestMz = 0.0;
                bool found = false;

                // Match closest peak by m/z (not most intense).
                // Matches Rust SpectralScorer::match_fragments in lib.rs:2239.
                for (int k = lo; k < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[k] <= upper; k++)
                {
                    double errorDa = Math.Abs(apexSpectrum.Mzs[k] - frag.Mz);
                    if (errorDa < bestError)
                    {
                        bestError = errorDa;
                        bestIntensity = apexSpectrum.Intensities[k];
                        bestMz = apexSpectrum.Mzs[k];
                        found = true;
                    }
                }

                if (found)
                {
                    matchedIntensity += bestIntensity;
                    double err = config.FragmentTolerance.MassError(frag.Mz, bestMz);
                    massErrSum += err;
                    absMassErrSum += Math.Abs(err);
                    nMatched++;
                }
            }

            if (totalIntensity > 1e-12)
                explainedIntensity = matchedIntensity / totalIntensity;

            if (nMatched > 0)
            {
                massAccuracyMean = massErrSum / nMatched;
                absMassAccuracyMean = absMassErrSum / nMatched;
            }
            else
            {
                // No matched fragments: report worst-case (calibrated) tolerance
                // as the absolute mass error, matching Rust compute_mass_accuracy
                // which returns (0.0, tolerance, tolerance) on empty matches
                // (osprey-scoring/src/lib.rs:462-465). This penalizes unmatched
                // entries in FDR instead of giving them a spurious 0 error.
                massAccuracyMean = 0.0;
                absMassAccuracyMean = config.FragmentTolerance.Tolerance;
            }
        }

        /// <summary>
        /// Compute MS1 features: correlation between the summed fragment XIC and the
        /// precursor MS1 XIC, and cosine similarity between the observed isotope envelope
        /// at the apex MS1 scan and the theoretical averagine envelope.
        /// </summary>
        private void ComputeMs1Features(
            LibraryEntry candidate,
            List<XicData> xics,
            int refXicIdx,
            XICPeakBounds peak,
            double[] windowRts, int startScan,
            List<MS1Spectrum> ms1Spectra,
            MzCalibrationResult ms1Calibration,
            OspreyConfig config,
            out double ms1PrecursorCoelution,
            out double ms1IsotopeCosine)
        {
            ms1PrecursorCoelution = 0.0;
            ms1IsotopeCosine = 0.0;

            if (xics.Count == 0 || ms1Spectra == null || ms1Spectra.Count == 0)
                return;

            int len = xics[0].Intensities.Length;
            if (len == 0)
                return;

            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(len - 1, peak.EndIndex);
            if (end - start + 1 < 3)
                return;

            // MS1 calibration: reverse-calibrate search m/z and use calibrated tolerance.
            // Matches Rust pipeline.rs:5363-5370 (reverse_calibrate_mz + calibrated_tolerance_ppm).
            double baseTolPpm = 10.0;
            double ms1TolPpm = baseTolPpm;
            double searchMz = candidate.PrecursorMz;
            if (ms1Calibration != null && ms1Calibration.Calibrated)
            {
                // calibrated_tolerance_ppm: max(3*SD, 1.0) ppm
                ms1TolPpm = Math.Max(3.0 * ms1Calibration.SD, 1.0);
                // reverse_calibrate_mz: observed ~ theoretical + offset
                if (ms1Calibration.Unit == "Th")
                    searchMz = candidate.PrecursorMz + ms1Calibration.Mean;
                else
                    searchMz = candidate.PrecursorMz * (1.0 + ms1Calibration.Mean / 1e6);
            }

            // Correlate MS1 precursor intensity with reference XIC (not summed fragment).
            // Rust pipeline.rs:5373-5389: uses ref_xic[start..=end], skips missing MS1.
            double[] refIntensities = xics[refXicIdx].Intensities;
            var ms1Intensities = new List<double>();
            var refValues = new List<double>();

            for (int i = start; i <= end; i++)
            {
                double rt = windowRts[startScan + i];
                var ms1 = FindNearestMs1(ms1Spectra, rt);
                if (ms1 != null)
                {
                    var peakInfo = ms1.FindPeakPpm(searchMz, ms1TolPpm);
                    double intensity = peakInfo.HasValue ? peakInfo.Value.Intensity : 0.0;
                    ms1Intensities.Add(intensity);
                    refValues.Add(i < refIntensities.Length ? refIntensities[i] : 0.0);
                }
            }

            if (ms1Intensities.Count >= 3)
            {
                double[] ms1Arr = ms1Intensities.ToArray();
                double[] refArr = refValues.ToArray();
                ms1PrecursorCoelution = PearsonCorrelation(ms1Arr, refArr, 0, ms1Arr.Length - 1);
                if (double.IsNaN(ms1PrecursorCoelution))
                    ms1PrecursorCoelution = 0.0;
            }

            // Isotope cosine at apex MS1 scan.
            // Rust pipeline.rs:5393-5404: gates on envelope.has_m0().
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));
            double apexRt = windowRts[startScan + apex];
            var apexMs1 = FindNearestMs1(ms1Spectra, apexRt);
            if (apexMs1 != null)
            {
                int charge = candidate.Charge > 0 ? candidate.Charge : 1;
                var envelope = IsotopeEnvelope.Extract(
                    apexMs1, searchMz, charge, ms1TolPpm);

                // Gate: skip if M0 peak is missing (matches Rust envelope.has_m0())
                if (envelope.Intensities != null && envelope.Intensities.Length > 1
                    && envelope.Intensities[1] > 0.0) // index 1 = M0 (M-1 at 0)
                {
                    // Sequence-based isotope distribution, matching Rust
                    // pipeline.rs:5400 peptide_isotope_cosine.
                    double score = IsotopeDistribution.PeptideIsotopeCosine(
                        candidate.Sequence, envelope.Intensities);
                    if (score >= 0.0)
                        ms1IsotopeCosine = score;
                }
            }
        }

        /// <summary>
        /// Find the MS1 spectrum with retention time closest to the given RT.
        /// Assumes MS1 spectra are sorted by RT.
        /// </summary>
        private static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
        {
            if (ms1Spectra == null || ms1Spectra.Count == 0)
                return null;

            int lo = 0;
            int hi = ms1Spectra.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (ms1Spectra[mid].RetentionTime < rt)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            if (lo >= ms1Spectra.Count)
                return ms1Spectra[ms1Spectra.Count - 1];
            if (lo == 0)
                return ms1Spectra[0];

            var prev = ms1Spectra[lo - 1];
            var next = ms1Spectra[lo];
            return Math.Abs(prev.RetentionTime - rt) <= Math.Abs(next.RetentionTime - rt)
                ? prev
                : next;
        }

        /// <summary>
        /// Build an approximate averagine theoretical isotope envelope at 5 positions
        /// [M-1, M+0, M+1, M+2, M+3]. Uses a simple mass-dependent decay model -
        /// sufficient for cosine-similarity comparison with the observed envelope.
        /// </summary>
        private static double[] TheoreticalIsotopeEnvelope(double precursorMz, int charge)
        {
            // Approximate neutral mass (ignores proton mass precisely - good enough here).
            double mass = precursorMz * charge;

            // Rough averagine ratios anchored to M+0 = 1.0.
            // For a 1500 Da peptide M+1/M+0 ~ 0.7, M+2/M+0 ~ 0.25, M+3/M+0 ~ 0.06.
            // Scale linearly with mass to capture heavier peptides having taller isotopes.
            double r1 = Math.Min(2.0, 0.00045 * mass);           // M+1/M+0
            double r2 = Math.Min(2.0, 0.00015 * mass * mass / 1000.0); // M+2/M+0
            double r3 = Math.Min(1.0, 0.00003 * mass * mass / 1000.0); // M+3/M+0

            double[] env = new double[5];
            env[0] = 0.0;    // M-1
            env[1] = 1.0;    // M+0
            env[2] = r1;
            env[3] = r2;
            env[4] = r3;
            return env;
        }

        /// <summary>
        /// Cosine similarity between two equal-length arrays (sqrt-intensity preprocessing).
        /// </summary>
        private static double CosineSimilarity(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0)
                return 0.0;

            double dot = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                double av = Math.Sqrt(Math.Max(0.0, a[i]));
                double bv = Math.Sqrt(Math.Max(0.0, b[i]));
                dot += av * bv;
                normA += av * av;
                normB += bv * bv;
            }

            double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            if (denom < 1e-12)
                return 0.0;

            return Math.Max(0.0, Math.Min(1.0, dot / denom));
        }

        /// <summary>
        /// Compute Pearson correlation between two intensity arrays over a range.
        /// </summary>
        private double PearsonCorrelation(double[] x, double[] y, int start, int end)
        {
            int n = end - start + 1;
            if (n < 3)
                return double.NaN;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = start; i <= end; i++)
            {
                sumX += x[i];
                sumY += y[i];
                sumXY += x[i] * y[i];
                sumX2 += x[i] * x[i];
                sumY2 += y[i] * y[i];
            }

            double denom = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
            if (denom < 1e-10)
                return 0.0;

            return (n * sumXY - sumX * sumY) / denom;
        }

        /// <summary>
        /// Count consecutive b/y ion matches at the apex spectrum.
        /// </summary>
        private byte CountConsecutiveIons(
            LibraryEntry entry, Spectrum spectrum, OspreyConfig config)
        {
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return 0;

            // Group fragments by ion type and check which ordinals match
            var bMatched = new HashSet<int>();
            var yMatched = new HashSet<int>();

            foreach (var frag in entry.Fragments)
            {
                if (SpectralScorer.HasMatch(frag.Mz, spectrum.Mzs, config.FragmentTolerance))
                {
                    if (frag.Annotation.IonType == IonType.B)
                        bMatched.Add(frag.Annotation.Ordinal);
                    else if (frag.Annotation.IonType == IonType.Y)
                        yMatched.Add(frag.Annotation.Ordinal);
                }
            }

            // Find longest consecutive run
            byte maxConsecutive = 0;
            maxConsecutive = Math.Max(maxConsecutive, LongestConsecutiveRun(bMatched));
            maxConsecutive = Math.Max(maxConsecutive, LongestConsecutiveRun(yMatched));

            return maxConsecutive;
        }

        private byte LongestConsecutiveRun(HashSet<int> ordinals)
        {
            if (ordinals.Count == 0)
                return 0;

            var sorted = ordinals.OrderBy(x => x).ToList();
            byte maxRun = 1;
            byte currentRun = 1;

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == sorted[i - 1] + 1)
                {
                    currentRun++;
                    if (currentRun > maxRun)
                        maxRun = currentRun;
                }
                else
                {
                    currentRun = 1;
                }
            }

            return maxRun;
        }

        /// <summary>
        /// Count top-6 fragment matches at the apex spectrum.
        /// </summary>
        /// <summary>
        /// Count how many of the top 6 library fragments (by intensity) have
        /// matching peaks in the spectrum. Used for the top6_matched feature
        /// value (called once per scored entry, not in the hot prefilter loop).
        /// The prefilter uses HasTopNFragmentMatch instead for speed.
        /// </summary>
        private byte CountTop6Matches(
            LibraryEntry entry, Spectrum spectrum, OspreyConfig config)
        {
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return 0;

            int nTop = Math.Min(entry.Fragments.Count, 6);
            byte matched = 0;

            if (entry.Fragments.Count <= 6)
            {
                for (int i = 0; i < entry.Fragments.Count; i++)
                {
                    if (SpectralScorer.HasMatch(entry.Fragments[i].Mz,
                        spectrum.Mzs, config.FragmentTolerance))
                        matched++;
                }
            }
            else
            {
                // Find top 6 indices by intensity
                var indices = new int[entry.Fragments.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                Array.Sort(indices, (a, b) =>
                    entry.Fragments[b].RelativeIntensity.CompareTo(
                        entry.Fragments[a].RelativeIntensity));

                for (int t = 0; t < nTop; t++)
                {
                    if (SpectralScorer.HasMatch(entry.Fragments[indices[t]].Mz,
                        spectrum.Mzs, config.FragmentTolerance))
                        matched++;
                }
            }
            return matched;
        }

        /// <summary>
        /// Deduplicate scored entries: keep best target and best decoy per base_id.
        /// </summary>
        private List<FdrEntry> DeduplicatePairs(List<FdrEntry> entries)
        {
            // Group by base_id (mask off high bit)
            var groups = new Dictionary<uint, KeyValuePair<FdrEntry, FdrEntry>>();

            foreach (var entry in entries)
            {
                uint baseId = entry.EntryId & 0x7FFFFFFF;
                KeyValuePair<FdrEntry, FdrEntry> existing;
                FdrEntry bestTarget = null;
                FdrEntry bestDecoy = null;

                if (groups.TryGetValue(baseId, out existing))
                {
                    bestTarget = existing.Key;
                    bestDecoy = existing.Value;
                }

                if (entry.IsDecoy)
                {
                    if (bestDecoy == null || entry.CoelutionSum > bestDecoy.CoelutionSum)
                        bestDecoy = entry;
                }
                else
                {
                    if (bestTarget == null || entry.CoelutionSum > bestTarget.CoelutionSum)
                        bestTarget = entry;
                }

                groups[baseId] = new KeyValuePair<FdrEntry, FdrEntry>(bestTarget, bestDecoy);
            }

            var deduped = new List<FdrEntry>(groups.Count * 2);
            foreach (var pair in groups.Values)
            {
                if (pair.Key != null)
                    deduped.Add(pair.Key);
                if (pair.Value != null)
                    deduped.Add(pair.Value);
            }

            int removed = entries.Count - deduped.Count;
            if (removed > 0)
            {
                LogInfo(string.Format("Deduplicated: {0} -> {1} entries ({2} removed)",
                    entries.Count, deduped.Count, removed));
            }

            return deduped;
        }

        #endregion

        #region Stage 5: FDR Control

        /// <summary>
        /// Run FDR control using the configured method.
        /// </summary>
        private void RunFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            switch (config.FdrMethod)
            {
                case FdrMethod.Percolator:
                    RunPercolatorFdr(perFileEntries, fullLibrary, config);
                    break;

                case FdrMethod.Simple:
                    RunSimpleFdr(perFileEntries, config);
                    break;

                default:
                    LogWarning(string.Format(
                        "FDR method {0} not yet supported, falling back to simple",
                        config.FdrMethod));
                    RunSimpleFdr(perFileEntries, config);
                    break;
            }
        }

        /// <summary>
        /// Run Percolator-based FDR control.
        /// Builds PercolatorEntry objects from FdrEntry stubs and runs Percolator.
        /// </summary>
        private void RunPercolatorFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            // Build PercolatorEntry list from all files
            var percEntries = new List<PercolatorEntry>();

            // Build library lookup for feature extraction
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var entry in fullLibrary)
                libraryById[entry.Id] = entry;

            int nWithFeatures = 0;
            int nWithoutFeatures = 0;
            int nInputTargets = 0;
            int nInputDecoys = 0;
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    // Prefer the 21-feature vector computed during coelution scoring.
                    // Fall back to an all-zeros vector only for stub entries (e.g. loaded
                    // from a Parquet cache without features) so the PercolatorEntry is
                    // well-formed.
                    double[] features;
                    if (fdrEntry.Features != null &&
                        fdrEntry.Features.Length == NUM_PIN_FEATURES)
                    {
                        features = fdrEntry.Features;
                        nWithFeatures++;
                    }
                    else
                    {
                        features = BuildBasicFeatures(fdrEntry, libraryById);
                        nWithoutFeatures++;
                    }

                    if (fdrEntry.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    percEntries.Add(new PercolatorEntry
                    {
                        Id = string.Format("{0}_{1}", fileName, fdrEntry.EntryId),
                        FileName = fileName,
                        Peptide = fdrEntry.ModifiedSequence,
                        Charge = fdrEntry.Charge,
                        IsDecoy = fdrEntry.IsDecoy,
                        EntryId = fdrEntry.EntryId,
                        Features = features
                    });
                }
            }

            LogInfo(string.Format(
                "[COUNT] Percolator input: {0} entries ({1} targets, {2} decoys, {3} features)",
                percEntries.Count, nInputTargets, nInputDecoys, NUM_PIN_FEATURES));
            LogInfo(string.Format(
                "[COUNT] Percolator features computed: {0} entries with PIN features, {1} fallback",
                nWithFeatures, nWithoutFeatures));

            LogInfo(string.Format("Running Percolator on {0} entries...", percEntries.Count));

            var percConfig = new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3,
                FeatureNames = ParquetScoreCache.PIN_FEATURE_NAMES
            };

            // Streaming vs direct dispatch, matching Rust
            // osprey/src/pipeline.rs::run_percolator_fdr. Above the
            // MaxTrainSize * 2 threshold the training set is dominated by
            // multi-observation-per-precursor redundancy; best-per-precursor
            // dedup + peptide-grouped subsample give the SVM a diverse
            // per-peptide training pool (same approach mokapot takes) and
            // keep the Stage 5 standardizer fit on the subset -- essential
            // for cross-impl byte parity with Rust once Astral-scale inputs
            // push past the threshold.
            PercolatorResults results;
            if (percConfig.MaxTrainSize > 0 &&
                percEntries.Count > percConfig.MaxTrainSize * 2)
            {
                results = RunPercolatorStreaming(percEntries, percConfig);
            }
            else
            {
                results = PercolatorFdr.RunPercolator(percEntries, percConfig);
            }

            // Map results back to FdrEntry stubs
            var resultMap = new Dictionary<string, PercolatorResult>();
            foreach (var result in results.Entries)
                resultMap[result.Id] = result;

            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var fdrEntry in kvp.Value)
                {
                    string id = string.Format("{0}_{1}", fileName, fdrEntry.EntryId);
                    PercolatorResult result;
                    if (resultMap.TryGetValue(id, out result))
                    {
                        fdrEntry.Score = result.Score;
                        fdrEntry.RunPrecursorQvalue = result.RunPrecursorQvalue;
                        fdrEntry.RunPeptideQvalue = result.RunPeptideQvalue;
                        fdrEntry.ExperimentPrecursorQvalue = result.ExperimentPrecursorQvalue;
                        fdrEntry.ExperimentPeptideQvalue = result.ExperimentPeptideQvalue;
                        fdrEntry.Pep = result.Pep;
                    }
                }
            }

            // Log FDR results
            int nTargetPassing = 0;
            int nDecoyPassing = 0;
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0;
                int fileDecoys = 0;
                foreach (var entry in kvp.Value)
                {
                    if (entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        if (entry.IsDecoy)
                            fileDecoys++;
                        else
                            fileTargets++;
                    }
                }
                LogInfo(string.Format(
                    "[COUNT] Percolator pass [{0}]: {1} targets, {2} decoys at {3:P0} FDR",
                    kvp.Key, fileTargets, fileDecoys, config.RunFdr));
                nTargetPassing += fileTargets;
                nDecoyPassing += fileDecoys;
            }

            LogInfo(string.Format(
                "Percolator results: {0} targets, {1} decoys pass {2:P1} FDR",
                nTargetPassing, nDecoyPassing, config.RunFdr));
            LogInfo(string.Format(
                "[COUNT] First-pass total across files: {0}",
                nTargetPassing));

            // Compute unique precursors across files (best q-value per modseq+charge)
            var bestQByPrecursor = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (entry.IsDecoy)
                        continue;
                    if (entry.EffectiveRunQvalue(config.FdrLevel) > config.RunFdr)
                        continue;
                    string pkey = entry.ModifiedSequence + "|" + entry.Charge;
                    double q = entry.EffectiveRunQvalue(config.FdrLevel);
                    double existing;
                    if (!bestQByPrecursor.TryGetValue(pkey, out existing) || q < existing)
                        bestQByPrecursor[pkey] = q;
                }
            }
            LogInfo(string.Format(
                "[COUNT] First-pass unique precursors (best q across files): {0}",
                bestQByPrecursor.Count));
        }

        /// <summary>
        /// Build a minimal PIN feature vector from an FdrEntry.
        /// Used as a fallback ONLY when <see cref="FdrEntry.Features"/> has not been
        /// populated (e.g. stubs loaded from a Parquet cache). In normal operation the
        /// 21-feature vector is computed during coelution scoring in
        /// <see cref="ScoreCandidate"/> and stored on the entry.
        /// </summary>
        private double[] BuildBasicFeatures(
            FdrEntry entry, Dictionary<uint, LibraryEntry> libraryById)
        {
            double[] features = new double[NUM_PIN_FEATURES];

            // 0: coelution_sum
            features[0] = entry.CoelutionSum;
            // 1: coelution_max (approximate as coelution_sum for basic version)
            features[1] = entry.CoelutionSum * 0.5;
            // 2: n_coeluting_fragments
            features[2] = 3.0;
            // 3: peak_apex
            features[3] = 0.0;
            // 4: peak_area
            features[4] = 0.0;
            // 5: peak_sharpness
            features[5] = 0.0;
            // 6: xcorr
            features[6] = 0.0;
            // 7: consecutive_ions
            features[7] = 0.0;
            // 8: explained_intensity
            features[8] = 0.0;
            // 9: mass_accuracy_mean
            features[9] = 0.0;
            // 10: abs_mass_accuracy_mean
            features[10] = 0.0;
            // 11: rt_deviation
            features[11] = 0.0;
            // 12: abs_rt_deviation
            features[12] = 0.0;
            // 13: ms1_precursor_coelution
            features[13] = 0.0;
            // 14: ms1_isotope_cosine
            features[14] = 0.0;
            // 15: median_polish_cosine
            features[15] = 0.0;
            // 16: median_polish_residual_ratio
            features[16] = 0.0;
            // 17: sg_weighted_xcorr
            features[17] = 0.0;
            // 18: sg_weighted_cosine
            features[18] = 0.0;
            // 19: median_polish_min_fragment_r2
            features[19] = 0.0;
            // 20: median_polish_residual_correlation
            features[20] = 0.0;

            return features;
        }

        /// <summary>
        /// Streaming Percolator dispatch for multi-observation-per-precursor
        /// inputs (total entries above <c>MaxTrainSize * 2</c>). Mirrors
        /// Rust's <c>run_percolator_fdr</c> streaming branch
        /// (osprey/src/pipeline.rs:4232-4580):
        /// <list type="number">
        /// <item>Best-per-precursor dedup across all per-file entries (one
        /// target + one decoy per base_id, by Features[0] = coelution_sum).
        /// </item>
        /// <item>Peptide-grouped subsample to <c>MaxTrainSize</c> using the
        /// same XOR-shift RNG and peptide-key sort order as Rust.</item>
        /// <item>Train fold models + standardizer on that subset
        /// (<c>TrainOnly = true</c>) -- the standardizer is fit on the
        /// subset, matching Rust's run_percolator-on-subset behaviour
        /// instead of fitting on the full 1M+ observation pool.</item>
        /// <item>Apply the averaged model to ALL entries for scoring, then
        /// compute PEP and per-run / experiment q-values on that flat
        /// score array.</item>
        /// </list>
        /// The selection uses <see cref="PercolatorFdr.SelectBestPerPrecursor"/>
        /// and <see cref="PercolatorFdr.SubsampleByPeptideGroup"/> -- the
        /// same helpers the direct path calls internally, so both paths
        /// select identical 300K subsets when given identical input.
        /// </summary>
        private PercolatorResults RunPercolatorStreaming(
            List<PercolatorEntry> percEntries,
            PercolatorConfig percConfig)
        {
            int n = percEntries.Count;
            int maxTrain = percConfig.MaxTrainSize;

            // Pull labels / entry IDs / peptides into flat arrays for the
            // subset helpers.
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            for (int i = 0; i < n; i++)
            {
                labels[i] = percEntries[i].IsDecoy;
                entryIds[i] = percEntries[i].EntryId;
                peptides[i] = percEntries[i].Peptide;
            }

            // 1. Best-per-precursor dedup.
            int[] bestIdx = PercolatorFdr.SelectBestPerPrecursor(labels, entryIds, percEntries);
            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestIdx.Length; i++)
            {
                if (labels[bestIdx[i]]) dedupDecoys++;
                else dedupTargets++;
            }
            LogInfo(string.Format(
                "[COUNT] Percolator streaming best-per-precursor: {0} entries ({1} targets, {2} decoys) from {3} total",
                bestIdx.Length, dedupTargets, dedupDecoys, n));

            // 2. Peptide-grouped subsample if dedup count still exceeds MaxTrainSize.
            int[] trainSubsetGlobalIdx;
            if (maxTrain > 0 && bestIdx.Length > maxTrain)
            {
                var dedupLabels = new bool[bestIdx.Length];
                var dedupEntryIds = new uint[bestIdx.Length];
                var dedupPeptides = new string[bestIdx.Length];
                for (int i = 0; i < bestIdx.Length; i++)
                {
                    int gi = bestIdx[i];
                    dedupLabels[i] = labels[gi];
                    dedupEntryIds[i] = entryIds[gi];
                    dedupPeptides[i] = peptides[gi];
                }
                int[] localSelected = PercolatorFdr.SubsampleByPeptideGroup(
                    dedupLabels, dedupEntryIds, dedupPeptides, maxTrain, percConfig.Seed);
                trainSubsetGlobalIdx = new int[localSelected.Length];
                for (int i = 0; i < localSelected.Length; i++)
                    trainSubsetGlobalIdx[i] = bestIdx[localSelected[i]];
            }
            else
            {
                trainSubsetGlobalIdx = bestIdx;
            }

            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubsetGlobalIdx.Length; i++)
            {
                if (labels[trainSubsetGlobalIdx[i]]) subDecoys++;
                else subTargets++;
            }
            LogInfo(string.Format(
                "[COUNT] Percolator streaming subsample: {0} entries ({1} targets, {2} decoys)",
                trainSubsetGlobalIdx.Length, subTargets, subDecoys));

            // 3. Build subset entry list + train.
            var subsetEntries = new List<PercolatorEntry>(trainSubsetGlobalIdx.Length);
            foreach (int i in trainSubsetGlobalIdx)
                subsetEntries.Add(percEntries[i]);
            var trainConfig = new PercolatorConfig
            {
                TrainFdr = percConfig.TrainFdr,
                TestFdr = percConfig.TestFdr,
                MaxIterations = percConfig.MaxIterations,
                NFolds = percConfig.NFolds,
                Seed = percConfig.Seed,
                CValues = percConfig.CValues,
                MaxTrainSize = percConfig.MaxTrainSize,
                FeatureNames = percConfig.FeatureNames,
                TrainOnly = true
            };
            PercolatorResults trainResults = PercolatorFdr.RunPercolator(subsetEntries, trainConfig);

            // 4. Apply averaged model to ALL entries and compute q-values.
            return PercolatorFdr.ScorePopulationAndComputeFdr(percEntries, trainResults, percConfig);
        }

        /// <summary>
        /// Run simple target-decoy competition FDR (no machine learning).
        /// Uses coelution_sum as the scoring function.
        /// </summary>
        private void RunSimpleFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            OspreyConfig config)
        {
            var fdrController = new FdrController(config.RunFdr);

            foreach (var kvp in perFileEntries)
            {
                var result = fdrController.CompeteAndFilter(
                    kvp.Value,
                    e => e.CoelutionSum,
                    e => e.IsDecoy,
                    e => e.EntryId);

                LogInfo(string.Format(
                    "  {0}: {1} targets pass (FDR={2:F4}, {3} target wins, {4} decoy wins)",
                    kvp.Key, result.PassingTargets.Count, result.FdrAtThreshold,
                    result.NTargetWins, result.NDecoyWins));

                // Assign q-values based on simple competition
                // Passing targets get fdr_at_threshold, non-passing get 1.0
                var passingIds = new HashSet<uint>();
                foreach (var target in result.PassingTargets)
                    passingIds.Add(target.EntryId);

                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy && passingIds.Contains(entry.EntryId))
                    {
                        entry.RunPrecursorQvalue = result.FdrAtThreshold;
                        entry.RunPeptideQvalue = result.FdrAtThreshold;
                        entry.ExperimentPrecursorQvalue = result.FdrAtThreshold;
                        entry.ExperimentPeptideQvalue = result.FdrAtThreshold;
                    }
                }
            }
        }

        #endregion

        #region Stage 8: Protein FDR

        /// <summary>
        /// Run protein-level FDR using parsimony and picked-protein competition.
        /// </summary>
        private void RunProteinFdr(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            // Collect best peptide scores
            var bestScores = ProteinFdr.CollectBestPeptideScores(perFileEntries);
            LogInfo(string.Format("Collected scores for {0} unique peptides", bestScores.Count));

            // Get detected peptide set (targets passing run-level FDR)
            var detectedPeptides = new HashSet<string>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        detectedPeptides.Add(entry.ModifiedSequence);
                    }
                }
            }

            LogInfo(string.Format("Detected {0} unique peptides at {1:P1} FDR",
                detectedPeptides.Count, config.RunFdr));
            LogInfo(string.Format(
                "[COUNT] Detected peptides for protein FDR: {0} unique",
                detectedPeptides.Count));

            // Build protein parsimony
            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedPeptides);

            LogInfo(string.Format("Protein parsimony: {0} groups", parsimony.Groups.Count));
            LogInfo(string.Format(
                "[COUNT] Protein parsimony groups: {0}", parsimony.Groups.Count));

            // Compute protein FDR
            double qvalueGate = config.RunFdr * 2.0; // relaxed gate for protein scoring
            var proteinFdr = ProteinFdr.ComputeProteinFdr(parsimony, bestScores, qvalueGate);

            // Count passing proteins
            int passingProteins = 0;
            foreach (var kvp in proteinFdr.GroupQvalues)
            {
                if (kvp.Value <= config.ProteinFdr.Value)
                    passingProteins++;
            }

            LogInfo(string.Format("{0} protein groups pass {1:P1} protein FDR",
                passingProteins, config.ProteinFdr.Value));
            LogInfo(string.Format(
                "[COUNT] Protein groups passing FDR: {0} at {1:P0}",
                passingProteins, config.ProteinFdr.Value));

            // Propagate protein q-values to FdrEntry stubs
            ProteinFdr.PropagateProteinQvalues(perFileEntries, proteinFdr, true, true);
        }

        #endregion

        #region Stage 9: Blib Output

        /// <summary>
        /// Write passing entries to a BiblioSpec blib file.
        /// </summary>
        private void WriteBlibOutput(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            List<LibraryEntry> fullLibrary,
            Dictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config)
        {
            // Determine effective FDR threshold
            double fdrThreshold = config.RunFdr;

            // Collect passing entries
            var passingEntries = new List<KeyValuePair<string, FdrEntry>>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy &&
                        entry.EffectiveRunQvalue(config.FdrLevel) <= fdrThreshold)
                    {
                        // If protein FDR is enabled, also check protein q-value
                        if (config.ProteinFdr.HasValue &&
                            entry.RunProteinQvalue > config.ProteinFdr.Value)
                        {
                            continue;
                        }

                        passingEntries.Add(
                            new KeyValuePair<string, FdrEntry>(kvp.Key, entry));
                    }
                }
            }

            LogInfo(string.Format("Writing {0} passing entries to blib", passingEntries.Count));

            if (passingEntries.Count == 0)
            {
                LogWarning("No entries pass FDR threshold. Creating empty blib.");
            }

            // Ensure output directory exists
            string outputDir = Path.GetDirectoryName(config.OutputBlib);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // Deduplicate by modified_sequence + charge (keep best q-value)
            var bestByPrecursor = new Dictionary<string, KeyValuePair<string, FdrEntry>>();
            foreach (var kvp in passingEntries)
            {
                string key = kvp.Value.ModifiedSequence + "_" + kvp.Value.Charge;
                KeyValuePair<string, FdrEntry> existing;
                if (!bestByPrecursor.TryGetValue(key, out existing) ||
                    kvp.Value.EffectiveRunQvalue(config.FdrLevel) <
                    existing.Value.EffectiveRunQvalue(config.FdrLevel))
                {
                    bestByPrecursor[key] = kvp;
                }
            }

            LogInfo(string.Format(
                "[COUNT] Best-per-precursor for blib: {0}", bestByPrecursor.Count));

            // Pre-index all per-file target entries by (ModifiedSequence, Charge) for O(1)
            // lookup of cross-file observations. Without this, the inner loop below is
            // O(N_passing * N_total) which is ~70 billion ops for typical experiments.
            var entriesByPrecursor =
                new Dictionary<string, List<KeyValuePair<string, FdrEntry>>>(
                    StringComparer.Ordinal);
            int nCrossFileObservations = 0;
            foreach (var fileKvp in perFileEntries)
            {
                string fn = fileKvp.Key;
                foreach (var fileEntry in fileKvp.Value)
                {
                    if (fileEntry.IsDecoy)
                        continue;
                    string key = fileEntry.ModifiedSequence + "|" + fileEntry.Charge;
                    List<KeyValuePair<string, FdrEntry>> list;
                    if (!entriesByPrecursor.TryGetValue(key, out list))
                    {
                        list = new List<KeyValuePair<string, FdrEntry>>(perFileEntries.Count);
                        entriesByPrecursor[key] = list;
                    }
                    list.Add(new KeyValuePair<string, FdrEntry>(fn, fileEntry));
                    nCrossFileObservations++;
                }
            }

            LogInfo(string.Format(
                "[COUNT] Cross-file observations to write: {0}", nCrossFileObservations));

            using (var writer = new BlibWriter(config.OutputBlib))
            {
                writer.BeginBatch();

                // Pre-create source file IDs once (instead of lazily inside the loop)
                var sourceFileIds = new Dictionary<string, long>();
                foreach (var kvp in perFileEntries)
                {
                    sourceFileIds[kvp.Key] = writer.AddSourceFile(
                        kvp.Key + ".mzML", kvp.Key + ".mzML", fdrThreshold);
                }

                foreach (var kvp in bestByPrecursor.Values)
                {
                    string fileName = kvp.Key;
                    var entry = kvp.Value;

                    LibraryEntry libEntry;
                    if (!libraryById.TryGetValue(entry.EntryId, out libEntry))
                        continue;

                    long fileId = sourceFileIds[fileName];

                    // Extract fragment arrays from library
                    double[] mzs = new double[libEntry.Fragments.Count];
                    float[] intensities = new float[libEntry.Fragments.Count];
                    for (int i = 0; i < libEntry.Fragments.Count; i++)
                    {
                        mzs[i] = libEntry.Fragments[i].Mz;
                        intensities[i] = libEntry.Fragments[i].RelativeIntensity;
                    }

                    double qvalue = entry.EffectiveRunQvalue(config.FdrLevel);

                    long refId = writer.AddSpectrum(
                        libEntry.Sequence,
                        libEntry.ModifiedSequence,
                        libEntry.PrecursorMz,
                        libEntry.Charge,
                        entry.ApexRt,
                        entry.StartRt,
                        entry.EndRt,
                        mzs, intensities,
                        qvalue, fileId, 1, 0.0);

                    // Add modifications
                    if (libEntry.Modifications != null && libEntry.Modifications.Count > 0)
                        writer.AddModifications(refId, libEntry.Modifications);

                    // Add protein mappings
                    if (libEntry.ProteinIds != null && libEntry.ProteinIds.Count > 0)
                        writer.AddProteinMapping(refId, libEntry.ProteinIds);

                    // Add per-file retention times for cross-file observations.
                    // Use the pre-built index for O(1) lookup by (modseq, charge).
                    string lookupKey = entry.ModifiedSequence + "|" + entry.Charge;
                    List<KeyValuePair<string, FdrEntry>> observations;
                    if (entriesByPrecursor.TryGetValue(lookupKey, out observations))
                    {
                        foreach (var obs in observations)
                        {
                            if (obs.Key == fileName)
                                continue;

                            long srcId = sourceFileIds[obs.Key];
                            var fileEntry = obs.Value;
                            bool passesFdr = fileEntry.EffectiveRunQvalue(config.FdrLevel)
                                <= fdrThreshold;

                            writer.AddRetentionTime(
                                refId, srcId,
                                passesFdr ? fileEntry.ApexRt : null,
                                fileEntry.StartRt,
                                fileEntry.EndRt,
                                fileEntry.EffectiveRunQvalue(config.FdrLevel),
                                false);
                        }
                    }
                }

                writer.Commit();

                // Add metadata
                writer.AddMetadata("osprey_version", Program.VERSION_STRING);
                writer.AddMetadata("search_parameter_hash", config.SearchParameterHash());
                writer.AddMetadata("n_passing_precursors", bestByPrecursor.Count.ToString());

                writer.FinalizeDatabase();
            }

            LogInfo(string.Format("Wrote {0} spectra to {1}",
                bestByPrecursor.Count, config.OutputBlib));
        }

        #endregion

        #region Utility Methods

        private static int BinarySearchLowerBound(double[] sortedArray, double value)
        {
            int lo = 0;
            int hi = sortedArray.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (sortedArray[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                if (duration.Hours > 0)
                    return string.Format("{0} days {1} hours", (int)duration.TotalDays, duration.Hours);
                return string.Format("{0} days", (int)duration.TotalDays);
            }
            if (duration.TotalHours >= 1)
            {
                if (duration.Minutes > 0)
                    return string.Format("{0} hours {1} minutes", (int)duration.TotalHours, duration.Minutes);
                return string.Format("{0} hours", (int)duration.TotalHours);
            }
            if (duration.TotalMinutes >= 1)
            {
                if (duration.Seconds > 0)
                    return string.Format("{0} minutes {1} seconds", (int)duration.TotalMinutes, duration.Seconds);
                return string.Format("{0} minutes", (int)duration.TotalMinutes);
            }
            if (duration.TotalSeconds >= 1)
                return string.Format("{0:F3} seconds", duration.TotalSeconds);
            return string.Format("{0} ms", (int)duration.TotalMilliseconds);
        }

        private static void LogInfo(string message)
        {
            Program.LogInfo(message);
        }

        private static void LogWarning(string message)
        {
            Program.LogWarning(message);
        }

        private static void LogError(string message)
        {
            Program.LogError(message);
        }

        #endregion
    }
}
