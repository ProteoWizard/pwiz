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
using System.Threading;
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
    /// <c>OSPREY_FDR_MODEL=gbdt</c> that assumption fails closed in the worst possible
    /// way: the weight list is empty, the transfer declines, and the run falls back to
    /// the anti-conservative 2nd-pass retrain -- the exact behavior transfer-compete was
    /// written to fix, reintroduced silently under a different flag.
    ///
    /// Scoring routes through the same shared row-scoring code the full-population
    /// passes use (<see cref="PercolatorScorer.ScoreStandardizedRow"/>), so the linear and
    /// tree models are applied identically no matter which pass applies them.
    ///
    /// Thread-safe: <see cref="Score(double[])"/> reuses a standardization buffer to avoid a per-entry
    /// allocation in the score loops, one per thread. It once reused a single buffer on the
    /// claim that every caller was serial, but the Stage 6 per-file second-pass workers of a
    /// --parallel-files run share one scorer, so concurrent calls scored one file's entries
    /// with another's features. Each call's arithmetic is unchanged, so scores stay
    /// deterministic whatever the thread.
    /// </summary>
    public sealed class FrozenModelScorer
    {
        private readonly IReadOnlyList<GradientBoostedTrees> _gbtModels;
        private readonly double[] _avgWeights;
        private readonly double _avgBias;
        private readonly FeatureStandardizer _standardizer;
        private readonly ThreadLocal<double[]> _scratch;

        private FrozenModelScorer(
            IReadOnlyList<GradientBoostedTrees> gbtModels,
            double[] avgWeights, double avgBias,
            FeatureStandardizer standardizer, int numFeatures, bool scoresProteinBest)
        {
            _gbtModels = gbtModels;
            _avgWeights = avgWeights;
            _avgBias = avgBias;
            _standardizer = standardizer;
            _scratch = new ThreadLocal<double[]>(() => new double[numFeatures]);
            NumFeatures = numFeatures;
            ScoresProteinBest = scoresProteinBest;
        }

        /// <summary>Feature-vector width this model was trained on, the protein-best column
        /// included when it has one.</summary>
        public int NumFeatures { get; }

        /// <summary>
        /// True when the model was trained with the protein-best column
        /// (<c>OSPREY_PROTEIN_BEST</c>) appended to the feature vectors: its last feature name is
        /// <see cref="ProteinBestFeature.FEATURE_NAME"/>. Such a model scores a vector only through
        /// <see cref="Score(double[], double)"/>, which appends the column.
        /// </summary>
        public bool ScoresProteinBest { get; }

        /// <summary>The width of the raw feature vector a caller supplies: the trained width,
        /// less the protein-best column the scorer appends. Callers skip entries whose vector
        /// does not match.</summary>
        public int InputFeatures { get { return ScoresProteinBest ? NumFeatures - 1 : NumFeatures; } }

        /// <summary>True when the frozen model is a tree ensemble
        /// (<c>OSPREY_FDR_MODEL=gbdt</c>) rather than the linear SVM. Reporting only -
        /// <see cref="Score(double[])"/> already handles both.</summary>
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
            bool scoresProteinBest = ProteinBestFeature.IsAppendedTo(model.FeatureNames);

            var gbtModels = model.FoldGbtModels != null && model.FoldGbtModels.Count > 0
                ? model.FoldGbtModels
                : null;
            if (gbtModels != null)
            {
                // Trees expose no weight vector to measure, so the width comes from the
                // standardizer that was fit on the same training matrix.
                return new FrozenModelScorer(
                    gbtModels, null, 0.0, model.Standardizer, model.Standardizer.NumFeatures, scoresProteinBest);
            }

            if (model.FoldWeights == null || model.FoldWeights.Count == 0)
                return null;

            // Average the fold weights + biases into a single linear model -- the same
            // averaged-model math PercolatorScorer applies before scoring a population.
            int nFeatures = model.FoldWeights[0].Length;
            var avgWeights = PercolatorScorer.AverageFoldWeights(model.FoldWeights, model.FoldBiases, nFeatures,
                out double avgBias);

            return new FrozenModelScorer(null, avgWeights, avgBias, model.Standardizer, nFeatures, scoresProteinBest);
        }

        /// <summary>
        /// Score one RAW (un-standardized) feature vector. Standardizes a copy into the
        /// internal buffer -- <paramref name="rawFeatures"/> is not mutated -- and applies
        /// the frozen model. Identical per-entry math to the full-population score passes,
        /// so a transferred score is on the same scale as a 1st-pass score by construction.
        /// </summary>
        public double Score(double[] rawFeatures)
        {
            double[] scratch = _scratch.Value;
            System.Array.Copy(rawFeatures, 0, scratch, 0, rawFeatures.Length);
            _standardizer.TransformSlice(scratch);
            return PercolatorScorer.ScoreStandardizedRow(_gbtModels, _avgWeights, _avgBias, scratch);
        }

        /// <summary>
        /// Score one RAW feature vector of <see cref="InputFeatures"/> values with the
        /// protein-best column <paramref name="proteinBest"/> appended, for a model that
        /// <see cref="ScoresProteinBest"/>. The same arithmetic as <see cref="Score(double[])"/>
        /// over the extended vector, which is the vector the first pass scored the row with.
        /// </summary>
        public double Score(double[] rawFeatures, double proteinBest)
        {
            if (!ScoresProteinBest)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new System.InvalidOperationException(@"This model was not trained with the protein-best column.");
            }
            double[] scratch = _scratch.Value;
            System.Array.Copy(rawFeatures, 0, scratch, 0, InputFeatures);
            scratch[InputFeatures] = proteinBest;
            _standardizer.TransformSlice(scratch);
            return PercolatorScorer.ScoreStandardizedRow(_gbtModels, _avgWeights, _avgBias, scratch);
        }

        /// <summary>
        /// Score one raw feature vector of <see cref="InputFeatures"/> values: with
        /// <paramref name="proteinBest"/>'s value for <paramref name="entryId"/> appended when the
        /// model <see cref="ScoresProteinBest"/>, and exactly as <see cref="Score(double[])"/>
        /// otherwise. A model with the column refuses a null <paramref name="proteinBest"/>
        /// rather than scoring a vector without it.
        /// </summary>
        public double Score(double[] rawFeatures, uint entryId, ProteinBestRunValues proteinBest)
        {
            if (!ScoresProteinBest)
                return Score(rawFeatures);
            if (proteinBest == null)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new System.ArgumentNullException(nameof(proteinBest),
                    @"This model scores a vector only with the protein-best column of its run.");
            }
            return Score(rawFeatures, proteinBest.ValueFor(entryId));
        }
    }
}
