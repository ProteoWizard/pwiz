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

        // Diagnostic: per-entry calibration window dump (one row per scored entry).
        // Set during RunCalibration when OSPREY_DUMP_CAL_WINDOWS=1, then read by
        // ScoreCalibrationEntry running in parallel. Cleared and written at the end
        // of RunCalibration.
        private static System.Collections.Concurrent.ConcurrentBag<string> s_calWindowDump;

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
                    if (!entry.IsDecoy) nLibraryTargets++;
                }
                LogInfo(string.Format("[COUNT] Library targets loaded: {0}", nLibraryTargets));

                List<LibraryEntry> decoys;
                if (!config.DecoysInLibrary)
                {
                    // Collision detection can exclude targets whose decoys collide
                    // with other target sequences. Replace `library` with only the
                    // targets that produced valid decoys, matching Rust's
                    // library = valid_targets; library.extend(decoys) pattern.
                    List<LibraryEntry> validTargets;
                    decoys = GenerateDecoys(library, config, out validTargets);
                    library = validTargets;
                }
                else
                {
                    decoys = new List<LibraryEntry>();
                }
                swLibrary.Stop();
                LogInfo(string.Format("[TIMING] Library loading + decoys: {0:F1}s",
                    swLibrary.Elapsed.TotalSeconds));

                LogInfo(string.Format("[COUNT] Library decoys generated: {0}", decoys.Count));

                var fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
                fullLibrary.AddRange(library);
                fullLibrary.AddRange(decoys);

                LogInfo(string.Format("Full library: {0} entries ({1} targets + {2} decoys)",
                    fullLibrary.Count, library.Count, decoys.Count));
                LogInfo(string.Format("[COUNT] Full library: {0} ({1} targets + {2} decoys)",
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
                if (!t.IsDecoy) targetSequences.Add(t.Sequence);
            }

            var generator = new DecoyGenerator();
            validTargets = new List<LibraryEntry>(targets.Count);
            var decoys = new List<LibraryEntry>(targets.Count);
            int nReversed = 0, nCycled = 0, nExcluded = 0, nSkipped = 0;

            foreach (var target in targets)
            {
                if (target.IsDecoy)
                    continue;
                if (target.Fragments == null || target.Fragments.Count == 0)
                {
                    nSkipped++;
                    continue;
                }

                // Try reversal first
                int[] mapping;
                string reversedSeq = generator.ReverseSequence(target.Sequence, out mapping);

                bool foundValid = false;
                LibraryEntry decoy = null;

                if (reversedSeq != target.Sequence && !targetSequences.Contains(reversedSeq))
                {
                    decoy = BuildDecoyFromSequence(target, reversedSeq, mapping);
                    if (decoy != null)
                    {
                        nReversed++;
                        foundValid = true;
                    }
                }

                // Fallback: cycling with lengths 1..min(len, 10)
                if (!foundValid)
                {
                    int maxRetries = Math.Min(target.Sequence.Length, 10);
                    for (int cycleLength = 1; cycleLength <= maxRetries; cycleLength++)
                    {
                        string cycledSeq = generator.CycleSequence(target.Sequence, cycleLength, out mapping);
                        if (cycledSeq != target.Sequence && !targetSequences.Contains(cycledSeq))
                        {
                            decoy = BuildDecoyFromSequence(target, cycledSeq, mapping);
                            if (decoy != null)
                            {
                                nCycled++;
                                foundValid = true;
                                break;
                            }
                        }
                    }
                }

                if (foundValid)
                {
                    validTargets.Add(target);
                    decoys.Add(decoy);
                }
                else
                {
                    nExcluded++;
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
            LogInfo(string.Format("[COUNT] mzML spectra loaded [{0}]: {1} MS2 + {2} MS1",
                fileName, spectra.Count, ms1Spectra != null ? ms1Spectra.Count : 0));

            // Extract isolation windows from spectra
            var isolationWindows = ExtractIsolationWindows(spectra);
            LogInfo(string.Format("Found {0} unique isolation windows", isolationWindows.Count));
            LogInfo(string.Format("[COUNT] Isolation windows [{0}]: {1}",
                fileName, isolationWindows.Count));

            // RT calibration
            RTCalibration rtCalibration = null;
            if (config.RtCalibration.Enabled)
            {
                var swCal = Stopwatch.StartNew();
                rtCalibration = RunCalibration(
                    fullLibrary, spectra, ms1Spectra, config, fileName);
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
                WriteFeatureDump(inputFile, fileName, scoredEntries, fullLibrary);
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
            List<FdrEntry> scoredEntries,
            List<LibraryEntry> fullLibrary)
        {
            string dumpPath = Path.Combine(
                Path.GetDirectoryName(inputFile) ?? ".",
                fileName + ".cs_features.tsv");

            // Build lookup from entry id -> library entry for mod sequence / protein info
            var libById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var le in fullLibrary)
                libById[le.Id] = le;

            var header = new string[]
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
            OspreyConfig config,
            string fileName)
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
            if (config.WritePin || Environment.GetEnvironmentVariable("OSPREY_DUMP_CAL_SAMPLE") == "1")
            {
                string dumpPath = fileName + ".cs_cal_sample.txt";
                var tuples = new List<string>();
                foreach (var e in sampledEntries)
                {
                    if (e.IsDecoy) continue;
                    tuples.Add(string.Format("{0}\t{1}\t{2}\t{3:F4}\t{4:F4}",
                        e.Id, e.ModifiedSequence, e.Charge, e.PrecursorMz, e.RetentionTime));
                }
                tuples.Sort(StringComparer.Ordinal);
                using (var w = new StreamWriter(dumpPath))
                {
                    w.WriteLine("id\tmodseq\tcharge\tmz\trt");
                    foreach (var t in tuples) w.WriteLine(t);
                }
                LogInfo(string.Format("[COUNT] Wrote calibration sample: {0} ({1} targets)",
                    dumpPath, tuples.Count));

                if (Environment.GetEnvironmentVariable("OSPREY_CAL_SAMPLE_ONLY") == "1")
                {
                    LogInfo("[BISECT] OSPREY_CAL_SAMPLE_ONLY set - aborting after sample dump");
                    Environment.Exit(0);
                }
            }
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

            // Pass 1: score all sampled entries with the linear pre-fit RT mapping
            // and the wide initial tolerance. Fits a LOESS RTCalibration from the
            // LDA + S/N surviving targets.
            var pass1 = RunCalibrationScoringPass(
                1,
                sampledEntries, spectraByWindowKey, config,
                rtSlope, rtIntercept, initialTolerance,
                null /* calibrationModel: pass 1 uses linear mapping */,
                fileName,
                config.RtCalibration.MinCalibrationPoints);

            if (pass1 == null)
            {
                LogWarning("Calibration pass 1 failed. Using fallback tolerance.");
                return null;
            }

            // === Iterative calibration refinement (2-pass) ===
            // Mirrors Rust pipeline.rs:714-839.
            // MAD × 1.4826 ≈ SD for a normal distribution; 3× that covers ~99.7%.
            double madTolerance = pass1.Stats.MAD * 1.4826 * 3.0;
            double pass1Tolerance = Math.Max(
                config.RtCalibration.MinRtTolerance,
                Math.Min(config.RtCalibration.MaxRtTolerance, madTolerance));

            LogInfo(string.Format(
                "First-pass RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R²={5:F4})",
                pass1Tolerance,
                pass1.Stats.MAD,
                pass1.Stats.MAD * 1.4826,
                pass1.Stats.ResidualSD,
                pass1.Stats.NPoints,
                pass1.Stats.RSquared));

            // Only refine if the tolerance narrowed at least 2× tighter than the
            // initial wide window.
            if (pass1Tolerance < initialTolerance * 0.5)
            {
                LogInfo(string.Format(
                    "Calibration refinement: re-scoring with {0:F2} min tolerance (was {1:F1} min)",
                    pass1Tolerance, initialTolerance));

                var pass2 = RunCalibrationScoringPass(
                    2,
                    sampledEntries, spectraByWindowKey, config,
                    rtSlope, rtIntercept, pass1Tolerance,
                    pass1.Calibration /* pass 2 predicts RT via the LOESS fit */,
                    fileName,
                    ABSOLUTE_MIN_CALIBRATION_POINTS);

                if (pass2 != null)
                {
                    double refinedMadTolerance = pass2.Stats.MAD * 1.4826 * 3.0;
                    double refinedTolerance = Math.Max(
                        config.RtCalibration.MinRtTolerance,
                        Math.Min(config.RtCalibration.MaxRtTolerance, refinedMadTolerance));

                    LogInfo(string.Format(
                        "Refined RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R²={5:F4})",
                        refinedTolerance,
                        pass2.Stats.MAD,
                        pass2.Stats.MAD * 1.4826,
                        pass2.Stats.ResidualSD,
                        pass2.Stats.NPoints,
                        pass2.Stats.RSquared));

                    // Accept the refined calibration only if R² didn't degrade
                    // by more than 1% (matches Rust pipeline.rs:811).
                    if (pass2.Stats.RSquared >= pass1.Stats.RSquared * 0.99)
                    {
                        return pass2.Calibration;
                    }
                    LogInfo(string.Format(
                        "Refined calibration not better (R² {0:F4} vs {1:F4}), keeping original",
                        pass2.Stats.RSquared, pass1.Stats.RSquared));
                }
                else
                {
                    LogInfo(string.Format(
                        "Refinement pass: insufficient points (need {0}), keeping original calibration",
                        ABSOLUTE_MIN_CALIBRATION_POINTS));
                }
            }

            return pass1.Calibration;
        }

        /// <summary>
        /// Result of one calibration scoring pass (scoring + LDA + S/N filter + LOESS fit).
        /// </summary>
        private class CalibrationPassResult
        {
            public RTCalibration Calibration;
            public RTCalibrationStats Stats;
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
            OspreyConfig config,
            double rtSlope, double rtIntercept, double tolerance,
            RTCalibration calibrationModel,
            string fileName,
            int minLoessPoints)
        {
            // Activate per-entry window dump if requested. Cleared after the
            // matching loop completes (file written below).
            bool dumpWindows = Environment.GetEnvironmentVariable("OSPREY_DUMP_CAL_WINDOWS") == "1";
            s_calWindowDump = dumpWindows
                ? new System.Collections.Concurrent.ConcurrentBag<string>()
                : null;

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
            // pass 2 dump overwrites pass 1 — same behaviour as Rust's
            // run_coelution_calibration_scoring dumping on every invocation.
            if (s_calWindowDump != null)
            {
                var rows = new List<string>(s_calWindowDump);
                rows.Sort(StringComparer.Ordinal);
                using (var w = new StreamWriter("cs_cal_windows.txt"))
                {
                    w.WriteLine("entry_id\tis_decoy\tcharge\tprecursor_mz\tlibrary_rt\tiso_lower\tiso_upper\texpected_rt\trt_window_start\trt_window_end");
                    foreach (var r in rows) w.WriteLine(r);
                }
                LogInfo(string.Format(
                    "[COUNT] Wrote calibration windows dump (pass {0}): cs_cal_windows.txt ({1} rows)",
                    passNumber, rows.Count));
                s_calWindowDump = null;

                if (Environment.GetEnvironmentVariable("OSPREY_CAL_WINDOWS_ONLY") == "1")
                {
                    LogInfo("[BISECT] OSPREY_CAL_WINDOWS_ONLY set - aborting after window dump");
                    Environment.Exit(0);
                }
            }

            // Cross-implementation diagnostic: dump per-entry calibration match info
            // for direct diff with Rust. Writes a row for EVERY sampled entry
            // (matched or not), sorted by entry_id for stable diff.
            if (Environment.GetEnvironmentVariable("OSPREY_DUMP_CAL_MATCH") == "1")
            {
                string dumpPath = "cs_cal_match.txt";
                var matchById = new Dictionary<uint, CalibrationMatch>(matches.Count);
                foreach (var m in matches)
                    matchById[m.EntryId] = m;

                // sampledEntries is the full set (targets + decoys) passed to scoring
                var sortedSampled = new List<LibraryEntry>(sampledEntries);
                sortedSampled.Sort((a, b) => a.Id.CompareTo(b.Id));

                int nMatched = 0, nUnmatched = 0;
                using (var w = new StreamWriter(dumpPath))
                {
                    // 11-column layout matching rust_cal_match.txt.
                    // scan is the MS2 scan number of the apex spectrum (the
                    // candidate spectrum whose RT is closest to the XIC apex
                    // RT, matching Rust's apex_spec_local_idx lookup).
                    // snr is the signal-to-noise feeding the S/N filter
                    // (gating which matches enter LOESS).
                    // Uses F10 for all float columns to avoid banker's-vs-round-
                    // half-up formatting mismatch with Rust.
                    w.WriteLine("entry_id\tis_decoy\tcharge\thas_match\tscan\tapex_rt\tcorrelation\tlibcosine\ttop6\txcorr\tsnr");
                    foreach (var entry in sortedSampled)
                    {
                        CalibrationMatch m;
                        if (matchById.TryGetValue(entry.Id, out m))
                        {
                            KeyValuePair<double, double> rtPair;
                            matchRts.TryGetValue(entry.Id, out rtPair);
                            double snr;
                            if (!snrByEntryId.TryGetValue(entry.Id, out snr))
                                snr = 0.0;
                            w.WriteLine(string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "{0}\t{1}\t{2}\t1\t{3}\t{4:F10}\t{5:F10}\t{6:F10}\t{7}\t{8:F10}\t{9:F10}",
                                entry.Id,
                                entry.IsDecoy ? 1 : 0,
                                entry.Charge,
                                m.ScanNumber,
                                rtPair.Value,
                                m.CorrelationScore,
                                m.LibcosineApex,
                                m.Top6MatchedApex,
                                m.XcorrScore,
                                snr));
                            nMatched++;
                        }
                        else
                        {
                            w.WriteLine(string.Format(
                                "{0}\t{1}\t{2}\t0\t\t\t\t\t\t\t",
                                entry.Id,
                                entry.IsDecoy ? 1 : 0,
                                entry.Charge));
                            nUnmatched++;
                        }
                    }
                }
                LogInfo(string.Format(
                    "[COUNT] Wrote calibration match dump (pass {0}): {1} ({2} matched, {3} unmatched)",
                    passNumber, dumpPath, nMatched, nUnmatched));

                if (Environment.GetEnvironmentVariable("OSPREY_CAL_MATCH_ONLY") == "1")
                {
                    LogInfo("[BISECT] OSPREY_CAL_MATCH_ONLY set - aborting after match dump");
                    Environment.Exit(0);
                }
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
            // Uses F10 to avoid banker's-vs-half-up text rounding mismatches with Rust.
            if (Environment.GetEnvironmentVariable("OSPREY_DUMP_LDA_SCORES") == "1")
            {
                var sortedByEntry = matchArray.OrderBy(m => m.EntryId).ToArray();
                using (var w = new StreamWriter("cs_lda_scores.txt"))
                {
                    w.WriteLine("entry_id\tis_decoy\tdiscriminant\tq_value");
                    foreach (var m in sortedByEntry)
                    {
                        w.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2:F10}\t{3:F10}",
                            m.EntryId,
                            m.IsDecoy ? 1 : 0,
                            m.DiscriminantScore,
                            m.QValue));
                    }
                }
                LogInfo(string.Format(
                    "[COUNT] Wrote LDA scores dump (pass {0}): cs_lda_scores.txt ({1} entries)",
                    passNumber, matchArray.Length));
                if (Environment.GetEnvironmentVariable("OSPREY_LDA_SCORES_ONLY") == "1")
                {
                    LogInfo("[BISECT] OSPREY_LDA_SCORES_ONLY set - aborting after LDA dump");
                    Environment.Exit(0);
                }
            }

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
                LogInfo(string.Format("[TIMING] Calibration pass {0} LOESS fit: {1:F2}s",
                    passNumber, swLoess.Elapsed.TotalSeconds));
                LogInfo(string.Format(
                    "RT calibration pass {0}: {1} points, R2={2:F4}, residual SD={3:F3} min, MAD={4:F3}",
                    passNumber, stats.NPoints, stats.RSquared, stats.ResidualSD, stats.MAD));

                return new CalibrationPassResult
                {
                    Calibration = rtCal,
                    Stats = stats,
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
                if (entry.IsDecoy) decoys.Add(entry);
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
                if (t.RetentionTime < rtMin) rtMin = t.RetentionTime;
                if (t.RetentionTime > rtMax) rtMax = t.RetentionTime;
                if (t.PrecursorMz < mzMin) mzMin = t.PrecursorMz;
                if (t.PrecursorMz > mzMax) mzMax = t.PrecursorMz;
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
                if (rtBin >= binsPerAxis) rtBin = binsPerAxis - 1;
                if (mzBin >= binsPerAxis) mzBin = binsPerAxis - 1;
                grid[rtBin, mzBin].Add(i);
            }

            // Count non-empty cells and compute per-cell quota
            int nOccupied = 0;
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    if (grid[i, j].Count > 0) nOccupied++;

            int perCell = nOccupied > 0 ? sampleSize / nOccupied : 1;
            if (perCell < 1) perCell = 1;

            // Diagnostic dump: scalar parameters + full grid contents,
            // matching Rust's dump format for direct diff.
            if (Environment.GetEnvironmentVariable("OSPREY_DUMP_CAL_SAMPLE") == "1")
            {
                using (var w = new StreamWriter("cs_cal_scalars.txt"))
                {
                    w.WriteLine("n_targets\t" + targets.Count);
                    w.WriteLine("n_decoys\t" + decoys.Count);
                    w.WriteLine("bins_per_axis\t" + binsPerAxis);
                    w.WriteLine("rt_min\t" + rtMin.ToString("R"));
                    w.WriteLine("rt_max\t" + rtMax.ToString("R"));
                    w.WriteLine("mz_min\t" + mzMin.ToString("R"));
                    w.WriteLine("mz_max\t" + mzMax.ToString("R"));
                    w.WriteLine("rt_range\t" + rtRange.ToString("R"));
                    w.WriteLine("mz_range\t" + mzRange.ToString("R"));
                    w.WriteLine("rt_bin_width\t" + rtBinWidth.ToString("R"));
                    w.WriteLine("mz_bin_width\t" + mzBinWidth.ToString("R"));
                    w.WriteLine("n_occupied\t" + nOccupied);
                    w.WriteLine("per_cell\t" + perCell);
                    w.WriteLine("seed\t" + seed);
                }
                using (var w = new StreamWriter("cs_cal_grid.txt"))
                {
                    w.WriteLine("rt_bin\tmz_bin\tcount\ttarget_ids");
                    for (int r = 0; r < binsPerAxis; r++)
                    {
                        for (int c = 0; c < binsPerAxis; c++)
                        {
                            var cell = grid[r, c];
                            if (cell.Count == 0) continue;
                            var ids = new List<uint>(cell.Count);
                            foreach (int ti in cell)
                                ids.Add(targets[ti].Id);
                            ids.Sort();
                            var sb = new System.Text.StringBuilder();
                            for (int k = 0; k < ids.Count; k++)
                            {
                                if (k > 0) sb.Append(',');
                                sb.Append(ids[k]);
                            }
                            w.WriteLine("{0}\t{1}\t{2}\t{3}", r, c, cell.Count, sb.ToString());
                        }
                    }
                }
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
                    if (cell.Count == 0) continue;

                    int nTake = Math.Min(cell.Count, perCell);
                    int stride = Math.Max(1, cell.Count / nTake);
                    int cellOffset = offset % Math.Max(1, cell.Count);

                    for (int j = 0; j < nTake; j++)
                    {
                        int idx = (cellOffset + j * stride) % cell.Count;
                        var target = targets[cell[idx]];

                        if (sampledIds.Contains(target.Id)) continue;
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
                    for (int ci = 0; ci < binsPerAxis && !done; ci++)
                    {
                        var cell = grid[ri, ci];
                        if (cell.Count == 0) continue;

                        int added = 0;
                        foreach (int targetIdx in cell)
                        {
                            if (added >= extraPerCell) break;
                            var target = targets[targetIdx];
                            if (sampledIds.Contains(target.Id)) continue;
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
            OspreyConfig config,
            double rtSlope, double rtIntercept, double initialTolerance,
            RTCalibration calibrationModel,
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
            if (s_calWindowDump != null)
            {
                var iso = windowSpectra[0].IsolationWindow;
                double rtLo = expectedRt - initialTolerance;
                double rtHi = expectedRt + initialTolerance;
                s_calWindowDump.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2}\t{3:F6}\t{4:F6}\t{5:F6}\t{6:F6}\t{7:F6}\t{8:F6}\t{9:F6}",
                    entry.Id,
                    entry.IsDecoy ? 1 : 0,
                    entry.Charge,
                    entry.PrecursorMz,
                    entry.RetentionTime,
                    iso.LowerBound,
                    iso.UpperBound,
                    expectedRt,
                    rtLo,
                    rtHi));
            }

            // Filter by RT tolerance and top-6 fragment prefilter.
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

            // Per-entry chromatogram diagnostic. Dump candidates + extracted XICs.
            // OSPREY_DIAG_XIC_ENTRY_ID selects which entry to dump; OSPREY_DIAG_XIC_PASS
            // selects the pass (default pass 1). We default to pass 1 because the
            // cross-tool bisection walks downstream: until pass 1 chromatograms
            // match, there's no point comparing pass 2 (which depends on pass 1's
            // LOESS fit).
            string diagXicEnv = Environment.GetEnvironmentVariable("OSPREY_DIAG_XIC_ENTRY_ID");
            string diagXicPassEnv = Environment.GetEnvironmentVariable("OSPREY_DIAG_XIC_PASS");
            int diagXicPass = 1;
            if (!string.IsNullOrEmpty(diagXicPassEnv))
                int.TryParse(diagXicPassEnv, out diagXicPass);
            int currentPass = calibrationModel != null ? 2 : 1;
            uint diagXicEntryId;
            bool isDiagXic = currentPass == diagXicPass &&
                !string.IsNullOrEmpty(diagXicEnv) &&
                uint.TryParse(diagXicEnv, out diagXicEntryId) && diagXicEntryId == entry.Id;
            string diagXicPath = isDiagXic ? "cs_xic_entry_" + entry.Id + ".txt" : null;
            if (isDiagXic)
            {
                using (var dw = new StreamWriter(diagXicPath))
                {
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# per-entry chromatogram dump for entry_id={0} (pass {1})",
                        entry.Id, currentPass));
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# {0} ({1}, charge={2}, lib_rt={3:F10}, mz={4:F10})",
                        entry.ModifiedSequence, entry.Sequence, entry.Charge,
                        entry.RetentionTime, entry.PrecursorMz));

                    // Pass 2 calculations block: dump the inputs that feed into XIC
                    // extraction so if the XICs don't match, we already have the
                    // intermediate values to localize the divergence (LOESS model
                    // stats, predicted RT, refined tolerance, selection window).
                    if (calibrationModel != null)
                    {
                        var loessStats = calibrationModel.Stats();
                        dw.WriteLine("# LOESS MODEL (pass 2 RT calibration)");
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.n_points={0}", loessStats.NPoints));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.r_squared={0:F10}", loessStats.RSquared));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.residual_sd={0:F10}", loessStats.ResidualSD));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.mean_residual={0:F10}", loessStats.MeanResidual));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.max_residual={0:F10}", loessStats.MaxResidual));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.p20_abs_residual={0:F10}", loessStats.P20AbsResidual));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.p80_abs_residual={0:F10}", loessStats.P80AbsResidual));
                        dw.WriteLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "# loess.mad={0:F10}", loessStats.MAD));
                    }
                    dw.WriteLine("# PASS CALCULATIONS");
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.library_rt={0:F10}", entry.RetentionTime));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.expected_rt={0:F10}", expectedRt));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.tolerance={0:F10}", initialTolerance));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.rt_window_lo={0:F10}", expectedRt - initialTolerance));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.rt_window_hi={0:F10}", expectedRt + initialTolerance));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.rt_slope={0:F10}", rtSlope));
                    dw.WriteLine(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "# pass.rt_intercept={0:F10}", rtIntercept));

                    dw.WriteLine("# n_post_prefilter_candidates=" + candidateSpectra.Count);
                    dw.WriteLine("# CANDIDATES (post-prefilter, sorted by RT)");
                    dw.WriteLine("candidate\tscan_idx\tscan_number\trt");
                    for (int i = 0; i < candidateSpectra.Count; i++)
                    {
                        dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "candidate\t{0}\t{1}\t{2:F10}",
                            i, candidateSpectra[i].ScanNumber, candidateSpectra[i].RetentionTime));
                    }

                    // Top-6 fragments
                    var sortedByIntensity = new List<KeyValuePair<int, float>>(entry.Fragments.Count);
                    for (int fi = 0; fi < entry.Fragments.Count; fi++)
                        sortedByIntensity.Add(new KeyValuePair<int, float>(fi, entry.Fragments[fi].RelativeIntensity));
                    sortedByIntensity.Sort((a, b) => b.Value.CompareTo(a.Value));
                    int topN = Math.Min(6, sortedByIntensity.Count);

                    dw.WriteLine("# TOP-6 FRAGMENTS (selected by intensity desc)");
                    dw.WriteLine("topfrag\ttop_idx\tlib_idx\tlib_mz\tlib_intensity");
                    for (int rank = 0; rank < topN; rank++)
                    {
                        int fi = sortedByIntensity[rank].Key;
                        var fobj = entry.Fragments[fi];
                        dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "topfrag\t{0}\t{1}\t{2:F10}\t{3:F10}",
                            rank, fi, fobj.Mz, fobj.RelativeIntensity));
                    }
                }
            }

            // Extract XICs for the top-N most intense library fragments.
            var xics = ExtractTopNFragmentXics(
                entry, candidateSpectra, rts, CAL_TOP_N_FRAGMENTS, config);

            if (isDiagXic)
            {
                using (var dw = new StreamWriter(diagXicPath, true))
                {
                    dw.WriteLine("# EXTRACTED XICS (lib_idx, scan_idx, rt, intensity)");
                    dw.WriteLine("xic\tlib_idx\tscan_idx\trt\tintensity");
                    // Use F10 — wide enough that half-way rounding on values
                    // like 1756.6640625 (exact f32 mantissa) doesn't diverge
                    // between Rust's banker's rounding and C#'s round-half-up.
                    foreach (var xic in xics)
                    {
                        for (int i = 0; i < xic.RetentionTimes.Length; i++)
                        {
                            dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "xic\t{0}\t{1}\t{2:F10}\t{3:F10}",
                                xic.FragmentIndex, i, xic.RetentionTimes[i], xic.Intensities[i]));
                        }
                    }
                }
                // Hard-exit immediately after dumping. We only care about the
                // diff between rust_xic_entry_<ID>.txt and cs_xic_entry_<ID>.txt;
                // no need to let the 200K-entry scoring loop finish (~minutes)
                // or continue to pass 2 / downstream FDR.
                // Other Parallel.ForEach workers will be killed by Exit(0).
                LogInfo(string.Format(
                    "[BISECT] OSPREY_DIAG_XIC_ENTRY_ID matched on pass {0} - wrote {1} and exiting",
                    currentPass, diagXicPath));
                Environment.Exit(0);
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

            // Identify the reference XIC — the single fragment with the highest
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
                if (scan >= refIntensities.Length) break;
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
            double xcorrApex = scorer.XcorrAtScan(apexSpectrum, entry);
            byte top6Matched = CountTop6Matches(entry, apexSpectrum, config);

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

        // Diagnostic: log detailed trace for a specific peptide. Set this to a
        // peptide modified sequence to dump its RT window, XICs, CWT peaks, and
        // winning peak selection. Used for bisecting divergences with Rust.
        private const string DIAG_PEPTIDE = "AAAAAAAAAAAAAAAGAGAGAK";

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
            bool diag = !candidate.IsDecoy && candidate.ModifiedSequence == DIAG_PEPTIDE
                && candidate.Charge == 2;
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
                        candidate.ModifiedSequence, expectedRt - rtTolerance,
                        expectedRt + rtTolerance));
                }
            }

            if (startScan < 0 || endScan < 0 || endScan - startScan + 1 < 5)
                return null;

            int rangeLen = endScan - startScan + 1;

            // Extract fragment XICs within the RT range
            var xics = ExtractFragmentXics(
                candidate, windowSpectra, windowRts, startScan, endScan, config);

            if (xics.Count < 2)
                return null;

            // Detect candidate peaks (CWT consensus across top-6 XICs).
            // Rust pipeline.rs has three fallbacks when CWT returns empty:
            //   1. CWT consensus (primary)
            //   2. Peak detection on median polish elution profile (fallback)
            //   3. Peak detection on reference XIC (highest-total-intensity) (fallback)
            // We currently only implement the primary path. Candidates missing from
            // CWT are dropped; Rust recovers them via fallback. This accounts for
            // some of the remaining under-detection vs Rust.
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);
            if (diag)
            {
                LogInfo(string.Format(
                    "[DIAG] {0}: xics extracted={1}, CWT peaks={2}",
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

            // Rust scores each candidate peak by mean pairwise fragment correlation
            // over the peak range, then picks the highest-scoring one. We were
            // picking peaks[0] directly (the CWT consensus top peak), which isn't
            // always the one with best pairwise correlation. Re-rank by correlation
            // to match Rust.
            XICPeakBounds bestPeak = null;
            double bestPeakScore = double.MinValue;
            int bestPeakIdx = -1;
            for (int pi = 0; pi < peaks.Count; pi++)
            {
                var p = peaks[pi];
                int pLen = p.EndIndex - p.StartIndex + 1;
                if (pLen < 3)
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
                double score = count > 0 ? sum / count : 0.0;
                if (diag)
                {
                    LogInfo(string.Format(
                        "[DIAG] {0}: peak[{1}] pairwise_corr_mean={2:F4}",
                        candidate.ModifiedSequence, pi, score));
                }
                if (score > bestPeakScore)
                {
                    bestPeakScore = score;
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

            if (bestPeak == null)
                return null;

            int apexGlobalIdx = startScan + bestPeak.ApexIndex;

            // Score at the apex spectrum
            if (apexGlobalIdx < 0 || apexGlobalIdx >= windowSpectra.Count)
                return null;

            var apexSpectrum = windowSpectra[apexGlobalIdx];

            // LibCosine at apex
            double libCosine = scorer.LibCosine(apexSpectrum, candidate, config.FragmentTolerance);

            // XCorr at apex (using unit-resolution binning)
            double xcorr = scorer.XcorrAtScan(apexSpectrum, candidate);

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

            // Savitzky-Golay weighted spectral scores at apex +/- 2 scans.
            // Matches Rust pipeline.rs sg_xcorr / sg_cosine (weights sum to 1,
            // quadratic SG filter of length 5).
            double sgXcorr = 0.0;
            double sgCosine = 0.0;
            int apexLocal = startScan + bestPeak.ApexIndex;
            for (int offset = -2; offset <= 2; offset++)
            {
                double weight = SG_WEIGHTS[offset + 2];
                int idx = apexLocal + offset;
                if (idx < 0 || idx >= windowSpectra.Count)
                    continue;
                var s = windowSpectra[idx];
                sgXcorr += scorer.XcorrAtScan(s, candidate) * weight;
                sgCosine += scorer.LibCosine(s, candidate, config.FragmentTolerance) * weight;
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
            // sg_weighted_xcorr (17) and sg_weighted_cosine (18) require multi-scan
            // Savitzky-Golay weighted scoring which is not yet ported. Leave at 0
            // rather than aliasing xcorr/libcosine (which would inflate SVM separation).
            // BISECT: sg_weighted_* features produced 2.2x more separation than
            // Rust's equivalent features. Temporarily zero out to find convergence
            // baseline. Revisit once the rest of the pipeline matches.
            features[17] = 0.0;             // sg_weighted_xcorr (disabled for bisection)
            features[18] = 0.0;             // sg_weighted_cosine (disabled for bisection)
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
            //      (dropping all-zero fragments biases decoys to higher R²)
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

                    if (fdrEntry.IsDecoy) nInputDecoys++;
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
                    if (entry.IsDecoy) continue;
                    if (entry.EffectiveRunQvalue(config.FdrLevel) > config.RunFdr) continue;
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
