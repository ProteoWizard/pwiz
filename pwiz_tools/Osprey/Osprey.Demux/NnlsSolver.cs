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

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// How an NNLS solve was resolved, for the tier statistics.
    /// </summary>
    public enum NnlsPath
    {
        /// <summary>Every right-hand-side value was zero, so the answer is zero.</summary>
        zero,

        /// <summary>
        /// The unconstrained least-squares solution was already non-negative, so it is the
        /// NNLS solution and no active-set iteration ran (spec Tier 1).
        /// </summary>
        unconstrained,

        /// <summary>Lawson-Hanson active-set iteration converged (spec Tier 2).</summary>
        active_set,

        /// <summary>
        /// Lawson-Hanson reached the iteration cap. The last feasible iterate is returned.
        /// </summary>
        iteration_cap,
    }

    /// <summary>
    /// Non-negative least squares for one small design matrix, solved for many right-hand
    /// sides: minimize ||A x - b||^2 subject to x &gt;= 0.
    /// </summary>
    /// <remarks>
    /// <para>Lawson and Hanson (1974), chapter 23, on the normal equations. The demux design
    /// matrices are at most a few dozen columns of 0/1 or fill-time entries, so a Cholesky
    /// factorization of the passive-set block of A^T A is both accurate and far cheaper
    /// than QR on A.</para>
    /// <para>Deterministic by construction: all arithmetic is scalar, every loop runs in index
    /// order, every tie resolves to the lowest index, and the iteration count is capped at
    /// a fixed number. The instance is immutable after construction and safe to share
    /// across threads; all mutable state lives in the caller's <see cref="Workspace"/>.</para>
    /// <para>When A has full column rank the unconstrained solution is precomputed as
    /// (A^T A)^-1 A^T, and a right-hand side whose unconstrained solution is already
    /// non-negative skips the active-set iteration entirely.</para>
    /// </remarks>
    public sealed class NnlsSolver
    {
        private readonly int _rows;
        private readonly int _columns;
        private readonly double[] _a;          // rows x columns, row-major
        private readonly double[] _ata;        // columns x columns, row-major
        private readonly double[] _pseudoInverse; // columns x rows, or null when rank deficient
        private readonly int _maxIterations;

        /// <summary>
        /// Creates a solver for the given design matrix.
        /// </summary>
        /// <param name="a">The design matrix, rows (measurements) by columns (bins).</param>
        /// <param name="maxIterations">
        /// Cap on least-squares sub-problems per solve; 0 selects 3 x columns, the bound
        /// Lawson and Hanson recommend.
        /// </param>
        public NnlsSolver(double[,] a, int maxIterations = 0)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));
            _rows = a.GetLength(0);
            _columns = a.GetLength(1);
            _maxIterations = maxIterations > 0 ? maxIterations : 3 * Math.Max(1, _columns);
            _a = new double[_rows * _columns];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                    _a[r * _columns + c] = a[r, c];
            }
            _ata = new double[_columns * _columns];
            for (int i = 0; i < _columns; i++)
            {
                for (int j = 0; j < _columns; j++)
                {
                    double sum = 0;
                    for (int r = 0; r < _rows; r++)
                        sum += _a[r * _columns + i] * _a[r * _columns + j];
                    _ata[i * _columns + j] = sum;
                }
            }
            _pseudoInverse = ComputePseudoInverse();
        }

        public int Rows { get { return _rows; } }
        public int Columns { get { return _columns; } }

        /// <summary>True when A has full column rank, so the NNLS solution is unique.</summary>
        public bool IsFullColumnRank { get { return _pseudoInverse != null; } }

        /// <summary>
        /// Solves for one right-hand side. <paramref name="b"/> has at least
        /// <see cref="Rows"/> entries; the solution is written to the first
        /// <see cref="Columns"/> entries of <paramref name="x"/>. The workspace must have been
        /// created for at least <see cref="Columns"/> columns, and may be shared by solvers of
        /// different shapes on one thread.
        /// </summary>
        public NnlsPath Solve(double[] b, double[] x, Workspace workspace)
        {
            bool allZero = true;
            for (int r = 0; r < _rows; r++)
            {
                if (b[r] != 0)
                {
                    allZero = false;
                    break;
                }
            }
            Array.Clear(x, 0, _columns);
            if (allZero)
                return NnlsPath.zero;

            if (_pseudoInverse != null)
            {
                bool feasible = true;
                for (int c = 0; c < _columns; c++)
                {
                    double sum = 0;
                    for (int r = 0; r < _rows; r++)
                        sum += _pseudoInverse[c * _rows + r] * b[r];
                    x[c] = sum;
                    if (sum < 0)
                        feasible = false;
                }
                if (feasible)
                    return NnlsPath.unconstrained;
                Array.Clear(x, 0, _columns);
            }

            return SolveActiveSet(b, x, workspace);
        }

        /// <summary>
        /// Solves min ||A x - b||^2 subject to x &gt;= 0 from its normal equations: the n x n
        /// row-major Gram matrix <paramref name="ata"/> = A^T A (or A^T W A for row weights W)
        /// and <paramref name="atb"/> = A^T b. For callers whose design matrix changes from solve
        /// to solve, where building an <see cref="NnlsSolver"/> each time would cost more than the
        /// solve. <paramref name="atb"/> is not modified; the solution goes to the first
        /// <paramref name="n"/> entries of <paramref name="x"/>.
        /// </summary>
        /// <param name="ata">The Gram matrix.</param>
        /// <param name="atb">A^T b.</param>
        /// <param name="n">The number of columns.</param>
        /// <param name="x">Receives the solution.</param>
        /// <param name="workspace">Buffers for at least <paramref name="n"/> columns.</param>
        /// <param name="maxIterations">Cap on least-squares sub-problems; 0 selects 3 x columns.</param>
        /// <param name="initial">
        /// A feasible starting point (entries &gt;= 0), typically the solution of a nearby problem:
        /// its positive entries start in the passive set, which spares the iterations that would
        /// rediscover them. Null starts from x = 0.
        /// </param>
        /// <remarks>
        /// Lawson-Hanson like <see cref="Solve"/>, but the Cholesky factor of the passive block is
        /// extended by one row when a column enters rather than refactored, and only refactored
        /// when columns leave. Same result, same tie-breaking to the lowest index.
        /// </remarks>
        public static NnlsPath SolveNormal(double[] ata, double[] atb, int n, double[] x, Workspace workspace,
            int maxIterations = 0, double[] initial = null)
        {
            Array.Clear(x, 0, n);
            bool allZero = true;
            for (int c = 0; c < n; c++)
            {
                if (atb[c] != 0)
                {
                    allZero = false;
                    break;
                }
            }
            if (allZero)
                return NnlsPath.zero;
            return ActiveSetIncremental(ata, atb, n, maxIterations > 0 ? maxIterations : 3 * Math.Max(1, n), x,
                workspace, initial);
        }

        private static NnlsPath ActiveSetIncremental(double[] g, double[] h, int n, int maxIterations, double[] x,
            Workspace ws, double[] initial)
        {
            double scale = 0;
            for (int c = 0; c < n; c++)
                scale = Math.Max(scale, Math.Abs(h[c]));
            double tolerance = 1e-12 * Math.Max(scale, double.Epsilon) * n;

            var passive = ws.Passive;
            var blocked = ws.Blocked;
            Array.Clear(passive, 0, n);
            Array.Clear(blocked, 0, n);
            var order = ws.Index;      // passive columns, in the factor's row order
            var l = ws.Factor;         // lower Cholesky factor of the passive block, row stride n
            var y = ws.Scratch;
            int p = 0;
            int iterations = 0;

            bool resume = false;
            if (initial != null)
            {
                for (int c = 0; c < n; c++)
                {
                    if (initial[c] > 0)
                    {
                        x[c] = initial[c];
                        passive[c] = true;
                        order[p++] = c;
                    }
                }
                if (p > 0 && !Refactor(g, n, order, p, l))
                {
                    // The warm start's columns are numerically dependent here: start cold.
                    for (int q = 0; q < p; q++)
                    {
                        x[order[q]] = 0;
                        passive[order[q]] = false;
                    }
                    p = 0;
                }
                resume = p > 0;
            }

            while (true)
            {
                int enter = -1;
                if (!resume)
                {
                    // The gradient h - G x, where x is nonzero only on the passive columns.
                    double best = tolerance;
                    for (int c = 0; c < n; c++)
                    {
                        if (passive[c] || blocked[c])
                            continue;
                        double w = h[c];
                        for (int q = 0; q < p; q++)
                            w -= g[c * n + order[q]] * x[order[q]];
                        if (w > best)
                        {
                            best = w;
                            enter = c;
                        }
                    }
                    if (enter < 0)
                        return NnlsPath.active_set;
                    if (!Append(g, n, order, p, l, enter))
                    {
                        // Numerically dependent on the passive columns: it cannot help.
                        blocked[enter] = true;
                        continue;
                    }
                    passive[enter] = true;
                    order[p++] = enter;
                }
                bool firstPass = !resume;
                resume = false;

                while (true)
                {
                    if (++iterations > maxIterations)
                        return NnlsPath.iteration_cap;
                    for (int q = 0; q < p; q++)
                        y[q] = h[order[q]];
                    SolveFactor(l, n, p, y);
                    if (firstPass && y[p - 1] <= 0)
                    {
                        // The entering column's own solution is not positive: chosen on round-off.
                        passive[enter] = false;
                        blocked[enter] = true;
                        p--;
                        break;
                    }
                    firstPass = false;

                    double alpha = 1;
                    int leave = -1;
                    for (int q = 0; q < p; q++)
                    {
                        int c = order[q];
                        if (y[q] > 0)
                            continue;
                        double step = x[c] / (x[c] - y[q]);
                        if (step < alpha || (step == alpha && leave >= 0 && c < leave))
                        {
                            alpha = step;
                            leave = c;
                        }
                    }
                    if (leave < 0)
                    {
                        for (int q = 0; q < p; q++)
                            x[order[q]] = y[q];
                        Array.Clear(blocked, 0, n);
                        break;
                    }
                    int kept = 0;
                    for (int q = 0; q < p; q++)
                    {
                        int c = order[q];
                        x[c] += alpha * (y[q] - x[c]);
                        if (c == leave || x[c] <= 0)
                        {
                            x[c] = 0;
                            passive[c] = false;
                        }
                        else
                        {
                            order[kept++] = c;
                        }
                    }
                    p = kept;
                    if (p > 0 && !Refactor(g, n, order, p, l))
                        return NnlsPath.active_set;  // not reached for a subset of a definite block
                }
            }
        }

        /// <summary>
        /// Extends the factor of the passive block by column <paramref name="t"/>. False if the
        /// column is numerically dependent on the passive ones.
        /// </summary>
        private static bool Append(double[] g, int n, int[] order, int p, double[] l, int t)
        {
            double maxDiagonal = g[t * n + t];
            for (int q = 0; q < p; q++)
                maxDiagonal = Math.Max(maxDiagonal, g[order[q] * n + order[q]]);
            double pivotFloor = 1e-10 * Math.Max(maxDiagonal, double.Epsilon);
            double sumSquares = 0;
            for (int i = 0; i < p; i++)
            {
                double sum = g[order[i] * n + t];
                for (int k = 0; k < i; k++)
                    sum -= l[i * n + k] * l[p * n + k];
                double value = sum / l[i * n + i];
                l[p * n + i] = value;
                sumSquares += value * value;
            }
            double d = g[t * n + t] - sumSquares;
            if (d <= pivotFloor)
                return false;
            l[p * n + p] = Math.Sqrt(d);
            return true;
        }

        /// <summary>The factor of the passive block from scratch, row stride n.</summary>
        private static bool Refactor(double[] g, int n, int[] order, int p, double[] l)
        {
            double maxDiagonal = 0;
            for (int i = 0; i < p; i++)
                maxDiagonal = Math.Max(maxDiagonal, g[order[i] * n + order[i]]);
            double pivotFloor = 1e-10 * Math.Max(maxDiagonal, double.Epsilon);
            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = g[order[i] * n + order[j]];
                    for (int k = 0; k < j; k++)
                        sum -= l[i * n + k] * l[j * n + k];
                    if (i == j)
                    {
                        if (sum <= pivotFloor)
                            return false;
                        l[i * n + i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        l[i * n + j] = sum / l[j * n + j];
                    }
                }
            }
            return true;
        }

        /// <summary>Solves L L^T y = b in place for the p x p factor held with row stride n.</summary>
        private static void SolveFactor(double[] l, int n, int p, double[] y)
        {
            for (int i = 0; i < p; i++)
            {
                double sum = y[i];
                for (int k = 0; k < i; k++)
                    sum -= l[i * n + k] * y[k];
                y[i] = sum / l[i * n + i];
            }
            for (int i = p - 1; i >= 0; i--)
            {
                double sum = y[i];
                for (int k = i + 1; k < p; k++)
                    sum -= l[k * n + i] * y[k];
                y[i] = sum / l[i * n + i];
            }
        }

        private NnlsPath SolveActiveSet(double[] b, double[] x, Workspace ws)
        {
            int n = _columns;
            var atb = ws.Atb;
            for (int c = 0; c < n; c++)
            {
                double sum = 0;
                for (int r = 0; r < _rows; r++)
                    sum += _a[r * n + c] * b[r];
                atb[c] = sum;
            }
            return ActiveSet(_ata, n, _maxIterations, x, ws);
        }

        /// <summary>
        /// Lawson-Hanson on the normal equations, from x = 0, with A^T b already in the
        /// workspace's <see cref="Workspace.Atb"/>.
        /// </summary>
        private static NnlsPath ActiveSet(double[] ata, int n, int maxIterations, double[] x, Workspace ws)
        {
            var atb = ws.Atb;
            double scale = 0;
            for (int c = 0; c < n; c++)
                scale = Math.Max(scale, Math.Abs(atb[c]));
            // Gradient tolerance relative to the problem's own scale, so intensities of 1e2
            // and 1e8 converge the same way.
            double tolerance = 1e-12 * Math.Max(scale, double.Epsilon) * n;

            var passive = ws.Passive;
            var blocked = ws.Blocked;
            Array.Clear(passive, 0, n);
            Array.Clear(blocked, 0, n);
            var w = ws.Gradient;
            var z = ws.Candidate;
            int iterations = 0;

            while (true)
            {
                ComputeGradient(ata, n, atb, x, w);
                int enter = -1;
                double best = tolerance;
                for (int c = 0; c < n; c++)
                {
                    if (passive[c] || blocked[c])
                        continue;
                    if (w[c] > best)
                    {
                        best = w[c];
                        enter = c;
                    }
                }
                if (enter < 0)
                    return NnlsPath.active_set;

                passive[enter] = true;
                bool firstPass = true;
                while (true)
                {
                    if (++iterations > maxIterations)
                        return NnlsPath.iteration_cap;
                    if (!SolvePassive(ata, n, atb, passive, z, ws))
                    {
                        // The passive columns became numerically dependent; the entering column
                        // cannot help, so exclude it and look for another.
                        passive[enter] = false;
                        x[enter] = 0;
                        blocked[enter] = true;
                        break;
                    }
                    if (firstPass && z[enter] <= 0)
                    {
                        // Standard Lawson-Hanson safeguard: a column whose own solution is not
                        // positive was chosen on round-off, so block it and choose again.
                        passive[enter] = false;
                        blocked[enter] = true;
                        break;
                    }
                    firstPass = false;

                    double alpha = 1;
                    int leave = -1;
                    for (int c = 0; c < n; c++)
                    {
                        if (!passive[c] || z[c] > 0)
                            continue;
                        double step = x[c] / (x[c] - z[c]);
                        if (step < alpha)
                        {
                            alpha = step;
                            leave = c;
                        }
                    }
                    if (leave < 0)
                    {
                        for (int c = 0; c < n; c++)
                            x[c] = passive[c] ? z[c] : 0;
                        Array.Clear(blocked, 0, n);
                        break;
                    }
                    for (int c = 0; c < n; c++)
                    {
                        if (!passive[c])
                            continue;
                        x[c] += alpha * (z[c] - x[c]);
                        if (c == leave || x[c] <= 0)
                        {
                            x[c] = 0;
                            passive[c] = false;
                        }
                    }
                }
            }
        }

        private static void ComputeGradient(double[] ata, int n, double[] atb, double[] x, double[] w)
        {
            for (int i = 0; i < n; i++)
            {
                double sum = atb[i];
                for (int j = 0; j < n; j++)
                    sum -= ata[i * n + j] * x[j];
                w[i] = sum;
            }
        }

        /// <summary>
        /// Solves the unconstrained least-squares problem restricted to the passive columns,
        /// writing zeros elsewhere. Returns false if that block of A^T A is not positive
        /// definite.
        /// </summary>
        private static bool SolvePassive(double[] ata, int n, double[] atb, bool[] passive, double[] z, Workspace ws)
        {
            var index = ws.Index;
            int p = 0;
            for (int c = 0; c < n; c++)
            {
                z[c] = 0;
                if (passive[c])
                    index[p++] = c;
            }
            var l = ws.Factor;
            if (!CholeskyFactor(ata, n, index, p, l))
                return false;
            var y = ws.Scratch;
            for (int i = 0; i < p; i++)
                y[i] = atb[index[i]];
            CholeskySolve(l, p, y);
            for (int i = 0; i < p; i++)
                z[index[i]] = y[i];
            return true;
        }

        private double[] ComputePseudoInverse()
        {
            int n = _columns;
            if (n == 0 || _rows < n)
                return null;
            var index = new int[n];
            for (int i = 0; i < n; i++)
                index[i] = i;
            var l = new double[n * n];
            if (!CholeskyFactor(_ata, n, index, n, l))
                return null;
            var result = new double[n * _rows];
            var column = new double[n];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < n; c++)
                    column[c] = _a[r * n + c];
                CholeskySolve(l, n, column);
                for (int c = 0; c < n; c++)
                    result[c * _rows + r] = column[c];
            }
            return result;
        }

        /// <summary>
        /// Cholesky factorization of the principal submatrix of a symmetric matrix selected
        /// by <paramref name="index"/>. The lower factor goes to <paramref name="l"/> as a
        /// p x p row-major array.
        /// </summary>
        private static bool CholeskyFactor(double[] matrix, int n, int[] index, int p, double[] l)
        {
            // A pivot this far below the largest diagonal entry means the selected columns are
            // numerically dependent.
            double maxDiagonal = 0;
            for (int i = 0; i < p; i++)
                maxDiagonal = Math.Max(maxDiagonal, matrix[index[i] * n + index[i]]);
            double pivotFloor = 1e-10 * Math.Max(maxDiagonal, double.Epsilon);

            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = matrix[index[i] * n + index[j]];
                    for (int k = 0; k < j; k++)
                        sum -= l[i * p + k] * l[j * p + k];
                    if (i == j)
                    {
                        if (sum <= pivotFloor)
                            return false;
                        l[i * p + i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        l[i * p + j] = sum / l[j * p + j];
                    }
                }
            }
            return true;
        }

        /// <summary>Solves L L^T y = b in place, for a p x p lower factor.</summary>
        private static void CholeskySolve(double[] l, int p, double[] y)
        {
            for (int i = 0; i < p; i++)
            {
                double sum = y[i];
                for (int k = 0; k < i; k++)
                    sum -= l[i * p + k] * y[k];
                y[i] = sum / l[i * p + i];
            }
            for (int i = p - 1; i >= 0; i--)
            {
                double sum = y[i];
                for (int k = i + 1; k < p; k++)
                    sum -= l[k * p + i] * y[k];
                y[i] = sum / l[i * p + i];
            }
        }

        /// <summary>
        /// Mutable per-thread buffers for <see cref="Solve"/>, sized for a maximum column count.
        /// </summary>
        public sealed class Workspace
        {
            public Workspace(int columns)
            {
                Capacity = columns;
                Atb = new double[columns];
                Gradient = new double[columns];
                Candidate = new double[columns];
                Passive = new bool[columns];
                Blocked = new bool[columns];
                Index = new int[columns];
                Factor = new double[columns * columns];
                Scratch = new double[columns];
            }

            /// <summary>The most columns a solver may have to use this workspace.</summary>
            public int Capacity { get; }

            internal double[] Atb { get; }
            internal double[] Gradient { get; }
            internal double[] Candidate { get; }
            internal bool[] Passive { get; }
            internal bool[] Blocked { get; }
            internal int[] Index { get; }
            internal double[] Factor { get; }
            internal double[] Scratch { get; }
        }
    }
}
