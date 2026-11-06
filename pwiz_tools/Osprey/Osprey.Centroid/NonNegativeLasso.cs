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
    /// The non-negative lasso of one signal region: minimizes <c>1/2 |y - A b|^2 + lambda |b|_1</c> subject to
    /// b &gt;= 0, from A'y and the Toeplitz first row of A'A (<see cref="CentrixBasis.GramRow"/>). Note that Centrix uses
    /// the analytic Gram of the infinite grid, not A'A of the region's own points, so a region's edge columns are
    /// treated as whole peaks; both solvers here solve that same problem.
    /// <para><see cref="Solve"/> is Centrix's coordinate descent. Each update is the soft threshold
    /// <c>b_j = max(0, rho_j - lambda) / G[j, j]</c>, where <c>rho_j = (A'y)_j - sum_{k != j} G[j, k] b_k</c>.
    /// Only the active set is swept: coefficients above zero or whose rho exceeds lambda. Every
    /// <see cref="CHECK_FREQUENCY"/> sweeps the rest are checked for a KKT violation a neighbor's change opened. It
    /// stops when no coefficient moves by the tolerance, or at the iteration cap; with neighboring columns correlated
    /// 0.91 the cap is reached in about 6% of Stellar regions, which then keep an unconverged solution.</para>
    /// <para><see cref="SolveActiveSet"/> reaches the same problem's exact optimum, which coordinate descent converges
    /// to, by solving the few nonzero coefficients directly. Its passive systems are ill-conditioned for runs of
    /// adjacent coefficients (condition number about 1e9 for 20 adjacent at the Stellar's MS2 spacing, 2e11 for 10 at
    /// its MS1 spacing), which Cholesky in double precision still solves; a failed factorization returns null and the
    /// caller falls back to coordinate descent. <c>CentrixTest.TestCentrixActiveSet</c> checks its optimality
    /// conditions on random regions.</para>
    /// </summary>
    public static class NonNegativeLasso
    {
        /// <summary>Sweeps between full checks of the inactive coefficients.</summary>
        public const int CHECK_FREQUENCY = 10;

        /// <summary>
        /// The active-set solve stops when no coefficient's rho exceeds lambda by more than this fraction of the
        /// largest A'y: far below a profile's precision, far above the rounding of the small solves.
        /// </summary>
        public const double ACTIVE_SET_TOLERANCE = 1e-11;

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
        /// The exact solution of the same problem, written as <c>min 1/2 b'Gb - (A'y - lambda)'b</c> subject to b &gt;= 0
        /// and solved by Lawson and Hanson's active-set method: the passive set (coefficients above zero) is solved
        /// exactly, stepping back to feasibility whenever a solve leaves one at or below zero, and the coefficient whose
        /// rho most exceeds lambda joins it, until none does. That is the point coordinate descent converges to, reached
        /// in about as many small solves as there are peaks rather than hundreds of sweeps. A warm start's coefficients
        /// above zero start the passive set. Null when a passive system is not numerically positive definite or the
        /// set cycles, for the caller to fall back to <see cref="Solve"/>.
        /// </summary>
        public static double[] SolveActiveSet(double[] aty, double[] gramRow, double lambda, double[] warmStart)
        {
            int n = aty.Length;
            int iterations = 0;
            var beta = new double[n];
            if (n == 0)
                return beta;

            var c = new double[n];
            double scale = 0;
            for (int j = 0; j < n; j++)
            {
                c[j] = aty[j] - lambda;
                scale = Math.Max(scale, Math.Abs(aty[j]));
            }
            double tolerance = ACTIVE_SET_TOLERANCE * scale;
            var passive = new List<int>();
            var isPassive = new bool[n];
            if (warmStart != null)
            {
                for (int j = 0; j < n; j++)
                {
                    if (warmStart[j] > 0)
                    {
                        beta[j] = warmStart[j];
                        passive.Add(j);
                        isPassive[j] = true;
                    }
                }
            }

            int maxIterations = 4 * n + 20;
            while (iterations < maxIterations)
            {
                iterations++;
                // Optimal on the passive set: each solve that leaves a coefficient at or below zero is taken only as
                // far as the first coefficient it zeroes, which leaves the set.
                while (passive.Count > 0)
                {
                    var s = SolvePassive(passive, c, gramRow);
                    if (s == null)
                        return null;
                    double alpha = 1;
                    int leaving = -1;
                    for (int p = 0; p < passive.Count; p++)
                    {
                        if (s[p] > 0)
                            continue;
                        double current = beta[passive[p]];
                        double step = current > 0 ? current / (current - s[p]) : 0;
                        if (step < alpha)
                        {
                            alpha = step;
                            leaving = p;
                        }
                    }
                    if (leaving < 0)
                    {
                        for (int p = 0; p < passive.Count; p++)
                            beta[passive[p]] = s[p];
                        break;
                    }
                    for (int p = 0; p < passive.Count; p++)
                        beta[passive[p]] += alpha * (s[p] - beta[passive[p]]);
                    beta[passive[leaving]] = 0;
                    for (int p = passive.Count - 1; p >= 0; p--)
                    {
                        if (beta[passive[p]] <= 0)
                        {
                            beta[passive[p]] = 0;
                            isPassive[passive[p]] = false;
                            passive.RemoveAt(p);
                        }
                    }
                }

                // The coefficient outside the set whose rho exceeds lambda the most, ties to the lowest index.
                int entering = -1;
                double most = tolerance;
                for (int j = 0; j < n; j++)
                {
                    if (isPassive[j])
                        continue;
                    double w = c[j] - GramDot(j, beta, gramRow, passive);
                    if (w > most)
                    {
                        most = w;
                        entering = j;
                    }
                }
                if (entering < 0)
                    return beta;
                int at = passive.BinarySearch(entering);
                passive.Insert(~at, entering);
                isPassive[entering] = true;
            }
            return null;
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

        /// <summary><c>(G b)_j</c> over the passive coefficients, the only ones above zero.</summary>
        private static double GramDot(int j, double[] beta, double[] gramRow, List<int> passive)
        {
            double dot = 0;
            foreach (int k in passive)
            {
                int d = Math.Abs(k - j);
                if (d < gramRow.Length)
                    dot += gramRow[d] * beta[k];
            }
            return dot;
        }

        /// <summary>
        /// The solution of <c>G_PP s = c_P</c> by Cholesky factorization, ordered as the passive set; null when a pivot
        /// is not positive, that is, when the set's peaks are too nearly dependent to solve in double precision.
        /// </summary>
        private static double[] SolvePassive(List<int> passive, double[] c, double[] gramRow)
        {
            int m = passive.Count;
            var l = new double[m * m];
            for (int i = 0; i < m; i++)
            {
                for (int k = 0; k <= i; k++)
                {
                    int d = Math.Abs(passive[i] - passive[k]);
                    double sum = d < gramRow.Length ? gramRow[d] : 0;
                    for (int t = 0; t < k; t++)
                        sum -= l[i * m + t] * l[k * m + t];
                    if (i == k)
                    {
                        if (!(sum > 0))
                            return null;
                        l[i * m + i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        l[i * m + k] = sum / l[k * m + k];
                    }
                }
            }
            var s = new double[m];
            for (int i = 0; i < m; i++)
            {
                double sum = c[passive[i]];
                for (int t = 0; t < i; t++)
                    sum -= l[i * m + t] * s[t];
                s[i] = sum / l[i * m + i];
            }
            for (int i = m - 1; i >= 0; i--)
            {
                double sum = s[i];
                for (int t = i + 1; t < m; t++)
                    sum -= l[t * m + i] * s[t];
                s[i] = sum / l[i * m + i];
            }
            return s;
        }
    }
}
