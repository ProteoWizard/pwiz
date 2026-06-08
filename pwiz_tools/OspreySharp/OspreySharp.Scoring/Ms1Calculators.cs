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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Shared MS1-family scoring pass for ms1_precursor_coelution (feature 13) and
    /// ms1_isotope_cosine (feature 14). Both features come from ONE pass that
    /// reverse-calibrates the precursor search m/z, selects the reference XIC, samples
    /// the MS1 precursor intensity along the peak, and reads the apex isotope envelope
    /// -- the inline <c>ComputeMs1Features</c>. The producer runs it once and the two
    /// calculators read its fields, so the calibration math, the reference-XIC pick,
    /// and the nearest-MS1 lookups happen exactly once and identically. Published per
    /// candidate to the <see cref="OspreyScoringContext"/> byproduct cache.
    ///
    /// PARITY TRAPS preserved here (byte-faithful to AbstractScoringTask.cs
    /// ComputeMs1Features, Rust pipeline.rs:5362-5404):
    ///  * HRAM GATE: both features are exactly 0.0 unless the run has MS1 features and a
    ///    non-empty MS1 spectrum list. Enforced by the calculators (which short-circuit
    ///    to 0.0 before GetOrCompute) AND mirrored by the inner guards here.
    ///  * REFERENCE-XIC SELECTION is MS1-SPECIFIC: seed total 0.0, '&gt;=' tie-break
    ///    (LAST fragment achieving the running max wins). This is DISTINCT from the
    ///    peak-shape family's selection (seed -1.0 / different tie-break) and from the
    ///    harness fallback ('&gt;' / seed 0.0). It is computed INSIDE this byproduct and is
    ///    NOT shared with peak-shape -- do not route it through any shared ref-XIC info.
    ///  * MISSING MS1 scans are SKIPPED, not zero-filled, so the Pearson sample count is
    ///    the number of present MS1 scans, not the peak width.
    ///  * Every observed intensity is a single float-&gt;double widen (MzIntensityPair.Intensity
    ///    is float). Do not cache as double end-to-end.
    ///  * Pearson uses ScoringMath.PearsonCorrelationInRange (denom guard &lt; 1e-10),
    ///    NOT the &lt; 1e-30 overload; NaN-&gt;0.0 is applied feature-side.
    ///  * Calibration branch uses plain string equality Unit == "Th" (matches inline).
    ///  * baseTolPpm = 10.0 is hardcoded (NOT promoted to Config) to preserve parity.
    ///  * The nearest-MS1 search is the single Core implementation
    ///    <see cref="MS1Spectrum.FindNearest"/> (binary search RetentionTime &lt; rt,
    ///    '&lt;=' tie-break) shared with the harness so the tie-break cannot drift.
    /// </summary>
    internal sealed class Ms1ScoringByproduct
    {
        public double PrecursorCoelution;
        public double IsotopeCosine;

        public static Ms1ScoringByproduct GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out Ms1ScoringByproduct byproduct))
                return byproduct;
            byproduct = Compute(context, peakData);
            context.AddInfo(byproduct);
            return byproduct;
        }

        private static Ms1ScoringByproduct Compute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            // Both feature values default to 0.0 (mirrors the inline
            // ms1PrecursorCoelution / ms1IsotopeCosine seeds). Inner early-returns
            // below leave them at 0.0.
            var result = new Ms1ScoringByproduct();

            var ms1Spectra = context.Ms1Spectra;
            var xics = peakData.Xics;

            // Inner guards. The outer HRAM gate is applied by the calculators; this
            // re-checks the data guards so a direct GetOrCompute is still safe.
            if (ms1Spectra == null || ms1Spectra.Count == 0 || xics.Count == 0)
                return result;

            int len = xics[0].Intensities.Length;
            if (len == 0)
                return result;

            var peak = peakData.PeakBounds;
            int start = Math.Max(0, peak.StartIndex);
            int end = Math.Min(len - 1, peak.EndIndex);
            if (end - start + 1 < 3)
                return result;

            // Reference XIC (highest total intensity). MS1-SPECIFIC selection: seed 0.0,
            // '>=' last-wins.
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
            double searchMz = peakData.Candidate.PrecursorMz;
            var ms1Calibration = context.Ms1Calibration;
            if (ms1Calibration != null && ms1Calibration.Calibrated)
            {
                // calibrated_tolerance_ppm: max(3*SD, 1.0) ppm
                ms1TolPpm = Math.Max(3.0 * ms1Calibration.SD, 1.0);
                // reverse_calibrate_mz: observed ~ theoretical + offset
                if (ms1Calibration.Unit == "Th")
                    searchMz = peakData.Candidate.PrecursorMz + ms1Calibration.Mean;
                else
                    searchMz = peakData.Candidate.PrecursorMz * (1.0 + ms1Calibration.Mean / 1e6);
            }

            // Map an XIC scan index i to its absolute RT via the window axis:
            // windowRts[startScan + i]. Reproduces the inline ComputeMs1Features
            // indexing exactly (no per-candidate slice copy).
            var windowRts = peakData.WindowRetentionTimes;
            int startScan = peakData.WindowStartIndex;

            // Correlate MS1 precursor intensity with reference XIC (not summed fragment).
            // Rust pipeline.rs:5373-5389: uses ref_xic[start..=end], skips missing MS1.
            double[] refIntensities = xics[refXicIdx].Intensities;
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
                    ms1Intensities.Add(intensity);
                    refValues.Add(i < refIntensities.Length ? refIntensities[i] : 0.0);
                }
            }

            if (ms1Intensities.Count >= 3)
            {
                double[] ms1Arr = ms1Intensities.ToArray();
                double[] refArr = refValues.ToArray();
                double coelution = ScoringMath.PearsonCorrelationInRange(ms1Arr, refArr, 0, ms1Arr.Length - 1);
                if (double.IsNaN(coelution))
                    coelution = 0.0;
                result.PrecursorCoelution = coelution;
            }

            // Isotope cosine at apex MS1 scan.
            // Rust pipeline.rs:5393-5404: gates on envelope.has_m0().
            int apex = Math.Max(start, Math.Min(end, peak.ApexIndex));
            double apexRt = windowRts[startScan + apex];
            var apexMs1 = MS1Spectrum.FindNearest(ms1Spectra, apexRt);
            if (apexMs1 != null)
            {
                int charge = peakData.Candidate.Charge > 0 ? peakData.Candidate.Charge : 1;
                var envelope = IsotopeEnvelope.Extract(apexMs1, searchMz, charge, ms1TolPpm);

                // Gate: skip if M0 peak is missing (matches Rust envelope.has_m0()).
                if (envelope.Intensities != null && envelope.Intensities.Length > 1
                    && envelope.Intensities[1] > 0.0) // index 1 = M0 (M-1 at 0)
                {
                    // Sequence-based isotope distribution, matching Rust
                    // pipeline.rs:5400 peptide_isotope_cosine. Uses the UNMODIFIED Sequence.
                    double score = IsotopeDistribution.PeptideIsotopeCosine(
                        peakData.Candidate.Sequence, envelope.Intensities);
                    if (score >= 0.0)
                        result.IsotopeCosine = score;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// ms1_precursor_coelution (feature 13): Pearson correlation between the MS1
    /// precursor-intensity trace (sampled at the nearest MS1 scan along the peak) and
    /// the reference fragment XIC. HRAM-only -- exactly 0.0 for unit-resolution runs or
    /// when no MS1 spectra are present. See <see cref="Ms1ScoringByproduct"/> for the
    /// parity traps (seed-0.0 / '&gt;=' reference-XIC pick, skip-not-zerofill sampling,
    /// the &lt; 1e-10 Pearson denominator guard with feature-side NaN-&gt;0.0).
    /// </summary>
    internal sealed class Ms1PrecursorCoelutionCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "ms1_precursor_coelution"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            // HRAM gate: no MS1 features -> exactly 0.0, do not even build the byproduct.
            if (!context.HasMs1Features || context.Ms1Spectra == null || context.Ms1Spectra.Count == 0)
                return 0.0;

            return Ms1ScoringByproduct.GetOrCompute(context, peakData).PrecursorCoelution;
        }
    }

    /// <summary>
    /// ms1_isotope_cosine (feature 14): cosine similarity between the observed isotope
    /// envelope at the apex MS1 scan and the theoretical averagine envelope for the
    /// candidate sequence. HRAM-only -- exactly 0.0 for unit-resolution runs or when no
    /// MS1 spectra are present. The M0 gate (Intensities[1] &gt; 0.0) and the
    /// PeptideIsotopeCosine score &gt;= 0.0 acceptance are preserved in
    /// <see cref="Ms1ScoringByproduct"/> (a composition failure or zero-norm returns
    /// a negative score there, so the feature stays 0.0). Uses the UNMODIFIED
    /// Candidate.Sequence.
    /// </summary>
    internal sealed class Ms1IsotopeCosineCalc : DetailedOspreyFeatureCalculator
    {
        public override string Name { get { return "ms1_isotope_cosine"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            // Same HRAM gate as feature 13.
            if (!context.HasMs1Features || context.Ms1Spectra == null || context.Ms1Spectra.Count == 0)
                return 0.0;

            return Ms1ScoringByproduct.GetOrCompute(context, peakData).IsotopeCosine;
        }
    }
}
