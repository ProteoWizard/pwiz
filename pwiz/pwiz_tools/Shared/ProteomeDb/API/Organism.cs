using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using NHibernate;
using ProteomeDb.DataModel;

namespace ProteomeDb.API
{
    public class Organism : EntityModel<DbOrganism>
    {
        public const int MAX_PEPTIDE_LENGTH = 64;
        public Organism(ProteomeDb proteomeDb, DbOrganism organism) : base(proteomeDb, organism)
        {
            Name = organism.Name;
        }
        public String Name { get; private set; }
        public IList<Digestion> ListDigestions()
        {
            using (ISession session = ProteomeDb.OpenSession())
            {
                DbOrganism organism = GetEntity(session);

                List<Digestion> digestions = new List<Digestion>();
                foreach (DbDigestion dbDigestion in organism.Digestions)
                {
                    digestions.Add(new Digestion(this, dbDigestion));
                }
                return digestions;
            }
        }
        public IList<Protein> ListProteins()
        {
            using (ISession session = ProteomeDb.OpenSession())
            {
                DbOrganism organism = GetEntity(session);
                List<Protein> proteins = new List<Protein>();
                foreach (DbProtein dbProtein in organism.Proteins)
                {
                    proteins.Add(new Protein(this, dbProtein));
                }
                return proteins;
            }
        }
        public Digestion Digest(IProtease protease, String name, String description, ProgressMonitor progressMonitor)
        {
            DbOrganism organism;
            DbDigestion digestion;
            List<DbProtein> proteins;
            using (ISession session = ProteomeDb.OpenWriteSession())
            {
                organism = GetEntity(session);
                session.BeginTransaction();
                digestion = new DbDigestion
                                {
                                    Name = name,
                                    Description = description,
                                    Organism = organism,
                                    MaxMissedCleavages = protease.MaxMissedCleavages
                                };
                session.Save(digestion);
                if (!progressMonitor.Invoke("Listing proteins", 0))
                {
                    return null;
                }
                proteins = new List<DbProtein>(organism.Proteins);
                Dictionary<String,long> digestedPeptideIds 
                    = new Dictionary<string, long>();
                const String sqlPeptide =
                        "INSERT INTO ProteomeDbDigestedPeptide (Digestion, MissedCleavages, Sequence, Version) VALUES(@Digestion,@MissedCleavages,@Sequence,1);select last_insert_rowid();";
                var commandPeptide = session.Connection.CreateCommand();
                commandPeptide.CommandText = sqlPeptide;
                commandPeptide.Parameters.Add(new SQLiteParameter("@Digestion"));
                commandPeptide.Parameters.Add(new SQLiteParameter("@MissedCleavages"));
                commandPeptide.Parameters.Add(new SQLiteParameter("@Sequence"));
                const String sqlPeptideProtein =
                    "INSERT INTO ProteomeDbDigestedPeptideProtein (StartIndex, Peptide, Protein, Version) VALUES(?,?,?,1);";
                var commandProtein = session.Connection.CreateCommand();
                commandProtein.CommandText = sqlPeptideProtein;
                commandProtein.Parameters.Add(new SQLiteParameter("@StartIndex"));
                commandProtein.Parameters.Add(new SQLiteParameter("@Peptide"));
                commandProtein.Parameters.Add(new SQLiteParameter("@Protein"));
                for (int i = 0; i < proteins.Count; i++)
                {
                    if (!progressMonitor.Invoke("Digesting " + proteins.Count 
                        + " proteins", 100 * i / proteins.Count))
                    {
                        return null;
                    }
                    Protein protein = new Protein(this, proteins[i]);
                    foreach (DigestedPeptide digestedPeptide in protease.Digest(protein))
                    {
                        if (digestedPeptide.Sequence.Length > MAX_PEPTIDE_LENGTH)
                        {
                            continue;
                        }
                        long digestedPeptideId;
                        if (!digestedPeptideIds.TryGetValue(digestedPeptide.Sequence, out digestedPeptideId))
                        {
                            ((SQLiteParameter) commandPeptide.Parameters[0]).Value = digestion.Id;
                            ((SQLiteParameter) commandPeptide.Parameters[1]).Value = digestedPeptide.MissedCleavages;
                            ((SQLiteParameter) commandPeptide.Parameters[2]).Value = digestedPeptide.Sequence;
                            digestedPeptideId = Convert.ToInt64(commandPeptide.ExecuteScalar());
                            digestedPeptideIds.Add(digestedPeptide.Sequence, digestedPeptideId);
                        }
                        ((SQLiteParameter) commandProtein.Parameters[0]).Value = digestedPeptide.Index;
                        ((SQLiteParameter) commandProtein.Parameters[1]).Value = digestedPeptideId;
                        ((SQLiteParameter) commandProtein.Parameters[2]).Value = proteins[i].Id;
                        commandProtein.ExecuteNonQuery();
                    }
                }
                if (!progressMonitor.Invoke("Committing transaction", 99))
                {
                    return null;
                }
                session.Transaction.Commit();
                progressMonitor.Invoke(
                    "Digested " + proteins.Count + " proteins into " + digestedPeptideIds.Count + " unique peptides",
                    100);
                return new Digestion(this, digestion);
            }
        }
    }
}
