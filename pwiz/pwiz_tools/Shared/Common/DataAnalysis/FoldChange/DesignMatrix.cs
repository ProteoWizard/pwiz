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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using pwiz.Common.DataAnalysis.Matrices;

namespace pwiz.Common.DataAnalysis.FoldChange
{
    /// <summary>
    /// Contains the linear regression model for a FoldChangeDataSet.
    /// The columns in the design matrix correspond to coefficients that will be fitted
    /// in the linear model.
    /// The rows in the matrix correspond to observed values.
    /// A value in the matrix is 1 if the coefficient applies to that observation.
    /// The contrast vector contains factors by which the fitted coefficients get multiplied
    /// to produce the final fold change result.
    /// </summary>
    // ReSharper disable NonLocalizedString
    public class DesignMatrix
    {
        private readonly double[][] _matrixColumns;
        private readonly double[][] _contrastValues;
        private readonly string[] _columnNames;

        private DesignMatrix(FoldChangeDataSet dataSet, double[][] matrixColumns, double[][] contrastValues, string[] columnNames)
        {
            DataSet = dataSet;
            _matrixColumns = matrixColumns;
            _contrastValues = contrastValues;
            _columnNames = columnNames;
        }

        /// <summary>
        /// Gets the design matrix used for obtaining a single abundance value per run.
        /// The contrast matrix ends up having the same number of rows as the number of runs.
        /// </summary>
        public static DesignMatrix GetRunQuantificationDesignMatrix(FoldChangeDataSet dataSet)
        {
            var columnNames = new List<string>();
            var matrixColumns = new List<double[]>();
            var contrastValues = new List<double[]>();
            var featuresByRun = Enumerable.Range(0, dataSet.RunCount)
                .Select(dataSet.GetFeaturesForRun).ToArray();
            columnNames.Add("Intercept");
            matrixColumns.Add(Enumerable.Repeat(1.0, dataSet.RowCount).ToArray());
            contrastValues.Add(Enumerable.Repeat(1.0, dataSet.RunCount).ToArray());

            for (int iFeature = 1; iFeature < dataSet.FeatureCount; iFeature++)
            {
                columnNames.Add("Feature" + iFeature);
                matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow => iFeature == dataSet.Features[iRow] ? 1.0 : 0.0).ToArray());
                contrastValues.Add(Enumerable.Range(0, dataSet.RunCount).Select(run =>
                {
                    var features = featuresByRun[run];
                    if (features.Contains(iFeature))
                    {
                        return 1.0/features.Count;
                    }
                    return 0;
                }).ToArray());
            }

            for (int iRun = 1; iRun < dataSet.RunCount; iRun++)
            {
                columnNames.Add("Run" + iRun);
                matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow=>iRun == dataSet.Runs[iRow] ? 1.0 : 0.0).ToArray());
                contrastValues.Add(Enumerable.Range(0, dataSet.RunCount).Select(run=>run == iRun ? 1.0 : 0).ToArray());
            }
            return new DesignMatrix(dataSet, matrixColumns.ToArray(), contrastValues.ToArray(), columnNames.ToArray());
        }

        /// <summary>
        /// Constructs a design matrix for a FoldChangeDataSet.
        /// If <paramref name="includeFeatureInteraction"/> is true, then the model is:
        /// ABUNDANCE ~ FEATURE +  SUBJECT + GROUP + FEATURE:GROUP
        /// if false, then the model is:
        /// ABUNDANCE ~ FEATURE +  SUBJECT + GROUP
        /// </summary>
        /// <param name="dataSet"></param>
        /// <param name="includeFeatureInteraction"></param>
        /// <returns></returns>
        public static DesignMatrix GetDesignMatrix(FoldChangeDataSet dataSet, bool includeFeatureInteraction)
        {
            var columnNames = new List<string>();
            var matrixColumns = new List<double[]>();
            var contrastValues = new List<double>();
            columnNames.Add("Intercept");
            matrixColumns.Add(Enumerable.Repeat(1.0, dataSet.RowCount).ToArray());
            contrastValues.Add(0);

            for (int iFeature = 1; iFeature < dataSet.FeatureCount; iFeature++)
            {
                columnNames.Add("Feature" + iFeature);
                matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow=>iFeature == dataSet.Features[iRow] ? 1.0 : 0.0).ToArray());
                contrastValues.Add(0.0);
            }

            if (dataSet.RowCount != dataSet.SubjectCount)
            {
                int controlGroupCount = dataSet.SubjectControls.Count(f => f);
                var subjectIndexes = Enumerable.Range(1, dataSet.SubjectCount - 1);
                foreach (int iSubject in subjectIndexes)
                {
                    columnNames.Add("Subject" + iSubject);
                    matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow=>iSubject == dataSet.Subjects[iRow] ? 1.0 : 0.0).ToArray());
                    if (dataSet.IsSubjectInControlGroup(iSubject))
                    {
                        contrastValues.Add(-1.0/controlGroupCount);
                    }
                    else
                    {
                        contrastValues.Add(1.0 / (dataSet.SubjectCount - controlGroupCount));
                    }
                }
            }

            columnNames.Add("Control");
            matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow => dataSet.IsRowInControlGroup(iRow) ? 1.0 : 0.0).ToArray());
            contrastValues.Add(-1.0);

            if (includeFeatureInteraction)
            {
                for (int iFeature = 1; iFeature < dataSet.FeatureCount; iFeature++)
                {
                    columnNames.Add("Feature" + iFeature + "Interaction");
                    matrixColumns.Add(Enumerable.Range(0, dataSet.RowCount).Select(iRow =>
                        iFeature == dataSet.Features[iRow] && dataSet.IsRowInControlGroup(iRow) ? 1.0 : 0.0).ToArray());
                    contrastValues.Add(-1.0 / dataSet.FeatureCount);
                }
            }
            return new DesignMatrix(dataSet, matrixColumns.ToArray(), contrastValues.Select(value => new[]{value}).ToArray(), columnNames.ToArray());
        }

        public FoldChangeDataSet DataSet { get; private set; }
        public Matrix<double> Matrix { get { return DenseMatrix.OfColumnArrays(_matrixColumns); } }

        /// <summary>
        /// Returns the vector for the given row of the contrast matrix.
        /// The dot product of the coefficents of the linear regression is equal to the estimated value.
        /// </summary>
        public IList<double> GetContrastValues(int iRow) { return _contrastValues.Select(value=>value[iRow]).ToArray(); }
        public IList<LinearFitResult> PerformLinearFit(QrFactorizationCache cache)
        {
            var linearModel = LinearModel.CreateModel(cache, DenseMatrix.OfColumnArrays(_matrixColumns), DenseMatrix.OfColumnArrays(_contrastValues), 1E-7);
            return linearModel.Fit(DataSet.Abundances.ToArray());
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(string.Join(",", _columnNames));
            for (int iRow = 0; iRow < _matrixColumns[0].Length; iRow++)
            {
                result.AppendLine(string.Join(",", 
                    _matrixColumns.Select(col => col[iRow].ToString(CultureInfo.InvariantCulture)).ToArray()));
            }
            result.AppendLine("Contrast values:");
            for (int iRow = 0; iRow < _contrastValues[0].Length; iRow++)
            {
                result.AppendLine(string.Join(",", GetContrastValues(iRow)));
            }
            return result.ToString();
        }
    }
}
