using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace pwiz.Common.DataAnalysis.Matrices
{
    public class QrFactorizationCache
    {
        private readonly IDictionary<Key, CacheEntry> _dictionary = new Dictionary<Key, CacheEntry>();

        public CacheEntry GetQrFactorization(Matrix<double> matrix, double tolerance)
        {
            var key = new Key(matrix, tolerance);
            CacheEntry cacheEntry;
            lock (_dictionary)
            {
                if (_dictionary.TryGetValue(key, out cacheEntry))
                {
                    return cacheEntry;
                }
            }
            var qrFactorization = QrFactorization.GetQrFactorization(matrix, tolerance);
            lock (_dictionary)
            {
                _dictionary[key] = cacheEntry = new CacheEntry(key, qrFactorization);
            }
            return cacheEntry;
        }

        private static Matrix<double> ComputeMatrixCrossproduct(Matrix<double> matrix, IList<int> columnIndexes)
        {
            var result = new DenseMatrix(columnIndexes.Count, columnIndexes.Count);
            for (int iRow = 0; iRow < columnIndexes.Count; iRow++)
            {
                for (int iCol = 0; iCol < columnIndexes.Count; iCol++)
                {
                    result[iRow, iCol] = matrix.Column(columnIndexes[iRow]).DotProduct(matrix.Column(columnIndexes[iCol]));
                }
            }
            return result;
        }



        public struct Key
        {
            public Key(Matrix<double> matrix, double tolerance) : this()
            {
                Matrix = ImmutableMatrix.OfMatrix(matrix);
                Tolerance = tolerance;
            }

            public ImmutableMatrix Matrix { get; private set; }
            public double Tolerance { get; private set; }
        }

        public class CacheEntry
        {
            public Key Key { get; private set; }

            public CacheEntry(Key key, QrFactorization qrFactorization)
            {
                Key = key;
                QrFactorization = qrFactorization;
                MatrixCrossproduct = ImmutableMatrix.OfMatrix(ComputeMatrixCrossproduct(key.Matrix, qrFactorization.IndependentColumnIndexes));
                MatrixCrossproductInverse = ImmutableMatrix.OfMatrix(MatrixCrossproduct.Inverse());
            }
            public QrFactorization QrFactorization { get; private set; }
            public ImmutableMatrix MatrixCrossproduct { get; private set; }
            public ImmutableMatrix MatrixCrossproductInverse { get; private set; }
        }
    }
}
