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
        // Internal so FirstJoinTask (which now owns RunPercolatorFdr +
        // RunPercolatorStreaming + BuildBasicFeatures) can reuse the
        // same feature width without redeclaring it. Derives from the
        // single source of truth in OspreySharp.Scoring so the two cannot drift.
        internal const int NUM_PIN_FEATURES = OspreyFeatureCalculators.FeatureCount;


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


        // Local alias so existing dump code using F10 continues to read cleanly.
        // The actual formatter lives in OspreyDiagnostics now.
        private static string F10(double v)
        {
            return OspreyDiagnosticsLog.F10(v);
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
            ScoringContext context,
            PipelineContext ctx)
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
                ctx.LogInfo(string.Format(
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
                ctx.LogInfo(string.Format(
                    "Coelution search using calibrated fragment tolerance: {0:F4} {1}",
                    calTol, unitStr));

                // Use calibrated tolerance for all downstream scoring.
                // (Spectra were already calibrated above into the local
                // calibratedSpectra list before spectraByWindowKey was
                // built, so no rebuild is needed here.)
                config.FragmentTolerance = searchFragTol;

                ctx.LogInfo(string.Format(
                    "Applying MS2 calibration: mean error = {0:F4} {1} -> correcting by {2:+F4;-F4;0} {1}",
                    ms2Calibration.Mean, ms2Calibration.Unit, -ms2Calibration.Mean));
            }

            // Per-entry search XIC diagnostic: log the intent once at start.
            // The per-entry dump check happens inline in ScoreCandidate via
            // the injected IScoringDiagnostics.ShouldDumpSearchXicFor(entry.Id).
            var diagSearchIds = ctx.Diagnostics?.DiagSearchEntryIds;
            if (diagSearchIds != null)
            {
                ctx.LogInfo(string.Format(
                    "[BISECT] OSPREY_DIAG_SEARCH_ENTRY_IDS: will dump {0} entries",
                    diagSearchIds.Count));
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
                ctx.LogInfo(string.Format(
                    "[BENCH] OSPREY_MAX_SCORING_WINDOWS={0} - capping {1} windows to first {0}",
                    maxWindows, isolationWindows.Count));
            }

            // Bracket the main-search parallel loop with the dotTrace Measure
            // API and peak-memory logging. When no profiler is attached the
            // ProfilerHooks calls are inexpensive no-ops.
            ProfilerHooks.LogMemoryStats(ctx.LogInfo, "pre-main-search");
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

            // Construct the per-window coelution scorer once. It captures the
            // log sink + the scoring-diagnostics sink (null when -d is off; the
            // scorer invokes it null-conditionally, so this is a no-op then).
            var coelutionScorer = new CoelutionScorer(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics);

            Parallel.For(0, windowsToScore.Count, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            wIdx =>
            {
                var window = windowsToScore[wIdx];
                var swWindow = Stopwatch.StartNew();
                var windowEntries = coelutionScorer.ScoreWindow(
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
                        ctx.LogInfo(string.Format("  Scored {0}/{1} isolation windows",
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
            ProfilerHooks.LogMemoryStats(ctx.LogInfo, "post-main-search");

            if (context.XcorrScratchPool != null)
            {
                ctx.LogInfo(string.Format(
                    "[POOL] scratch_allocs={0}, bins_allocs={1}",
                    context.XcorrScratchPool.ScratchAllocCount,
                    context.XcorrScratchPool.BinsAllocCount));
            }

            // Summarize per-window timings.
            LogWindowTimingSummary(windowTimings, ctx);

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
        private void LogWindowTimingSummary(ConcurrentBag<WindowTiming> timings, PipelineContext ctx)
        {
            if (timings == null || timings.Count == 0)
                return;

            var sorted = timings.OrderBy(t => t.Seconds).ToList();
            int n = sorted.Count;
            double minS = sorted[0].Seconds;
            double maxS = sorted[n - 1].Seconds;
            double medS = sorted[n / 2].Seconds;
            var slowest = sorted[n - 1];
            ctx.LogInfo(string.Format(
                "[TIMING] Per-window: min={0:F2}s, median={1:F2}s, max={2:F2}s (slowest m/z={3:F1} had {4} candidates)",
                minS, medS, maxS, slowest.CenterMz, slowest.CandidateCount));
        }


        /// <summary>
        /// Find the MS1 spectrum with retention time closest to the given RT.
        /// Assumes MS1 spectra are sorted by RT. Thin forwarder to the single
        /// implementation in Core (<see cref="MS1Spectrum.FindNearest"/>) so the
        /// harness (here + <c>Calibrator</c>) and the MS1 feature calculators share
        /// one binary search / tie-break and cannot drift.
        /// </summary>
        internal static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
        {
            return MS1Spectrum.FindNearest(ms1Spectra, rt);
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
            OspreyConfig config,
            PipelineContext ctx)
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
                ctx.LogInfo(string.Format(
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


        protected List<FdrEntry> DeduplicatePairs(List<FdrEntry> entries, PipelineContext ctx)
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
                ctx.LogInfo(string.Format("Deduplicated: {0} -> {1} entries ({2} removed)",
                    entries.Count, deduped.Count, removed));
            }

            return deduped;
        }
    }
}
