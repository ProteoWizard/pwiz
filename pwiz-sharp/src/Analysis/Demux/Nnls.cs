// C# port of Eigen-NNLS (Non-Negative Least Squares Algorithm for Eigen).
//
// Copyright (C) 2013 Hannes Matuschek <hmatuschek at uni-potsdam.de>
//
// This Source Code Form is subject to the terms of the Mozilla Public License v. 2.0.
// If a copy of the MPL was not distributed with this file, you can obtain one at
// http://mozilla.org/MPL/2.0/.
//
// pwiz cpp ships the original C++ implementation under libraries/Eigen/nnls.h. This file
// reproduces the same Lawson-Hanson 1974 algorithm in C# on top of MathNet.Numerics matrices,
// preserving the Skyline-specific feasibility patch (cpp lines 367-378 of nnls.h).

using MathNet.Numerics.LinearAlgebra;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Non-negative least squares solver. Given an m×n system matrix <c>A</c> and an m-vector
/// <c>b</c>, finds the n-vector <c>x ≥ 0</c> that minimizes <c>‖Ax - b‖²</c>. Direct port of
/// pwiz cpp's <c>Eigen::NNLS</c> (<c>libraries/Eigen/nnls.h</c>) — same Lawson-Hanson 1974
/// algorithm with the Skyline feasibility patch.
/// </summary>
/// <remarks>
/// <para>Reference: Charles L. Lawson and Richard J. Hanson, "Solving Least Squares Problems",
/// Prentice-Hall, 1974, chapter 23. The algorithm partitions coefficient indices into a passive
/// set P (where <c>x[i] &gt; 0</c>) and an active set Z (where <c>x[i] = 0</c>); on each outer
/// iteration it picks the index in Z whose gradient indicates the largest descent and moves it
/// to P, then iteratively shrinks P by removing indices whose unconstrained LS solution would
/// turn negative.</para>
/// <para>Designed for reuse: <c>A^T A</c> is cached at construction as a flat <see cref="double"/>
/// array for fast indexing, and per-call buffers are resized only on shape change. Inner
/// LS sub-problem in P is solved via Cholesky on the cached <c>A^T A</c> submatrix instead of
/// re-running QR on a fresh <c>A^P</c> — Eigen-NNLS uses QR for stability, but for the small,
/// well-conditioned demux design matrices (P ≤ 50, mostly 0/1 entries) Cholesky on normal
/// equations is correct and ~10× faster. <see cref="NnlsSolver"/> uses one solver instance per
/// thread (cpp's OpenMP <c>firstprivate(solver)</c> pattern).</para>
/// </remarks>
public sealed class Nnls
{
    private readonly Matrix<double> _A;
    private readonly double[] _AtAFlat; // n*n row-major copy of A^T A for fast indexing
    private readonly int _n;
    private readonly int _m;
    private readonly int _maxIter;
    private readonly double _epsilon;

    // Per-call working buffers — resized only when n changes (it doesn't, for repeat calls
    // against the same A). Avoids per-Solve GC pressure that dominated the demux hot path.
    private double[] _x;
    private double[] _w;
    private double[] _y;
    private double[] _Atb;
    private int[] _perm;
    private int _Np;
    private int _numLs;

    // Cholesky scratch buffers reused across SolveLsInP calls. Sized to n×n flat + n.
    private readonly double[] _LFlat; // n*n lower-triangular Cholesky factor
    private readonly double[] _zScratch; // n forward/back-sub scratch

    /// <summary>Number of LS sub-problems solved during the most recent solve.</summary>
    public int IterationCount => _numLs;

    /// <summary>The solution vector after a successful solve.</summary>
    public double[] X => _x;

    /// <summary>Constructs an NNLS solver for the system matrix <paramref name="A"/>.</summary>
    /// <param name="A">m×n system matrix.</param>
    /// <param name="maxIter">Maximum LS sub-problems before giving up; -1 means no limit.</param>
    /// <param name="epsilon">Optimality tolerance on the gradient (cpp default 1e-10).</param>
    public Nnls(Matrix<double> A, int maxIter = -1, double epsilon = 1e-10)
    {
        ArgumentNullException.ThrowIfNull(A);
        _A = A;
        _n = A.ColumnCount;
        _m = A.RowCount;
        _maxIter = maxIter;
        _epsilon = epsilon;

        _AtAFlat = new double[_n * _n];
        var ata = A.TransposeThisAndMultiply(A);
        for (int i = 0; i < _n; i++)
            for (int j = 0; j < _n; j++)
                _AtAFlat[i * _n + j] = ata[i, j];

        _x = new double[_n];
        _w = new double[_n];
        _y = new double[_n];
        _Atb = new double[_n];
        _perm = new int[_n];
        _LFlat = new double[_n * _n];
        _zScratch = new double[_n];
    }

    /// <summary>One-shot helper: solves <c>min ‖Ax - b‖² s.t. x ≥ 0</c>. Returns the solution
    /// vector, or null on non-convergence.</summary>
    public static double[]? Solve(Matrix<double> A, Vector<double> b, int maxIter = -1, double epsilon = 1e-10)
    {
        var solver = new Nnls(A, maxIter, epsilon);
        return solver.Solve(b) ? (double[])solver.X.Clone() : null;
    }

    /// <summary>Solves the NNLS problem for the given right-hand side. Returns true on convergence.</summary>
    public bool Solve(Vector<double> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Count != _m)
            throw new ArgumentException($"b has {b.Count} rows but A has {_m}");
        // _Atb = A^T b (manual mult against the original A).
        for (int i = 0; i < _n; i++)
        {
            double s = 0;
            for (int j = 0; j < _m; j++) s += _A[j, i] * b[j];
            _Atb[i] = s;
        }
        return SolveCore();
    }

    /// <summary>Solves <c>min ‖A x - B[:, col]‖² s.t. x ≥ 0</c> reusing pre-computed
    /// <paramref name="AtB"/> = A^T B. Avoids the O(m·n) <c>A^T b</c> mult per column when the
    /// caller already has the full <c>A^T B</c>. Hot path inside <see cref="NnlsSolver"/>.</summary>
    public bool Solve(Matrix<double> AtB, int col)
    {
        ArgumentNullException.ThrowIfNull(AtB);
        if (AtB.RowCount != _n)
            throw new ArgumentException($"AtB has {AtB.RowCount} rows but A has {_n} cols");
        for (int i = 0; i < _n; i++) _Atb[i] = AtB[i, col];
        return SolveCore();
    }

    private bool SolveCore()
    {
        Array.Clear(_x);
        for (int i = 0; i < _n; i++) _perm[i] = i;
        _Np = 0;
        _numLs = 0;

        // Outer loop: pick next index to bring into P, then re-solve.
        while (true)
        {
            UpdateGradient();

            // Converged if every index is in P, or the largest gradient in Z is below tolerance.
            if (_Np == _n || MaxInZ(_w) - _epsilon < 0)
                return true;

            int swapAt = ArgMaxInZ(_w);
            AddToP(swapAt);

            // Inner loop: solve LS in P only; if any partial solution component is non-positive,
            // interpolate to feasibility and drop that index from P.
            while (true)
            {
                if (_maxIter > 0 && _numLs >= _maxIter) return false;

                if (!SolveLsInP()) return false; // singular sub-system

                bool feasible = true;
                double alpha = double.MaxValue;
                int remIdxPos = -1;
                for (int i = 0; i < _Np; i++)
                {
                    int idx = _perm[i];

                    if (_y[idx] <= 0)
                    {
                        // Skyline feasibility patch (cpp nnls.h:367-378): if y == x exactly, treat
                        // alpha as +∞ to skip the division; otherwise interpolate by t = -x / (y - x).
                        double denom = _y[idx] - _x[idx];
                        double t = denom == 0 ? double.MaxValue : -_x[idx] / denom;
                        if (alpha >= t) { alpha = t; remIdxPos = i; }
                        feasible = false;
                    }
                }

                if (feasible)
                {
                    Array.Copy(_y, _x, _n);
                    break;
                }

                // Interpolate towards the feasible region by `alpha`, then drop the offending index.
                for (int i = 0; i < _Np; i++)
                {
                    int idx = _perm[i];
                    _x[idx] += alpha * (_y[idx] - _x[idx]);
                }
                RemFromP(remIdxPos);
            }
        }
    }

    /// <summary>Updates the gradient: <c>w = A^T b - A^T A x</c>.</summary>
    private void UpdateGradient()
    {
        int n = _n;
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            int rowOff = i * n;
            for (int j = 0; j < n; j++) s += _AtAFlat[rowOff + j] * _x[j];
            _w[i] = _Atb[i] - s;
        }
    }

    /// <summary>Finds the index position in Z with the largest <paramref name="v"/> value.</summary>
    private int ArgMaxInZ(double[] v)
    {
        int mPos = _Np;
        double m = v[_perm[mPos]];
        for (int i = _Np + 1; i < _n; i++)
        {
            int idx = _perm[i];
            if (m < v[idx]) { m = v[idx]; mPos = i; }
        }
        return mPos;
    }

    /// <summary>Largest value of <paramref name="v"/> across the active set Z.</summary>
    private double MaxInZ(double[] v)
    {
        double m = v[_perm[_Np]];
        for (int i = _Np + 1; i < _n; i++)
        {
            int idx = _perm[i];
            if (m < v[idx]) m = v[idx];
        }
        return m;
    }

    /// <summary>Moves <c>_perm[swapAt]</c> into the passive set by swapping it with
    /// <c>_perm[_Np]</c> and incrementing <c>_Np</c>.</summary>
    private void AddToP(int swapAt)
    {
        (_perm[swapAt], _perm[_Np]) = (_perm[_Np], _perm[swapAt]);
        _Np++;
    }

    /// <summary>Removes the column at position <paramref name="pos"/> from P.</summary>
    private void RemFromP(int pos)
    {
        // Swap the offender to the end of P, then shrink. cpp's version preserves the order of P
        // and updates the QR rank-1; we don't keep a QR around so order doesn't matter.
        (_perm[pos], _perm[_Np - 1]) = (_perm[_Np - 1], _perm[pos]);
        _Np--;
    }

    /// <summary>Solves the LS sub-problem on the passive set P via Cholesky on the cached
    /// <c>A^T A</c> submatrix. Writes <c>y[_perm[0.._Np)]</c> with the solution.</summary>
    /// <returns>False on non-positive-definite sub-matrix (rank-deficient passive set).</returns>
    private bool SolveLsInP()
    {
        int P = _Np;
        if (P == 0) return true;
        int n = _n;

        // Pull (A^T A)_PP — submatrix at rows/cols _perm[0..P) — into _LFlat row-major.
        for (int i = 0; i < P; i++)
        {
            int rowOff = i * P;
            int srcRow = _perm[i] * n;
            for (int j = 0; j < P; j++) _LFlat[rowOff + j] = _AtAFlat[srcRow + _perm[j]];
        }

        // In-place Cholesky over the lower triangle: L * L^T = (A^T A)_PP.
        for (int k = 0; k < P; k++)
        {
            int kRow = k * P;
            double diag = _LFlat[kRow + k];
            for (int j = 0; j < k; j++) { double v = _LFlat[kRow + j]; diag -= v * v; }
            if (diag <= 0)
            {
                // Singular passive set — bail out the same way the QR-based path bailed on NaN.
                for (int j = 0; j < P; j++) _y[_perm[j]] = double.NaN;
                _numLs++;
                return false;
            }
            double piv = System.Math.Sqrt(diag);
            _LFlat[kRow + k] = piv;
            for (int i = k + 1; i < P; i++)
            {
                int iRow = i * P;
                double s = _LFlat[iRow + k];
                for (int j = 0; j < k; j++) s -= _LFlat[iRow + j] * _LFlat[kRow + j];
                _LFlat[iRow + k] = s / piv;
            }
        }

        // RHS: z = (A^T b)_P, then forward-sub L z = z, then backward-sub L^T y = z.
        for (int i = 0; i < P; i++) _zScratch[i] = _Atb[_perm[i]];
        for (int i = 0; i < P; i++)
        {
            int iRow = i * P;
            double s = _zScratch[i];
            for (int j = 0; j < i; j++) s -= _LFlat[iRow + j] * _zScratch[j];
            _zScratch[i] = s / _LFlat[iRow + i];
        }
        for (int i = P - 1; i >= 0; i--)
        {
            double s = _zScratch[i];
            for (int j = i + 1; j < P; j++) s -= _LFlat[j * P + i] * _zScratch[j]; // L^T[i,j] = L[j,i]
            _zScratch[i] = s / _LFlat[i * P + i];
        }

        Array.Clear(_y);
        for (int j = 0; j < P; j++) _y[_perm[j]] = _zScratch[j];
        _numLs++;
        return true;
    }
}
