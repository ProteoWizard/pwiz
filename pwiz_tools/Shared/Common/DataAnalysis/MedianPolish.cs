/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Statistics;

namespace pwiz.Common.DataAnalysis
{
    /// <summary>
    /// Implementation of the Tukey Median Polish algorithm.
    /// </summary>
    public class MedianPolish
    {
        public static MedianPolish GetMedianPolish(double?[,] matrix)
        {
            return GetMedianPolish(matrix, 0.01, 10);
        }

        public static MedianPolish GetMedianPolish(double?[,] matrix, double epsilon, int maxIterations)
        {
            int rowCount = matrix.GetLength(0);
            int columnCount = matrix.GetLength(1);
            double overallConstant = 0;
            Vector<double> rowEffects = new DenseVector(rowCount);
            Vector<double> columnEffects = new DenseVector(columnCount);
            double?[,] residuals = (double?[,]) matrix.Clone();
            double oldSum = 0;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                for (int iRow = 0; iRow < rowCount; iRow++)
                {
                    double median = GetRow(residuals, iRow).Median();
                    SetRow(residuals, iRow, GetRow(residuals, iRow).Select(value=>value-median));
                    rowEffects[iRow] += median;
                }
                double cDelta = columnEffects.Median();
                columnEffects = columnEffects.Subtract(cDelta);
                overallConstant += cDelta;
                for (int iCol = 0; iCol < columnCount; iCol++)
                {
                    double median = GetColumn(residuals, iCol).Median();
                    SetColumn(residuals, iCol, GetColumn(residuals, iCol).Select(value => value - median));
                    columnEffects[iCol] += median;
                }
                double rDelta = rowEffects.Median();
                rowEffects = rowEffects.Subtract(rDelta);
                overallConstant += rDelta;
                double newSum = 0;
                foreach (var value in residuals)
                {
                    newSum += Math.Abs(value.GetValueOrDefault());
                }
                bool converged = newSum == 0 || Math.Abs(newSum - oldSum) < epsilon*newSum;
                if (converged)
                {
                    break;
                }
                oldSum = newSum;
            }
            return new MedianPolish
            {
                ColumnEffects = columnEffects.ToArray(),
                OverallConstant = overallConstant,
                Residuals = residuals,
                RowEffects = rowEffects.ToArray()
            };
        }

        public double OverallConstant { get; set; }
        public double[] RowEffects { get; set; }
        public double[] ColumnEffects { get; set; }
        public double?[,] Residuals { get; set; }

        public static IEnumerable<double?> GetRow(double?[,] matrix, int rowIndex)
        {
            return Enumerable.Range(0, matrix.GetLength(1)).Select(colIndex => matrix[rowIndex, colIndex]);
        }

        public static IEnumerable<double?> GetColumn(double?[,] matrix, int columnIndex)
        {
            return Enumerable.Range(0, matrix.GetLength(0)).Select(rowIndex => matrix[rowIndex, columnIndex]);
        }

        public static void SetRow(double?[,] matrix, int rowIndex, IEnumerable<double?> values)
        {
            int iCol = 0;
            foreach (var value in values)
            {
                matrix[rowIndex, iCol] = value;
                iCol ++;
            }
        }

        public static void SetColumn(double?[,] matrix, int colIndex, IEnumerable<double?> values)
        {
            int iRow = 0;
            foreach (var value in values)
            {
                matrix[iRow, colIndex] = value;
                iRow++;
            }
        }
    }
}
