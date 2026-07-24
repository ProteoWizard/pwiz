/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results
{
    public class IntensityAccumulator
    {
        public double TotalIntensity { get; set; }
        public double MeanMassError { get; set; }

        // Summed extractor with IM tracking: intensity-weighted running mean of the
        // observed IM values across the extraction band - the "intensity center of
        // gravity" of the mobilogram (per the original feature request), accumulated
        // exactly the way MeanMassError is. Carried across spectra at the same
        // retention time by the caller (SpectrumFilterPair) via this property.
        public double MeanObservedIonMobility { get; set; }

        // Base-peak extractor with IM tracking: IM at the single highest-intensity
        // peak ever seen. Carried across spectra by the caller via this property.
        public double BasePeakIonMobility { get; set; }

        bool _highAcc;
        bool _trackIonMobility;
        ChromExtractor _extractor;
        private double _targetMz;

        public IntensityAccumulator(bool highAcc, ChromExtractor extractor, double targetMz,
            bool trackIonMobility = false)
        {
            _highAcc = highAcc;
            _extractor = extractor;
            _targetMz = targetMz;
            _trackIonMobility = trackIonMobility;
        }

        public void AddPoint(double mz, double intensity, double? ionMobility = null)
        {
            bool basePeakReset = false;
            if (_extractor == ChromExtractor.summed)
                TotalIntensity += intensity;
            else if (intensity > TotalIntensity)
            {
                TotalIntensity = intensity;
                MeanMassError = 0;
                BasePeakIonMobility = 0;
                basePeakReset = true;
            }

            if (TotalIntensity <= 0.0)
                return;

            // Accumulate weighted mean mass error for summed, or take a single
            // mass error of the most intense peak for base peak.
            if (_highAcc && (_extractor == ChromExtractor.summed || MeanMassError == 0))
            {
                double deltaPeakMz = mz - _targetMz;
                MeanMassError += (deltaPeakMz - MeanMassError) * intensity / TotalIntensity;
            }

            // Track observed ion mobility the same way mass error is tracked above:
            // for summed extraction, an intensity-weighted running mean of the IM
            // values across the extraction band (the "intensity center of gravity"
            // of the mobilogram, per the original feature request); for base-peak
            // extraction, the IM at the single strongest peak. The mean is taken in
            // the native IM unit rather than in CCS: for the linear converters
            // (Bruker 1/K0, Agilent drift time - Mason-Schamp) it equals a CCS-space
            // mean, and for a nonlinear TWIMS calibration the deviation over a single
            // narrow mobility peak is second-order (Jensen, ~0.01%). Observed CCS is
            // derived per-peak later, where the precursor charge is available.
            if (_trackIonMobility && ionMobility.HasValue)
            {
                if (_extractor == ChromExtractor.summed)
                    MeanObservedIonMobility += (ionMobility.Value - MeanObservedIonMobility) * intensity / TotalIntensity;
                else if (basePeakReset)
                    BasePeakIonMobility = ionMobility.Value;
            }
        }

        /// <summary>
        /// Final observed IM for this accumulator: the intensity-weighted mean IM
        /// (center of gravity) for summed extraction, or the IM at the strongest
        /// peak for base-peak extraction.
        /// </summary>
        public double ObservedIonMobility
        {
            get
            {
                if (!_trackIonMobility)
                    return 0;
                return _extractor == ChromExtractor.summed ? MeanObservedIonMobility : BasePeakIonMobility;
            }
        }

        public override string ToString() // Debug convenience, not user facing
        {
            return $@"mz:{_targetMz} i:{TotalIntensity} mzErr:{MeanMassError:F6} im:{ObservedIonMobility:F6}";
        }

        // Cross-isotope abundance weighting is intentionally not done here - each channel
        // records its own faithful observed IM, and the predicted-abundance-weighted
        // combination into a single per-ion value happens at the precursor level
        // (see PrecursorResult.ObservedIonMobility).
    }

    /// <summary>
    /// Peak-shape metrics for a mobilogram - the (ion mobility -> summed intensity) histogram
    /// at a peak: apex <see cref="Height"/> and <see cref="Area"/> measured above a noise
    /// baseline, and <see cref="FullWidthHalfMax"/>, all in the histogram's native IM units.
    /// A single mobility peak is assumed (interference / isomers are not resolved). Ill-formed
    /// peaks (noise) are handled by baseline subtraction and by withholding FWHM when the peak
    /// is too narrow to characterize (see <see cref="MIN_BINS_ABOVE_HALF_MAX"/>).
    /// </summary>
    public readonly struct MobilogramPeakMetrics
    {
        // FWHM is reported only when at least this many bins rise above the half-maximum level;
        // fewer means a single-bin spike / noise rather than a resolved peak.
        public const int MIN_BINS_ABOVE_HALF_MAX = 3;

        public MobilogramPeakMetrics(double height, double area, double? fullWidthHalfMax)
        {
            Height = height;
            Area = area;
            FullWidthHalfMax = fullWidthHalfMax;
        }

        /// <summary>Apex intensity above the noise baseline.</summary>
        public double Height { get; }
        /// <summary>Baseline-subtracted area under the mobilogram curve (intensity integrated over IM).</summary>
        public double Area { get; }
        /// <summary>Full width at half maximum in native IM units, or null when the peak is too
        /// ill-formed to measure (fewer than <see cref="MIN_BINS_ABOVE_HALF_MAX"/> bins above half-max).</summary>
        public double? FullWidthHalfMax { get; }

        /// <summary>
        /// Computes metrics from an (IM -> summed intensity) histogram. Returns null when there
        /// are fewer than two points or no signal above baseline. The baseline is the minimum bin
        /// intensity; Height and Area are measured above it, so a noisy floor neither inflates the
        /// area nor counts as signal. FWHM is measured at the half-max level (halfway from baseline
        /// to apex) by interpolating the crossing on each side, but only when at least
        /// <see cref="MIN_BINS_ABOVE_HALF_MAX"/> bins exceed that level (otherwise the "peak" is a
        /// spike / noise and FWHM is left null). When the curve does not fall to half-max before
        /// the band edge (peak truncated by the IM window), that edge is used.
        /// </summary>
        public static MobilogramPeakMetrics? Compute(IEnumerable<KeyValuePair<double, double>> imIntensityHistogram)
        {
            if (imIntensityHistogram == null)
                return null;
            var points = new List<KeyValuePair<double, double>>(imIntensityHistogram);
            if (points.Count < 2)
                return null;
            points.Sort((a, b) => a.Key.CompareTo(b.Key));

            int apex = 0;
            double apexValue = points[0].Value;
            double baseline = points[0].Value;
            for (int i = 1; i < points.Count; i++)
            {
                double v = points[i].Value;
                if (v > apexValue)
                {
                    apexValue = v;
                    apex = i;
                }
                if (v < baseline)
                    baseline = v;
            }
            double height = apexValue - baseline;
            if (height <= 0)
                return null;

            // Baseline-subtracted trapezoidal area (baseline is the min, so terms are >= 0).
            double area = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dIm = points[i].Key - points[i - 1].Key;
                area += dIm * ((points[i].Value - baseline) + (points[i - 1].Value - baseline)) / 2.0;
            }

            // FWHM at the level halfway from baseline to apex, gated on a minimum peak width.
            double halfLevel = baseline + height / 2.0;
            int binsAboveHalf = 0;
            foreach (var p in points)
            {
                if (p.Value > halfLevel)
                    binsAboveHalf++;
            }
            double? fwhm = null;
            if (binsAboveHalf >= MIN_BINS_ABOVE_HALF_MAX)
            {
                double leftIm = points[0].Key;
                for (int i = apex; i > 0; i--)
                {
                    if (points[i - 1].Value <= halfLevel)
                    {
                        leftIm = InterpolateCrossing(points[i - 1], points[i], halfLevel);
                        break;
                    }
                }
                double rightIm = points[points.Count - 1].Key;
                for (int i = apex; i < points.Count - 1; i++)
                {
                    if (points[i + 1].Value <= halfLevel)
                    {
                        rightIm = InterpolateCrossing(points[i], points[i + 1], halfLevel);
                        break;
                    }
                }
                fwhm = Math.Abs(rightIm - leftIm);
            }
            return new MobilogramPeakMetrics(height, area, fwhm);
        }

        // Linear interpolation of the IM at which the segment a->b crosses the given intensity.
        private static double InterpolateCrossing(KeyValuePair<double, double> a,
            KeyValuePair<double, double> b, double crossingIntensity)
        {
            double dv = b.Value - a.Value;
            if (Math.Abs(dv) < double.Epsilon)
                return a.Key;
            double t = (crossingIntensity - a.Value) / dv;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return a.Key + t * (b.Key - a.Key);
        }
    }
}
