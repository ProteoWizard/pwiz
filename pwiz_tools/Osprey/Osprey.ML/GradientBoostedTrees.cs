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
using pwiz.Osprey.Core;

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
        /// <summary>Threads used for training: binning, gradients, histograms, partitioning
        /// and margin updates. Every floating-point sum is taken in an order fixed by the data
        /// alone (histogram row chunks are sized from the row count, never the thread count),
        /// and everything else split across threads is per-row or pure data movement, so the
        /// trained model is bit-identical at any value. Defaults to 1 (sequential); a caller
        /// training several models concurrently should divide its thread budget between
        /// them.</summary>
        public int MaxDegreeOfParallelism = 1;
        /// <summary>Whether the sigmoid and the logistic base score use the platform-independent
        /// fdlibm arm of <see cref="DeterministicMath"/> (true) or <see cref="Math"/> (false).
        /// Defaults to <see cref="OspreyEnvironment.UseDeterministicMath"/>
        /// (OSPREY_DETERMINISTIC_MATH); a test sets it to train both arms in one process. Only
        /// true is bit-identical across operating systems.</summary>
        public bool UseDeterministicMath = OspreyEnvironment.UseDeterministicMath;

        /// <summary>A copy of these parameters, for a caller that needs to vary one field
        /// (typically <see cref="MaxDegreeOfParallelism"/>) without touching a shared
        /// instance.</summary>
        public GbtParams Clone()
        {
            return (GbtParams)MemberwiseClone();
        }
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
    /// on every target framework, on Windows and Linux, and at any
    /// <see cref="GbtParams.MaxDegreeOfParallelism"/>. The pieces that guarantee it:
    /// <list type="bullet">
    /// <item>the two transcendentals (the logistic base score's log-odds and the sigmoid in
    /// every round's gradient) come from <see cref="DeterministicMath"/>, not
    /// <see cref="Math"/>, whose Exp and Log are the OS C library's and differ in the last
    /// bit between Windows and Linux. Everything else is IEEE addition, subtraction,
    /// multiplication, division and comparison, which every platform rounds alike.
    /// <see cref="GbtParams.UseDeterministicMath"/> = false gives up the cross-OS half of
    /// the guarantee (results stay identical across thread counts on one OS);</item>
    /// <item>subsampling draws from <see cref="XorShift64"/> -- the same seeded,
    /// bit-exact-by-definition PRNG the rest of Osprey.ML uses -- NOT
    /// <c>System.Random</c>, whose seeded sequence is a framework implementation detail
    /// (this builds net472 AND net8.0, so a divergence there would silently train two
    /// different models from one source);</item>
    /// <item>every float accumulation (histograms, leaf gradients/hessians) runs in a fixed
    /// order. A node's leaf sums walk its rows in partition order on one thread. Its
    /// histogram is accumulated in row chunks whose count and boundaries depend only on the
    /// node's row count, each chunk into its own partial, and the partials are added in
    /// chunk order; a larger child's histogram is its parent's minus the smaller child's.
    /// Threads only decide WHO computes each partial, never the order inside it, so no sum
    /// can drift with the thread count. The other threaded steps (binning, gradients,
    /// margin updates, partitioning, the per-tree gather) compute each row or feature
    /// independently or only move data, so they cannot drift either;</item>
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
        // Smallest node worth handing to the scheduler in AccumulateHistograms, in
        // row-by-feature accumulation steps. Node population halves at every level, so most
        // nodes in a depth-6 tree are far too small to repay a parallel dispatch; the
        // million-row cost the parallel path exists for lives in the handful of wide nodes
        // near the root.
        private const long PARALLEL_WORK_THRESHOLD = 1L << 16;

        // Row range handed to one thread by the per-row loops (gradients, margin updates).
        private const int ROW_RANGE = 1 << 14;

        // Most features one histogram pass accumulates together; see AccumulateHistograms.
        private const int HISTOGRAM_GROUP_MAX = 4;

        // Row chunks a node's histogram is accumulated in: one per this many rows, at most
        // MAX_HISTOGRAM_CHUNKS. Fixed constants, never the thread count, because the chunking
        // sets the summation order. See AccumulateHistograms.
        private const int HISTOGRAM_CHUNK_ROWS = 1 << 13;
        private const int MAX_HISTOGRAM_CHUNKS = 32;

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
            if (n == 0)
                throw new ArgumentException(@"GradientBoostedTrees.Train: empty training set");
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

            // Null keeps every loop below on the calling thread; see ForRanges.
            int maxDegreeOfParallelism = Math.Max(1, p.MaxDegreeOfParallelism);
            var parallelOptions = maxDegreeOfParallelism > 1
                ? new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }
                : null;

            // --- 1. Quantile bin edges per feature; precompute byte bin indices ---
            // Bins are stored column-major so the per-tree gather in TreeWorkspace.Reset
            // streams each sampled feature's column front to back.
            var cuts = new double[nFeat][];
            var bin = new byte[n * nFeat];
            ForEachFeature(parallelOptions, nFeat, n, (j, col) =>
            {
                for (int i = 0; i < n; i++)
                {
                    double v = x[i][j];
                    col[i] = double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
                }
                var cj = QuantileCuts(col, maxBins);
                cuts[j] = cj;
                int colStart = j * n;
                for (int i = 0; i < n; i++)
                    bin[colStart + i] = (byte)BinOf(cj, x[i][j]);
            });

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
                baseScore = DeterministicMath.Log(frac / (1 - frac), p.UseDeterministicMath);
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

            var workspace = new TreeWorkspace(n, nColUse, maxBins, p, parallelOptions, bin, g, h);
            bool squaredError = p.Objective == GbtObjective.SquaredError;
            bool deterministicMath = p.UseDeterministicMath;

            // --- 3. Boosting rounds ---
            for (int t = 0; t < p.NTrees; t++)
            {
                // Per-row and order-free, so splitting the range across threads cannot
                // change a single value.
                ForRanges(parallelOptions, n, (lo, hi) =>
                {
                    for (int i = lo; i < hi; i++)
                    {
                        double wi = w != null ? w[i] : 1.0;
                        if (squaredError)
                        {
                            g[i] = (f[i] - y[i]) * wi;
                            h[i] = wi;
                        }
                        else
                        {
                            double pi = Sigmoid(f[i], deterministicMath);
                            g[i] = (pi - y[i]) * wi;
                            h[i] = Math.Max(pi * (1 - pi) * wi, 1e-6);
                        }
                    }
                });

                // Row subsample (paired grouping is enforced upstream in fold assignment),
                // drawn into the workspace's own row buffer, then the column subsample.
                int rowCount = workspace.Subsample(p.Subsample, rng);
                var feats = SampleColumns(allFeat, nColUse, rng);

                workspace.Reset(rowCount, feats);
                int firstNode = nodesFeature.Count;
                int root = BuildRoot(workspace, rowCount, cuts, p,
                    nodesFeature, nodesThresh, nodesLeft, nodesRight, nodesLeaf);
                treeRoots.Add(root);

                UpdateMargins(parallelOptions, x, f, firstNode, root,
                    nodesFeature, nodesThresh, nodesLeft, nodesRight, nodesLeaf);
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
                {
                    throw new ArgumentException(string.Format(
                        @"GradientBoostedTrees.FromModelData: tree {0} root is outside the node array", t));
                }
            }

            return new GradientBoostedTrees((int[])data.Feature.Clone(), (double[])data.Threshold.Clone(),
                (int[])data.Left.Clone(), (int[])data.Right.Clone(), (double[])data.Leaf.Clone(),
                (int[])data.TreeRoot.Clone(), data.BaseScore, data.FeatureCount, data.Objective);
        }

        private static double Sigmoid(double z, bool deterministicMath)
        {
            if (z >= 0) { double e = DeterministicMath.Exp(-z, deterministicMath); return 1.0 / (1.0 + e); }
            double ez = DeterministicMath.Exp(z, deterministicMath); return ez / (1.0 + ez);
        }

        // Per-tree scratch that outlives the recursion. Pooling matters at scale: a fresh
        // double[maxBins] pair per feature per node is millions of short-lived arrays on a
        // multi-million-row training set, and a fresh row buffer per tree is a
        // large-object-heap allocation per boosting round.
        //
        // The layout is what makes a wide node cheap. At the start of each tree the sampled
        // rows' bins and gradient/hessian pairs are gathered ONCE into partition order, one
        // contiguous column per sampled feature, and every partition after that moves them
        // with the rows. A node's rows are then always one contiguous range of every column,
        // so a histogram walks memory front to back instead of gathering from a
        // multi-megabyte column at random positions - the dominant cost once the training set
        // outgrows the cache. The columns are double-buffered by depth parity: a node at
        // depth d reads buffer d % 2 and partitions its range into the other, where only its
        // own subtree will ever look, so no partition needs a copy back.
        //
        // The cost is memory held for the whole Train call: per row about 32 bytes of ordered
        // gradient/hessian pairs, 2 bytes per sampled feature of ordered bins and 8 bytes of
        // row and position indices (about 30 MB per fold at the default 300k training rows),
        // plus at most MAX_HISTOGRAM_CHUNKS partial histograms.
        private sealed class TreeWorkspace
        {
            // Only this and the row buffer's contents change per boosting round; everything
            // else is fixed for the whole Train call and is therefore set once, in the
            // constructor.
            private int[] _feats;

            private readonly int _n;
            private readonly int _capacity;
            private readonly int[] _rows;
            private readonly int[] _position;
            private readonly byte[] _bin;
            private readonly double[] _g;
            private readonly double[] _h;
            private readonly byte[][] _ordered;
            private readonly double[][] _gh;
            private readonly byte[] _goLeft;
            private readonly int _maxBins;
            private readonly int _histLength;
            private readonly double[][] _gradHist;
            private readonly double[][] _hessHist;
            private readonly double[][] _chunkGradHist;
            private readonly double[][] _chunkHessHist;
            private readonly ParallelOptions _parallelOptions;

            public TreeWorkspace(int n, int nColUse, int maxBins, GbtParams p,
                ParallelOptions parallelOptions, byte[] bin, double[] g, double[] h)
            {
                _n = n;
                _capacity = SubsampleCount(n, p.Subsample);
                _maxBins = maxBins;
                _parallelOptions = parallelOptions;
                _bin = bin;
                _g = g;
                _h = h;
                _rows = new int[n];
                _position = new int[n];
                _goLeft = new byte[_capacity];
                // One spare slot per column (and gradient pair) past the subsample; see Reset.
                int orderedLength = nColUse * (_capacity + 1);
                _ordered = new[] { new byte[orderedLength], new byte[orderedLength] };
                _gh = new[] { new double[2 * (_capacity + 1)], new double[2 * (_capacity + 1)] };

                // One histogram buffer per depth that can still split: the root's at depth 0,
                // and at each split the smaller child's at the child depth while the larger
                // child inherits its parent's (see BuildTree). A node at MaxDepth is always a
                // leaf and never needs one. Width is nColUse, not nFeat, because only the
                // sampled columns are indexed.
                int levels = Math.Max(1, p.MaxDepth);
                _gradHist = new double[levels][];
                _hessHist = new double[levels][];
                for (int d = 0; d < levels; d++)
                {
                    _gradHist[d] = new double[nColUse * maxBins];
                    _hessHist[d] = new double[nColUse * maxBins];
                }

                // Partial histograms for a wide node's row chunks, created on first use: a
                // small training set never has more than one chunk and never needs them.
                _chunkGradHist = new double[MAX_HISTOGRAM_CHUNKS][];
                _chunkHessHist = new double[MAX_HISTOGRAM_CHUNKS][];
                _histLength = nColUse * maxBins;
            }

            public int[] Feats { get { return _feats; } }
            public int MaxBins { get { return _maxBins; } }
            public ParallelOptions ParallelOptions { get { return _parallelOptions; } }

            /// <summary>Distance between consecutive sampled-feature columns in
            /// <see cref="Ordered"/>.</summary>
            public int ColumnStride { get { return _capacity + 1; } }

            /// <summary>Bins of the sampled features for the rows in partition order, as seen
            /// by a node at <paramref name="depth"/>: column fi starts at fi *
            /// <see cref="ColumnStride"/>.</summary>
            public byte[] Ordered(int depth)
            {
                return _ordered[depth & 1];
            }

            /// <summary>Gradient and hessian of the row at partition position r, at 2r and
            /// 2r + 1, as seen by a node at <paramref name="depth"/>.</summary>
            public double[] Gh(int depth)
            {
                return _gh[depth & 1];
            }

            /// <summary>
            /// Draw this round's row subsample into the row buffer and return its size. The
            /// same partial Fisher-Yates over the identity permutation, with the same draws,
            /// as a fresh array per round would make; reusing the buffer only saves the
            /// allocation.
            /// </summary>
            public int Subsample(double frac, XorShift64 rng)
            {
                var idx = _rows;
                for (int i = 0; i < _n; i++)
                    idx[i] = i;
                if (frac >= 0.999)
                    return _n;
                int m = SubsampleCount(_n, frac);
                for (int i = 0; i < m; i++)
                {
                    int j = i + (int)(rng.Next() % (ulong)(_n - i));
                    int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp;
                }
                return m;
            }

            /// <summary>Gather the subsampled rows' gradients and the sampled features' bins
            /// into the depth-0 buffers, in subsample order.</summary>
            public void Reset(int rowCount, int[] feats)
            {
                _feats = feats;

                // Scatter rather than gather: walk the source rows in order and write each
                // selected one to its subsample position, so the reads stream and the random
                // accesses are stores, which do not stall. A row left out of the subsample
                // is written to the spare slot past the end of each buffer instead of being
                // branched around; the spare is never read, so concurrent writes to it from
                // different row ranges are harmless.
                var rows = _rows;
                var position = _position;
                int spare = _capacity;
                for (int i = 0; i < _n; i++)
                    position[i] = spare;
                for (int r = 0; r < rowCount; r++)
                    position[rows[r]] = r;

                var g = _g;
                var h = _h;
                var gh = _gh[0];
                ForRanges(_parallelOptions, _n, (lo, hi) =>
                {
                    for (int i = lo; i < hi; i++)
                    {
                        int r = position[i];
                        gh[2 * r] = g[i];
                        gh[2 * r + 1] = h[i];
                    }
                });

                var bin = _bin;
                var ordered = _ordered[0];
                int stride = ColumnStride;
                ForEach(_parallelOptions, feats.Length, fi =>
                {
                    int src = feats[fi] * _n;
                    int dst = fi * stride;
                    for (int i = 0; i < _n; i++)
                        ordered[dst + position[i]] = bin[src + i];
                });
            }

            // Indexed directly: a histogram is only built at a depth where the node or its
            // sibling can still split, so depth is always below MaxDepth. Clamping instead
            // would let an out-of-contract depth quietly alias a parent's buffer rather than
            // throw.
            public double[] GradHist(int depth)
            {
                return _gradHist[depth];
            }

            public double[] HessHist(int depth)
            {
                return _hessHist[depth];
            }

            /// <summary>Partial gradient histogram for row chunk <paramref name="chunk"/>,
            /// created by <see cref="EnsureChunks"/> on the calling thread before any parallel
            /// accumulation reads it.</summary>
            public double[] ChunkGradHist(int chunk)
            {
                return _chunkGradHist[chunk];
            }

            public double[] ChunkHessHist(int chunk)
            {
                return _chunkHessHist[chunk];
            }

            /// <summary>Create the partial histograms for the first <paramref name="chunks"/>
            /// row chunks, if not yet created.</summary>
            public void EnsureChunks(int chunks)
            {
                for (int c = 0; c < chunks; c++)
                {
                    if (_chunkGradHist[c] == null)
                    {
                        _chunkGradHist[c] = new double[_histLength];
                        _chunkHessHist[c] = new double[_histLength];
                    }
                }
            }

            /// <summary>
            /// Stable partition of positions [start, start + count) around the chosen
            /// (sampled-feature index, bin), from the depth-<paramref name="depth"/> buffers
            /// into the child depth's, returning the left count. Rows keep their relative
            /// order on both sides, so each child sees exactly the row sequence the parent saw.
            /// Pure data movement plus an integer count, so it is identical at any thread
            /// count.
            /// </summary>
            public int Partition(int depth, int start, int count, int bestFi, int bestBin)
            {
                var srcOrdered = _ordered[depth & 1];
                var dstOrdered = _ordered[(depth + 1) & 1];
                var srcGh = _gh[depth & 1];
                var dstGh = _gh[(depth + 1) & 1];
                var goLeft = _goLeft;
                int stride = ColumnStride;
                int end = start + count;

                // Which side each position goes to, once, for every column's move below.
                int splitColumn = bestFi * stride;
                int leftCount = 0;
                for (int r = start; r < end; r++)
                {
                    int k = srcOrdered[splitColumn + r] <= bestBin ? 1 : 0;
                    goLeft[r] = (byte)k;
                    leftCount += k;
                }
                int rightCount = count - leftCount;

                // Item 0 moves the gradient pairs; item fi + 1 moves sampled column fi.
                //
                // Branch-free: every value is written BOTH to the next left slot of the child
                // buffer and to the next right slot, compacted in place at the front of its own
                // source range, and only the matching cursor advances. Branching on a split
                // that sends rows either way at random mispredicts constantly. The spare
                // writes are harmless: a source slot at or before the one just read is dead,
                // and a child slot written early is overwritten by the next left value or, at
                // the end, by the rights copied in behind the lefts.
                int items = _feats.Length + 1;
                Action<int> move = item =>
                {
                    int li = start, ri = start;
                    if (item == 0)
                    {
                        for (int r = start; r < end; r++)
                        {
                            int k = goLeft[r];
                            double gv = srcGh[2 * r];
                            double hv = srcGh[2 * r + 1];
                            dstGh[2 * li] = gv;
                            dstGh[2 * li + 1] = hv;
                            srcGh[2 * ri] = gv;
                            srcGh[2 * ri + 1] = hv;
                            li += k;
                            ri += 1 - k;
                        }
                        Array.Copy(srcGh, 2 * start, dstGh, 2 * (start + leftCount), 2 * rightCount);
                        return;
                    }

                    int column = (item - 1) * stride;
                    for (int r = start; r < end; r++)
                    {
                        int k = goLeft[r];
                        byte v = srcOrdered[column + r];
                        dstOrdered[column + li] = v;
                        srcOrdered[column + ri] = v;
                        li += k;
                        ri += 1 - k;
                    }
                    Array.Copy(srcOrdered, column + start, dstOrdered, column + start + leftCount, rightCount);
                };
                if ((long)count * items < PARALLEL_WORK_THRESHOLD)
                {
                    for (int item = 0; item < items; item++)
                        move(item);
                }
                else
                {
                    ForEach(_parallelOptions, items, move);
                }
                return leftCount;
            }
        }

        // Build one tree over rows [start, start + count) of the workspace's row permutation:
        // the root's sums and histogram, then the recursion. Returns the root's node index.
        private static int BuildRoot(TreeWorkspace ws, int count, double[][] cuts, GbtParams p,
            List<int> nFeat, List<double> nThr, List<int> nLeft, List<int> nRight, List<double> nLeaf)
        {
            double gSum, hSum;
            SumRange(ws.Gh(0), 0, count, out gSum, out hSum);
            double[] hg = null, hh = null;
            if (!IsLeaf(p, 0, count, hSum))
            {
                hg = ws.GradHist(0);
                hh = ws.HessHist(0);
                AccumulateHistograms(ws, 0, count, 0, hg, hh);
            }
            return BuildTree(ws, 0, count, 0, gSum, hSum, hg, hh, cuts, p, nFeat, nThr, nLeft, nRight, nLeaf);
        }

        // Recursively build the subtree over rows [start, start + count), whose gradient and
        // hessian sums are gSum and hSum and whose histogram is (hg, hh) - null for a node
        // that cannot split. Appends nodes to the shared flat lists and returns the node
        // index of this subtree's root.
        //
        // Only the SMALLER child's histogram is accumulated from rows; the larger child's is
        // the parent's minus it, computed in place in the parent's buffer (histogram
        // subtraction). The parent's histogram is dead once its split is chosen, so the larger
        // child inherits that buffer and the smaller one takes the next depth's; neither is
        // touched by the other's subtree, which only ever uses its own buffer and deeper ones.
        private static int BuildTree(TreeWorkspace ws, int start, int count, int depth,
            double gSum, double hSum, double[] hg, double[] hh,
            double[][] cuts, GbtParams p,
            List<int> nFeat, List<double> nThr, List<int> nLeft, List<int> nRight, List<double> nLeaf)
        {
            int maxBins = ws.MaxBins;
            bool leaf = IsLeaf(p, depth, count, hSum);
            int bestFeat = -1, bestFi = -1, bestBin = -1;
            double bestGain = p.Gamma; // require gain strictly above gamma
            if (!leaf)
            {
                var feats = ws.Feats;
                double parentTerm = gSum * gSum / (hSum + p.RegLambda);
                for (int fi = 0; fi < feats.Length; fi++)
                {
                    int j = feats[fi];
                    int histStart = fi * maxBins;
                    double gl = 0, hl = 0;
                    for (int b = 0; b < maxBins - 1; b++)
                    {
                        gl += hg[histStart + b]; hl += hh[histStart + b];
                        // An empty left side. Tested on the hessian alone: a subtracted histogram leaves
                        // rounding residue in a bin no row falls in, so its gradient sum need not be 0.
                        if (hl < 1e-12) continue;
                        double gr = gSum - gl, hr = hSum - hl;
                        if (hl < p.MinChildWeight || hr < p.MinChildWeight) continue;
                        double gain = 0.5 * (gl * gl / (hl + p.RegLambda) + gr * gr / (hr + p.RegLambda) - parentTerm) - p.Gamma;
                        if (gain > bestGain) { bestGain = gain; bestFeat = j; bestFi = fi; bestBin = b; }
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

            int leftCount = ws.Partition(depth, start, count, bestFi, bestBin);
            int rightCount = count - leftCount;
            int childDepth = depth + 1;

            // The children's sums come from their own rows, in partition order, never from
            // the histograms, so leaf values stay exact sums over the leaf's rows.
            var childGh = ws.Gh(childDepth);
            double leftG, leftH, rightG, rightH;
            SumRange(childGh, start, leftCount, out leftG, out leftH);
            SumRange(childGh, start + leftCount, rightCount, out rightG, out rightH);

            double[] leftHg = null, leftHh = null, rightHg = null, rightHh = null;
            bool leftSplits = !IsLeaf(p, childDepth, leftCount, leftH);
            bool rightSplits = !IsLeaf(p, childDepth, rightCount, rightH);
            if (leftSplits || rightSplits)
            {
                bool leftSmaller = leftCount <= rightCount;
                var smallHg = ws.GradHist(childDepth);
                var smallHh = ws.HessHist(childDepth);
                if (leftSmaller)
                    AccumulateHistograms(ws, start, leftCount, childDepth, smallHg, smallHh);
                else
                    AccumulateHistograms(ws, start + leftCount, rightCount, childDepth, smallHg, smallHh);
                if (leftSmaller ? rightSplits : leftSplits)
                    SubtractHistogram(ws, hg, hh, smallHg, smallHh);

                if (leftSmaller)
                {
                    leftHg = smallHg; leftHh = smallHh;
                    rightHg = hg; rightHh = hh;
                }
                else
                {
                    leftHg = hg; leftHh = hh;
                    rightHg = smallHg; rightHh = smallHh;
                }
            }

            int self = nFeat.Count;
            nFeat.Add(bestFeat); nThr.Add(cuts[bestFeat][bestBin]); nLeft.Add(-1); nRight.Add(-1); nLeaf.Add(0);
            int lc = BuildTree(ws, start, leftCount, childDepth, leftG, leftH, leftHg, leftHh,
                cuts, p, nFeat, nThr, nLeft, nRight, nLeaf);
            int rc = BuildTree(ws, start + leftCount, rightCount, childDepth, rightG, rightH, rightHg, rightHh,
                cuts, p, nFeat, nThr, nLeft, nRight, nLeaf);
            nLeft[self] = lc; nRight[self] = rc;
            return self;
        }

        // A node that cannot split, whatever its histogram would show.
        private static bool IsLeaf(GbtParams p, int depth, int count, double hSum)
        {
            return depth >= p.MaxDepth || count < 2 || hSum < 2 * p.MinChildWeight;
        }

        // Gradient and hessian sums over partition positions [start, start + count), in order.
        private static void SumRange(double[] gh, int start, int count, out double gSum, out double hSum)
        {
            gSum = 0;
            hSum = 0;
            for (int r = start; r < start + count; r++)
            {
                gSum += gh[2 * r];
                hSum += gh[2 * r + 1];
            }
        }

        // (hg, hh) -= (sg, sh) over the sampled features' bins: the parent's histogram becomes
        // the larger child's.
        private static void SubtractHistogram(TreeWorkspace ws, double[] hg, double[] hh, double[] sg, double[] sh)
        {
            int used = ws.Feats.Length * ws.MaxBins;
            for (int k = 0; k < used; k++)
            {
                hg[k] -= sg[k];
                hh[k] -= sh[k];
            }
        }

        // Fill (hg, hh) with the node's gradient and hessian sums per (sampled feature, bin).
        //
        // The rows are cut into a number of chunks that depends only on the node's row
        // count, each chunk accumulates into its own partial histogram, and the partials are
        // added together in chunk order. So the summation order - and the trained model - is
        // fixed by the data alone, identical at any thread count, while the threads get both
        // row chunks and feature groups to work on: a chunk is read once for a group of
        // features instead of once per feature, which is what bounds the feature-only
        // split on a memory-bound histogram.
        private static void AccumulateHistograms(TreeWorkspace ws, int start, int count, int depth,
            double[] hg, double[] hh)
        {
            int nCols = ws.Feats.Length;
            int maxBins = ws.MaxBins;
            int used = nCols * maxBins;
            var ordered = ws.Ordered(depth);
            var gh = ws.Gh(depth);
            int stride = ws.ColumnStride;
            int chunks = Math.Min(MAX_HISTOGRAM_CHUNKS, Math.Max(1, count / HISTOGRAM_CHUNK_ROWS));
            int chunkRows = (count + chunks - 1) / chunks;
            if (chunks > 1)
                ws.EnsureChunks(chunks);

            // Features are accumulated a few at a time in one pass over the rows. A run of
            // rows landing in the same bin is a chain of dependent read-modify-write adds on
            // one histogram slot, which stalls a single-feature loop; interleaving
            // independent features gives the processor other chains to overlap with it. Each
            // feature still sums its own rows in order, so the grouping changes no value,
            // only the speed. Groups shrink when needed to give every thread work.
            var parallelOptions = ws.ParallelOptions;
            int groupSize = HISTOGRAM_GROUP_MAX;
            if (parallelOptions != null)
            {
                groupSize = Math.Max(1, Math.Min(HISTOGRAM_GROUP_MAX,
                    nCols * chunks / parallelOptions.MaxDegreeOfParallelism));
            }
            int groups = (nCols + groupSize - 1) / groupSize;

            Action<int> accumulate = task =>
            {
                int chunk = task / groups;
                int group = task % groups;
                var tg = chunks == 1 ? hg : ws.ChunkGradHist(chunk);
                var th = chunks == 1 ? hh : ws.ChunkHessHist(chunk);
                int lo = start + chunk * chunkRows;
                int len = Math.Min(start + count, lo + chunkRows) - lo;
                int fi = group * groupSize;
                int fiEnd = Math.Min(nCols, fi + groupSize);
                Array.Clear(tg, fi * maxBins, (fiEnd - fi) * maxBins);
                Array.Clear(th, fi * maxBins, (fiEnd - fi) * maxBins);
                for (; fi + 4 <= fiEnd; fi += 4)
                    AccumulateFour(ordered, gh, lo, len, fi * stride, stride, fi * maxBins, maxBins, tg, th);
                for (; fi < fiEnd; fi++)
                    AccumulateOne(ordered, gh, lo, len, fi * stride, fi * maxBins, tg, th);
            };

            // NOTE on the choice of Parallel.For over this assembly's own OspreyParallel.For:
            // OspreyParallel allocates dedicated Threads per call, which is right at fold
            // granularity (a handful of calls per run) but not here, where a call happens at
            // every internal node. Thread creation would swamp the work it parallelizes.
            // The ThreadPool this uses instead is shared with any concurrently training fold,
            // so a caller training folds in parallel should split its thread budget across
            // them rather than hand each fold every core.
            int tasks = chunks * groups;
            if ((long)count * nCols < PARALLEL_WORK_THRESHOLD)
            {
                for (int task = 0; task < tasks; task++)
                    accumulate(task);
            }
            else
            {
                ForEach(parallelOptions, tasks, accumulate);
            }
            if (chunks == 1)
                return;

            ForEach(parallelOptions, nCols, fi =>
            {
                int lo = fi * maxBins;
                Array.Copy(ws.ChunkGradHist(0), lo, hg, lo, maxBins);
                Array.Copy(ws.ChunkHessHist(0), lo, hh, lo, maxBins);
                for (int c = 1; c < chunks; c++)
                {
                    var cg = ws.ChunkGradHist(c);
                    var ch = ws.ChunkHessHist(c);
                    for (int k = lo; k < lo + maxBins; k++)
                    {
                        hg[k] += cg[k];
                        hh[k] += ch[k];
                    }
                }
            });
        }

        private static void AccumulateOne(byte[] ordered, double[] gh, int start, int count,
            int column, int histStart, double[] hg, double[] hh)
        {
            int end = start + count;
            for (int r = start; r < end; r++)
            {
                int b = histStart + ordered[column + r];
                hg[b] += gh[2 * r];
                hh[b] += gh[2 * r + 1];
            }
        }

        private static void AccumulateFour(byte[] ordered, double[] gh, int start, int count,
            int column, int stride, int histStart, int maxBins, double[] hg, double[] hh)
        {
            int c1 = column + stride, c2 = c1 + stride, c3 = c2 + stride;
            int h1 = histStart + maxBins, h2 = h1 + maxBins, h3 = h2 + maxBins;
            int end = start + count;
            for (int r = start; r < end; r++)
            {
                double gr = gh[2 * r];
                double hr = gh[2 * r + 1];
                int b0 = histStart + ordered[column + r];
                int b1 = h1 + ordered[c1 + r];
                int b2 = h2 + ordered[c2 + r];
                int b3 = h3 + ordered[c3 + r];
                hg[b0] += gr;
                hh[b0] += hr;
                hg[b1] += gr;
                hh[b1] += hr;
                hg[b2] += gr;
                hh[b2] += hr;
                hg[b3] += gr;
                hh[b3] += hr;
            }
        }

        // Add the newest tree's leaf value to every row's margin. Each row's update is
        // independent of every other's, so the rows are split freely across threads. The
        // walk compares raw feature values, not bins, so a NaN feature takes the right
        // branch here even though binning maps it to bin 0.
        private static void UpdateMargins(ParallelOptions parallelOptions, double[][] x, double[] f,
            int firstNode, int root,
            List<int> nFeat, List<double> nThr, List<int> nLeft, List<int> nRight, List<double> nLeaf)
        {
            // The tree is the tail of the flat lists from firstNode on, copied out so the
            // per-row walk indexes plain arrays rather than List indexers.
            int nodes = nFeat.Count - firstNode;
            var feature = new int[nodes];
            var threshold = new double[nodes];
            var left = new int[nodes];
            var right = new int[nodes];
            var leafValue = new double[nodes];
            for (int k = 0; k < nodes; k++)
            {
                feature[k] = nFeat[firstNode + k];
                threshold[k] = nThr[firstNode + k];
                left[k] = nLeft[firstNode + k] - firstNode;
                right[k] = nRight[firstNode + k] - firstNode;
                leafValue[k] = nLeaf[firstNode + k];
            }
            int rootLocal = root - firstNode;

            ForRanges(parallelOptions, x.Length, (lo, hi) =>
            {
                for (int i = lo; i < hi; i++)
                {
                    var xi = x[i];
                    int node = rootLocal;
                    while (feature[node] >= 0)
                        node = xi[feature[node]] <= threshold[node] ? left[node] : right[node];
                    f[i] += leafValue[node];
                }
            });
        }

        // Run body over [0, count) in fixed-size row ranges, spread across threads when
        // parallelOptions is non-null. Only for bodies whose rows are independent of each
        // other, where the split cannot change any value.
        private static void ForRanges(ParallelOptions parallelOptions, int count, Action<int, int> body)
        {
            if (parallelOptions == null || count < 2 * ROW_RANGE)
            {
                body(0, count);
                return;
            }

            int ranges = (count + ROW_RANGE - 1) / ROW_RANGE;
            Parallel.For(0, ranges, parallelOptions,
                k => body(k * ROW_RANGE, Math.Min(count, (k + 1) * ROW_RANGE)));
        }

        // Run body for each index in [0, count), spread across threads when parallelOptions
        // is non-null. Only for bodies that write disjoint state.
        private static void ForEach(ParallelOptions parallelOptions, int count, Action<int> body)
        {
            if (parallelOptions == null)
            {
                for (int k = 0; k < count; k++)
                    body(k);
                return;
            }

            Parallel.For(0, count, parallelOptions, body);
        }

        // Run body once per feature, each call with a private n-long scratch column that is
        // reused across the features the same thread takes.
        private static void ForEachFeature(ParallelOptions parallelOptions, int nFeat, int n,
            Action<int, double[]> body)
        {
            if (parallelOptions == null)
            {
                var col = new double[n];
                for (int j = 0; j < nFeat; j++)
                    body(j, col);
                return;
            }

            Parallel.For(0, nFeat, parallelOptions, () => new double[n],
                (j, state, col) =>
                {
                    body(j, col);
                    return col;
                },
                col => { });
        }

        // Rows drawn per boosting round: all of them at a fraction of (nearly) 1, otherwise
        // the rounded fraction, at least one.
        private static int SubsampleCount(int n, double frac)
        {
            if (frac >= 0.999)
                return n;
            return Math.Max(1, (int)Math.Round(n * Math.Min(Math.Max(frac, 0.01), 1.0)));
        }

        // Optimal leaf weight with L1 soft-threshold + L2 shrinkage, times learning rate.
        private static double LeafValue(double g, double h, GbtParams p)
        {
            // Under squared error the hessian is the raw sample weight, so a node whose rows
            // all carry weight 0 has h == 0 and, with RegLambda also 0 (settable from the
            // environment), the division below is 0/0. Its gradient is 0 too (g = (f-y)w), so
            // the correct update is no update. The logistic branch floors h at 1e-6 and never
            // gets here; for every h + RegLambda > 0 this is a pure pass-through.
            if (h + p.RegLambda <= 0)
                return 0.0;

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

        // Sorts values in place: the caller's scratch column, whose order is not needed again.
        private static double[] QuantileCuts(double[] values, int maxBins)
        {
            var sorted = values;
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

        private static int[] SampleColumns(int[] all, int k, XorShift64 rng)
        {
            if (k >= all.Length) return (int[])all.Clone();
            var idx = (int[])all.Clone();
            for (int i = 0; i < k; i++) { int j = i + (int)(rng.Next() % (ulong)(idx.Length - i)); int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp; }
            var res = new int[k]; Array.Copy(idx, res, k); Array.Sort(res); return res; // Array.Sort OK: single primitive array of DISTINCT feature indices (partial Fisher-Yates over a distinct set), so the comparator never ties
        }
    }
}
