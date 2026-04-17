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
using System.Linq;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// LOESS model fitted to data. Stores sorted x-values and fitted y-values
    /// for interpolation-based prediction.
    /// Maps to RTCalibration fields in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public class LoessModel
    {
        private readonly double[] _sortedX;
        private readonly double[] _fittedY;

        internal LoessModel(double[] sortedX, double[] fittedY)
        {
            _sortedX = sortedX;
            _fittedY = fittedY;
        }

        /// <summary>Number of data points in the model.</summary>
        public int Count { get { return _sortedX.Length; } }

        /// <summary>Sorted x-values used for fitting.</summary>
        public double[] SortedX { get { return _sortedX; } }

        /// <summary>Fitted y-values at each x-value.</summary>
        public double[] FittedY { get { return _fittedY; } }

        /// <summary>
        /// Predict y for a given x using linear interpolation between fitted points.
        /// Extrapolates linearly outside the data range.
        /// </summary>
        public double Predict(double x)
        {
            int n = _sortedX.Length;
            if (n == 0)
                return x;

            int idx = Array.BinarySearch(_sortedX, x);
            if (idx < 0)
                idx = ~idx; // insertion point

            if (idx == 0)
                return ExtrapolateBelow(x);
            if (idx >= n)
                return ExtrapolateAbove(x);

            // Interpolate between idx-1 and idx
            double x0 = _sortedX[idx - 1];
            double x1 = _sortedX[idx];
            if (Math.Abs(x1 - x0) < 1e-12)
                return (_fittedY[idx - 1] + _fittedY[idx]) / 2.0;

            double y0 = _fittedY[idx - 1];
            double y1 = _fittedY[idx];
            double t = (x - x0) / (x1 - x0);
            return y0 + t * (y1 - y0);
        }

        /// <summary>
        /// Inverse predict: given a y value, find the corresponding x.
        /// Assumes the fitted curve is monotonic (which is typical for RT calibration).
        /// </summary>
        public double InversePredict(double y)
        {
            int n = _fittedY.Length;
            if (n == 0)
                return y;

            // Check monotonicity
            bool isMonotonic = true;
            for (int i = 0; i < n - 1; i++)
            {
                if (_fittedY[i + 1] < _fittedY[i] - 1e-12)
                {
                    isMonotonic = false;
                    break;
                }
            }

            if (isMonotonic)
            {
                int idx = Array.BinarySearch(_fittedY, y);
                if (idx < 0)
                    idx = ~idx;

                if (idx == 0)
                    return InverseExtrapolateBelow(y);
                if (idx >= n)
                    return InverseExtrapolateAbove(y);

                double y0 = _fittedY[idx - 1];
                double y1 = _fittedY[idx];
                if (Math.Abs(y1 - y0) < 1e-12)
                    return (_sortedX[idx - 1] + _sortedX[idx]) / 2.0;

                double x0 = _sortedX[idx - 1];
                double x1 = _sortedX[idx];
                double t = (y - y0) / (y1 - y0);
                return x0 + t * (x1 - x0);
            }

            // Non-monotonic fallback: find nearest fitted value
            int nearestIdx = 0;
            double minDist = Math.Abs(y - _fittedY[0]);
            for (int i = 1; i < n; i++)
            {
                double dist = Math.Abs(y - _fittedY[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIdx = i;
                }
            }
            return _sortedX[nearestIdx];
        }

        private double ExtrapolateBelow(double x)
        {
            int n = _sortedX.Length;
            if (n < 2)
                return _fittedY[0];
            for (int i = 0; i < n - 1; i++)
            {
                double dx = _sortedX[i + 1] - _sortedX[i];
                if (Math.Abs(dx) > 1e-12)
                {
                    double slope = (_fittedY[i + 1] - _fittedY[i]) / dx;
                    return _fittedY[i] + slope * (x - _sortedX[i]);
                }
            }
            return _fittedY[0];
        }

        private double ExtrapolateAbove(double x)
        {
            int n = _sortedX.Length;
            if (n < 2)
                return _fittedY[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                double dx = _sortedX[i + 1] - _sortedX[i];
                if (Math.Abs(dx) > 1e-12)
                {
                    double slope = (_fittedY[i + 1] - _fittedY[i]) / dx;
                    return _fittedY[i + 1] + slope * (x - _sortedX[i + 1]);
                }
            }
            return _fittedY[n - 1];
        }

        private double InverseExtrapolateBelow(double y)
        {
            int n = _fittedY.Length;
            if (n < 2)
                return _sortedX[0];
            for (int i = 0; i < n - 1; i++)
            {
                double dy = _fittedY[i + 1] - _fittedY[i];
                if (Math.Abs(dy) > 1e-12)
                {
                    double slope = (_sortedX[i + 1] - _sortedX[i]) / dy;
                    return _sortedX[i] + slope * (y - _fittedY[i]);
                }
            }
            return _sortedX[0];
        }

        private double InverseExtrapolateAbove(double y)
        {
            int n = _fittedY.Length;
            if (n < 2)
                return _sortedX[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                double dy = _fittedY[i + 1] - _fittedY[i];
                if (Math.Abs(dy) > 1e-12)
                {
                    double slope = (_sortedX[i + 1] - _sortedX[i]) / dy;
                    return _sortedX[i + 1] + slope * (y - _fittedY[i + 1]);
                }
            }
            return _sortedX[n - 1];
        }
    }

    /// <summary>
    /// LOESS (Locally Estimated Scatterplot Smoothing) regression.
    /// Fits local weighted polynomial regressions using tricube distance weighting
    /// and optional bisquare robustness iterations.
    /// Maps to the LOESS fitting logic in osprey-chromatography/src/calibration/rt.rs.
    /// </summary>
    public static class LoessRegression
    {
        /// <summary>
        /// Fit a LOESS model to the given data.
        /// </summary>
        /// <param name="x">Independent variable values.</param>
        /// <param name="y">Dependent variable values.</param>
        /// <param name="bandwidth">Fraction of data to use for each local fit (0.0-1.0).</param>
        /// <param name="degree">Polynomial degree for local fits (0 = constant, 1 = linear).</param>
        /// <param name="robustnessIterations">Number of bisquare robustness iterations.</param>
        /// <param name="classicalRobust">
        /// When false (default), absolute residuals are computed ONCE from the initial
        /// fit and reused across all robustness iterations, matching the current
        /// Rust calibration_ml.rs behavior (which captures `residuals` before the
        /// loop and never refreshes them). When true, residuals are recomputed from
        /// the current fit on each iteration, which is the classical Cleveland (1979)
        /// robust LOESS algorithm. Default matches Rust so OspreySharp stays
        /// bit-identical out of the box.
        /// </param>
        /// <returns>A fitted LoessModel for prediction.</returns>
        public static LoessModel Fit(double[] x, double[] y, double bandwidth = 0.3,
            int degree = 1, int robustnessIterations = 2,
            bool classicalRobust = false)
        {
            if (x == null || y == null)
                throw new ArgumentNullException(x == null ? "x" : "y");
            if (x.Length != y.Length)
                throw new ArgumentException("x and y must have the same length");
            if (x.Length < 2)
                throw new ArgumentException("Need at least 2 data points");

            // Sort by x (stable, matching Rust's slice::sort_by). Array.Sort
            // with Comparison<T> is unstable (introsort) and reorders ties
            // differently than Rust for duplicate x values, causing LOESS
            // divergence on data with repeated x (e.g. multi-charge peptides
            // sharing a library RT).
            int n = x.Length;
            int[] order = Enumerable.Range(0, n).OrderBy(i => x[i]).ToArray();

            double[] sortedX = new double[n];
            double[] sortedY = new double[n];
            for (int i = 0; i < n; i++)
            {
                sortedX[i] = x[order[i]];
                sortedY[i] = y[order[i]];
            }

            // Initial fit
            double[] fitted = LoessFitInternal(sortedX, sortedY, bandwidth, degree, null);

            // Compute absolute residuals from the INITIAL fit. In the default
            // (Rust-compat) mode these residuals are reused across every
            // robustness iteration, matching Rust's osprey-chromatography
            // calibration_ml.rs which captures `residuals` before the loop and
            // never refreshes them (so all iterations compute the same bisquare
            // weights and the same refined fit). In classical mode they are
            // refreshed from the current fit at the top of each iteration, which
            // is the Cleveland (1979) robust LOESS algorithm.
            double[] absResiduals = new double[n];
            for (int i = 0; i < n; i++)
                absResiduals[i] = Math.Abs(sortedY[i] - fitted[i]);

            // Robustness iterations
            double[] weights = new double[n];
            for (int i = 0; i < n; i++)
                weights[i] = 1.0;
            for (int iter = 0; iter < robustnessIterations; iter++)
            {
                if (classicalRobust && iter > 0)
                {
                    // Refresh residuals from the CURRENT fit (classical mode)
                    for (int i = 0; i < n; i++)
                        absResiduals[i] = Math.Abs(sortedY[i] - fitted[i]);
                }

                double medianAbsResidual = Median(absResiduals);
                double s = 6.0 * medianAbsResidual;

                if (s > 1e-10)
                {
                    for (int i = 0; i < n; i++)
                    {
                        double u = absResiduals[i] / s;
                        weights[i] = Math.Abs(u) < 1.0
                            ? Math.Pow(1.0 - u * u, 2)
                            : 0.0;
                    }
                }

                fitted = LoessFitInternal(sortedX, sortedY, bandwidth, degree, weights);
            }

            return new LoessModel(sortedX, fitted);
        }

        /// <summary>Compute the median of an array of values.</summary>
        public static double Median(double[] values)
        {
            if (values == null || values.Length == 0)
                return 0.0;
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        /// <summary>Compute the sample standard deviation (Bessel-corrected).</summary>
        public static double StdDev(double[] values)
        {
            if (values == null || values.Length < 2)
                return 0.0;
            double n = values.Length;
            double mean = 0;
            for (int i = 0; i < values.Length; i++)
                mean += values[i];
            mean /= n;

            double sumSq = 0;
            for (int i = 0; i < values.Length; i++)
            {
                double d = values[i] - mean;
                sumSq += d * d;
            }
            return Math.Sqrt(sumSq / (n - 1.0));
        }

        /// <summary>
        /// Compute the value at a given percentile (0.0 to 1.0).
        /// </summary>
        public static double PercentileValue(double[] values, double p)
        {
            if (values == null || values.Length == 0)
                return 0.0;
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            // Round half away from zero to match Rust's f64::round(). The
            // default Math.Round uses banker's rounding (round-to-even),
            // which disagrees on exactly .5 values -- e.g. n=6398, p=0.50
            // gives 3198.5 where Rust picks 3199 and banker's picks 3198.
            int idx = (int)Math.Round(p * (sorted.Length - 1), MidpointRounding.AwayFromZero);
            idx = Math.Min(idx, sorted.Length - 1);
            return sorted[idx];
        }

        /// <summary>Tricube weight function: (1 - |u|^3)^3 for |u| &lt; 1, else 0.</summary>
        public static double Tricube(double u)
        {
            double absU = Math.Abs(u);
            if (absU >= 1.0)
                return 0.0;
            double t = 1.0 - absU * absU * absU;
            return t * t * t;
        }

        /// <summary>Bisquare weight function: (1 - u^2)^2 for |u| &lt; 1, else 0.</summary>
        public static double Bisquare(double u)
        {
            if (Math.Abs(u) >= 1.0)
                return 0.0;
            double t = 1.0 - u * u;
            return t * t;
        }

        private static double[] LoessFitInternal(double[] x, double[] y, double bandwidth,
            int degree, double[] weights)
        {
            int n = x.Length;
            int k = (int)Math.Ceiling(bandwidth * n);
            k = Math.Max(k, degree + 2);
            k = Math.Min(k, n);

            double[] fitted = new double[n];

            for (int i = 0; i < n; i++)
            {
                double xi = x[i];

                // Find k nearest neighbors (contiguous in sorted x)
                int lo, hi;
                FindKNearestSorted(x, i, k, out lo, out hi);

                double maxDist = Math.Max(Math.Abs(x[lo] - xi), Math.Abs(x[hi - 1] - xi));

                // Accumulate weighted sums
                double sumW = 0, sumWx = 0, sumWy = 0, sumWxx = 0, sumWxy = 0;

                for (int j = lo; j < hi; j++)
                {
                    double dist = Math.Abs(x[j] - xi);
                    double u = maxDist > 1e-10 ? dist / maxDist : 0.0;
                    double tw = Tricube(u);
                    double w = weights != null ? tw * weights[j] : tw;

                    sumW += w;
                    sumWx += w * x[j];
                    sumWy += w * y[j];
                    sumWxx += w * x[j] * x[j];
                    sumWxy += w * x[j] * y[j];
                }

                // Fit local polynomial
                if (degree == 0)
                {
                    fitted[i] = sumW > 1e-10 ? sumWy / sumW : y[i];
                }
                else
                {
                    // Weighted linear regression: solve 2x2 normal equations
                    double det = sumW * sumWxx - sumWx * sumWx;
                    if (Math.Abs(det) < 1e-10)
                    {
                        fitted[i] = sumW > 1e-10 ? sumWy / sumW : y[i];
                    }
                    else
                    {
                        double b0 = (sumWxx * sumWy - sumWx * sumWxy) / det;
                        double b1 = (sumW * sumWxy - sumWx * sumWy) / det;
                        fitted[i] = b0 + b1 * xi;
                    }
                }
            }

            return fitted;
        }

        /// <summary>
        /// Find the contiguous range [lo, hi) of k nearest neighbors of x[center]
        /// in a sorted array. Two-pointer expansion from center index.
        /// </summary>
        internal static void FindKNearestSorted(double[] x, int center, int k,
            out int lo, out int hi)
        {
            int n = x.Length;
            lo = center;
            hi = center + 1;

            while (hi - lo < k)
            {
                bool canGoLeft = lo > 0;
                bool canGoRight = hi < n;

                if (canGoLeft && canGoRight)
                {
                    if (x[center] - x[lo - 1] <= x[hi] - x[center])
                        lo--;
                    else
                        hi++;
                }
                else if (canGoLeft)
                {
                    lo--;
                }
                else
                {
                    hi++;
                }
            }
        }
    }
}
