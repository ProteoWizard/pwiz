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

namespace pwiz.Quasar.Core.Elution
{
    /// <summary>
    /// Weighted non-negative least squares for small dense problems: minimizes
    /// sum_i w_i (y_i - sum_j A_ij x_j)^2 subject to every x_j &gt;= 0. Uses the Lawson-Hanson
    /// active-set method on the normal equations, which is exact and fast for the handful of
    /// unknowns a window fit has (the templates overlapping one trace, plus a baseline).
    /// </summary>
    public static class NonNegativeLeastSquares
    {
        /// <summary>
        /// Solves the weighted problem for the matrix whose columns are
        /// <paramref name="columns"/> (each of length n), observations <paramref name="y"/> and
        /// per-observation weights <paramref name="weights"/> (null for unweighted).
        /// </summary>
        public static double[] Solve(IReadOnlyList<double[]> columns, double[] y, double[] weights)
        {
            int p = columns.Count;
            var gram = new double[p, p];
            var rhs = new double[p];
            for (int a = 0; a < p; a++)
            {
                double[] ca = columns[a];
                for (int i = 0; i < y.Length; i++)
                    rhs[a] += Weight(weights, i) * ca[i] * y[i];
                for (int b = a; b < p; b++)
                {
                    double[] cb = columns[b];
                    double sum = 0.0;
                    for (int i = 0; i < y.Length; i++)
                        sum += Weight(weights, i) * ca[i] * cb[i];
                    gram[a, b] = sum;
                    gram[b, a] = sum;
                }
            }

            return SolveNormal(gram, rhs);
        }

        /// <summary>
        /// Lawson-Hanson on the normal equations: minimizes x'Gx - 2h'x subject to x &gt;= 0, for the
        /// symmetric positive semidefinite Gram matrix <paramref name="gram"/> = A'WA and
        /// <paramref name="rhs"/> = A'Wy. A column that would make the free set singular (a copy
        /// of other columns) is left at zero.
        /// </summary>
        public static double[] SolveNormal(double[,] gram, double[] rhs)
        {
            int p = rhs.Length;
            var x = new double[p];
            var passive = new bool[p];
            var excluded = new bool[p];
            double scale = 0.0;
            for (int j = 0; j < p; j++)
                scale = Math.Max(scale, Math.Max(Math.Abs(rhs[j]), Math.Abs(gram[j, j])));
            double tolerance = 1e-12 * Math.Max(scale, double.Epsilon);

            int maxOuter = 3 * p + 10;
            for (int outer = 0; outer < maxOuter; outer++)
            {
                // The gradient of the objective (up to a factor -2) is h - Gx. The free variable
                // with the most positive gradient enters the passive set.
                int entering = -1;
                double best = tolerance;
                for (int j = 0; j < p; j++)
                {
                    if (passive[j] || excluded[j])
                        continue;
                    double g = rhs[j];
                    for (int k = 0; k < p; k++)
                        g -= gram[j, k] * x[k];
                    if (g > best)
                    {
                        best = g;
                        entering = j;
                    }
                }

                if (entering < 0)
                    break;

                passive[entering] = true;
                for (int inner = 0; inner <= p; inner++)
                {
                    var z = SolvePassive(gram, rhs, passive);
                    if (z == null)
                    {
                        passive[entering] = false;
                        excluded[entering] = true;
                        break;
                    }

                    // Step from x toward z until the first passive variable reaches zero.
                    double alpha = 1.0;
                    for (int j = 0; j < p; j++)
                    {
                        if (!passive[j] || z[j] > 0.0)
                            continue;
                        double distance = x[j] - z[j];
                        alpha = distance > 0.0 ? Math.Min(alpha, x[j] / distance) : 0.0;
                    }

                    for (int j = 0; j < p; j++)
                        x[j] += alpha * (z[j] - x[j]);

                    if (alpha >= 1.0)
                        break;

                    for (int j = 0; j < p; j++)
                    {
                        if (passive[j] && x[j] <= tolerance)
                        {
                            passive[j] = false;
                            x[j] = 0.0;
                        }
                    }
                }
            }

            for (int j = 0; j < p; j++)
            {
                if (x[j] < 0.0)
                    x[j] = 0.0;
            }

            return x;
        }

        /// <summary>
        /// Weighted sum of squared residuals of <paramref name="x"/> for the problem given to
        /// <see cref="Solve"/>.
        /// </summary>
        public static double WeightedSumOfSquares(IReadOnlyList<double[]> columns, double[] y, double[] weights,
            double[] x)
        {
            double sum = 0.0;
            for (int i = 0; i < y.Length; i++)
            {
                double fitted = 0.0;
                for (int j = 0; j < columns.Count; j++)
                    fitted += columns[j][i] * x[j];
                double r = y[i] - fitted;
                sum += Weight(weights, i) * r * r;
            }

            return sum;
        }

        private static double Weight(double[] weights, int i)
        {
            return weights == null ? 1.0 : weights[i];
        }

        /// <summary>
        /// Unconstrained least squares on the passive variables (the others held at zero), or null
        /// when that subsystem is singular.
        /// </summary>
        private static double[] SolvePassive(double[,] gram, double[] rhs, bool[] passive)
        {
            var indices = new List<int>();
            for (int j = 0; j < passive.Length; j++)
            {
                if (passive[j])
                    indices.Add(j);
            }

            int m = indices.Count;
            var sub = new double[m, m];
            var subRhs = new double[m];
            for (int a = 0; a < m; a++)
            {
                subRhs[a] = rhs[indices[a]];
                for (int b = 0; b < m; b++)
                    sub[a, b] = gram[indices[a], indices[b]];
            }

            if (!DenseLinearSolver.TrySolve(sub, subRhs, out double[] subSolution))
                return null;

            var z = new double[passive.Length];
            for (int a = 0; a < m; a++)
                z[indices[a]] = subSolution[a];
            return z;
        }
    }
}
