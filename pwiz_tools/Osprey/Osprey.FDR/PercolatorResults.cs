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

using System.Collections.Generic;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Result for a single entry after Percolator scoring.
    /// </summary>
    public class PercolatorResult
    {
        /// <summary>SVM decision function score.</summary>
        public double Score { get; set; }

        /// <summary>Per-run precursor-level q-value.</summary>
        public double RunPrecursorQvalue { get; set; }

        /// <summary>Per-run peptide-level q-value.</summary>
        public double RunPeptideQvalue { get; set; }

        /// <summary>Experiment-wide precursor-level q-value.</summary>
        public double ExperimentPrecursorQvalue { get; set; }

        /// <summary>Experiment-wide peptide-level q-value.</summary>
        public double ExperimentPeptideQvalue { get; set; }

        /// <summary>Posterior error probability.</summary>
        public double Pep { get; set; }

        /// <summary>
        /// The per-entry score the experiment-scope competitions ranked this entry on
        /// (sidecar v4, issue #4522); see <see cref="Core.FdrEntry.ExperimentAggregateScore"/>.
        /// </summary>
        public double ExperimentAggregateScore { get; set; }

        public PercolatorResult()
        {
            RunPrecursorQvalue = 1.0;
            RunPeptideQvalue = 1.0;
            ExperimentPrecursorQvalue = 1.0;
            ExperimentPeptideQvalue = 1.0;
            Pep = 1.0;
        }
    }

    /// <summary>
    /// Full results from Percolator analysis.
    /// </summary>
    public class PercolatorResults
    {
        /// <summary>Per-entry results.</summary>
        public List<PercolatorResult> Entries { get; set; }

        /// <summary>Feature weights from best model per fold. Empty on the
        /// gradient-boosted-trees path, which has no linear weights -- see
        /// <see cref="FoldGbtModels"/>.</summary>
        public List<double[]> FoldWeights { get; set; }

        /// <summary>Bias terms from best model per fold. Empty on the
        /// gradient-boosted-trees path.</summary>
        public List<double> FoldBiases { get; set; }

        /// <summary>
        /// Best gradient-boosted-trees model per fold when the run was configured with
        /// <see cref="PercolatorConfig.UseGradientBoostedTrees"/>; <c>null</c> on the
        /// default SVM path. The two are mutually exclusive: whichever is populated
        /// selects how the score passes turn a standardized feature row into a score.
        ///
        /// The linear path averages the fold WEIGHTS and scores once; trees cannot be
        /// averaged that way, so the tree path averages the fold SCORES instead. For a
        /// linear model those are the same operation (a dot product is linear in the
        /// weights), which is why the SVM path keeps its cheaper weight-average form.
        /// </summary>
        public List<GradientBoostedTrees> FoldGbtModels { get; set; }

        /// <summary>
        /// The Skyline-style per-feature percent-contribution decomposition of the
        /// trained averaged model, or <c>null</c> when scoring did not run (e.g. the
        /// <c>TrainOnly</c> early-return or the empty-population shortcut). Carries
        /// the decomposition object the report is printed from; not used by scoring.
        /// </summary>
        public FeatureContributions FeatureContributions { get; set; }

        /// <summary>Feature standardizer used during training.</summary>
        public FeatureStandardizer Standardizer { get; set; }

        /// <summary>Number of iterations used per fold.</summary>
        public List<int> IterationsPerFold { get; set; }

        /// <summary>
        /// Set when <see cref="PercolatorTrainer.RunPercolator"/> wrote a
        /// diagnostic-only (<c>*Only</c>) dump and stopped early instead of
        /// completing FDR. The Tasks-layer caller inspects this and performs the
        /// process early-exit; the engine itself never exits. The other fields are
        /// unpopulated when this is <c>true</c>.
        /// </summary>
        public bool DiagnosticAbort { get; set; }

        public PercolatorResults()
        {
            Entries = new List<PercolatorResult>();
            FoldWeights = new List<double[]>();
            FoldBiases = new List<double>();
            IterationsPerFold = new List<int>();
        }
    }

}
