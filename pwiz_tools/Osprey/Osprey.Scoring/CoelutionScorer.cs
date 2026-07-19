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
using System.Linq;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
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
    ///
    /// Data production (RT window, XICs, peak detection, apex/reference-XIC
    /// resolution) is delegated to <see cref="PeakDataExtractor"/> -- the producer
    /// seam mirroring Skyline's results layer. This class composes the extractor,
    /// then runs the 21-feature calculator pass over the resulting peak-data view
    /// and assembles the FdrEntry; it no longer interleaves data preparation with
    /// calculator dispatch.
    /// </summary>
    public class CoelutionScorer
    {
        private readonly IScoringDiagnostics _diagnostics;   // nullable by contract; invoked null-conditionally
        private readonly PeakDataExtractor _extractor;

        public CoelutionScorer(IScoringDiagnostics diagnostics)
        {
            _diagnostics = diagnostics;
            _extractor = new PeakDataExtractor(diagnostics);
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

            // Sort spectra by RT for XIC extraction, with ScanNumber as a unique
            // secondary key so the comparator never returns 0 -- an allocation-free
            // total order that matches Rust's stable slice::sort_by on the scan-ordered
            // window (tie hazard #4362).
            windowSpectra.Sort((a, b) => // Array.Sort OK: (RetentionTime, ScanNumber) is a unique total order (ScanNumber unique per scan), so the comparator never ties
            {
                int byRt = a.RetentionTime.CompareTo(b.RetentionTime);
                return byRt != 0 ? byRt : a.ScanNumber.CompareTo(b.ScanNumber);
            });

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
                        ms1Spectra, ms1Calibration,
                        context,
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


        /// <summary>
        /// Score one candidate: ask the <see cref="PeakDataExtractor"/> for the
        /// per-candidate peak-data view (RT window, XICs, winning peak, apex), then
        /// publish the median-polish fit, run the 21 PIN feature calculators over
        /// that view, and assemble the <see cref="FdrEntry"/>. Returns null when
        /// the extractor found no scorable peak. The detection arithmetic and its
        /// diagnostics live in the extractor; only the median-polish publish (which
        /// is harness-owned because its bisection dump is exe-layer) and the
        /// calculator pass remain here.
        /// </summary>
        private FdrEntry ScoreCandidate(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            RTCalibration rtCalibration,
            double globalRtTolerance,
            double rtSigma,
            List<MS1Spectrum> ms1Spectra,
            MzCalibrationResult ms1Calibration,
            ScoringContext context,
            OspreyScoringContext ospreyContext,
            OspreyPeakData ospreyPeakData)
        {
            if (!_extractor.TryExtract(
                    candidate, windowSpectra, windowRts, rtCalibration,
                    globalRtTolerance, rtSigma, ms1Spectra, ms1Calibration,
                    context, ospreyPeakData, out var ext))
                return null;

            var bestPeak = ext.BestPeak;
            var xics = ext.Xics;
            var apexSpectrum = ext.ApexSpectrum;

            // The extractor has populated ospreyPeakData via Set. Reset the
            // per-candidate byproduct cache (carried over from the previous
            // candidate) BEFORE the median-polish publish below so that byproduct
            // survives to the calculators. Calculator-backed features mirror
            // Skyline's IPeakFeatureCalculator.
            ospreyContext.ClearByproducts();

            // Tukey median-polish inputs + fit (features 15, 16, 19, 20). The crop,
            // WriteMpInputsRow, and Compute stay here because the bisection
            // diagnostics (OspreyDiagnostics) live in the exe layer that
            // Osprey.Scoring cannot reference; the four feature values are
            // computed by the calculators from the published MedianPolishByproduct,
            // and the optional WriteMpDump fires after the feature vector below.
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
            List<CwtCandidate> cwtCandidatesOut = CaptureCwtCandidates(
                ext.CapturedPeaks, xics, ext.RefXicIdx, ext.RefXicIntensities, context);

            // Build FdrEntry from the winning peak + feature vector (frag
            // blobs, reference-XIC slice, bounds). Extracted to BuildFdrEntry.
            var entry = BuildFdrEntry(
                candidate, bestPeak, features, cwtCandidatesOut,
                xics, ext.RefXicIdx, ext.RefXicIntensities, windowRts, ext.StartScan, apexSpectrum);

            return entry;
        }


        /// <summary>
        /// Capture the top-N CWT peak candidates (Stage 6 reconciliation input),
        /// ranked by penalized rank score, with each kept candidate's apex / area
        /// / SNR recomputed over the reference-XIC slice. Mirrors Rust run_search
        /// at pipeline.rs:6852-6879. The stored coelution_score is the raw mean
        /// (NOT the RT-penalized rank score) -- reconciliation has its own RT
        /// tolerance logic via consensus RT comparison. Returns null when there
        /// is nothing to capture (override path / no captured peaks / TopN == 0).
        /// </summary>
        private static List<CwtCandidate> CaptureCwtCandidates(
            List<(XICPeakBounds peak, double coelutionScore, double rankScore)> capturedPeaks,
            List<XicData> xics,
            int refXicIdx,
            double[] refXicIntensities,
            ScoringContext context)
        {
            int topN = context.Config.Reconciliation != null
                ? context.Config.Reconciliation.TopNPeaks
                : 0;
            if (capturedPeaks == null || capturedPeaks.Count == 0 || topN <= 0)
                return null;

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
            var ordered = capturedPeaks
                .OrderByDescending(p => p.rankScore, TotalOrder.Comparer)
                .ToList();
            int kept = Math.Min(topN, ordered.Count);
            var cwtCandidatesOut = new List<CwtCandidate>(kept);
            double[] refRts = xics[refXicIdx].RetentionTimes;
            for (int k = 0; k < kept; k++)
            {
                var cap = ordered[k];
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
            return cwtCandidatesOut;
        }


        /// <summary>
        /// Build the <see cref="FdrEntry"/> for a scored candidate's winning peak.
        /// Serializes the full library fragment list (m/z + relative intensity),
        /// slices the reference XIC across the winning peak's [start..end] window,
        /// and copies the feature vector + bounds onto the entry. Pure: no logging,
        /// no diagnostics -- the six blob/scalar fields mirror Rust
        /// CoelutionScoredEntry::{fragment_mzs, fragment_intensities, reference_xic,
        /// peak.area, peak.signal_to_noise} so the reconciled .scores.parquet
        /// write-back stays byte-identical for cross-impl validation.
        /// </summary>
        private static FdrEntry BuildFdrEntry(
            LibraryEntry candidate,
            XICPeakBounds bestPeak,
            double[] features,
            List<CwtCandidate> cwtCandidatesOut,
            List<XicData> xics,
            int refXicIdx,
            double[] refXicIntensities,
            double[] windowRts,
            int startScan,
            Spectrum apexSpectrum)
        {
            // FragmentMzs / FragmentIntensities iterate the FULL library
            // fragment list (not just the top-N used by XIC extraction)
            // because Rust's parquet writer at pipeline.rs:1620-1631
            // serializes every library fragment.
            var frags = candidate.Fragments;
            int nFrags = frags?.Count ?? 0;
            double[] fragMzs = new double[nFrags];
            float[] fragInts = new float[nFrags];
            if (frags != null)
            {
                for (int fi = 0; fi < nFrags; fi++)
                {
                    fragMzs[fi] = frags[fi].Mz;
                    fragInts[fi] = frags[fi].RelativeIntensity;
                }
            }

            // ReferenceXic{Rts,Intensities} are sliced from the highest-
            // total-intensity fragment XIC across the winning peak's
            // [si..=ei] window, matching Rust's
            // `ref_xic[peak.start_index..=peak.end_index].to_vec()` at
            // pipeline.rs:6538. Use the SAFE indices (clipped by the
            // post-rank apex recompute) for non-override entries; the
            // override path's bestPeak retains its original boundaries.
            double[] refXicRtsAll = xics[refXicIdx].RetentionTimes;
            double[] refXicRts;
            double[] refXicInts;
            if (refXicRtsAll == null || refXicIntensities == null ||
                refXicRtsAll.Length == 0 || refXicIntensities.Length == 0)
            {
                refXicRts = new double[0];
                refXicInts = new double[0];
            }
            else
            {
                int refMaxLen = Math.Min(refXicRtsAll.Length, refXicIntensities.Length);
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

            return new FdrEntry
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
        }
    }
}
