/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Configuration for the Percolator runner.
    /// </summary>
    public class PercolatorConfig
    {
        /// <summary>FDR threshold for selecting positive training set (default: 0.01).</summary>
        public double TrainFdr { get; set; }

        /// <summary>FDR threshold for final results (default: 0.01).</summary>
        public double TestFdr { get; set; }

        /// <summary>Maximum SVM training iterations per fold (default: 10).</summary>
        public int MaxIterations { get; set; }

        /// <summary>Number of cross-validation folds (default: 3).</summary>
        public int NFolds { get; set; }

        /// <summary>Random seed for reproducibility (default: 42).</summary>
        public ulong Seed { get; set; }

        /// <summary>Grid search C values for SVM cost parameter.</summary>
        public double[] CValues { get; set; }

        /// <summary>Maximum paired entries for SVM cross-validation (default: 300000).</summary>
        public int MaxTrainSize { get; set; }

        /// <summary>
        /// Optional per-feature metadata (machine name, display label, expected
        /// direction) in PIN-index order -- a single vector of
        /// <see cref="OspreyFeatureInfo"/> populated by the Tasks layer from the
        /// feature calculators (the FDR project does not reference the Scoring
        /// assembly that owns the calculator SPI). The names drive the Stage 5
        /// diagnostic dumps and the best-initial-feature log line; the labels +
        /// directions drive the post-training feature-contribution report. Null
        /// suppresses the names (logged as "unknown"/"feature_i") and the
        /// unexpected-direction flag. Never used for scoring.
        /// </summary>
        public OspreyFeatureInfo[] FeatureInfos { get; set; }

        /// <summary>
        /// If true, the feature-contribution accumulator also bins each feature's
        /// standardized value by class into per-feature target/decoy histograms
        /// (the <c>--model-diagnostics</c> per-feature distribution view). Off for
        /// the production scoring path so it adds no work. Never used for scoring.
        /// </summary>
        public bool CollectFeatureHistograms { get; set; }

        /// <summary>
        /// If true, <see cref="PercolatorTrainer.RunPercolator"/> trains the fold
        /// models + standardizer and returns early -- skips CV/averaged
        /// scoring of the input entries, PEP estimation, q-value
        /// computation, and per-file FDR logging. Used by the streaming
        /// path in <c>AnalysisPipeline.RunPercolatorStreaming</c>, where
        /// the caller pre-dedups + subsamples the entries for training,
        /// then invokes <see cref="PercolatorScorer.ScorePopulationAndComputeFdr"/>
        /// on the full per-file FdrEntry population. Mirrors Rust's
        /// <c>PercolatorConfig::train_only</c>.
        /// </summary>
        public bool TrainOnly { get; set; }

        /// <summary>
        /// Stage 5 diagnostic-dump gates, or <c>null</c> (the common case) when
        /// diagnostics are off. Carried in by the Tasks-layer caller so
        /// <see cref="PercolatorTrainer.RunPercolator"/> stays a pure function of its
        /// inputs -- it reads no env vars and never exits the process. See
        /// <see cref="PercolatorDiagnosticsConfig"/>.
        /// </summary>
        public PercolatorDiagnosticsConfig Diagnostics { get; set; }

        /// <summary>
        /// Train gradient-boosted decision trees (<c>--fdr-method gbdt</c>) instead
        /// of the linear SVM. Everything else about the run is unchanged: the same
        /// best-per-precursor dedup, the same peptide-grouped CV folds, the same
        /// semi-supervised positive-set iteration, and the same target-decoy
        /// competition / q-value / PEP math. Only the per-fold classifier and the
        /// full-population scoring function differ, so the two methods are directly
        /// comparable at matched entrapment FDP. See <see cref="GbtParams"/>.
        /// </summary>
        public bool UseGradientBoostedTrees { get; set; }

        /// <summary>
        /// Hyper-parameters for the <see cref="UseGradientBoostedTrees"/> classifier.
        /// Ignored on the default (SVM) path. Never null -- defaults to the validated
        /// conservative setting from <see cref="GbtParams"/>.
        /// </summary>
        public GbtParams GbtParams { get; set; }

        /// <summary>
        /// Worker threads for the gradient-boosted-trees full-population score pass
        /// (from <c>--threads</c>). Used ONLY there: the linear score pass stays serial
        /// because its feature-contribution accumulator is a running float sum whose
        /// order is byte-parity-locked, whereas a tree score is a pure per-row function
        /// that accumulates nothing. Defaults to the machine's processor count.
        /// </summary>
        public int NThreads { get; set; }

        public PercolatorConfig()
        {
            TrainFdr = 0.01;
            TestFdr = 0.01;
            MaxIterations = 10;
            NFolds = 3;
            Seed = 42;
            CValues = new[] { 0.001, 0.01, 0.1, 1.0, 10.0, 100.0 };
            MaxTrainSize = 300000;
            TrainOnly = false;
            UseGradientBoostedTrees = false;
            GbtParams = new GbtParams();
            NThreads = Environment.ProcessorCount;
        }

        /// <summary>
        /// A copy of this config with <see cref="TrainOnly"/> set: what the streaming
        /// paths hand to <see cref="PercolatorTrainer.RunPercolator"/> for the
        /// train-the-fold-models pass over the subsampled training set. Copies every
        /// knob that selects HOW a model is trained -- including the classifier choice
        /// and its hyper-parameters -- so the training pass cannot silently diverge from
        /// the scoring pass that consumes its output. (Both streaming paths previously
        /// hand-copied this field list, which meant a new training knob had to be added
        /// in two places or one path would quietly train the wrong model.)
        ///
        /// <see cref="CollectFeatureHistograms"/> is deliberately NOT carried: the
        /// histograms are accumulated by the score pass over the full population, not
        /// over the training subset.
        /// </summary>
        public PercolatorConfig CloneForTrainOnly()
        {
            return new PercolatorConfig
            {
                TrainFdr = TrainFdr,
                TestFdr = TestFdr,
                MaxIterations = MaxIterations,
                NFolds = NFolds,
                Seed = Seed,
                CValues = CValues,
                MaxTrainSize = MaxTrainSize,
                FeatureInfos = FeatureInfos,
                UseGradientBoostedTrees = UseGradientBoostedTrees,
                GbtParams = GbtParams,
                NThreads = NThreads,
                TrainOnly = true,
                Diagnostics = Diagnostics
            };
        }
    }

}
