// Gauss-Jordan elimination for solution of systems of linear equations
//
// LDA requires solving the generalized eigenvalue problem for scatter matrices.
// We solve the linear system Sw * x = Sb, then calculate the eigenvalue for x.
//
// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Gauss-Jordan elimination solver.
    /// Port of gauss.rs.
    /// </summary>
    internal class GaussSolver
    {
        private Matrix _left;
        private Matrix _right;

        private GaussSolver(Matrix left, Matrix right)
        {
            _left = left;
            _right = right;
        }

        /// <summary>
        /// Solve the system left * X = right using Gauss-Jordan elimination.
        /// Returns the solution matrix, or null if solving fails.
        /// </summary>
        public static Matrix Solve(Matrix left, Matrix right)
        {
            double eps = 1E-8;
            while (eps <= 1.0)
            {
                Matrix result = SolveInner(left, right, eps);
                if (result != null)
                    return result;
                eps *= 10.0;
            }
            return null;
        }

        private static Matrix SolveInner(Matrix left, Matrix right, double eps)
        {
            // Clone the matrices so we don't modify the originals
            var leftData = new double[left.Rows * left.Cols];
            var rightData = new double[right.Rows * right.Cols];
            for (int r = 0; r < left.Rows; r++)
                for (int c = 0; c < left.Cols; c++)
                    leftData[left.Cols * r + c] = left[r, c];
            for (int r = 0; r < right.Rows; r++)
                for (int c = 0; c < right.Cols; c++)
                    rightData[right.Cols * r + c] = right[r, c];

            var l = new Matrix(leftData, left.Rows, left.Cols);
            var rr = new Matrix(rightData, right.Rows, right.Cols);

            var solver = new GaussSolver(l, rr);
            solver.FillZero(eps);
            solver.Echelon();
            solver.Reduce();
            solver.Backfill();

            if (solver.LeftSolved())
                return solver._right;
            return null;
        }

        /// <summary>
        /// Add eps to diagonal to handle zero diagonals.
        /// </summary>
        private void FillZero(double eps)
        {
            for (int i = 0; i < _left.Cols; i++)
                _left[i, i] += eps;
        }

        /// <summary>
        /// Check if left is an identity matrix (or has rows of all zeros).
        /// </summary>
        private bool LeftSolved()
        {
            int n = _left.Cols;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double x = _left[i, j];
                    if (i == j)
                    {
                        if (x != 1.0 && x != 0.0)
                            return false;
                    }
                    else if (x > 1E-8)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Row echelon form via partial pivoting.
        /// </summary>
        private void Echelon()
        {
            int m = _left.Rows;
            int n = _left.Cols;
            int h = 0;
            int k = 0;

            while (h < m && k < n)
            {
                // Find the row with the largest value in column k
                int maxRow = h;
                double maxVal = double.MinValue;
                for (int i = h; i < m; i++)
                {
                    if (_left[i, k] >= maxVal)
                    {
                        maxVal = _left[i, k];
                        maxRow = i;
                    }
                }

                if (_left[maxRow, k] == 0.0)
                {
                    k++;
                    continue;
                }

                // Swap rows (partial pivoting)
                if (h != maxRow)
                {
                    _left.SwapRows(h, maxRow);
                    _right.SwapRows(h, maxRow);
                }

                // Clear rows below pivot row
                for (int i = h + 1; i < m; i++)
                {
                    double factor = _left[i, k] / _left[h, k];
                    _left[i, k] = 0.0;
                    for (int j = k + 1; j < n; j++)
                        _left[i, j] -= _left[h, j] * factor;
                    for (int j = 0; j < _right.Cols; j++)
                        _right[i, j] -= _right[h, j] * factor;
                }
                h++;
                k++;
            }
        }

        /// <summary>
        /// Reduce to reduced row echelon form (diagonal is all ones).
        /// </summary>
        private void Reduce()
        {
            for (int i = _left.Rows - 1; i >= 0; i--)
            {
                for (int j = 0; j < _left.Cols; j++)
                {
                    double x = _left[i, j];
                    if (x == 0.0)
                        continue;
                    for (int kk = j; kk < _left.Cols; kk++)
                        _left[i, kk] /= x;
                    for (int kk = 0; kk < _right.Cols; kk++)
                        _right[i, kk] /= x;
                    break;
                }
            }
        }

        /// <summary>
        /// Back-substitution for upper triangular matrix.
        /// </summary>
        private void Backfill()
        {
            for (int i = _left.Rows - 1; i >= 0; i--)
            {
                for (int j = 0; j < _left.Cols; j++)
                {
                    if (_left[i, j] == 0.0)
                        continue;
                    for (int k = 0; k < i; k++)
                    {
                        double factor = _left[k, j] / _left[i, j];
                        for (int hh = 0; hh < _left.Cols; hh++)
                            _left[k, hh] -= _left[i, hh] * factor;
                        for (int hh = 0; hh < _right.Cols; hh++)
                            _right[k, hh] -= _right[i, hh] * factor;
                    }
                    break;
                }
            }
        }
    }
}
