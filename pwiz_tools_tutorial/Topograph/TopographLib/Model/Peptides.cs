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
    public class Peptides : EntityModelCollection<DbWorkspace, long, DbPeptide, Peptide> 
    {
        public Peptides(Workspace workspace, DbWorkspace dbWorkspace) : base(workspace, dbWorkspace)
        {
        }
        protected override bool TrustChildCount { get { return false;}}
        protected override IEnumerable<KeyValuePair<long, DbPeptide>> GetChildren(DbWorkspace parent)
        {
            foreach (var dbPeptide in parent.Peptides)
            {
                yield return new KeyValuePair<long, DbPeptide>(dbPeptide.Id.Value, dbPeptide);
            }
        }

        public override Peptide WrapChild(DbPeptide entity)
        {
            return new Peptide(Workspace, entity);
        }

        protected override int GetChildCount(DbWorkspace parent)
        {
            return parent.PeptideCount;
        }

        protected override void SetChildCount(DbWorkspace parent, int childCount)
        {
            parent.PeptideCount = childCount;
        }
        public Peptide GetPeptide(DbPeptide dbPeptide)
        {
            return GetChild(dbPeptide.Id.Value);
        }
    }
}
