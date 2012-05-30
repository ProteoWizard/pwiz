/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Criterion;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteomeDatabase.Util;

namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Public interface to a proteome database.
    /// </summary>
    public class ProteomeDb : IDisposable
    {
        public const string EXT_PROTDB = ".protdb";

        private static readonly Type TYPE_DB = typeof(DbDigestion);
        private static readonly Dictionary<String, ProteomeDb> PROTEOME_DBS = new Dictionary<string, ProteomeDb>();
        public const int MIN_SEQUENCE_LENGTH = 4;
        public const int MAX_SEQUENCE_LENGTH = 7;
        private ProteomeDb(String path)
        {
            Path = path;
            SessionFactory = SessionFactoryFactory.CreateSessionFactory(path, TYPE_DB, false);
            DatabaseLock = new ReaderWriterLock();
        }

        public void Dispose()
        {
            if (SessionFactory != null)
            {
                SessionFactory.Dispose();
                SessionFactory = null;
            }
        }

        public ISession OpenSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, false);
        }

        public ISession OpenWriteSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, true);        
        }
        public ReaderWriterLock DatabaseLock { get; private set; }
        public String Path { get; private set; }
        public void ConfigureMappings(Configuration configuration)
        {
            SessionFactoryFactory.ConfigureMappings(configuration, TYPE_DB);
        }

        private struct ProtIdNames
        {
            public ProtIdNames(long id, ICollection<DbProteinName> names) : this()
            {
                Id = id;
                Names = names;
            }

            public long Id { get; private set; }
            public ICollection<DbProteinName> Names { get; private set; }
        }

        public void AddFastaFile(StreamReader reader, ProgressMonitor progressMonitor)
        {
            Dictionary<string, ProtIdNames> proteinIds = new Dictionary<string, ProtIdNames>();
            using (ISession session = OpenWriteSession())
            {
                foreach (DbProtein protein in session.CreateCriteria(typeof(DbProtein)).List())
                {
                    if (protein.Id.HasValue)
                        proteinIds.Add(protein.Sequence, new ProtIdNames(protein.Id.Value, protein.Names));
                }
                int proteinCount = 0;
                using (var transaction = session.BeginTransaction())
                {
                    FastaImporter fastaImporter = new FastaImporter();
                    IDbCommand insertProtein = session.Connection.CreateCommand();
                    insertProtein.CommandText =
                        "INSERT INTO ProteomeDbProtein (Version, Sequence) Values (1,?);select last_insert_rowid();";
                    insertProtein.Parameters.Add(new SQLiteParameter());
                    IDbCommand insertName = session.Connection.CreateCommand();
                    insertName.CommandText =
                        "INSERT INTO ProteomeDbProteinName (Version, Protein, IsPrimary, Name, Description) Values(1,?,?,?,?)";
                    insertName.Parameters.Add(new SQLiteParameter());
                    insertName.Parameters.Add(new SQLiteParameter());
                    insertName.Parameters.Add(new SQLiteParameter());
                    insertName.Parameters.Add(new SQLiteParameter());

                    foreach (DbProtein protein in fastaImporter.Import(reader))
                    {
                        int iProgress = (int)(reader.BaseStream.Position * 100 / (reader.BaseStream.Length + 1));
                        if (!progressMonitor.Invoke("Added " + proteinCount + " proteins", iProgress))
                        {
                            return;
                        }
                        bool existingProtein = false;
                        ProtIdNames proteinIdNames;
                        if (proteinIds.TryGetValue(protein.Sequence, out proteinIdNames))
                        {
                            existingProtein = true;
                        }
                        else
                        {
                            ((SQLiteParameter)insertProtein.Parameters[0]).Value = protein.Sequence;
                            proteinIdNames = new ProtIdNames(Convert.ToInt64(insertProtein.ExecuteScalar()), new DbProteinName[0]);
                            proteinIds.Add(protein.Sequence, proteinIdNames);
                            proteinCount++;
                        }
                        foreach (var proteinName in protein.Names)
                        {
                            // Skip any names that already exist
                            if (proteinIdNames.Names.Any(dbProteinName => Equals(dbProteinName.Name, proteinName.Name)))
                                continue;

                            try
                            {
                                ((SQLiteParameter)insertName.Parameters[0]).Value = proteinIdNames.Id;
                                ((SQLiteParameter)insertName.Parameters[1]).Value = proteinName.IsPrimary && !existingProtein;
                                ((SQLiteParameter)insertName.Parameters[2]).Value = proteinName.Name;
                                ((SQLiteParameter)insertName.Parameters[3]).Value = proteinName.Description;
                                insertName.ExecuteNonQuery();
                            }
                            catch (Exception exception)
                            {
                                Console.Out.WriteLine(exception);
                            }
                        }
                    }
                    if (!progressMonitor.Invoke("Committing transaction", 99))
                    {
                        return;
                    }
                    transaction.Commit();
                }
                AnalyzeDb(session);
                progressMonitor.Invoke("Finished importing " + proteinCount + " proteins", 100);
            }
        }

        /// <summary>
        /// Executes the "analyze" command to update statistics about indexes in the database.
        /// This should be called after a large number of records have been inserted.
        /// </summary>
        private static void AnalyzeDb(ISession session)
        {
            using(IDbCommand command = session.Connection.CreateCommand())
            {
                command.CommandText = "Analyze;";
                command.ExecuteNonQuery();
            }
        }

        public int GetProteinCount()
        {
            using (var session = OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery("SELECT Count(P.Id) From " + typeof (DbProtein) + " P").UniqueResult());
            }
        }
        public void AttachDatabase(IDbConnection connection)
        {
            using(IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = "ATTACH DATABASE '" + Path + "' AS ProteomeDb";
                command.ExecuteNonQuery();
            }
        }
        private ISessionFactory SessionFactory { get; set; }
        public static ProteomeDb OpenProteomeDb(String path, bool isTempDb = false)
        {
            if (isTempDb)
                return new ProteomeDb(path);
            lock (PROTEOME_DBS)
            {
                ProteomeDb proteomeDb;
                if (!PROTEOME_DBS.TryGetValue(path, out proteomeDb))
                {
                    proteomeDb = new ProteomeDb(path);
                    PROTEOME_DBS.Add(path, proteomeDb);
                }
                return proteomeDb;
            }
        }

        public static ProteomeDb CreateProteomeDb(String path)
        {
            using (SessionFactoryFactory.CreateSessionFactory(path, TYPE_DB, true))
            {
            }
            return OpenProteomeDb(path);
        }

        public static void ClearCache()
        {
            lock (PROTEOME_DBS)
            {
                foreach (var proteomeDb in PROTEOME_DBS.Values)
                    proteomeDb.Dispose();
                PROTEOME_DBS.Clear();
            }
        }

        public IList<Digestion> ListDigestions()
        {
            using (ISession session = OpenSession())
            {
                List<Digestion> digestions = new List<Digestion>();
                foreach (DbDigestion dbDigestion in session.CreateCriteria(typeof(DbDigestion)).List())
                {
                    digestions.Add(new Digestion(this, dbDigestion));
                }
                return digestions;
            }
        }

        public Digestion GetDigestion(String name)
        {
            DbDigestion digestion = GetDbDigestion(name);
            if (digestion == null)
            {
                return null;
            }
            return new Digestion(this, digestion);
        }

        internal DbDigestion GetDbDigestion(String name)
        {
            using (ISession session = OpenSession())
            {
                return (DbDigestion) session.CreateCriteria(typeof (DbDigestion))
                    .Add(Restrictions.Eq("Name", name))
                    .UniqueResult();
            }
        }
        public IList<Protein> ListProteinSequences()
        {
            using (ISession session = OpenSession())
            {
                List<Protein> proteins = new List<Protein>();
                foreach (DbProtein dbProtein in session.CreateCriteria(typeof(DbProtein)).List())
                {
                    proteins.Add(new Protein(this, dbProtein));
                }
                return proteins;
            }
        }
        public IList<Protein> ListProteinsWithPrefix(String prefix, int maxResults)
        {
            using (ISession session = OpenSession())
            {
                List<DbProteinName> proteinNames = new List<DbProteinName>();
                ICriteria criteria = session.CreateCriteria(typeof(DbProteinName))
                    .Add(Restrictions.InsensitiveLike("Name", prefix + "%"))
                    .AddOrder(Order.Asc("Name"))
                    .SetMaxResults(maxResults);
                criteria.List(proteinNames);
                List<Protein> result = new List<Protein>();
                foreach (var dbProteinName in proteinNames)
                {
                    result.Add(new Protein(this, dbProteinName.Protein, dbProteinName));
                }
                return result;
            }
        }
        public Protein GetProteinByName(String name)
        {
            using (ISession session = OpenSession())
            {
                ICriteria criteria = session.CreateCriteria(typeof(DbProteinName))
                    .Add(Restrictions.Eq("Name", name));
                DbProteinName proteinName = (DbProteinName)criteria.UniqueResult();
                if (proteinName == null)
                {
                    return null;
                }
                return new Protein(this, proteinName.Protein, proteinName);
            }
        }
        public Digestion Digest(IProtease protease, ProgressMonitor progressMonitor)
        {
            using (ISession session = OpenWriteSession())
            {
                DbDigestion dbDigestion = GetDbDigestion(protease.Name);
                HashSet<string> existingSequences = new HashSet<string>();
                using (var transaction = session.BeginTransaction())
                {
                    if (dbDigestion != null)
                    {
                        if (dbDigestion.MaxSequenceLength >= MAX_SEQUENCE_LENGTH)
                        {
                            return new Digestion(this, dbDigestion);
                        }
                        if (!progressMonitor.Invoke("Listing existing peptides", 0))
                        {
                            return null;
                        }
                        IQuery query = session.CreateQuery("SELECT P.Sequence FROM "
                                                           + typeof(DbDigestedPeptide) + " P WHERE P.Digestion = :Digestion")
                            .SetParameter("Digestion", dbDigestion);
                        List<String> listSequences = new List<string>();
                        query.List(listSequences);
                        existingSequences.UnionWith(listSequences);
                        dbDigestion.MaxSequenceLength = MAX_SEQUENCE_LENGTH;
                        session.Update(dbDigestion);
                    }
                    else
                    {
                        dbDigestion = new DbDigestion
                        {
                            Name = protease.Name,
                            MinSequenceLength = MIN_SEQUENCE_LENGTH,
                            MaxSequenceLength = MAX_SEQUENCE_LENGTH,
                        };
                        session.Save(dbDigestion);
                    }
                    if (!progressMonitor.Invoke("Listing proteins", 0))
                    {
                        return null;
                    }
                    List<DbProtein> proteins = new List<DbProtein>();
                    session.CreateCriteria(typeof(DbProtein)).List(proteins);
                    Dictionary<String, long> digestedPeptideIds
                        = new Dictionary<string, long>();
                    const String sqlPeptide =
                            "INSERT INTO ProteomeDbDigestedPeptide (Digestion, Sequence) VALUES(?,?);select last_insert_rowid();";
                    using (var commandPeptide = session.Connection.CreateCommand())
                    {
                        commandPeptide.CommandText = sqlPeptide;
                        commandPeptide.Parameters.Add(new SQLiteParameter());
                        commandPeptide.Parameters.Add(new SQLiteParameter());
                        const String sqlPeptideProtein =
                            "INSERT INTO ProteomeDbDigestedPeptideProtein (Peptide, Protein) VALUES(?,?);";
                        var commandProtein = session.Connection.CreateCommand();
                        commandProtein.CommandText = sqlPeptideProtein;
                        commandProtein.Parameters.Add(new SQLiteParameter());
                        commandProtein.Parameters.Add(new SQLiteParameter());
                        commandProtein.Parameters.Add(new SQLiteParameter());
                        for (int i = 0; i < proteins.Count; i++)
                        {
                            var proteinSequences = new HashSet<string>();
                            if (!progressMonitor.Invoke("Digesting " + proteins.Count
                                + " proteins", 100 * i / proteins.Count))
                            {
                                return null;
                            }
                            Protein protein = new Protein(this, proteins[i]);

                            foreach (DigestedPeptide digestedPeptide in protease.Digest(protein))
                            {
                                if (digestedPeptide.Sequence.Length < dbDigestion.MinSequenceLength)
                                {
                                    continue;
                                }
                                String truncatedSequence = digestedPeptide.Sequence.Substring(
                                    0, Math.Min(digestedPeptide.Sequence.Length, dbDigestion.MaxSequenceLength));
                                if (existingSequences.Contains(truncatedSequence))
                                {
                                    continue;
                                }
                                if (proteinSequences.Contains(truncatedSequence))
                                {
                                    continue;
                                }
                                proteinSequences.Add(truncatedSequence);
                                long digestedPeptideId;
                                if (!digestedPeptideIds.TryGetValue(truncatedSequence, out digestedPeptideId))
                                {
                                    ((SQLiteParameter)commandPeptide.Parameters[0]).Value = dbDigestion.Id;
                                    ((SQLiteParameter)commandPeptide.Parameters[1]).Value = truncatedSequence;
                                    digestedPeptideId = Convert.ToInt64(commandPeptide.ExecuteScalar());
                                    digestedPeptideIds.Add(truncatedSequence, digestedPeptideId);
                                }
                                ((SQLiteParameter)commandProtein.Parameters[0]).Value = digestedPeptideId;
                                ((SQLiteParameter)commandProtein.Parameters[1]).Value = protein.Id;
                                commandProtein.ExecuteNonQuery();
                            }
                        }
                    }
                    if (!progressMonitor.Invoke("Committing transaction", 99))
                    {
                        return null;
                    }
                    transaction.Commit();

                    AnalyzeDb(session);
                    progressMonitor.Invoke(string.Format("Digested {0} proteins into {1} unique peptides", proteins.Count, digestedPeptideIds.Count),
                        100);
                }
                return new Digestion(this, dbDigestion);
            }
        }
    }
}
