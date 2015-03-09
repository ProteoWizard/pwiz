using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Storage;

namespace pwiz.Common.DataAnalysis.Matrices
{
    public sealed class ImmutableMatrix : Matrix
    {
        public static ImmutableMatrix OfColumns(IEnumerable<IEnumerable<double>> columns)
        {
            return new ImmutableMatrix(new ReadOnlyMatrixStorage<double>(DenseMatrix.OfColumns(columns).Storage));
        }

        public static ImmutableMatrix OfArray(double[,] array)
        {
            return new ImmutableMatrix(new ReadOnlyMatrixStorage<double>(DenseMatrix.OfArray(array).Storage));
        }

        public static ImmutableMatrix OfMatrix(Matrix<double> matrix)
        {
            ImmutableMatrix immutableMatrix = matrix as ImmutableMatrix;
            if (null != immutableMatrix)
            {
                return immutableMatrix;
            }
            return OfColumns(matrix.EnumerateColumns());
        }
        
        private ImmutableMatrix(MatrixStorage<double> storage) : base(storage)
        {
        }
    }
}
