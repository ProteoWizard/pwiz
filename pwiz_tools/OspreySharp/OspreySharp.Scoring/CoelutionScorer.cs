/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Linq;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Per-window + per-candidate coelution scoring engine. Relocated verbatim
    /// out of AbstractScoringTask, which carried these as instance members
    /// coupled to the exe-layer PipelineContext for logging and to
    /// OspreyDiagnostics for the per-entry dump sink. Those two ambient
    /// dependencies are now injected: logging via an <see cref="Action{T}"/>
    /// sink, and the dump sink via <see cref="IScoringDiagnostics"/> (nullable;
    /// invoked null-conditionally). The arithmetic, Parallel usage, CWT / peak /
    /// feature logic, and tie-breaks are unchanged, so cross-impl parity is
    /// unaffected.
    /// </summary>
    public class CoelutionScorer
    {
        private readonly Action<string> _logInfo;
        private readonly IScoringDiagnostics _diagnostics;   // nullable by contract; invoked null-conditionally

        public CoelutionScorer(Action<string> logInfo, IScoringDiagnostics diagnostics)
        {
            _logInfo = logInfo ?? (_ => { });
            _diagnostics = diagnostics;
        }


        /// <summary>
        /// Score all candidate library entries within a single isolation window.
        /// For each candidate:
        /// 1. Extract fragment XICs from spectra in this window
        /// 2. Detect consensus CWT peaks
        /// 3. Score XCorr and LibCosine at the best peak apex
        /// 4. Build feature set and create FdrEntry
        /// </summary>
        public List<FdrEntry> ScoreWindow(
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

            // Reused per-window scoring context + peak-data adapter for the
            // modular feature calculators. The per-candidate loop mutates them in
            // place (ClearByproducts + Set) so scoring allocates no context /
            // peak-data per candidate. Windows are scored on separate threads
            // (Parallel.For above), so these are window-local, never shared task
            // state.
            var ospreyContext = new OspreyScoringContext(config);
            ospreyContext.SetWindow(context.Resolution, preprocessedXcorr, scorer,
                context.XcorrScratchPool);
            ospreyContext.SetMs1Machinery(context.Resolution.HasMs1Features, ms1Spectra, ms1Calibration);
            var ospreyPeakData = new OspreyPeakData();

            try
            {
                // Score each candidate
                foreach (var candidate in candidates)
                {
                    var fdrEntry = ScoreCandidate(
                        candidate, windowSpectra, windowRts,
                        rtCalibration,
                        globalRtTolerance, rtSigma,
                        scorer, context,
                        ospreyContext, ospreyPeakData);

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


        private FdrEntry ScoreCandidate(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            RTCalibration rtCalibration,
            double globalRtTolerance,
            double rtSigma,
            SpectralScorer scorer,
            ScoringContext context,
            OspreyScoringContext ospreyContext,
            OspreyPeakData ospreyPeakData)
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
            // Bisection seam DISABLED (perf hotspot): this dumped
            // (entry_id, library_rt -> expected_rt) for every per-window
            // candidate. In the per-candidate inner loop even the gated-off
            // call cost a call + branch each candidate, so it is commented
            // out rather than routed through the diagnostics sink. To
            // restore, re-enable this and the paired WritePredictRtArrays /
            // ClosePredictRtDump in PerFileRescoreTask, and remove the
            // OSPREY_DUMP_PREDICT_RT guard (NotImplementedException) in
            // OspreyFileDiagnostics's constructor. Mirrors Rust's
            // dump_predict_rt_call at pipeline.rs ~7014. See
            // ai/todos/active/TODO-20260606_ospreysharp_diagnostics_di.md.
            // OspreyDiagnostics.WritePredictRtCall(
            //     candidate.Id, candidate.RetentionTime, expectedRt);
            double rtTolerance = globalRtTolerance;

            if (diag)
            {
                _logInfo(string.Format(
                    "[DIAG] {0} charge {1}: library_rt={2:F3}, expected_rt={3:F3}, tolerance={4:F3}",
                    candidate.ModifiedSequence, candidate.Charge,
                    candidate.RetentionTime, expectedRt, rtTolerance));
                _logInfo(string.Format(
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
                    _logInfo(string.Format(
                        "[DIAG] {0}: scan range [{1}..{2}] RT [{3:F3}..{4:F3}] ({5} scans)",
                        candidate.ModifiedSequence, startScan, endScan,
                        windowRts[startScan], windowRts[endScan],
                        endScan - startScan + 1));
                    _logInfo(string.Format(
                        "[DIAG] {0}: spectrum scan_numbers in range: first={1}, last={2}",
                        candidate.ModifiedSequence,
                        windowSpectra[startScan].ScanNumber,
                        windowSpectra[endScan].ScanNumber));
                }
                else
                {
                    _logInfo(string.Format(
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
            var xics = TopFragmentExtractor.ExtractFragmentXics(
                candidate, windowSpectra, windowRts, startScan, endScan, config);

            // Per-entry search XIC diagnostic. Fires for every scoring
            // path; if the entry is scored twice (consensus + override)
            // the LAST call wins on disk. Caller can isolate by
            // limiting OSPREY_DIAG_SEARCH_ENTRY_IDS to the right
            // entries, or by tagging the dump filename with the path.
            if (_diagnostics?.ShouldDumpSearchXicFor(candidate.Id) ?? false)
            {
                _diagnostics?.WriteSearchXicDump(
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
                _logInfo(string.Format(
                    "[DIAG] {0}: xics extracted={1}, peaks={2}",
                    candidate.ModifiedSequence, xics.Count, peaks.Count));
                for (int i = 0; i < peaks.Count; i++)
                {
                    var p = peaks[i];
                    int apexAbsIdx = startScan + p.ApexIndex;
                    double apexRt = windowRts[apexAbsIdx];
                    uint apexScanNum = windowSpectra[apexAbsIdx].ScanNumber;
                    _logInfo(string.Format(
                        "[DIAG] {0}: peak[{1}] apex_local={2} apex_rt={3:F3} scan#={4} range=[{5}..{6}]",
                        candidate.ModifiedSequence, i, p.ApexIndex,
                        apexRt, apexScanNum, p.StartIndex, p.EndIndex));
                }
            }
            if (peaks.Count == 0)
            {
                if (!overrideBounds.HasValue)
                {
                    _diagnostics?.WriteCwtPathRow(
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
                    _logInfo(string.Format(
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
                if (TotalOrder.Greater(rankScore, bestRankScore))
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
                _logInfo(string.Format(
                    "[DIAG] {0}: WINNER peak[{1}] apex_rt={2:F3} scan#={3}",
                    candidate.ModifiedSequence, bestPeakIdx,
                    windowRts[apexAbsIdx], windowSpectra[apexAbsIdx].ScanNumber));
            }

            // Append peak boundaries to search XIC diagnostic dump
            if (_diagnostics?.ShouldDumpSearchXicFor(candidate.Id) ?? false)
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
                    _diagnostics?.WriteCwtPathRow(
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
                    _diagnostics?.WriteCwtPathRow(
                        context.FileName, candidate.Id,
                        diagNCwtPeaks, peaks.Count, diagNScored, false, xics);
                }
                return null;
            }

            var apexSpectrum = windowSpectra[apexGlobalIdx];

            // LibCosine at apex
            double libCosine = scorer.LibCosine(apexSpectrum, candidate, config.FragmentTolerance);

            // Count fragment matches (top-6 dedup byproduct, NOT a PIN feature;
            // stays inline). consecutive_ions / explained_intensity / mass-accuracy
            // (features 7-10) moved to the apex-match calculators below.
            byte top6Matches = TopFragmentExtractor.CountTop6Matches(candidate, apexSpectrum, config);

            // MS1 features (precursor coelution, isotope cosine) are computed by the
            // MS1 calculators (features 13, 14) below from the published per-window
            // MS1 machinery (ospreyContext.SetMs1Machinery) and the window RT axis
            // (windowRts) passed to ospreyPeakData.Set. The HRAM gate (Rust
            // pipeline.rs:5362 is_hram) lives in the calculators.

            // Per-candidate state for the modular feature calculators. Reused
            // window-local instances (see RunCoelutionScoring); ClearByproducts
            // resets the per-candidate byproduct cache. Done BEFORE the
            // median-polish publish below so that byproduct survives to the
            // calculators. Calculator-backed features mirror Skyline's
            // IPeakFeatureCalculator; the rest stay inline until extracted.
            // windowRts is passed by reference (no per-candidate copy); the MS1
            // family maps an XIC index i to an absolute RT via windowRts[startScan + i]
            // (= WindowRetentionTimes[WindowStartIndex + i]).
            ospreyContext.ClearByproducts();
            ospreyPeakData.Set(candidate, bestPeak, xics, apexSpectrum.RetentionTime, expectedRt, apexSpectrum,
                apexGlobalIdx, bestPeak.ApexIndex, startScan, rangeLen, windowSpectra, windowRts);

            // Tukey median-polish inputs + fit (features 15, 16, 19, 20). The crop,
            // WriteMpInputsRow, and Compute stay here because the bisection
            // diagnostics live in the exe layer that OspreySharp.Scoring cannot
            // reference; the four feature values are computed by the calculators
            // from the published MedianPolishByproduct, and the optional
            // WriteMpDump fires after the feature vector below.
            // Crop XICs to the peak range so the polish operates only on signal,
            // not the wider RT search window. Matches Rust pipeline.rs:5198-5212.
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
                _diagnostics?.WriteMpInputsRow(
                    candidate.Id, apexSpectrum.ScanNumber, peakXics, peakRts);

                var polish = TukeyMedianPolish.Compute(peakXics, peakRts, 10, 0.01);
                // Only publish when the fit converged. Every consumer (the four
                // calculators and the WriteMpDump guard) treats a missing byproduct
                // and a byproduct with null Polish identically (family default / no
                // dump), so skipping the publish on a null fit is value-identical and
                // avoids the allocation.
                if (polish != null)
                    ospreyContext.AddInfo(new MedianPolishByproduct(polish, peakXics));
            }

            // Build full 21-element PIN feature vector
            double[] features = new double[OspreyFeatureCalculators.FeatureCount];
            features[0] = OspreyFeatureCalculators.Get(0).Calculate(ospreyContext, ospreyPeakData);
            features[1] = OspreyFeatureCalculators.Get(1).Calculate(ospreyContext, ospreyPeakData);
            features[2] = OspreyFeatureCalculators.Get(2).Calculate(ospreyContext, ospreyPeakData);
            features[3] = OspreyFeatureCalculators.Get(3).Calculate(ospreyContext, ospreyPeakData);
            features[4] = OspreyFeatureCalculators.Get(4).Calculate(ospreyContext, ospreyPeakData);
            features[5] = OspreyFeatureCalculators.Get(5).Calculate(ospreyContext, ospreyPeakData);
            features[6] = OspreyFeatureCalculators.Get(6).Calculate(ospreyContext, ospreyPeakData);
            features[7] = OspreyFeatureCalculators.Get(7).Calculate(ospreyContext, ospreyPeakData);
            features[8] = OspreyFeatureCalculators.Get(8).Calculate(ospreyContext, ospreyPeakData);
            features[9] = OspreyFeatureCalculators.Get(9).Calculate(ospreyContext, ospreyPeakData);
            features[10] = OspreyFeatureCalculators.Get(10).Calculate(ospreyContext, ospreyPeakData);
            features[11] = OspreyFeatureCalculators.Get(11).Calculate(ospreyContext, ospreyPeakData);
            features[12] = OspreyFeatureCalculators.Get(12).Calculate(ospreyContext, ospreyPeakData);
            features[13] = OspreyFeatureCalculators.Get(13).Calculate(ospreyContext, ospreyPeakData);
            features[14] = OspreyFeatureCalculators.Get(14).Calculate(ospreyContext, ospreyPeakData);
            features[15] = OspreyFeatureCalculators.Get(15).Calculate(ospreyContext, ospreyPeakData);
            features[16] = OspreyFeatureCalculators.Get(16).Calculate(ospreyContext, ospreyPeakData);
            features[17] = OspreyFeatureCalculators.Get(17).Calculate(ospreyContext, ospreyPeakData);
            features[18] = OspreyFeatureCalculators.Get(18).Calculate(ospreyContext, ospreyPeakData);
            features[19] = OspreyFeatureCalculators.Get(19).Calculate(ospreyContext, ospreyPeakData);
            features[20] = OspreyFeatureCalculators.Get(20).Calculate(ospreyContext, ospreyPeakData);

            // Median-polish bisection dump (after the feature vector so the four
            // values are available). Reads the polish + cropped inputs from the
            // byproduct published above; gated by ShouldDumpMpFor as before. The
            // standard parity gate compares Stage 7 + blib, not the -d dumps.
            if (peakLen >= 3
                && ospreyContext.TryGetInfo(out MedianPolishByproduct mpByproduct)
                && mpByproduct.Polish != null
                && (_diagnostics?.ShouldDumpMpFor(apexSpectrum.ScanNumber, candidate.ModifiedSequence) ?? false))
            {
                _diagnostics?.WriteMpDump(
                    candidate, apexSpectrum.ScanNumber,
                    bestPeak, peakLen,
                    features[15], features[16], features[19], features[20],
                    mpByproduct.Polish, mpByproduct.PeakXics);
            }

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
                // TotalOrder.Comparer to match Rust byte-for-byte.
                capturedPeaks = capturedPeaks
                    .OrderByDescending(p => p.rankScore, TotalOrder.Comparer)
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
                CoelutionSum = features[0],
                Score = features[0],
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
                _diagnostics?.WriteCwtPathRow(
                    context.FileName, candidate.Id,
                    diagNCwtPeaks, peaks.Count, diagNScored, true, xics);
            }
            return entry;
        }


        // Diagnostic: log detailed trace for a specific peptide. Set this to a
        // peptide modified sequence to dump its RT window, XICs, CWT peaks, and
        // winning peak selection. Used for bisecting divergences with Rust.
        private const string DIAG_PEPTIDE = "AAAAAAAAAAAAAAAGAGAGAK";


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
    }
}
