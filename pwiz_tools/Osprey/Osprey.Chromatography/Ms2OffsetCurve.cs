/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Chromatography
{
    /// <summary>
    /// One library fragment of a calibration match and the peaks around it in the match's apex
    /// spectrum: the input of <see cref="Ms2OffsetCurve"/>.
    /// </summary>
    public sealed class Ms2FragmentPeaks
    {
        /// <param name="libraryMz">The fragment's library (theoretical) m/z.</param>
        /// <param name="charge">The fragment's charge; 0 (unknown) counts as 1.</param>
        /// <param name="offsets">
        /// Observed m/z minus <paramref name="libraryMz"/> for every peak within
        /// <see cref="Ms2OffsetCurve.SCAN_HALF_WIDTH_TH"/>, in ascending order.
        /// </param>
        /// <param name="intensities">Each peak's intensity, in the order of <paramref name="offsets"/>.</param>
        /// <remarks>
        /// Single precision: the calibration holds these for every scored target match, and a float
        /// is exact to well under a thousandth of a Th over the few Th collected.
        /// </remarks>
        public Ms2FragmentPeaks(double libraryMz, int charge, float[] offsets, float[] intensities)
        {
            LibraryMz = libraryMz;
            Charge = Math.Max(1, charge);
            Offsets = offsets ?? Array.Empty<float>();
            Intensities = intensities ?? Array.Empty<float>();
            if (Intensities.Length != Offsets.Length)
                throw new ArgumentException(@"offsets and intensities must have the same length");
        }

        /// <summary>The fragment's library (theoretical) m/z.</summary>
        public double LibraryMz { get; }

        /// <summary>The fragment's charge, at least 1.</summary>
        public int Charge { get; }

        /// <summary>Observed m/z minus <see cref="LibraryMz"/> of the peaks around it, ascending.</summary>
        public float[] Offsets { get; }

        /// <summary>The intensity of each of <see cref="Offsets"/>.</summary>
        public float[] Intensities { get; }

        /// <summary>The most intense peak within <paramref name="tolerance"/> of <paramref name="offset"/>; 0 if none.</summary>
        public double MaxIntensityNear(double offset, double tolerance)
        {
            double max = 0;
            for (int i = 0; i < Offsets.Length; i++)
            {
                if (Math.Abs(Offsets[i] - offset) <= tolerance && Intensities[i] > max)
                    max = Intensities[i];
            }
            return max;
        }
    }

    /// <summary>
    /// The MS2 m/z calibration of unit-resolution (Th) data: an offset that depends on m/z, found
    /// as the shift that puts the most library fragments on their monoisotopic peak, then fitted
    /// across m/z by LOESS, as the RT calibration is.
    /// <para>
    /// Why not the mean of each fragment's closest peak within the fragment tolerance, as for
    /// ppm data: on a linear ion trap the offset can be as large as that tolerance. A Stellar's
    /// 200 kTh/s MS2 run 0.4-0.7 Th low against a 0.5 Th tolerance, so the true peak sits on the
    /// window's edge and the closest peak is as often the one above it; the mean of a window
    /// truncated that way comes out near zero (+0.08 Th on a Stellar run whose Thermo centroids
    /// sit 0.6-0.7 Th low). Counting matches at each trial shift has no such bias: the shift that
    /// matches most fragments is the offset, wherever in the scan range it lies.
    /// </para>
    /// <para>
    /// An offset near half a Th also sits between a fragment's isotopes, so a shift one isotope
    /// spacing away matches the M+1 nearly as well. The monoisotopic peak is the one with nothing
    /// larger one isotope spacing below it (there is no M-1, and below about 1,800 Da the
    /// monoisotopic peak is the larger): the shift is stepped down while most singly charged
    /// fragments show a larger peak one spacing below (<see cref="MonoisotopicShift"/>).
    /// </para>
    /// <para>
    /// Experimental, opt-in (OSPREY_MS2_CAL_CURVE_PPM): the same curve for ppm searches, with
    /// widths scaled from the ppm tolerance and no M+1 check (<see cref="Widths.Ppm"/>). Centered
    /// on the bulk of the fragment errors rather than their mean, it gave 5.7% more precursors on
    /// a demultiplexed ZenoTOF 8600 ZT Scan run whose errors are skewed. See docs/04-calibration.md.
    /// </para>
    /// </summary>
    public static class Ms2OffsetCurve
    {
        /// <summary>The shift scan's range: trial shifts run from minus this to plus this.</summary>
        public const double SHIFT_RANGE_TH = 1.3;

        /// <summary>The shift scan's step.</summary>
        public const double SCAN_STEP_TH = 0.01;

        /// <summary>A fragment counts as matched at a trial shift when a peak lies this close to it.</summary>
        public const double HIT_TOLERANCE_TH = 0.2;

        /// <summary>The spacing of a singly charged fragment's isotopes (13C - 12C).</summary>
        public const double ISOTOPE_SPACING_TH = 1.0033548;

        /// <summary>
        /// Peaks within this of a fragment's library m/z are collected: the shift range, one
        /// isotope spacing below it for the monoisotopic check, and the hit tolerance.
        /// </summary>
        public const double SCAN_HALF_WIDTH_TH = SHIFT_RANGE_TH + ISOTOPE_SPACING_TH + HIT_TOLERANCE_TH;

        /// <summary>
        /// A fragment's offset is its peak closest to the curve within this: half the spacing of
        /// unit-resolution peaks, so a neighbouring ion's or isotope's peak is never taken for it.
        /// </summary>
        public const double ASSOCIATION_HALF_WIDTH_TH = 0.5;

        /// <summary>Fewer fragments than this with a peak to associate, and no curve is fitted.</summary>
        public const int MIN_FRAGMENTS = 100;

        /// <summary>Points the curve is stored at, evenly spaced over the fragments' m/z range.</summary>
        public const int KNOT_COUNT = 21;

        /// <summary>
        /// The share of matched singly charged fragments with a larger peak one isotope spacing
        /// below, above which a shift is taken for the M+1 and stepped down. On a Stellar run it is
        /// 1-2% at the monoisotopic shift and 72-83% at the M+1's.
        /// </summary>
        public const double M_PLUS_1_FRACTION = 0.5;

        /// <summary>
        /// The share of the fragment tolerance within which the mean sees an offset whole: an offset
        /// inside half the window the mean is taken over. The curve is used at any offset; where too
        /// few fragments lie near a larger one to fit it, the mean the calibration falls back to
        /// cannot see that offset, and the calibrator warns.
        /// </summary>
        public const double MEAN_WINDOW_FRACTION = 0.5;

        /// <summary>
        /// The widths an offset curve is found with, in the unit of its fragments' offsets: the
        /// shift scan's range, step and hit tolerance, the association half-width, whether the
        /// shift is checked for the M+1, and the shift beyond which the mean cannot see it.
        /// </summary>
        public sealed class Widths
        {
            private Widths(string unit, double shiftRange, double scanStep, double hitTolerance,
                double associationHalfWidth, bool stepIsotopes, double meanWindow)
            {
                Unit = unit;
                ShiftRange = shiftRange;
                ScanStep = scanStep;
                HitTolerance = hitTolerance;
                AssociationHalfWidth = associationHalfWidth;
                StepIsotopes = stepIsotopes;
                MeanWindow = meanWindow;
            }

            /// <summary>
            /// Unit-resolution (Th) data searched with <paramref name="fragmentToleranceTh"/>: the
            /// constants of <see cref="Ms2OffsetCurve"/>, the M+1 check, and the mean seeing an offset
            /// within <see cref="MEAN_WINDOW_FRACTION"/> of the tolerance.
            /// </summary>
            public static Widths UnitResolution(double fragmentToleranceTh)
            {
                return new Widths(MzCalibration.UNIT_TH, SHIFT_RANGE_TH, SCAN_STEP_TH, HIT_TOLERANCE_TH,
                    ASSOCIATION_HALF_WIDTH_TH, true, MEAN_WINDOW_FRACTION * fragmentToleranceTh);
            }

            /// <summary>
            /// Experimental (OSPREY_MS2_CAL_CURVE_PPM): HRAM data searched with
            /// <paramref name="fragmentTolerancePpm"/>, offsets in ppm. The scan covers twice the
            /// tolerance either way in hundredths of it, a hit is within a quarter of it, a fragment's
            /// offset is its peak within the tolerance of the curve, there is no M+1 check (resolved
            /// isotopes lie thousands of ppm apart), and no shift is warned about when the curve
            /// cannot be fitted.
            /// </summary>
            public static Widths Ppm(double fragmentTolerancePpm)
            {
                return new Widths(MzCalibration.UNIT_PPM, 2 * fragmentTolerancePpm, fragmentTolerancePpm / 100,
                    fragmentTolerancePpm / 4, fragmentTolerancePpm, false, -1);
            }

            /// <summary>The unit of the offsets and of the calibration found.</summary>
            public string Unit { get; }

            /// <summary>Trial shifts run from minus this to plus this.</summary>
            public double ShiftRange { get; }

            /// <summary>The shift scan's step.</summary>
            public double ScanStep { get; }

            /// <summary>A fragment counts as matched at a trial shift when a peak lies this close to it.</summary>
            public double HitTolerance { get; }

            /// <summary>A fragment's offset is its peak closest to the curve within this.</summary>
            public double AssociationHalfWidth { get; }

            /// <summary>Whether the shift is stepped down off the M+1 (<see cref="MonoisotopicShift"/>).</summary>
            public bool StepIsotopes { get; }

            /// <summary>
            /// The largest shift the mean sees whole: beyond it, a calibration left to the mean for
            /// want of fragments to fit the curve is warned about. Negative, never.
            /// </summary>
            public double MeanWindow { get; }

            /// <summary>Peaks within this of a fragment's library m/z are collected for it.</summary>
            public double CollectionHalfWidth
            {
                get { return ShiftRange + (StepIsotopes ? ISOTOPE_SPACING_TH : 0) + HitTolerance; }
            }
        }

        /// <summary>
        /// The calibration of <paramref name="fragments"/>: the offset curve
        /// (<see cref="MzCalibrationResult.OffsetCurveMz"/>, <see cref="MzCalibrationResult.OffsetCurve"/>),
        /// its mean over the associated fragments as <see cref="MzCalibrationResult.Mean"/>, the
        /// SD of the fragments' offsets about it, and 3 SD as the tolerance (the curve itself
        /// is subtracted), whatever the offset. Null, so the caller keeps the mean, when too few
        /// fragments have a peak near the offset.
        /// </summary>
        /// <param name="fragments">The calibration matches' top fragments and the peaks around them.</param>
        /// <param name="fragmentToleranceTh">
        /// The fragment tolerance the mean's errors are collected within, which sets the offset the
        /// mean sees (<see cref="Widths.MeanWindow"/>).
        /// </param>
        /// <param name="shift">The monoisotopic shift the curve starts from, NaN with too few fragments.</param>
        public static MzCalibrationResult Calculate(IReadOnlyList<Ms2FragmentPeaks> fragments,
            double fragmentToleranceTh, out double shift)
        {
            return Calculate(fragments, Widths.UnitResolution(fragmentToleranceTh), out shift);
        }

        /// <summary>
        /// The calibration of <paramref name="fragments"/>, whose offsets are in the unit of
        /// <paramref name="widths"/>, found with its widths: as the overload taking a Th tolerance
        /// for unit-resolution data, and the experimental ppm curve (<see cref="Widths.Ppm"/>).
        /// </summary>
        public static MzCalibrationResult Calculate(IReadOnlyList<Ms2FragmentPeaks> fragments,
            Widths widths, out double shift)
        {
            shift = double.NaN;
            if (fragments == null || fragments.Count(f => f.Offsets.Length > 0) < MIN_FRAGMENTS)
                return null;

            shift = BestShift(fragments, widths);
            if (widths.StepIsotopes)
                shift = MonoisotopicShift(fragments, shift);
            double start = shift;
            double[] mz, offset;
            Associate(fragments, _ => start, widths.AssociationHalfWidth, out mz, out offset);
            if (mz.Length < MIN_FRAGMENTS)
                return null;
            // LOESS at its default bandwidth (0.3), as the RT calibration fits.
            var model = LoessRegression.Fit(mz, offset);
            // Once more about the fitted curve: where the offset changes with m/z, the first
            // association, about one shift, reaches the wrong peak at the ends of the range.
            Associate(fragments, model.Predict, widths.AssociationHalfWidth, out mz, out offset);
            if (mz.Length < MIN_FRAGMENTS)
                return null;
            model = LoessRegression.Fit(mz, offset);

            // The SD of the fragments' offsets about the curve, as the mean's SD is of the errors about
            // it: a robust (MAD) SD misses the heavy tails of unit-resolution centroids, and the
            // 3 SD tolerance it gives (0.18 Th on the Stellar test subset, against 0.33 Th) loses
            // most of a predicted library's matches.
            var fitted = mz.Select(model.Predict).ToArray();
            var residuals = offset.Select((o, i) => o - fitted[i]).ToArray();
            double sd = LoessRegression.StdDev(residuals);

            double lo = mz.Min(), hi = mz.Max();
            var knotMz = new double[KNOT_COUNT];
            var knotOffset = new double[KNOT_COUNT];
            for (int k = 0; k < KNOT_COUNT; k++)
            {
                knotMz[k] = lo + (hi - lo) * k / (KNOT_COUNT - 1);
                knotOffset[k] = model.Predict(knotMz[k]);
            }
            return new MzCalibrationResult
            {
                Mean = fitted.Average(),
                Median = LoessRegression.Median(offset),
                SD = sd,
                Count = mz.Length,
                Unit = widths.Unit,
                AdjustedTolerance = 3.0 * sd,
                Calibrated = true,
                OffsetCurveMz = knotMz,
                OffsetCurve = knotOffset
            };
        }

        /// <summary>
        /// The shift, on a <see cref="SCAN_STEP_TH"/> grid within <see cref="SHIFT_RANGE_TH"/>, at
        /// which the most fragments have a peak within <see cref="HIT_TOLERANCE_TH"/>. Every shift
        /// within the tolerance of a tight cluster of peaks matches as many, so of a run of shifts
        /// that tie, the middle; of runs that tie, the one nearest zero.
        /// </summary>
        public static double BestShift(IReadOnlyList<Ms2FragmentPeaks> fragments)
        {
            return BestShift(fragments, Widths.UnitResolution(0));
        }

        /// <summary>
        /// The shift on the <see cref="Widths.ScanStep"/> grid of <paramref name="widths"/>, within its
        /// <see cref="Widths.ShiftRange"/>, at which the most fragments have a peak within its
        /// <see cref="Widths.HitTolerance"/>; among ties, as the unit-resolution overload chooses.
        /// </summary>
        public static double BestShift(IReadOnlyList<Ms2FragmentPeaks> fragments, Widths widths)
        {
            double step = widths.ScanStep, hit = widths.HitTolerance;
            double first = -widths.ShiftRange;
            int steps = (int)Math.Round(2 * widths.ShiftRange / step) + 1;
            var hits = new int[steps];
            var lastFragment = Enumerable.Repeat(-1, steps).ToArray();   // a fragment counts once per shift
            for (int f = 0; f < fragments.Count; f++)
            {
                foreach (double o in fragments[f].Offsets)
                {
                    // Grid shifts s with |o - s| <= HIT_TOLERANCE_TH; the epsilon keeps a peak exactly
                    // at the tolerance in, whichever way the division rounds.
                    int from = Math.Max(0, (int)Math.Ceiling((o - hit - first) / step - 1e-9));
                    int to = Math.Min(steps - 1, (int)Math.Floor((o + hit - first) / step + 1e-9));
                    for (int s = from; s <= to; s++)
                    {
                        if (lastFragment[s] == f)
                            continue;
                        lastFragment[s] = f;
                        hits[s]++;
                    }
                }
            }
            int max = hits.Max();
            double best = double.NaN;
            for (int s = 0; s < steps; s++)
            {
                if (hits[s] != max)
                    continue;
                int end = s;
                while (end + 1 < steps && hits[end + 1] == max)
                    end++;
                double middle = first + (s + end) / 2.0 * step;
                if (double.IsNaN(best) || Math.Abs(middle) < Math.Abs(best))
                    best = middle;
                s = end;
            }
            return best;
        }

        /// <summary>
        /// <paramref name="shift"/>, stepped down by the isotope spacing while it sits on the M+1:
        /// while more than <see cref="M_PLUS_1_FRACTION"/> of the singly charged fragments with a peak
        /// there have a larger peak one spacing below, and the step stays within the scan range.
        /// Left as it is when fewer than <see cref="MIN_FRAGMENTS"/> fragments are singly charged:
        /// the step is the singly charged spacing, and a shift that landed on the M+1 of mostly 2+
        /// fragments lies only half of it above their monoisotopic peak.
        /// </summary>
        public static double MonoisotopicShift(IReadOnlyList<Ms2FragmentPeaks> fragments, double shift)
        {
            var singly = fragments.Where(f => f.Charge == 1).ToList();
            if (singly.Count < MIN_FRAGMENTS)
                return shift;
            while (shift - ISOTOPE_SPACING_TH >= -SHIFT_RANGE_TH - HIT_TOLERANCE_TH &&
                   LargerPeakBelowFraction(singly, shift) > M_PLUS_1_FRACTION)
            {
                shift -= ISOTOPE_SPACING_TH;
            }
            return shift;
        }

        /// <summary>
        /// Of the fragments with a peak within <see cref="HIT_TOLERANCE_TH"/> of <paramref name="shift"/>,
        /// the share whose most intense peak one isotope spacing below is more intense than the one
        /// there; 0 when too few have a peak there to tell. The fragments are singly charged.
        /// </summary>
        private static double LargerPeakBelowFraction(IReadOnlyList<Ms2FragmentPeaks> fragments, double shift)
        {
            int present = 0, below = 0;
            foreach (var fragment in fragments)
            {
                double here = fragment.MaxIntensityNear(shift, HIT_TOLERANCE_TH);
                if (here <= 0)
                    continue;
                present++;
                if (fragment.MaxIntensityNear(shift - ISOTOPE_SPACING_TH, HIT_TOLERANCE_TH) > here)
                    below++;
            }
            return present < MIN_FRAGMENTS ? 0 : below / (double)present;
        }

        /// <summary>
        /// Each fragment's peak closest to <paramref name="expected"/>(its m/z), within
        /// <paramref name="halfWidth"/>, as (fragment m/z, offset) pairs; a fragment with no
        /// peak that close is left out. Of two peaks as close, the lower.
        /// </summary>
        private static void Associate(IReadOnlyList<Ms2FragmentPeaks> fragments, Func<double, double> expected,
            double halfWidth, out double[] mz, out double[] offset)
        {
            var mzList = new List<double>(fragments.Count);
            var offsetList = new List<double>(fragments.Count);
            foreach (var fragment in fragments)
            {
                double center = expected(fragment.LibraryMz);
                double bestDistance = double.MaxValue;
                double bestOffset = 0;
                foreach (double o in fragment.Offsets)
                {
                    double distance = Math.Abs(o - center);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestOffset = o;
                    }
                }
                if (bestDistance <= halfWidth)
                {
                    mzList.Add(fragment.LibraryMz);
                    offsetList.Add(bestOffset);
                }
            }
            mz = mzList.ToArray();
            offset = offsetList.ToArray();
        }
    }
}
