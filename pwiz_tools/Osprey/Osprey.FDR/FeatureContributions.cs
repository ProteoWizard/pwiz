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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Skyline-style per-feature percent-contribution decomposition of a trained
    /// linear model. Ports TargetDecoyGenerator.GetPercentContribution: for each
    /// feature
    ///   contribution_j = w_j*(meanTarget_j - meanDecoy_j) / sum_k w_k*(meanTarget_k - meanDecoy_k)
    /// so the percentages sum to 100% by linearity (the denominator is the sum of
    /// the numerators). All means are over the STANDARDIZED feature space the SVM
    /// scores in (the same space the average weights live in), so the product
    /// w_j*deltaMu_j is space-invariant.
    ///
    /// This is the pure calculation: a read of the averaged weights and the
    /// accumulated target/decoy feature sums. It performs no I/O and computes no
    /// presentation order; the presentation layer (PercolatorFdr.EmitFeatureContributions)
    /// owns the heading, the column formatting, and the most-influential-first sort.
    /// </summary>
    public sealed class FeatureContributions
    {
        /// <summary>
        /// The contribution decomposition for a single feature of the trained
        /// linear model, in standardized feature space.
        /// </summary>
        public sealed class FeatureContribution
        {
            /// <summary>Zero-based feature index (canonical feature order).</summary>
            public int Index { get; }

            /// <summary>Internal feature name, or <c>null</c> when none was supplied.</summary>
            public string Name { get; }

            /// <summary>
            /// Display label: the supplied label, else the name, else
            /// <c>feature_{index}</c>. Never null.
            /// </summary>
            public string Label { get; }

            /// <summary>Standardized averaged weight w_j (the trained coefficient).</summary>
            public double Coefficient { get; }

            /// <summary>
            /// Target-minus-decoy mean gap Delta-mu_j = mean_target - mean_decoy in
            /// standardized space.
            /// </summary>
            public double TargetDecoyMeanGap { get; }

            /// <summary>The weighted contribution w_j * Delta-mu_j.</summary>
            public double Weighted { get; }

            /// <summary>
            /// Percent of the composite this feature accounts for
            /// (100 * Weighted / Composite), or <see cref="double.NaN"/> when the
            /// composite is degenerate.
            /// </summary>
            public double Percent { get; }

            /// <summary>Whether the feature is declared a reversed (lower-is-better) score.</summary>
            public bool IsReversedScore { get; }

            /// <summary>
            /// Skyline's weight-sign test <c>IsReversedScore XOR (Coefficient &lt; 0)</c>:
            /// the trained weight disagrees with the declared score direction.
            /// </summary>
            public bool IsUnexpectedDirection { get; }

            /// <param name="index">Zero-based feature index.</param>
            /// <param name="name">Internal feature name, or null.</param>
            /// <param name="label">Display label (already resolved through the fallback).</param>
            /// <param name="coefficient">Standardized averaged weight w_j.</param>
            /// <param name="targetDecoyMeanGap">Delta-mu_j = mean_target - mean_decoy.</param>
            /// <param name="weighted">w_j * Delta-mu_j.</param>
            /// <param name="percent">Percent of composite, or NaN when degenerate.</param>
            /// <param name="isReversedScore">Whether the feature is a declared reversed score.</param>
            /// <param name="isUnexpectedDirection">IsReversedScore XOR (coefficient &lt; 0).</param>
            public FeatureContribution(int index, string name, string label,
                double coefficient, double targetDecoyMeanGap, double weighted,
                double percent, bool isReversedScore, bool isUnexpectedDirection)
            {
                Index = index;
                Name = name;
                Label = label;
                Coefficient = coefficient;
                TargetDecoyMeanGap = targetDecoyMeanGap;
                Weighted = weighted;
                Percent = percent;
                IsReversedScore = isReversedScore;
                IsUnexpectedDirection = isUnexpectedDirection;
            }
        }

        /// <summary>
        /// Streams the per-feature target/decoy sums over the standardized
        /// population the model scores, then averages the per-fold weight vectors
        /// and builds the <see cref="FeatureContributions"/>. Owns BOTH the summing
        /// and the fold-averaging so the caller no longer hand-rolls either; the
        /// only public path to a <see cref="FeatureContributions"/> from a trained
        /// model.
        /// </summary>
        public sealed class Accumulator
        {
            private readonly double[] _sumTarget;
            private readonly double[] _sumDecoy;
            private long _nTarget;
            private long _nDecoy;

            /// <param name="featureCount">Number of features per entry.</param>
            public Accumulator(int featureCount)
            {
                _sumTarget = new double[featureCount];
                _sumDecoy = new double[featureCount];
            }

            /// <summary>
            /// Accumulate one standardized feature vector (the streaming path's
            /// reused featureBuf).
            /// </summary>
            public void Add(double[] standardizedFeatures, bool isDecoy)
            {
                if (isDecoy)
                {
                    _nDecoy++;
                    for (int j = 0; j < _sumDecoy.Length; j++)
                        _sumDecoy[j] += standardizedFeatures[j];
                }
                else
                {
                    _nTarget++;
                    for (int j = 0; j < _sumTarget.Length; j++)
                        _sumTarget[j] += standardizedFeatures[j];
                }
            }

            /// <summary>
            /// Accumulate one row of a standardized feature matrix (the direct
            /// path's full stdFeatures matrix).
            /// </summary>
            public void Add(Matrix standardizedFeatures, int row, bool isDecoy)
            {
                if (isDecoy)
                {
                    _nDecoy++;
                    for (int j = 0; j < _sumDecoy.Length; j++)
                        _sumDecoy[j] += standardizedFeatures[row, j];
                }
                else
                {
                    _nTarget++;
                    for (int j = 0; j < _sumTarget.Length; j++)
                        _sumTarget[j] += standardizedFeatures[row, j];
                }
            }

            /// <summary>
            /// Average the per-fold weight vectors into the standardized averaged
            /// model (mean_f over the folds, matching the scoring path) and
            /// decompose it over the accumulated target/decoy sums.
            /// </summary>
            /// <param name="foldWeights">Per-fold standardized weight vectors.</param>
            /// <param name="featureInfos">Per-feature metadata (name / label / direction), or null.</param>
            public FeatureContributions Build(
                IReadOnlyList<double[]> foldWeights, OspreyFeatureInfo[] featureInfos)
            {
                int p = _sumTarget.Length;
                int nFolds = foldWeights.Count;
                var avgWeights = new double[p];
                for (int f = 0; f < nFolds; f++)
                {
                    double[] foldW = foldWeights[f];
                    for (int j = 0; j < p; j++)
                        avgWeights[j] += foldW[j];
                }
                double nFoldsD = nFolds;
                for (int j = 0; j < p; j++)
                    avgWeights[j] /= nFoldsD;
                return new FeatureContributions(avgWeights, _sumTarget, _sumDecoy,
                    _nTarget, _nDecoy, featureInfos);
            }
        }

        /// <summary>
        /// The per-feature contributions in canonical feature-index order (not
        /// sorted for display). The presentation layer applies its own ordering.
        /// </summary>
        public IReadOnlyList<FeatureContribution> Features { get; }

        /// <summary>
        /// The composite sum_j Weighted_j == the Delta-mu of the composite score
        /// (the bias cancels in the target-decoy difference). Targets should score
        /// above decoys, so this is expected to be positive.
        /// </summary>
        public double Composite { get; }

        /// <summary>
        /// Whether the composite is degenerate (<c>|Composite| &lt;= 1e-12</c>), in
        /// which case the per-feature percents are <see cref="double.NaN"/>.
        /// </summary>
        public bool IsDegenerate { get; }

        /// <summary>
        /// Decompose the trained linear model into per-feature contributions.
        /// </summary>
        /// <param name="avgWeights">Standardized averaged per-feature weights w_j.</param>
        /// <param name="sumTarget">Per-feature sum of standardized target values.</param>
        /// <param name="sumDecoy">Per-feature sum of standardized decoy values.</param>
        /// <param name="nTarget">Number of targets accumulated into <paramref name="sumTarget"/>.</param>
        /// <param name="nDecoy">Number of decoys accumulated into <paramref name="sumDecoy"/>.</param>
        /// <param name="featureInfos">
        /// Per-feature metadata (machine name, display label, reversed-score flag) in
        /// canonical index order, or null to suppress names + the direction flag.
        /// </param>
        /// <remarks>
        /// Internal: the public construction path from a trained model is
        /// <see cref="Accumulator.Build"/>, which owns the fold-averaging that
        /// produces <paramref name="avgWeights"/>. Tests construct directly to
        /// exercise the decomposition from a hand-set model.
        /// </remarks>
        internal FeatureContributions(
            double[] avgWeights,
            double[] sumTarget, double[] sumDecoy, long nTarget, long nDecoy,
            OspreyFeatureInfo[] featureInfos)
        {
            int p = avgWeights.Length;
            var weighted = new double[p];
            var deltaMu = new double[p];
            double composite = 0.0;
            for (int j = 0; j < p; j++)
            {
                double mt = nTarget > 0 ? sumTarget[j] / nTarget : 0.0;
                double md = nDecoy > 0 ? sumDecoy[j] / nDecoy : 0.0;
                deltaMu[j] = mt - md;
                weighted[j] = avgWeights[j] * deltaMu[j];
                composite += weighted[j];   // == sum_k w_k*deltaMu_k == deltaMu_composite
            }
            Composite = composite;
            // Targets should score above decoys, so composite > 0. Guard /0 for a
            // degenerate model (percentages become NaN, which the report shows plainly).
            bool degenerate = Math.Abs(composite) <= 1e-12;
            IsDegenerate = degenerate;

            var features = new FeatureContribution[p];
            for (int j = 0; j < p; j++)
            {
                double pct = degenerate ? double.NaN : 100.0 * weighted[j] / composite;
                bool haveInfo = featureInfos != null && j < featureInfos.Length;
                var info = haveInfo ? featureInfos[j] : default(OspreyFeatureInfo);
                bool reversed = haveInfo && info.IsReversedScore;
                bool wrongSign = haveInfo && (info.IsReversedScore ^ (avgWeights[j] < 0.0));
                string name = info.Name;
                string label = info.Label ?? info.Name ?? string.Format("feature_{0}", j);
                features[j] = new FeatureContribution(j, name, label,
                    avgWeights[j], deltaMu[j], weighted[j], pct, reversed, wrongSign);
            }
            Features = features;
        }

        /// <summary>
        /// The human-readable contribution table as a sequence of lines: a heading,
        /// a column header, then one row per feature sorted most-influential-first
        /// (|percent| descending, ties broken by feature index). Pure formatting --
        /// no I/O; the presentation layer writes these lines wherever it logs (so each
        /// line keeps its own log timestamp), and <see cref="ToString"/> joins them for
        /// debugger / human display.
        /// </summary>
        public IEnumerable<string> ToReportLines()
        {
            // Framed as a model sanity check, NOT feature importance: the percent is
            // each feature's share of the target-decoy separation (a mean-difference
            // decomposition), not a ranking of predictive value. A near-zero share on
            // a score expected to matter (e.g. library dot-product, RT difference), or
            // a large share weighted opposite its expected direction, is a flag to
            // investigate the library / calibration -- the analog of Skyline's mProphet
            // model view. The raw standardized coefficient is kept alongside for the
            // Compare-Peaks-style read of how the composite score was built.
            yield return "  Model sanity check -- feature share of target-decoy separation (trained linear model, coefficients standardized):";
            yield return string.Format("    {0,-36} {1,12} {2,9}", "feature", "coefficient", "share (%)");
            foreach (var f in Features
                .OrderByDescending(f => IsDegenerate ? 0.0 : Math.Abs(f.Percent))
                .ThenBy(f => f.Index))
            {
                yield return string.Format("    {0,-36} {1,12:F4} {2,8:F1}%{3}",
                    f.Label, f.Coefficient, f.Percent,
                    f.IsUnexpectedDirection ? "  (unexpected direction)" : string.Empty);
            }
        }

        /// <summary>
        /// The contribution table (<see cref="ToReportLines"/>) as a single multi-line
        /// string, so inspecting a <see cref="FeatureContributions"/> in a debugger shows
        /// the full decomposition.
        /// </summary>
        public override string ToString()
        {
            return string.Join(Environment.NewLine, ToReportLines());
        }
    }
}
