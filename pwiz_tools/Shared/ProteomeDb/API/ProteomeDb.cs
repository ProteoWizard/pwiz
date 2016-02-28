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
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteomeDatabase.Properties;
using pwiz.ProteomeDatabase.Util;
namespace pwiz.ProteomeDatabase.API
{
    /// <summary>
    /// Public interface to a proteome database.
    /// </summary>
    public class ProteomeDb : IDisposable
    {
        public const string EXT_PROTDB = ".protdb"; // Not L10N

        public static int PROTDB_MAX_MISSED_CLEAVAGES = 6;

        internal static readonly Type TYPE_DB = typeof(DbDigestion);
        public const int SCHEMA_VERSION_MAJOR_0 = 0;
        public const int SCHEMA_VERSION_MINOR_0 = 0;
        public const int SCHEMA_VERSION_MINOR_1 = 1;
        public const int SCHEMA_VERSION_MAJOR_CURRENT = SCHEMA_VERSION_MAJOR_0; // v0.0 protdb files are just subsets of v0.1 files
        public const int SCHEMA_VERSION_MINOR_CURRENT = SCHEMA_VERSION_MINOR_1; // v0.1 adds protein metadata, and schema versioning
        private int _schemaVersionMajor;
        private int _schemaVersionMinor;
        private int _schemaVersionMajorAsRead;
        private int _schemaVersionMinorAsRead;
        public const int MIN_SEQUENCE_LENGTH = 4;
        public const int MAX_SEQUENCE_LENGTH = 7;
        private DatabaseResource _databaseResource;
        private readonly bool _isTmp; // We won't hang onto the global session factory if we know we're temporary
        private ProteomeDb(String path, bool isTmp)
        {
            _schemaVersionMajor = -1; // unknown
            _schemaVersionMinor = -1; // unknown
            _schemaVersionMajorAsRead = -1; // unknown
            _schemaVersionMinorAsRead = -1; // unknown
            _isTmp = isTmp;
            _databaseResource = DatabaseResource.GetDbResource(path);

            using (var session = OpenSession())
            {
                // Is this even a proper protDB file? (https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/thread.view?rowId=14893)
                using (IDbCommand command = session.Connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='ProteomeDbProteinName'"; // Not L10N
                    var obj = command.ExecuteScalar();
                    if (Convert.ToInt32(obj) == 0)
                        throw new FileLoadException(
                            String.Format(Resources.ProteomeDb_ProteomeDb__0__does_not_appear_to_be_a_valid___protDB__background_proteome_file_, path));
                }

                // Do we need to update the db to current version?
                ReadVersion(session);
            }
            if (_schemaVersionMajor != SCHEMA_VERSION_MAJOR_CURRENT)
            {
                throw new FileLoadException(
                    String.Format(Resources.SessionFactoryFactory_EnsureVersion_Background_proteome_file__0__has_a_format_which_is_newer_than_the_current_software___Please_update_to_the_latest_software_version_and_try_again_,
                        Path));
            }
            else if (_schemaVersionMinor < SCHEMA_VERSION_MINOR_CURRENT)
            {
                using (var session = OpenWriteSession())
                {
                    UpdateSchema(session);
                }
            }
        }

        private bool UpdateProgressAndCheckForCancellation(IProgressMonitor progressMonitor, ref ProgressStatus status, string message, int pctComplete)
        {
            if (progressMonitor.IsCanceled)
                return false;
            progressMonitor.UpdateProgress(status = status.ChangeMessage(message).ChangePercentComplete(pctComplete));
            return true;
        }

        public int Refcount { get { return _databaseResource.Refcount; } }

        // Close all file handles
        public void CloseDbConnection()
        {
            if (Refcount > 1) // Don't try this unless you're the only instance using this db
                throw new Exception("ProteomeDb locking problem"); // Not L10N
            _databaseResource.SessionFactory.Close();
        }

        private void ReadVersion(ISession session)
        {
            using (IDbCommand cmd = session.Connection.CreateCommand())
            {
                // do we even have a version? 0th-gen protdb doesn't have this.
                cmd.CommandText =
                    "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='ProteomeDbSchemaVersion'"; // Not L10N
                var obj = cmd.ExecuteScalar();
                if (Convert.ToInt32(obj) == 0)
                {
                    _schemaVersionMajor = SCHEMA_VERSION_MAJOR_0; // an ancient, unversioned file
                    _schemaVersionMinor = SCHEMA_VERSION_MINOR_0; // an ancient, unversioned file
                }
                else
                {
                    using (IDbCommand cmd2 = session.Connection.CreateCommand())
                    {
                        cmd2.CommandText = "SELECT SchemaVersionMajor FROM ProteomeDbSchemaVersion"; // Not L10N
                        var obj2 = cmd2.ExecuteScalar();
                        _schemaVersionMajor = Convert.ToInt32(obj2);
                    }
                    using (IDbCommand cmd3 = session.Connection.CreateCommand())
                    {
                        cmd3.CommandText = "SELECT SchemaVersionMinor FROM ProteomeDbSchemaVersion"; // Not L10N
                        var obj3 = cmd3.ExecuteScalar();
                        _schemaVersionMinor = Convert.ToInt32(obj3);
                    }
                }
                _schemaVersionMajorAsRead = _schemaVersionMajor;
                _schemaVersionMinorAsRead = _schemaVersionMinor;
            }
        }

        private void UpdateSchema(ISession session)
        {
            ReadVersion(session);  // Recheck version, in case another thread got here before us
            if ((_schemaVersionMajor == SCHEMA_VERSION_MAJOR_0) &&
                (_schemaVersionMinor < SCHEMA_VERSION_MINOR_1))
            {
                using (var transaction = session.BeginTransaction())
                using (IDbCommand command = session.Connection.CreateCommand())
                {
                    foreach (
                        var col in
                            new[] {"PreferredName", "Accession", "Gene", "Species", "WebSearchStatus"}) // Not L10N
                        // new protein metadata for v2  // Not L10N
                    {
                        command.CommandText =
                            String.Format("ALTER TABLE ProteomeDbProteinName ADD COLUMN {0} TEXT", col); // Not L10N
                        command.ExecuteNonQuery();
                    }
                    command.CommandText =
                        "CREATE TABLE ProteomeDbSchemaVersion (Id integer primary key autoincrement, SchemaVersionMajor INT, SchemaVersionMinor INT )"; // Not L10N
                    command.ExecuteNonQuery();
                    _schemaVersionMajor = SCHEMA_VERSION_MAJOR_0;
                    _schemaVersionMinor = SCHEMA_VERSION_MINOR_1;
                    session.Save(new DbVersionInfo { SchemaVersionMajor = _schemaVersionMajor , SchemaVersionMinor = _schemaVersionMinor });
                    transaction.Commit();
                }
            }
            // else unhandled schema version update - let downstream process issue detailed exceptions about missing fields etc
        }

        public void Dispose()
        {
            DatabaseResource databaseResource = Interlocked.Exchange(ref _databaseResource, null);
            if (null != databaseResource)
            {
                databaseResource.Release(_isTmp);
            }
        }


        public ISession OpenSession()
        {
            var session = new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, false);
            return session;
        }

        public ISession OpenWriteSession()
        {
            var session = new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, true);
            _schemaVersionMajorAsRead = _schemaVersionMajor; // now it's natively current version
            _schemaVersionMinorAsRead = _schemaVersionMinor; // now it's natively current version
            return session;
        }
        
        public ReaderWriterLock DatabaseLock { get { return _databaseResource.DatabaseLock; } }
        public String Path { get { return _databaseResource.Path; } }
        public ProteomeDbPath ProteomeDbPath {get { return new ProteomeDbPath(Path);}}
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

        public void AddFastaFile(StreamReader reader, IProgressMonitor progressMonitor, ref ProgressStatus status,  bool delayAnalyzeDb)
        {
            Dictionary<string, ProtIdNames> proteinIds = new Dictionary<string, ProtIdNames>();
            using (ISession session = OpenWriteSession()) // This is a long session, but there's no harm since db is useless till its done
            {
                foreach (DbProtein protein in session.CreateCriteria(typeof(DbProtein)).List())
                {
                    if (protein.Id.HasValue)
                        proteinIds.Add(protein.Sequence, new ProtIdNames(protein.Id.Value, protein.Names));
                }
                int proteinCount = 0;
                using (var transaction = session.BeginTransaction())
                using (IDbCommand insertProtein = session.Connection.CreateCommand())
                using (IDbCommand insertName = session.Connection.CreateCommand())
                {
                    WebEnabledFastaImporter fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.DelayedWebSearchProvider()); // just parse, no search for now
                    insertProtein.CommandText =
                        "INSERT INTO ProteomeDbProtein (Version, Sequence) Values (1,?);select last_insert_rowid();"; // Not L10N
                    insertProtein.Parameters.Add(new SQLiteParameter());
                    insertName.CommandText =
                        "INSERT INTO ProteomeDbProteinName (Version, Protein, IsPrimary, Name, Description, PreferredName, Accession, Gene, Species, WebSearchStatus) Values(1,?,?,?,?,?,?,?,?,?)"; // Not L10N
                    insertName.Parameters.Add(new SQLiteParameter()); // Id
                    insertName.Parameters.Add(new SQLiteParameter()); // IsPrimary
                    insertName.Parameters.Add(new SQLiteParameter()); // Name
                    insertName.Parameters.Add(new SQLiteParameter()); // Description
                    insertName.Parameters.Add(new SQLiteParameter()); // PreferredName
                    insertName.Parameters.Add(new SQLiteParameter()); // Accession
                    insertName.Parameters.Add(new SQLiteParameter()); // Gene
                    insertName.Parameters.Add(new SQLiteParameter()); // Species
                    insertName.Parameters.Add(new SQLiteParameter()); // WebSearchInfo


                    foreach (DbProtein protein in fastaImporter.Import(reader))
                    {
                        int iProgress = (int)(reader.BaseStream.Position * 100 / (reader.BaseStream.Length + 1));
                        if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, string.Format(Resources.ProteomeDb_AddFastaFile_Added__0__proteins,proteinCount), iProgress))
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
                                ((SQLiteParameter)insertName.Parameters[4]).Value = proteinName.PreferredName;
                                ((SQLiteParameter)insertName.Parameters[5]).Value = proteinName.Accession;
                                ((SQLiteParameter)insertName.Parameters[6]).Value = proteinName.Gene;
                                ((SQLiteParameter)insertName.Parameters[7]).Value = proteinName.Species;
                                ((SQLiteParameter)insertName.Parameters[8]).Value = proteinName.WebSearchStatus; // represent as a string for ease of serialization
                                insertName.ExecuteNonQuery();
                            }
                            catch (Exception exception)
                            {
                                Console.Out.WriteLine(exception);
                            }
                        }
                    }
                    if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_AddFastaFile_Saving_changes, 99))
                    {
                        return;
                    }
                    // TODO(bspratt): update or just wipe the Digestion tables, they're out of date now (issue #304, see commented out test ing ProteomeDbTest.cs, and other TODO in this file)
                    transaction.Commit();
                }
                if (!delayAnalyzeDb)
                {
                    AnalyzeDb(session); // NB This runs asynchronously and may interfere with further writes
                }
                UpdateProgressAndCheckForCancellation(progressMonitor, ref status, 
                    string.Format(Resources.ProteomeDb_AddFastaFile_Finished_importing__0__proteins, proteinCount), 100);
            }
        }

        /// <summary>
        /// Executes the "analyze" command to update statistics about indexes in the database.
        /// This should be called after a large number of records have been inserted.
        /// Note that this runs asynchronously and may interfere with writes, so use judiciously
        /// </summary>
        private static void AnalyzeDb(ISession session)
        {
            using(IDbCommand command = session.Connection.CreateCommand())
            {
                command.CommandText = "Analyze;"; // Not L10N
                command.ExecuteNonQuery();
            }
        }

        public void AnalyzeDb()
        {
            using (var session = OpenWriteSession())
            {
                AnalyzeDb(session);
            }
        }

        public int GetSchemaVersionMajor()
        {
            return _schemaVersionMajorAsRead;
        }

        public int GetSchemaVersionMinor()
        {
            return _schemaVersionMinorAsRead;
        }

        public int GetProteinCount()
        {
            using (var session = OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery("SELECT Count(P.Id) From " + typeof(DbProtein) + " P").UniqueResult()); // Not L10N
            }
        }
        public void AttachDatabase(IDbConnection connection)  // TODO - versioning issues?
        {
            using(IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = "ATTACH DATABASE '" + Path + "' AS ProteomeDb"; // Not L10N
                command.ExecuteNonQuery();
            }
        }
        private ISessionFactory SessionFactory { get { return _databaseResource.SessionFactory; } }
        public static ProteomeDb OpenProteomeDb(String path, bool isTemporary=false)
        {
            return new ProteomeDb(path, isTemporary);
        }

        public static ProteomeDb CreateProteomeDb(String path)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, TYPE_DB, true))
            {
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(new DbVersionInfo { SchemaVersionMajor = SCHEMA_VERSION_MAJOR_CURRENT, SchemaVersionMinor = SCHEMA_VERSION_MINOR_CURRENT });
                    transaction.Commit();
                }
            }

            return OpenProteomeDb(path);
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

        public bool HasProteinNamesWithUnresolvedMetadata()
        {
            // Get a list of proteins with unresolved metadata websearches
            using (var session = OpenSession())
            {
                var hql = "SELECT WebSearchStatus FROM " + typeof (DbProteinName); // Not L10N
                var query = session.CreateQuery(hql);
                foreach (var value in query.List())
                {
                    var term = value == null ? string.Empty : value.ToString();
                    var webSearchInfo = WebSearchInfo.FromString(term);
                    if (webSearchInfo.GetPendingSearchTerm().Length > 0 || // Protein not yet searched
                        webSearchInfo.IsEmpty()) // Protein has never been considered for metadata search
                    {
                        return true;
                    }
                }
            }
            return false;
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
                return GetDbDigestion(name, session);
            }
        }

        private DbDigestion GetDbDigestion(string name, ISession session)
        {
            return (DbDigestion) session.CreateCriteria(typeof (DbDigestion))
                .Add(Restrictions.Eq("Name", name)) // Not L10N
                .UniqueResult();
        }

        public IList<Protein> ListProteinSequences()
        {
            using (ISession session = OpenSession())
            {
                List<Protein> proteins = new List<Protein>();
                foreach (DbProtein dbProtein in session.CreateCriteria(typeof(DbProtein)).List())
                {
                    proteins.Add(new Protein(ProteomeDbPath, dbProtein));
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
                    .Add(Restrictions.InsensitiveLike("Name", prefix + "%")) // Not L10N
                    .AddOrder(Order.Asc("Name")) // Not L10N
                    .SetMaxResults(maxResults);
                criteria.List(proteinNames);
                List<Protein> result = new List<Protein>();
                foreach (var dbProteinName in proteinNames)
                {
                    result.Add(new Protein(ProteomeDbPath, dbProteinName.Protein, dbProteinName));
                }
                return result;
            }
        }

        /// <summary>
        /// Return protein matching given string - as a name, or failing that as an accession ID or PreferredName
        /// </summary>
        /// <param name="name">name or accession value</param>
        public Protein GetProteinByName(String name)
        {
            if (!String.IsNullOrEmpty(name))
            {
                using (ISession session = OpenSession())
                {
                    var proteinName = GetProteinName(session, name);
                    if (proteinName == null)
                    {
                        return null;
                    }
                    return new Protein(ProteomeDbPath, proteinName.Protein, proteinName);
                }
            }
            return null;
        }

        /// <summary>
        /// Return metadata for protein matching given string - as a name, or failing that as an accession ID or PreferredName
        /// </summary>
        /// <param name="name">name or accession value</param>
        public ProteinMetadata GetProteinMetadataByName(String name)
        {
            if (!String.IsNullOrEmpty(name))
            {
                using (ISession session = OpenSession())
                {
                    var proteinName = GetProteinName(session, name);
                    if (proteinName == null)
                    {
                        return null;
                    }
                    return proteinName.GetProteinMetadata();
                }
            }
            return null;
        }

        private static DbProteinName GetProteinName(ISession session, string searchName)
        {
            ICriteria criteriaName = session.CreateCriteria(typeof(DbProteinName))
                .Add(Restrictions.Eq("Name", searchName)); // Not L10N
            DbProteinName proteinName = (DbProteinName)criteriaName.UniqueResult();
            if (proteinName != null)
                return proteinName;
            string[] hints = {"Accession", "Gene", "PreferredName"}; // Not L10N
            var criterion = Restrictions.Disjunction();
            foreach (var name in hints)
            {
                criterion.Add(Restrictions.Eq(name, searchName));
            }
            List<DbProteinName> proteinNames = new List<DbProteinName>();
            ICriteria criteria = session.CreateCriteria(typeof(DbProteinName))
                .Add(criterion).SetMaxResults(1);
            criteria.List(proteinNames);
            return proteinNames.Any() ? proteinNames[0] : null;
        }

        public Digestion Digest(IProtease protease, int maxMissedCleavages, IProgressMonitor progressMonitor, ref ProgressStatus status, bool delayDbIndexing = false)
        {
            using (ISession session = OpenWriteSession())
            {
                DbDigestion dbDigestion = GetDbDigestion(protease.Name, session);
                HashSet<string> existingSequences;  // TODO(bspratt) - the logic around this seems fishy, investigate.  Probably never actually been used.  Part of fix for issue #304, probably
                if (dbDigestion != null)
                {
                    if (dbDigestion.MaxSequenceLength >= MAX_SEQUENCE_LENGTH)
                    {
                        return new Digestion(this, dbDigestion);
                    }
                    if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_Digest_Listing_existing_peptides, 0))
                    {
                        return null;
                    }
                    IQuery query = session.CreateQuery("SELECT P.Sequence FROM " // Not L10N
                                                        + typeof(DbDigestedPeptide) + " P WHERE P.Digestion = :Digestion") // Not L10N
                        .SetParameter("Digestion", dbDigestion); // Not L10N
                    List<String> listSequences = new List<string>();
                    query.List(listSequences);
                    existingSequences = new HashSet<string>(listSequences);
                    dbDigestion.MaxSequenceLength = MAX_SEQUENCE_LENGTH;
                }
                else
                {
                    dbDigestion = new DbDigestion
                    {
                        Name = protease.Name,
                        MinSequenceLength = MIN_SEQUENCE_LENGTH,
                        MaxSequenceLength = MAX_SEQUENCE_LENGTH,
                    };
                    existingSequences = new HashSet<string>();
                }
                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_Digest_Listing_proteins, 0)) 
                {
                    return null;
                }
                var dbProteins = new List<DbProtein>();
                session.CreateCriteria(typeof (DbProtein)).List(dbProteins);

                // Digest the proteins
                var proteinCount = dbProteins.Count;
                if (proteinCount == 0)
                    return null;

                var proteinsList = new Protein[proteinCount];
                var truncatedSequences = new HashSet<string>[proteinCount]; // One hashset of sequences for each protein of interest
                const int N_DIGEST_THREADS = 16; // Arbitrary value - do a progress/canel check every nth protein
                for (var i = 0; i < proteinCount; i += N_DIGEST_THREADS)
                {
                    var endRange = Math.Min(proteinCount, i + N_DIGEST_THREADS);
                    if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, string.Format(Resources.ProteomeDb_Digest_Digesting__0__proteins, proteinCount), 50 * endRange / proteinCount))
                    {
                        return null;
                    }
                    for (int ii = i; ii < endRange; ii++)
                    {
                        var protein = new Protein(ProteomeDbPath, dbProteins[ii]);
                        proteinsList[ii] = protein;
                    }
                    Parallel.For(i, endRange, ii => 
                    {
                        var proteinSequences = new HashSet<string>(); // We only save the first dbDigestion.MaxSequenceLength characters of each peptide so collisions are likely
                        truncatedSequences[ii] = proteinSequences;  // One hashset of sequences for each protein of interest

                        foreach (var digestedPeptide in protease.DigestSequence(proteinsList[ii].Sequence, maxMissedCleavages, null))
                        {
                            if (digestedPeptide.Sequence.Length < dbDigestion.MinSequenceLength)
                            {
                                continue;
                            }
                            var truncatedSequence = digestedPeptide.Sequence.Substring(
                                0, Math.Min(digestedPeptide.Sequence.Length, dbDigestion.MaxSequenceLength));
                            if (!existingSequences.Contains(truncatedSequence))
                            {
                                proteinSequences.Add(truncatedSequence);
                            }
                        }
                    });
                }

                // Now write to db
                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_AddFastaFile_Saving_changes, 50))
                {
                    return null;
                }
                bool committed = true;
                int digestedPeptideIdsCount;
                try
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(dbDigestion);

                        Dictionary<String, long> digestedPeptideIds
                            = new Dictionary<string, long>();
                        const String sqlPeptide =
                                "INSERT INTO ProteomeDbDigestedPeptide (Digestion, Sequence) VALUES(?,?);select last_insert_rowid();"; // Not L10N
                        using (var commandPeptide = session.Connection.CreateCommand())
                        using (var commandProtein = session.Connection.CreateCommand())
                        {
                            commandPeptide.CommandText = sqlPeptide;
                            commandPeptide.Parameters.Add(new SQLiteParameter());
                            commandPeptide.Parameters.Add(new SQLiteParameter());
                            const String sqlPeptideProtein =
                                "INSERT INTO ProteomeDbDigestedPeptideProtein (Peptide, Protein) VALUES(?,?);"; // Not L10N
                            commandProtein.CommandText = sqlPeptideProtein;
                            commandProtein.Parameters.Add(new SQLiteParameter());
                            commandProtein.Parameters.Add(new SQLiteParameter());
                            commandProtein.Parameters.Add(new SQLiteParameter());
                            for (int i = 0; i < proteinCount; i++)
                            {
                                var protein = proteinsList[i];
                                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, string.Format(Resources.ProteomeDb_Digest_Digesting__0__proteins, proteinCount), 50 * (proteinCount + i) / proteinCount))
                                {
                                    return null;
                                }
                                foreach (var truncatedSequence in truncatedSequences[i])
                                {
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
                        try
                        {
                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            committed = false;
                        }
                        digestedPeptideIdsCount = digestedPeptideIds.Count;
                    }
                }
                catch (Exception)
                {
                    if (!committed)
                    {
                        return null; // Interrupted
                    }
                    else
                    {
                        throw;
                    }
                }
                if (committed && !delayDbIndexing)
                {
                    AnalyzeDb(session); // This runs asynchronously, and interferes with writes
                }
                if (committed)
                {
                    progressMonitor.UpdateProgress(new ProgressStatus(string.Format(Resources.ProteomeDb_Digest_Digested__0__proteins_into__1__unique_peptides,proteinCount, digestedPeptideIdsCount)).ChangePercentComplete(100));
                }
                return committed ? new Digestion(this, dbDigestion) : null;
            }
        }

        //
        // Minimal amount of protein info sufficient for peptide uniqueness checks
        //
        public class MinimalProteinInfo
        {
            public string Sequence;
            public string Id;
            public string Gene;
            public string Species;
        }

        public IEnumerable<MinimalProteinInfo> GetMinimalProteinInfo()
        {
            using (var session = OpenSession())
            {
                using (IDbCommand command = session.Connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Sequence, Protein, Gene, Species \n" + // Not L10N
                        "FROM ProteomeDbProtein \n" + // Not L10N
                        "INNER JOIN ProteomeDbProteinName ON ProteomeDbProteinName.Protein = ProteomeDbProtein.Id \n" + // Not L10N
                        "WHERE ProteomeDbProteinName.IsPrimary <> 0"; // Not L10N
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        yield return new MinimalProteinInfo
                        {
                            Sequence = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            Id = reader.GetInt64(1).ToString(),
                            Gene = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Species = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                        };
                    }
                    yield return null; // Done
                }
            }
        }

        /// <summary>
        /// Access the web to resolve protein metadata not directly found in fasta file.
        /// The fasta text importer will have left search hints in ProteinMetadata.
        /// </summary>
        /// <param name="progressMonitor"></param>
        /// <param name="status"></param>
        /// <param name="fastaImporter">object that accesses the web, or pretends to if in a test</param>
        /// <param name="parseOnly">if true, attempt to parse protein metadata from descriptions but do not proceed to web access</param>
        /// <param name="done">will return true if there is nothung more to look up</param>
        /// <returns>true on success</returns>
        public bool LookupProteinMetadata(IProgressMonitor progressMonitor, ref ProgressStatus status, WebEnabledFastaImporter fastaImporter, bool parseOnly, out bool done)
        {
            var unsearchedProteins = new List<ProteinSearchInfo>();
            done = false;
            // If we're here, it's because the background loader is done digesting and has moved on to protein metadata,
            // or because the PeptideSettingsUI thread needs to have protein metadata resolved for uniqueness purposes before
            // it can proceed.   Either way, we should be working on a temp copy and be the only one needing write access, so get a lock now
            using (ISession session = OpenWriteSession())	// We may update the protdb file with web search results
            {
                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_LookupProteinMetadata_looking_for_unresolved_protein_details, 0))
                {
                    return false;
                }
                // get a list of proteins with unresolved metadata websearches
                var proteinNames = session.CreateCriteria(typeof (DbProteinName)).List<DbProteinName>().Where(x => x.WebSearchInfo.NeedsSearch()).ToList();
                var proteinsToSearch =
                    proteinNames.Where(proteinName => (proteinName.GetProteinMetadata().GetPendingSearchTerm().Length > 0))
                        .ToList();
                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_LookupProteinMetadata_looking_for_unresolved_protein_details, 0))
                    return false;
                // and a list of proteins which have never been considered for metadata search
                var untaggedProteins = proteinNames.Where(proteinName => proteinName.WebSearchInfo.IsEmpty()).ToList();

                foreach (var untaggedProtein in untaggedProteins)
                {
                    untaggedProtein.SetWebSearchCompleted(); // by default take this out of consideration for next time
                    var metadata = untaggedProtein.GetProteinMetadata();
                    if (metadata.HasMissingMetadata())
                    {
                        var search = fastaImporter.ParseProteinMetaData(metadata);
                        if (search!=null)
                        {
                            metadata = untaggedProtein.ChangeProteinMetadata(metadata.Merge(search)); // don't stomp name by accident
                            metadata = untaggedProtein.ChangeProteinMetadata(metadata.ChangeWebSearchInfo(search.WebSearchInfo));
                        }
                    }
                    if (metadata.NeedsSearch())
                        proteinsToSearch.Add(untaggedProtein); // add to the list of things to commit back to the db
                }
                // Get the lengths of the sequences without getting the sequences themselves, for best speed
                var proteinIds = proteinsToSearch.Select(name => name.Protein.Id.Value).Distinct().ToArray();
                var proteinLengths = new Dictionary<long, int>();
                using (var cmd = session.Connection.CreateCommand())
                {
                    string sql = "SELECT Id, LENGTH(Sequence) AS SequenceLength FROM ProteomeDbProtein P"; // Not L10N
                    if (proteinIds.Length < 1000)
                    {
                        sql += " WHERE P.Id IN (" + // Not L10N
                        string.Join(",", proteinIds) + ")"; // Not L10N
                    }
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetValue(0);
                            var len = reader.GetValue(1);
                            proteinLengths.Add(Convert.ToInt64(id), Convert.ToInt32(len));
                            if (proteinLengths.Count % 100 == 0)  // Periodic cancellation check
                            {
                                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, Resources.ProteomeDb_LookupProteinMetadata_looking_for_unresolved_protein_details, 0))
                                    return false;
                            }
                        }
                    }
                }
                foreach (var p in proteinsToSearch)
                {
                    int length;
                    proteinLengths.TryGetValue(p.Protein.Id.GetValueOrDefault(), out length);
                    unsearchedProteins.Add(new ProteinSearchInfo(p, length));
                }

                if (untaggedProteins.Any(untagged => !untagged.GetProteinMetadata().NeedsSearch())) // did any get set as unsearchable?
                {
                    // Write back the ones that were formerly without search terms, but which now indicate no search is possible
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (var untagged in untaggedProteins.Where(untagged => !untagged.GetProteinMetadata().NeedsSearch()))
                            session.SaveOrUpdate(untagged); // update the metadata
                        transaction.Commit();
                    }
                }

                if (unsearchedProteins.Any() && !parseOnly)
                {
                    int resultsCount = 0;
                    int unsearchedCount = unsearchedProteins.Count;
                    for (bool success = true; success;)
                    {
                        success = false; // Until we see at least one succeed this round
                        var results = new List<DbProteinName>();
                        if (progressMonitor.IsCanceled)
                            return false;

                        // The "true" arg means "do just one batch then return"
                        foreach (var result in fastaImporter.DoWebserviceLookup(unsearchedProteins, progressMonitor, true))
                        {
                            if (result != null)
                            {
                                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, string.Format(Resources.ProteomeDb_LookupProteinMetadata_Retrieving_details_for__0__proteins,
                                            unsearchedProteins.Count), 100*resultsCount++/unsearchedCount))
                                {
                                    return false;
                                }
                                success = true;
                                results.Add(result.ProteinDbInfo);
                            }
                        }
                        if (results.Any()) // save this batch
                        {
                            using (var transaction = session.BeginTransaction())
                            {
                                foreach (var result in results)
                                    session.SaveOrUpdate(result); 
                                transaction.Commit();
                            }
                        }
                        // Edit this list rather than rederive with database access
                        var hits = unsearchedProteins.Where(p => !p.GetProteinMetadata().NeedsSearch()).ToList();
                        foreach (var hit in hits)
                        {
                            unsearchedProteins.Remove(hit);
                        }
                    }
                }
                done = !unsearchedProteins.Any();
            } // End writesession
            return true;
        }
    }
}
