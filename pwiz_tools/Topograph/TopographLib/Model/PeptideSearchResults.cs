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
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideSearchResults : SimpleChildCollection<DbPeptide, long, DbPeptideSearchResult>
    {
        public PeptideSearchResults(Peptide peptide, DbPeptide dbPeptide) : base(peptide.Workspace, dbPeptide)
        {
            Peptide = peptide;
        }

        public Peptide Peptide { get; private set; }

        protected override IEnumerable<KeyValuePair<long, DbPeptideSearchResult>> GetChildren(DbPeptide parent)
        {
            foreach (var dbPeptideSearchResult in parent.SearchResults)
            {
                yield return new KeyValuePair<long, DbPeptideSearchResult>(dbPeptideSearchResult.MsDataFile.Id.Value, dbPeptideSearchResult);
            }
        }

        protected override int GetChildCount(DbPeptide parent)
        {
            return parent.SearchResultCount;
        }

        protected override void SetChildCount(DbPeptide parent, int childCount)
        {
            parent.SearchResultCount = childCount;
        }

        protected override void SetParent(DbPeptideSearchResult child, DbPeptide parent)
        {
            child.Peptide = parent;
        }
        public void AddChild(DbPeptideSearchResult peptideSearchResult)
        {
            AddChild(peptideSearchResult.MsDataFile.Id.Value, peptideSearchResult);
        }
    }
}
