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
using NHibernate.Criterion;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideAnalyses : WeakEntityModelCollection<DbWorkspace, DbPeptideAnalysis, PeptideAnalysis>
    {
        public PeptideAnalyses(Workspace workspace, DbWorkspace dbWorkspace) : base(workspace, dbWorkspace)
        {
            
        }

        protected override IEnumerable<KeyValuePair<long, DbPeptideAnalysis>> GetChildren(DbWorkspace parent)
        {
            foreach (var dbPeptideAnalysis in parent.PeptideAnalyses)
            {
                yield return new KeyValuePair<long, DbPeptideAnalysis>(dbPeptideAnalysis.Id.Value, dbPeptideAnalysis);
            }
        }

        public override PeptideAnalysis WrapChild(DbPeptideAnalysis entity)
        {
            throw new InvalidOperationException();
        }

        protected override int GetChildCount(DbWorkspace parent)
        {
            return parent.PeptideAnalysisCount;
        }

        protected override void SetChildCount(DbWorkspace parent, int childCount)
        {
            parent.PeptideAnalysisCount = childCount;
        }

        public PeptideAnalysis GetPeptideAnalysis(DbPeptideAnalysis dbPeptideAnalysis)
        {
            return GetChild(dbPeptideAnalysis.Id.Value);
        }
        public List<PeptideAnalysis> ListOpenPeptideAnalyses()
        {
            using (GetReadLock())
            {
                var result = new List<PeptideAnalysis>();
                foreach (var peptideAnalysis in ListChildren())
                {
                    if (peptideAnalysis.GetChromatogramRefCount() > 0)
                    {
                        result.Add(peptideAnalysis);
                    }
                }
                return result;
            }
        }
    }
}
