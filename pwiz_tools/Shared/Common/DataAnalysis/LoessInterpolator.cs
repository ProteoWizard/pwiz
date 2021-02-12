/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;

namespace pwiz.Common.DataAnalysis
{
    /**
     * Implements the <a href="http://en.wikipedia.org/wiki/Local_regression">
     * Local Regression Algorithm</a> (also Loess, Lowess) for interpolation of
     * real univariate functions.
     * <p/>
     * For reference, see
     * <a href="http://www.math.tau.ac.il/~yekutiel/MA seminar/Cleveland 1979.pdf">
     * William S. Cleveland - Robust Locally Weighted Regression and Smoothing
     * Scatterplots</a>
     * <p/>
     * This class implements both the loess method and serves as an interpolation
     * adapter to it, allowing to build a spline on the obtained loess fit.
     *
     * @version $Revision: 990655 $ $Date: 2010-08-29 23:49:40 +0200 (dim. 29 aoÃ»t 2010) $
     * @since 2.0
     */
    public class LoessInterpolator
    {

        /** Default value of the bandwidth parameter. */
        public const double DEFAULT_BANDWIDTH = 0.3;

        /** Default value of the number of robustness iterations. */
        public const int DEFAULT_ROBUSTNESS_ITERS = 2;

        /**
         * Default value for accuracy.
         * @since 2.1
         */
        public const double DEFAULT_ACCURACY = 1e-12;

        /**
         * The bandwidth parameter: when computing the loess fit at
         * a particular point, this fraction of source points closest
         * to the current point is taken into account for computing
         * a least-squares regression.
         * <p/>
         * A sensible value is usually 0.25 to 0.5.
         */
        private readonly double _bandwidth;

        /**
         * The number of robustness iterations parameter: this many
         * robustness iterations are done.
         * <p/>
         * A sensible value is usually 0 (just the initial fit without any
         * robustness iterations) to 4.
         */
        private readonly int _robustnessIters;

        /**
         * If the median residual at a certain robustness iteration
         * is less than this amount, no more iterations are done.
         */
        private readonly double _accuracy;

        /**
         * Constructs a new {@link LoessInterpolator}
         * with a bandwidth of {@link #DEFAULT_BANDWIDTH},
         * {@link #DEFAULT_ROBUSTNESS_ITERS} robustness iterations
         * and an accuracy of {#link #DEFAULT_ACCURACY}.
         * See {@link #LoessInterpolator(double, int, double)} for an explanation of
         * the parameters.
         */
        public LoessInterpolator()
        {
            _bandwidth = DEFAULT_BANDWIDTH;
            _robustnessIters = DEFAULT_ROBUSTNESS_ITERS;
            _accuracy = DEFAULT_ACCURACY;
        }

        /**
         * Constructs a new {@link LoessInterpolator}
         * with given bandwidth and number of robustness iterations.
         * <p>
         * Calling this constructor is equivalent to calling {link {@link
         * #LoessInterpolator(double, int, double) LoessInterpolator(bandwidth,
         * robustnessIters, LoessInterpolator.DEFAULT_ACCURACY)}
         * </p>
         *
         * @param bandwidth  when computing the loess fit at
         * a particular point, this fraction of source points closest
         * to the current point is taken into account for computing
         * a least-squares regression.<br/>
         * A sensible value is usually 0.25 to 0.5, the default value is
         * {@link #DEFAULT_BANDWIDTH}.
         * @param robustnessIters This many robustness iterations are done.<br/>
         * A sensible value is usually 0 (just the initial fit without any
         * robustness iterations) to 4, the default value is
         * {@link #DEFAULT_ROBUSTNESS_ITERS}.
         * @throws MathException if bandwidth does not lie in the interval [0,1]
         * or if robustnessIters is negative.
         * @see #LoessInterpolator(double, int, double)
         */
        public LoessInterpolator(double bandwidth, int robustnessIters) :
            this(bandwidth, robustnessIters, DEFAULT_ACCURACY)
        {
        }

        /**
         * Constructs a new {@link LoessInterpolator}
         * with given bandwidth, number of robustness iterations and accuracy.
         *
         * @param bandwidth  when computing the loess fit at
         * a particular point, this fraction of source points closest
         * to the current point is taken into account for computing
         * a least-squares regression.<br/>
         * A sensible value is usually 0.25 to 0.5, the default value is
         * {@link #DEFAULT_BANDWIDTH}.
         * @param robustnessIters This many robustness iterations are done.<br/>
         * A sensible value is usually 0 (just the initial fit without any
         * robustness iterations) to 4, the default value is
         * {@link #DEFAULT_ROBUSTNESS_ITERS}.
         * @param accuracy If the median residual at a certain robustness iteration
         * is less than this amount, no more iterations are done.
         * @throws MathException if bandwidth does not lie in the interval [0,1]
         * or if robustnessIters is negative.
         * @see #LoessInterpolator(double, int)
         * @since 2.1
         */
        public LoessInterpolator(double bandwidth, int robustnessIters, double accuracy)
        {
            if (bandwidth < 0 || bandwidth > 1)
            {
                throw new ArgumentException(@"Bandwidth must be between 0 and 1");
            }
            _bandwidth = bandwidth;
            if (robustnessIters < 0)
            {
                throw new ArgumentException(@"RobustnessIters must be non-negative");
            }
            _robustnessIters = robustnessIters;
            _accuracy = accuracy;
        }

        /**
         * Compute a weighted loess fit on the data at the original abscissae.
         *
         * @param xval the arguments for the interpolation points
         * @param yval the values for the interpolation points
         * @param weights point weights: coefficients by which the robustness weight of a point is multiplied
         * @return values of the loess fit at corresponding original abscissae
         * @throws MathException if some of the following conditions are false:
         * <ul>
         * <li> Arguments and values are of the same size that is greater than zero</li>
         * <li> The arguments are in a strictly increasing order</li>
         * <li> All arguments and values are finite real numbers</li>
         * </ul>
         * @since 2.1
         */
        public double[] Smooth(double[] xval, double[] yval, double[] weights, CancellationToken token)
        {
            if (xval.Length != yval.Length)
            {
                throw new ArgumentException(@"Mismatched array lengths");
            }

            int n = xval.Length;

            if (n == 0)
            {
                throw new ArgumentException(@"Must have at least one point");
            }

            CheckAllFiniteReal(xval);
            CheckAllFiniteReal(yval);
            CheckAllFiniteReal(weights);

            CheckNotDecreasing(xval);

            if (n == 1)
            {
                return new[] { yval[0] };
            }

            if (n == 2)
            {
                return new[] { yval[0], yval[1] };
            }

            int bandwidthInPoints = (int)(_bandwidth * n);

            if (bandwidthInPoints < 2)
            {
                throw new ArgumentException(@"Bandwidth too small");
            }

            double[] res = new double[n];

            double[] residuals = new double[n];
            double[] sortedResiduals = new double[n];

            // Do an initial fit and 'robustnessIters' robustness iterations.
            // This is equivalent to doing 'robustnessIters+1' robustness iterations
            // starting with all robustness weights set to 1.
            double[] robustnessWeights = Enumerable.Repeat(1.0, n).ToArray();

            for (int iter = 0; iter <= _robustnessIters; ++iter)
            {
                int[] bandwidthInterval = { 0, bandwidthInPoints - 1 };
                // At each x, compute a local weighted linear regression
                for (int i = 0; i < n; ++i)
                {
                    token.ThrowIfCancellationRequested();

                    double x = xval[i];

                    // Find out the interval of source points on which
                    // a regression is to be made.
                    if (i > 0)
                    {
                        UpdateBandwidthInterval(xval, weights, i, bandwidthInterval);
                    }

                    int ileft = bandwidthInterval[0];
                    int iright = bandwidthInterval[1];

                    // Compute the point of the bandwidth interval that is
                    // farthest from x
                    int edge = xval[i] - xval[ileft] > xval[iright] - xval[i]
                        ? ileft
                        : iright;

                    // Compute a least-squares linear fit weighted by
                    // the product of robustness weights and the tricube
                    // weight function.
                    // See http://en.wikipedia.org/wiki/Linear_regression
                    // (section "Univariate linear case")
                    // and http://en.wikipedia.org/wiki/Weighted_least_squares
                    // (section "Weighted least squares")
                    double sumWeights = 0;
                    double sumX = 0;
                    double sumXSquared = 0;
                    double sumY = 0;
                    double sumXy = 0;
                    double denom = Math.Abs(1.0 / (xval[edge] - x));
                    for (int k = ileft; k <= iright; ++k)
                    {
                        double xk = xval[k];
                        double yk = yval[k];
                        double dist = (k < i) ? x - xk : xk - x;
                        double w = Tricube(dist * denom) * robustnessWeights[k] * weights[k];
                        double xkw = xk * w;
                        sumWeights += w;
                        sumX += xkw;
                        sumXSquared += xk * xkw;
                        sumY += yk * w;
                        sumXy += yk * xkw;
                    }

                    double meanX = sumX / sumWeights;
                    double meanY = sumY / sumWeights;
                    double meanXy = sumXy / sumWeights;
                    double meanXSquared = sumXSquared / sumWeights;

                    double beta;
                    if (Math.Sqrt(Math.Abs(meanXSquared - meanX * meanX)) < _accuracy)
                    {
                        beta = 0;
                    }
                    else
                    {
                        beta = (meanXy - meanX * meanY) / (meanXSquared - meanX * meanX);
                    }

                    double alpha = meanY - beta * meanX;

                    res[i] = beta * x + alpha;
                    residuals[i] = Math.Abs(yval[i] - res[i]);
                }

                // No need to recompute the robustness weights at the last
                // iteration, they won't be needed anymore
                if (iter == _robustnessIters)
                {
                    break;
                }

                // Recompute the robustness weights.

                // Find the median residual.
                // An arraycopy and a sort are completely tractable here,
                // because the preceding loop is a lot more expensive
                Array.Copy(residuals, 0, sortedResiduals, 0, n);
                Array.Sort(sortedResiduals);
                double medianResidual = sortedResiduals[n / 2];

                if (Math.Abs(medianResidual) < _accuracy)
                {
                    break;
                }

                for (int i = 0; i < n; ++i)
                {
                    double arg = residuals[i] / (6 * medianResidual);
                    if (arg >= 1)
                    {
                        robustnessWeights[i] = 0;
                    }
                    else
                    {
                        double w = 1 - arg * arg;
                        robustnessWeights[i] = w * w;
                    }
                }
            }

            return res;
        }

        /**
         * Compute a loess fit on the data at the original abscissae.
         *
         * @param xval the arguments for the interpolation points
         * @param yval the values for the interpolation points
         * @return values of the loess fit at corresponding original abscissae
         * @throws MathException if some of the following conditions are false:
         * <ul>
         * <li> Arguments and values are of the same size that is greater than zero</li>
         * <li> The arguments are in a strictly increasing order</li>
         * <li> All arguments and values are finite real numbers</li>
         * </ul>
         */
        public double[] Smooth(double[] xval, double[] yval, CancellationToken token)
        {
            if (xval.Length != yval.Length)
            {
                throw new ArgumentException(@"Array lengths must match");
            }

            double[] unitWeights = Enumerable.Repeat(1.0, xval.Length).ToArray();

            return Smooth(xval, yval, unitWeights, token);
        }

        /**
         * Given an index interval into xval that embraces a certain number of
         * points closest to xval[i-1], update the interval so that it embraces
         * the same number of points closest to xval[i], ignoring zero weights.
         *
         * @param xval arguments array
         * @param weights weights array
         * @param i the index around which the new interval should be computed
         * @param bandwidthInterval a two-element array {left, right} such that: <p/>
         * <tt>(left==0 or xval[i] - xval[left-1] > xval[right] - xval[i])</tt>
         * <p/> and also <p/>
         * <tt>(right==xval.length-1 or xval[right+1] - xval[i] > xval[i] - xval[left])</tt>.
         * The array will be updated.
         */
        private static void UpdateBandwidthInterval(double[] xval, double[] weights,
                                                    int i,
                                                    int[] bandwidthInterval)
        {
            int left = bandwidthInterval[0];
            int right = bandwidthInterval[1];

            // The right edge should be adjusted if the next point to the right
            // is closer to xval[i] than the leftmost point of the current interval
            int nextRight = NextNonzero(weights, right);
            if (nextRight < xval.Length && xval[nextRight] - xval[i] < xval[i] - xval[left])
            {
                int nextLeft = NextNonzero(weights, bandwidthInterval[0]);
                bandwidthInterval[0] = nextLeft;
                bandwidthInterval[1] = nextRight;
            }
        }

        /**
         * Returns the smallest index j such that j > i && (j==weights.length || weights[j] != 0)
         * @param weights weights array
         * @param i the index from which to start search; must be less than weights.length
         * @return the smallest index j such that j > i && (j==weights.length || weights[j] != 0)
         */
        private static int NextNonzero(double[] weights, int i)
        {
            int j = i + 1;
            while (j < weights.Length && weights[j] == 0)
            {
                j++;
            }
            return j;
        }

        /**
         * Compute the
         * <a href="http://en.wikipedia.org/wiki/Local_regression#Weight_function">tricube</a>
         * weight function
         *
         * @param x the argument
         * @return (1-|x|^3)^3
         */
        private static double Tricube(double x)
        {
            double tmp = 1 - x * x * x;
            return tmp * tmp * tmp;
        }

        /**
         * Check that all elements of an array are finite real numbers.
         *
         * @param values the values array
         * @param pattern pattern of the error message
         * @throws MathException if one of the values is not a finite real number
         */
// ReSharper disable UnusedParameter.Local
        private static void CheckAllFiniteReal(IEnumerable<double> values)
// ReSharper restore UnusedParameter.Local
        {
            if (values.Any(x => double.IsInfinity(x) || Double.IsNaN(x)))
            {
                throw new ArgumentException(@"Not a real number");
            }
        }

        /**
         * Check that elements of the abscissae array are in a strictly
         * increasing order.
         *
         * @param xval the abscissae array
         * @throws MathException if the abscissae array
         * is not in a strictly increasing order
         */
        private static void CheckNotDecreasing(double[] xval)
        {
            for (int i = 1; i < xval.Length; ++i)
            {
                if (i >= 1 && xval[i - 1] > xval[i])
                {
                    throw new ArgumentException(@"Values out of order at " + i);
                }
            }
        }

        public static double Interpolate(double x, double[] xValues, double[] yValues)
        {
            //CheckNotDecreasing(xValues);
            var index = Array.BinarySearch(xValues, x);
            if (index >= 0)
            {
                return yValues[index];
            }
            index = ~index;
            if (index <= 0)
            {
                return yValues[0];
            }
            if (index >= xValues.Length)
            {
                return yValues[yValues.Length - 1];
            }
            return (((x - xValues[index - 1]) * yValues[index]) + (xValues[index] - x) * yValues[index - 1])
                   / (xValues[index] - xValues[index - 1]);
        }
    }
}
