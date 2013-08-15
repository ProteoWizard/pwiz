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
using System.Linq;
using NHibernate;
using NHibernate.Criterion;

namespace pwiz.Topograph.Data
{
    public class DbPeptide : DbAnnotatedEntity<DbPeptide>
    {
        public virtual String Sequence
        {
            get; set;
        }
        public virtual String FullSequence
        {
            get; 
            set;
        }
        public virtual String Protein
        {
            get; set;
        }
        public virtual String ProteinDescription
        {
            get; set;
        }
        public override string ToString()
        {
            return FullSequence;
        }

        public virtual ILookup<long, double> PsmTimesByDataFileId(ISession session)
        {
            return session.CreateCriteria<DbPeptideSpectrumMatch>()
                .Add(Restrictions.Eq("Peptide", this))
                .List<DbPeptideSpectrumMatch>()
                .ToLookup(psm=>psm.MsDataFile.Id.Value, psm=>psm.RetentionTime);
        }
    }
}
