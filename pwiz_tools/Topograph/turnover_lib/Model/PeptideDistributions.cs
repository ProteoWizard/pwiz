/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class PeptideDistributions : EntityModelCollection<DbPeptideFileAnalysis, PeptideQuantity, DbPeptideDistribution, PeptideDistribution>
    {
        public PeptideDistributions(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis)
            : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            Parent = peptideFileAnalysis;
        }

        public PeptideDistributions(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis.Workspace)
        {
            Parent = peptideFileAnalysis;
            SetId(peptideFileAnalysis.Id);
            IsDirty = true;
        }

        public bool IsDirty
        {
            get; set;
        }

        public void Calculate(Peaks peaks)
        {
            IList<double> observedIntensities;
            IDictionary<TracerPercentFormula, IList<double>> tracerPercentPredictedIntensities;
            var precursorEnrichment = ComputePrecursorEnrichments(peaks, out observedIntensities,
                                                                  out tracerPercentPredictedIntensities);
            if (precursorEnrichment != null)
            {
                AddChild(PeptideQuantity.precursor_enrichment, precursorEnrichment);
            }
            IDictionary<TracerFormula, IList<double>> tracerPredictedIntensities;
            var tracerCount = ComputeTracerAmounts(peaks, out observedIntensities, out tracerPredictedIntensities);
            if (tracerCount != null)
            {
                AddChild(PeptideQuantity.tracer_count, tracerCount);
            }
        }

        public PeptideDistribution ComputePrecursorEnrichments(
            Peaks peaks, out IList<double> observedIntensities, 
            out IDictionary<TracerPercentFormula, IList<double>> predictedIntensities)
        {
            if (peaks.ChildCount == 0)
            {
                observedIntensities = null;
                predictedIntensities = null;
                return null;
            }
            observedIntensities = peaks.GetAverageIntensities();
            var result = new PeptideDistribution(this, PeptideQuantity.precursor_enrichment) { Parent = this};
            PeptideFileAnalysis.TurnoverCalculator.GetEnrichmentAmounts(result, peaks.GetAverageIntensitiesExcludedAsNaN(), 
                PeptideFileAnalysis.PeptideAnalysis.IntermediateLevels, out predictedIntensities);
            return result;
        }

        public PeptideDistribution ComputeTracerAmounts(Peaks peaks, out IList<double> observedIntensities, 
            out IDictionary<TracerFormula, IList<double>> predictedIntensities)
        {
            if (peaks.GetChildCount() == 0)
            {
                observedIntensities = null;
                predictedIntensities = null;
                return null;
            }
            observedIntensities = peaks.GetAverageIntensities();
            var result = new PeptideDistribution(this, PeptideQuantity.tracer_count) { Parent = this };
            PeptideFileAnalysis.TurnoverCalculator.GetTracerAmounts(
                result, peaks.GetAverageIntensitiesExcludedAsNaN(), out predictedIntensities);
            if (result.ChildCount > 2)
            {
                double turnover;
                IDictionary<TracerFormula, double> bestMatch;
                result.PrecursorEnrichmentFormula = PeptideFileAnalysis.TurnoverCalculator.ComputePrecursorEnrichmentAndTurnover(result.ToDictionary(), out turnover, out bestMatch);
                if (result.PrecursorEnrichmentFormula != null)
                {
                    result.PrecursorEnrichment = result.PrecursorEnrichmentFormula.Values.Sum() / 100.0;
                }
                result.Turnover = turnover;
            }

            return result;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get { return (PeptideFileAnalysis) Parent;}}
        protected override int GetChildCount(DbPeptideFileAnalysis parent)
        {
            return parent.PeptideDistributionCount;
        }
        protected override void SetChildCount(DbPeptideFileAnalysis parent, int childCount)
        {
            parent.PeptideDistributionCount = childCount;
        }
        protected override IEnumerable<KeyValuePair<PeptideQuantity, DbPeptideDistribution>> GetChildren(DbPeptideFileAnalysis parent)
        {
            foreach (var peptideDistribution in parent.PeptideDistributions)
            {
                yield return new KeyValuePair<PeptideQuantity, DbPeptideDistribution>(peptideDistribution.PeptideQuantity, peptideDistribution);
            }
        }
        public override PeptideDistribution WrapChild(DbPeptideDistribution entity)
        {
            return new PeptideDistribution(Workspace, entity);
        }
    }
}
