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
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class Peptide : AnnotatedEntityModel<DbPeptide>
    {
        private int _searchResultCount;
        public static String TrimSequence(String sequence)
        {
            return new ChargedPeptide(sequence, 1).Sequence;
        }
        public Peptide(Workspace workspace, DbPeptide dbPeptide)
            : base(workspace, dbPeptide)
        {
        }

        protected override void Load(DbPeptide entity)
        {
            base.Load(entity);
            ProteinName = entity.Protein;
            ProteinDescription = entity.ProteinDescription;
            Sequence = entity.Sequence;
            FullSequence = entity.FullSequence;
            _searchResultCount = entity.SearchResultCount;
        }

        protected override DbPeptide UpdateDbEntity(ISession session)
        {
            var peptide = base.UpdateDbEntity(session);
            peptide.Protein = ProteinName;
            peptide.ProteinDescription = ProteinDescription;
            return peptide;
        }

        public ChargedPeptide GetChargedPeptide(int charge)
        {
            return new ChargedPeptide(FullSequence, charge);
        }
        public String FullSequence { get; private set; }
        public String Sequence { get; private set; }
        public String ProteinName { get; private set; }
        public String ProteinDescription { get; private set; }
        public void UpdateProtein(String name, String description)
        {
            using (GetWriteLock())
            {
                ProteinName = name;
                ProteinDescription = description;
                OnChange();
            }
        }
        public int MaxTracerCount { 
            get
            {
                return Workspace.GetMaxTracerCount(Sequence);
            }
        }

        public DbPeptideAnalysis CreateDbPeptideAnalysis(ISession session)
        {
            int minCharge, maxCharge;
            var dbPeptide = session.Load<DbPeptide>(Id);
            var query = session.CreateQuery("SELECT MIN(s.MinCharge), MAX(s.MaxCharge)"
                                            + "\nFROM " + typeof (DbPeptideSearchResult) +
                                            " s WHERE s.Peptide = :peptide")
                .SetParameter("peptide", dbPeptide);
            var minMaxCharge = (object[]) query.UniqueResult();
            if (minMaxCharge == null)
            {
                return null;
            }
            minCharge = Convert.ToInt32(minMaxCharge[0]);
            maxCharge = Convert.ToInt32(minMaxCharge[1]);
            var dbWorkspace = Workspace.LoadDbWorkspace(session);
            return new DbPeptideAnalysis
            {
                Peptide = session.Load<DbPeptide>(Id),
                Workspace = dbWorkspace,
                MinCharge = minCharge,
                MaxCharge = maxCharge,
                IntermediateEnrichmentLevels = 0,
            };
        }

        public PeptideAnalysis EnsurePeptideAnalysis()
        {
            PeptideAnalysis peptideAnalysis;
            using (Workspace.GetReadLock())
            {
                using (var session = Workspace.OpenWriteSession())
                {
                    var dbPeptide = session.Load<DbPeptide>(Id);
                    var criteria = session.CreateCriteria(typeof (DbPeptideAnalysis))
                        .Add(Restrictions.Eq("Peptide", dbPeptide))
                        .Add(Restrictions.Eq("Workspace", Workspace.LoadDbWorkspace(session)));
                    var dbPeptideAnalysis = (DbPeptideAnalysis) criteria.UniqueResult();
                    if (dbPeptideAnalysis != null)
                    {
                        return Workspace.PeptideAnalyses.GetChild(dbPeptideAnalysis.Id.Value, session);
                    }
                    dbPeptideAnalysis = CreateDbPeptideAnalysis(session);
                    if (dbPeptideAnalysis == null)
                    {
                        return null;
                    }
                    session.BeginTransaction();
                    session.Save(dbPeptideAnalysis);
                    session.Transaction.Commit();
                    peptideAnalysis = new PeptideAnalysis(Workspace, dbPeptideAnalysis);
                }
            }
            using (Workspace.GetWriteLock()) 
            {
                Workspace.PeptideAnalyses.AddChild(peptideAnalysis.Id.Value, peptideAnalysis);
                Workspace.AddEntityModel(peptideAnalysis);
                Workspace.ChromatogramGenerator.SetRequeryPending();
                return peptideAnalysis;
            }
        }
        public int SearchResultCount
        {
            get
            {
                return _searchResultCount;
            }
            set
            {
                SetIfChanged(ref _searchResultCount, value);
            }
        }
    }
}
