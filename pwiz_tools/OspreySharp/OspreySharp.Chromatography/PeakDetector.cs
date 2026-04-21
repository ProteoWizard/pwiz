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
using System.Linq;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// Simple peak detector for coefficient time series.
    /// Ported from osprey-chromatography/src/lib.rs PeakDetector.
    /// </summary>
    public class PeakDetector
    {
        /// <summary>Minimum peak height threshold.</summary>
        private double _minHeight;

        /// <summary>Minimum peak width in scans.</summary>
        private int _minWidth;

        /// <summary>
        /// Create a new peak detector with default settings
        /// (minHeight = 0.01, minWidth = 3).
        /// </summary>
        public PeakDetector()
        {
            _minHeight = 0.01;
            _minWidth = 3;
        }

        /// <summary>Minimum peak height threshold.</summary>
        public double MinHeight
        {
            get { return _minHeight; }
            set { _minHeight = value; }
        }

        /// <summary>Minimum peak width in scans.</summary>
        public int MinWidth
        {
            get { return _minWidth; }
            set { _minWidth = value; }
        }

        /// <summary>
        /// Detect peaks in a coefficient time series.
        ///
        /// Input: list of (retention_time, coefficient) pairs.
        /// Output: list of peak boundaries.
        /// </summary>
        public List<PeakBoundaries> Detect(List<(double rt, double coef)> series)
        {
            List<PeakBoundaries> peaks = new List<PeakBoundaries>();
            if (series.Count == 0)
                return peaks;

            bool inPeak = false;
            int peakStartIdx = 0;
            int apexIdx = 0;
            double apexValue = 0.0;

            for (int i = 0; i < series.Count; i++)
            {
                double coef = series[i].coef;

                if (coef >= _minHeight)
                {
                    if (!inPeak)
                    {
                        // Start of new peak
                        inPeak = true;
                        peakStartIdx = i;
                        apexIdx = i;
                        apexValue = coef;
                    }
                    else if (coef > apexValue)
                    {
                        // New apex
                        apexIdx = i;
                        apexValue = coef;
                    }
                }
                else if (inPeak)
                {
                    // End of peak
                    int peakEndIdx = Math.Max(i - 1, 0);
                    if (peakEndIdx - peakStartIdx + 1 >= _minWidth)
                    {
                        double area = TrapezoidalAreaFromTuples(series, peakStartIdx, peakEndIdx);
                        peaks.Add(new PeakBoundaries
                        {
                            StartRt = series[peakStartIdx].rt,
                            EndRt = series[peakEndIdx].rt,
                            ApexRt = series[apexIdx].rt,
                            ApexCoefficient = apexValue,
                            IntegratedArea = area,
                            PeakQuality = new PeakQuality()
                        });
                    }
                    inPeak = false;
                }
            }

            // Handle peak at end of series
            if (inPeak)
            {
                int peakEndIdx = series.Count - 1;
                if (peakEndIdx - peakStartIdx + 1 >= _minWidth)
                {
                    double area = TrapezoidalAreaFromTuples(series, peakStartIdx, peakEndIdx);
                    peaks.Add(new PeakBoundaries
                    {
                        StartRt = series[peakStartIdx].rt,
                        EndRt = series[peakEndIdx].rt,
                        ApexRt = series[apexIdx].rt,
                        ApexCoefficient = apexValue,
                        IntegratedArea = area,
                        PeakQuality = new PeakQuality()
                    });
                }
            }

            return peaks;
        }

        /// <summary>
        /// Find the best peak near the expected retention time.
        /// </summary>
        public PeakBoundaries FindBestPeak(
            List<PeakBoundaries> peaks,
            double expectedRt,
            double rtTolerance)
        {
            PeakBoundaries best = null;
            foreach (PeakBoundaries p in peaks)
            {
                if (Math.Abs(p.ApexRt - expectedRt) <= rtTolerance)
                {
                    if (best == null || p.ApexCoefficient > best.ApexCoefficient)
                        best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Compute trapezoidal area from parallel RT and value arrays over a range.
        ///
        /// area = sum( (v[i] + v[i+1]) / 2 * (rt[i+1] - rt[i]) )
        /// </summary>
        /// <param name="rts">Retention time array.</param>
        /// <param name="values">Value array (intensity, etc.).</param>
        /// <param name="startIdx">Start index (inclusive).</param>
        /// <param name="endIdx">End index (inclusive).</param>
        /// <returns>Trapezoidal area.</returns>
        public static double TrapezoidalArea(double[] rts, double[] values, int startIdx, int endIdx)
        {
            if (endIdx <= startIdx)
                return endIdx == startIdx ? values[startIdx] : 0.0;

            double area = 0.0;
            for (int i = startIdx; i < endIdx; i++)
            {
                double dt = rts[i + 1] - rts[i];
                double avgHeight = (values[i] + values[i + 1]) / 2.0;
                area += avgHeight * dt;
            }
            return area;
        }

        /// <summary>
        /// Compute trapezoidal area from a list of (RT, value) pairs.
        ///
        /// area = sum( (v[i] + v[i+1]) / 2 * (rt[i+1] - rt[i]) )
        /// </summary>
        public static double TrapezoidalArea(List<(double rt, double value)> series)
        {
            if (series.Count < 2)
                return series.Count == 1 ? series[0].value : 0.0;

            double area = 0.0;
            for (int i = 0; i < series.Count - 1; i++)
            {
                double dt = series[i + 1].rt - series[i].rt;
                double avgHeight = (series[i].value + series[i + 1].value) / 2.0;
                area += avgHeight * dt;
            }
            return area;
        }

        /// <summary>
        /// Compute SNR: apex intensity minus background mean, divided by background standard deviation.
        ///
        /// Background is estimated from 5 points immediately before the peak start
        /// and 5 points immediately after the peak end.
        /// </summary>
        /// <param name="intensities">Full intensity array.</param>
        /// <param name="apexIdx">Index of the peak apex.</param>
        /// <param name="startIdx">Index of the peak start.</param>
        /// <param name="endIdx">Index of the peak end.</param>
        /// <returns>Signal-to-noise ratio (>= 0).</returns>
        public static double ComputeSnr(double[] intensities, int apexIdx, int startIdx, int endIdx)
        {
            // Collect background points
            List<double> bgPoints = new List<double>();

            int leftStart = Math.Max(startIdx - 5, 0);
            for (int i = leftStart; i < startIdx; i++)
                bgPoints.Add(intensities[i]);

            int rightEnd = Math.Min(endIdx + 6, intensities.Length);
            for (int i = endIdx + 1; i < rightEnd; i++)
                bgPoints.Add(intensities[i]);

            if (bgPoints.Count == 0)
                return 0.0;

            double n = bgPoints.Count;
            double bgMean = 0.0;
            for (int i = 0; i < bgPoints.Count; i++)
                bgMean += bgPoints[i];
            bgMean /= n;

            double bgVar = 0.0;
            for (int i = 0; i < bgPoints.Count; i++)
            {
                double diff = bgPoints[i] - bgMean;
                bgVar += diff * diff;
            }
            double bgSd = Math.Sqrt(bgVar / n);

            if (bgSd > 1e-10)
            {
                return Math.Max(0.0, (intensities[apexIdx] - bgMean) / bgSd);
            }

            // Background is flat
            double signal = intensities[apexIdx] - bgMean;
            if (signal > 0.0 && bgMean > 1e-10)
                return signal / bgMean;
            if (signal > 0.0)
                return signal * 100.0;
            return 0.0;
        }

        /// <summary>
        /// Compute trapezoidal area from a subrange of a tuple list.
        /// </summary>
        private static double TrapezoidalAreaFromTuples(
            List<(double rt, double coef)> series,
            int startIdx,
            int endIdx)
        {
            if (endIdx <= startIdx)
                return endIdx == startIdx ? series[startIdx].coef : 0.0;

            double area = 0.0;
            for (int i = startIdx; i < endIdx; i++)
            {
                double dt = series[i + 1].rt - series[i].rt;
                double avgHeight = (series[i].coef + series[i + 1].coef) / 2.0;
                area += avgHeight * dt;
            }
            return area;
        }

        /// <summary>
        /// Smooth a time series with a 5-point Savitzky-Golay quadratic filter.
        /// Coefficients: [-3, 12, 17, 12, -3] / 35. Endpoints (first 2 and last 2
        /// points) are left unsmoothed. Negative values from the filter are
        /// clamped to zero. Series shorter than 5 points are returned unsmoothed.
        /// Direct port of Rust smooth_savitzky_golay (osprey-chromatography/src/lib.rs:191).
        /// </summary>
        public static double[] SmoothSavitzkyGolay(double[] values)
        {
            int n = values.Length;
            if (n < 5)
            {
                double[] copy = new double[n];
                Array.Copy(values, copy, n);
                return copy;
            }
            double[] outArr = new double[n];
            outArr[0] = values[0];
            outArr[1] = values[1];
            outArr[n - 2] = values[n - 2];
            outArr[n - 1] = values[n - 1];
            for (int i = 2; i < n - 2; i++)
            {
                double v = (-3.0 * values[i - 2]
                    + 12.0 * values[i - 1]
                    + 17.0 * values[i]
                    + 12.0 * values[i + 1]
                    + -3.0 * values[i + 2]) / 35.0;
                outArr[i] = Math.Max(v, 0.0);
            }
            return outArr;
        }

        /// <summary>
        /// Detect ALL candidate peaks in a XIC, returning them sorted by apex
        /// intensity descending. Each local maximum above minHeight produces a
        /// candidate peak with its own boundaries (valley detection + FWHM
        /// capping). The caller should evaluate each candidate using coelution
        /// scoring and pick the best one. Direct port of Rust detect_all_xic_peaks
        /// (osprey-chromatography/src/lib.rs:437).
        /// </summary>
        /// <param name="rts">Retention times (length = xic length).</param>
        /// <param name="intensities">Intensities (length = xic length).</param>
        /// <param name="minHeight">Minimum apex intensity after smoothing.</param>
        /// <param name="peakBoundary">Intensity divisor for boundary threshold (5.0 means 20% of apex).</param>
        public static List<XICPeakBounds> DetectAllXicPeaks(
            double[] rts, double[] intensities,
            double minHeight, double peakBoundary)
        {
            var peaks = new List<XICPeakBounds>();
            int n = intensities.Length;
            if (n < 3)
                return peaks;

            double[] smoothed = SmoothSavitzkyGolay(intensities);

            // Collect local maxima above threshold. Interior first, then endpoints
            // - matches Rust ordering in detect_all_xic_peaks.
            var apexCandidates = new List<KeyValuePair<int, double>>();
            for (int i = 1; i < n - 1; i++)
            {
                if (smoothed[i] >= minHeight
                    && smoothed[i] >= smoothed[i - 1]
                    && smoothed[i] >= smoothed[i + 1])
                {
                    apexCandidates.Add(new KeyValuePair<int, double>(i, smoothed[i]));
                }
            }
            // Always at leat 3 smoothed points (checked above)
            if (smoothed[0] >= minHeight && smoothed[0] >= smoothed[1])
                apexCandidates.Add(new KeyValuePair<int, double>(0, smoothed[0]));
            if (smoothed[n - 1] >= minHeight && smoothed[n - 1] >= smoothed[n - 2])
                apexCandidates.Add(new KeyValuePair<int, double>(n - 1, smoothed[n - 1]));

            // Sort by intensity descending. Rust uses `sort_by(b.1.total_cmp(&a.1))`
            // which is a STABLE sort - ties keep their insertion order. .NET's
            // List<T>.Sort is unstable (introsort); LINQ's OrderByDescending is
            // stable, so use that to match Rust exactly.
            apexCandidates = apexCandidates.OrderByDescending(kv => kv.Value).ToList();

            // Background = min(smoothed), clamped to >= 0.
            double background = double.PositiveInfinity;
            for (int i = 0; i < n; i++)
                if (smoothed[i] < background)
                    background = smoothed[i];
            if (background < 0.0 || double.IsInfinity(background))
                background = 0.0;

            foreach (var kv in apexCandidates)
            {
                int apexIdx = kv.Key;
                double apexIntensity = kv.Value;

                double signalAboveBg = Math.Max(apexIntensity - background, 0.0);
                double boundaryThreshold = background + signalAboveBg / peakBoundary;

                int startIdx = WalkBoundaryLeft(smoothed, apexIdx, apexIntensity, boundaryThreshold);
                int endIdx = WalkBoundaryRight(smoothed, apexIdx, apexIntensity, boundaryThreshold);

                // FWHM capping: cap boundaries at apex +/- 2 * half-width
                double leftHw, rightHw;
                if (ComputeAsymmetricHalfWidths(smoothed, rts, apexIdx, out leftHw, out rightHw))
                {
                    const double capFactor = 2.0;
                    double apexRt = rts[apexIdx];

                    double minStartRt = apexRt - capFactor * leftHw;
                    if (rts[startIdx] < minStartRt)
                    {
                        for (int i = startIdx; i < apexIdx; i++)
                        {
                            if (rts[i] >= minStartRt)
                            {
                                startIdx = i;
                                break;
                            }
                        }
                    }

                    double maxEndRt = apexRt + capFactor * rightHw;
                    if (rts[endIdx] > maxEndRt)
                    {
                        for (int i = endIdx; i >= apexIdx; i--)
                        {
                            if (rts[i] <= maxEndRt)
                            {
                                endIdx = i;
                                break;
                            }
                        }
                    }
                }

                if (endIdx - startIdx + 1 < 3)
                    continue;

                double area = TrapezoidalArea(rts, intensities, startIdx, endIdx);
                double snr = ComputeSnr(intensities, apexIdx, startIdx, endIdx);

                peaks.Add(new XICPeakBounds
                {
                    ApexRt = rts[apexIdx],
                    ApexIntensity = apexIntensity,
                    ApexIndex = apexIdx,
                    StartRt = rts[startIdx],
                    EndRt = rts[endIdx],
                    StartIndex = startIdx,
                    EndIndex = endIdx,
                    Area = area,
                    SignalToNoise = snr,
                });
            }

            return peaks;
        }

        /// <summary>
        /// Walk left from apex to find start boundary using DIA-NN-style valley
        /// detection. Stops at whichever comes first:
        /// - Intensity drops below boundaryThreshold
        /// - Valley detected: local minimum is &lt;50% of apex AND &lt;50% of the next rising neighbor
        /// Direct port of Rust walk_boundary_left (osprey-chromatography/src/lib.rs:544).
        /// </summary>
        private static int WalkBoundaryLeft(
            double[] smoothed, int apexIdx, double apexIntensity, double boundaryThreshold)
        {
            double valley = smoothed[apexIdx];
            int valleyPos = apexIdx;

            for (int i = apexIdx - 1; i >= 0; i--)
            {
                if (smoothed[i] < valley)
                {
                    valley = smoothed[i];
                    valleyPos = i;
                }
                else if (valley < apexIntensity / 2.0 && valley < smoothed[i] / 2.0)
                {
                    return valleyPos;
                }

                if (smoothed[i] < boundaryThreshold)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Walk right from apex to find end boundary using DIA-NN-style valley
        /// detection. Direct port of Rust walk_boundary_right.
        /// </summary>
        private static int WalkBoundaryRight(
            double[] smoothed, int apexIdx, double apexIntensity, double boundaryThreshold)
        {
            int n = smoothed.Length;
            double valley = smoothed[apexIdx];
            int valleyPos = apexIdx;

            for (int i = apexIdx + 1; i < n; i++)
            {
                double val = smoothed[i];
                if (val < valley)
                {
                    valley = val;
                    valleyPos = i;
                }
                else if (valley < apexIntensity / 2.0 && valley < val / 2.0)
                {
                    return valleyPos;
                }

                if (val < boundaryThreshold)
                    return i;
            }
            return n - 1;
        }

        /// <summary>
        /// Compute asymmetric half-widths at half-maximum on a smoothed intensity
        /// series. Returns separate left and right half-widths (minutes), which
        /// naturally captures chromatographic peak tailing. Uses linear
        /// interpolation at the half-height crossing. If one side is missing,
        /// the found side is used for both (assume roughly symmetric). Returns
        /// false if neither side can be found.
        /// Direct port of Rust compute_asymmetric_half_widths.
        /// </summary>
        private static bool ComputeAsymmetricHalfWidths(
            double[] smoothed, double[] rts, int apexIdx,
            out double leftHw, out double rightHw)
        {
            leftHw = 0.0;
            rightHw = 0.0;

            double apexVal = smoothed[apexIdx];
            if (apexVal <= 0.0)
                return false;

            double half = apexVal / 2.0;
            double apexRt = rts[apexIdx];

            // Scan left from apex to find half-height crossing
            double? left = null;
            for (int i = apexIdx; i >= 1; i--)
            {
                if (smoothed[i] >= half && smoothed[i - 1] < half)
                {
                    double denom = smoothed[i] - smoothed[i - 1];
                    double crossingRt;
                    if (denom > 1e-30)
                    {
                        double frac = (smoothed[i] - half) / denom;
                        crossingRt = rts[i] - frac * (rts[i] - rts[i - 1]);
                    }
                    else
                    {
                        crossingRt = rts[i];
                    }
                    left = apexRt - crossingRt;
                    break;
                }
            }

            // Scan right from apex to find half-height crossing
            double? right = null;
            for (int i = apexIdx; i < smoothed.Length - 1; i++)
            {
                if (smoothed[i] >= half && smoothed[i + 1] < half)
                {
                    double denom = smoothed[i] - smoothed[i + 1];
                    double crossingRt;
                    if (denom > 1e-30)
                    {
                        double frac = (smoothed[i] - half) / denom;
                        crossingRt = rts[i] + frac * (rts[i + 1] - rts[i]);
                    }
                    else
                    {
                        crossingRt = rts[i];
                    }
                    right = crossingRt - apexRt;
                    break;
                }
            }

            if (left.HasValue && right.HasValue && left.Value > 0.0 && right.Value > 0.0)
            {
                leftHw = left.Value;
                rightHw = right.Value;
                return true;
            }
            // One side found: use it for both
            if (left.HasValue && left.Value > 0.0)
            {
                leftHw = left.Value;
                rightHw = left.Value;
                return true;
            }
            if (right.HasValue && right.Value > 0.0)
            {
                leftHw = right.Value;
                rightHw = right.Value;
                return true;
            }
            return false;
        }
    }
}
