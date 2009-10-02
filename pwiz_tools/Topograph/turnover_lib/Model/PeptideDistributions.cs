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

namespace pwiz.Topograph.Model
{
    public class PeptideDistributions : EntityModelCollection<DbPeptideFileAnalysis, PeptideQuantity, DbPeptideDistribution, PeptideDistribution>
    {
        public PeptideDistributions(PeptideFileAnalysis peptideFileAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis)
            : base(peptideFileAnalysis.Workspace, dbPeptideFileAnalysis)
        {
            Parent = peptideFileAnalysis;
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

        public PeptideDistribution EnsureChild(PeptideQuantity peptideQuantity)
        {
            lock(this)
            {
                var result = GetChild(peptideQuantity);
                if (result != null)
                {
                    return result;
                }
                result = new PeptideDistribution(this, peptideQuantity);
                AddChild(peptideQuantity, result);
                return result;
            }
        }

        protected override void OnChange()
        {
            PeptideFileAnalysis.PeptideAnalysis.PeptideRates.Clear();
            base.OnChange();
        }
    }
}
