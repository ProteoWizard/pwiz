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
    public class PeptideFileAnalyses : EntityModelCollection<DbPeptideAnalysis, long, DbPeptideFileAnalysis,PeptideFileAnalysis>
    {
        public PeptideFileAnalyses(PeptideAnalysis peptideAnalysis, DbPeptideAnalysis dbPeptideAnalysis) : base(peptideAnalysis.Workspace, dbPeptideAnalysis)
        {
            Parent = peptideAnalysis;
        }

        public PeptideAnalysis PeptideAnalysis { get { return (PeptideAnalysis) Parent;} }
        protected override IEnumerable<KeyValuePair<long, DbPeptideFileAnalysis>> GetChildren(DbPeptideAnalysis parent)
        {
            foreach (var fileAnalysis in parent.FileAnalyses)
            {
                yield return new KeyValuePair<long, DbPeptideFileAnalysis>(fileAnalysis.Id.Value, fileAnalysis);
            }
        }

        protected override void SetChildCount(DbPeptideAnalysis parent, int childCount)
        {
            parent.FileAnalysisCount = childCount;
        }

        protected override int GetChildCount(DbPeptideAnalysis parent)
        {
            return parent.FileAnalysisCount;
        }

        public override PeptideFileAnalysis WrapChild(DbPeptideFileAnalysis entity)
        {
            return new PeptideFileAnalysis(PeptideAnalysis, entity);
        }

        public IList<PeptideFileAnalysis> ListPeptideFileAnalyses(bool filterRejects)
        {
            var result = new List<PeptideFileAnalysis>();
            foreach (var peptideFileAnalysis in ListChildren())
            {
                if (filterRejects && peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
                {
                    continue;
                }
                result.Add(peptideFileAnalysis);
            }
            return result;
        }

        public PeptideFileAnalysis EnsurePeptideFileAnalysis(DbPeptideFileAnalysis dbPeptideFileAnalysis)
        {
            var result = GetChild(dbPeptideFileAnalysis.Id.Value);
            if (result != null)
            {
                return result;
            }
            result = new PeptideFileAnalysis(PeptideAnalysis, dbPeptideFileAnalysis);
            AddChild(result.Id.Value, result);
            return result;
        }
    }
}
