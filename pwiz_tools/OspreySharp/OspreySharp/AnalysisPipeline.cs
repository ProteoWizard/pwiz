using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

                List<LibraryEntry> decoys;
                if (!config.DecoysInLibrary)
                {
                    decoys = GenerateDecoys(library, config);
                }
                else
                {
                    decoys = new List<LibraryEntry>();
                }
                swLibrary.Stop();
                LogInfo(string.Format("[TIMING] Library loading + decoys: {0:F1}s",
                    swLibrary.Elapsed.TotalSeconds));

                var fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
                fullLibrary.AddRange(library);
                fullLibrary.AddRange(decoys);

                LogInfo(string.Format("Full library: {0} entries ({1} targets + {2} decoys)",
                    fullLibrary.Count, library.Count, decoys.Count));

                // Build library lookup by ID for fast access
                var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
                foreach (var entry in fullLibrary)
                    libraryById[entry.Id] = entry;

                // Stage 2-4: Per-file calibration + coelution scoring
                var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();

                var swAllFiles = Stopwatch.StartNew();
                for (int fileIdx = 0; fileIdx < config.InputFiles.Count; fileIdx++)
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);

                    LogInfo("");
                    LogInfo(string.Format("===== Processing file {0}/{1}: {2} =====",
                        fileIdx + 1, config.InputFiles.Count, inputFile));

                    var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config);
                    if (fileResult != null)
                    {
                        perFileEntries.Add(
                            new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
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
                    totalScored, config.InputFiles.Count));

                if (perFileEntries.Count == 0 || totalScored == 0)
                {
                    LogWarning("No scored entries found. Cannot perform FDR control.");
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
        /// Load spectral library from the configured source.
        /// Supports DIA-NN TSV, blib, and elib formats.
        /// </summary>
        private List<LibraryEntry> LoadLibrary(OspreyConfig config)
        {
            string path = config.LibrarySource.Path;
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
            return entries;
        }

        /// <summary>
        /// Generate decoy entries from the target library.
        /// </summary>
        private List<LibraryEntry> GenerateDecoys(List<LibraryEntry> targets, OspreyConfig config)
        {
            LogInfo(string.Format("Generating decoys using {0} method...", config.DecoyMethod));

            var generator = new DecoyGenerator();
            var decoys = new List<LibraryEntry>(targets.Count);

            foreach (var target in targets)
            {
                if (target.IsDecoy)
                    continue;
                if (target.Fragments == null || target.Fragments.Count == 0)
                    continue;

                var decoy = generator.Generate(target);
                decoys.Add(decoy);
            }

            LogInfo(string.Format("Generated {0} decoys from {1} targets",
                decoys.Count, targets.Count));
            return decoys;
        }

        #endregion

        #region Stage 2-4: Per-File Processing

        /// <summary>
        /// Process a single mzML file: load spectra, calibrate RT, score coelution.
        /// </summary>
        private List<FdrEntry> ProcessFile(
            string inputFile, string fileName,
            List<LibraryEntry> fullLibrary, OspreyConfig config)
        {
            // Load spectra (from mzML or .spectra.bin cache)
            List<Spectrum> spectra;
            List<MS1Spectrum> ms1Spectra;
            var swParse = Stopwatch.StartNew();
            LoadSpectra(inputFile, out spectra, out ms1Spectra);
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

            // Extract isolation windows from spectra
            var isolationWindows = ExtractIsolationWindows(spectra);
            LogInfo(string.Format("Found {0} unique isolation windows", isolationWindows.Count));

            // RT calibration
            RTCalibration rtCalibration = null;
            if (config.RtCalibration.Enabled)
            {
                var swCal = Stopwatch.StartNew();
                rtCalibration = RunCalibration(
                    fullLibrary, spectra, ms1Spectra, config);
                swCal.Stop();
                int nPoints = rtCalibration != null ? rtCalibration.Stats().NPoints : 0;
                LogInfo(string.Format(
                    "[TIMING] RT calibration: {0:F1}s ({1} calibration points)",
                    swCal.Elapsed.TotalSeconds, nPoints));
            }

            // Run coelution scoring across all isolation windows
            var swScoring = Stopwatch.StartNew();
            var scoredEntries = RunCoelutionScoring(
                fullLibrary, spectra, ms1Spectra,
                isolationWindows, rtCalibration, config);
            swScoring.Stop();
            double scoringSeconds = swScoring.Elapsed.TotalSeconds;
            double ratePerSec = scoringSeconds > 0.001
                ? scoredEntries.Count / scoringSeconds
                : 0.0;
            LogInfo(string.Format(
                "[TIMING] Coelution scoring: {0:F1}s ({1} candidates, {2:F0} cand/s)",
                scoringSeconds, scoredEntries.Count, ratePerSec));

            LogInfo(string.Format("Scored {0} entries ({1} targets, {2} decoys) for {3}",
                scoredEntries.Count,
                scoredEntries.Count(e => !e.IsDecoy),
                scoredEntries.Count(e => e.IsDecoy),
                fileName));

            // Deduplicate: keep best target and best decoy per base_id
            scoredEntries = DeduplicatePairs(scoredEntries);

            return scoredEntries;
        }

        /// <summary>
        /// Load spectra from mzML file or spectra cache.
        /// </summary>
        private void LoadSpectra(string inputFile,
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

            // Parse mzML directly
            LogInfo(string.Format("Parsing mzML: {0}", inputFile));
            var mzmlResult = MzmlReader.LoadAllSpectra(inputFile);
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
        private const int MIN_COELUTION_SPECTRA = 5;
        private const double CAL_FDR_THRESHOLD = 0.01;

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
            OspreyConfig config)
        {
            LogInfo("Running RT calibration...");

            // Calculate library and mzML RT ranges
            double libMinRt = double.MaxValue, libMaxRt = double.MinValue;
            double mzmlMinRt = double.MaxValue, mzmlMaxRt = double.MinValue;

            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    if (entry.RetentionTime < libMinRt) libMinRt = entry.RetentionTime;
                    if (entry.RetentionTime > libMaxRt) libMaxRt = entry.RetentionTime;
                }
            }

            foreach (var spectrum in spectra)
            {
                if (spectrum.RetentionTime < mzmlMinRt) mzmlMinRt = spectrum.RetentionTime;
                if (spectrum.RetentionTime > mzmlMaxRt) mzmlMaxRt = spectrum.RetentionTime;
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

            // Sample library entries (paired target+decoy, stratified on RT).
            var swSample = Stopwatch.StartNew();
            var sampledEntries = SampleLibraryForCalibration(
                library, config.RtCalibration.CalibrationSampleSize);
            swSample.Stop();
            int nSampledTargets = 0;
            int nSampledDecoys = 0;
            foreach (var e in sampledEntries)
            {
                if (e.IsDecoy) nSampledDecoys++;
                else nSampledTargets++;
            }
            LogInfo(string.Format(
                "[TIMING] Calibration sampling: {0:F2}s ({1} targets + {2} decoys)",
                swSample.Elapsed.TotalSeconds, nSampledTargets, nSampledDecoys));

            if (nSampledTargets == 0)
            {
                LogWarning("No target entries available for calibration sampling.");
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

            // Parallel score each sampled entry.
            var swScoring = Stopwatch.StartNew();
            var matches = new ConcurrentBag<CalibrationMatch>();
            var snrByEntryId = new ConcurrentDictionary<uint, double>();
            var matchRts = new ConcurrentDictionary<uint, KeyValuePair<double, double>>();

            Parallel.ForEach(sampledEntries, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            () => new SpectralScorer(),
            (entry, loopState, localScorer) =>
            {
                double entrySnr;
                double entryLibRt;
                double entryMeasuredRt;
                var match = ScoreCalibrationEntry(
                    entry, spectraByWindowKey, config,
                    rtSlope, rtIntercept, initialTolerance, localScorer,
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
                "[TIMING] Calibration scoring: {0:F2}s ({1} matches)",
                swScoring.Elapsed.TotalSeconds, matches.Count));

            if (matches.Count == 0)
            {
                LogWarning("No calibration matches could be scored.");
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
                if (cmp != 0) return cmp;
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
                    if (m.IsDecoy) nDecoyWins++;
                    else nTargetWins++;
                }
            }
            LogInfo(string.Format(
                "[TIMING] Calibration LDA: {0:F2}s ({1} target wins, {2} decoy wins at 1% FDR)",
                swLda.Elapsed.TotalSeconds, nTargetWins, nDecoyWins));
            LogInfo(string.Format(
                "Calibration LDA passing count: {0} (returned by TrainAndScoreCalibration)",
                nPassing));

            // Collect high-confidence target matches that also meet the S/N quality gate.
            var libRtsDetected = new List<double>();
            var measuredRtsDetected = new List<double>();
            int nSnrFiltered = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy) continue;
                if (m.QValue > CAL_FDR_THRESHOLD) continue;

                KeyValuePair<double, double> rtPair;
                if (!matchRts.TryGetValue(m.EntryId, out rtPair)) continue;

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
                    "  RT quality filter: {0} -> {1} peptides (removed {2} with S/N < {3:F1})",
                    nTargetWins, libRtsDetected.Count, nSnrFiltered, MIN_SNR_FOR_RT_CAL));
            }

            LogInfo(string.Format("Found {0} calibration points", libRtsDetected.Count));

            if (libRtsDetected.Count < config.RtCalibration.MinCalibrationPoints)
            {
                LogWarning(string.Format(
                    "Insufficient calibration points ({0} < {1}). Using fallback tolerance.",
                    libRtsDetected.Count, config.RtCalibration.MinCalibrationPoints));
                return null;
            }

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
                    OutlierRetention = 1.0 // LDA + S/N already filtered
                };
                var calibrator = new RTCalibrator(calibratorConfig);
                var rtCal = calibrator.Fit(libRts, measuredRts);
                swLoess.Stop();

                var stats = rtCal.Stats();
                LogInfo(string.Format("[TIMING] Calibration LOESS fit: {0:F2}s",
                    swLoess.Elapsed.TotalSeconds));
                LogInfo(string.Format(
                    "RT calibration: {0} points, R2={1:F4}, residual SD={2:F3} min",
                    stats.NPoints, stats.RSquared, stats.ResidualSD));

                return rtCal;
            }
            catch (Exception ex)
            {
                swLoess.Stop();
                LogWarning(string.Format("RT calibration failed: {0}. Using fallback tolerance.",
                    ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Sample library entries for calibration discovery, keeping paired target-decoy
        /// pairs together (matched by base_id = entry_id &amp; 0x7FFFFFFF).
        /// Uses RT stratification through <see cref="RTStratifiedSampler"/> so the
        /// sample is representative across the full gradient.
        /// </summary>
        private static List<LibraryEntry> SampleLibraryForCalibration(
            List<LibraryEntry> library, int sampleSize)
        {
            // Build target list (eligible for scoring) and decoy lookup by base_id.
            var targets = new List<LibraryEntry>();
            var decoyByBaseId = new Dictionary<uint, LibraryEntry>();

            foreach (var entry in library)
            {
                if (entry.Fragments == null || entry.Fragments.Count < 2)
                    continue;
                if (entry.IsDecoy)
                    decoyByBaseId[entry.Id & 0x7FFFFFFF] = entry;
                else
                    targets.Add(entry);
            }

            if (targets.Count == 0)
                return new List<LibraryEntry>();

            // If requested sample size is 0 or >= targets, use all target+decoy entries.
            bool useAll = sampleSize <= 0 || targets.Count <= sampleSize;

            List<LibraryEntry> selectedTargets;
            if (useAll)
            {
                selectedTargets = targets;
            }
            else
            {
                // Stratified sample by RT so calibration points cover the full gradient.
                var sampler = new RTStratifiedSampler
                {
                    NBins = 20,
                    PeptidesPerBin = Math.Max(1, sampleSize / 20)
                };
                double[] rts = new double[targets.Count];
                for (int i = 0; i < targets.Count; i++)
                    rts[i] = targets[i].RetentionTime;
                int[] sampledIdx = sampler.Sample(rts);
                selectedTargets = new List<LibraryEntry>(sampledIdx.Length);
                foreach (int idx in sampledIdx)
                    selectedTargets.Add(targets[idx]);
            }

            // Emit selected targets together with their paired decoys.
            var result = new List<LibraryEntry>(selectedTargets.Count * 2);
            foreach (var t in selectedTargets)
            {
                result.Add(t);
                LibraryEntry decoy;
                if (decoyByBaseId.TryGetValue(t.Id & 0x7FFFFFFF, out decoy))
                    result.Add(decoy);
            }
            return result;
        }

        /// <summary>
        /// Score a single library entry for calibration: extract fragment XICs across
        /// spectra in the entry's isolation window that fall within the initial RT
        /// tolerance, detect the best co-eluting peak, and compute the four LDA
        /// features at the apex (correlation, LibCosine, top-6 matched, XCorr).
        /// Returns null if the entry has no viable peak.
        /// </summary>
        private CalibrationMatch ScoreCalibrationEntry(
            LibraryEntry entry,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            OspreyConfig config,
            double rtSlope, double rtIntercept, double initialTolerance,
            SpectralScorer scorer,
            out double signalToNoise,
            out double libraryRt,
            out double measuredRt)
        {
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

            // Filter by RT tolerance and top-6 fragment prefilter.
            double expectedRt = entry.RetentionTime * rtSlope + rtIntercept;
            var candidateSpectra = new List<Spectrum>();
            foreach (var spec in windowSpectra)
            {
                if (Math.Abs(spec.RetentionTime - expectedRt) > initialTolerance)
                    continue;
                if (CountTop6Matches(entry, spec, config) < 2)
                    continue;
                candidateSpectra.Add(spec);
            }

            if (candidateSpectra.Count < MIN_COELUTION_SPECTRA)
                return null;

            // Build shared RT axis for XIC extraction.
            int nScans = candidateSpectra.Count;
            double[] rts = new double[nScans];
            for (int i = 0; i < nScans; i++)
                rts[i] = candidateSpectra[i].RetentionTime;

            // Extract XICs for the top-N most intense library fragments.
            var xics = ExtractTopNFragmentXics(
                entry, candidateSpectra, rts, CAL_TOP_N_FRAGMENTS, config);
            if (xics.Count < 2)
                return null;

            // Detect consensus CWT peaks and score by pairwise correlation sum.
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);
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

                if (corrSum > bestCorrSum)
                {
                    bestCorrSum = corrSum;
                    bestPeak = peak;
                }
            }

            if (bestPeak == null || bestCorrSum < MIN_COELUTION_CORR_SCORE)
                return null;

            // Determine the apex within the best peak: the scan with the highest
            // summed fragment intensity that also matches at least 2 of the top-6
            // library fragments.
            int apexLocalIdx = -1;
            double apexSum = -1.0;
            for (int scan = bestPeak.StartIndex; scan <= bestPeak.EndIndex; scan++)
            {
                double sumInt = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double[] inten = xics[f].Intensities;
                    if (scan < inten.Length)
                        sumInt += inten[scan];
                }

                if (sumInt > apexSum)
                {
                    // Require >= 2 of the top-6 library fragments to be matched at this scan.
                    if (CountTop6Matches(entry, candidateSpectra[scan], config) >= 2)
                    {
                        apexSum = sumInt;
                        apexLocalIdx = scan;
                    }
                }
            }

            if (apexLocalIdx < 0)
                return null;

            var apexSpectrum = candidateSpectra[apexLocalIdx];
            measuredRt = apexSpectrum.RetentionTime;

            // S/N on the composite summed XIC within the peak boundaries.
            double[] compositeXic = new double[nScans];
            for (int scan = 0; scan < nScans; scan++)
            {
                double s = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double[] inten = xics[f].Intensities;
                    if (scan < inten.Length)
                        s += inten[scan];
                }
                compositeXic[scan] = s;
            }
            signalToNoise = PeakDetector.ComputeSnr(
                compositeXic, apexLocalIdx, bestPeak.StartIndex, bestPeak.EndIndex);

            // Compute the four LDA features at the apex.
            double libCosineApex = scorer.LibCosine(
                apexSpectrum, entry, config.FragmentTolerance);
            double xcorrApex = ComputeXcorrForSpectrum(apexSpectrum, entry, scorer);
            byte top6Matched = CountTop6Matches(entry, apexSpectrum, config);

            return new CalibrationMatch
            {
                EntryId = entry.Id,
                IsDecoy = entry.IsDecoy,
                Sequence = entry.Sequence,
                CorrelationScore = bestCorrSum,
                LibcosineApex = libCosineApex,
                Top6MatchedApex = top6Matched,
                XcorrScore = xcorrApex,
                IsotopeCosine = 0.0,
                DiscriminantScore = bestCorrSum,
                QValue = 1.0
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
                bool anyNonZero = false;

                for (int scanIdx = 0; scanIdx < nScans; scanIdx++)
                {
                    var spectrum = candidateSpectra[scanIdx];
                    if (spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                        continue;

                    int lo = BinarySearchLowerBound(spectrum.Mzs, lower);
                    double bestIntensity = 0.0;
                    for (int k = lo; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                    {
                        double intensity = spectrum.Intensities[k];
                        if (intensity > bestIntensity)
                            bestIntensity = intensity;
                    }
                    intensities[scanIdx] = bestIntensity;
                    if (bestIntensity > 0.0)
                        anyNonZero = true;
                }

                if (anyNonZero)
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
            OspreyConfig config)
        {
            var allEntries = new List<FdrEntry>();
            var scorer = new SpectralScorer();
            int windowsProcessed = 0;

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

            // Determine RT tolerance
            double fallbackTolerance = config.RtCalibration.FallbackRtTolerance;

            // Per-window timings collected thread-safely for post-summary.
            var windowTimings = new ConcurrentBag<WindowTiming>();

            // Process each isolation window (parallelizable)
            object lockObj = new object();

            Parallel.ForEach(isolationWindows, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            window =>
            {
                var swWindow = Stopwatch.StartNew();
                var windowEntries = ScoreWindow(
                    window, fullLibrary, spectraByWindowKey, ms1Spectra,
                    rtCalibration, fallbackTolerance, scorer, config);
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
                    if (windowsProcessed % 10 == 0 || windowsProcessed == isolationWindows.Count)
                    {
                        LogInfo(string.Format("  Scored {0}/{1} isolation windows ({2} entries so far)",
                            windowsProcessed, isolationWindows.Count, allEntries.Count));
                    }
                }
            });

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
            double fallbackTolerance,
            SpectralScorer scorer,
            OspreyConfig config)
        {
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

            // Find candidate library entries whose precursor m/z falls in this window
            var candidates = new List<LibraryEntry>();
            foreach (var entry in fullLibrary)
            {
                if (entry.Fragments == null || entry.Fragments.Count < 3)
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

            // Score each candidate
            foreach (var candidate in candidates)
            {
                var fdrEntry = ScoreCandidate(
                    candidate, windowSpectra, windowRts,
                    ms1Spectra, rtCalibration, fallbackTolerance,
                    scorer, config);

                if (fdrEntry != null)
                    entries.Add(fdrEntry);
            }

            return entries;
        }

        /// <summary>
        /// Score a single library entry candidate against spectra in its isolation window.
        /// Extracts fragment XICs, detects CWT peaks, and scores at the best apex.
        /// </summary>
        private FdrEntry ScoreCandidate(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            List<MS1Spectrum> ms1Spectra,
            RTCalibration rtCalibration,
            double fallbackTolerance,
            SpectralScorer scorer,
            OspreyConfig config)
        {
            int nScans = windowSpectra.Count;
            if (nScans < 5)
                return null;

            // Determine RT search window
            double expectedRt;
            double rtTolerance;

            if (rtCalibration != null)
            {
                expectedRt = rtCalibration.Predict(candidate.RetentionTime);
                rtTolerance = rtCalibration.LocalTolerance(
                    candidate.RetentionTime,
                    config.RtCalibration.RtToleranceFactor,
                    config.RtCalibration.MinRtTolerance);
            }
            else
            {
                expectedRt = candidate.RetentionTime;
                rtTolerance = fallbackTolerance;
            }

            // Find scan range within RT tolerance
            int startScan = -1, endScan = -1;
            for (int i = 0; i < nScans; i++)
            {
                if (windowRts[i] >= expectedRt - rtTolerance)
                {
                    if (startScan < 0) startScan = i;
                    endScan = i;
                }
                if (windowRts[i] > expectedRt + rtTolerance)
                    break;
            }

            if (startScan < 0 || endScan < 0 || endScan - startScan + 1 < 5)
                return null;

            int rangeLen = endScan - startScan + 1;

            // Extract fragment XICs within the RT range
            var xics = ExtractFragmentXics(
                candidate, windowSpectra, windowRts, startScan, endScan, config);

            if (xics.Count < 2)
                return null;

            // Detect consensus CWT peaks
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);
            if (peaks.Count == 0)
                return null;

            // Use the best (highest consensus) peak
            var bestPeak = peaks[0];
            int apexGlobalIdx = startScan + bestPeak.ApexIndex;

            // Score at the apex spectrum
            if (apexGlobalIdx < 0 || apexGlobalIdx >= windowSpectra.Count)
                return null;

            var apexSpectrum = windowSpectra[apexGlobalIdx];

            // LibCosine at apex
            double libCosine = scorer.LibCosine(apexSpectrum, candidate, config.FragmentTolerance);

            // XCorr at apex (using unit-resolution binning)
            double xcorr = ComputeXcorrForSpectrum(apexSpectrum, candidate, scorer);

            // Compute pairwise coelution features (sum, min, max, n_positive)
            double coelutionSum, coelutionMin, coelutionMax;
            int nCoelutingFragments;
            ComputeCoelutionStats(xics, bestPeak,
                out coelutionSum, out coelutionMin, out coelutionMax,
                out nCoelutingFragments);

            // Peak shape features: apex, area, sharpness
            double peakApex, peakArea, peakSharpness;
            ComputePeakShapeFeatures(xics, bestPeak,
                out peakApex, out peakArea, out peakSharpness);

            // RT deviation (absolute even if calibration disabled — measured vs library RT)
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

            // MS1 features: precursor coelution, isotope cosine
            double ms1PrecursorCoelution = 0.0;
            double ms1IsotopeCosine = 0.0;
            if (ms1Spectra != null && ms1Spectra.Count > 0)
            {
                ComputeMs1Features(
                    candidate, xics, bestPeak,
                    windowRts, startScan,
                    ms1Spectra, config,
                    out ms1PrecursorCoelution, out ms1IsotopeCosine);
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
            // Proxies for features we don't compute in this first pass
            features[15] = libCosine;       // median_polish_cosine <- dot_product proxy
            features[16] = 0.0;             // median_polish_residual_ratio
            features[17] = xcorr;           // sg_weighted_xcorr <- xcorr proxy
            features[18] = libCosine;       // sg_weighted_cosine <- libcosine proxy
            features[19] = coelutionMin;    // median_polish_min_fragment_r2 <- coelution_min proxy
            features[20] = 0.0;             // median_polish_residual_correlation

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
        /// Extract fragment XICs for a candidate across the scan range.
        /// </summary>
        private List<XicData> ExtractFragmentXics(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            int startScan, int endScan,
            OspreyConfig config)
        {
            int rangeLen = endScan - startScan + 1;
            var xics = new List<XicData>();

            // Build shared RT array for this range
            double[] rangeRts = new double[rangeLen];
            for (int i = 0; i < rangeLen; i++)
                rangeRts[i] = windowRts[startScan + i];

            // Extract XIC for each fragment
            for (int fragIdx = 0; fragIdx < candidate.Fragments.Count; fragIdx++)
            {
                var fragment = candidate.Fragments[fragIdx];
                double[] intensities = new double[rangeLen];

                for (int scanIdx = 0; scanIdx < rangeLen; scanIdx++)
                {
                    var spectrum = windowSpectra[startScan + scanIdx];
                    double bestIntensity = 0.0;

                    // Binary search for fragment m/z in the spectrum
                    if (spectrum.Mzs != null && spectrum.Mzs.Length > 0)
                    {
                        double tolDa = config.FragmentTolerance.ToleranceDa(fragment.Mz);
                        double lower = fragment.Mz - tolDa;
                        double upper = fragment.Mz + tolDa;

                        int lo = BinarySearchLowerBound(spectrum.Mzs, lower);
                        for (int k = lo; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                        {
                            double intensity = spectrum.Intensities[k];
                            if (intensity > bestIntensity)
                                bestIntensity = intensity;
                        }
                    }

                    intensities[scanIdx] = bestIntensity;
                }

                xics.Add(new XicData(fragIdx, rangeRts, intensities));
            }

            return xics;
        }

        /// <summary>
        /// Compute XCorr for a spectrum against a library entry.
        /// </summary>
        private double ComputeXcorrForSpectrum(
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer)
        {
            var binConfig = scorer.BinConfig;
            if (binConfig.NBins <= 0)
                return 0.0;

            double[] observedBins = new double[binConfig.NBins];
            double[] libraryBins = new double[binConfig.NBins];

            // Bin observed spectrum
            if (spectrum.Mzs != null)
            {
                for (int i = 0; i < spectrum.Mzs.Length; i++)
                {
                    int bin = binConfig.MzToBin(spectrum.Mzs[i]);
                    if (bin >= 0 && bin < observedBins.Length)
                    {
                        double sqrtInt = Math.Sqrt(spectrum.Intensities[i]);
                        if (sqrtInt > observedBins[bin])
                            observedBins[bin] = sqrtInt;
                    }
                }
            }

            // Bin library fragments
            foreach (var frag in entry.Fragments)
            {
                int bin = binConfig.MzToBin(frag.Mz);
                if (bin >= 0 && bin < libraryBins.Length)
                {
                    double sqrtInt = Math.Sqrt(frag.RelativeIntensity);
                    if (sqrtInt > libraryBins[bin])
                        libraryBins[bin] = sqrtInt;
                }
            }

            return scorer.XCorr(observedBins, libraryBins);
        }

        /// <summary>
        /// Compute coelution sum/min/max and count of positively-correlated fragments
        /// from pairwise fragment correlations.
        /// </summary>
        private void ComputeCoelutionStats(
            List<XicData> xics, XICPeakBounds peak,
            out double sum, out double min, out double max, out int nCoeluting)
        {
            sum = 0.0;
            min = 0.0;
            max = 0.0;
            nCoeluting = 0;

            if (xics.Count < 2)
                return;

            // Per-fragment best positive correlation — a fragment is "coeluting" if any
            // of its pairwise correlations is > 0.
            bool[] anyPositive = new bool[xics.Count];
            bool haveAny = false;
            double minCorr = double.PositiveInfinity;
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
                    if (corr < minCorr) minCorr = corr;
                    if (corr > maxCorr) maxCorr = corr;
                    haveAny = true;

                    if (corr > 0.0)
                    {
                        anyPositive[i] = true;
                        anyPositive[j] = true;
                    }
                }
            }

            if (haveAny)
            {
                min = minCorr;
                max = maxCorr;
            }

            for (int i = 0; i < anyPositive.Length; i++)
            {
                if (anyPositive[i])
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

            // Sum across fragments per scan to get the composite XIC.
            double apexSum = 0.0;
            double area = 0.0;
            for (int scan = start; scan <= end; scan++)
            {
                double s = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double[] inten = xics[f].Intensities;
                    if (scan < inten.Length)
                        s += inten[scan];
                }
                area += s;
                if (scan == apex)
                    apexSum = s;
            }

            peakApex = apexSum;
            peakArea = area;

            // Sharpness: apex divided by mean of the two edge intensities.
            double leftSum = 0.0, rightSum = 0.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double[] inten = xics[f].Intensities;
                if (start < inten.Length) leftSum += inten[start];
                if (end < inten.Length) rightSum += inten[end];
            }
            double edgeMean = (leftSum + rightSum) * 0.5;
            if (edgeMean > 1e-12)
                peakSharpness = apexSum / edgeMean;
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
                double bestIntensity = 0.0;
                double bestMz = 0.0;
                bool found = false;

                for (int k = lo; k < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[k] <= upper; k++)
                {
                    double intensity = apexSpectrum.Intensities[k];
                    if (intensity > bestIntensity)
                    {
                        bestIntensity = intensity;
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
        }

        /// <summary>
        /// Compute MS1 features: correlation between the summed fragment XIC and the
        /// precursor MS1 XIC, and cosine similarity between the observed isotope envelope
        /// at the apex MS1 scan and the theoretical averagine envelope.
        /// </summary>
        private void ComputeMs1Features(
            LibraryEntry candidate,
            List<XicData> xics,
            XICPeakBounds peak,
            double[] windowRts, int startScan,
            List<MS1Spectrum> ms1Spectra,
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

            // MS1 tolerance (use precursor tolerance from config if provided).
            double precursorTolPpm = config.PrecursorTolerance != null &&
                                     config.PrecursorTolerance.Unit == ToleranceUnit.Ppm
                ? config.PrecursorTolerance.Tolerance
                : 20.0;

            // Summed fragment XIC over the peak window and matching MS1 precursor XIC
            // sampled at the nearest MS1 scan for each MS2 RT. peak.Start/End/ApexIndex are
            // relative to the XIC range (which starts at startScan in windowRts).
            int n = end - start + 1;
            double[] fragSum = new double[n];
            double[] ms1Xic = new double[n];

            for (int i = 0; i < n; i++)
            {
                int xicIdx = start + i;
                double rt = windowRts[startScan + xicIdx];

                double s = 0.0;
                for (int f = 0; f < xics.Count; f++)
                {
                    double[] inten = xics[f].Intensities;
                    if (xicIdx < inten.Length)
                        s += inten[xicIdx];
                }
                fragSum[i] = s;

                var ms1 = FindNearestMs1(ms1Spectra, rt);
                if (ms1 != null)
                {
                    var peakInfo = ms1.FindPeakPpm(candidate.PrecursorMz, precursorTolPpm);
                    if (peakInfo.HasValue)
                        ms1Xic[i] = peakInfo.Value.Intensity;
                }
            }

            ms1PrecursorCoelution = PearsonCorrelation(fragSum, ms1Xic, 0, n - 1);
            if (double.IsNaN(ms1PrecursorCoelution))
                ms1PrecursorCoelution = 0.0;

            // Isotope cosine at apex MS1 scan
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));
            double apexRt = windowRts[startScan + apex];
            var apexMs1 = FindNearestMs1(ms1Spectra, apexRt);
            if (apexMs1 != null)
            {
                int charge = candidate.Charge > 0 ? candidate.Charge : 1;
                var envelope = IsotopeEnvelope.Extract(
                    apexMs1, candidate.PrecursorMz, charge, precursorTolPpm);

                // Theoretical envelope approximation (averagine-like): [M-1, M, M+1, M+2, M+3]
                // We use a simple decaying envelope scaled by precursor mass.
                double[] theoretical = TheoreticalIsotopeEnvelope(
                    candidate.PrecursorMz, charge);

                ms1IsotopeCosine = CosineSimilarity(envelope.Intensities, theoretical);
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
        /// [M-1, M+0, M+1, M+2, M+3]. Uses a simple mass-dependent decay model —
        /// sufficient for cosine-similarity comparison with the observed envelope.
        /// </summary>
        private static double[] TheoreticalIsotopeEnvelope(double precursorMz, int charge)
        {
            // Approximate neutral mass (ignores proton mass precisely — good enough here).
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
        private byte CountTop6Matches(
            LibraryEntry entry, Spectrum spectrum, OspreyConfig config)
        {
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return 0;

            // Sort fragments by intensity descending, take top 6
            var sortedFrags = entry.Fragments
                .OrderByDescending(f => f.RelativeIntensity)
                .Take(6)
                .ToList();

            byte matched = 0;
            foreach (var frag in sortedFrags)
            {
                if (SpectralScorer.HasMatch(frag.Mz, spectrum.Mzs, config.FragmentTolerance))
                    matched++;
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
                "Percolator feature source: {0} entries with computed PIN features, {1} fallback",
                nWithFeatures, nWithoutFeatures));

            LogInfo(string.Format("Running Percolator on {0} entries...", percEntries.Count));

            var percConfig = new PercolatorConfig
            {
                TrainFdr = config.RunFdr,
                TestFdr = config.RunFdr,
                MaxIterations = 10,
                NFolds = 3
            };

            var results = PercolatorFdr.RunPercolator(percEntries, percConfig);

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
                foreach (var entry in kvp.Value)
                {
                    if (entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    {
                        if (entry.IsDecoy)
                            nDecoyPassing++;
                        else
                            nTargetPassing++;
                    }
                }
            }

            LogInfo(string.Format(
                "Percolator results: {0} targets, {1} decoys pass {2:P1} FDR",
                nTargetPassing, nDecoyPassing, config.RunFdr));
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

            // Build protein parsimony
            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedPeptides);

            LogInfo(string.Format("Protein parsimony: {0} groups", parsimony.Groups.Count));

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

            // Pre-index all per-file target entries by (ModifiedSequence, Charge) for O(1)
            // lookup of cross-file observations. Without this, the inner loop below is
            // O(N_passing * N_total) which is ~70 billion ops for typical experiments.
            var entriesByPrecursor =
                new Dictionary<string, List<KeyValuePair<string, FdrEntry>>>(
                    StringComparer.Ordinal);
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
                }
            }

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
                                passesFdr ? (double?)fileEntry.ApexRt : null,
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
