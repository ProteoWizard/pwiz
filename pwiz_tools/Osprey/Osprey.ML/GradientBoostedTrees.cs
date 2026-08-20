/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

// Pure-managed gradient-boosted decision trees, used as a non-linear alternative to the
// linear Percolator SVM for FDR scoring (binary logistic) and as the m/z calibration
// model for MARS (squared error).
//
// Second-order (Newton) boosting with the XGBoost regularized objective
// (Chen & Guestrin 2016): per-leaf L2 (lambda) + L1 (alpha) penalties, minimum split
// gain (gamma), minimum child hessian, row/column subsampling, and shrinkage.
// Histogram split finding over quantile-binned features. No native dependencies
// (builds on net472 + net8.0).
//
// Everything except the base score and the per-round gradient is loss-agnostic: quantile
// binning, histogram split finding, the L1 soft-threshold and L2 leaf weight, subsampling
// and the flat node arrays all apply unchanged to either objective.
//
// The model output is the raw additive margin with no link function. For logistic that is
// a log-odds the caller ranks by exactly as with the SVM discriminant (target-decoy
// competition, q-values, PEP); for squared error it is the prediction itself.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace pwiz.Osprey.ML
{
    /// <summary>Loss function optimized by <see cref="GradientBoostedTrees"/>.</summary>
    public enum GbtObjective
    {
        /// <summary>Binary logistic loss. Base score is the log-odds of the weighted
        /// positive fraction; h = p(1-p)w, which never exceeds 0.25w.</summary>
        LogisticBinary,

        /// <summary>Squared error. Base score is the weighted mean of y; g = (f-y)w and
        /// h = w, so an unweighted hessian sum is exactly a sample count.</summary>
        SquaredError
    }

    /// <summary>Hyper-parameters for <see cref="GradientBoostedTrees"/>. Defaults are a
    /// conservative, regularized setting matching the validated Python XGBoost run.</summary>
    public sealed class GbtParams
    {
        /// <summary>Loss function. Defaults to the binary logistic loss used by the FDR path.</summary>
        public GbtObjective Objective = GbtObjective.LogisticBinary;
        public int NTrees = 200;
        public int MaxDepth = 6;
        public double LearningRate = 0.1;
        /// <summary>Minimum summed hessian per leaf; blocks leaves that fit a handful of
        /// points. NOTE that the hessian means different things under the two objectives:
        /// under <see cref="GbtObjective.LogisticBinary"/> it is p(1-p), at most 0.25 and
        /// shrinking as the model sharpens, so a summed hessian is far below the sample
        /// count; under <see cref="GbtObjective.SquaredError"/> it is the sample weight, so
        /// with unit weights a threshold of 1.0 means exactly one sample. Carry the
        /// hyper-parameters over from the reference run rather than assuming these defaults
        /// transfer between objectives.</summary>
        public double MinChildWeight = 1.0;
        /// <summary>Row subsample fraction per tree (stochastic boosting).</summary>
        public double Subsample = 0.8;
        /// <summary>Feature subsample fraction per tree.</summary>
        public double ColSample = 0.8;
        /// <summary>Minimum split gain (gamma) to keep a split.</summary>
        public double Gamma = 0.0;
        /// <summary>L2 penalty on leaf weights (lambda).</summary>
        public double RegLambda = 1.0;
        /// <summary>L1 penalty on leaf weights (alpha).</summary>
        public double RegAlpha = 0.0;
        /// <summary>Histogram bins per feature. CLAMPED to [2, 255] at the start of training,
        /// because a bin index has to fit a byte. Note that XGBoost's own <c>max_bin</c>
        /// default is 256, so a value transcribed from a Python configuration trains with 255
        /// here rather than the 256 it asks for.</summary>
        public int MaxBins = 64;
        /// <summary>Seed for the row/column subsampling PRNG. Drives
        /// <see cref="XorShift64"/> -- see the determinism note on
        /// <see cref="GradientBoostedTrees"/>.</summary>
        public ulong Seed = 42;
        /// <summary>Threads used to accumulate histograms. Parallelism is applied ACROSS
        /// FEATURES only, so every histogram is still summed in ascending row order by a
        /// single thread and the trained model is bit-identical at any value. Defaults to
        /// 1, which keeps the FDR path exactly sequential; raise it only for training sets
        /// large enough that the accumulation dominates (millions of rows).</summary>
        public int MaxDegreeOfParallelism = 1;
    }

    /// <summary>
    /// A trained ensemble reduced to plain arrays, so callers that need to persist a model
    /// can serialize it without reflecting over private state. Round-trips exactly: the
    /// arrays ARE the model, and <see cref="GradientBoostedTrees.FromModelData"/> rebuilds
    /// a scorer that returns bit-identical margins.
    ///
    /// Internal nodes have Feature in [0, FeatureCount) and branch on Threshold
    /// (value &lt;= Threshold goes to Left), with Left and Right both greater than the
    /// node's own index and different from each other. Leaves have Feature == -1, carry
    /// -1 in BOTH Left and Right, and contribute Leaf, already scaled by the learning
    /// rate. TreeRoot holds the node index each tree starts at.
    ///
    /// A writer that omits Left/Right for leaves rather than emitting -1 will be rejected
    /// on load; those two fields are part of the contract, not an implementation detail.
    /// </summary>
    public sealed class GbtModelData
    {
        public int[] Feature;
        public double[] Threshold;
        public int[] Left;
        public int[] Right;
        public double[] Leaf;
        public int[] TreeRoot;
        public double BaseScore;

        /// <summary>Feature-vector width the model was trained on. Lets the load bounds-check
        /// every split feature, so a corrupted index fails there rather than as an
        /// index-out-of-range inside <see cref="GradientBoostedTrees.ScoreSingle"/>.</summary>
        public int FeatureCount;

        /// <summary>Objective the model was trained under. Recorded because the node arrays
        /// alone cannot distinguish a squared-error margin from a log-odds one, and feeding a
        /// reloaded regression margin to q-value or PEP estimation would be silently wrong.</summary>
        public GbtObjective Objective;
    }

    /// <summary>
    /// Gradient-boosted decision trees (Newton boosting) with L1/L2 leaf regularization.
    /// Trained via <see cref="Train(double[][], bool[], GbtParams, double[])"/> for binary
    /// classification or <see cref="Train(double[][], double[], GbtParams, double[])"/> for
    /// regression; scored via <see cref="ScoreSingle"/>, which returns the raw additive
    /// margin.
    ///
    /// DETERMINISTIC by construction, to the same standard as the linear SVM it stands
    /// in for: identical input produces a bit-identical model and bit-identical scores,
    /// on every target framework and at any <see cref="GbtParams.MaxDegreeOfParallelism"/>.
    /// The pieces that guarantee it:
    /// <list type="bullet">
    /// <item>subsampling draws from <see cref="XorShift64"/> -- the same seeded,
    /// bit-exact-by-definition PRNG the rest of Osprey.ML uses -- NOT
    /// <c>System.Random</c>, whose seeded sequence is a framework implementation detail
    /// (this builds net472 AND net8.0, so a divergence there would silently train two
    /// different models from one source);</item>
    /// <item>every float accumulation (histograms, leaf gradients/hessians) runs in a fixed
    /// row order. Histogram work may be spread across threads, but only ACROSS FEATURES:
    /// one thread owns a feature's histogram and walks the node's rows in ascending order,
    /// so no summation order can drift with the thread count;</item>
    /// <item>row partitioning is stable, so each child sees its rows in the same relative
    /// order they had in the parent;</item>
    /// <item>split selection scans features and bins in ascending order and takes a new
    /// best only on a strict improvement, so ties resolve to the lowest (feature, bin);</item>
    /// <item>the one <c>Array.Sort</c> is over a primitive array read only by quantile
    /// index, where equal values are interchangeable.</item>
    /// </list>
    /// Callers may train folds in parallel: each <see cref="Train(double[][], bool[], GbtParams, double[])"/>
    /// call owns its PRNG and touches no shared state. <see cref="ScoreSingle"/> is pure
    /// and thread-safe.
    /// </summary>
    public sealed class GradientBoostedTrees
    {
        // Flattened node arrays across all trees. Internal node: Feature >= 0, split
        // at Threshold (value &lt;= Threshold -> Left, else Right). Leaf: Feature == -1,
        // contribution == Leaf (already scaled by learning rate).
        private readonly int[] _feature;
        private readonly double[] _threshold;
        private readonly int[] _left;
        private readonly int[] _right;
        private readonly double[] _leaf;
        private readonly int[] _treeRoot;
        private readonly double _baseScore;
        private readonly int _featureCount;
        private readonly GbtObjective _objective;

        private GradientBoostedTrees(int[] feature, double[] threshold, int[] left, int[] right,
            double[] leaf, int[] treeRoot, double baseScore, int featureCount, GbtObjective objective)
        {
            _feature = feature; _threshold = threshold; _left = left; _right = right;
            _leaf = leaf; _treeRoot = treeRoot; _baseScore = baseScore;
            _featureCount = featureCount; _objective = objective;
        }

        /// <summary>Feature-vector width this model expects.</summary>
        public int FeatureCount { get { return _featureCount; } }

        /// <summary>Objective this model was trained under. A margin from
        /// <see cref="GbtObjective.SquaredError"/> is a prediction, not a log-odds, and must
        /// not be handed to q-value or PEP estimation.</summary>
        public GbtObjective Objective { get { return _objective; } }

        /// <summary>
        /// Train on <paramref name="x"/> (rows = samples, cols = features) with binary
        /// labels: positive = target (<c>!isDecoy</c>), negative = decoy. Optional
        /// per-sample weights.
        /// </summary>
        public static GradientBoostedTrees Train(double[][] x, bool[] isDecoy, GbtParams p, double[] sampleWeight = null)
        {
            if (isDecoy == null)
                throw new ArgumentNullException(nameof(isDecoy));
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            // This overload promises a log-odds margin from binary labels, and callers rank
            // by it. A GbtParams instance carried over from a regression call would otherwise
            // quietly fit squared error to 0/1 targets and return something that is not a
            // log-odds at all, which q-value and PEP estimation downstream would not survive.
            if (p.Objective != GbtObjective.LogisticBinary)
            {
                throw new ArgumentException(string.Format(
                    @"GradientBoostedTrees.Train: the binary-label overload requires GbtObjective.LogisticBinary, not {0}. Use the continuous-target overload for regression.",
                    p.Objective));
            }

            var y = new double[isDecoy.Length];
            for (int i = 0; i < isDecoy.Length; i++)
                y[i] = isDecoy[i] ? 0.0 : 1.0;
            return Train(x, y, p, sampleWeight);
        }

        /// <summary>
        /// Train on <paramref name="x"/> (rows = samples, cols = features) against a
        /// continuous target <paramref name="y"/>, using the loss named by
        /// <see cref="GbtParams.Objective"/>. Optional per-sample weights.
        /// </summary>
        public static GradientBoostedTrees Train(double[][] x, double[] y, GbtParams p, double[] sampleWeight = null)
        {
            if (x == null)
                throw new ArgumentNullException(nameof(x));
            if (p == null)
                throw new ArgumentNullException(nameof(p));
            int n = x.Length;
            if (n == 0) throw new ArgumentException(@"GradientBoostedTrees.Train: empty training set");
            if (y == null || y.Length != n)
                throw new ArgumentException(@"GradientBoostedTrees.Train: target length must match the row count");

            // Caught here rather than as an index-out-of-range partway through boosting,
            // which would leave the caller guessing which array was the wrong length.
            if (sampleWeight != null && sampleWeight.Length != n)
            {
                throw new ArgumentException(
                    @"GradientBoostedTrees.Train: sample weight length must match the row count");
            }

            // Under squared error the hessian IS the weight, with no positive floor of the
            // kind the logistic branch applies. A negative weight can then drive a node's
            // summed hessian to exactly -RegLambda and divide by zero in LeafValue, and one
            // NaN leaf poisons every later round through the margin update. RegLambda is
            // settable to 0 from the environment, so this is reachable in production.
            if (sampleWeight != null)
            {
                for (int i = 0; i < n; i++)
                {
                    if (double.IsNaN(sampleWeight[i]) || double.IsInfinity(sampleWeight[i]) || sampleWeight[i] < 0)
                    {
                        throw new ArgumentException(string.Format(
                            @"GradientBoostedTrees.Train: sample weight {0} is {1}; weights must be finite and non-negative",
                            i, sampleWeight[i]));
                    }
                }
            }

            switch (p.Objective)
            {
                case GbtObjective.LogisticBinary:
                    // The logistic gradient is sigmoid(f) - y, which only means anything for a
                    // y in [0, 1]. Leaving Objective at its default and passing a continuous
                    // target through this overload would otherwise train a finite-looking but
                    // meaningless model with no diagnostic at all.
                    for (int i = 0; i < n; i++)
                    {
                        if (double.IsNaN(y[i]) || y[i] < 0.0 || y[i] > 1.0)
                        {
                            throw new ArgumentException(string.Format(
                                @"GradientBoostedTrees.Train: target {0} is {1}; GbtObjective.LogisticBinary requires targets in [0, 1]. Set Objective to SquaredError for regression.",
                                i, y[i]));
                        }
                    }

                    break;

                case GbtObjective.SquaredError:
                    for (int i = 0; i < n; i++)
                    {
                        if (double.IsNaN(y[i]) || double.IsInfinity(y[i]))
                        {
                            throw new ArgumentException(string.Format(
                                @"GradientBoostedTrees.Train: target {0} is {1}; targets must be finite", i, y[i]));
                        }
                    }

                    break;

                default:
                    // An out-of-range cast would otherwise fall through to the logistic branch
                    // and train the wrong loss silently.
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.Train: unknown objective {0}", p.Objective));
            }

            int nFeat = x[0].Length;
            int maxBins = Math.Max(2, Math.Min(255, p.MaxBins));
            if ((long)n * nFeat > int.MaxValue)
                throw new ArgumentException(@"GradientBoostedTrees.Train: training matrix exceeds the addressable bin array size");

            // --- 1. Quantile bin edges per feature; precompute byte bin indices ---
            // Bins are stored column-major so histogram accumulation for one feature walks
            // a contiguous run instead of striding across one small array per row.
            var cuts = new double[nFeat][];
            var bin = new byte[n * nFeat];
            var col = new double[n];
            for (int j = 0; j < nFeat; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    double v = x[i][j];
                    col[i] = double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
                }
                cuts[j] = QuantileCuts(col, maxBins);
                var cj = cuts[j];
                int colStart = j * n;
                for (int i = 0; i < n; i++)
                    bin[colStart + i] = (byte)BinOf(cj, x[i][j]);
            }

            // --- 2. Weights and base score ---
            var w = sampleWeight;
            double pos = 0, tot = 0;
            for (int i = 0; i < n; i++)
            {
                double wi = w != null ? w[i] : 1.0;
                pos += y[i] * wi; tot += wi;
            }
            double baseScore;
            if (p.Objective == GbtObjective.SquaredError)
            {
                baseScore = tot > 0 ? pos / tot : 0.0;
            }
            else
            {
                double frac = tot > 0 ? Math.Min(Math.Max(pos / tot, 1e-6), 1 - 1e-6) : 0.5;
                baseScore = Math.Log(frac / (1 - frac));
            }

            var f = new double[n];
            for (int i = 0; i < n; i++) f[i] = baseScore;
            var g = new double[n];
            var h = new double[n];

            var rng = new XorShift64(p.Seed);
            var nodesFeature = new List<int>(); var nodesThresh = new List<double>();
            var nodesLeft = new List<int>(); var nodesRight = new List<int>();
            var nodesLeaf = new List<double>();
            var treeRoots = new List<int>();

            int nColUse = Math.Max(1, (int)Math.Round(nFeat * Math.Min(Math.Max(p.ColSample, 0.01), 1.0)));
            var allFeat = new int[nFeat];
            for (int j = 0; j < nFeat; j++) allFeat[j] = j;

            var workspace = new TreeWorkspace(n, nColUse, maxBins, p, bin, g, h);

            // --- 3. Boosting rounds ---
            for (int t = 0; t < p.NTrees; t++)
            {
                if (p.Objective == GbtObjective.SquaredError)
                {
                    for (int i = 0; i < n; i++)
                    {
                        double wi = w != null ? w[i] : 1.0;
                        g[i] = (f[i] - y[i]) * wi;
                        h[i] = wi;
                    }
                }
                else
                {
                    for (int i = 0; i < n; i++)
                    {
                        double pi = Sigmoid(f[i]);
                        double wi = w != null ? w[i] : 1.0;
                        g[i] = (pi - y[i]) * wi;
                        h[i] = Math.Max(pi * (1 - pi) * wi, 1e-6);
                    }
                }

                // Row subsample (paired grouping is enforced upstream in fold assignment).
                var rows = Subsample(n, p.Subsample, rng);
                // Column subsample for this tree.
                var feats = SampleColumns(allFeat, nColUse, rng);

                workspace.Reset(rows, feats);
                int root = BuildTree(workspace, 0, rows.Length, 0, cuts, p,
                    nodesFeature, nodesThresh, nodesLeft, nodesRight, nodesLeaf);
                treeRoots.Add(root);

                // Update margins for ALL samples with the new tree. The walk compares raw
                // feature values, not bins, so a NaN feature takes the right branch here
                // even though binning maps it to bin 0.
                for (int i = 0; i < n; i++)
                {
                    int node = root;
                    while (nodesFeature[node] >= 0)
                        node = x[i][nodesFeature[node]] <= nodesThresh[node] ? nodesLeft[node] : nodesRight[node];
                    f[i] += nodesLeaf[node];
                }
            }

            return new GradientBoostedTrees(nodesFeature.ToArray(), nodesThresh.ToArray(),
                nodesLeft.ToArray(), nodesRight.ToArray(), nodesLeaf.ToArray(),
                treeRoots.ToArray(), baseScore, nFeat, p.Objective);
        }

        /// <summary>Raw additive margin for one feature vector: a log-odds under
        /// <see cref="GbtObjective.LogisticBinary"/>, the prediction itself under
        /// <see cref="GbtObjective.SquaredError"/>.</summary>
        public double ScoreSingle(double[] x)
        {
            if (x == null)
                throw new ArgumentNullException(nameof(x));

            // A short vector would otherwise read past the caller's array only for whichever
            // features the traversal happens to touch, so the failure would depend on the
            // data rather than on the mistake.
            if (x.Length < _featureCount)
            {
                throw new ArgumentException(string.Format(
                    @"GradientBoostedTrees.ScoreSingle: model expects {0} features, got {1}",
                    _featureCount, x.Length));
            }

            double f = _baseScore;
            for (int t = 0; t < _treeRoot.Length; t++)
            {
                int node = _treeRoot[t];
                while (_feature[node] >= 0)
                    node = x[_feature[node]] <= _threshold[node] ? _left[node] : _right[node];
                f += _leaf[node];
            }
            return f;
        }

        /// <summary>Flatten this model to plain arrays for persistence. The arrays are
        /// copies; mutating them does not affect this instance.</summary>
        public GbtModelData ToModelData()
        {
            return new GbtModelData
            {
                Feature = (int[])_feature.Clone(),
                Threshold = (double[])_threshold.Clone(),
                Left = (int[])_left.Clone(),
                Right = (int[])_right.Clone(),
                Leaf = (double[])_leaf.Clone(),
                TreeRoot = (int[])_treeRoot.Clone(),
                BaseScore = _baseScore,
                FeatureCount = _featureCount,
                Objective = _objective
            };
        }

        /// <summary>Rebuild a scorer from persisted arrays. Validates the node graph rather
        /// than trusting it: a truncated or hand-edited model file would otherwise surface
        /// as an index-out-of-range deep inside scoring, or worse, as silently wrong
        /// scores.</summary>
        public static GradientBoostedTrees FromModelData(GbtModelData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Feature == null || data.Threshold == null || data.Left == null ||
                data.Right == null || data.Leaf == null || data.TreeRoot == null)
            {
                throw new ArgumentException(@"GradientBoostedTrees.FromModelData: incomplete model data");
            }

            int nodes = data.Feature.Length;
            if (data.Threshold.Length != nodes || data.Left.Length != nodes ||
                data.Right.Length != nodes || data.Leaf.Length != nodes)
            {
                throw new ArgumentException(@"GradientBoostedTrees.FromModelData: node arrays must be the same length");
            }
            if (data.TreeRoot.Length == 0)
                throw new ArgumentException(@"GradientBoostedTrees.FromModelData: model has no trees");
            if (data.FeatureCount <= 0)
                throw new ArgumentException(@"GradientBoostedTrees.FromModelData: feature count must be positive");

            for (int i = 0; i < nodes; i++)
            {
                // Bounds-checking the split feature here is what keeps a corrupted index from
                // surfacing as an index-out-of-range inside ScoreSingle instead.
                if (data.Feature[i] >= data.FeatureCount)
                {
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.FromModelData: node {0} splits on feature {1}, outside the {2} the model was trained on",
                        i, data.Feature[i], data.FeatureCount));
                }

                if (data.Feature[i] < 0)
                {
                    // A leaf owns no children. Rejecting stale indices here stops a partial
                    // edit from leaving a node that scores as a leaf but still points somewhere.
                    if (data.Left[i] != -1 || data.Right[i] != -1)
                    {
                        throw new ArgumentException(string.Format(
                            @"GradientBoostedTrees.FromModelData: leaf {0} carries a child index", i));
                    }

                    continue;
                }

                if (data.Left[i] < 0 || data.Left[i] >= nodes || data.Right[i] < 0 || data.Right[i] >= nodes)
                {
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.FromModelData: node {0} has a child index outside the node array", i));
                }

                // BuildTree appends a node before recursing into its children, so a child
                // index always exceeds its parent's and the two differ. Requiring that on load
                // is what rules out a cycle: without it, corrupted data makes ScoreSingle spin
                // forever instead of failing.
                if (data.Left[i] <= i || data.Right[i] <= i || data.Left[i] == data.Right[i])
                {
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.FromModelData: node {0} has child indices {1} and {2} that do not both increase, which would make scoring cycle",
                        i, data.Left[i], data.Right[i]));
                }
            }
            for (int t = 0; t < data.TreeRoot.Length; t++)
            {
                if (data.TreeRoot[t] < 0 || data.TreeRoot[t] >= nodes)
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.FromModelData: tree {0} root is outside the node array", t));
            }

            return new GradientBoostedTrees((int[])data.Feature.Clone(), (double[])data.Threshold.Clone(),
                (int[])data.Left.Clone(), (int[])data.Right.Clone(), (double[])data.Leaf.Clone(),
                (int[])data.TreeRoot.Clone(), data.BaseScore, data.FeatureCount, data.Objective);
        }

        private static double Sigmoid(double z)
        {
            if (z >= 0) { double e = Math.Exp(-z); return 1.0 / (1.0 + e); }
            double ez = Math.Exp(z); return ez / (1.0 + ez);
        }

        // Per-tree scratch that outlives the recursion: the row index permutation being
        // partitioned in place, the pooled per-depth histograms, and the partition buffer.
        // Pooling matters at scale. A fresh double[maxBins] pair per feature per node is
        // millions of short-lived arrays on a multi-million-row training set.
        private sealed class TreeWorkspace
        {
            // Only these two change per boosting round; everything else is fixed for the
            // whole Train call and is therefore set once, in the constructor.
            public int[] Rows;
            public int[] Feats;

            public readonly byte[] Bin;
            public readonly int RowStride;
            public readonly double[] G;
            public readonly double[] H;
            public readonly int MaxBins;
            public readonly int MaxDegreeOfParallelism;

            private readonly double[][] _gradHist;
            private readonly double[][] _hessHist;
            private readonly int[] _partition;
            private readonly ParallelOptions _parallelOptions;

            public TreeWorkspace(int n, int nColUse, int maxBins, GbtParams p,
                byte[] bin, double[] g, double[] h)
            {
                MaxBins = maxBins;
                MaxDegreeOfParallelism = Math.Max(1, p.MaxDegreeOfParallelism);
                Bin = bin;
                RowStride = n;
                G = g;
                H = h;
                _partition = new int[n];

                // Allocated once rather than at every node. A depth-6 tree has up to 63
                // internal nodes, so per-node allocation would be ~12,600 throwaway objects
                // per fold per boosting run.
                _parallelOptions = MaxDegreeOfParallelism > 1
                    ? new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism }
                    : null;

                // One histogram buffer per depth that can still split: a node's histogram is
                // dead once its split is chosen, so the two children share the next level's
                // buffer in turn, and a node at MaxDepth is always a leaf and never builds one.
                // Width is nColUse, not nFeat, because only the sampled columns are indexed.
                int levels = Math.Max(1, p.MaxDepth);
                _gradHist = new double[levels][];
                _hessHist = new double[levels][];
                for (int d = 0; d < levels; d++)
                {
                    _gradHist[d] = new double[nColUse * maxBins];
                    _hessHist[d] = new double[nColUse * maxBins];
                }
            }

            public ParallelOptions ParallelOptions
            {
                get { return _parallelOptions; }
            }

            public void Reset(int[] rows, int[] feats)
            {
                Rows = rows; Feats = feats;
            }

            // Indexed directly: AccumulateHistograms is only reached for a node that can
            // still split, so depth is always below MaxDepth. Clamping instead would let an
            // out-of-contract depth quietly alias a parent's buffer rather than throw.
            public double[] GradHist(int depth)
            {
                return _gradHist[depth];
            }

            public double[] HessHist(int depth)
            {
                return _hessHist[depth];
            }

            public int[] PartitionBuffer
            {
                get { return _partition; }
            }
        }

        // Recursively build one tree over rows [start, start + count) of the workspace's
        // row permutation; appends nodes to the shared flat lists and returns the node
        // index of this subtree's root.
        private static int BuildTree(TreeWorkspace ws, int start, int count, int depth,
            double[][] cuts, GbtParams p,
            List<int> nFeat, List<double> nThr, List<int> nLeft, List<int> nRight, List<double> nLeaf)
        {
            int maxBins = ws.MaxBins;
            var rows = ws.Rows;
            var g = ws.G;
            var h = ws.H;
            double gSum = 0, hSum = 0;
            for (int r = start; r < start + count; r++) { int i = rows[r]; gSum += g[i]; hSum += h[i]; }

            bool leaf = depth >= p.MaxDepth || count < 2 || hSum < 2 * p.MinChildWeight;
            int bestFeat = -1, bestBin = -1;
            double bestGain = p.Gamma; // require gain strictly above gamma
            if (!leaf)
            {
                var hg = ws.GradHist(depth);
                var hh = ws.HessHist(depth);
                var feats = ws.Feats;
                AccumulateHistograms(ws, start, count, depth, hg, hh, feats);

                double parentTerm = gSum * gSum / (hSum + p.RegLambda);
                for (int fi = 0; fi < feats.Length; fi++)
                {
                    int j = feats[fi];
                    int histStart = fi * maxBins;
                    double gl = 0, hl = 0;
                    for (int b = 0; b < maxBins - 1; b++)
                    {
                        gl += hg[histStart + b]; hl += hh[histStart + b];
                        if (hl < 1e-12 && gl == 0) continue;
                        double gr = gSum - gl, hr = hSum - hl;
                        if (hl < p.MinChildWeight || hr < p.MinChildWeight) continue;
                        double gain = 0.5 * (gl * gl / (hl + p.RegLambda) + gr * gr / (hr + p.RegLambda) - parentTerm) - p.Gamma;
                        if (gain > bestGain) { bestGain = gain; bestFeat = j; bestBin = b; }
                    }
                }
                if (bestFeat < 0) leaf = true;
            }

            if (leaf)
            {
                int idx = nFeat.Count;
                nFeat.Add(-1); nThr.Add(0); nLeft.Add(-1); nRight.Add(-1);
                nLeaf.Add(LeafValue(gSum, hSum, p));
                return idx;
            }

            int leftCount = Partition(ws, start, count, bestFeat, bestBin);

            int self = nFeat.Count;
            nFeat.Add(bestFeat); nThr.Add(cuts[bestFeat][bestBin]); nLeft.Add(-1); nRight.Add(-1); nLeaf.Add(0);
            int lc = BuildTree(ws, start, leftCount, depth + 1, cuts, p, nFeat, nThr, nLeft, nRight, nLeaf);
            int rc = BuildTree(ws, start + leftCount, count - leftCount, depth + 1, cuts, p, nFeat, nThr, nLeft, nRight, nLeaf);
            nLeft[self] = lc; nRight[self] = rc;
            return self;
        }

        // Fill this depth's pooled histogram with the node's gradient and hessian sums per
        // (sampled feature, bin). One thread owns a feature and walks the node's rows in
        // ascending order, so the sums do not depend on the thread count.
        // Smallest node worth handing to the scheduler, in row-by-feature accumulation steps.
        // Node population halves at every level, so most nodes in a depth-6 tree are far too
        // small to repay a parallel dispatch; the million-row cost this exists for lives in
        // the handful of wide nodes near the root.
        private const long PARALLEL_WORK_THRESHOLD = 1L << 16;

        private static void AccumulateHistograms(TreeWorkspace ws, int start, int count, int depth,
            double[] hg, double[] hh, int[] feats)
        {
            int used = feats.Length * ws.MaxBins;
            Array.Clear(hg, 0, used);
            Array.Clear(hh, 0, used);

            // NOTE on the choice of Parallel.For over this assembly's own OspreyParallel.For:
            // OspreyParallel allocates dedicated Threads per call, which is right at fold
            // granularity (a handful of calls per run) but not here, where a call happens at
            // every internal node. Thread creation would swamp the work it parallelizes.
            // The trade-off is that this path uses the shared ThreadPool, so it must not be
            // turned on inside fold-parallel training; MaxDegreeOfParallelism defaults to 1
            // and the FDR path never enters this branch.
            if (ws.MaxDegreeOfParallelism <= 1 ||
                (long)count * feats.Length < PARALLEL_WORK_THRESHOLD)
            {
                for (int fi = 0; fi < feats.Length; fi++)
                    AccumulateFeature(ws, start, count, fi, hg, hh);
                return;
            }

            Parallel.For(0, feats.Length, ws.ParallelOptions,
                fi => AccumulateFeature(ws, start, count, fi, hg, hh));
        }

        private static void AccumulateFeature(TreeWorkspace ws, int start, int count, int fi,
            double[] hg, double[] hh)
        {
            var rows = ws.Rows;
            var bin = ws.Bin;
            var g = ws.G;
            var h = ws.H;
            int colStart = ws.Feats[fi] * ws.RowStride;
            int histStart = fi * ws.MaxBins;
            for (int r = start; r < start + count; r++)
            {
                int i = rows[r];
                int b = bin[colStart + i];
                hg[histStart + b] += g[i];
                hh[histStart + b] += h[i];
            }
        }

        // Stable in-place partition of rows [start, start + count) around the chosen bin.
        // Rows keep their relative order on both sides, so each child sees exactly the row
        // sequence the previous list-building implementation produced.
        private static int Partition(TreeWorkspace ws, int start, int count, int bestFeat, int bestBin)
        {
            var rows = ws.Rows;
            var bin = ws.Bin;
            var right = ws.PartitionBuffer;
            int colStart = bestFeat * ws.RowStride;

            int leftCount = 0, rightCount = 0;
            for (int r = start; r < start + count; r++)
            {
                int i = rows[r];
                if (bin[colStart + i] <= bestBin)
                    rows[start + leftCount++] = i;
                else
                    right[rightCount++] = i;
            }

            Array.Copy(right, 0, rows, start + leftCount, rightCount);
            return leftCount;
        }

        // Optimal leaf weight with L1 soft-threshold + L2 shrinkage, times learning rate.
        private static double LeafValue(double g, double h, GbtParams p)
        {
            double num = g;
            if (p.RegAlpha > 0)
                num = g > p.RegAlpha ? g - p.RegAlpha : (g < -p.RegAlpha ? g + p.RegAlpha : 0.0);
            return -p.LearningRate * num / (h + p.RegLambda);
        }

        // cuts is length maxBins-1; bin index in [0, maxBins-1] = count of cuts < v.
        private static int BinOf(double[] cuts, double v)
        {
            if (double.IsNaN(v)) return 0;
            int lo = 0, hi = cuts.Length;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (cuts[mid] < v) lo = mid + 1; else hi = mid; }
            return lo;
        }

        private static double[] QuantileCuts(double[] values, int maxBins)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted); // Array.Sort OK: single primitive array read only by quantile INDEX to pick cut points; equal values are interchangeable, so tie order cannot affect the cuts
            int nCut = maxBins - 1;
            var cuts = new List<double>(nCut);
            double last = double.NegativeInfinity;
            for (int k = 1; k <= nCut; k++)
            {
                double q = (double)k / maxBins;
                int idx = (int)(q * (sorted.Length - 1));
                double c = sorted[idx];
                if (c > last) { cuts.Add(c); last = c; } // dedupe (skewed features)
            }
            if (cuts.Count == 0) cuts.Add(sorted[sorted.Length - 1]); // constant feature: one trivial cut
            return cuts.ToArray();
        }

        private static int[] Subsample(int n, double frac, XorShift64 rng)
        {
            if (frac >= 0.999) { var all = new int[n]; for (int i = 0; i < n; i++) all[i] = i; return all; }
            int m = Math.Max(1, (int)Math.Round(n * Math.Min(Math.Max(frac, 0.01), 1.0)));
            var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i;
            for (int i = 0; i < m; i++) { int j = i + (int)(rng.Next() % (ulong)(n - i)); int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp; }
            var res = new int[m]; Array.Copy(idx, res, m); return res;
        }

        private static int[] SampleColumns(int[] all, int k, XorShift64 rng)
        {
            if (k >= all.Length) return (int[])all.Clone();
            var idx = (int[])all.Clone();
            for (int i = 0; i < k; i++) { int j = i + (int)(rng.Next() % (ulong)(idx.Length - i)); int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp; }
            var res = new int[k]; Array.Copy(idx, res, k); Array.Sort(res); return res; // Array.Sort OK: single primitive array of DISTINCT feature indices (partial Fisher-Yates over a distinct set), so the comparator never ties
        }
    }
}
