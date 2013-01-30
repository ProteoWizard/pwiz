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
using System.ComponentModel;
using System.Diagnostics;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class Peptide : EntityModel<long, PeptideData>
    {
// ReSharper disable UnassignedField.Local
        // TODO(nicksh): Set valid search result count
        private int _searchResultCount;
// ReSharper restore UnassignedField.Local
        public static String TrimSequence(String sequence)
        {
            return new ChargedPeptide(sequence, 1).Sequence;
        }
        public Peptide(Workspace workspace, long id, PeptideData peptideData) : base(workspace, id, peptideData)
        {
        }
        public Peptide(Workspace workspace, DbPeptide dbPeptide) : this(workspace, dbPeptide.GetId(), new PeptideData(dbPeptide))
        {
            
        }
        public Peptide(Workspace workspace, long id) : base(workspace, id)
        {
        }

        public long Id { get { return Key; } }

        public ChargedPeptide GetChargedPeptide(int charge)
        {
            return new ChargedPeptide(FullSequence, charge);
        }
        [Browsable(false)]
        public String FullSequence { get { return Data.FullSequence; } }
        public String Sequence { get { return Data.Sequence; } }
        public String ProteinName { get { return Data.ProteinName; } }
        public String ProteinDescription { get { return Data.ProteinDescription; } }
        public void Save(ISession session)
        {
            var dbPeptide = session.Get<DbPeptide>(Id);
            dbPeptide.Sequence = Sequence;
            dbPeptide.FullSequence = FullSequence;
            dbPeptide.Protein = ProteinName;
            dbPeptide.ProteinDescription = ProteinDescription;
            session.Update(dbPeptide);
        }
        public void UpdateProtein(string fullSequence, String name, String description)
        {
            Data = Data.SetFullSequence(fullSequence).SetProteinName(name).SetProteinDescription(description);
        }
        [DisplayName("Max Tracers")]
        public int MaxTracerCount { 
            get
            {
                return Workspace.GetMaxTracerCount(Sequence);
            }
        }

        public static DbPeptideAnalysis CreateDbPeptideAnalysis(ISession session, DbPeptide dbPeptide)
        {
            int minCharge, maxCharge;
            var query = session.CreateQuery("SELECT MIN(s.PrecursorCharge), MAX(s.PrecursorCharge)"
                                            + "\nFROM " + typeof (DbPeptideSpectrumMatch) +
                                            " s WHERE s.Peptide = :peptide")
                .SetParameter("peptide", dbPeptide);
            var minMaxCharge = (object[]) query.UniqueResult();
            if (minMaxCharge == null)
            {
                return null;
            }
            minCharge = Convert.ToInt32(minMaxCharge[0]);
            maxCharge = Convert.ToInt32(minMaxCharge[1]);
            return new DbPeptideAnalysis
            {
                Peptide = dbPeptide,
                MinCharge = minCharge,
                MaxCharge = maxCharge,
            };
        }

        public DbPeptideAnalysis CreateDbPeptideAnalysis(ISession session)
        {
            return CreateDbPeptideAnalysis(session, session.Load<DbPeptide>(Id));
        }

        public PeptideAnalysis EnsurePeptideAnalysis()
        {
            DbPeptideAnalysis dbPeptideAnalysis;
            using (var session = Workspace.OpenWriteSession())
            {
                var dbPeptide = session.Load<DbPeptide>(Id);
                var criteria = session.CreateCriteria(typeof (DbPeptideAnalysis))
                                      .Add(Restrictions.Eq("Peptide", dbPeptide));
                dbPeptideAnalysis = (DbPeptideAnalysis) criteria.UniqueResult();
                if (dbPeptideAnalysis == null)
                {
                    dbPeptideAnalysis = CreateDbPeptideAnalysis(session);
                    if (dbPeptideAnalysis == null)
                    {
                        return null;
                    }
                    session.BeginTransaction();
                    session.Save(dbPeptideAnalysis);
                    session.Transaction.Commit();
                }
            }
            Workspace.DatabasePoller.LoadAndMergeChanges(null);
            var peptideAnalysis =  Workspace.PeptideAnalyses.FindByKey(dbPeptideAnalysis.GetId());
            if (null == peptideAnalysis)
            {
                Debug.Assert(false);
            }
            return peptideAnalysis;
        }
        [DisplayName("# Data Files")]
        public int SearchResultCount
        {
            get
            {
                return _searchResultCount;
            }
        }

        public String GetProteinKey()
        {
            return Workspace.GetProteinKey(ProteinName, ProteinDescription);
        }

        public string ProteinKey
        {
            get
            {
                return Workspace.GetProteinKey(ProteinName, ProteinDescription);
            }
        }

        public double MonoisotopicMass
        {
            get { return Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(Sequence); }
        }

        public override string ToString()
        {
            return FullSequence;
        }

        public override PeptideData GetData(WorkspaceData workspaceData)
        {
            PeptideData peptideData;
            workspaceData.Peptides.TryGetValue(Id, out peptideData);
            return peptideData;
        }

        public override WorkspaceData SetData(WorkspaceData workspaceData, PeptideData value)
        {
            return workspaceData.SetPeptides(workspaceData.Peptides.Replace(Id, value));
        }
    }
}
