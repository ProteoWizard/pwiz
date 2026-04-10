using System;
using System.Collections.Generic;
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
    }
}
