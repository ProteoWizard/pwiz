/*
 * Translator: Jarrett Egertson <jegertso .at. u.washington.edu>,
 *             MacCoss Lab, Department of Genome Sciences, UW
 *
 * Translated to C# from the Matlab implementation lsqnonneg:
 * 
 * http://www.mathworks.com/help/techdoc/ref/lsqnonneg.html
 */


/***************************************************************************
*  Copyright 1984-2008 The MathWorks, Inc.
*  $Revision: 1.15.4.12.2.1 $  $Date: 2008/12/22 17:26:15 $
*
*  Reference:
*  Lawson and Hanson, "Solving Least Squares Problems", Prentice-Hall, 1974.
****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using pwiz.Skyline.Properties;
using LinProvider = MathNet.Numerics.Providers.LinearAlgebra.ManagedLinearAlgebraProvider;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// a simple wrapper around Matrix to keep track of the virtual and real size
    /// of the matrix are
    /// </summary>
    public class MatrixWrap
    {
        public Matrix<double> Matrix { get; set; }
        public int MaxRows { get; private set; }
        public int NumRows { get; private set; }
        public int MaxCols { get; private set; }
        public int NumCols { get; private set; }
        public MatrixWrap (int maxRows, int maxCols)
        {
            Matrix = new DenseMatrix(maxRows, maxCols);
            MaxRows = maxRows;
            MaxCols = maxCols;
            NumRows = 0;
            NumCols = 0;
        }

        public void Resize(int numRows, int numCols)
        {
            SetNumRows(numRows);
            SetNumCols(numCols);
        }
        public void SetNumCols(int numCols)
        {
            if (numCols < 0 || numCols > MaxCols)
                throw new ArgumentOutOfRangeException("numCols"); // Not L10N
            NumCols = numCols;
        }
        public void IncrementNumCols()
        {
            SetNumCols(NumCols + 1);
        }
        public void SetNumRows(int numRows)
        {
            if (numRows < 0 || numRows > MaxRows)
                throw new ArgumentOutOfRangeException("numRows"); // Not L10N
            NumRows = numRows;
        }
        public void IncrementNumRows()
        {
            SetNumRows(NumRows+1);
        }

        public void Clear()
        {
            Matrix.Clear();
        }
        public void Reset()
        {
            Matrix.Clear();
            NumRows = 0;
            NumCols = 0;
        }
    }

    public interface IBlockConditioner
    {
        void Condition(DeconvBlock db);
    }

    public interface ILsSolver
    {
        void Solve(DeconvBlock db);
    }



    public class WeightedConditioner : IBlockConditioner
    {
        private readonly double[] _smoothCoefs;
        public WeightedConditioner()
        {
            _smoothCoefs = new[]{-0.086, 0.343, 0.486, 0.343, -0.086 };
        }
        public WeightedConditioner(double[] smoothCoefs)
        {
            _smoothCoefs = smoothCoefs;
        }
        public void Condition(DeconvBlock db)
        {
            int numRows = db.NumRows;
            if (numRows >=5)
            {
                int rowsPerSection = numRows/5;
                for (int i = 0; i < numRows; ++i)
                {
                    int smoothCoefIndex = i/rowsPerSection;
                    // boundary scenario
                    if (smoothCoefIndex >= 5)
                    {
                        smoothCoefIndex = 4;
                    }
                    double smoothCoef = _smoothCoefs[smoothCoefIndex];
                    for (int j = 0; j < db.Masks.NumCols; ++j)
                        db.Masks.Matrix[i, j] *= smoothCoef;
                    for (int j = 0; j < db.BinnedData.NumCols; ++j)
                        db.BinnedData.Matrix[i, j] *= smoothCoef;
                }
            }
            db.BinnedData.Matrix.NormalizeColumns(1);
        }
    }

    /// <summary>
    /// An efficient set which holds a integers given constrained to 
    /// </summary>
    public class SizedSet : IEnumerable<int>
    {
        private readonly bool[] _data;
        public int Count { get; private set; }
        public int Size { get; private set; }

        public SizedSet(int setSize)
        {
            _data = new bool[setSize];
            for (int i = 0; i < _data.Length; ++i)
                _data[i] = false;
            Size = setSize;
        }

        public void Clear()
        {
            for (int i = 0; i < _data.Length; ++i)
                _data[i] = false;
            Count = 0;
        }

        public void Add(int value)
        {
            if (!CheckBoundary(value))
                throw new IndexOutOfRangeException(Resources.SizedSet_Add_SizedSet_index_value_is_out_of_range);
            if (_data[value]) return;
            _data[value] = true;
            ++Count;
        }

        public bool Contains(int value)
        {
            return CheckBoundary(value) && _data[value];
        }

        public bool Remove(int value)
        {
            if (!Contains(value)) return false;
            _data[value] = false;
            --Count;
            return true;
        }

        private bool CheckBoundary(int index)
        {
            return index >= 0 && index < _data.Length;
        }

        public IEnumerator<int> GetEnumerator()
        {
            if (Count == 0) yield break;
            for (int i = 0; i < _data.Length; ++i)
            {
                if (_data[i])
                    yield return i;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public class OverlapLsSolver : NonNegLsSolver
    {
        private readonly Dictionary<Matrix<double>, QR<double>> _decompCache; 

        public OverlapLsSolver(int numIsos, int maxRow, int maxTransitions, bool useFirstGuess = false):
            base(numIsos,maxRow,maxTransitions, useFirstGuess)
        {
            double[] smoothCoefs = { 1.0, 1.0, 1.0, 1.0, 1.0 };
            _conditioner = new WeightedConditioner(smoothCoefs);
            _decompCache = new Dictionary<Matrix<double>, QR<double>>();
        }

        protected override void SetTolerance(DeconvBlock db)
        {
            base.SetTolerance(db);
            _tol = 1e-8;
        }

        // ReSharper disable UnusedMember.Local
        protected override Vector<double> DecompSolve(Matrix<double> a, Vector<double> b)
        // ReSharper restore UnusedMember.Local
        {
            return GetSolver(a).Solve(b);
        }

        protected override Matrix<double> DecompSolve(Matrix<double> a, Matrix<double> b)
        {
            return GetSolver(a).Solve(b);
        }

        protected QR<double> GetSolver(Matrix<double> a)
        {
            QR<double> solver;
            if (!_decompCache.TryGetValue(a, out solver))
            {
                solver = a.QR();
                _decompCache.Add(a, solver);
            }
            return solver;
        }
    }

    /// <summary>
    /// implementation of the non negative least squares algorithm implemented in matlab
    /// </summary>
    public class NonNegLsSolver : ILsSolver
    {
        protected IBlockConditioner _conditioner;

        // data structures to reuse
        private Matrix<double> _firstGuess;
        private readonly Matrix<double> _matrixAx;
        private readonly Matrix<double> _matrixWs;
        private readonly Matrix<double> _x;
        private readonly Matrix<double> _z;
        private Vector<double> _solutionCol;
        private readonly Vector<double> _initializeCol;
        private readonly Vector<double> _binnedDataCol;
        private readonly Vector<double> _matrixAxCol;
        private readonly Vector<double> _matrixWsCol;
        private readonly SizedSet _zSet;
        private readonly SizedSet _pSet;
        private readonly Matrix<double> _colMatrixB;
        private readonly Matrix<double> _w;
        private readonly Matrix<double> _matrixAxC;
        private readonly Matrix<double> _matrixDiff;
        private readonly Matrix<double> _matrixDiffBig;
        private readonly bool _useFirstGuess;

        protected double? _tol;
        protected int? _maxIter;

        public NonNegLsSolver(int numIsos, int maxRows, int maxTransitions, bool useFirstGuess = false)
        {
            _conditioner = new WeightedConditioner();
            _firstGuess = new DenseMatrix(numIsos, maxTransitions);
            _matrixAx = new DenseMatrix(maxRows, maxTransitions);
            _matrixWs = new DenseMatrix(numIsos, maxTransitions);
            _x = new DenseMatrix(numIsos, 1);
            _z = new DenseMatrix(numIsos, 1);
            _solutionCol = new DenseVector(numIsos);
            _initializeCol = new DenseVector(numIsos);
            _binnedDataCol = new DenseVector(maxRows);
            _matrixAxCol = new DenseVector(maxRows);
            _matrixWsCol = new DenseVector(numIsos);
            _tol = null;
            _maxIter = null;
            _zSet = new SizedSet(numIsos);
            _pSet = new SizedSet(numIsos);
            _colMatrixB = new DenseMatrix(maxRows, 1);
            _w = new DenseMatrix(numIsos, 1);
            _matrixAxC = new DenseMatrix(maxRows, 1);
            _matrixDiff = new DenseMatrix(maxRows, 1);
            _matrixDiffBig = new DenseMatrix(maxRows, maxTransitions);
            _useFirstGuess = useFirstGuess;

            SetMathNetParameters();
        }

        #region Test
        public void SetConditioner(IBlockConditioner conditioner)
        {
            _conditioner = conditioner;
        }

        public void SetTolerance(double tol)
        {
            _tol = tol;
        }

        public void SetMaxIter(int maxIter)
        {
            _maxIter = maxIter;
        }

        public bool SolveColumnTest(Matrix<double> matrixA, Vector<double> colB,
            Vector<double> initialize, Matrix<double> matrixAt,
            Vector<double> ax, Vector<double> w, ref Vector<double> solution)
        {
            return SolveColumn(matrixA, colB, initialize, matrixAt, ax, w, ref solution);
        }
        #endregion Test

        private static void SetMathNetParameters()
        {
            MathNet.Numerics.Control.LinearAlgebraProvider = new LinProvider();
        }

        protected virtual void ClearMatrices()
        {
            _firstGuess.Clear();
            _matrixAx.Clear();
            _matrixWs.Clear();
            _x.Clear();
            _z.Clear();
            _solutionCol.Clear();
            _initializeCol.Clear();
            _binnedDataCol.Clear();
            _matrixAxCol.Clear();
            _matrixWsCol.Clear();
        }

        protected virtual void SetTolerance(DeconvBlock db)
        {
            // Number of blocks of scans that you average over
            int maxCount = db.Masks.NumRows;
            _tol = db.Masks.Matrix.L1Norm() * 10.0 * double.Epsilon * maxCount;
            _maxIter = db.Masks.NumCols * 3;
        }

        public virtual void Solve (DeconvBlock db)
        {
            ClearMatrices();
            _conditioner.Condition(db);

            if (!_tol.HasValue)
            {
                SetTolerance(db);
            }
            var matrixAt = db.Masks.Matrix.Transpose();
            FindFirstGuess(db.Masks.Matrix, db.BinnedData.Matrix, ref _firstGuess);
            db.Masks.Matrix.Multiply(_firstGuess, _matrixAx);
            _matrixDiffBig.Clear();
            db.BinnedData.Matrix.Subtract(_matrixAx, _matrixDiffBig);
            matrixAt.Multiply(_matrixDiffBig, _matrixWs);

            var matrixResult = db.Solution;
            // if useFirstGuess constructor parameter is true,
            // don't bother iterating, just use the first guess
            if (_useFirstGuess)
            {
                for (int colNum = 0; colNum < db.BinnedData.NumCols; ++colNum)
                {
                    CopyColumnToVector(_firstGuess, colNum, _initializeCol);
                    matrixResult.Matrix.SetColumn(colNum, _initializeCol);
                }
                return;
            }
            for (int colNum = 0; colNum < db.BinnedData.NumCols; ++colNum)
            {
                _initializeCol.Clear();
                CopyColumnToVector(_firstGuess, colNum, _initializeCol);
                CopyColumnToVector(db.BinnedData.Matrix, colNum, _binnedDataCol);
                _matrixAxCol.Clear();
                _matrixWsCol.Clear();
                CopyColumnToVector(_matrixAx, colNum, _matrixAxCol);
                CopyColumnToVector(_matrixWs, colNum, _matrixWsCol);
                if (SolveColumn(db.Masks.Matrix, _binnedDataCol,
                    _initializeCol, matrixAt, _matrixAxCol, _matrixWsCol,
                    ref _solutionCol))
                {
                    matrixResult.Matrix.SetColumn(colNum, _solutionCol);
                }
                else
                {
                    // can't figure out the results, so set the value to the 
                    // least squares initial guess
                    matrixResult.Matrix.SetColumn(colNum, _initializeCol);
                }
            }
        }

        private bool SolveColumn(Matrix<double> matrixA, Vector<double> colB,
            Vector<double> initialize, Matrix<double> matrixAt, 
            Vector<double> ax, Vector<double> wFullVector, ref Vector<double> solution)
        {
            // initialize sets to keep track of which columns are active (zero)
            // and which are not active (positive)
            int numColumns = matrixA.ColumnCount;
            var zSet = _zSet;
            zSet.Clear();
            var pSet = _pSet;
            pSet.Clear();
            // currently, all columns are active
            for (int i = 0; i < numColumns; ++i)
            {
                zSet.Add(i);
            }
            // TODO: should not need to make a new hash set or zeros here each time either
            var z = _z;
            z.Clear();
            // TODO: end
            for (int i = 0; i<initialize.Count; ++i)
            {
                if (initialize[i] < 0.0)
                    initialize[i] = 0.0;
            }

            var x = _x;
            for (int i = 0; i < initialize.Count; ++i)
                x[i,0] = initialize[i];

            var colMatrixB = _colMatrixB;
            colMatrixB.Clear();
            for (int i = 0; i < colB.Count; ++i)
                colMatrixB[i,0] = colB[i];
            var w = _w;
            w.Clear();
            // var w = wFullVector.ToColumnMatrix();
            for (int i = 0; i < wFullVector.Count; ++i)
                w[i, 0] = wFullVector[i];
            int iter = 0;
            CopyColumnToVector(x, 0, solution);
            // The initialized variable keeps track of whether we're on the first iteration,
            // which needs to be treated specially
            bool initialized = false;
            // Move the nonzero variables to the non-active set
            var initialMove = zSet.Where(item => initialize[item] > _tol);
            foreach (var index in initialMove)
            {
                zSet.Remove(index);
                pSet.Add(index);
            }
            while (((zSet.Count > 0) && zSet.Any(item => w[item, 0] > _tol)) ||
                    !initialized)
            {
                int maxW = 0;
                double maxVal = double.MinValue;
                foreach (var activeIndex in zSet)
                {
                    if (w[activeIndex, 0] > maxVal)
                    {
                        maxVal = w[activeIndex, 0];
                        maxW = activeIndex;
                    }
                }
                // Handles the case where initialization is zero and it happens to be an optimum
                if (maxVal <= _tol && pSet.Count == 0 && !initialized)
                    break;
                initialized = true;
                zSet.Remove(maxW);
                pSet.Add(maxW);
                // compute the intermediate solution with only the non-active columns
                Matrix<double> intermediateA = GetMatrixColumns(matrixA, pSet);
                Matrix<double> intermediateZ;
                try
                {
                    intermediateZ = DecompSolve(intermediateA, colMatrixB);
                }
                catch
                {
                    // usually happens if matrix is rank deficient
                    return false;
                }
                for (int i = 0; i < z.RowCount; ++i)
                    z[i, 0] = 0.0;
                int counter = 0;

                foreach (var index in pSet)
                {
                    if (double.IsNaN(intermediateZ[counter, 0]))
                        return false;
                    z[index, 0] = intermediateZ[counter++, 0];
                }

                while (pSet.Any(item => z[item, 0] <= _tol))
                {
                    ++iter;
                    if (iter > _maxIter)
                    {
                        CopyColumnToVector(z, 0, solution);
                        return true;
                    }
                    // create a set of indices from the non active set where z[index]<= tolerance
                    var qSet = pSet.Where(item => z[item, 0] <= _tol);
                    double alpha = qSet.Min(item => (x[item, 0] == z[item, 0]) ? double.MaxValue : x[item, 0] / (x[item, 0] - z[item, 0]));
                    
                    x = (x + alpha * (z - x));
                    Matrix<double> x1 = x;
                    var toMove = pSet.Where(item => Math.Abs(x1[item, 0]) < _tol);
                    foreach (var index in toMove)
                    {
                        pSet.Remove(index);
                        zSet.Add(index);
                    }
                    if (pSet.Count > 0)
                    {
                        intermediateA = GetMatrixColumns(matrixA, pSet);
                        try
                        {
                            intermediateZ = DecompSolve(intermediateA, colMatrixB);
                        }
                        catch
                        {
                            // usually happens if matrix is rank deficient
                            return false;
                        }
                    }
                    for (int i = 0; i < z.RowCount; ++i)
                        z[i, 0] = 0.0;

                    counter = 0;
                    foreach (var index in pSet)
                        z[index, 0] = intermediateZ[counter++, 0];
                }
                z.CopyTo(x);
                _matrixAxC.Clear();
                matrixA.Multiply(x, _matrixAxC);
                _matrixDiff.Clear();
                colMatrixB.Subtract(_matrixAxC, _matrixDiff);
                matrixAt.Multiply(_matrixDiff, w);
            }
            CopyColumnToVector(x, 0, solution);
            return true;
        }

        /// <summary>
        /// Copies a column from a matrix into a vector.  The supplied vector must be at least as long as
        /// the number of rows in the matrix.
        /// </summary>
        private static void CopyColumnToVector(Matrix<double> matrix, int columnIndex, Vector<double> targetVector)
        {
            for (int rowIndex = 0; rowIndex < matrix.RowCount; rowIndex++)
            {
                targetVector[rowIndex] = matrix[rowIndex, columnIndex];
            }
        }
       
// ReSharper disable UnusedMember.Local
        protected virtual Vector<double> DecompSolve(Matrix<double> a, Vector<double> b)
// ReSharper restore UnusedMember.Local
        {
            var solver = a.QR();
            return solver.Solve(b);
        }

        protected virtual Matrix<double> DecompSolve(Matrix<double> a, Matrix<double> b)
        {
            var solver = a.QR();
            return solver.Solve(b);
        }

        private ISolver<double> GetSolver(Matrix<double> a)
        {
            if (a.RowCount == a.ColumnCount)
            {
                return a.LU();
            }
            else if (a.RowCount > a.ColumnCount)
            {
                return a.QR();
            }
            else
            {
                // This shouldn't happen because the number of scans in a cycle should never be less than the number of windows
                throw new NotSupportedException("NonNegLsSolver DecompSolve: LQ decomposition not supported"); // Not L10N
            }
        }

        private void DecompSolve(Matrix<double> a, Matrix<double> b, ref Matrix<double> solution)
        {
            var solver = GetSolver(a);
            solver.Solve(b, solution);
        }


        private Matrix<double> GetMatrixColumns(Matrix<double> input, SizedSet columns)
        {
            double[,] matrixData = new double[input.RowCount, columns.Count]; 
            for (int row = 0; row <input.RowCount; ++row)
            {
                int destColumn = 0;
                foreach (var column in columns)
                    matrixData[row, destColumn++] = input[row, column];
            }
            return DenseMatrix.OfArray(matrixData);
        }

        private void FindFirstGuess(Matrix<double> matrixA, Matrix<double> matrixB, 
            ref Matrix<double> solution)
        {
            DecompSolve(matrixA, matrixB, ref solution);
            // this matrix now contains the deconvolved spectra, for each isolation window

            for (int row = 0; row < solution.RowCount; ++row)
            {
                for (int col = 0; col < solution.ColumnCount; ++col)
                {
                    if (solution[row, col] < 0.0 || double.IsNaN(solution[row,col]))
                        solution[row, col] = 0.0;
                }
            }
        }
    }
}
