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

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>
        /// The pass-level half of a model's <see cref="TrainingSetAnalysis"/>, for the report: who
        /// the training set was, the composite ROC curves, the permutation baseline, and the
        /// feature correlation with its groups. The per-feature half rides on each
        /// <see cref="FeatureRow"/>.
        /// </summary>
        public sealed class TrainingSetView
        {
            public int NFolds { get; set; }
            public double TrainFdr { get; set; }
            public int SubsetTargets { get; set; }
            public int SubsetDecoys { get; set; }
            public int[] FoldPositives { get; set; }
            public int[] FoldDecoys { get; set; }
            public int[] FoldIterations { get; set; }

            /// <summary>The FPR <see cref="FeatureRow.TprAtReferenceFpr"/> is read at.</summary>
            public double ReferenceFpr { get; set; }

            /// <summary>The FPR grid of every curve (<see cref="FeatureRow.RocTpr"/> included).</summary>
            public double[] RocFpr { get; set; }

            public TrainingSetAnalysis.CompositeRoc TrainingComposite { get; set; }
            public TrainingSetAnalysis.CompositeRoc InSampleComposite { get; set; }
            public TrainingSetAnalysis.CompositeRoc HeldOutComposite { get; set; }

            public int PermutationBaselinePassing { get; set; }
            public int PermutationRows { get; set; }

            public double CorrelationThreshold { get; set; }

            /// <summary>Feature labels in canonical index order, the axes of <see cref="Correlation"/>.</summary>
            public string[] CorrelationLabels { get; set; }

            /// <summary>Pearson correlation over the training subset, canonical order.</summary>
            public double[][] Correlation { get; set; }

            public List<GroupRow> Groups { get; set; }

            /// <summary>
            /// The view of <paramref name="contributions"/>' training-set analysis, or null when
            /// it carries none.
            /// </summary>
            public static TrainingSetView From(FeatureContributions contributions)
            {
                var analysis = contributions?.TrainingAnalysis;
                if (analysis == null)
                    return null;
                var byIndex = contributions.Features.OrderBy(f => f.Index).ToArray();
                var labels = byIndex.Select(f => f.ReportLabel).ToArray();
                return new TrainingSetView
                {
                    NFolds = analysis.NFolds,
                    TrainFdr = analysis.TrainFdr,
                    SubsetTargets = analysis.SubsetTargets,
                    SubsetDecoys = analysis.SubsetDecoys,
                    FoldPositives = analysis.FoldPositives,
                    FoldDecoys = analysis.FoldDecoys,
                    FoldIterations = analysis.FoldIterations,
                    ReferenceFpr = TrainingSetAnalysis.REFERENCE_FPR,
                    RocFpr = analysis.RocFpr,
                    TrainingComposite = analysis.TrainingComposite,
                    InSampleComposite = analysis.InSampleComposite,
                    HeldOutComposite = analysis.HeldOutComposite,
                    PermutationBaselinePassing = analysis.PermutationBaselinePassing,
                    PermutationRows = analysis.PermutationRows,
                    CorrelationThreshold = analysis.CorrelationThreshold,
                    CorrelationLabels = labels,
                    Correlation = analysis.Correlation,
                    Groups = (analysis.Groups ?? Array.Empty<TrainingSetAnalysis.FeatureGroup>()).Select(g => new GroupRow
                    {
                        Members = g.Members,
                        Labels = g.Members.Select(j => labels[j]).ToArray(),
                        MinAbsCorrelation = g.MinAbsCorrelation,
                        // The members' shares of the composite separation summed: what the group
                        // carries together, which no single member's share shows when the SVM
                        // spreads weight across it. NaN when the shares are (a tree ensemble).
                        SharePercent = g.Members.Sum(j => byIndex[j].Percent),
                        PermutationDrop = g.PermutationDrop,
                    }).ToList(),
                };
            }

            /// <summary>Per canonical feature index, its group in <see cref="Groups"/> or -1; null
            /// when there is no analysis.</summary>
            internal static int[] GroupIndexByFeature(TrainingSetAnalysis analysis)
            {
                if (analysis?.Features == null)
                    return null;
                var groupOf = Enumerable.Repeat(-1, analysis.Features.Length).ToArray();
                for (int g = 0; analysis.Groups != null && g < analysis.Groups.Length; g++)
                {
                    foreach (int j in analysis.Groups[g].Members)
                    {
                        if (j < groupOf.Length)
                            groupOf[j] = g;
                    }
                }
                return groupOf;
            }
        }

        /// <summary>One group of correlated features.</summary>
        public sealed class GroupRow
        {
            public int[] Members { get; set; }
            public string[] Labels { get; set; }
            public double MinAbsCorrelation { get; set; }
            /// <summary>Sum of the members' percent contributions.</summary>
            public double SharePercent { get; set; }
            /// <summary>Fractional drop in held-out passing targets with every member permuted together.</summary>
            public double PermutationDrop { get; set; }
        }
    }
}
