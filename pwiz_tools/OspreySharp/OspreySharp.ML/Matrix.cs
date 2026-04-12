// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Simple row-major matrix for machine learning operations.
    /// Port of osprey-ml/src/matrix.rs.
    /// </summary>
    public class Matrix
    {
        private readonly double[] _data;
        private readonly int _rows;
        private readonly int _cols;

        /// <summary>Number of rows.</summary>
        public int Rows { get { return _rows; } }

        /// <summary>Number of columns.</summary>
        public int Cols { get { return _cols; } }

        /// <summary>
        /// Create a new Matrix from flat row-major data.
        /// The data array is defensively cloned so callers can safely reuse their buffer.
        /// </summary>
        public Matrix(double[] data, int rows, int cols)
        {
            if (data.Length != rows * cols)
                throw new ArgumentException(
                    string.Format("data length {0} does not match shape ({1}, {2})", data.Length, rows, cols));
            _data = (double[])data.Clone();
            _rows = rows;
            _cols = cols;
        }

        /// <summary>
        /// Internal constructor that takes ownership of the data array without cloning.
        /// Used by hot-path operations (e.g., ExtractRows in Percolator) where the caller
        /// has just allocated a fresh array and is not going to mutate it. Avoiding the
        /// clone halves allocations in tight SVM training loops.
        /// </summary>
        internal static Matrix WrapNoClone(double[] data, int rows, int cols)
        {
            if (data.Length != rows * cols)
                throw new ArgumentException(
                    string.Format("data length {0} does not match shape ({1}, {2})", data.Length, rows, cols));
            return new Matrix(data, rows, cols, takeOwnership: true);
        }

        private Matrix(double[] data, int rows, int cols, bool takeOwnership)
        {
            _data = data;
            _rows = rows;
            _cols = cols;
        }

        /// <summary>
        /// Create a zero-filled matrix.
        /// </summary>
        public static Matrix Zeros(int rows, int cols)
        {
            return new Matrix(new double[rows * cols], rows, cols);
        }

        /// <summary>
        /// Create an identity matrix.
        /// </summary>
        public static Matrix Identity(int size)
        {
            var m = Zeros(size, size);
            for (int i = 0; i < size; i++)
                m.Set(i, i, 1.0);
            return m;
        }

        /// <summary>
        /// Create a diagonal matrix with the given value on the diagonal.
        /// </summary>
        public static Matrix Diagonal(int size, double value)
        {
            var m = Zeros(size, size);
            for (int i = 0; i < size; i++)
                m.Set(i, i, value);
            return m;
        }

        /// <summary>
        /// Create a matrix from an array of row arrays.
        /// </summary>
        public static Matrix FromRows(double[][] rows)
        {
            if (rows.Length == 0)
                return new Matrix(new double[0], 0, 0);
            int cols = rows[0].Length;
            var data = new double[rows.Length * cols];
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length != cols)
                    throw new ArgumentException("All rows must have the same number of columns");
                Array.Copy(rows[r], 0, data, r * cols, cols);
            }
            return new Matrix(data, rows.Length, cols);
        }

        /// <summary>
        /// Treat data as a column vector (n x 1).
        /// </summary>
        public static Matrix ColVector(double[] data)
        {
            return new Matrix(data, data.Length, 1);
        }

        /// <summary>
        /// Treat data as a row vector (1 x n).
        /// </summary>
        public static Matrix RowVector(double[] data)
        {
            return new Matrix(data, 1, data.Length);
        }

        /// <summary>
        /// Get element at (row, col). Returns null if out of bounds.
        /// </summary>
        public double Get(int row, int col)
        {
            if (row < 0 || row >= _rows || col < 0 || col >= _cols)
                throw new IndexOutOfRangeException(
                    string.Format("Index ({0}, {1}) out of bounds for shape ({2}, {3})", row, col, _rows, _cols));
            return _data[_cols * row + col];
        }

        /// <summary>
        /// Set element at (row, col).
        /// </summary>
        public void Set(int row, int col, double value)
        {
            if (row < 0 || row >= _rows || col < 0 || col >= _cols)
                throw new IndexOutOfRangeException(
                    string.Format("Index ({0}, {1}) out of bounds for shape ({2}, {3})", row, col, _rows, _cols));
            _data[_cols * row + col] = value;
        }

        /// <summary>
        /// Indexer for (row, col) access.
        /// </summary>
        public double this[int row, int col]
        {
            get { return _data[_cols * row + col]; }
            set { _data[_cols * row + col] = value; }
        }

        /// <summary>
        /// Copy a single row into a new array.
        /// </summary>
        public double[] Row(int row)
        {
            var result = new double[_cols];
            Array.Copy(_data, _cols * row, result, 0, _cols);
            return result;
        }

        /// <summary>
        /// Copy a single column into a new array.
        /// </summary>
        public double[] Col(int col)
        {
            var result = new double[_rows];
            for (int r = 0; r < _rows; r++)
                result[r] = _data[_cols * r + col];
            return result;
        }

        /// <summary>
        /// Get a reference to the internal row slice (start index and length).
        /// For performance-critical code that avoids allocation.
        /// </summary>
        public void RowSlice(int row, out int start, out int length)
        {
            start = _cols * row;
            length = _cols;
        }

        /// <summary>
        /// Direct access to internal data for row operations.
        /// </summary>
        internal double[] Data { get { return _data; } }

        /// <summary>
        /// Matrix multiplication: this * rhs.
        /// </summary>
        public static Matrix Dot(Matrix lhs, Matrix rhs)
        {
            if (lhs._cols != rhs._rows)
                throw new ArgumentException(
                    string.Format("Shape mismatch: ({0},{1}) x ({2},{3})",
                        lhs._rows, lhs._cols, rhs._rows, rhs._cols));

            var result = new double[lhs._rows * rhs._cols];
            for (int row = 0; row < lhs._rows; row++)
            {
                for (int col = 0; col < rhs._cols; col++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < lhs._cols; k++)
                        sum += lhs._data[lhs._cols * row + k] * rhs._data[rhs._cols * k + col];
                    result[rhs._cols * row + col] = sum;
                }
            }
            return new Matrix(result, lhs._rows, rhs._cols);
        }

        /// <summary>
        /// Matrix-vector product: this * v.
        /// </summary>
        public static double[] DotVector(Matrix m, double[] v)
        {
            if (m._cols != v.Length)
                throw new ArgumentException(
                    string.Format("Shape mismatch: ({0},{1}) x ({2})", m._rows, m._cols, v.Length));

            var result = new double[m._rows];
            for (int row = 0; row < m._rows; row++)
            {
                double sum = 0.0;
                int offset = m._cols * row;
                for (int k = 0; k < m._cols; k++)
                    sum += m._data[offset + k] * v[k];
                result[row] = sum;
            }
            return result;
        }

        /// <summary>
        /// Transpose this matrix.
        /// </summary>
        public Matrix Transpose()
        {
            if (_cols == 1 || _rows == 1)
            {
                return new Matrix((double[])_data.Clone(), _cols, _rows);
            }
            var result = new double[_rows * _cols];
            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                    result[_rows * col + row] = _data[_cols * row + col];
            }
            return new Matrix(result, _cols, _rows);
        }

        /// <summary>
        /// Extract a submatrix of rows [rowStart, rowEnd).
        /// </summary>
        public Matrix Slice(int rowStart, int rowEnd)
        {
            if (rowStart < 0 || rowEnd > _rows || rowStart > rowEnd)
                throw new ArgumentOutOfRangeException(
                    string.Format("Slice [{0}, {1}) out of bounds for {2} rows", rowStart, rowEnd, _rows));
            int count = rowEnd - rowStart;
            var data = new double[count * _cols];
            Array.Copy(_data, rowStart * _cols, data, 0, count * _cols);
            return new Matrix(data, count, _cols);
        }

        /// <summary>
        /// Extract a submatrix containing only the specified rows, in the order given.
        /// Direct port of osprey-scoring/calibration_ml.rs `extract_rows`. Used by LDA
        /// cross-validation training to build train/test matrices from index arrays.
        /// Uses Array.Copy for each row and WrapNoClone to avoid the defensive clone in
        /// the public constructor (this is called inside tight CV loops over ~200K rows).
        /// </summary>
        public Matrix ExtractRows(int[] rowIndices)
        {
            int nRows = rowIndices.Length;
            var data = new double[nRows * _cols];
            for (int i = 0; i < nRows; i++)
            {
                int srcOffset = rowIndices[i] * _cols;
                int dstOffset = i * _cols;
                Array.Copy(_data, srcOffset, data, dstOffset, _cols);
            }
            return WrapNoClone(data, nRows, _cols);
        }

        /// <summary>
        /// Calculate mean of each column.
        /// </summary>
        public double[] Mean()
        {
            var means = new double[_cols];
            for (int col = 0; col < _cols; col++)
            {
                double sum = 0.0;
                for (int row = 0; row < _rows; row++)
                    sum += _data[_cols * row + col];
                means[col] = sum / _rows;
            }
            return means;
        }

        /// <summary>
        /// Power method to find the eigenvector with the largest eigenvalue.
        /// </summary>
        public double[] PowerMethod(double[] initial)
        {
            double n = MlMath.Norm(initial);
            var v = new double[initial.Length];
            for (int i = 0; i < initial.Length; i++)
                v[i] = initial[i] / n;

            double lastEig = 0.0;
            for (int iter = 0; iter < 50; iter++)
            {
                var v1 = DotVector(this, v);
                double norm = MlMath.Norm(v1);
                if (Math.Abs(norm - lastEig) < 1E-8)
                    break;
                lastEig = norm;
                for (int i = 0; i < v1.Length; i++)
                    v1[i] /= norm;
                v = v1;
            }
            return v;
        }

        /// <summary>
        /// Add another matrix to this one in-place.
        /// </summary>
        public void AddInPlace(Matrix rhs)
        {
            if (_rows != rhs._rows || _cols != rhs._cols)
                throw new ArgumentException("Matrices must have equal shape to add");
            for (int i = 0; i < _data.Length; i++)
                _data[i] += rhs._data[i];
        }

        /// <summary>
        /// Divide all elements by a scalar, returning a new matrix.
        /// </summary>
        public Matrix Divide(double divisor)
        {
            var result = new double[_data.Length];
            for (int i = 0; i < _data.Length; i++)
                result[i] = _data[i] / divisor;
            return new Matrix(result, _rows, _cols);
        }

        /// <summary>
        /// Swap two rows in-place.
        /// </summary>
        internal void SwapRows(int i, int j)
        {
            for (int k = 0; k < _cols; k++)
            {
                double tmp = _data[_cols * i + k];
                _data[_cols * i + k] = _data[_cols * j + k];
                _data[_cols * j + k] = tmp;
            }
        }
    }
}
