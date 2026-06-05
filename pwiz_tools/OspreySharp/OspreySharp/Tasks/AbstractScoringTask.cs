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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Base class for tasks that drive the OspreySharp scoring engine
    /// (PerFileScoringTask and PerFileRescoreTask). Owns the shared
    /// per-window scoring + per-candidate feature computation +
    /// library prep + dedup helpers; subclasses orchestrate the
    /// per-file or per-pass flow that uses them.
    ///
    /// Methods are protected so subclasses can call them as bare
    /// names; static helpers and constants are protected static so
    /// subclasses (or static contexts inside them) can reach them
    /// without back-references.
    ///
    /// Phase A scope: a mechanical lift of the methods that used to
    /// live on AnalysisPipeline. The shared scoring engine now lives
    /// here; AnalysisPipeline becomes the thin task-pipeline driver
    /// (Run + log sinks).
    /// </summary>
    public abstract class AbstractScoringTask : OspreyTask
    {
        // PipelineContext is set on Run entry by the concrete subclass
        // before any of the moved methods log; the base class never
        // calls Run itself.
        internal PipelineContext _ctx;


        // Number of top-intensity library fragments used for
        // calibration scoring + dedup top-6 overlap. Read both
        // from PerFileScoringTask (calibration) and from the
        // shared scoring engine here.
        internal const int CAL_TOP_N_FRAGMENTS = 6;
        /// <summary>
        /// IComparer&lt;double&gt; implementing IEEE 754-2008 total order
        /// (matches Rust's f64::total_cmp). Key property versus the
        /// default Comparer&lt;double&gt;: distinguishes -0.0 &lt; +0.0,
        /// orders NaNs consistently. Required wherever a stable sort
        /// needs to mirror Rust's slice::sort_by(... .total_cmp(...))
        /// — pair with LINQ OrderBy/OrderByDescending (stable per
        /// .NET contract) to match Rust byte-for-byte.
        /// </summary>
        internal static readonly IComparer<double> TotalOrderComparer =
            Comparer<double>.Create((a, b) =>
            {
                long la = BitConverter.DoubleToInt64Bits(a);
                long lb = BitConverter.DoubleToInt64Bits(b);
                if (la < 0) la ^= 0x7FFFFFFFFFFFFFFFL;
                if (lb < 0) lb ^= 0x7FFFFFFFFFFFFFFFL;
                return la.CompareTo(lb);
            });
        // Internal so FirstJoinTask (which now owns RunPercolatorFdr +
        // RunPercolatorStreaming + BuildBasicFeatures) can reuse the
        // same 21-feature width without redeclaring it.
        internal const int NUM_PIN_FEATURES = 21;


        // EntryId encodes target/decoy in the high bit; base_id is the
        // lower 31 bits, shared by a target and its paired decoy.
        // Internal so the Tasks/ subfolder partials (e.g. FirstJoinTask)
        // can use the same constant.
        internal const uint BASE_ID_MASK = 0x7FFFFFFFu;


        // Serializes mzML reads across concurrent ProcessFile() calls.
        // The producer inside MzmlReader.LoadAllSpectra is a sequential
        // XmlReader over a FileStream, so 3 files parsing in parallel
        // means 3 sequential disk scans fighting for the same head/cache.
        // Gating the parse step funnels the disk-bound work into one
        // stream at a time while leaving the subsequent main-search
        // phase free to run in parallel across files.
        // Internal so LoadSpectra (now on PerFileScoringTask) can take
        // the same gate without redeclaring a parallel SemaphoreSlim.
        internal static readonly SemaphoreSlim s_mzmlReadGate = new SemaphoreSlim(1, 1);


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
        /// Generate decoy entries from the target library with collision detection.
        /// Matches Rust DecoyGenerator.generate_all_with_collision_detection:
        ///   1. Build set of target sequences (stripped) for collision detection
        ///   2. For each target, try reversing
        ///   3. If reversed collides or is palindromic, try cycling with lengths 1..10
        ///   4. If all methods fail, exclude the target-decoy pair
        /// Modifies <paramref name="validTargets"/> to contain only targets that
        /// produced valid decoys (Rust: library = valid_targets; library.extend(decoys)).
        /// </summary>
        protected List<LibraryEntry> GenerateDecoys(
            List<LibraryEntry> targets, OspreyConfig config,
            out List<LibraryEntry> validTargets)
        {
            _ctx.LogInfo(string.Format("Generating decoys using {0} method...", config.DecoyMethod));

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

            _ctx.LogInfo(string.Format(
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


        /// <summary>
        /// Extract unique isolation windows from the first cycle of MS2 spectra.
        /// </summary>
        protected List<IsolationWindow> ExtractIsolationWindows(List<Spectrum> spectra)
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


        // The shared scoring infrastructure (ExtractTopNFragmentXics
        // etc.) reads CAL_TOP_N_FRAGMENTS from PerFileScoringTask;
        // see that class for the canonical declaration.

        /// <summary>
        /// Extract XICs for the top N most intense library fragments across the
        /// supplied (pre-filtered) spectra list. Returns only fragments that have
        /// at least one non-zero intensity point.
        /// </summary>
        protected List<XicData> ExtractTopNFragmentXics(
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
                // Stable sort matching Rust slice::sort_by on
                // RelativeIntensity ties; List<T>.Sort with
                // Comparison<T> is introsort and unstable.
                topIndices = Enumerable.Range(0, nFrags)
                    .OrderByDescending(i => entry.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();
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

                    int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
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
        /// Run coelution scoring for all library entries across all isolation windows.
        /// For each window, finds candidate entries whose precursor falls in the window,
        /// extracts fragment XICs, detects CWT peaks, and scores at each peak.
        /// </summary>
        protected List<FdrEntry> RunCoelutionScoring(
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

            // Apply MS2 calibration to a LOCAL copy of the spectra
            // list, mirroring Rust run_search at pipeline.rs:6750-6772
            // which builds `calibrated_spectra` and then operates on
            // it via `spectra_ref`. Do NOT mutate the input parameter
            // -- the Stage 6 rescore loop calls RunCoelutionScoring
            // multiple times per file (rescore + gap-fill CWT +
            // gap-fill forced) sharing the same spectra list, and
            // mutating in place applies the m/z offset cumulatively
            // across calls (mz - mean -> mz - 2*mean -> mz - 3*mean),
            // which produces wrong fragment matches in the second and
            // third calls. Verified via per-entry XIC dump: scan 8
            // for entry 10110 had Rust intensity = 0 (peak shifted
            // out of tolerance after a single calibration) but C#
            // intensity = 8873 (peak still in range because the
            // accumulated calibration moved a different peak in).
            List<Spectrum> calibratedSpectra;
            if (ms2Calibration.Calibrated)
            {
                calibratedSpectra = new List<Spectrum>(spectra.Count);
                for (int si = 0; si < spectra.Count; si++)
                {
                    var s = spectra[si];
                    double[] correctedMzs = new double[s.Mzs.Length];
                    for (int mi = 0; mi < s.Mzs.Length; mi++)
                        correctedMzs[mi] = MzCalibration.ApplyCalibration(s.Mzs[mi], ms2Calibration);
                    calibratedSpectra.Add(new Spectrum
                    {
                        ScanNumber = s.ScanNumber,
                        RetentionTime = s.RetentionTime,
                        PrecursorMz = s.PrecursorMz,
                        IsolationWindow = s.IsolationWindow,
                        Mzs = correctedMzs,
                        Intensities = s.Intensities
                    });
                }
            }
            else
            {
                // No calibration -> alias the input list (no copy).
                calibratedSpectra = spectra;
            }

            // Group spectra by isolation window center (rounded key) for efficient lookup
            var spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in calibratedSpectra)
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
                // Mirror Rust run_search at pipeline.rs:6776-6815: prefer
                // the per-file .calibration.json's rt_calibration.mad
                // (the FIRST-PASS MAD), and only fall back to the
                // calibration's stats MAD when that JSON value is absent.
                // This is the difference between using ~0.144 (broad
                // first-pass spread) and ~0.012 (narrow refined-cal
                // spread that gets clamped to MinRtTolerance), which
                // costs ~28% of window width and produces ~33k divergent
                // best-peak picks per Stellar file.
                double mad = context.OriginalRtMad ?? rtCalibration.Stats().MAD;
                double robustSd = mad * 1.4826;
                double rtToleranceMad = robustSd * 3.0;
                rtToleranceGlobal = Math.Max(
                    config.RtCalibration.MinRtTolerance,
                    Math.Min(config.RtCalibration.MaxRtTolerance, rtToleranceMad));
                rtSigmaGlobal = Math.Max(robustSd * 5.0, 0.1);
                _ctx.LogInfo(string.Format(
                    "Coelution search RT tolerance: {0:F2} min (3*MAD*1.4826, MAD={1:F3}{2})",
                    rtToleranceGlobal, mad,
                    context.OriginalRtMad.HasValue ? " from .calibration.json" : " from cal stats"));
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
                _ctx.LogInfo(string.Format(
                    "Coelution search using calibrated fragment tolerance: {0:F4} {1}",
                    calTol, unitStr));

                // Use calibrated tolerance for all downstream scoring.
                // (Spectra were already calibrated above into the local
                // calibratedSpectra list before spectraByWindowKey was
                // built, so no rebuild is needed here.)
                config.FragmentTolerance = searchFragTol;

                _ctx.LogInfo(string.Format(
                    "Applying MS2 calibration: mean error = {0:F4} {1} -> correcting by {2:+F4;-F4;0} {1}",
                    ms2Calibration.Mean, ms2Calibration.Unit, -ms2Calibration.Mean));
            }

            // Per-entry search XIC diagnostic: log the intent once at start.
            // The per-entry dump check happens inline in ScoreCandidate via
            // OspreyDiagnostics.ShouldDumpSearchXicFor(entry.Id).
            if (OspreyDiagnostics.DiagSearchEntryIds != null)
            {
                _ctx.LogInfo(string.Format(
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
                _ctx.LogInfo(string.Format(
                    "[BENCH] OSPREY_MAX_SCORING_WINDOWS={0} - capping {1} windows to first {0}",
                    maxWindows, isolationWindows.Count));
            }

            // Bracket the main-search parallel loop with the dotTrace Measure
            // API and peak-memory logging. When no profiler is attached the
            // ProfilerHooks calls are inexpensive no-ops.
            ProfilerHooks.LogMemoryStats(_ctx.LogInfo, "pre-main-search");
            ProfilerHooks.StartMeasure();

            // Process each isolation window (parallelizable). Per-window
            // results land in windowResults[wIdx] so the final flatten is
            // deterministic in window order regardless of completion order.
            // A naive AddRange-under-lock pattern interleaves rows in
            // completion order, which leaves the row set identical but
            // the row sequence (and hence the parquet bytes) different
            // across same-input runs — breaking byte-level reproducibility
            // gates like the Test-Snapshot.ps1 regression harness.
            var windowResults = new List<FdrEntry>[windowsToScore.Count];
            object lockObj = new object();

            Parallel.For(0, windowsToScore.Count, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            wIdx =>
            {
                var window = windowsToScore[wIdx];
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

                windowResults[wIdx] = windowEntries;

                lock (lockObj)
                {
                    windowsProcessed++;
                    if (windowsProcessed % 10 == 0 || windowsProcessed == windowsToScore.Count)
                    {
                        _ctx.LogInfo(string.Format("  Scored {0}/{1} isolation windows",
                            windowsProcessed, windowsToScore.Count));
                    }
                }
            });

            // Flatten in deterministic window-index order.
            for (int wIdx = 0; wIdx < windowResults.Length; wIdx++)
            {
                if (windowResults[wIdx] != null)
                    allEntries.AddRange(windowResults[wIdx]);
            }

            ProfilerHooks.SaveAndStopMeasure();
            ProfilerHooks.LogMemoryStats(_ctx.LogInfo, "post-main-search");

            if (context.XcorrScratchPool != null)
            {
                _ctx.LogInfo(string.Format(
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
        private void LogWindowTimingSummary(ConcurrentBag<WindowTiming> timings)
        {
            if (timings == null || timings.Count == 0)
                return;

            var sorted = timings.OrderBy(t => t.Seconds).ToList();
            int n = sorted.Count;
            double minS = sorted[0].Seconds;
            double maxS = sorted[n - 1].Seconds;
            double medS = sorted[n / 2].Seconds;
            var slowest = sorted[n - 1];
            _ctx.LogInfo(string.Format(
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


        /// <summary>
        /// Build a one-element peak list at the supplied (apex, start, end)
        /// RT triple, mapped onto the reference XIC's RT axis. Returns null
        /// when the resulting index range is degenerate. Mirrors the
        /// boundary_overrides peak-construction path in run_search at
        /// osprey/crates/osprey/src/pipeline.rs:6596-6644.
        /// </summary>
        private static List<XICPeakBounds> BuildOverridePeaks(
            (double Apex, double Start, double End) ob,
            List<XicData> xics)
        {
            // Reference XIC = highest total-intensity fragment, matching
            // Rust run_search at pipeline.rs:7140-7148 which uses
            // `xics.max_by(|a, b| sum_a.total_cmp(&sum_b))` (returns
            // the LAST equal element on ties). Use `>=` to match -- a
            // strict `>` would keep the FIRST equal element here while
            // every other ref_xic selection in this file (including
            // the rank-scoring loop further down) uses `>=`, so an
            // override entry whose top two fragments tie on total
            // intensity would have BuildOverridePeaks pick a different
            // ref than the rank loop expects. That mismatch shows up
            // as ~32k peak_apex divergent rows in the reconciled
            // parquet vs the Rust output.
            int refIdx = 0;
            double refTotal = -1.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                var ints = xics[f].Intensities;
                for (int j = 0; j < ints.Length; j++)
                    total += ints[j];
                if (total >= refTotal) { refTotal = total; refIdx = f; }
            }
            var rtArr = xics[refIdx].RetentionTimes;
            var intArr = xics[refIdx].Intensities;
            int last = rtArr.Length - 1;
            if (last < 2)
                return null;

            // Map override RTs to indices via Rust partition_point semantics:
            // first index where rt >= target. start_index then saturating_sub(1).
            int startIdx = ScoringMath.BinarySearchLowerBound(rtArr, ob.Start);
            if (startIdx > 0) startIdx--;
            if (startIdx > last) startIdx = last;

            int endIdx = ScoringMath.BinarySearchLowerBound(rtArr, ob.End);
            if (endIdx > last) endIdx = last;

            int apexIdx = ScoringMath.BinarySearchLowerBound(rtArr, ob.Apex);
            if (apexIdx > last) apexIdx = last;
            if (apexIdx > 0 &&
                Math.Abs(rtArr[apexIdx - 1] - ob.Apex) < Math.Abs(rtArr[apexIdx] - ob.Apex))
                apexIdx--;
            if (apexIdx < startIdx) apexIdx = startIdx;
            if (apexIdx > endIdx) apexIdx = endIdx;

            if (endIdx <= startIdx + 1)
                return null;

            return new List<XICPeakBounds>
            {
                new XICPeakBounds
                {
                    ApexRt = rtArr[apexIdx],
                    ApexIntensity = intArr[apexIdx],
                    ApexIndex = apexIdx,
                    StartRt = rtArr[startIdx],
                    EndRt = rtArr[endIdx],
                    StartIndex = startIdx,
                    EndIndex = endIdx,
                    Area = PeakDetector.TrapezoidalArea(rtArr, intArr, startIdx, endIdx),
                    SignalToNoise = PeakDetector.ComputeSnr(intArr, apexIdx, startIdx, endIdx),
                },
            };
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

            // Stage 6 boundary override (post-FDR re-scoring): when set,
            // peak detection + the signal pre-filter are skipped and
            // scoring uses the supplied (apex, start, end) RT triple.
            // Mirrors the boundary_overrides path in run_search at
            // osprey/crates/osprey/src/pipeline.rs:6453-6664.
            (double Apex, double Start, double End)? overrideBounds = null;
            if (context.BoundaryOverrides != null &&
                context.BoundaryOverrides.TryGetValue(candidate.Id, out var bnd))
            {
                overrideBounds = bnd;
            }

            // Determine RT search window.
            // Use the global tolerance passed from RunCoelutionScoring (matches
            // Rust's single rt_tolerance for all entries in run_search).
            double expectedRt = rtCalibration != null
                ? rtCalibration.Predict(candidate.RetentionTime)
                : candidate.RetentionTime;
            // Bisection seam: dump (entry_id, library_rt -> expected_rt)
            // for every per-window candidate scoring. Mirrors Rust's
            // dump_predict_rt_call at pipeline.rs ~7014. Pair with
            // WritePredictRtArrays at the top of the rescore loop to
            // narrow whether RT divergences come from cal arrays
            // diverging or from Predict() output differing on identical
            // arrays.
            OspreyDiagnostics.WritePredictRtCall(
                candidate.Id, candidate.RetentionTime, expectedRt);
            double rtTolerance = globalRtTolerance;

            if (diag)
            {
                _ctx.LogInfo(string.Format(
                    "[DIAG] {0} charge {1}: library_rt={2:F3}, expected_rt={3:F3}, tolerance={4:F3}",
                    candidate.ModifiedSequence, candidate.Charge,
                    candidate.RetentionTime, expectedRt, rtTolerance));
                _ctx.LogInfo(string.Format(
                    "[DIAG] {0}: window m/z={1:F3}, fragments={2}, window_spectra={3}",
                    candidate.ModifiedSequence, candidate.PrecursorMz,
                    candidate.Fragments.Count, nScans));
            }

            // Find scan range for XIC extraction.
            //
            // For boundary overrides: use the given boundaries plus margin
            // for SNR context — peak_width on each side, with a 0.2 min
            // floor. Mirrors run_search at pipeline.rs:6473-6477.
            //
            // For normal search (matches Rust commit 885339b): extract over
            // a window wider than rtTolerance so CWT has context on both
            // sides of any in-tolerance apex to determine full peak
            // boundaries. The apex itself is still required to be within
            // rtTolerance (enforced in the candidate-scoring loop below).
            // Half-width is rtTolerance plus max(rtTolerance, 0.1) —
            // tight-calibration runs get a 0.1 min floor of extra context;
            // wider runs scale with rtTolerance.
            // Two filter shapes mirroring Rust pipeline.rs:7031-7065 byte-
            // for-byte. The override branch uses [rtLo, rtHi]; the
            // normal-search branch uses |rt - expectedRt| <= xicHalfWidth.
            // Mathematically identical but NOT f64-equivalent at the
            // boundary -- writing out the precomputed `rtHi = expectedRt
            // + xicHalfWidth` and comparing `rt <= rtHi` can include /
            // exclude a boundary scan that the abs-diff form would not,
            // because the two arithmetic chains round differently in the
            // last bit. Without the abs-diff form, ~1k entries per
            // Stellar file pick a different best apex than Rust because a
            // single boundary spectrum slips into one side's window and
            // not the other's, cascading through CWT peak detection.
            int startScan = -1, endScan = -1;
            if (overrideBounds.HasValue)
            {
                var ob = overrideBounds.Value;
                double peakWidth = Math.Max(0.1, ob.End - ob.Start);
                double margin = Math.Max(0.2, peakWidth);
                double rtLo = ob.Start - margin;
                double rtHi = ob.End + margin;
                for (int i = 0; i < nScans; i++)
                {
                    if (windowRts[i] >= rtLo && windowRts[i] <= rtHi)
                    {
                        if (startScan < 0)
                            startScan = i;
                        endScan = i;
                    }
                }
            }
            else
            {
                double xicHalfWidth = rtTolerance + Math.Max(rtTolerance, 0.1);
                for (int i = 0; i < nScans; i++)
                {
                    if (Math.Abs(windowRts[i] - expectedRt) <= xicHalfWidth)
                    {
                        if (startScan < 0)
                            startScan = i;
                        endScan = i;
                    }
                }
            }

            if (diag)
            {
                if (startScan >= 0 && endScan >= 0)
                {
                    _ctx.LogInfo(string.Format(
                        "[DIAG] {0}: scan range [{1}..{2}] RT [{3:F3}..{4:F3}] ({5} scans)",
                        candidate.ModifiedSequence, startScan, endScan,
                        windowRts[startScan], windowRts[endScan],
                        endScan - startScan + 1));
                    _ctx.LogInfo(string.Format(
                        "[DIAG] {0}: spectrum scan_numbers in range: first={1}, last={2}",
                        candidate.ModifiedSequence,
                        windowSpectra[startScan].ScanNumber,
                        windowSpectra[endScan].ScanNumber));
                }
                else
                {
                    _ctx.LogInfo(string.Format(
                        "[DIAG] {0}: no scans in RT window around expected_rt={1:F3}",
                        candidate.ModifiedSequence, expectedRt));
                }
            }

            if (startScan < 0 || endScan < 0 || endScan - startScan + 1 < 5)
                return null;

            int rangeLen = endScan - startScan + 1;

            // Signal pre-filter: require at least 2 of top 6 fragments present
            // in at least 3 of 4 consecutive scans. Matches Rust pipeline.rs:6032-6066.
            // Skips noise-only candidates before the expensive XIC extraction.
            // Skipped for boundary overrides — caller has already decided to
            // score here.
            if (config.PrefilterEnabled && !overrideBounds.HasValue)
            {
                const int WIN = 4;
                const int MIN_PASS = 3;
                bool[] window = new bool[WIN];
                int winSum = 0;
                bool hasSignal = false;

                for (int i = startScan; i <= endScan; i++)
                {
                    bool passes = FragmentMath.HasTopNFragmentMatch(
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

            // Per-entry search XIC diagnostic. Fires for every scoring
            // path; if the entry is scored twice (consensus + override)
            // the LAST call wins on disk. Caller can isolate by
            // limiting OSPREY_DIAG_SEARCH_ENTRY_IDS to the right
            // entries, or by tagging the dump filename with the path.
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
            //
            // For boundary overrides (Stage 6 re-scoring), peak detection is
            // skipped and a single synthetic XICPeakBounds is built directly
            // from the supplied (apex, start, end). Mirrors run_search at
            // pipeline.rs:6596-6664.
            List<XICPeakBounds> peaks;
            // Track whether peaks came from CWT (vs fallback / override) so the
            // top-N CWT candidate capture below only fires for the CWT path,
            // matching Rust run_search at pipeline.rs:6856 which only stores
            // CWT-sourced candidates on the returned CoelutionScoredEntry.
            bool peaksFromCwt = false;
            if (overrideBounds.HasValue)
            {
                peaks = BuildOverridePeaks(overrideBounds.Value, xics);
                if (peaks == null)
                    return null;
            }
            else
            {
                peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);
                peaksFromCwt = peaks.Count > 0;
            }
            // Snapshot CWT consensus peak count for the OSPREY_DUMP_CWT_PATH
            // dump. The dump only fires for non-override entries (the
            // override path bypasses CWT entirely on both Rust and C#);
            // call sites below gate on `!overrideBounds.HasValue`. Sigma
            // + consensus-signal stats are computed inside
            // OspreyDiagnostics.WriteCwtPathRow when the dump is active,
            // so production callers carry only this single int.
            int diagNCwtPeaks = peaks.Count;

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
                _ctx.LogInfo(string.Format(
                    "[DIAG] {0}: xics extracted={1}, peaks={2}",
                    candidate.ModifiedSequence, xics.Count, peaks.Count));
                for (int i = 0; i < peaks.Count; i++)
                {
                    var p = peaks[i];
                    int apexAbsIdx = startScan + p.ApexIndex;
                    double apexRt = windowRts[apexAbsIdx];
                    uint apexScanNum = windowSpectra[apexAbsIdx].ScanNumber;
                    _ctx.LogInfo(string.Format(
                        "[DIAG] {0}: peak[{1}] apex_local={2} apex_rt={3:F3} scan#={4} range=[{5}..{6}]",
                        candidate.ModifiedSequence, i, p.ApexIndex,
                        apexRt, apexScanNum, p.StartIndex, p.EndIndex));
                }
            }
            if (peaks.Count == 0)
            {
                if (!overrideBounds.HasValue)
                {
                    OspreyDiagnostics.WriteCwtPathRow(
                        context.FileName, candidate.Id,
                        diagNCwtPeaks, 0, 0, false, xics);
                }
                return null;
            }

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
            // run_search at pipeline.rs:7140-7148 which uses
            // `xics.max_by(|a, b| sum_a.total_cmp(&sum_b))`. Rust's
            // `Iterator::max_by` returns the LAST equal element on ties
            // (per std doc), so use `>=` here, NOT `>`. Without this,
            // ~33k Stellar entries pick the first tied fragment as ref_xic
            // while Rust picks the last, producing divergent
            // peak_apex / peak_sharpness in the reconciled parquet.
            int refXicIdx = 0;
            double refXicBestTotal = -1.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                for (int i = 0; i < xics[f].Intensities.Length; i++)
                    total += xics[f].Intensities[i];
                if (total >= refXicBestTotal) { refXicBestTotal = total; refXicIdx = f; }
            }
            double[] refXicIntensities = xics[refXicIdx].Intensities;

            XICPeakBounds bestPeak = null;
            double bestRankScore = double.MinValue;
            int bestPeakIdx = -1;
            int diagNScored = 0; // peaks that pass apex-acceptance
            double twoSigmaSq = 2.0 * rtSigma * rtSigma;
            // Capture every scored peak with its raw coelution score
            // and rank score for the top-N CwtCandidate list assigned
            // below (Stage 6 reconciliation input). Mirrors Rust
            // run_search's scored_candidates collection at
            // pipeline.rs:7261-7327, which fires for the non-override
            // path -- the override branch (pipeline.rs:7155-7223)
            // returns at line 7223 BEFORE reaching the cwt_top_n
            // code, so override entries leave cwt_candidates as the
            // default empty `Vec::new()` on the rescored
            // CoelutionScoredEntry.
            //
            // Build for CWT-OR-FALLBACK paths (anything reaching the
            // rank-scoring loop without an override). Earlier C#
            // versions gated this on `peaksFromCwt` (CWT-consensus
            // success only) which left fallback-path entries with
            // empty cwt_candidates blobs while Rust still populated
            // them; that produced the last 5 / 3 / 2 cwt_candidates
            // divergent rows per Stellar file.
            var capturedPeaks = !overrideBounds.HasValue
                ? new List<(XICPeakBounds peak, double coelutionScore, double rankScore)>(peaks.Count)
                : null;
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
                // locations still have to be within it. Bypassed for
                // boundary overrides (caller has already chosen the apex).
                double peakApexRt = windowRts[startScan + p.ApexIndex];
                double rtResidual = Math.Abs(peakApexRt - expectedRt);
                if (!overrideBounds.HasValue && rtResidual > rtTolerance)
                    continue;
                diagNScored++;

                double sum = 0.0;
                int count = 0;
                for (int ii = 0; ii < xics.Count; ii++)
                {
                    for (int jj = ii + 1; jj < xics.Count; jj++)
                    {
                        double corr = ScoringMath.PearsonCorrelationInRange(
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
                    _ctx.LogInfo(string.Format(
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

                if (capturedPeaks != null)
                    capturedPeaks.Add((p, coelutionScore, rankScore));
            }

            if (diag && bestPeak != null)
            {
                int apexAbsIdx = startScan + bestPeak.ApexIndex;
                _ctx.LogInfo(string.Format(
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
                                    double c = ScoringMath.PearsonCorrelationInRange(xics[ii].Intensities, xics[jj].Intensities,
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
            {
                if (!overrideBounds.HasValue)
                {
                    OspreyDiagnostics.WriteCwtPathRow(
                        context.FileName, candidate.Id,
                        diagNCwtPeaks, peaks.Count, diagNScored, false, xics);
                }
                return null;
            }

            // For CWT-path entries (no override), Rust pipeline.rs:7406-7424
            // RECOMPUTES the peak's apex_index / apex_intensity using
            // ref_xic[si..=ei] (the highest-total-intensity single
            // fragment), discarding the apex_index that came out of
            // detect_cwt_consensus_peaks (which used ref_signal -- the
            // SUM of all fragment intensities). When a peak's max in
            // the summed signal sits at a different scan than the max
            // in the single ref_xic, leaving the consensus apex_index
            // in place produces divergent peak_apex / peak_sharpness
            // vs Rust on the reconciled .scores.parquet (~32k rows on
            // Stellar before this fix). Override entries are excluded
            // because Rust's override branch (pipeline.rs:7155-7223)
            // uses the override-supplied apex_index directly without
            // refining it -- match by skipping the recompute.
            if (!overrideBounds.HasValue)
            {
                int si = bestPeak.StartIndex;
                int ei = bestPeak.EndIndex;
                int newApexIdx = si;
                double newApexVal = refXicIntensities[si];
                for (int i = si + 1; i <= ei; i++)
                {
                    // Use `>=` to match Rust's `max_by` last-on-tie.
                    if (refXicIntensities[i] >= newApexVal)
                    {
                        newApexVal = refXicIntensities[i];
                        newApexIdx = i;
                    }
                }
                // Rust pipeline.rs:7433-7444 RECOMPUTES `peak.area` and
                // `peak.signal_to_noise` from `ref_xic[si..=ei]` here, NOT
                // preserving the original CWT detection's values. The
                // original area/SNR came from the consensus signal's
                // boundary (which can differ from the CWT apex's
                // [start..end] in the ref_xic). Preserving them produces
                // ~560 bounds_area / ~546 bounds_snr divergent rows on
                // Stellar where the parquet reports the WRONG (consensus-
                // boundary) area for the WINNING (ref_xic-boundary) peak.
                // peak_area / peak_sharpness as PIN features already
                // recompute correctly via ComputePeakShapeFeatures; this
                // recompute keeps the parquet's bounds_area / bounds_snr
                // consistent with that.
                double[] refRtsAll = xics[refXicIdx].RetentionTimes;
                double newArea = PeakDetector.TrapezoidalArea(
                    refRtsAll, refXicIntensities, si, ei);
                double newSnr = PeakDetector.ComputeSnr(
                    refXicIntensities, newApexIdx, si, ei);
                bestPeak = new XICPeakBounds
                {
                    ApexRt = refRtsAll[newApexIdx],
                    ApexIntensity = newApexVal,
                    ApexIndex = newApexIdx,
                    StartRt = refRtsAll[si],
                    EndRt = refRtsAll[ei],
                    StartIndex = si,
                    EndIndex = ei,
                    Area = newArea,
                    SignalToNoise = newSnr,
                };
            }

            int apexGlobalIdx = startScan + bestPeak.ApexIndex;

            // Score at the apex spectrum
            if (apexGlobalIdx < 0 || apexGlobalIdx >= windowSpectra.Count)
            {
                if (!overrideBounds.HasValue)
                {
                    OspreyDiagnostics.WriteCwtPathRow(
                        context.FileName, candidate.Id,
                        diagNCwtPeaks, peaks.Count, diagNScored, false, xics);
                }
                return null;
            }

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

                // Bisection seam: dump (frag_pos, frag_idx, scan_idx, rt,
                // intensity) for every median-polish call. Mirrors the
                // Rust diagnostics::dump_mp_inputs at pipeline.rs:6494.
                // Use the same input buffers passed to the median polish
                // so we capture the exact data the algorithm sees.
                OspreyDiagnostics.WriteMpInputsRow(
                    candidate.Id, apexSpectrum.ScanNumber, peakXics, peakRts);

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

            // Stage 6 reconciliation input: capture the top-N CWT peak
            // candidates ranked by penalized rank score, with each kept
            // candidate's apex/area/snr recomputed over the reference XIC
            // slice. Mirrors Rust run_search at pipeline.rs:6852-6879.
            // The stored coelution_score is the raw mean (NOT the RT-
            // penalized rank score) -- reconciliation has its own RT
            // tolerance logic via consensus RT comparison.
            List<CwtCandidate> cwtCandidatesOut = null;
            int topN = context.Config.Reconciliation != null
                ? context.Config.Reconciliation.TopNPeaks
                : 0;
            if (capturedPeaks != null && capturedPeaks.Count > 0 && topN > 0)
            {
                // Rust scored_candidates.sort_by at pipeline.rs:7329 is
                // STABLE (slice::sort_by is stable) AND uses f64::total_cmp,
                // which distinguishes -0.0 < +0.0. The default
                // Comparer<double> treats them equal, so two peaks whose
                // rank_score = coelution * rt_penalty * intensityWeight
                // collapses to a signed zero (intensityWeight = 0 when
                // ref_xic intensity at apex is 0; sign comes from
                // coelution) compare as tied under standard <, while
                // Rust orders them positive-then-negative. Pair LINQ
                // OrderByDescending (stable per .NET contract) with
                // TotalOrderComparer to match Rust byte-for-byte.
                capturedPeaks = capturedPeaks
                    .OrderByDescending(p => p.rankScore, TotalOrderComparer)
                    .ToList();
                int kept = Math.Min(topN, capturedPeaks.Count);
                cwtCandidatesOut = new List<CwtCandidate>(kept);
                double[] refRts = xics[refXicIdx].RetentionTimes;
                for (int k = 0; k < kept; k++)
                {
                    var cap = capturedPeaks[k];
                    var p = cap.peak;
                    int safeStart = Math.Max(0, Math.Min(p.StartIndex, refXicIntensities.Length - 1));
                    int safeEnd = Math.Max(safeStart, Math.Min(p.EndIndex, refXicIntensities.Length - 1));
                    int apexIdx = safeStart;
                    double apexVal = refXicIntensities[safeStart];
                    for (int s = safeStart; s <= safeEnd; s++)
                    {
                        if (refXicIntensities[s] >= apexVal)
                        {
                            apexVal = refXicIntensities[s];
                            apexIdx = s;
                        }
                    }
                    double area = PeakDetector.TrapezoidalArea(
                        refRts, refXicIntensities, safeStart, safeEnd);
                    double snr = PeakDetector.ComputeSnr(
                        refXicIntensities, apexIdx, safeStart, safeEnd);
                    cwtCandidatesOut.Add(new CwtCandidate
                    {
                        ApexRt = refRts[apexIdx],
                        StartRt = refRts[safeStart],
                        EndRt = refRts[safeEnd],
                        Area = area,
                        Snr = snr,
                        CoelutionScore = cap.coelutionScore,
                    });
                }
            }

            // Build FdrEntry. The six blob/scalar fields below mirror
            // Rust CoelutionScoredEntry::{fragment_mzs, fragment_intensities,
            // reference_xic, peak.area, peak.signal_to_noise} so the
            // reconciled .scores.parquet write-back can produce byte-
            // identical blob columns for cross-impl validation.
            //
            // FragmentMzs / FragmentIntensities iterate the FULL library
            // fragment list (not just the top-N used by XIC extraction)
            // because Rust's parquet writer at pipeline.rs:1620-1631
            // serializes every library fragment.
            int nFrags = candidate.Fragments?.Count ?? 0;
            double[] fragMzs = new double[nFrags];
            float[] fragInts = new float[nFrags];
            for (int fi = 0; fi < nFrags; fi++)
            {
                fragMzs[fi] = candidate.Fragments[fi].Mz;
                fragInts[fi] = candidate.Fragments[fi].RelativeIntensity;
            }

            // ReferenceXic{Rts,Intensities} are sliced from the highest-
            // total-intensity fragment XIC across the winning peak's
            // [si..=ei] window, matching Rust's
            // `ref_xic[peak.start_index..=peak.end_index].to_vec()` at
            // pipeline.rs:6538. Use the SAFE indices (clipped by the
            // post-rank apex recompute) for non-override entries; the
            // override path's bestPeak retains its original boundaries.
            double[] refXicRtsAll = xics[refXicIdx].RetentionTimes;
            int refMaxLen = Math.Min(
                refXicRtsAll != null ? refXicRtsAll.Length : 0,
                refXicIntensities != null ? refXicIntensities.Length : 0);
            double[] refXicRts;
            double[] refXicInts;
            if (refMaxLen == 0)
            {
                refXicRts = new double[0];
                refXicInts = new double[0];
            }
            else
            {
                int refSi = Math.Max(0, Math.Min(bestPeak.StartIndex, refMaxLen - 1));
                int refEi = Math.Max(refSi, Math.Min(bestPeak.EndIndex, refMaxLen - 1));
                int refLen = refEi - refSi + 1;
                refXicRts = new double[refLen];
                refXicInts = new double[refLen];
                for (int i = 0; i < refLen; i++)
                {
                    refXicRts[i] = refXicRtsAll[refSi + i];
                    refXicInts[i] = refXicIntensities[refSi + i];
                }
            }

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
                Features = features,
                CwtCandidates = cwtCandidatesOut,
                FragmentMzs = fragMzs,
                FragmentIntensities = fragInts,
                ReferenceXicRts = refXicRts,
                ReferenceXicIntensities = refXicInts,
                BoundsArea = bestPeak.Area,
                BoundsSnr = bestPeak.SignalToNoise,
            };

            if (!overrideBounds.HasValue)
            {
                OspreyDiagnostics.WriteCwtPathRow(
                    context.FileName, candidate.Id,
                    diagNCwtPeaks, peaks.Count, diagNScored, true, xics);
            }
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
                // Rust's `indexed.sort_by(|a, b| b.1.total_cmp(&a.1))` at
                // osprey-scoring/src/lib.rs:528 is STABLE (slice::sort_by
                // is stable). Switch from `List<T>.Sort` (introsort,
                // unstable) to LINQ `OrderByDescending` (stable per .NET
                // contract) so that ties on RelativeIntensity preserve
                // the library's fragment order, matching Rust. Without
                // this, a peptide with two fragments at equal relative
                // intensity can land different fragments in its top-N on
                // the C# side, which cascades through XIC extraction →
                // peak detection → rankScore → bestPeak selection.
                topIndices = Enumerable.Range(0, nFrags)
                    .OrderByDescending(i => candidate.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();
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

                    int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
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
                    double corr = ScoringMath.PearsonCorrelationInRange(
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

            // Use reference XIC (highest total intensity), matching Rust
            // pipeline.rs:7140-7148. Rust's `xics.iter().max_by(...)`
            // returns the LAST equal element on ties (per Iterator::max_by
            // doc), so use `>=` here, NOT `>`. Without this, the first
            // tied fragment wins on the C# side while Rust picks the
            // last, producing divergent peak_apex / peak_area /
            // peak_sharpness when fragments tie on total intensity.
            int refIdx = 0;
            double bestTotal = -1.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                double[] inten = xics[f].Intensities;
                for (int i = 0; i < inten.Length; i++)
                    total += inten[i];
                if (total >= bestTotal)
                {
                    bestTotal = total;
                    refIdx = f;
                }
            }

            double[] refInten = xics[refIdx].Intensities;
            double[] refRts = xics[refIdx].RetentionTimes;

            // peak_apex == intensity at peak.ApexIndex in the reference
            // XIC. This matches Rust pipeline.rs:6547 which sets
            // `peak_apex: peak.apex_intensity` (the value already
            // assigned in BuildOverridePeaks / the CWT-path FindPeaks).
            //
            // Earlier C# implementations recomputed apex as the local
            // max in `ref_xic[start..=end]`. That recomputation diverges
            // from Rust on the OVERRIDE path: there, Rust deliberately
            // uses the override-supplied apex_index even when a
            // different scan in [start..=end] has higher intensity (the
            // override is the authoritative apex for reconciliation /
            // gap-fill scoring). The local-max approach put C# at
            // ~32k row peak_apex / peak_sharpness divergence vs Rust on
            // the reconciled .scores.parquet.
            //
            // Use peak.ApexIndex (clipped above) and look up the
            // intensity directly. Sharpness slopes below also use this
            // apex position so the left/right edges align with what
            // Rust computes.
            int apexIdx = apex;
            double apexVal = refInten[apexIdx];
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

                int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
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

            // Do NOT early-return when apex spectrum or candidate fragments
            // are empty. Rust's compute_mass_accuracy
            // (osprey-scoring/src/lib.rs:464) handles empty inputs by
            // returning (0.0, tolerance, tolerance) — so a candidate that
            // reaches this function with no matchable fragments still
            // contributes the calibrated tolerance as its abs mass error.
            // The early-return form left absMassAccuracyMean at 0 instead,
            // producing ~65 divergent rows on Astral (file 49: 2 rows,
            // 55: 27 rows, 60: 36 rows). Let the matching loop run with
            // zero iterations and fall through to the nMatched==0 fallback
            // at the bottom of the function for cross-impl symmetry.
            double totalIntensity = 0.0;
            if (apexSpectrum.Intensities != null)
            {
                for (int i = 0; i < apexSpectrum.Intensities.Length; i++)
                    totalIntensity += apexSpectrum.Intensities[i];
            }

            double matchedIntensity = 0.0;
            double massErrSum = 0.0;
            double absMassErrSum = 0.0;
            int nMatched = 0;

            if (apexSpectrum.Mzs != null && apexSpectrum.Intensities != null &&
                candidate.Fragments != null)
            {
                foreach (var frag in candidate.Fragments)
                {
                    double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                    double lower = frag.Mz - tolDa;
                    double upper = frag.Mz + tolDa;

                    int lo = ScoringMath.BinarySearchLowerBound(apexSpectrum.Mzs, lower);
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
                ms1PrecursorCoelution = ScoringMath.PearsonCorrelationInRange(ms1Arr, refArr, 0, ms1Arr.Length - 1);
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
        protected static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
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
        // Dead code: no callers tree-wide (found during the domain-helper
        // relocation). Left in place, not relocated, pending the Tasks-layer
        // dead-code pass tracked in TODO-ospreysharp_task_layer_decomposition
        // (PR-C); do not delete without confirming it is still uncalled.
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
        // Dead code: no callers tree-wide. Left in place (not relocated with the
        // other stateless math) pending the Tasks-layer dead-code pass tracked in
        // TODO-ospreysharp_task_layer_decomposition (PR-C); do not delete without
        // confirming it is still uncalled.
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
        protected byte CountTop6Matches(
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
                // Stable top-6 by RelativeIntensity, matching Rust
                // slice::sort_by ties (Array.Sort with Comparison<T>
                // is introsort and unstable).
                var indices = Enumerable.Range(0, entry.Fragments.Count)
                    .OrderByDescending(i => entry.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();

                for (int t = 0; t < indices.Length; t++)
                {
                    if (SpectralScorer.HasMatch(entry.Fragments[indices[t]].Mz,
                        spectrum.Mzs, config.FragmentTolerance))
                        matched++;
                }
            }
            return matched;
        }


        /// <summary>
        /// Within each isolation window, drop scored entries whose top-6
        /// fragment lists overlap >= 50% with another same-class entry
        /// eluting within +/-5 spectra. Of each colliding pair, the
        /// entry with the higher coelution_sum survives. Mirrors
        /// osprey/crates/osprey/src/pipeline.rs::deduplicate_double_counting
        /// so the same precursor cannot be counted twice from a shared
        /// chromatographic feature.
        /// </summary>
        protected List<FdrEntry> DeduplicateDoubleCounting(
            List<FdrEntry> entries,
            List<LibraryEntry> library,
            IList<Spectrum> spectra,
            MzCalibrationResult ms2Cal,
            List<IsolationWindow> isolationWindows,
            OspreyConfig config)
        {
            int originalCount = entries.Count;
            if (originalCount == 0 || isolationWindows == null || isolationWindows.Count == 0)
                return entries;

            // Effective fragment tolerance: 3-sigma from MS2 calibration when
            // calibrated, falling back to the configured value. Floors at
            // 0.05 Da / 1 ppm so a tightly fit calibration cannot collapse
            // the matcher to a sub-isotope window.
            double fragTolValue;
            ToleranceUnit fragTolUnit;
            if (ms2Cal != null && ms2Cal.Calibrated)
            {
                double tol3sd = 3.0 * ms2Cal.SD;
                fragTolUnit = string.Equals(ms2Cal.Unit, "Th", StringComparison.OrdinalIgnoreCase)
                    ? ToleranceUnit.Mz : ToleranceUnit.Ppm;
                double minTol = fragTolUnit == ToleranceUnit.Mz ? 0.05 : 1.0;
                fragTolValue = Math.Max(tol3sd, minTol);
            }
            else
            {
                fragTolValue = config.FragmentTolerance.Tolerance;
                fragTolUnit  = config.FragmentTolerance.Unit;
            }

            // RT neighborhood = 5 x median spectrum spacing.
            double rtNeighborhood;
            {
                var sortedRts = new List<double>(spectra.Count);
                foreach (var s in spectra) sortedRts.Add(s.RetentionTime);
                sortedRts.Sort();
                // Dedup adjacent identicals
                int writeIdx = 0;
                for (int i = 0; i < sortedRts.Count; i++)
                {
                    if (i == 0 || sortedRts[i] != sortedRts[i - 1])
                    {
                        sortedRts[writeIdx++] = sortedRts[i];
                    }
                }
                if (writeIdx < sortedRts.Count) sortedRts.RemoveRange(writeIdx, sortedRts.Count - writeIdx);
                if (sortedRts.Count < 2)
                {
                    rtNeighborhood = 0.25; // 5 * 0.05 fallback
                }
                else
                {
                    var intervals = new List<double>(sortedRts.Count - 1);
                    for (int i = 1; i < sortedRts.Count; i++)
                        intervals.Add(sortedRts[i] - sortedRts[i - 1]);
                    intervals.Sort();
                    rtNeighborhood = 5.0 * intervals[intervals.Count / 2];
                }
            }

            // Library lookup by EntryId (Id may not be array-index aligned).
            var libIdMap = new Dictionary<uint, int>(library.Count);
            for (int i = 0; i < library.Count; i++) libIdMap[library[i].Id] = i;

            // Resolve each entry's precursor m/z from the library by
            // EntryId. FdrEntry is a lightweight stub (no PrecursorMz);
            // the library knows. Entries whose EntryId is missing from
            // the library are excluded from dedup (and from the
            // partition below) — they're left untouched downstream.
            int n = entries.Count;
            double[] entryMz = new double[n];
            for (int i = 0; i < n; i++)
            {
                int libIdx;
                entryMz[i] = libIdMap.TryGetValue(entries[i].EntryId, out libIdx)
                    ? library[libIdx].PrecursorMz
                    : double.NaN;
            }

            // Per-window entry indices via sorted-mz partition. Exclusive
            // upper bound [lower, upper) makes every entry belong to at
            // most one window — no cross-window write conflicts.
            int[] mzSortedIdx = new int[n];
            for (int i = 0; i < n; i++) mzSortedIdx[i] = i;
            Array.Sort(mzSortedIdx, (a, b) => entryMz[a].CompareTo(entryMz[b])); // Array.Sort OK: tied entryMz partition into the same window; per-window order is then re-derived inside the window loop. TODO(parity): audit whether stable order is needed if downstream becomes order-sensitive.
            double[] mzSortedVal = new double[n];
            for (int i = 0; i < n; i++) mzSortedVal[i] = entryMz[mzSortedIdx[i]];

            int LowerBoundLt(double v)
            {
                int lo = 0, hi = mzSortedVal.Length;
                while (lo < hi) { int m = (lo + hi) >> 1;
                    if (mzSortedVal[m] < v) lo = m + 1; else hi = m; }
                return lo;
            }

            var windowEntries = new List<int[]>(isolationWindows.Count);
            foreach (var w in isolationWindows)
            {
                int lo = LowerBoundLt(w.LowerBound);
                int hi = LowerBoundLt(w.UpperBound);
                int len = hi - lo;
                int[] arr = new int[len];
                for (int i = 0; i < len; i++) arr[i] = mzSortedIdx[lo + i];
                windowEntries.Add(arr);
            }

            // Removal flags. Each entry with a library mapping is in at
            // most one window so a plain bool[] is safe under per-window
            // parallelism. Entries without a library mapping (EntryId
            // missing from libIdMap; entryMz = NaN) can land in multiple
            // windows via the LowerBoundLt scan, but the inner pair
            // loop's libIdMap.TryGetValue short-circuits before any
            // write to removed[], so the bool[] still has at most one
            // writer per slot.
            bool[] removed = new bool[n];

            Parallel.ForEach(windowEntries, indices =>
            {
                if (indices.Length < 2) return;

                int[] rtSorted = (int[])indices.Clone();
                // Stable sort: apex_rt then base_id then entry_id (matches
                // Rust's deterministic tiebreaker for the dedup pass).
                Array.Sort(rtSorted, (a, b) => // Array.Sort OK: comparator's terminal key is the unique EntryId, so no ties
                {
                    int c = entries[a].ApexRt.CompareTo(entries[b].ApexRt);
                    if (c != 0) return c;
                    uint baseA = entries[a].EntryId & 0x7FFFFFFFu;
                    uint baseB = entries[b].EntryId & 0x7FFFFFFFu;
                    c = baseA.CompareTo(baseB);
                    if (c != 0) return c;
                    return entries[a].EntryId.CompareTo(entries[b].EntryId);
                });

                for (int iPos = 0; iPos < rtSorted.Length; iPos++)
                {
                    int idxA = rtSorted[iPos];
                    if (removed[idxA]) continue;
                    double apexA = entries[idxA].ApexRt;

                    for (int jPos = iPos + 1; jPos < rtSorted.Length; jPos++)
                    {
                        int idxB = rtSorted[jPos];
                        double apexB = entries[idxB].ApexRt;
                        if (apexB - apexA > rtNeighborhood) break;
                        if (removed[idxB]) continue;
                        if (entries[idxA].IsDecoy != entries[idxB].IsDecoy) continue;

                        int libIdxA, libIdxB;
                        if (!libIdMap.TryGetValue(entries[idxA].EntryId, out libIdxA)) continue;
                        if (!libIdMap.TryGetValue(entries[idxB].EntryId, out libIdxB)) continue;

                        var fragsA = library[libIdxA].Fragments;
                        var fragsB = library[libIdxB].Fragments;
                        int overlap = FragmentOverlap.CountTopNFragmentOverlap(fragsA, fragsB, 6,
                            fragTolValue, fragTolUnit);
                        int minA = Math.Min(fragsA.Count, 6);
                        int minB = Math.Min(fragsB.Count, 6);
                        int threshold = (int)Math.Ceiling(Math.Min(minA, minB) * 0.5);
                        if (overlap < threshold) continue;

                        if (entries[idxA].CoelutionSum >= entries[idxB].CoelutionSum)
                        {
                            removed[idxB] = true;
                        }
                        else
                        {
                            removed[idxA] = true;
                            break; // idxA is gone; further pairs irrelevant
                        }
                    }
                }
            });

            int removedCount = 0;
            int removedTargets = 0;
            for (int i = 0; i < n; i++)
            {
                if (!removed[i]) continue;
                removedCount++;
                if (!entries[i].IsDecoy) removedTargets++;
            }
            int removedDecoys = removedCount - removedTargets;
            if (removedCount > 0)
            {
                _ctx.LogInfo(string.Format(
                    "Double-counting deduplication: removed {0} entries " +
                    "({1} targets, {2} decoys; {3} remaining)",
                    removedCount, removedTargets, removedDecoys,
                    originalCount - removedCount));
            }

            var kept = new List<FdrEntry>(originalCount - removedCount);
            for (int i = 0; i < n; i++)
                if (!removed[i]) kept.Add(entries[i]);
            return kept;
        }


        protected List<FdrEntry> DeduplicatePairs(List<FdrEntry> entries)
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

            // Sort by EntryId for deterministic order regardless of Dictionary
            // iteration. Mirrors Rust deduplicate_pairs's final sort_by_key
            // (pipeline.rs:6123) and its comment: "Without this, the random
            // HashMap order propagates to SVM feature matrix row ordering,
            // causing non-deterministic gradient updates and model weights."
            // Cross-impl: the straight-through path feeds these entries
            // directly to Percolator (no parquet round-trip to mask the
            // un-sorted order), so an unsorted dedup output cascades into
            // SVM working-set divergence and ~190-precursor / ~270-peptide
            // first-pass FDR drift on Stellar Single.
            deduped.Sort((a, b) => a.EntryId.CompareTo(b.EntryId));

            int removed = entries.Count - deduped.Count;
            if (removed > 0)
            {
                _ctx.LogInfo(string.Format("Deduplicated: {0} -> {1} entries ({2} removed)",
                    entries.Count, deduped.Count, removed));
            }

            return deduped;
        }
    }
}
