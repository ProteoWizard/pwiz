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
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using pwiz.Common.DataAnalysis.Matrices;

namespace pwiz.Common.DataAnalysis
{
    public class LinearModel
    {
        public static LinearModel CreateModel(QrFactorizationCache cache, Matrix<double> designMatrix, Matrix<double> contrastValues, double tolerance)
        {
            if (contrastValues.ColumnCount != designMatrix.ColumnCount)
            {
                throw new ArgumentException("Wrong number of columns"); // Not L10N
            }
            var cacheEntry = cache.GetQrFactorization(designMatrix, tolerance);
            return new LinearModel(designMatrix, contrastValues, cacheEntry);
        }
        
        public LinearModel(Matrix<double> designMatrix, Matrix<double> contrastValues, QrFactorizationCache.CacheEntry cacheEntry)
        {
            DesignMatrix = ImmutableMatrix.OfMatrix(designMatrix);
            ContrastValues = ImmutableMatrix.OfMatrix(contrastValues);
            QrFactorization = cacheEntry.QrFactorization;
            MatrixCrossproductInverse = cacheEntry.MatrixCrossproductInverse;
        }

        public ImmutableMatrix DesignMatrix { get; private set; }
        public ImmutableMatrix ContrastValues { get; private set; }
        public QrFactorization QrFactorization { get; private set; }
        public ImmutableMatrix MatrixCrossproductInverse { get; private set; } 

        public IList<LinearFitResult> Fit(double[] observations)
        {
            if (observations.Length != DesignMatrix.RowCount)
            {
                throw new ArgumentException("Wrong number of rows"); // Not L10N
            }
            var coefficients = QrFactorization.Solve(observations);
            var fittedValues = new double[observations.Length];
            var residuals = new double[observations.Length];
            for (int iRow = 0; iRow < observations.Length; iRow++)
            {
                var designRow = Enumerable.Range(0, DesignMatrix.ColumnCount).Select(index => DesignMatrix[iRow, index]).ToArray();
                fittedValues[iRow] = DotProduct(designRow, coefficients);
                residuals[iRow] = observations[iRow] - fittedValues[iRow];
            }
            double rss = DotProduct(residuals, residuals);

            int degreesOfFreedom = observations.Length - QrFactorization.NumberIndependentColumns;
            double resVar = rss / degreesOfFreedom;
            double sigma = Math.Sqrt(resVar);
            var covarianceUnscaled = MatrixCrossproductInverse;
            var scaledCovariance = covarianceUnscaled.Multiply(sigma * sigma);
            var indepColIndexes = QrFactorization.IndependentColumnIndexes.ToArray();
            var result = new List<LinearFitResult>();
            foreach (var contrastRow in ContrastValues.EnumerateRows())
            {
                double standardError = 0;
                for (int iRow = 0; iRow < indepColIndexes.Length; iRow++)
                {
                    for (int iCol = 0; iCol < indepColIndexes.Length; iCol++)
                    {
                        standardError += contrastRow[indepColIndexes[iRow]] * scaledCovariance[iRow, iCol] * contrastRow[indepColIndexes[iCol]];
                    }
                }
                standardError = Math.Sqrt(standardError);
                double foldChange = DotProduct(coefficients, contrastRow);
                double tValue = foldChange / standardError;
                double pValue;
                if (0 != degreesOfFreedom)
                {
                    var studentT = new StudentT(0, 1.0, degreesOfFreedom);
                    pValue = (1 - studentT.CumulativeDistribution(Math.Abs(tValue))) * 2;
                }
                else
                {
                    pValue = 1;
                }
                result.Add(new LinearFitResult(foldChange)
                    .SetDegreesOfFreedom(degreesOfFreedom)
                    .SetTValue(tValue)
                    .SetStandardError(standardError)
                    .SetPValue(pValue));
            }
            return result;
        }

        public static double DotProduct(IEnumerable<double> vector1, IEnumerable<double> vector2)
        {
            double total = 0;
            var en1 = vector1.GetEnumerator();
            var en2 = vector2.GetEnumerator();
            while (en1.MoveNext())
            {
                en2.MoveNext();
                total += en1.Current * en2.Current;
            }
            if (en2.MoveNext())
            {
                throw new ArgumentException("vector2 was too long"); // Not L10N
            }
            return total;
        }
    }
}
