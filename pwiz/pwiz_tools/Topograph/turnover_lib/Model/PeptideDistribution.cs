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
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class PeptideDistribution : SimpleChildCollection<DbPeptideDistribution, string, DbPeptideAmount>
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

        protected override IEnumerable<KeyValuePair<string, DbPeptideAmount>> GetChildren(DbPeptideDistribution parent)
        {
            foreach (var peptideAmount in parent.PeptideAmounts)
            {
                yield return new KeyValuePair<string, DbPeptideAmount>(peptideAmount.TracerFormula, peptideAmount);
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
            if (double.IsNaN(TracerPercent))
            {
                result.Score = 0;
                result.TracerPercent = 0;
            }
            else
            {
                result.Score = Score;
                result.TracerPercent = TracerPercent;
            }
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
        public double TotalPercentAmount
        {
            get
            {
                double total = 0;
                foreach (var child in ListChildren())
                {
                    total += child.PercentAmountValue;
                }
                return total;
            }
        }
        public double GetTracerPercent(TracerDef tracerDef)
        {
            int maxTracerCount = tracerDef.GetMaximumTracerCount(PeptideFileAnalysis.Peptide.Sequence);
            if (maxTracerCount == 0)
            {
                return 0;
            }
            double result = 0;
            foreach (var peptideAmount in ListChildren())
            {
                var tracerFormula = Molecule.Parse(peptideAmount.TracerFormula);
                double percent = tracerFormula.GetElementCount(tracerDef.Name);
                if (PeptideQuantity == PeptideQuantity.tracer_count)
                {
                    percent = percent * 100 / maxTracerCount;
                }
                result += percent * peptideAmount.PercentAmountValue / 100;
            }
            return result;
        }
        public double TracerPercent
        {
            get
            {
                double totalAmount = 0;
                double totalValue = 0;
                foreach (var child in ListChildren())
                {
                    totalAmount += child.PercentAmountValue;
                    totalValue += child.TracerPercent * child.PercentAmountValue;
                }
                return totalValue/totalAmount;
            }
        }
        public double Turnover
        {
            get
            {
                return 100*(1 - GetChild("").PercentAmountValue/TotalPercentAmount);
            }
        }
        public PeptideQuantity PeptideQuantity { get; private set; }
    }
}