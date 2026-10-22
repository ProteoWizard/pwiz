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

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// Minimizes <c>1/2 |y - A b|^2 + lambda |b|_1</c> subject to b &gt;= 0 by coordinate descent, from A'y and the
    /// Toeplitz first row of A'A (<see cref="CentrixBasis.GramRow"/>). Each update is the soft threshold
    /// <c>b_j = max(0, rho_j - lambda) / G[j, j]</c>, where <c>rho_j = (A'y)_j - sum_{k != j} G[j, k] b_k</c>.
    /// Only the active set is swept: coefficients above zero or whose rho exceeds lambda. Every
    /// <see cref="CHECK_FREQUENCY"/> sweeps the rest are checked for a KKT violation a neighbor's change opened.
    /// </summary>
    public static class NonNegativeLasso
    {
        /// <summary>Sweeps between full checks of the inactive coefficients.</summary>
        public const int CHECK_FREQUENCY = 10;

        /// <summary>
        /// The solution, from zeros or from a warm start (copied, not changed). Iterations and convergence are
        /// returned for tests and diagnostics.
        /// </summary>
        public static double[] Solve(double[] aty, double[] gramRow, double lambda, double[] warmStart, int maxIterations,
            double tolerance, out int iterations, out bool converged)
        {
            int n = aty.Length;
            iterations = 0;
            converged = true;
            if (n == 0)
                return Array.Empty<double>();

            double g0 = gramRow[0];
            var beta = warmStart != null ? (double[])warmStart.Clone() : new double[n];
            var isActive = new bool[n];
            var active = new List<int>();
            for (int j = 0; j < n; j++)
            {
                if (beta[j] > 0 || aty[j] > lambda)
                {
                    isActive[j] = true;
                    active.Add(j);
                }
            }

            converged = false;
            var nextActive = new List<int>(active.Count);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                iterations = iteration + 1;
                if (iteration % CHECK_FREQUENCY == 0)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (!isActive[j] && Rho(j, beta, aty, gramRow) > lambda)
                        {
                            isActive[j] = true;
                            active.Add(j);
                        }
                    }
                }

                double maxDelta = 0;
                nextActive.Clear();
                foreach (int j in active)
                {
                    double rho = Rho(j, beta, aty, gramRow);
                    double updated = Math.Max(rho - lambda, 0) / g0;
                    double delta = Math.Abs(updated - beta[j]);
                    if (delta > maxDelta)
                        maxDelta = delta;
                    beta[j] = updated;
                    // Kept while above zero or able to leave it.
                    if (updated > 0 || rho > lambda)
                        nextActive.Add(j);
                    else
                        isActive[j] = false;
                }
                (active, nextActive) = (nextActive, active);

                if (maxDelta < tolerance)
                {
                    converged = true;
                    break;
                }
            }

            for (int j = 0; j < n; j++)
            {
                if (beta[j] < 0)
                    beta[j] = 0;
            }
            return beta;
        }

        /// <summary>
        /// One centroid per coefficient above zero, at its position, moved to the vertex of the parabola through the
        /// logarithms of it and its neighbors when both neighbors are above zero, the parabola opens downward and the
        /// vertex is within half a spacing. The intensity is the coefficient; <see cref="CentrixBasis.AreaScale"/>
        /// makes it an integrated intensity.
        /// </summary>
        public static List<(double Mz, double Amplitude)> RefineSubgrid(double[] beta, double[] grid)
        {
            int n = beta.Length;
            var centroids = new List<(double Mz, double Amplitude)>();
            for (int j = 0; j < n; j++)
            {
                if (beta[j] <= 0)
                    continue;
                double mz = grid[j];
                if (j > 0 && j + 1 < n && beta[j - 1] > 0 && beta[j + 1] > 0)
                {
                    double ly0 = Math.Log(beta[j - 1]);
                    double ly1 = Math.Log(beta[j]);
                    double ly2 = Math.Log(beta[j + 1]);
                    double h = grid[j] - grid[j - 1];
                    if (h > 1e-9)
                    {
                        double a = (ly0 - 2 * ly1 + ly2) / (2 * h * h);
                        if (a < 0)
                        {
                            double b = (ly2 - ly0) / (2 * h);
                            double offset = -b / (2 * a);
                            if (Math.Abs(offset) <= 0.5 * h)
                                mz = grid[j] + offset;
                        }
                    }
                }
                centroids.Add((mz, beta[j]));
            }
            return centroids;
        }

        /// <summary>
        /// <c>(A'y)_j - sum_k G[j, k] b_k + G[j, j] b_j</c>, the sum over the coefficients within the Gram row's reach,
        /// in index order.
        /// </summary>
        internal static double Rho(int j, double[] beta, double[] aty, double[] gramRow)
        {
            int reach = gramRow.Length;
            int lo = Math.Max(j - (reach - 1), 0);
            int hi = Math.Min(j + reach, beta.Length);
            double dot = 0;
            for (int k = lo; k < hi; k++)
                dot += gramRow[Math.Abs(k - j)] * beta[k];
            return aty[j] - dot + gramRow[0] * beta[j];
        }
    }
}
