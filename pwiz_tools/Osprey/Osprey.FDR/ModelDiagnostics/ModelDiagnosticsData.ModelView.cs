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

using System.Collections.Generic;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>
        /// The pass-1 report's model views - the Model tab's feature rows, the per-feature
        /// histogram edges, the composite - as reduced from the trained first-pass model's
        /// <see cref="FeatureContributions"/>. Those exist only in first-pass Percolator's memory,
        /// so every run saves this beside its model, with or without <c>--model-diagnostics</c>,
        /// and a report built by a later process reads it back (doc 00, P16).
        /// </summary>
        public sealed class ModelView
        {
            public int FeatureCount { get; set; }
            public double ModelComposite { get; set; }
            public bool ModelDegenerate { get; set; }
            public bool ModelIsTreeEnsemble { get; set; }
            public double[] FeatureHistEdges { get; set; }
            public List<FeatureRow> Model { get; set; }

            /// <summary>
            /// The views of <paramref name="contributions"/>, or null when no model was trained.
            /// </summary>
            public static ModelView From(FeatureContributions contributions)
            {
                if (contributions == null)
                    return null;
                return new ModelView
                {
                    FeatureCount = contributions.Features.Count,
                    ModelComposite = contributions.Composite,
                    ModelDegenerate = contributions.IsDegenerate,
                    ModelIsTreeEnsemble = contributions.IsTreeEnsemble,
                    FeatureHistEdges = contributions.HistogramEdges,
                    Model = BuildFeatureRows(contributions),
                };
            }

            /// <summary>
            /// Set <paramref name="data"/>'s model views from <paramref name="view"/>; a null view
            /// leaves the empty Model tab of a run that trained no model.
            /// </summary>
            internal static void ApplyTo(ModelView view, ModelDiagnosticsData data)
            {
                data.FeatureCount = view?.FeatureCount ?? 0;
                if (view == null)
                    return;
                data.ModelComposite = view.ModelComposite;
                data.ModelDegenerate = view.ModelDegenerate;
                data.ModelIsTreeEnsemble = view.ModelIsTreeEnsemble;
                data.FeatureHistEdges = view.FeatureHistEdges;
                data.Model = view.Model ?? new List<FeatureRow>();
            }
        }
    }
}
