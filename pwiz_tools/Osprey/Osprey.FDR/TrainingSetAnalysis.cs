/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// What Percolator's cross-validated training did with each feature, measured on the rows it
    /// actually trained on (issues #4550 and #4467). Computed by
    /// <see cref="PercolatorTrainer.RunPercolator"/> while the standardized training subset is
    /// resident, and persisted with the first-pass model, because the per-fold training sets
    /// exist nowhere else: a report written later from the artifacts must say the same thing
    /// (doc 00, P16).
    ///
    /// <para>Three populations, all inside the training subset (the <c>MaxTrainSize</c>
    /// subsample, so the cost is bounded by that cap and not by the cohort):</para>
    /// <list type="bullet">
    /// <item>The FINAL TRAINING SET: per fold, the positives and decoys the fold's returned
    /// (best-iteration) model was trained on. Per-feature values pool the folds, so a row that
    /// trained in two folds counts twice; the counts are training-row counts, not precursors.</item>
    /// <item>In-sample: every subset row a fold's model trained on, all targets vs all decoys,
    /// scored by that model.</item>
    /// <item>Held-out: each subset row scored by the model of the fold it was held out of.</item>
    /// </list>
    ///
    /// <para>Plain properties throughout so the Tasks layer can serialize it without this
    /// assembly referencing Json.NET.</para>
    /// </summary>
    public sealed class TrainingSetAnalysis
    {
        /// <summary>Features whose absolute Pearson correlation on the training subset reaches
        /// this are joined into one group (single linkage).</summary>
        public const double CORRELATION_GROUP_THRESHOLD = 0.8;

        /// <summary>The false-positive rate at which <see cref="FeatureStats.TprAtReferenceFpr"/>
        /// reads the ROC curve: the low-FPR corner the q &lt;= 0.01 acceptance lives in.</summary>
        public const double REFERENCE_FPR = 0.01;

        /// <summary>
        /// Held-out rows per fold the permutations re-score for a tree ensemble, where every
        /// re-scored row walks every tree: about 1 + 21 + groups re-scorings of each row. Larger
        /// folds are thinned to roughly this many rows, whole target-decoy pairs at a time.
        /// </summary>
        public const int TREE_PERMUTATION_ROW_CAP = 50000;

        // Log-spaced FPR grid for the stored ROC curves: 1e-4 .. 1, ten points per decade.
        private const int ROC_DECADES = 4;
        private const int ROC_POINTS_PER_DECADE = 10;

        /// <summary>Cross-validation folds.</summary>
        public int NFolds { get; set; }

        /// <summary>The FDR the positive training sets were selected at (after any relaxation).</summary>
        public double TrainFdr { get; set; }

        /// <summary>Targets in the training subset (each row once).</summary>
        public int SubsetTargets { get; set; }

        /// <summary>Decoys in the training subset (each row once).</summary>
        public int SubsetDecoys { get; set; }

        /// <summary>Per fold: positives in the final training set.</summary>
        public int[] FoldPositives { get; set; }

        /// <summary>Per fold: decoys in the final training set.</summary>
        public int[] FoldDecoys { get; set; }

        /// <summary>Per fold: the iteration whose model the fold returned.</summary>
        public int[] FoldIterations { get; set; }

        /// <summary>The shared FPR grid of every stored ROC curve (ascending, log-spaced).</summary>
        public double[] RocFpr { get; set; }

        /// <summary>Per feature, in canonical feature-index order.</summary>
        public FeatureStats[] Features { get; set; }

        /// <summary>The composite score on the final training set, in-sample (fold model on its own training rows).</summary>
        public CompositeRoc TrainingComposite { get; set; }

        /// <summary>The composite score on every in-sample row, all targets vs all decoys.</summary>
        public CompositeRoc InSampleComposite { get; set; }

        /// <summary>The composite score on every held-out row, all targets vs all decoys. Its gap
        /// to <see cref="InSampleComposite"/> is the overfitting readout.</summary>
        public CompositeRoc HeldOutComposite { get; set; }

        /// <summary>Held-out targets passing <see cref="TrainFdr"/> under paired target-decoy
        /// competition, summed over folds, with no feature permuted: the permutation baseline.</summary>
        public int PermutationBaselinePassing { get; set; }

        /// <summary>Held-out rows the permutation metric is counted over: the whole subset, or for
        /// trees at most about <see cref="TREE_PERMUTATION_ROW_CAP"/> per fold.</summary>
        public int PermutationRows { get; set; }

        /// <summary>The threshold <see cref="Groups"/> were formed at.</summary>
        public double CorrelationThreshold { get; set; }

        /// <summary>Pearson correlation of the standardized features over the training subset
        /// (each row once, targets and decoys together), canonical order.</summary>
        public double[][] Correlation { get; set; }

        /// <summary>Groups of two or more features linked at <see cref="CorrelationThreshold"/>.</summary>
        public FeatureGroup[] Groups { get; set; }

        /// <summary>One feature's separation on the final training set and its permutation importance.</summary>
        public sealed class FeatureStats
        {
            /// <summary>P(positive value &gt; decoy value) on the pooled final training set, ties
            /// counted half. Below 0.5 means lower values are the target-like ones.</summary>
            public double Auroc { get; set; }

            /// <summary>Smallest per-fold AUROC.</summary>
            public double AurocFoldMin { get; set; }

            /// <summary>Largest per-fold AUROC.</summary>
            public double AurocFoldMax { get; set; }

            /// <summary>(mean positive - mean decoy) / pooled standard deviation, pooled final training set.</summary>
            public double CohenD { get; set; }

            /// <summary>The fraction of positives above the decoy threshold that admits
            /// <see cref="REFERENCE_FPR"/> of the decoys.</summary>
            public double TprAtReferenceFpr { get; set; }

            /// <summary>TPR at each point of <see cref="RocFpr"/>.</summary>
            public double[] RocTpr { get; set; }

            /// <summary>Positive-class histogram over <see cref="FeatureContributions.HistEdges"/>.</summary>
            public int[] TrainTargetHist { get; set; }

            /// <summary>Decoy-class histogram over <see cref="FeatureContributions.HistEdges"/>.</summary>
            public int[] TrainDecoyHist { get; set; }

            /// <summary>Held-out targets passing with this feature permuted, summed over folds.</summary>
            public int PermutationPassing { get; set; }

            /// <summary>Fractional drop from <see cref="PermutationBaselinePassing"/> to
            /// <see cref="PermutationPassing"/>.</summary>
            public double PermutationDrop { get; set; }
        }

        /// <summary>A ROC curve averaged over folds (each fold's model scores its own rows).</summary>
        public sealed class CompositeRoc
        {
            /// <summary>Mean of <see cref="AurocByFold"/>.</summary>
            public double Auroc { get; set; }

            /// <summary>Per fold AUROC.</summary>
            public double[] AurocByFold { get; set; }

            /// <summary>Fold-averaged TPR at each point of <see cref="RocFpr"/>.</summary>
            public double[] Tpr { get; set; }
        }

        /// <summary>Correlated features and their joint permutation importance.</summary>
        public sealed class FeatureGroup
        {
            /// <summary>Canonical feature indices, ascending.</summary>
            public int[] Members { get; set; }

            /// <summary>The weakest absolute correlation between any two members.</summary>
            public double MinAbsCorrelation { get; set; }

            /// <summary>Held-out targets passing with every member permuted together (one
            /// permutation of rows, so the members keep their correlation with each other).</summary>
            public int PermutationPassing { get; set; }

            /// <summary>Fractional drop from the baseline.</summary>
            public double PermutationDrop { get; set; }
        }

        /// <summary>
        /// Measure the trained folds on the training subset.
        /// </summary>
        /// <param name="features">The standardized training subset.</param>
        /// <param name="isDecoy">Per subset row, true for a decoy.</param>
        /// <param name="entryIds">Per subset row, the id target-decoy competition pairs on.</param>
        /// <param name="foldAssignments">Per subset row, the fold it is held out of.</param>
        /// <param name="trainingSets">Per fold, the final training set (subset row indices).</param>
        /// <param name="foldScorers">Per fold, the model's score of one standardized row.</param>
        /// <param name="foldIterations">Per fold, the iteration of the returned model.</param>
        /// <param name="trainFdr">The FDR the training sets were selected at.</param>
        /// <param name="seed">Seed of the permutations.</param>
        /// <param name="nThreads">Worker threads.</param>
        /// <param name="permutationRowCap">Most held-out rows per fold the permutations re-score; a
        /// larger fold is thinned by target-decoy pair (<see cref="TREE_PERMUTATION_ROW_CAP"/> for
        /// trees, whose per-row score is costly; unbounded for the linear model).</param>
        /// <returns>The analysis, or null when a fold has no training set.</returns>
        public static TrainingSetAnalysis Compute(Matrix features, bool[] isDecoy, uint[] entryIds,
            int[] foldAssignments, FoldTrainingSet[] trainingSets, Func<double[], double>[] foldScorers,
            int[] foldIterations, double trainFdr, ulong seed, int nThreads, int permutationRowCap = int.MaxValue)
        {
            int nFolds = trainingSets.Length;
            if (trainingSets.Any(s => s == null || s.Positives.Length == 0 || s.Decoys.Length == 0))
                return null;
            int p = features.Cols;
            int threads = Math.Max(1, nThreads);
            var fprGrid = FeatureSeparation.LogFprGrid(ROC_DECADES, ROC_POINTS_PER_DECADE);
            var analysis = new TrainingSetAnalysis
            {
                NFolds = nFolds,
                TrainFdr = trainFdr,
                SubsetDecoys = isDecoy.Count(d => d),
                FoldPositives = trainingSets.Select(s => s.Positives.Length).ToArray(),
                FoldDecoys = trainingSets.Select(s => s.Decoys.Length).ToArray(),
                FoldIterations = (int[])foldIterations.Clone(),
                RocFpr = fprGrid,
                CorrelationThreshold = CORRELATION_GROUP_THRESHOLD,
            };
            analysis.SubsetTargets = isDecoy.Length - analysis.SubsetDecoys;

            var stats = new FeatureStats[p];
            OspreyParallel.For(0, p, threads, j => stats[j] = MeasureFeature(features, j, trainingSets, fprGrid));
            analysis.Features = stats;
            analysis.Correlation = FeatureSeparation.Correlation(features, threads);
            var groups = FeatureSeparation.GroupByCorrelation(analysis.Correlation, CORRELATION_GROUP_THRESHOLD);

            var heldOut = new HeldOutFold[nFolds];
            var permutationFolds = new HeldOutFold[nFolds];
            for (int f = 0; f < nFolds; f++)
            {
                var rows = Enumerable.Range(0, foldAssignments.Length).Where(i => foldAssignments[i] == f).ToArray();
                heldOut[f] = new HeldOutFold(features, isDecoy, entryIds, rows, foldScorers[f]);
                permutationFolds[f] = rows.Length <= permutationRowCap
                    ? heldOut[f]
                    : new HeldOutFold(features, isDecoy, entryIds, ThinByPair(rows, entryIds, permutationRowCap), foldScorers[f]);
            }
            analysis.TrainingComposite = MeasureComposite(fprGrid, f => ScoreRows(features, trainingSets[f].Positives, foldScorers[f]),
                f => ScoreRows(features, trainingSets[f].Decoys, foldScorers[f]), nFolds);
            analysis.InSampleComposite = MeasureComposite(fprGrid,
                f => ScoreRows(features, trainingSets[f].FitRows.Where(i => !isDecoy[i]).ToArray(), foldScorers[f]),
                f => ScoreRows(features, trainingSets[f].FitRows.Where(i => isDecoy[i]).ToArray(), foldScorers[f]), nFolds);
            analysis.HeldOutComposite = MeasureComposite(fprGrid, f => heldOut[f].ClassScores(false),
                f => heldOut[f].ClassScores(true), nFolds);

            // Permutation importance: one task per feature, then one per correlated group. Each
            // task seeds its own generator from its index, so the result does not depend on
            // which thread ran it.
            analysis.PermutationRows = permutationFolds.Sum(h => h.Scores.Length);
            analysis.PermutationBaselinePassing = permutationFolds.Sum(h => h.CountPassing(h.Scores, trainFdr));
            var permutationSets = Enumerable.Range(0, p).Select(j => new[] { j }).Concat(groups).ToArray();
            var permuted = new int[permutationSets.Length];
            OspreyParallel.For(0, permutationSets.Length, threads, k =>
            {
                int passing = 0;
                for (int f = 0; f < nFolds; f++)
                {
                    var random = new Random(unchecked((int)seed + 7919 * k + f));
                    var fold = permutationFolds[f];
                    passing += fold.CountPassing(fold.ScorePermuted(permutationSets[k], random), trainFdr);
                }
                permuted[k] = passing;
            });
            int baseline = analysis.PermutationBaselinePassing;
            for (int j = 0; j < p; j++)
            {
                stats[j].PermutationPassing = permuted[j];
                stats[j].PermutationDrop = FeatureSeparation.PermutationDrop(baseline, permuted[j]);
            }
            analysis.Groups = new FeatureGroup[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                analysis.Groups[g] = new FeatureGroup
                {
                    Members = groups[g],
                    MinAbsCorrelation = WeakestCorrelation(analysis.Correlation, groups[g]),
                    PermutationPassing = permuted[p + g],
                    PermutationDrop = FeatureSeparation.PermutationDrop(baseline, permuted[p + g])
                };
            }
            return analysis;
        }

        /// <summary>One feature's separation statistics on the pooled and per-fold training sets.</summary>
        private static FeatureStats MeasureFeature(Matrix features, int j, FoldTrainingSet[] trainingSets, double[] fprGrid)
        {
            var foldAurocs = new double[trainingSets.Length];
            var pooledPositives = new List<double>();
            var pooledDecoys = new List<double>();
            for (int f = 0; f < trainingSets.Length; f++)
            {
                var positives = Column(features, trainingSets[f].Positives, j);
                var decoys = Column(features, trainingSets[f].Decoys, j);
                pooledPositives.AddRange(positives);
                pooledDecoys.AddRange(decoys);
                foldAurocs[f] = FeatureSeparation.Auroc(positives, decoys);
            }
            var pos = pooledPositives.ToArray();
            var neg = pooledDecoys.ToArray();
            var stats = new FeatureStats
            {
                AurocFoldMin = foldAurocs.Min(),
                AurocFoldMax = foldAurocs.Max(),
                CohenD = FeatureSeparation.CohenD(pos, neg),
                TrainTargetHist = Histogram(pos),
                TrainDecoyHist = Histogram(neg)
            };
            Array.Sort(pos); // Array.Sort OK: plain doubles, equal values are indistinguishable
            Array.Sort(neg); // Array.Sort OK: plain doubles, equal values are indistinguishable
            stats.Auroc = FeatureSeparation.AurocSorted(pos, neg);
            stats.RocTpr = FeatureSeparation.RocTprSorted(pos, neg, fprGrid);
            stats.TprAtReferenceFpr = FeatureSeparation.RocTprSorted(pos, neg, new[] { REFERENCE_FPR })[0];
            return stats;
        }

        /// <summary>Per-fold ROC of a score, averaged vertically over folds.</summary>
        private static CompositeRoc MeasureComposite(double[] fprGrid, Func<int, double[]> positiveScores,
            Func<int, double[]> decoyScores, int nFolds)
        {
            var aurocs = new double[nFolds];
            var tpr = new double[fprGrid.Length];
            for (int f = 0; f < nFolds; f++)
            {
                var pos = positiveScores(f);
                var neg = decoyScores(f);
                Array.Sort(pos); // Array.Sort OK: plain doubles, equal values are indistinguishable
                Array.Sort(neg); // Array.Sort OK: plain doubles, equal values are indistinguishable
                aurocs[f] = FeatureSeparation.AurocSorted(pos, neg);
                var foldTpr = FeatureSeparation.RocTprSorted(pos, neg, fprGrid);
                for (int i = 0; i < tpr.Length; i++)
                    tpr[i] += foldTpr[i] / nFolds;
            }
            return new CompositeRoc { Auroc = aurocs.Average(), AurocByFold = aurocs, Tpr = tpr };
        }

        /// <summary>Column <paramref name="j"/> of <paramref name="rows"/>, NaN dropped (a NaN would
        /// sort first and stall the AUROC merge).</summary>
        private static double[] Column(Matrix features, int[] rows, int j)
        {
            return rows.Select(i => features[i, j]).Where(v => !double.IsNaN(v)).ToArray();
        }

        private static int[] Histogram(double[] values)
        {
            var counts = new int[FeatureContributions.HistBinCount];
            foreach (double v in values)
            {
                if (!double.IsNaN(v))
                    counts[FeatureContributions.HistBin(v)]++;
            }
            return counts;
        }

        private static double[] ScoreRows(Matrix features, int[] rows, Func<double[], double> scorer)
        {
            var scores = new double[rows.Length];
            var buffer = new double[features.Cols];
            for (int i = 0; i < rows.Length; i++)
            {
                MatrixRows.CopyRow(features, rows[i], buffer);
                scores[i] = scorer(buffer);
            }
            return scores;
        }

        /// <summary>
        /// About <paramref name="cap"/> of <paramref name="rows"/>, keeping or dropping each target
        /// with its paired decoy (their ids differ only in the decoy bit), so competition still
        /// sees whole pairs. Deterministic: the choice depends on the ids alone.
        /// </summary>
        private static int[] ThinByPair(int[] rows, uint[] entryIds, int cap)
        {
            int stride = (rows.Length + cap - 1) / cap;
            return rows.Where(i => (entryIds[i] & 0x7FFFFFFF) % stride == 0).ToArray();
        }

        private static double WeakestCorrelation(double[][] correlation, int[] members)
        {
            double min = 1.0;
            for (int a = 0; a < members.Length; a++)
            {
                for (int b = a + 1; b < members.Length; b++)
                    min = Math.Min(min, Math.Abs(correlation[members[a]][members[b]]));
            }
            return min;
        }

        /// <summary>
        /// One fold's held-out rows and their score under the fold's model, which the
        /// permutation tasks re-score with columns shuffled.
        /// </summary>
        private sealed class HeldOutFold
        {
            private readonly Matrix _features;
            private readonly int[] _rows;
            private readonly bool[] _isDecoy;
            private readonly uint[] _entryIds;
            private readonly Func<double[], double> _scorer;

            public HeldOutFold(Matrix features, bool[] isDecoy, uint[] entryIds, int[] rows,
                Func<double[], double> scorer)
            {
                _features = features;
                _scorer = scorer;
                _rows = rows;
                _isDecoy = _rows.Select(i => isDecoy[i]).ToArray();
                _entryIds = _rows.Select(i => entryIds[i]).ToArray();
                Scores = ScoreRows(features, _rows, scorer);
            }

            public double[] Scores { get; }

            public double[] ClassScores(bool decoys)
            {
                return Scores.Where((s, i) => _isDecoy[i] == decoys).ToArray();
            }

            public int CountPassing(double[] scores, double trainFdr)
            {
                return PercolatorQValues.CountPassing(scores, _isDecoy, _entryIds, trainFdr);
            }

            /// <summary>Re-score with the columns in <paramref name="columns"/> taken, together,
            /// from a random permutation of the rows.</summary>
            public double[] ScorePermuted(int[] columns, Random random)
            {
                int n = _rows.Length;
                var order = new int[n];
                for (int i = 0; i < n; i++)
                    order[i] = i;
                for (int i = n - 1; i > 0; i--)
                {
                    int k = random.Next(i + 1);
                    (order[i], order[k]) = (order[k], order[i]);
                }
                var scores = new double[n];
                var buffer = new double[_features.Cols];
                for (int i = 0; i < n; i++)
                {
                    MatrixRows.CopyRow(_features, _rows[i], buffer);
                    foreach (int c in columns)
                        buffer[c] = _features[_rows[order[i]], c];
                    scores[i] = _scorer(buffer);
                }
                return scores;
            }
        }
    }

    /// <summary>
    /// The rows one fold's returned model was trained on, as indices into the training subset.
    /// </summary>
    public sealed class FoldTrainingSet
    {
        /// <param name="positives">The selected confident targets.</param>
        /// <param name="decoys">The decoys.</param>
        /// <param name="fitRows">Every row the fold's model was fit on, both classes: the fold's
        /// training rows for the SVM, the inner fit rows (not the iteration-selection rows) for trees.</param>
        public FoldTrainingSet(int[] positives, int[] decoys, int[] fitRows)
        {
            Positives = positives;
            Decoys = decoys;
            FitRows = fitRows;
        }

        /// <summary>Every row the fold's model was fit on, both classes (the in-sample population).</summary>
        public int[] FitRows { get; }

        /// <summary>The selected confident targets.</summary>
        public int[] Positives { get; }

        /// <summary>The decoys.</summary>
        public int[] Decoys { get; }
    }

    /// <summary>
    /// The univariate separation and permutation arithmetic behind <see cref="TrainingSetAnalysis"/>,
    /// kept free of the trainer so each metric can be checked on a hand-built set.
    /// </summary>
    public static class FeatureSeparation
    {
        /// <summary>
        /// AUROC: the probability that a positive scores above a negative, ties counted half
        /// (the Mann-Whitney U statistic over <c>nPos * nNeg</c>). NaN when either class is empty.
        /// </summary>
        public static double Auroc(double[] positives, double[] negatives)
        {
            var pos = (double[])positives.Clone();
            var neg = (double[])negatives.Clone();
            Array.Sort(pos); // Array.Sort OK: plain doubles, equal values are indistinguishable
            Array.Sort(neg); // Array.Sort OK: plain doubles, equal values are indistinguishable
            return AurocSorted(pos, neg);
        }

        /// <summary><see cref="Auroc"/> of two ascending-sorted arrays, in one merge pass.</summary>
        public static double AurocSorted(double[] positivesAscending, double[] negativesAscending)
        {
            int nPos = positivesAscending.Length, nNeg = negativesAscending.Length;
            if (nPos == 0 || nNeg == 0)
                return double.NaN;
            double wins = 0;
            int below = 0, notAbove = 0;
            foreach (double v in positivesAscending)
            {
                while (below < nNeg && negativesAscending[below] < v)
                    below++;
                if (notAbove < below)
                    notAbove = below;
                while (notAbove < nNeg && negativesAscending[notAbove] <= v)
                    notAbove++;
                wins += below + 0.5 * (notAbove - below);
            }
            return wins / ((double)nPos * nNeg);
        }

        /// <summary>
        /// Cohen's d: <c>(mean_pos - mean_neg) / s_pooled</c>, with
        /// <c>s_pooled^2 = ((n_pos - 1) s_pos^2 + (n_neg - 1) s_neg^2) / (n_pos + n_neg - 2)</c>.
        /// NaN when a class has fewer than two values or the pooled deviation is zero.
        /// </summary>
        public static double CohenD(double[] positives, double[] negatives)
        {
            int nPos = positives.Length, nNeg = negatives.Length;
            if (nPos < 2 || nNeg < 2)
                return double.NaN;
            double meanPos = positives.Average(), meanNeg = negatives.Average();
            double ssPos = positives.Sum(v => (v - meanPos) * (v - meanPos));
            double ssNeg = negatives.Sum(v => (v - meanNeg) * (v - meanNeg));
            double pooled = Math.Sqrt((ssPos + ssNeg) / (nPos + nNeg - 2));
            return pooled > 0 ? (meanPos - meanNeg) / pooled : double.NaN;
        }

        /// <summary>An ascending log-spaced FPR grid from 10^-decades to 1.</summary>
        public static double[] LogFprGrid(int decades, int pointsPerDecade)
        {
            int n = decades * pointsPerDecade + 1;
            var grid = new double[n];
            for (int i = 0; i < n; i++)
                grid[i] = Math.Pow(10.0, -decades + (double)i / pointsPerDecade);
            return grid;
        }

        /// <summary>
        /// The ROC curve read at each requested FPR: the threshold is the negative score with
        /// <c>floor(fpr * nNeg)</c> negatives ranked above it, and the TPR is
        /// the fraction of positives strictly above that threshold. So the realized FPR never
        /// exceeds the one asked for. Both arrays ascending.
        /// </summary>
        public static double[] RocTprSorted(double[] positivesAscending, double[] negativesAscending, double[] fprGrid)
        {
            int nPos = positivesAscending.Length, nNeg = negativesAscending.Length;
            var tpr = new double[fprGrid.Length];
            if (nPos == 0 || nNeg == 0)
            {
                for (int i = 0; i < tpr.Length; i++)
                    tpr[i] = double.NaN;
                return tpr;
            }
            for (int i = 0; i < fprGrid.Length; i++)
            {
                int allowed = (int)Math.Floor(fprGrid[i] * nNeg);
                if (allowed >= nNeg)
                {
                    tpr[i] = 1.0;
                    continue;
                }
                double threshold = negativesAscending[nNeg - 1 - allowed];
                tpr[i] = (double)(nPos - UpperBound(positivesAscending, threshold)) / nPos;
            }
            return tpr;
        }

        /// <summary>
        /// The fractional loss of passing targets when a feature is permuted:
        /// <c>(baseline - permuted) / baseline</c>. Negative when permuting helped. NaN with no
        /// baseline.
        /// </summary>
        public static double PermutationDrop(int baseline, int permuted)
        {
            return baseline > 0 ? (double)(baseline - permuted) / baseline : double.NaN;
        }

        /// <summary>
        /// Pearson correlation of every pair of columns, as a symmetric matrix with a unit
        /// diagonal. NaN for a pair involving a constant column.
        /// </summary>
        public static double[][] Correlation(Matrix features, int nThreads)
        {
            int n = features.Rows, p = features.Cols;
            var means = new double[p];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < p; j++)
                    means[j] += features[i, j];
            }
            for (int j = 0; j < p; j++)
                means[j] /= Math.Max(1, n);
            var cov = new double[p][];
            OspreyParallel.For(0, p, Math.Max(1, nThreads), a =>
            {
                var row = new double[p];
                for (int i = 0; i < n; i++)
                {
                    double da = features[i, a] - means[a];
                    for (int b = a; b < p; b++)
                        row[b] += da * (features[i, b] - means[b]);
                }
                cov[a] = row;
            });
            var corr = new double[p][];
            for (int a = 0; a < p; a++)
                corr[a] = new double[p];
            for (int a = 0; a < p; a++)
            {
                for (int b = a; b < p; b++)
                {
                    double denom = Math.Sqrt(cov[a][a] * cov[b][b]);
                    double r = a == b ? 1.0 : denom > 0 ? cov[a][b] / denom : double.NaN;
                    corr[a][b] = r;
                    corr[b][a] = r;
                }
            }
            return corr;
        }

        /// <summary>
        /// Single-linkage groups of the columns whose absolute correlation reaches
        /// <paramref name="threshold"/> (NaN never links). Only groups of two or more are
        /// returned, each ascending, ordered by smallest member.
        /// </summary>
        public static int[][] GroupByCorrelation(double[][] correlation, double threshold)
        {
            int p = correlation.Length;
            var parent = Enumerable.Range(0, p).ToArray();
            int Find(int x)
            {
                while (parent[x] != x)
                    x = parent[x] = parent[parent[x]];
                return x;
            }
            for (int a = 0; a < p; a++)
            {
                for (int b = a + 1; b < p; b++)
                {
                    if (Math.Abs(correlation[a][b]) >= threshold)
                        parent[Find(b)] = Find(a);
                }
            }
            return Enumerable.Range(0, p).GroupBy(Find)
                .Select(g => g.OrderBy(j => j).ToArray())
                .Where(g => g.Length > 1)
                .OrderBy(g => g[0])
                .ToArray();
        }

        /// <summary>Index of the first element greater than <paramref name="value"/>.</summary>
        private static int UpperBound(double[] ascending, double value)
        {
            int lo = 0, hi = ascending.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (ascending[mid] <= value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
