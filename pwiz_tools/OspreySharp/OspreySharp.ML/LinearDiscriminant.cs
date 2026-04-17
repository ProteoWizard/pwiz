/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

// Linear Discriminant Analysis for binary classification
//
// Fisher's LDA: maximize between-class / within-class scatter.
//
// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Linear Discriminant Analysis classifier.
    /// Port of LinearDiscriminantAnalysis from linear_discriminant.rs.
    /// </summary>
    public class LinearDiscriminant
    {
        private readonly double[] _eigenvector;

        private LinearDiscriminant(double[] eigenvector)
        {
            _eigenvector = eigenvector;
        }

        /// <summary>
        /// Create LDA from pre-computed weights (eigenvector).
        /// </summary>
        public static LinearDiscriminant FromWeights(double[] weights)
        {
            if (weights == null || weights.Length == 0)
                throw new ArgumentException("Weights vector cannot be empty");
            return new LinearDiscriminant((double[])weights.Clone());
        }

        /// <summary>
        /// Fit LDA model to data.
        /// </summary>
        /// <param name="features">Feature matrix (rows = samples, cols = features)</param>
        /// <param name="decoy">Boolean labels (true = decoy, false = target)</param>
        /// <returns>Trained LDA model, or null if fitting failed</returns>
        public static LinearDiscriminant Fit(Matrix features, bool[] decoy)
        {
            if (features.Rows != decoy.Length)
                throw new ArgumentException("Feature rows must match label count");

            // Calculate overall mean
            var xBar = features.Mean();
            var scatterWithin = Matrix.Zeros(features.Cols, features.Cols);
            var scatterBetween = Matrix.Zeros(features.Cols, features.Cols);

            var classMeansList = new List<double>();

            bool[] classes = { true, false };
            foreach (bool cls in classes)
            {
                // Collect rows for this class
                var classRows = new List<int>();
                for (int i = 0; i < decoy.Length; i++)
                {
                    if (decoy[i] == cls)
                        classRows.Add(i);
                }
                int count = classRows.Count;
                if (count == 0)
                    continue;

                // Build class data matrix
                var classData = new double[count * features.Cols];
                for (int r = 0; r < count; r++)
                {
                    int srcRow = classRows[r];
                    for (int c = 0; c < features.Cols; c++)
                        classData[r * features.Cols + c] = features[srcRow, c];
                }
                var classMatrix = new Matrix(classData, count, features.Cols);
                var classMean = classMatrix.Mean();

                // Center class data
                for (int r = 0; r < count; r++)
                {
                    for (int c = 0; c < features.Cols; c++)
                        classMatrix[r, c] -= classMean[c];
                }

                // Within-class scatter: (X_centered^T * X_centered) / n
                var cov = Matrix.Dot(classMatrix.Transpose(), classMatrix).Divide(count);
                scatterWithin.AddInPlace(cov);

                // Between-class scatter: (mean_class - mean_overall) * (mean_class - mean_overall)^T
                var diff = new double[features.Cols];
                for (int c = 0; c < features.Cols; c++)
                    diff[c] = classMean[c] - xBar[c];
                var diffCol = Matrix.ColVector(diff);
                var diffRow = diffCol.Transpose();
                scatterBetween.AddInPlace(Matrix.Dot(diffCol, diffRow));

                for (int c = 0; c < features.Cols; c++)
                    classMeansList.Add(classMean[c]);
            }

            // Solve: inv(Sw) * Sb via Gauss elimination, then power method
            Matrix solved = GaussSolver.Solve(scatterWithin, scatterBetween);
            if (solved == null)
                return null;

            var evec = solved.PowerMethod(xBar);

            // Ensure target class scores higher than decoy class
            // Class means: first class is decoy (true), second is target (false)
            var classeMeansArray = classMeansList.ToArray();
            var classMeansMatrix = new Matrix(classeMeansArray, 2, features.Cols);
            var coef = Matrix.DotVector(classMeansMatrix, evec);
            if (coef[1] < coef[0])
            {
                for (int i = 0; i < evec.Length; i++)
                    evec[i] *= -1.0;
            }

            return new LinearDiscriminant(evec);
        }

        /// <summary>
        /// Score samples using the learned discriminant.
        /// Higher values indicate more target-like.
        /// </summary>
        public double[] Predict(Matrix features)
        {
            return Matrix.DotVector(features, _eigenvector);
        }

        /// <summary>
        /// Get the learned eigenvector (feature weights).
        /// </summary>
        public double[] Eigenvector { get { return _eigenvector; } }
    }
}
