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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var property in base.GetModelProperties())
            {
                yield return property;
            }
            yield return Property<Peptide,String>(
                m=>m.ProteinName, (m,v)=>m.ProteinName=v,
                e=>e.Protein, (e,v)=>e.Protein=v);
            yield return Property<Peptide, String>(
                m => m.ProteinDescription, (m, v) => m.ProteinDescription = v,
                e => e.ProteinDescription, (e, v) => e.ProteinDescription = v);
        }

        protected override void Load(DbPeptide entity)
        {
            Sequence = entity.Sequence;
            FullSequence = entity.FullSequence;
            _searchResultCount = entity.SearchResultCount;
            base.Load(entity);
        }

        protected override DbPeptide UpdateDbEntity(ISession session)
        {
            var peptide = base.UpdateDbEntity(session);
            peptide.Protein = ProteinName;
            peptide.ProteinDescription = ProteinDescription;
            session.Save(new DbChangeLog(this));
            return peptide;
        }

        public ChargedPeptide GetChargedPeptide(int charge)
        {
            return new ChargedPeptide(FullSequence, charge);
        }
        [Browsable(false)]
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
            return new DbPeptideAnalysis
            {
                Peptide = dbPeptide,
                Workspace = dbPeptide.Workspace,
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
            using (Workspace.GetReadLock())
            {
                using (var session = Workspace.OpenWriteSession())
                {
                    var dbPeptide = session.Load<DbPeptide>(Id);
                    var criteria = session.CreateCriteria(typeof (DbPeptideAnalysis))
                        .Add(Restrictions.Eq("Peptide", dbPeptide))
                        .Add(Restrictions.Eq("Workspace", Workspace.LoadDbWorkspace(session)));
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
            }
            return Workspace.Reconciler.LoadPeptideAnalysis(dbPeptideAnalysis.Id.Value);
        }
        [DisplayName("# Data Files")]
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
    }
}
