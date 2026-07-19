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

using System.Collections.Generic;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// A trained <see cref="PercolatorResults"/> model reduced to the one thing the
    /// 2nd-pass transfer paths need from it: raw feature vector in, score out.
    ///
    /// This exists so the Tasks-layer frozen-model paths (OSPREY_PASS2_QVALUE=transfer
    /// and =transfer-compete) do not have to know WHICH classifier the 1st pass trained.
    /// They previously reached into <see cref="PercolatorResults.FoldWeights"/>, averaged
    /// the fold weights themselves, and inlined a dot product -- a third copy of the
    /// averaged-model math that silently assumed a linear model. With
    /// <c>--fdr-method fasttree</c> that assumption fails closed in the worst possible
    /// way: the weight list is empty, the transfer declines, and the run falls back to
    /// the anti-conservative 2nd-pass retrain -- the exact behavior transfer-compete was
    /// written to fix, reintroduced silently under a different flag.
    ///
    /// Scoring routes through the same shared row-scoring code the full-population
    /// passes use (<see cref="PercolatorFdr.ScoreStandardizedRow"/>), so the linear and
    /// tree models are applied identically no matter which pass applies them.
    ///
    /// NOT thread-safe: <see cref="Score"/> reuses one standardization buffer to avoid a
    /// per-entry allocation in the score loops. Every caller is serial by design -- the
    /// score passes are deliberately single-threaded so float accumulation order stays
    /// deterministic for cross-impl parity.
    /// </summary>
    public sealed class FrozenModelScorer
    {
        private readonly IReadOnlyList<GradientBoostedTrees> _gbtModels;
        private readonly double[] _avgWeights;
        private readonly double _avgBias;
        private readonly FeatureStandardizer _standardizer;
        private readonly double[] _scratch;

        private FrozenModelScorer(
            IReadOnlyList<GradientBoostedTrees> gbtModels,
            double[] avgWeights, double avgBias,
            FeatureStandardizer standardizer, int numFeatures)
        {
            _gbtModels = gbtModels;
            _avgWeights = avgWeights;
            _avgBias = avgBias;
            _standardizer = standardizer;
            _scratch = new double[numFeatures];
            NumFeatures = numFeatures;
        }

        /// <summary>Feature-vector width this model was trained on. Callers skip entries
        /// whose vector does not match.</summary>
        public int NumFeatures { get; }

        /// <summary>True when the frozen model is a tree ensemble
        /// (<c>--fdr-method fasttree</c>) rather than the linear SVM. Reporting only --
        /// <see cref="Score"/> already handles both.</summary>
        public bool IsGradientBoostedTrees { get { return _gbtModels != null; } }

        /// <summary>
        /// Reduce a trained model to a scorer, or return <c>null</c> when it carries no
        /// usable model (no standardizer, or neither fold weights nor tree ensembles) --
        /// the signal for the caller to log and fall back. Returning null rather than
        /// throwing preserves the existing fall-back-to-retrain contract at every
        /// transfer site.
        /// </summary>
        public static FrozenModelScorer TryCreate(PercolatorResults model)
        {
            if (model?.Standardizer == null)
                return null;

            var gbtModels = model.FoldGbtModels != null && model.FoldGbtModels.Count > 0
                ? model.FoldGbtModels
                : null;
            if (gbtModels != null)
            {
                // Trees expose no weight vector to measure, so the width comes from the
                // standardizer that was fit on the same training matrix.
                return new FrozenModelScorer(
                    gbtModels, null, 0.0, model.Standardizer, model.Standardizer.NumFeatures);
            }

            if (model.FoldWeights == null || model.FoldWeights.Count == 0)
                return null;

            // Average the fold weights + biases into a single linear model -- the same
            // averaged-model math PercolatorFdr applies before scoring a population.
            int nModels = model.FoldWeights.Count;
            int nFeatures = model.FoldWeights[0].Length;
            var avgWeights = new double[nFeatures];
            double avgBias = 0.0;
            for (int f = 0; f < nModels; f++)
            {
                double[] foldW = model.FoldWeights[f];
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] += foldW[j];
                avgBias += model.FoldBiases[f];
            }
            for (int j = 0; j < nFeatures; j++)
                avgWeights[j] /= nModels;
            avgBias /= nModels;

            return new FrozenModelScorer(null, avgWeights, avgBias, model.Standardizer, nFeatures);
        }

        /// <summary>
        /// Score one RAW (un-standardized) feature vector. Standardizes a copy into the
        /// internal buffer -- <paramref name="rawFeatures"/> is not mutated -- and applies
        /// the frozen model. Identical per-entry math to the full-population score passes,
        /// so a transferred score is on the same scale as a 1st-pass score by construction.
        /// </summary>
        public double Score(double[] rawFeatures)
        {
            System.Array.Copy(rawFeatures, 0, _scratch, 0, rawFeatures.Length);
            _standardizer.TransformSlice(_scratch);
            return PercolatorFdr.ScoreStandardizedRow(_gbtModels, _avgWeights, _avgBias, _scratch);
        }
    }
}
