/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Properties;

namespace pwiz.Common.DataAnalysis.Matrices
{
    /// <summary>
    /// Performs QR factorization of a matrix with a tolerance for detecting linear dependence of columns.
    /// This implementation was originally taken from the class 
    /// <see cref="MathNet.Numerics.Providers.LinearAlgebra.ManagedLinearAlgebraProvider" />
    /// but was then modified to enable a tolerance for detecting linear dependence.
    /// The logic for detecting linear dependence was taken from the R source code in the file "dqrdc2.f".
    /// </summary>
    // ReSharper disable NonLocalizedString
    public class QrFactorization
    {
        public static QrFactorization GetQrFactorization(Matrix<double> matrix, double tolerance)
        {
            double[] tau = new double[Math.Min(matrix.RowCount, matrix.ColumnCount)];
            double[,] rFull = matrix.ToArray();
            double[,] q = new DenseMatrix(matrix.RowCount).ToArray();
            var solver = new QrSolver {Tolerance = tolerance};
            int[] indepColumnIndexes = solver.QRFactor(rFull, q, tau);
            return new QrFactorization(q, rFull, tau, indepColumnIndexes);
        }

        private readonly double[,] _q;
        private readonly double[] _qFlat;
        private readonly double[,] _rFull;
        private readonly double[] _independentRFlat;

        private readonly double[] _tau;
        private readonly int[] _independentColumnIndexes;

        private QrFactorization(double[,] q, double[,] rFull, double[] tau, int[] independentColumnIndexes)
        {
            _q = q;
            _qFlat = ToFlatArray(_q);
            _rFull = rFull;
            _tau = tau;
            _independentColumnIndexes = independentColumnIndexes;
            double[,] independentR;
            if (_independentColumnIndexes.SequenceEqual(Enumerable.Range(0, _rFull.GetLength(0))))
            {
                independentR = _rFull;
            }
            else
            {
                independentR = new double[_rFull.GetLength(0), _independentColumnIndexes.Length];
                for (int iRow = 0; iRow < independentR.GetLength(0); iRow++)
                {
                    for (int iCol = 0; iCol < _independentColumnIndexes.Length; iCol++)
                    {
                        independentR[iRow, iCol] = _rFull[iRow, iCol];
                    }
                }
            }
            _independentRFlat = ToFlatArray(independentR);
        }

        public double[] Solve(double[] values)
        {
            double[] finalResult = new double[_rFull.GetLength(1)];
            var originalResult = new double[NumberIndependentColumns];
            Control.LinearAlgebraProvider.QRSolveFactored(_qFlat,
                _independentRFlat, _q.GetLength(0), NumberIndependentColumns, 
                _tau.Take(NumberIndependentColumns).ToArray(),
                values, 1, originalResult);
            for (int i = 0; i < NumberIndependentColumns; i++)
            {
                finalResult[_independentColumnIndexes[i]] = originalResult[i];
            }
            return finalResult;
        }

        private static T[] ToFlatArray<T>(T[,] array)
        {
            int rowCount = array.GetLength(0);
            int colCount = array.GetLength(1);
            var result = new T[rowCount*colCount];
            for (int iRow = 0; iRow < rowCount; iRow++)
            {
                for (int iCol = 0; iCol < colCount; iCol++)
                {
                    result[iRow + iCol * rowCount] = array[iRow, iCol];
                }
            }
            return result;
        }

        public int NumberIndependentColumns { get { return _independentColumnIndexes.Length; } }
        public IList<int> IndependentColumnIndexes { get { return Array.AsReadOnly(_independentColumnIndexes); }}

        private class QrSolver
        {
            public double Tolerance { get; set; }
            /// <summary>
            /// Computes the QR factorization of A.
            /// </summary>
            /// <param name="r">On entry, it is the M by N A matrix to factor. On exit,
            /// it is overwritten with the R matrix of the QR factorization. </param>
            /// <param name="q">On exit, A M by M matrix that holds the Q matrix of the
            /// QR factorization.</param>
            /// <param name="tau">A min(m,n) vector. On exit, contains additional information
            /// to be used by the QR solve routine.</param>
            /// <remarks>This is similar to the GEQRF and ORGQR LAPACK routines.</remarks>
            public int[] QRFactor(double[,] r, double[,] q, double[] tau)
            {
                if (r == null)
                {
                    throw new ArgumentNullException("r");
                }

                if (q == null)
                {
                    throw new ArgumentNullException("q");
                }
                int rowsR = r.GetLength(0);
                int columnsR = r.GetLength(1);
                var minmn = Math.Min(rowsR, columnsR);
                if (tau.Length < minmn)
                {
                    throw new ArgumentException(string.Format(Resources.ArrayTooSmall, "min(m,n)"), "tau");
                }

                if (q.GetLength(0) != r.GetLength(0) || q.GetLength(1) != r.GetLength(0))
                {
                    throw new ArgumentException(string.Format(Resources.ArgumentArrayWrongLength, "rowsR * rowsR"), "q");
                }
                for (int i = 0; i < r.GetLength(0); i++)
                {
                    q[i, i] = 1.0;
                }

                var originalNorms = Enumerable.Range(0, columnsR).Select(iCol => GetColumnNorm2(r, iCol)).ToArray();
                int[] originalColumnIndexes = Enumerable.Range(0, columnsR).ToArray();
                int indepColCount = columnsR;

                var newColumns = new List<double[]>();
                for (var i = 0; i < minmn; i++)
                {
                    // Check whether this column has a negligible norm.
                    while (i < indepColCount && GetLowerColumnNorm2(r, i) < originalNorms[i] * Tolerance)
                    {
                        MoveColumnToEnd(r, i);
                        MoveToEnd(originalNorms, i);
                        MoveToEnd(originalColumnIndexes, i);
                        indepColCount--;
                    }
                    var newColumn = GenerateColumn(r, i, i);
                    newColumns.Add(newColumn);
                    ComputeQR(newColumn, r, i, i + 1);
                }

                for (var i = minmn - 1; i >= 0; i--)
                {
                    ComputeQR(newColumns[i], q, i, i);
                }
                return originalColumnIndexes.Take(indepColCount).ToArray();
            }

            /// <summary>
            /// Perform calculation of Q or R
            /// </summary>
            /// <param name="newColumn">Work array</param>
            /// <param name="a">Q or R matrices</param>
            /// <param name="rowStart">The row to start at</param>
            /// <param name="columnStart">The column to start at</param>
            static void ComputeQR(double[] newColumn, double[,] a, int rowStart, int columnStart)
            {
                int rowCount = a.GetLength(0);
                int columnCount = a.GetLength(1);
                if (rowStart > rowCount || columnStart > columnCount)
                {
                    return;
                }

                for (var j = columnStart; j < columnCount; j++)
                {
                    var scale = 0.0;
                    for (var i = rowStart; i < rowCount; i++)
                    {
                        scale += newColumn[i - rowStart] * a[i, j];
                    }

                    for (var i = rowStart; i < rowCount; i++)
                    {
                        a[i, j] -= newColumn[i - rowStart] * scale;
                    }
                }
            }

            /// <summary>
            /// Generate column from initial matrix to work array
            /// </summary>
            /// <param name="a">Initial matrix</param>
            /// <param name="row">The first row</param>
            /// <param name="column">Column index</param>
            static double[] GenerateColumn(double[,] a, int row, int column)
            {
                int rowCount = a.GetLength(0);
                var resultColumn = new double[rowCount - row];

                for (int i = row; i < rowCount; i++)
                {
                    resultColumn[i - row] = a[i, column];
                    a[i, column] = 0.0;
                }

                var norm = 0.0;
                for (var i = 0; i < rowCount - row; ++i)
                {
                    norm += resultColumn[i] * resultColumn[i];
                }

                norm = Math.Sqrt(norm);
                if (row == rowCount - 1 || norm == 0)
                {
                    a[row, column] = -resultColumn[0];
                    resultColumn[0] = Constants.Sqrt2;
                    return resultColumn;
                }

                var scale = 1.0 / norm;
                if (resultColumn[0] < 0.0)
                {
                    scale *= -1.0;
                }

                a[row, column] = -1.0 / scale;
                for (int i = 0; i < rowCount - row; i++)
                {
                    resultColumn[i] *= scale;
                }
                resultColumn[0] += 1.0;

                var s = Math.Sqrt(1.0 / resultColumn[0]);
                for (int i = 0; i < rowCount - row; i++)
                {
                    resultColumn[i] *= s;
                }
                return resultColumn;
            }

            private double GetColumnNorm2(double[,] matrix, int columnIndex)
            {
                double total = 0;
                for (int iRow = 0; iRow < matrix.GetLength(0); iRow++)
                {
                    var value = matrix[iRow, columnIndex];
                    total += value * value;
                }
                return Math.Sqrt(total);
            }

            private double GetLowerColumnNorm2(double[,] matrix, int columnRowIndex)
            {
                double total = 0;
                for (int iRow = columnRowIndex; iRow < matrix.GetLength(0); iRow++)
                {
                    var value = matrix[iRow, columnRowIndex];
                    total += value * value;
                }
                return Math.Sqrt(total);
            }
            private void MoveColumnToEnd(double[,] matrix, int columnIndex)
            {
                for (int iRow = 0; iRow < matrix.GetLength(0); iRow++)
                {
                    double tmp = matrix[iRow, columnIndex];
                    for (int iCol = columnIndex + 1; iCol < matrix.GetLength(1); iCol++)
                    {
                        matrix[iRow, iCol - 1] = matrix[iRow, iCol];
                    }
                    matrix[iRow, matrix.GetLength(1) - 1] = tmp;
                }
            }

            private void MoveToEnd<T>(T[] values, int index)
            {
                T tmp = values[index];
                for (int i = index + 1; i < values.Length; i++)
                {
                    values[i - 1] = values[i];
                }
                values[values.Length - 1] = tmp;
            }
        }
    }
}
