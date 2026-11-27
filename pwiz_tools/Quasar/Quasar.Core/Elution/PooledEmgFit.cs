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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// The template fit (spec Stage 2d): one EMG shape (mu, sigma, tau) fitted to all the points
    /// of several co-eluting traces at once, each trace with its own non-negative amplitude and
    /// linear baseline, weighted by the noise model. With six traces of six points each the shape
    /// is fitted to thirty-six points, so it is well determined even when no single trace is.
    /// </summary>
    /// <remarks>
    /// <para>The per-trace amplitudes and baselines are linear given the shape, so they are solved
    /// exactly (non-negative least squares) inside every evaluation, and Levenberg-Marquardt
    /// searches only the three shape parameters (variable projection).</para>
    /// <para>The fit is made robust by iteratively reweighted least squares with Huber weights:
    /// after each pass, points whose standardized residual exceeds <see cref="HUBER_K"/> are
    /// down-weighted, so an interference bump on one trace does not move the shared shape. The
    /// variances are recomputed from the fitted values at the same time.</para>
    /// </remarks>
    public static class PooledEmgFit
    {
        /// <summary>The Huber tuning constant (95% efficiency for Gaussian residuals).</summary>
        public const double HUBER_K = 1.345;

        /// <summary>The first pass plus this many reweighted passes.</summary>
        private const int REWEIGHT_PASSES = 3;

        private const int MAX_ITERATIONS = 60;

        /// <summary>
        /// Fits one shape to points <paramref name="start"/> to <paramref name="end"/> (inclusive)
        /// of every trace in <paramref name="traces"/>, all sampled at <paramref name="rt"/>.
        /// <paramref name="prior"/>, the run's typical shape, keeps a weak template from collapsing
        /// to no tail; null fits the data alone. <paramref name="initial"/> starts the search; when
        /// null, the start is an EMG fitted to the pointwise median of the traces scaled to their
        /// maxima. When the traces are residuals, <paramref name="explained"/> holds, per trace, the
        /// signal already taken out of it, which counts toward the variance as in
        /// the template fit. Returns null when there are no traces, too few points, or
        /// no usable shape.
        /// </summary>
        public static PooledEmgFitResult Fit(double[] rt, IReadOnlyList<double[]> traces, int start, int end,
            NoiseModel noise, EmgShapePrior prior = null, EmgShape initial = null,
            IReadOnlyList<double[]> explained = null)
        {
            start = Math.Max(0, start);
            end = Math.Min(rt.Length - 1, end);
            int n = end - start + 1;
            int m = traces.Count;
            if (m == 0 || n < EmgFit.MIN_POINTS)
                return null;

            var x = new double[n];
            Array.Copy(rt, start, x, 0, n);
            var obs = new double[m][];
            var offsets = new double[m][];
            for (int k = 0; k < m; k++)
            {
                obs[k] = new double[n];
                Array.Copy(traces[k], start, obs[k], 0, n);
                offsets[k] = new double[n];
                if (explained?[k] != null)
                    Array.Copy(explained[k], start, offsets[k], 0, n);
            }

            initial ??= InitialShape(x, obs);
            if (initial == null)
                return null;

            var problem = new Problem(x, obs, offsets, prior);
            var weights = new double[m][];
            for (int k = 0; k < m; k++)
            {
                var offset = offsets[k];
                weights[k] = obs[k].Select((v, i) => 1.0 / noise.Variance(v + offset[i])).ToArray();
            }

            var theta = new[] { initial.Mu, initial.Sigma, initial.Tau };
            for (int pass = 0; pass <= REWEIGHT_PASSES; pass++)
            {
                var passWeights = weights;
                theta = LevenbergMarquardt.FitResiduals(t => problem.Residuals(t, passWeights), theta, MAX_ITERATIONS);
                if (pass < REWEIGHT_PASSES)
                    weights = problem.Reweight(theta, passWeights, noise);
            }

            double sigma = Math.Abs(theta[1]);
            double tau = Math.Abs(theta[2]);
            if (!double.IsFinite(theta[0]) || !double.IsFinite(sigma) || !double.IsFinite(tau) ||
                sigma < 1e-9 || tau < 1e-9)
            {
                return null;
            }

            var shape = new EmgShape(theta[0], sigma, tau);
            var amplitudes = new double[m];
            var baselineStarts = new double[m];
            var baselineEnds = new double[m];
            double chiSquare = 0.0;
            var columns = problem.Columns(shape);
            for (int k = 0; k < m; k++)
            {
                double[] coefficients = NonNegativeLeastSquares.Solve(columns, obs[k], weights[k]);
                amplitudes[k] = coefficients[0];
                baselineStarts[k] = coefficients[1];
                baselineEnds[k] = coefficients[2];
                chiSquare += NonNegativeLeastSquares.WeightedSumOfSquares(columns, obs[k], weights[k], coefficients);
            }

            return new PooledEmgFitResult(shape, amplitudes, baselineStarts, baselineEnds, chiSquare, n * m);
        }

        /// <summary>
        /// An EMG fitted to the pointwise median of the traces, each scaled to its own maximum, so
        /// every trace counts equally whatever its intensity. Falls back to the most intense trace
        /// alone when the median does not fit.
        /// </summary>
        private static EmgShape InitialShape(double[] x, double[][] obs)
        {
            var scaled = obs.Where(trace => trace.Max() > 0.0)
                .Select(trace =>
                {
                    double max = trace.Max();
                    return trace.Select(v => v / max).ToArray();
                }).ToArray();
            if (scaled.Length == 0)
                return null;

            var median = new double[x.Length];
            var column = new double[scaled.Length];
            for (int i = 0; i < x.Length; i++)
            {
                for (int k = 0; k < scaled.Length; k++)
                    column[k] = scaled[k][i];
                Array.Sort(column);
                int mid = column.Length / 2;
                median[i] = column.Length % 2 == 1 ? column[mid] : 0.5 * (column[mid - 1] + column[mid]);
            }

            var fit = EmgFit.Fit(x, median);
            if (fit != null)
                return fit.Shape;

            var strongest = obs.OrderByDescending(trace => trace.Sum()).First();
            return EmgFit.Fit(x, strongest)?.Shape;
        }

        /// <summary>The fixed data of one pooled fit: the time axis, the observed traces and the prior.</summary>
        private sealed class Problem
        {
            private readonly double[] _x;
            private readonly double[][] _obs;
            private readonly double[][] _offsets;
            private readonly EmgShapePrior _prior;
            private readonly double[] _rampDown;
            private readonly double[] _rampUp;

            public Problem(double[] x, double[][] obs, double[][] offsets, EmgShapePrior prior)
            {
                _x = x;
                _obs = obs;
                _offsets = offsets;
                _prior = prior;
                int n = x.Length;
                double span = x[n - 1] - x[0];
                _rampDown = new double[n];
                _rampUp = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double u = span > 0.0 ? (x[i] - x[0]) / span : 0.0;
                    _rampDown[i] = 1.0 - u;
                    _rampUp[i] = u;
                }
            }

            /// <summary>The design columns for one trace: the unit-area shape, then the baseline ramps.</summary>
            public double[][] Columns(EmgShape shape)
            {
                return new[] { _x.Select(shape.Density).ToArray(), _rampDown, _rampUp };
            }

            /// <summary>
            /// Weighted residuals of every trace, concatenated, for shape parameters
            /// (mu, sigma, tau), with each trace's amplitude and baseline solved exactly, followed
            /// by the prior's two residuals when there is a prior.
            /// </summary>
            public double[] Residuals(double[] theta, double[][] weights)
            {
                var columns = Columns(ShapeOf(theta));
                int n = _x.Length;
                int dataCount = n * _obs.Length;
                var residuals = new double[dataCount + (_prior != null ? 2 : 0)];
                for (int k = 0; k < _obs.Length; k++)
                {
                    double[] coefficients = NonNegativeLeastSquares.Solve(columns, _obs[k], weights[k]);
                    for (int i = 0; i < n; i++)
                        residuals[k * n + i] = Math.Sqrt(weights[k][i]) * (_obs[k][i] - Fitted(columns, coefficients, i));
                }

                if (_prior != null)
                {
                    residuals[dataCount] = _prior.SigmaResidual(theta[1]);
                    residuals[dataCount + 1] = _prior.TauResidual(theta[2]);
                }

                return residuals;
            }

            /// <summary>
            /// New weights after a pass: the variance from the fitted values, times the Huber weight
            /// of each point's standardized residual.
            /// </summary>
            public double[][] Reweight(double[] theta, double[][] weights, NoiseModel noise)
            {
                var columns = Columns(ShapeOf(theta));
                int n = _x.Length;
                var result = new double[_obs.Length][];
                for (int k = 0; k < _obs.Length; k++)
                {
                    double[] coefficients = NonNegativeLeastSquares.Solve(columns, _obs[k], weights[k]);
                    result[k] = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        double fitted = Fitted(columns, coefficients, i);
                        double variance = noise.Variance(fitted + _offsets[k][i]);
                        double z = Math.Abs(_obs[k][i] - fitted) / Math.Sqrt(variance);
                        double huber = z <= HUBER_K ? 1.0 : HUBER_K / z;
                        result[k][i] = huber / variance;
                    }
                }

                return result;
            }

            private static EmgShape ShapeOf(double[] theta)
            {
                return new EmgShape(theta[0], Math.Abs(theta[1]), Math.Abs(theta[2]));
            }

            private static double Fitted(double[][] columns, double[] coefficients, int i)
            {
                return columns[0][i] * coefficients[0] + columns[1][i] * coefficients[1] +
                       columns[2][i] * coefficients[2];
            }
        }
    }
}
