/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on datum (https://github.com/maccoss/datum)
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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// A compact Levenberg-Marquardt least-squares fitter for models with a handful of
    /// parameters, using a forward-difference Jacobian (adequate for smooth peak shapes).
    /// <see cref="Fit"/> is ported from datum with the arithmetic unchanged, so fits match datum's
    /// to rounding. <see cref="FitResiduals"/> is the same iteration for problems whose residuals
    /// are not a single curve against one x axis, such as a shape shared by several traces.
    /// </summary>
    public static class LevenbergMarquardt
    {
        /// <summary>
        /// Fits <paramref name="model"/>(parameters, x) to (<paramref name="x"/>, <paramref name="y"/>)
        /// starting from <paramref name="initial"/>, which also sets the parameter count.
        /// </summary>
        /// <returns>The fitted parameters, or the last iterate if it did not converge.</returns>
        public static double[] Fit(double[] x, double[] y, double[] initial,
            Func<double[], double, double> model, int maxIterations = 100)
        {
            int n = x.Length;
            int p = initial.Length;
            var beta = (double[])initial.Clone();
            double lambda = 1e-3;
            double cost = Cost(x, y, beta, model);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // The model at the current parameters is evaluated once and reused for both the
                // residuals and the forward-difference Jacobian.
                var jac = new double[n][];
                var residual = new double[n];
                var f0 = new double[n];
                for (int i = 0; i < n; i++)
                {
                    f0[i] = model(beta, x[i]);
                    residual[i] = y[i] - f0[i];
                    jac[i] = new double[p];
                }

                for (int k = 0; k < p; k++)
                {
                    double step = Math.Max(1e-6, Math.Abs(beta[k]) * 1e-6);
                    var bumped = (double[])beta.Clone();
                    bumped[k] += step;
                    double invStep = 1.0 / step;
                    for (int i = 0; i < n; i++)
                        jac[i][k] = (model(bumped, x[i]) - f0[i]) * invStep;
                }

                if (!TryStep(jac, residual, lambda, out double[] delta))
                    break;

                var candidate = (double[])beta.Clone();
                for (int a = 0; a < p; a++)
                    candidate[a] += delta[a];

                double candidateCost = Cost(x, y, candidate, model);
                if (candidateCost < cost)
                {
                    if (Math.Abs(cost - candidateCost) < 1e-12 * (1.0 + cost))
                    {
                        beta = candidate;
                        break;
                    }

                    beta = candidate;
                    cost = candidateCost;
                    lambda = Math.Max(lambda * 0.5, 1e-9);
                }
                else
                {
                    lambda = Math.Min(lambda * 4.0, 1e9);
                }
            }

            return beta;
        }

        /// <summary>
        /// Minimizes the sum of squares of <paramref name="residuals"/>(parameters), starting from
        /// <paramref name="initial"/>. The residuals are data minus model, already weighted, and the
        /// function must return the same number of them for every parameter vector. The fit stops
        /// when a step lowers the sum of squares by less than <paramref name="relativeTolerance"/>
        /// times (1 + the sum).
        /// </summary>
        /// <returns>The fitted parameters, or the last iterate if it did not converge.</returns>
        public static double[] FitResiduals(Func<double[], double[]> residuals, double[] initial,
            int maxIterations = 100, double relativeTolerance = 1e-12)
        {
            int p = initial.Length;
            var beta = (double[])initial.Clone();
            double lambda = 1e-3;
            double[] r0 = residuals(beta);
            double cost = SumOfSquares(r0);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // With r = data - model, d(model)/d(beta) = -dr/d(beta), so the step takes the same
                // normal equations as Fit with the model Jacobian.
                int n = r0.Length;
                var jac = new double[n][];
                for (int i = 0; i < n; i++)
                    jac[i] = new double[p];

                for (int k = 0; k < p; k++)
                {
                    double step = Math.Max(1e-6, Math.Abs(beta[k]) * 1e-6);
                    var bumped = (double[])beta.Clone();
                    bumped[k] += step;
                    double invStep = 1.0 / step;
                    double[] rk = residuals(bumped);
                    for (int i = 0; i < n; i++)
                        jac[i][k] = (r0[i] - rk[i]) * invStep;
                }

                if (!TryStep(jac, r0, lambda, out double[] delta))
                    break;

                var candidate = (double[])beta.Clone();
                for (int a = 0; a < p; a++)
                    candidate[a] += delta[a];

                double[] candidateResiduals = residuals(candidate);
                double candidateCost = SumOfSquares(candidateResiduals);
                if (candidateCost < cost)
                {
                    bool converged = Math.Abs(cost - candidateCost) < relativeTolerance * (1.0 + cost);
                    beta = candidate;
                    r0 = candidateResiduals;
                    cost = candidateCost;
                    if (converged)
                        break;
                    lambda = Math.Max(lambda * 0.5, 1e-9);
                }
                else
                {
                    lambda = Math.Min(lambda * 4.0, 1e9);
                }
            }

            return beta;
        }

        /// <summary>
        /// Solves (JtJ + lambda * diag(JtJ)) delta = Jt r for the step, where J is the model
        /// Jacobian and r is data minus model.
        /// </summary>
        private static bool TryStep(double[][] jac, double[] residual, double lambda, out double[] delta)
        {
            int n = residual.Length;
            int p = n > 0 ? jac[0].Length : 0;
            var jtj = new double[p, p];
            var jtr = new double[p];
            for (int i = 0; i < n; i++)
            {
                for (int a = 0; a < p; a++)
                {
                    jtr[a] += jac[i][a] * residual[i];
                    for (int b = 0; b < p; b++)
                        jtj[a, b] += jac[i][a] * jac[i][b];
                }
            }

            var augmented = (double[,])jtj.Clone();
            for (int a = 0; a < p; a++)
                augmented[a, a] += lambda * (jtj[a, a] + 1e-12);

            return DenseLinearSolver.TrySolve(augmented, jtr, out delta);
        }

        private static double Cost(double[] x, double[] y, double[] beta, Func<double[], double, double> model)
        {
            double sum = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                double r = y[i] - model(beta, x[i]);
                sum += r * r;
            }

            return sum;
        }

        private static double SumOfSquares(double[] values)
        {
            double sum = 0.0;
            foreach (double v in values)
                sum += v * v;
            return sum;
        }
    }
}
