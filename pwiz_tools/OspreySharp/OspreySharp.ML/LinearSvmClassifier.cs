// Linear SVM for binary classification
//
// Implements a linear Support Vector Machine using dual coordinate descent
// for L2-regularized L2-loss SVM (squared hinge loss).
//
// Reference: Hsieh et al. (2008) "A Dual Coordinate Descent Method for
// Large-scale Linear SVM", ICML.
//
// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Deterministic xorshift64 PRNG for reproducible shuffling.
    /// Matches the Rust implementation: x ^= x &lt;&lt; 13; x ^= x &gt;&gt; 7; x ^= x &lt;&lt; 17.
    /// </summary>
    public class XorShift64
    {
        private ulong _state;

        public XorShift64(ulong seed)
        {
            // Ensure non-zero state
            _state = seed == 0 ? 1UL : seed;
        }

        public ulong Next()
        {
            ulong x = _state;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _state = x;
            return x;
        }
    }

    /// <summary>
    /// Standardize features to zero mean and unit variance.
    /// Port of FeatureStandardizer from svm.rs.
    /// </summary>
    public class FeatureStandardizer
    {
        private readonly double[] _means;
        private readonly double[] _stds;

        private FeatureStandardizer(double[] means, double[] stds)
        {
            _means = means;
            _stds = stds;
        }

        /// <summary>Mean values for each feature.</summary>
        public double[] Means { get { return _means; } }

        /// <summary>Standard deviation values for each feature.</summary>
        public double[] Stds { get { return _stds; } }

        /// <summary>Number of features this standardizer was fit on.</summary>
        public int NumFeatures { get { return _means.Length; } }

        /// <summary>
        /// Compute mean and std for each feature column.
        /// </summary>
        public static FeatureStandardizer Fit(Matrix features)
        {
            double n = features.Rows;
            int p = features.Cols;

            var means = new double[p];
            var stds = new double[p];

            // Compute means
            for (int row = 0; row < features.Rows; row++)
            {
                for (int col = 0; col < p; col++)
                    means[col] += features[row, col];
            }
            for (int col = 0; col < p; col++)
                means[col] /= n;

            // Compute standard deviations
            for (int row = 0; row < features.Rows; row++)
            {
                for (int col = 0; col < p; col++)
                {
                    double diff = features[row, col] - means[col];
                    stds[col] += diff * diff;
                }
            }
            for (int col = 0; col < p; col++)
            {
                stds[col] = Math.Sqrt(stds[col] / n);
                // Avoid division by zero for zero-variance features
                if (stds[col] < 1e-12)
                    stds[col] = 1.0;
            }

            return new FeatureStandardizer(means, stds);
        }

        /// <summary>
        /// Transform features using pre-computed mean/std: (x - mean) / std.
        /// </summary>
        public Matrix Transform(Matrix features)
        {
            int p = features.Cols;
            var data = new double[features.Rows * p];
            for (int row = 0; row < features.Rows; row++)
            {
                for (int col = 0; col < p; col++)
                {
                    int idx = row * p + col;
                    data[idx] = (features[row, col] - _means[col]) / _stds[col];
                }
            }
            return new Matrix(data, features.Rows, p);
        }

        /// <summary>
        /// Fit and transform in one step.
        /// </summary>
        public static FeatureStandardizer FitTransform(Matrix features, out Matrix transformed)
        {
            var standardizer = Fit(features);
            transformed = standardizer.Transform(features);
            return standardizer;
        }

        /// <summary>
        /// Transform a single feature vector in-place.
        /// </summary>
        public void TransformSlice(double[] features)
        {
            for (int col = 0; col < features.Length; col++)
                features[col] = (features[col] - _means[col]) / _stds[col];
        }
    }

    /// <summary>
    /// Linear SVM classifier trained via dual coordinate descent (Liblinear algorithm).
    /// Port of LinearSvm from svm.rs.
    /// </summary>
    public class LinearSvmClassifier
    {
        private static readonly double CONVERGENCE_EPS = 0.01;
        private static readonly int MAX_ITER = 200;

        private readonly double[] _weights;
        private readonly double _bias;

        /// <summary>
        /// Create a LinearSvmClassifier with pre-computed weights and bias.
        /// </summary>
        public LinearSvmClassifier(double[] weights, double bias)
        {
            _weights = (double[])weights.Clone();
            _bias = bias;
        }

        /// <summary>Learned feature weights (hyperplane normal vector).</summary>
        public double[] Weights { get { return _weights; } }

        /// <summary>Bias term (intercept).</summary>
        public double Bias { get { return _bias; } }

        /// <summary>
        /// Train a linear SVM using dual coordinate descent for L2-regularized
        /// L2-loss SVM (squared hinge loss).
        /// </summary>
        /// <param name="features">Feature matrix (rows = samples, cols = features)</param>
        /// <param name="labels">Labels: true = decoy (y=-1), false = target (y=+1)</param>
        /// <param name="c">Cost parameter (higher = less regularization)</param>
        /// <param name="seed">Random seed for reproducible shuffling</param>
        /// <returns>Trained LinearSvmClassifier model</returns>
        public static LinearSvmClassifier Train(Matrix features, bool[] labels, double c, ulong seed)
        {
            if (features.Rows != labels.Length)
                throw new ArgumentException("Feature rows must match label count");

            int n = features.Rows;
            int p = features.Cols;

            if (n == 0 || p == 0)
                return new LinearSvmClassifier(new double[p], 0.0);

            // Hoist hot-path locals out of the inner loop. The JIT can sometimes
            // CSE these but explicit locals guarantee no per-iteration property
            // dispatch and let the JIT keep them in registers.
            double[] data = features.Data;
            int cols = p;

            // Convert labels: target (false) -> +1, decoy (true) -> -1
            var y = new double[n];
            for (int i = 0; i < n; i++)
                y[i] = labels[i] ? -1.0 : 1.0;

            double inv2c = 1.0 / (2.0 * c);

            // Precompute diagonal: D_ii = ||x_i||^2 + 1.0 (bias feature) + 1/(2C)
            var diag = new double[n];
            for (int i = 0; i < n; i++)
            {
                double normSq = 0.0;
                int rowStart = cols * i;
                for (int k = 0; k < cols; k++)
                {
                    double v = data[rowStart + k];
                    normSq += v * v;
                }
                diag[i] = normSq + 1.0 + inv2c;
            }

            // Initialize dual variables and primal weight vector
            // w has p+1 elements: w[0..p] = feature weights, w[p] = bias
            var alpha = new double[n];
            var w = new double[p + 1];

            // RNG for index permutation
            var rng = new XorShift64(seed);
            var indices = new int[n];
            for (int i = 0; i < n; i++)
                indices[i] = i;

            double initialMaxPg = double.NegativeInfinity;

            for (int iter = 0; iter < MAX_ITER; iter++)
            {
                FisherYatesShuffle(indices, rng);

                double maxPgViolation = 0.0;

                for (int idx = 0; idx < n; idx++)
                {
                    int i = indices[idx];
                    int rowStart = cols * i;

                    // w . x_i (augmented: includes bias w[p] * 1.0)
                    double wx = 0.0;
                    for (int k = 0; k < cols; k++)
                        wx += w[k] * data[rowStart + k];
                    wx += w[p];

                    // Gradient: g = y_i * (w . x_i) - 1 + alpha_i/(2C)
                    double g = y[i] * wx - 1.0 + alpha[i] * inv2c;

                    // Projected gradient for convergence check
                    double pg = (alpha[i] == 0.0) ? Math.Min(g, 0.0) : g;

                    double absPg = Math.Abs(pg);
                    if (absPg > maxPgViolation)
                        maxPgViolation = absPg;

                    if (absPg > 1e-12)
                    {
                        double alphaOld = alpha[i];
                        alpha[i] = Math.Max(alpha[i] - g / diag[i], 0.0);
                        double d = (alpha[i] - alphaOld) * y[i];

                        // Update w += d * x_i (augmented)
                        for (int k = 0; k < cols; k++)
                            w[k] += d * data[rowStart + k];
                        w[p] += d; // bias feature = 1.0
                    }
                }

                // Set initial max PG on first iteration for relative convergence
                if (iter == 0)
                {
                    initialMaxPg = maxPgViolation;
                    if (initialMaxPg <= 0.0)
                        break;
                }

                if (maxPgViolation < CONVERGENCE_EPS * initialMaxPg)
                    break;
            }

            // Split augmented weight vector into weights and bias
            double bias = w[p];
            var weights = new double[p];
            Array.Copy(w, weights, p);

            return new LinearSvmClassifier(weights, bias);
        }

        /// <summary>
        /// Compute decision function values: w . x + b for each sample.
        /// Higher values indicate more target-like (positive class).
        /// </summary>
        public double[] DecisionFunction(Matrix features)
        {
            var scores = Matrix.DotVector(features, _weights);
            for (int i = 0; i < scores.Length; i++)
                scores[i] += _bias;
            return scores;
        }

        /// <summary>
        /// Score a single feature vector: w . x + b.
        /// </summary>
        public double ScoreSingle(double[] features)
        {
            double score = _bias;
            for (int i = 0; i < _weights.Length && i < features.Length; i++)
                score += _weights[i] * features[i];
            return score;
        }

        /// <summary>
        /// Predict class labels: true if decision function > 0 (target), false otherwise.
        /// </summary>
        public bool[] Predict(Matrix features)
        {
            var scores = DecisionFunction(features);
            var predictions = new bool[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                predictions[i] = scores[i] > 0;
            return predictions;
        }

        /// <summary>
        /// Fisher-Yates shuffle using XorShift64 PRNG.
        /// Matches the Rust implementation exactly.
        /// </summary>
        private static void FisherYatesShuffle(int[] slice, XorShift64 rng)
        {
            for (int i = slice.Length - 1; i >= 1; i--)
            {
                int j = (int)(rng.Next() % (ulong)(i + 1));
                int tmp = slice[i];
                slice[i] = slice[j];
                slice[j] = tmp;
            }
        }
    }
}
