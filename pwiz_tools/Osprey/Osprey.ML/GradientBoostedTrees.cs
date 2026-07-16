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

// Pure-managed gradient-boosted decision trees for binary classification, used
// as a non-linear alternative to the linear Percolator SVM for FDR scoring.
//
// Second-order (Newton) boosting with the XGBoost regularized objective
// (Chen & Guestrin 2016): logistic loss, per-leaf L2 (lambda) + L1 (alpha)
// penalties, minimum split gain (gamma), minimum child hessian, row/column
// subsampling, and shrinkage. Histogram split finding over quantile-binned
// features. No native dependencies (builds on net472 + net8.0).
//
// The model output is a raw log-odds margin; the caller ranks by it exactly as
// with the SVM discriminant (target-decoy competition, q-values, PEP).

using System;
using System.Collections.Generic;

namespace pwiz.Osprey.ML
{
    /// <summary>Hyper-parameters for <see cref="GradientBoostedTrees"/>. Defaults are a
    /// conservative, regularized setting matching the validated Python XGBoost run.</summary>
    public sealed class GbtParams
    {
        public int NTrees = 200;
        public int MaxDepth = 6;
        public double LearningRate = 0.1;
        /// <summary>Minimum summed hessian (Σ p(1-p)) per leaf; blocks leaves that fit a handful of points.</summary>
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
        /// <summary>Histogram bins per feature (&lt;= 255 so bin indices fit a byte).</summary>
        public int MaxBins = 64;
        public int Seed = 42;
    }

    /// <summary>
    /// Gradient-boosted decision trees (Newton boosting, logistic loss) with L1/L2
    /// leaf regularization. Trained via <see cref="Train"/>; scored via
    /// <see cref="ScoreSingle"/>, which returns a raw log-odds margin.
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

        private GradientBoostedTrees(int[] feature, double[] threshold, int[] left, int[] right,
            double[] leaf, int[] treeRoot, double baseScore)
        {
            _feature = feature; _threshold = threshold; _left = left; _right = right;
            _leaf = leaf; _treeRoot = treeRoot; _baseScore = baseScore;
        }

        /// <summary>Raw log-odds margin for one feature vector.</summary>
        public double ScoreSingle(double[] x)
        {
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

        private static double Sigmoid(double z)
        {
            if (z >= 0) { double e = Math.Exp(-z); return 1.0 / (1.0 + e); }
            double ez = Math.Exp(z); return ez / (1.0 + ez);
        }

        /// <summary>
        /// Train on <paramref name="x"/> (rows = samples, cols = features) with binary
        /// labels: positive = target (<c>!isDecoy</c>), negative = decoy. Optional
        /// per-sample weights.
        /// </summary>
        public static GradientBoostedTrees Train(double[][] x, bool[] isDecoy, GbtParams p, double[] sampleWeight = null)
        {
            int n = x.Length;
            if (n == 0) throw new ArgumentException(@"GradientBoostedTrees.Train: empty training set");
            int nFeat = x[0].Length;
            int maxBins = Math.Max(2, Math.Min(255, p.MaxBins));

            // --- 1. Quantile bin edges per feature; precompute byte bin indices ---
            var cuts = new double[nFeat][];
            var bin = new byte[n][];
            for (int i = 0; i < n; i++) bin[i] = new byte[nFeat];
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
                for (int i = 0; i < n; i++)
                    bin[i][j] = (byte)BinOf(cj, x[i][j]);
            }

            // --- 2. Labels, weights, base score ---
            var y = new double[n];
            var w = sampleWeight ?? null;
            double pos = 0, tot = 0;
            for (int i = 0; i < n; i++)
            {
                y[i] = isDecoy[i] ? 0.0 : 1.0;
                double wi = w != null ? w[i] : 1.0;
                pos += y[i] * wi; tot += wi;
            }
            double frac = tot > 0 ? Math.Min(Math.Max(pos / tot, 1e-6), 1 - 1e-6) : 0.5;
            double baseScore = Math.Log(frac / (1 - frac));

            var f = new double[n];
            for (int i = 0; i < n; i++) f[i] = baseScore;
            var g = new double[n];
            var h = new double[n];

            var rng = new Random(p.Seed);
            var nodesFeature = new List<int>(); var nodesThresh = new List<double>();
            var nodesLeft = new List<int>(); var nodesRight = new List<int>();
            var nodesLeaf = new List<double>();
            var treeRoots = new List<int>();

            int nColUse = Math.Max(1, (int)Math.Round(nFeat * Math.Min(Math.Max(p.ColSample, 0.01), 1.0)));
            var allFeat = new int[nFeat];
            for (int j = 0; j < nFeat; j++) allFeat[j] = j;

            // --- 3. Boosting rounds ---
            for (int t = 0; t < p.NTrees; t++)
            {
                for (int i = 0; i < n; i++)
                {
                    double pi = Sigmoid(f[i]);
                    double wi = w != null ? w[i] : 1.0;
                    g[i] = (pi - y[i]) * wi;
                    h[i] = Math.Max(pi * (1 - pi) * wi, 1e-6);
                }

                // Row subsample (paired grouping is enforced upstream in fold assignment).
                var rows = Subsample(n, p.Subsample, rng);
                // Column subsample for this tree.
                var feats = SampleColumns(allFeat, nColUse, rng);

                int root = BuildTree(rows, 0, bin, cuts, feats, g, h, p, maxBins,
                    nodesFeature, nodesThresh, nodesLeft, nodesRight, nodesLeaf);
                treeRoots.Add(root);

                // Update margins for ALL samples with the new tree.
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
                treeRoots.ToArray(), baseScore);
        }

        // Recursively build one tree; appends nodes to the shared flat lists, returns the
        // node index of this subtree's root.
        private static int BuildTree(int[] rows, int depth, byte[][] bin, double[][] cuts,
            int[] feats, double[] g, double[] h, GbtParams p, int maxBins,
            List<int> nFeat, List<double> nThr, List<int> nLeft, List<int> nRight, List<double> nLeaf)
        {
            double G = 0, H = 0;
            for (int r = 0; r < rows.Length; r++) { int i = rows[r]; G += g[i]; H += h[i]; }

            bool leaf = depth >= p.MaxDepth || rows.Length < 2 || H < 2 * p.MinChildWeight;
            int bestFeat = -1, bestBin = -1;
            double bestGain = p.Gamma; // require gain strictly above gamma
            if (!leaf)
            {
                double parentTerm = G * G / (H + p.RegLambda);
                for (int fi = 0; fi < feats.Length; fi++)
                {
                    int j = feats[fi];
                    var hg = new double[maxBins];
                    var hh = new double[maxBins];
                    for (int r = 0; r < rows.Length; r++)
                    {
                        int i = rows[r]; int b = bin[i][j];
                        hg[b] += g[i]; hh[b] += h[i];
                    }
                    double gl = 0, hl = 0;
                    for (int b = 0; b < maxBins - 1; b++)
                    {
                        gl += hg[b]; hl += hh[b];
                        if (hl < 1e-12 && gl == 0) continue;
                        double gr = G - gl, hr = H - hl;
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
                nLeaf.Add(LeafValue(G, H, p));
                return idx;
            }

            // Partition rows by the chosen bin threshold.
            var left = new List<int>(); var right = new List<int>();
            for (int r = 0; r < rows.Length; r++)
            {
                int i = rows[r];
                if (bin[i][bestFeat] <= bestBin) left.Add(i); else right.Add(i);
            }

            int self = nFeat.Count;
            nFeat.Add(bestFeat); nThr.Add(cuts[bestFeat][bestBin]); nLeft.Add(-1); nRight.Add(-1); nLeaf.Add(0);
            int lc = BuildTree(left.ToArray(), depth + 1, bin, cuts, feats, g, h, p, maxBins, nFeat, nThr, nLeft, nRight, nLeaf);
            int rc = BuildTree(right.ToArray(), depth + 1, bin, cuts, feats, g, h, p, maxBins, nFeat, nThr, nLeft, nRight, nLeaf);
            nLeft[self] = lc; nRight[self] = rc;
            return self;
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
            Array.Sort(sorted);
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

        private static int[] Subsample(int n, double frac, Random rng)
        {
            if (frac >= 0.999) { var all = new int[n]; for (int i = 0; i < n; i++) all[i] = i; return all; }
            int m = Math.Max(1, (int)Math.Round(n * Math.Min(Math.Max(frac, 0.01), 1.0)));
            var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i;
            for (int i = 0; i < m; i++) { int j = i + rng.Next(n - i); int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp; }
            var res = new int[m]; Array.Copy(idx, res, m); return res;
        }

        private static int[] SampleColumns(int[] all, int k, Random rng)
        {
            if (k >= all.Length) return (int[])all.Clone();
            var idx = (int[])all.Clone();
            for (int i = 0; i < k; i++) { int j = i + rng.Next(idx.Length - i); int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp; }
            var res = new int[k]; Array.Copy(idx, res, k); Array.Sort(res); return res;
        }
    }
}
