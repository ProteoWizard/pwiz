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
using System.Collections.Generic;
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideDistribution : SimpleChildCollection<DbPeptideDistribution, int, DbPeptideAmount>
    {
        private long peptideFileAnalysisId;
        public PeptideDistribution(Workspace workspace, DbPeptideDistribution peptideDistribution) : base(workspace, peptideDistribution)
        {
            peptideFileAnalysisId = peptideDistribution.PeptideFileAnalysis.Id.Value;
        }
        public PeptideDistribution(PeptideDistributions peptideDistributions, PeptideQuantity peptideQuantity) : base(peptideDistributions.Workspace)
        {
            peptideFileAnalysisId = peptideDistributions.Id.Value;
            PeptideQuantity = peptideQuantity;
            ChildCount = 0;
        }

        public PeptideDistributions PeptideDistributions { get
        {
            return (PeptideDistributions) Parent;
        }}
        public PeptideFileAnalysis PeptideFileAnalysis { get
        {
            return PeptideDistributions.PeptideFileAnalysis;
        }}

        protected override int GetChildCount(DbPeptideDistribution parent)
        {
            return parent.PeptideAmountCount;
        }

        protected override void SetChildCount(DbPeptideDistribution parent, int childCount)
        {
            parent.PeptideAmountCount = childCount;
        }

        protected override IEnumerable<KeyValuePair<int, DbPeptideAmount>> GetChildren(DbPeptideDistribution parent)
        {
            foreach (var peptideAmount in parent.PeptideAmounts)
            {
                yield return new KeyValuePair<int, DbPeptideAmount>(peptideAmount.EnrichmentIndex, peptideAmount);
            }
        }

        protected override void SetParent(DbPeptideAmount child, DbPeptideDistribution parent)
        {
            child.PeptideDistribution = parent;
        }

        protected override void Load(DbPeptideDistribution parent)
        {
            base.Load(parent);
            Score = parent.Score;
            PeptideQuantity = parent.PeptideQuantity;
        }

        protected override DbPeptideDistribution UpdateDbEntity(ISession session)
        {
            var result = base.UpdateDbEntity(session);
            result.Score = Score;
            result.AggregateValue = AverageEnrichmentValue;
            return result;
        }

        protected override DbPeptideDistribution ConstructEntity(ISession session)
        {
            return new DbPeptideDistribution
                       {
                           PeptideQuantity = PeptideQuantity,
                           PeptideFileAnalysis = session.Load<DbPeptideFileAnalysis>(peptideFileAnalysisId),
                       };
        }

        public double Score { get; set; }
        public double TotalAmount
        {
            get
            {
                double total = 0;
                foreach (var child in ListChildren())
                {
                    total += child.PercentAmount;
                }
                return total;
            }
        }
        public double AverageEnrichmentValue
        {
            get
            {
                double totalAmount = 0;
                double totalValue = 0;
                foreach (var child in ListChildren())
                {
                    totalAmount += child.PercentAmount;
                    totalValue += child.EnrichmentValue*child.PercentAmount;
                }
                return totalValue/totalAmount;
            }
        }
        public double Turnover
        {
            get
            {
                return 100*(1 - GetChild(0).PercentAmount/TotalAmount);
            }
        }
        public PeptideQuantity PeptideQuantity { get; private set; }
    }
}