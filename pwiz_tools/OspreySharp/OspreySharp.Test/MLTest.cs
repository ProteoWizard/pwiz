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

// Tests for OspreySharp.ML module
// Ported from Rust test suite in osprey-ml

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.ML;

namespace pwiz.OspreySharp.Test
{
    [TestClass]
    public class MLTest
    {
        #region Matrix Tests

        [TestMethod]
        public void TestMatrixDot()
        {
            // 2x3 * 3x2 = 2x2
            var a = new Matrix(new double[] { 1, 2, 3, 4, 5, 6 }, 2, 3);
            var b = new Matrix(new double[] { 7, 8, 9, 10, 11, 12 }, 3, 2);
            var c = Matrix.Dot(a, b);

            Assert.AreEqual(2, c.Rows);
            Assert.AreEqual(2, c.Cols);
            // Row 0: 1*7 + 2*9 + 3*11 = 7+18+33 = 58
            //         1*8 + 2*10 + 3*12 = 8+20+36 = 64
            // Row 1: 4*7 + 5*9 + 6*11 = 28+45+66 = 139
            //         4*8 + 5*10 + 6*12 = 32+50+72 = 154
            Assert.AreEqual(58.0, c[0, 0], 1e-10);
            Assert.AreEqual(64.0, c[0, 1], 1e-10);
            Assert.AreEqual(139.0, c[1, 0], 1e-10);
            Assert.AreEqual(154.0, c[1, 1], 1e-10);
        }

        [TestMethod]
        public void TestMatrixDot4x3()
        {
            // Port of the Rust dot() test: 4x3 * 3x3 = 4x3
            var a = new Matrix(new double[]
            {
                1, 0, 1,
                2, 1, 1,
                0, 1, 1,
                1, 1, 2
            }, 4, 3);

            var b = new Matrix(new double[]
            {
                1, 2, 1,
                2, 3, 1,
                4, 2, 2
            }, 3, 3);

            var c = Matrix.Dot(a, b);
            Assert.AreEqual(4, c.Rows);
            Assert.AreEqual(3, c.Cols);

            var expected = new double[] { 5, 4, 3, 8, 9, 5, 6, 5, 3, 11, 9, 6 };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], c[i / c.Cols, i % c.Cols], 1e-10, "Mismatch at index " + i);
        }

        [TestMethod]
        public void TestMatrixDotColVector()
        {
            // Port of Rust dot() test with column vector
            var d = new Matrix(new double[] { 1, 2, 3, 4, 5, 6 }, 2, 3);
            var e = Matrix.ColVector(new double[] { 7, 9, 11 });
            var result = Matrix.Dot(d, e);
            Assert.AreEqual(2, result.Rows);
            Assert.AreEqual(1, result.Cols);
            Assert.AreEqual(58.0, result[0, 0], 1e-10);
            Assert.AreEqual(139.0, result[1, 0], 1e-10);
        }

        [TestMethod]
        public void TestMatrixTranspose()
        {
            // Port of Rust transpose() test
            var mat = new Matrix(new double[] { 1, 2, 3, 4, 5, 6 }, 3, 2);

            Assert.AreEqual(1.0, mat[0, 0]);
            Assert.AreEqual(2.0, mat[0, 1]);
            Assert.AreEqual(3.0, mat[1, 0]);
            Assert.AreEqual(4.0, mat[1, 1]);
            Assert.AreEqual(5.0, mat[2, 0]);
            Assert.AreEqual(6.0, mat[2, 1]);

            var t = mat.Transpose();
            Assert.AreEqual(2, t.Rows);
            Assert.AreEqual(3, t.Cols);

            Assert.AreEqual(1.0, t[0, 0]);
            Assert.AreEqual(3.0, t[0, 1]);
            Assert.AreEqual(5.0, t[0, 2]);
            Assert.AreEqual(2.0, t[1, 0]);
            Assert.AreEqual(4.0, t[1, 1]);
            Assert.AreEqual(6.0, t[1, 2]);
        }

        [TestMethod]
        public void TestMatrixSlice()
        {
            // Port of Rust slice() test
            var b = new Matrix(new double[]
            {
                1, 2, 1,
                2, 3, 1,
                4, 2, 2
            }, 3, 3);

            var row0 = b.Row(0);
            var row1 = b.Row(1);
            var row2 = b.Row(2);

            CollectionAssert.AreEqual(new double[] { 1, 2, 1 }, row0);
            CollectionAssert.AreEqual(new double[] { 2, 3, 1 }, row1);
            CollectionAssert.AreEqual(new double[] { 4, 2, 2 }, row2);

            // Test Slice method
            var top2 = b.Slice(0, 2);
            Assert.AreEqual(2, top2.Rows);
            Assert.AreEqual(3, top2.Cols);
            Assert.AreEqual(1.0, top2[0, 0]);
            Assert.AreEqual(3.0, top2[1, 1]);
        }

        [TestMethod]
        public void TestMatrixDotVector()
        {
            // Port of Rust dotv() test
            var a = new Matrix(new double[] { 1, 2, 3, 4 }, 2, 2);
            var v = Matrix.DotVector(a, new[] { 0.5, 0.5 });
            Assert.AreEqual(1.5, v[0], 1e-10);
            Assert.AreEqual(3.5, v[1], 1e-10);

            double n = MlMath.Norm(v);
            var c = new double[v.Length];
            for (int i = 0; i < v.Length; i++)
                c[i] = v[i] / n;
            Assert.AreEqual(0.3939193, c[0], 0.0001);
            Assert.AreEqual(0.91914503, c[1], 0.0001);
        }

        [TestMethod]
        public void TestMatrixFromRows()
        {
            var rows = new[]
            {
                new double[] { 1, 2, 3 },
                new double[] { 4, 5, 6 }
            };
            var m = Matrix.FromRows(rows);
            Assert.AreEqual(2, m.Rows);
            Assert.AreEqual(3, m.Cols);
            Assert.AreEqual(1.0, m[0, 0]);
            Assert.AreEqual(6.0, m[1, 2]);
        }

        [TestMethod]
        public void TestMatrixIdentity()
        {
            var m = Matrix.Identity(3);
            Assert.AreEqual(1.0, m[0, 0]);
            Assert.AreEqual(0.0, m[0, 1]);
            Assert.AreEqual(0.0, m[1, 0]);
            Assert.AreEqual(1.0, m[1, 1]);
            Assert.AreEqual(1.0, m[2, 2]);
        }

        [TestMethod]
        public void TestMatrixMean()
        {
            var m = new Matrix(new double[] { 1, 10, 2, 20, 3, 30 }, 3, 2);
            var mean = m.Mean();
            Assert.AreEqual(2.0, mean[0], 1e-10);
            Assert.AreEqual(20.0, mean[1], 1e-10);
        }

        #endregion

        #region SVM Tests

        [TestMethod]
        public void TestSvmLinearlySeparable()
        {
            // Port of Rust test_linearly_separable
            var features = new Matrix(new[] { 5.0, 5.0, 4.0, 6.0, 6.0, 4.0, 5.5, 5.5, 1.0, 1.0, 0.0, 2.0, 2.0, 0.0, 1.5, 1.5 }, 8, 2);

            var labels = new[] { false, false, false, false, true, true, true, true };
            var model = LinearSvmClassifier.Train(features, labels, 1.0, 42);
            var scores = model.DecisionFunction(features);

            // All targets should score higher than all decoys
            double targetMin = double.PositiveInfinity;
            for (int i = 0; i < 4; i++)
                targetMin = Math.Min(targetMin, scores[i]);
            double decoyMax = double.NegativeInfinity;
            for (int i = 4; i < 8; i++)
                decoyMax = Math.Max(decoyMax, scores[i]);

            Assert.IsTrue(targetMin > decoyMax,
                string.Format("target_min={0}, decoy_max={1}", targetMin, decoyMax));
        }

        [TestMethod]
        public void TestSvmDecisionFunction()
        {
            // Port of Rust test_decision_function
            var model = new LinearSvmClassifier(new[] { 1.0, 2.0 }, -3.0);
            var features = new Matrix(new[] { 1.0, 1.0, 2.0, 2.0 }, 2, 2);
            var scores = model.DecisionFunction(features);

            // score[0] = 1*1 + 2*1 + (-3) = 0
            // score[1] = 1*2 + 2*2 + (-3) = 3
            Assert.AreEqual(0.0, scores[0], 1e-10);
            Assert.AreEqual(3.0, scores[1], 1e-10);
        }

        [TestMethod]
        public void TestSvmDeterministicWithSeed()
        {
            // Port of Rust test_deterministic_with_seed
            var features = new Matrix(new[] { 5.0, 5.0, 1.0, 1.0, 4.0, 6.0, 2.0, 0.0 }, 4, 2);
            var labels = new[] { false, true, false, true };

            var model1 = LinearSvmClassifier.Train(features, labels, 1.0, 42);
            var model2 = LinearSvmClassifier.Train(features, labels, 1.0, 42);

            CollectionAssert.AreEqual(model1.Weights, model2.Weights);
            Assert.AreEqual(model1.Bias, model2.Bias);
        }

        [TestMethod]
        public void TestSvmWeightsDirection()
        {
            // Port of Rust test_weights_direction
            var features = new Matrix(new[] { 10.0, 0.5, 9.0, 0.3, 8.0, 0.7, 1.0, 0.4, 2.0, 0.6, 3.0, 0.2 }, 6, 2);

            var labels = new[] { false, false, false, true, true, true };
            var model = LinearSvmClassifier.Train(features, labels, 1.0, 42);

            // Weight for feature 0 should be positive (targets have higher values)
            Assert.IsTrue(model.Weights[0] > 0.0,
                string.Format("weights: [{0}, {1}]", model.Weights[0], model.Weights[1]));
            // Feature 0 should have larger absolute weight than feature 1 (noise)
            Assert.IsTrue(Math.Abs(model.Weights[0]) > Math.Abs(model.Weights[1]),
                string.Format("weights: [{0}, {1}]", model.Weights[0], model.Weights[1]));
        }

        [TestMethod]
        public void TestSvmEmptyInput()
        {
            var features = Matrix.Zeros(0, 3);
            var labels = new bool[0];
            var model = LinearSvmClassifier.Train(features, labels, 1.0, 42);
            Assert.AreEqual(3, model.Weights.Length);
            Assert.AreEqual(0.0, model.Bias);
        }

        [TestMethod]
        public void TestSvmOverlappingClasses()
        {
            // Port of Rust test_overlapping_classes
            var features = new Matrix(new[] { 5.0, 5.0, 4.0, 4.0, 3.0, 3.0, 1.0, 1.0, 2.0, 2.0, 2.5, 2.5 }, 6, 2);

            var labels = new[] { false, false, false, true, true, true };
            var model = LinearSvmClassifier.Train(features, labels, 1.0, 42);
            var scores = model.DecisionFunction(features);

            double avgTarget = (scores[0] + scores[1] + scores[2]) / 3.0;
            double avgDecoy = (scores[3] + scores[4] + scores[5]) / 3.0;
            Assert.IsTrue(avgTarget > avgDecoy,
                string.Format("avg_target={0}, avg_decoy={1}", avgTarget, avgDecoy));
        }

        [TestMethod]
        public void TestSvmScoreSingle()
        {
            var model = new LinearSvmClassifier(new[] { 1.0, 2.0 }, -3.0);
            double score = model.ScoreSingle(new[] { 1.0, 1.0 });
            Assert.AreEqual(0.0, score, 1e-10);
        }

        #endregion

        #region FeatureStandardizer Tests

        [TestMethod]
        public void TestFeatureStandardizer()
        {
            // Port of Rust test_feature_standardizer
            var features = new Matrix(new[] { 10.0, 100.0, 20.0, 200.0, 30.0, 300.0 }, 3, 2);

            Matrix transformed;
            var standardizer = FeatureStandardizer.FitTransform(features, out transformed);

            // Means should be 20.0 and 200.0
            Assert.AreEqual(20.0, standardizer.Means[0], 1e-10);
            Assert.AreEqual(200.0, standardizer.Means[1], 1e-10);

            // Transformed mean should be ~0
            double col0Mean = 0, col1Mean = 0;
            for (int r = 0; r < 3; r++)
            {
                col0Mean += transformed[r, 0];
                col1Mean += transformed[r, 1];
            }
            col0Mean /= 3.0;
            col1Mean /= 3.0;
            Assert.AreEqual(0.0, col0Mean, 1e-10, "col0_mean = " + col0Mean);
            Assert.AreEqual(0.0, col1Mean, 1e-10, "col1_mean = " + col1Mean);

            // Transformed std should be ~1
            double col0Std = 0;
            for (int r = 0; r < 3; r++)
            {
                double diff = transformed[r, 0] - col0Mean;
                col0Std += diff * diff;
            }
            col0Std = Math.Sqrt(col0Std / 3.0);
            Assert.AreEqual(1.0, col0Std, 1e-10, "col0_std = " + col0Std);
        }

        [TestMethod]
        public void TestStandardizerZeroVariance()
        {
            // Port of Rust test_standardizer_zero_variance
            var features = new Matrix(new[] { 5.0, 1.0, 5.0, 2.0, 5.0, 3.0 }, 3, 2);

            var standardizer = FeatureStandardizer.Fit(features);

            // Feature 0 has zero variance - std should be set to 1.0
            Assert.AreEqual(1.0, standardizer.Stds[0], 1e-10);

            // Should not produce NaN
            var transformed = standardizer.Transform(features);
            for (int row = 0; row < 3; row++)
                Assert.IsFalse(double.IsNaN(transformed[row, 0]), "NaN in zero-variance feature");
        }

        #endregion

        #region XorShift64 Tests

        [TestMethod]
        public void TestXorShiftDeterministic()
        {
            // Port of Rust test_xorshift_deterministic
            var rng1 = new XorShift64(42);
            var rng2 = new XorShift64(42);

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(rng1.Next(), rng2.Next());
        }

        [TestMethod]
        public void TestXorShiftZeroSeed()
        {
            // Zero seed should be treated as 1 to avoid degenerate sequence
            var rng = new XorShift64(0);
            ulong val = rng.Next();
            Assert.AreNotEqual(0UL, val);
        }

        [TestMethod]
        public void TestXorShiftSequence()
        {
            // Verify XorShift64 matches the Rust implementation:
            // x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            // Starting with state = 42:
            var rng = new XorShift64(42);

            // Verified against Rust and C# compiled output
            ulong first = rng.Next();
            Assert.AreEqual(45454805674UL, first,
                string.Format("First XorShift64 value with seed 42: expected 45454805674, got {0}", first));
        }

        #endregion

        #region Q-Value Tests

        [TestMethod]
        public void TestQValueComputation()
        {
            // Port of Rust test_qvalue_calculation
            // 5 targets, 2 decoys (all targets before decoys)
            var isDecoy = new[] { false, false, false, false, false, true, true };
            var qValues = new double[7];

            int passing = QValueCalculator.ComputeQValues(isDecoy, qValues);

            // Expected FDR progression: 0/1, 0/2, 0/3, 0/4, 0/5, 1/5, 2/5
            // Q-values (cumulative min from end): 0.0, 0.0, 0.0, 0.0, 0.0, 0.2, 0.4
            Assert.AreEqual(0.0, qValues[0], 1e-10);
            Assert.AreEqual(0.0, qValues[4], 1e-10);
            Assert.AreEqual(0.2, qValues[5], 1e-10);
            Assert.AreEqual(0.4, qValues[6], 1e-10);

            // All 5 targets pass 1% FDR (q = 0.0 <= 0.01)
            Assert.AreEqual(5, passing);
        }

        [TestMethod]
        public void TestQValueMixed()
        {
            // Port of Rust test_qvalue_mixed
            var isDecoy = new[] { false, false, true, false, false, true, false };
            var qValues = new double[7];

            QValueCalculator.ComputeQValues(isDecoy, qValues);

            // Verify q-values are monotonically non-decreasing
            // (since sorted by score descending, q-values should be non-decreasing)
            for (int i = 1; i < qValues.Length; i++)
            {
                Assert.IsTrue(qValues[i] >= qValues[i - 1] - 1e-10,
                    string.Format("Q-values not monotonic: q[{0}]={1} < q[{2}]={3}",
                        i, qValues[i], i - 1, qValues[i - 1]));
            }

            Assert.IsTrue(qValues[0] <= 1.0);
        }

        [TestMethod]
        public void TestQValueConvenienceOverload()
        {
            var isDecoy = new[] { false, false, false, true, true };
            int passing;
            var qValues = QValueCalculator.ComputeQValues(isDecoy, out passing);

            Assert.AreEqual(5, qValues.Length);
            Assert.AreEqual(3, passing); // 3 targets at q=0
        }

        #endregion

        #region Isotonic Regression Tests

        [TestMethod]
        public void TestIsotonicRegressionAlreadyDecreasing()
        {
            var values = new[] { 1.0, 0.8, 0.6, 0.4, 0.2 };
            PepEstimator.IsotonicRegressionDecreasing(values);
            Assert.AreEqual(1.0, values[0], 1e-10);
            Assert.AreEqual(0.8, values[1], 1e-10);
            Assert.AreEqual(0.6, values[2], 1e-10);
            Assert.AreEqual(0.4, values[3], 1e-10);
            Assert.AreEqual(0.2, values[4], 1e-10);
        }

        [TestMethod]
        public void TestIsotonicRegressionSingleViolation()
        {
            // Port of Rust test: values[2] > values[1] is a violation
            var values = new[] { 1.0, 0.3, 0.5, 0.2, 0.1 };
            PepEstimator.IsotonicRegressionDecreasing(values);

            // After PAVA: indices 1,2 should be averaged: (0.3+0.5)/2 = 0.4
            Assert.AreEqual(0.4, values[1], 1e-10);
            Assert.AreEqual(0.4, values[2], 1e-10);

            // Check monotonicity
            for (int i = 1; i < values.Length; i++)
            {
                Assert.IsTrue(values[i] <= values[i - 1] + 1e-10,
                    string.Format("values[{0}]={1} > values[{2}]={3}", i, values[i], i - 1, values[i - 1]));
            }
        }

        [TestMethod]
        public void TestIsotonicRegressionAllIncreasing()
        {
            // Port of Rust test
            var values = new[] { 0.1, 0.2, 0.3, 0.4, 0.5 };
            PepEstimator.IsotonicRegressionDecreasing(values);

            // All should become the average: (0.1+0.2+0.3+0.4+0.5)/5 = 0.3
            for (int i = 0; i < values.Length; i++)
                Assert.AreEqual(0.3, values[i], 1e-10, "v = " + values[i]);
        }

        [TestMethod]
        public void TestIsotonicRegressionEmptyAndSingle()
        {
            var empty = Array.Empty<double>();
            PepEstimator.IsotonicRegressionDecreasing(empty);

            var single = new[] { 0.5 };
            PepEstimator.IsotonicRegressionDecreasing(single);
            Assert.AreEqual(0.5, single[0], 1e-10);
        }

        #endregion

        #region Linear Discriminant Tests

        [TestMethod]
        public void TestLinearDiscriminantPowerMethod()
        {
            // Port of Rust linear_discriminant test: power method
            var a = new Matrix(new double[] { 1, 2, 3, 4 }, 2, 2);
            var eigenvector = a.PowerMethod(new[] { 0.54, 0.34 });
            Assert.IsTrue(MlMath.AllClose(eigenvector, new[] { 0.4159736, 0.90937671 }, 1E-5),
                string.Format("eigenvector: [{0}, {1}]", eigenvector[0], eigenvector[1]));
        }

        [TestMethod]
        public void TestLinearDiscriminant()
        {
            // Port of Rust linear_discriminant test
            var feats = new Matrix(new[] { 5, 4, 3, 2, 4, 5, 4, 3, 6, 3, 4, 5, 1, 0, 2, 9, 5, 4, 4, 3, 2, 1, 1, 9.5, 1, 0, 2, 8, 3, 2, -2, 10 }, 8, 4);

            var labels = new[] { false, false, false, true, false, true, true, true };
            var lda = LinearDiscriminant.Fit(feats, labels);
            Assert.IsNotNull(lda, "LDA fitting should succeed");

            var scores = lda.Predict(feats);
            double normVal = MlMath.Norm(scores);
            var normalizedScores = new double[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                normalizedScores[i] = scores[i] / normVal;

            var expected = new[]
            {
                0.49706043,
                0.48920177,
                0.48920177,
                -0.07209359,
                0.51204672,
                -0.02849527,
                -0.04924864,
                -0.06055943,
            };

            Assert.IsTrue(MlMath.AllClose(normalizedScores, expected, 1E-5),
                string.Format("First score: {0} vs expected {1}", normalizedScores[0], expected[0]));
        }

        [TestMethod]
        public void TestLinearDiscriminantSeparation()
        {
            // Verify LDA correctly assigns higher scores to targets
            var feats = new Matrix(new[] { 5, 4, 3, 2, 4, 5, 4, 3, 6, 3, 4, 5, 1, 0, 2, 9, 5, 4, 4, 3, 2, 1, 1, 9.5, 1, 0, 2, 8, 3, 2, -2, 10 }, 8, 4);

            var labels = new[] { false, false, false, true, false, true, true, true };
            var lda = LinearDiscriminant.Fit(feats, labels);
            var scores = lda.Predict(feats);

            // Targets (indices 0,1,2,4) should score higher than decoys (indices 3,5,6,7)
            double targetMin = double.PositiveInfinity;
            double decoyMax = double.NegativeInfinity;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!labels[i])
                    targetMin = Math.Min(targetMin, scores[i]);
                else
                    decoyMax = Math.Max(decoyMax, scores[i]);
            }
            Assert.IsTrue(targetMin > decoyMax,
                string.Format("target_min={0}, decoy_max={1}", targetMin, decoyMax));
        }

        [TestMethod]
        public void TestLinearDiscriminantFromWeights()
        {
            var lda = LinearDiscriminant.FromWeights(new[] { 1.0, 2.0, 3.0 });
            Assert.AreEqual(3, lda.Eigenvector.Length);

            var feats = new Matrix(new double[] { 1, 0, 0, 0, 1, 0 }, 2, 3);
            var scores = lda.Predict(feats);
            Assert.AreEqual(1.0, scores[0], 1e-10);
            Assert.AreEqual(2.0, scores[1], 1e-10);
        }

        #endregion

        #region MlMath Tests

        [TestMethod]
        public void TestMlMathNorm()
        {
            double n = MlMath.Norm(new double[] { 3, 4 });
            Assert.AreEqual(5.0, n, 1e-10);
        }

        [TestMethod]
        public void TestMlMathMean()
        {
            double m = MlMath.Mean(new double[] { 1, 2, 3, 4, 5 });
            Assert.AreEqual(3.0, m, 1e-10);
        }

        [TestMethod]
        public void TestMlMathStd()
        {
            double s = MlMath.Std(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
            Assert.AreEqual(2.0, s, 1e-10);
        }

        #endregion

        #region PEP Estimator Tests

        [TestMethod]
        public void TestPepWellSeparated()
        {
            // Port of Rust test_pep_well_separated
            var scores = new double[200];
            var isDecoy = new bool[200];

            // 100 targets with scores around 5.0
            for (int i = 0; i < 100; i++)
            {
                scores[i] = 4.0 + i * 0.02;
                isDecoy[i] = false;
            }
            // 100 decoys with scores around 1.0
            for (int i = 0; i < 100; i++)
            {
                scores[100 + i] = 0.0 + i * 0.02;
                isDecoy[100 + i] = true;
            }

            var estimator = PepEstimator.FitDefault(scores, isDecoy);

            double pepHigh = estimator.PosteriorError(5.0);
            Assert.IsTrue(pepHigh < 0.1, "PEP at high score: " + pepHigh);

            double pepLow = estimator.PosteriorError(0.5);
            Assert.IsTrue(pepLow > 0.5, "PEP at low score: " + pepLow);
        }

        [TestMethod]
        public void TestPepMonotonicity()
        {
            // Port of Rust test_pep_monotonicity
            var scores = new double[100];
            var isDecoy = new bool[100];

            for (int i = 0; i < 50; i++)
            {
                scores[i] = 3.0 + i * 0.1;
                isDecoy[i] = false;
            }
            for (int i = 0; i < 50; i++)
            {
                scores[50 + i] = 1.0 + i * 0.1;
                isDecoy[50 + i] = true;
            }

            var estimator = PepEstimator.FitDefault(scores, isDecoy);

            var testScores = new double[20];
            for (int i = 0; i < 20; i++)
                testScores[i] = 0.5 + i * 0.5;

            var peps = new double[20];
            for (int i = 0; i < 20; i++)
                peps[i] = estimator.PosteriorError(testScores[i]);

            for (int i = 1; i < peps.Length; i++)
            {
                Assert.IsTrue(peps[i] <= peps[i - 1] + 1e-6,
                    string.Format("PEP not monotonic: score={0}, PEP={1} > prev PEP={2}",
                        testScores[i], peps[i], peps[i - 1]));
            }
        }

        [TestMethod]
        public void TestPepEmptyInput()
        {
            var estimator = PepEstimator.FitDefault(new double[0], new bool[0]);
            Assert.AreEqual(1.0, estimator.PosteriorError(1.0), 1e-10);
        }

        [TestMethod]
        public void TestPepAllTargets()
        {
            var scores = new[] { 5.0, 4.0, 3.0 };
            var isDecoy = new[] { false, false, false };
            var estimator = PepEstimator.FitDefault(scores, isDecoy);
            // With no decoys, PEP should be 1.0
            Assert.AreEqual(1.0, estimator.PosteriorError(5.0), 1e-10);
        }

        #endregion
    }
}
