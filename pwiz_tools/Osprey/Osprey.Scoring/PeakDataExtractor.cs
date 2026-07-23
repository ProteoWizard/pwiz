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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Produces the per-candidate peak data the feature calculators consume:
    /// selects the RT scan range, extracts the fragment XICs, detects and
    /// rank-scores candidate peaks, resolves the winning peak's apex / area / SNR
    /// over the reference XIC, and populates an <see cref="OspreyPeakData"/> view.
    ///
    /// This is the producer seam mirroring Skyline's results layer: the scorer
    /// (<see cref="CoelutionScorer"/>) composes an extractor and consumes its
    /// output, rather than interleaving data preparation with calculator dispatch.
    /// All detection-side diagnostics (search-XIC dump, CWT-path row)
    /// live here with the logic they describe; the median-polish publish and the
    /// 21-calculator pass stay in the scorer.
    /// </summary>
    internal sealed class PeakDataExtractor
    {
        private readonly IScoringDiagnostics _diagnostics;

        public PeakDataExtractor(IScoringDiagnostics diagnostics)
        {
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Build the per-candidate peak data. Returns <c>true</c> and populates
        /// <paramref name="peakData"/> (via <see cref="OspreyPeakData.Set"/>) plus
        /// the <paramref name="extracted"/> side artifacts the scorer needs for
        /// CWT-candidate capture and FdrEntry assembly; returns <c>false</c> when
        /// the candidate has no scorable peak (the scorer then drops it). Mirrors
        /// the detection half of Rust run_search; the arithmetic is byte-identical
        /// to the former inline block in <c>CoelutionScorer.ScoreCandidate</c>.
        /// </summary>
        public bool TryExtract(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            RTCalibration rtCalibration,
            double globalRtTolerance,
            double rtSigma,
            List<MS1Spectrum> ms1Spectra,
            MzCalibrationResult ms1Calibration,
            ScoringContext context,
            OspreyPeakData peakData,
            out ExtractedPeak extracted)
        {
            extracted = default;
            var config = context.Config;
            int nScans = windowSpectra.Count;
            if (nScans < 5)
                return false;

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
            double rtTolerance = globalRtTolerance;

            // Find scan range for XIC extraction. Extracted to FindScanRange
            // (override-bounds vs normal-search filter shapes).
            FindScanRange(overrideBounds, windowRts, nScans, expectedRt, rtTolerance,
                out int startScan, out int endScan);

            if (startScan < 0 || endScan < 0 || endScan - startScan + 1 < 5)
                return false;

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
                    return false;
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
                return false;

            // Detect candidate peaks (3-tier CWT/fallback, or a synthetic peak
            // from override bounds). Extracted to DetectCandidatePeaks; returns
            // null ONLY when an override produced a degenerate index range
            // (the original override `if (peaks == null) return null`).
            List<XICPeakBounds> peaks = DetectCandidatePeaks(
                overrideBounds, xics, out int diagNCwtPeaks);
            if (peaks == null)
                return false;

            if (peaks.Count == 0)
            {
                if (!overrideBounds.HasValue)
                {
                    _diagnostics?.WriteCwtPathRow(
                        context.FileName, candidate.Id,
                        diagNCwtPeaks, 0, 0, false, xics);
                }
                return false;
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

            // Peak-pick model selection. The pure product form is the DEFAULT (Rust parity +
            // committed golden); the learned resolution-keyed linear model is opt-in. Precedence:
            //   1. OSPREY_PICK_LDA_MODEL (env json) -> that model (override for testing new models);
            //   2. else OSPREY_PICK_LDA -> the hardcoded model for this resolution (Stellar for
            //      unit, Astral for HRAM), via context.Resolution.HasMs1Features;
            //   3. else (DEFAULT) -> null == the pure product pick
            //      (coelution * rt_penalty * ln_intensity, no median-polish factor).
            // The model + the per-candidate capture (OSPREY_PICK_DUMP_CANDIDATES) both consume the
            // SAME four raw terms (coelution, ln_intensity, rt_penalty, median_polish); the
            // per-candidate median-polish cosine is computed once below and reused by both.
            // Capture is a first-pass-only concern: the override path builds a single synthetic
            // peak (no CWT candidate set), so it is excluded here just as capturedPeaks below is.
            PickLdaModel envModel = PickLdaModel.Current;
            PickLdaModel pickModel;
            if (envModel != null)
                pickModel = envModel;                                              // (1) env override
            else if (OspreyEnvironment.PickLda)
                pickModel = PickLdaModel.ForResolution(context.Resolution.HasMs1Features); // (2) opt-in model
            else
                pickModel = null;                                                  // (3) default: legacy product pick
            bool modelActive = pickModel != null;
            bool doDump = context.PickDump != null && !overrideBounds.HasValue;
            bool needMedianPolish = modelActive || doDump;
            var dumpRows = doDump ? new List<PickCandidateDump.Row>(peaks.Count) : null;

            int diagNScored = 0; // peaks that pass apex-acceptance
            double twoSigmaSq = 2.0 * rtSigma * rtSigma;
            // Capture every scored peak with its raw coelution score
            // and rank score for the top-N CwtCandidate list assigned
            // by the scorer (Stage 6 reconciliation input). Mirrors Rust
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

                // Per-candidate median-polish cosine (feature 15 computed per candidate, not
                // only for the winner). Computed ONCE here and reused by the linear model and
                // the candidate dump. Neutral 1.0 when neither is active, so it costs nothing on
                // the legacy product path. Same value the dump records.
                double medianPolish = needMedianPolish
                    ? CandidateLibCosine(xics, candidate, p)
                    : 1.0;

                double rankScore;
                if (modelActive)
                {
                    // Learned linear pick model (default, or OSPREY_PICK_LDA_MODEL): replace the
                    // product form with a standardized linear combination of the same four raw
                    // terms -- median_polish is a proper weighted feature here. The argmax +
                    // total-order tie-break below are unchanged.
                    rankScore = pickModel.Score(coelutionScore, intensityWeight, rtPenalty, medianPolish);
                }
                else
                {
                    // Legacy / standard pick (OSPREY_PICK_LEGACY): the pure product form, exactly
                    // coelution * rt_penalty * ln_intensity, with no median-polish factor. This is
                    // the Rust cross-impl-parity / regression-golden pick.
                    rankScore = coelutionScore * rtPenalty * intensityWeight;
                }

                // Per-candidate capture (OSPREY_PICK_DUMP_CANDIDATES). is_picked is filled in
                // after the loop once the winning index is known. Records the EXACT raw terms
                // above so a downstream trainer learns on what the model consumes.
                if (dumpRows != null)
                {
                    dumpRows.Add(new PickCandidateDump.Row
                    {
                        BaseId = candidate.Id & 0x7FFFFFFFu,
                        IsDecoy = (candidate.Id & 0x80000000u) != 0,
                        CandIndex = pi,
                        Coelution = coelutionScore,
                        LnIntensity = intensityWeight,
                        RtPenalty = rtPenalty,
                        MedianPolish = medianPolish,
                        ApexRt = peakApexRt,
                        StartRt = windowRts[startScan + p.StartIndex],
                        EndRt = windowRts[startScan + p.EndIndex],
                    });
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

            // Candidate dump (OSPREY_PICK_DUMP_CANDIDATES): now that the winning peak index is
            // known, mark the chosen candidate and hand this precursor's rows to the per-file
            // thread-safe collector (flushed once, per input mzML, by the orchestrator).
            if (dumpRows != null)
            {
                for (int r = 0; r < dumpRows.Count; r++)
                    dumpRows[r].IsPicked = dumpRows[r].CandIndex == bestPeakIdx;
                context.PickDump.AddRows(dumpRows);
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
                return false;
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
                // recompute correctly via the peak-shape calculators; this
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
                return false;
            }

            var apexSpectrum = windowSpectra[apexGlobalIdx];

            // Publish the per-candidate peak-data view for the calculators. The
            // window-global apex index, candidate-local apex index, window start,
            // and range length carry the index machinery the xcorr / SG family
            // reads; windowRts is shared by reference (no per-candidate copy).
            peakData.Set(candidate, bestPeak, xics, apexSpectrum.RetentionTime, expectedRt, apexSpectrum,
                apexGlobalIdx, bestPeak.ApexIndex, startScan, rangeLen, windowSpectra);

            // Produce the MS1 data the ms1_precursor_coelution / ms1_isotope_cosine
            // features consume (HRAM only) -- the precursor chromatogram, its
            // co-sampled reference fragment chromatogram, and the apex isotope
            // envelope. Lifting this here makes those scores pure consumers (they
            // drop to the Detailed tier) and mirrors how Skyline produces MS1
            // chromatograms upstream. The HRAM gate is the resolution strategy; on
            // unit-resolution runs no MS1 data is produced (the Set reset leaves the
            // accessors null) and both features evaluate to 0.0, as before.
            if (context.Resolution.HasMs1Features)
            {
                ProduceMs1Data(candidate, xics, bestPeak, startScan, windowRts,
                    ms1Spectra, ms1Calibration,
                    out XicData ms1PrecursorXic, out XicData ms1ReferenceXic, out double[] apexEnvelope);
                if (ms1PrecursorXic != null)
                    peakData.SetMs1(ms1PrecursorXic, ms1ReferenceXic, apexEnvelope);
            }

            // Detection-side CWT-path dump for the winning candidate (success
            // row). Content depends only on detection counters, not on the
            // built FdrEntry, so it is emitted here next to the logic it
            // describes; the scorer's downstream feature pass adds no rows.
            if (!overrideBounds.HasValue)
            {
                _diagnostics?.WriteCwtPathRow(
                    context.FileName, candidate.Id,
                    diagNCwtPeaks, peaks.Count, diagNScored, true, xics);
            }

            extracted = new ExtractedPeak(
                xics, refXicIdx, refXicIntensities, bestPeak, startScan, apexSpectrum, capturedPeaks);
            return true;
        }

        /// <summary>
        /// Find the [startScan..endScan] range for XIC extraction. For boundary
        /// overrides: the given boundaries plus margin for SNR context (peak_width
        /// each side, 0.2 min floor; run_search pipeline.rs:6473-6477). For normal
        /// search (Rust commit 885339b): a window wider than rtTolerance so CWT has
        /// context on both sides of any in-tolerance apex; half-width is
        /// rtTolerance + max(rtTolerance, 0.1). Both yield -1/-1 when no scan
        /// matches. The normal branch uses the abs-diff form |rt - expectedRt| &lt;=
        /// xicHalfWidth (NOT a precomputed rtHi compare): the two arithmetic chains
        /// round differently in the last bit, and ~1k entries per Stellar file pick
        /// a different best apex if a single boundary spectrum slips in/out of the
        /// window (cascades through CWT peak detection). Mirrors pipeline.rs:7031-7065.
        /// </summary>
        private static void FindScanRange(
            (double Apex, double Start, double End)? overrideBounds,
            double[] windowRts, int nScans,
            double expectedRt, double rtTolerance,
            out int startScan, out int endScan)
        {
            startScan = -1;
            endScan = -1;
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
        }

        /// <summary>
        /// Library spectral-match term for ONE candidate window: the median-polish cosine of the
        /// cropped peak XICs against the theoretical library fragments -- feature 15 computed per
        /// candidate rather than only for the winner. This is the <c>median_polish</c> term the
        /// learned pick model weights and the OSPREY_PICK_DUMP_CANDIDATES capture records. Crop +
        /// fit mirror <see cref="CoelutionScorer"/>'s best-peak median-polish publish exactly, so
        /// for the chosen peak this equals the reported median_polish_cosine. Neutral (1.0) when
        /// the window is too short, the fit does not converge, or the cosine is NaN/&lt;=0.
        /// Mirrors skyline-osprey-tool OspreyFeatureScorer.WindowTerms.
        /// </summary>
        private static double CandidateLibCosine(List<XicData> xics, LibraryEntry candidate, XICPeakBounds p)
        {
            int peakLen = p.EndIndex - p.StartIndex + 1;
            if (peakLen < 3 || xics.Count == 0)
                return 1.0;

            var peakXics = new List<KeyValuePair<int, double[]>>(xics.Count);
            var peakRts = new double[peakLen];
            for (int s = 0; s < peakLen; s++)
                peakRts[s] = xics[0].RetentionTimes[p.StartIndex + s];
            for (int xi = 0; xi < xics.Count; xi++)
            {
                var src = xics[xi].Intensities;
                var slice = new double[peakLen];
                for (int s = 0; s < peakLen; s++)
                    slice[s] = src[p.StartIndex + s];
                peakXics.Add(new KeyValuePair<int, double[]>(xics[xi].FragmentIndex, slice));
            }

            var polish = TukeyMedianPolish.Compute(peakXics, peakRts, 10, 0.01);
            if (polish == null)
                return 1.0;
            double lc = TukeyMedianPolish.LibCosine(polish, candidate.Fragments);
            return !double.IsNaN(lc) && lc > 0.0 ? lc : 1.0;
        }

        /// <summary>
        /// Detect candidate peaks with a three-tier fallback (matches Rust
        /// pipeline.rs:6244-6259): (1) CWT consensus, (2) peak detection on the
        /// median-polish elution profile, (3) peak detection on the reference
        /// XIC (highest total intensity). For boundary overrides (Stage 6
        /// re-scoring) peak detection is skipped and a single synthetic
        /// <see cref="XICPeakBounds"/> is built from the supplied (apex, start,
        /// end); mirrors run_search at pipeline.rs:6596-6664. Returns null ONLY
        /// when an override produced a degenerate index range; otherwise a
        /// (possibly empty) peak list. <paramref name="diagNCwtPeaks"/> snapshots
        /// the CWT consensus peak count for the OSPREY_DUMP_CWT_PATH dump.
        /// </summary>
        private static List<XICPeakBounds> DetectCandidatePeaks(
            (double Apex, double Start, double End)? overrideBounds,
            List<XicData> xics,
            out int diagNCwtPeaks)
        {
            List<XICPeakBounds> peaks;
            if (overrideBounds.HasValue)
            {
                peaks = BuildOverridePeaks(overrideBounds.Value, xics);
                if (peaks == null)
                {
                    diagNCwtPeaks = 0;
                    return null;
                }
            }
            else
            {
                peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);
            }
            diagNCwtPeaks = peaks.Count;

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

            return peaks;
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

        /// <summary>
        /// Produce the MS1 data for one candidate (HRAM path): the precursor
        /// chromatogram sampled at the nearest MS1 scan along the peak, the
        /// co-sampled reference fragment chromatogram it is correlated against, and
        /// the apex isotope-envelope intensities. Reproduces the former
        /// <c>Ms1ScoringByproduct.Compute</c> sampling exactly (MS1-specific
        /// reference selection seed-0.0 / '&gt;=' last-wins, reverse calibration,
        /// skip-not-zerofill nearest-MS1 sampling, apex isotope envelope), so the
        /// ms1_precursor_coelution / ms1_isotope_cosine features stay byte-identical
        /// -- they now just Pearson / cosine the produced vectors. Outputs are all
        /// null for the degenerate cases (no MS1 spectra, empty XICs, too-short peak)
        /// in which both features are 0.0; <paramref name="apexEnvelope"/> is null
        /// when there is no apex MS1 scan.
        /// </summary>
        private static void ProduceMs1Data(
            LibraryEntry candidate, List<XicData> xics, XICPeakBounds peak,
            int startScan, double[] windowRts,
            List<MS1Spectrum> ms1Spectra, MzCalibrationResult ms1Calibration,
            out XicData precursorXic, out XicData referenceXic, out double[] apexEnvelope)
        {
            precursorXic = null;
            referenceXic = null;
            apexEnvelope = null;

            if (ms1Spectra == null || ms1Spectra.Count == 0 || xics.Count == 0)
                return;
            int len = xics[0].Intensities.Length;
            if (len == 0)
                return;
            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(len - 1, peak.EndIndex);
            if (end - start + 1 < 3)
                return;

            // Reference XIC (highest total intensity). MS1-SPECIFIC selection: seed
            // 0.0, '>=' last-wins -- distinct from the peak-shape selection (seed
            // -1.0) and the harness fallback ('>'). Do not unify.
            int refXicIdx = 0;
            double bestTotal = 0.0;
            for (int f = 0; f < xics.Count; f++)
            {
                double total = 0.0;
                double[] inten = xics[f].Intensities;
                for (int k = 0; k < inten.Length; k++)
                    total += inten[k];
                if (total >= bestTotal) { bestTotal = total; refXicIdx = f; }
            }

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

            // Sample the MS1 precursor intensity along the peak at the nearest MS1
            // scan, skipping (not zero-filling) scans with no MS1, alongside the
            // reference fragment XIC value at the same scan. Rust pipeline.rs:5373-5389
            // (ref_xic[start..=end], skip missing MS1). Every observed intensity is a
            // single float->double widen.
            double[] refIntensities = xics[refXicIdx].Intensities;
            var retainedRts = new List<double>();
            var ms1Intensities = new List<double>();
            var refValues = new List<double>();
            for (int i = start; i <= end; i++)
            {
                double rt = windowRts[startScan + i];
                var ms1 = MS1Spectrum.FindNearest(ms1Spectra, rt);
                if (ms1 != null)
                {
                    var peakInfo = ms1.FindPeakPpm(searchMz, ms1TolPpm);
                    double intensity = peakInfo.HasValue ? peakInfo.Value.Intensity : 0.0;
                    retainedRts.Add(rt);
                    ms1Intensities.Add(intensity);
                    refValues.Add(i < refIntensities.Length ? refIntensities[i] : 0.0);
                }
            }

            // The precursor chromatogram and its co-sampled reference companion share
            // the retained-scan RT axis; the ms1_precursor_coelution Pearson runs over
            // their intensities (the RT axis is the chromatogram's, not used by the
            // correlation). Always produced when the guards pass; the feature applies
            // the >= 3 sample count gate.
            double[] retainedRtArr = retainedRts.ToArray();
            precursorXic = new XicData(-1, retainedRtArr, ms1Intensities.ToArray());
            referenceXic = new XicData(xics[refXicIdx].FragmentIndex, retainedRtArr, refValues.ToArray());

            // Isotope envelope at apex MS1 scan. Rust pipeline.rs:5393-5404 gates on
            // envelope.has_m0(); the feature applies that gate. Computed independently
            // of the precursor sample count, matching the original.
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));
            double apexRt = windowRts[startScan + apex];
            var apexMs1 = MS1Spectrum.FindNearest(ms1Spectra, apexRt);
            if (apexMs1 != null)
            {
                int charge = candidate.Charge > 0 ? candidate.Charge : 1;
                var envelope = IsotopeEnvelope.Extract(apexMs1, searchMz, charge, ms1TolPpm);
                apexEnvelope = envelope.Intensities;
            }
        }
    }

    /// <summary>
    /// The side artifacts a successful <see cref="PeakDataExtractor.TryExtract"/>
    /// hands back to the scorer: the extracted XICs, the reference-XIC selection,
    /// the winning peak, the window start offset, the apex spectrum, and the
    /// per-peak captures for the Stage 6 CWT-candidate list. The populated
    /// per-candidate <see cref="OspreyPeakData"/> view is set separately on the
    /// reused instance the scorer passes in.
    /// </summary>
    internal sealed class ExtractedPeak
    {
        public List<XicData> Xics { get; }
        public int RefXicIdx { get; }
        public double[] RefXicIntensities { get; }
        public XICPeakBounds BestPeak { get; }
        public int StartScan { get; }
        public Spectrum ApexSpectrum { get; }
        public List<(XICPeakBounds peak, double coelutionScore, double rankScore)> CapturedPeaks { get; }

        public ExtractedPeak(
            List<XicData> xics, int refXicIdx, double[] refXicIntensities,
            XICPeakBounds bestPeak, int startScan, Spectrum apexSpectrum,
            List<(XICPeakBounds peak, double coelutionScore, double rankScore)> capturedPeaks)
        {
            Xics = xics;
            RefXicIdx = refXicIdx;
            RefXicIntensities = refXicIntensities;
            BestPeak = bestPeak;
            StartScan = startScan;
            ApexSpectrum = apexSpectrum;
            CapturedPeaks = capturedPeaks;
        }
    }
}
