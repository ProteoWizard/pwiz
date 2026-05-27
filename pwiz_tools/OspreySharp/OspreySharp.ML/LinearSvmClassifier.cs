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
using System.Collections.Concurrent;
using System.Threading;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Minimal explicit-thread parallel-for, modeled after
    /// pwiz.Common.SystemUtil.ParallelEx (Skyline). The key difference
    /// vs <c>System.Threading.Tasks.Parallel.For</c> is direct
    /// thread allocation: a fixed count of dedicated Threads pull
    /// indices from a shared atomic counter and call the body. No
    /// TaskReplicator heuristics, no ThreadPool hill-climbing, no
    /// scheduler throttling. Used by the Percolator hot path where
    /// TPL Parallel.For was failing to scale beyond ~2.5x on HRAM
    /// Astral (vs Rust rayon's ~9x on the same workload).
    /// </summary>
    public static class OspreyParallel
    {
        public static void For(int fromInclusive, int toExclusive, int threadCount, Action<int> body)
        {
            int count = toExclusive - fromInclusive;
            if (count <= 0) return;
            if (count == 1 || threadCount <= 1)
            {
                for (int i = fromInclusive; i < toExclusive; i++)
                    body(i);
                return;
            }

            int effectiveThreads = Math.Min(threadCount, count);
            int next = fromInclusive - 1;       // pre-decrement; Interlocked.Increment yields fromInclusive on first call
            Exception firstException = null;
            object exLock = new object();

            var threads = new Thread[effectiveThreads];
            for (int t = 0; t < effectiveThreads; t++)
            {
                threads[t] = new Thread(() =>
                {
                    while (true)
                    {
                        int i = Interlocked.Increment(ref next);
                        if (i >= toExclusive) return;
                        try
                        {
                            body(i);
                        }
                        catch (Exception ex)
                        {
                            lock (exLock)
                            {
                                if (firstException == null) firstException = ex;
                            }
                            return;
                        }
                    }
                });
                threads[t].IsBackground = true;
                threads[t].Name = "OspreyParallel";
                threads[t].Start();
            }
            foreach (var th in threads) th.Join();
            if (firstException != null)
                throw new AggregateException("Exception in OspreyParallel.For", firstException);
        }
    }


    /// <summary>
    /// Reusable per-call buffers for <c>LinearSvmClassifier.Train</c>.
    /// On large training sets each Train call allocated five fresh arrays
    /// (y, diag, alpha, w, indices) totalling ~2 MB at HRAM-Astral scale
    /// (n ~ 51K). With ~570 Train calls running 8-way parallel under
    /// Percolator's grid search, that allocation pressure showed up as
    /// per-call slowdown vs Rust (which uses stack-frame-local Vec but
    /// no GC pressure). Pool one scratch per parallel worker; rent/return
    /// around each Train call.
    /// </summary>
    public sealed class SvmTrainScratch
    {
        // n-sized (resized as the largest seen n grows; never shrinks)
        public double[] Y;
        public double[] Diag;
        public double[] Alpha;
        public int[] Indices;
        // (p+1)-sized; p (feature count) is constant in a Percolator run
        public double[] W;

        // Pooled row-major data buffers for ExtractRows results used in
        // Percolator's grid search. Each one wraps an (rows * p)-size
        // double[] that the caller fills via ExtractRowsInto and then
        // hands as a Matrix to LinearSvm.Train / DecisionFunction. For
        // HRAM Astral these would otherwise be ~8 MB LOH allocations
        // ~540x per file. TrainData and TestData are paired so a single
        // grid-search iteration can hold both simultaneously.
        public double[] TrainData;
        public double[] TestData;

        // Pooled buffers for PercolatorFdr.CountPassing's two per-call
        // arrays (allIndices: 0..n-1; qValues: per-winner). Sized to
        // initialN at scratch construction; EnsureCountPassingCapacity
        // grows on rare oversize requests.
        public int[] CountPassingIndices;
        public double[] CountPassingQvalues;

        // Pooled output buffers for the hot-path CompeteFromIndicesInto
        // helper. Sized to initialN. The active prefix length is
        // returned by the helper; callers read only [0..count).
        public int[] CompetitionWinnerIndices;
        public double[] CompetitionWinnerScores;
        public bool[] CompetitionWinnerIsDecoy;

        public SvmTrainScratch(int initialN, int p)
        {
            Y = new double[initialN];
            Diag = new double[initialN];
            Alpha = new double[initialN];
            Indices = new int[initialN];
            W = new double[p + 1];
            // Pre-allocate the ExtractRows buffers up front -- they
            // dominate per-call allocation pressure (8+ MB each for HRAM
            // Astral). The pool constructor knows the largest expected
            // subset size (subN); sizing here avoids the first-iteration
            // LOH stampede when ~20 parallel scratches each lazily
            // allocate 17 MB simultaneously (showed up as 10s OwnTime
            // in EnsureExtractCapacity in dotTrace).
            int extractCap = initialN * p;
            TrainData = new double[extractCap];
            TestData = new double[extractCap];
            CountPassingIndices = new int[initialN];
            CountPassingQvalues = new double[initialN];
            CompetitionWinnerIndices = new int[initialN];
            CompetitionWinnerScores = new double[initialN];
            CompetitionWinnerIsDecoy = new bool[initialN];
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void EnsureCountPassingCapacity(int n)
        {
            if (CountPassingIndices.Length < n)
                CountPassingIndices = new int[n];
            if (CountPassingQvalues.Length < n)
                CountPassingQvalues = new double[n];
            if (CompetitionWinnerIndices.Length < n)
                CompetitionWinnerIndices = new int[n];
            if (CompetitionWinnerScores.Length < n)
                CompetitionWinnerScores = new double[n];
            if (CompetitionWinnerIsDecoy.Length < n)
                CompetitionWinnerIsDecoy = new bool[n];
        }

        public void EnsureCapacity(int n, int p)
        {
            if (Y.Length < n)
            {
                Y = new double[n];
                Diag = new double[n];
                Alpha = new double[n];
                Indices = new int[n];
            }
            if (W.Length < p + 1)
                W = new double[p + 1];
        }

        /// <summary>
        /// Ensure <see cref="TrainData"/> / <see cref="TestData"/> each
        /// have capacity for <paramref name="rows"/> * <paramref name="p"/>
        /// doubles. The constructor pre-sizes both to the expected max,
        /// so this is a no-op in the steady state; the branch covers
        /// rare cases where a caller's actual subset exceeds the
        /// pool's <c>initialN</c>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void EnsureExtractCapacity(int rows, int p)
        {
            int need = rows * p;
            if (TrainData.Length < need)
                TrainData = new double[need];
            if (TestData.Length < need)
                TestData = new double[need];
        }
    }

    /// <summary>
    /// Concurrent pool of <see cref="SvmTrainScratch"/> sets. Same pattern
    /// as <c>XcorrScratchPool</c>: organic growth up to the parallel-worker
    /// high-water mark; arrays live in gen-2 LOH for the lifetime of the
    /// Percolator run; no LOH allocation in steady state.
    /// </summary>
    public sealed class SvmTrainScratchPool
    {
        private readonly ConcurrentBag<SvmTrainScratch> _bag = new ConcurrentBag<SvmTrainScratch>();
        private readonly int _initialN;
        private readonly int _p;
        private int _allocCount;

        public SvmTrainScratchPool(int initialN, int p)
        {
            _initialN = initialN;
            _p = p;
        }

        public int AllocCount { get { return _allocCount; } }

        public SvmTrainScratch Rent()
        {
            SvmTrainScratch s;
            if (_bag.TryTake(out s))
                return s;
            Interlocked.Increment(ref _allocCount);
            return new SvmTrainScratch(_initialN, _p);
        }

        public void Return(SvmTrainScratch s)
        {
            if (s == null)
                return;
            // No zeroing on return: Train re-initializes Y, Alpha, W,
            // Diag, and Indices from scratch each call.
            _bag.Add(s);
        }
    }


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
            return Train(features, labels, c, seed, null);
        }

        /// <summary>
        /// Overload that reuses pre-allocated working buffers from
        /// <paramref name="scratch"/>. Pass a per-worker
        /// <see cref="SvmTrainScratch"/> rented from
        /// <see cref="SvmTrainScratchPool"/> to avoid the five n-sized
        /// array allocations per call. Pass null to allocate fresh (the
        /// pre-pool behavior, retained for tests and ad-hoc callers).
        /// </summary>
        public static LinearSvmClassifier Train(Matrix features, bool[] labels, double c, ulong seed,
            SvmTrainScratch scratch)
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

            // Working buffers: either from the scratch pool (no allocation)
            // or fresh per-call (the legacy path). Pool mode reuses arrays
            // sized by the largest-seen n; we re-initialize the prefix we
            // actually use, so leftover bytes past n are harmless.
            double[] y, diag, alpha, w;
            int[] indices;
            if (scratch != null)
            {
                scratch.EnsureCapacity(n, p);
                y = scratch.Y;
                diag = scratch.Diag;
                alpha = scratch.Alpha;
                w = scratch.W;
                indices = scratch.Indices;
                // alpha and w start at zero each call; the loop reads them
                // before writing so we must clear the prefix we use.
                Array.Clear(alpha, 0, n);
                Array.Clear(w, 0, p + 1);
            }
            else
            {
                y = new double[n];
                diag = new double[n];
                alpha = new double[n];
                w = new double[p + 1];
                indices = new int[n];
            }

            // Convert labels: target (false) -> +1, decoy (true) -> -1
            for (int i = 0; i < n; i++)
                y[i] = labels[i] ? -1.0 : 1.0;

            double inv2c = 1.0 / (2.0 * c);

            // Precompute diagonal: D_ii = ||x_i||^2 + 1.0 (bias feature) + 1/(2C)
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

            // RNG for index permutation
            var rng = new XorShift64(seed);
            for (int i = 0; i < n; i++)
                indices[i] = i;

            double initialMaxPg = double.NegativeInfinity;

            for (int iter = 0; iter < MAX_ITER; iter++)
            {
                FisherYatesShuffle(indices, n, rng);

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
        /// Shuffles <c>slice[0..length]</c> -- callers that pass a pooled
        /// over-sized buffer must supply the active prefix length so
        /// shuffling stops there.
        /// </summary>
        private static void FisherYatesShuffle(int[] slice, int length, XorShift64 rng)
        {
            for (int i = length - 1; i >= 1; i--)
            {
                int j = (int)(rng.Next() % (ulong)(i + 1));
                int tmp = slice[i];
                slice[i] = slice[j];
                slice[j] = tmp;
            }
        }
    }
}
