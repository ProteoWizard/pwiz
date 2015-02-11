using System;
using MathNet.Numerics.LinearAlgebra.Storage;

namespace pwiz.Common.DataAnalysis.Matrices
{
    public class ReadOnlyMatrixStorage<T> : MatrixStorage<T> where T : struct, IEquatable<T>, IFormattable
    {
        private readonly MatrixStorage<T> _matrixStorage;
        public ReadOnlyMatrixStorage(MatrixStorage<T> matrixStorage) 
            : base(matrixStorage.RowCount, matrixStorage.ColumnCount)
        {
            _matrixStorage = matrixStorage;
        }

        public override bool IsMutableAt(int row, int column)
        {
            return false;
        }

        public override T At(int row, int column)
        {
            return _matrixStorage.At(row, column);
        }

        public override void At(int row, int column, T value)
        {
            throw new InvalidOperationException();
        }

        public override bool IsDense
        {
            get { return _matrixStorage.IsDense; }
        }

        public override bool IsFullyMutable
        {
            get { return false; }
        }
    }
}
