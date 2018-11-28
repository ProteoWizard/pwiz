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
using NHibernate.Criterion;
using pwiz.Common.Database;
using pwiz.Common.Database.NHibernate;
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
        public const string EXT_PROTDB = ".protdb";

        internal static readonly Type TYPE_DB = typeof(DbVersionInfo);
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
        public const int MAX_SEQUENCE_LENGTH = 6;
        private DatabaseResource _databaseResource;
        private readonly bool _isTmp; // We won't hang onto the global session factory if we know we're temporary

        private ProteomeDb(String path, CancellationToken cancellationToken, bool isTmp)
        {
            _schemaVersionMajor = -1; // unknown
            _schemaVersionMinor = -1; // unknown
            _schemaVersionMajorAsRead = -1; // unknown
            _schemaVersionMinorAsRead = -1; // unknown
            _isTmp = isTmp;
            if (!File.Exists(path))
            {
                // Do not try to open the file if it does not exist, because that would create a zero byte file.
                throw new FileLoadException(String.Format(Resources.ProteomeDb_ProteomeDb_The_file__0__does_not_exist_, path));
            }
            _databaseResource = DatabaseResource.GetDbResource(path);
            CancellationToken = cancellationToken;
            using (var session = OpenSession())
            {
                // Is this even a proper protDB file? (https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/thread.view?rowId=14893)
                if (!SqliteOperations.TableExists(session.Connection, @"ProteomeDbProteinName"))
                {
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

        private bool UpdateProgressAndCheckForCancellation(IProgressMonitor progressMonitor, ref IProgressStatus status, string message, int pctComplete)
        {
            if (progressMonitor.IsCanceled)
                return false;
            if (pctComplete != status.PercentComplete)
                progressMonitor.UpdateProgress(status = status.ChangeMessage(message).ChangePercentComplete(pctComplete));
            return true;
        }

        public CancellationToken CancellationToken { get; private set; }

        // Close all file handles
        public void CloseDbConnection()
        {
            _databaseResource.SessionFactory.Close();
        }

        private void ReadVersion(ISession session)
        {
            // do we even have a version? 0th-gen protdb doesn't have this.
            if (!SqliteOperations.TableExists(session.Connection, @"ProteomeDbSchemaVersion"))
            {
                _schemaVersionMajor = SCHEMA_VERSION_MAJOR_0; // an ancient, unversioned file
                _schemaVersionMinor = SCHEMA_VERSION_MINOR_0; // an ancient, unversioned file
            }
            else
            {
                using (IDbCommand cmd2 = session.Connection.CreateCommand())
                {
                    cmd2.CommandText = @"SELECT SchemaVersionMajor FROM ProteomeDbSchemaVersion";
                    var obj2 = cmd2.ExecuteScalar();
                    _schemaVersionMajor = Convert.ToInt32(obj2);
                }
                using (IDbCommand cmd3 = session.Connection.CreateCommand())
                {
                    cmd3.CommandText = @"SELECT SchemaVersionMinor FROM ProteomeDbSchemaVersion";
                    var obj3 = cmd3.ExecuteScalar();
                    _schemaVersionMinor = Convert.ToInt32(obj3);
                }
            }
            _schemaVersionMajorAsRead = _schemaVersionMajor;
            _schemaVersionMinorAsRead = _schemaVersionMinor;
        }

        public bool IsDigested()
        {
            using (var session = OpenSession())
            {
                return CheckHasSubsequenceTable(session.Connection);
            }
        }

        public void Digest(IProgressMonitor progressMonitor, ref IProgressStatus progressStatus)
        {
            try
            {
                using (var session = OpenStatelessSession(true))
                {
                    using (var transation = session.BeginTransaction())
                    {
                        var noNames = new DbProteinName[0];
                        var proteinSequences =
                            session.CreateQuery(@"SELECT P.Sequence, P.Id FROM " + typeof(DbProtein) + @" P")
                                .List<object[]>()
                                .ToDictionary(row => (string) row[0], row => new ProtIdNames((long) row[1], noNames));

                        if (!HasSubsequencesTable(() => session.Connection))
                        {
                            session.CreateSQLQuery(
                                    @"CREATE TABLE ProteomeDbSubsequence (Sequence TEXT not null, ProteinIdBytes BLOB, primary key (Sequence));")
                                .ExecuteUpdate();
                        }
                        DigestProteins(session.Connection, proteinSequences, progressMonitor, ref progressStatus);
                        if (progressMonitor.IsCanceled)
                        {
                            return;
                        }
                        transation.Commit();
                    }
                }
            }
            catch (Exception)
            {
                // If the operation was cancelled, then we want to throw OperationCancelledException instead of whatever we caught
                CancellationToken.ThrowIfCancellationRequested();
                // Otherwise, throw the original exception
                throw;
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
                            new[] {@"PreferredName", @"Accession", @"Gene", @"Species", @"WebSearchStatus"})
                        // new protein metadata for v2
                    {
                        command.CommandText =
                            String.Format(@"ALTER TABLE ProteomeDbProteinName ADD COLUMN {0} TEXT", col);
                        command.ExecuteNonQuery();
                    }
                    command.CommandText =
                        @"CREATE TABLE ProteomeDbSchemaVersion (Id integer primary key autoincrement, SchemaVersionMajor INT, SchemaVersionMinor INT )";
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
            var session = new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, false, CancellationToken);
            return session;
        }

        public IStatelessSession OpenStatelessSession(bool write)
        {
            return new StatelessSessionWithLock(SessionFactory.OpenStatelessSession(), DatabaseLock, write, CancellationToken);
        }

        public ISession OpenWriteSession()
        {
            var session = new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, true, CancellationToken);
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

        public void AddFastaFile(StreamReader reader, IProgressMonitor progressMonitor, ref IProgressStatus status, bool delayAnalyzeDb)
        {
            int duplicateSequenceCount;
            AddFastaFile(reader, progressMonitor, ref status, delayAnalyzeDb, out duplicateSequenceCount);
        }

        public void AddFastaFile(StreamReader reader, IProgressMonitor progressMonitor, ref IProgressStatus status,
            bool delayAnalyzeDb, out int duplicateSequenceCount)
        {
            Dictionary<string, ProtIdNames> proteinIds = new Dictionary<string, ProtIdNames>();
            using (IStatelessSession session = SessionFactory.OpenStatelessSession()) // This is a long session, but there's no harm since db is useless till its done
            {
                var proteinNames = session.CreateCriteria<DbProteinName>().List<DbProteinName>().ToLookup(name => name.Id.Value);
                foreach (DbProtein protein in session.CreateCriteria(typeof(DbProtein)).List())
                {
                    if (protein.Id.HasValue)
                        proteinIds.Add(protein.Sequence, new ProtIdNames(protein.Id.Value, proteinNames[protein.Id.Value].ToArray()));
                }
                int proteinCount = 0;
                duplicateSequenceCount = 0;
                using (var transaction = session.BeginTransaction())
                using (IDbCommand insertProtein = session.Connection.CreateCommand())
                using (IDbCommand insertName = session.Connection.CreateCommand())
                {
                    WebEnabledFastaImporter fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.DelayedWebSearchProvider()); // just parse, no search for now
                    insertProtein.CommandText =
                        @"INSERT INTO ProteomeDbProtein (Version, Sequence) Values (1,?);select last_insert_rowid();";
                    insertProtein.Parameters.Add(new SQLiteParameter());
                    insertName.CommandText =
                        @"INSERT INTO ProteomeDbProteinName (Version, Protein, IsPrimary, Name, Description, PreferredName, Accession, Gene, Species, WebSearchStatus) Values(1,?,?,?,?,?,?,?,?,?)";
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
                            duplicateSequenceCount++;
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

                    if (HasSubsequencesTable(() => session.Connection))
                    {
                        DigestProteins(session.Connection, proteinIds, progressMonitor, ref status);
                    }
                    if (progressMonitor.IsCanceled)
                    {
                        return;
                    }
                    transaction.Commit();
                }
                if (!delayAnalyzeDb)
                {
                    AnalyzeDb(session.Connection); // NB This runs asynchronously and may interfere with further writes
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
        private static void AnalyzeDb(IDbConnection connection)
        {
            using(IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"Analyze;";
                command.ExecuteNonQuery();
            }
        }

        public void AnalyzeDb()
        {
            using (var session = OpenWriteSession())
            {
                AnalyzeDb(session.Connection);
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
                        session.CreateQuery(@"SELECT Count(P.Id) From " + typeof(DbProtein) + @" P").UniqueResult());
            }
        }

        private ISessionFactory SessionFactory { get { return _databaseResource.SessionFactory; } }
        public static ProteomeDb OpenProteomeDb(String path, bool isTemporary=false)
        {
            return new ProteomeDb(path, CancellationToken.None, isTemporary);
        }

        public static ProteomeDb OpenProteomeDb(String path, CancellationToken cancellationToken)
        {
            return new ProteomeDb(path, cancellationToken, false);
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

        internal static bool CheckHasSubsequenceTable(IDbConnection connection)
        {
            return SqliteOperations.TableExists(connection, @"ProteomeDbSubsequence");
        }

        private bool? _hasSubsequencesTable;
        internal bool HasSubsequencesTable(Func<IDbConnection> getConnectionFunc)
        {
            if (!_hasSubsequencesTable.HasValue)
            {
                _hasSubsequencesTable = CheckHasSubsequenceTable(getConnectionFunc());
            }
            return _hasSubsequencesTable.Value;
        }

        public bool HasProteinNamesWithUnresolvedMetadata()
        {
            // Get a list of proteins with unresolved metadata websearches
            using (var session = OpenSession())
            {
                var hql = @"SELECT WebSearchStatus FROM " + typeof (DbProteinName);
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
                    .Add(Restrictions.InsensitiveLike(@"Name", prefix + @"%"))
                    .AddOrder(Order.Asc(@"Name"))
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
                .Add(Restrictions.Eq(@"Name", searchName));
            DbProteinName proteinName = (DbProteinName)criteriaName.UniqueResult();
            if (proteinName != null)
                return proteinName;
            string[] hints = {@"Accession", @"Gene", @"PreferredName"};
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

        public Digestion GetDigestion()
        {
            return new Digestion(this);
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool DigestProteins(IDbConnection connection, IDictionary<String, ProtIdNames> protSequences, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            progressMonitor.UpdateProgress(status = status.ChangeMessage(Resources.ProteomeDb_DigestProteins_Analyzing_protein_sequences));
            Dictionary<String, byte[]> subsequenceProteinIds = new Dictionary<string, byte[]>();
            foreach (var entry in protSequences)
            {
                if (progressMonitor.IsCanceled)
                {
                    return false;
                }
                long id = entry.Value.Id;
                byte[] idBytes = DbSubsequence.ProteinIdsToBytes(new[] {id});
                String proteinSequence = entry.Key;
                HashSet<string> subsequences = new HashSet<string>();
                for (int ich = 0; ich < proteinSequence.Length - MIN_SEQUENCE_LENGTH; ich++)
                {
                    String subsequence = proteinSequence.Substring(ich,
                        Math.Min(proteinSequence.Length - ich, MAX_SEQUENCE_LENGTH));
                    if (subsequences.Add(subsequence))
                    {
                        byte[] idListBytes;
                        if (!subsequenceProteinIds.TryGetValue(subsequence, out idListBytes))
                        {
                            subsequenceProteinIds.Add(subsequence, idBytes);
                        }
                        else
                        {
                            var bytesNew = new byte[idListBytes.Length + idBytes.Length];
                            Array.Copy(idListBytes, 0, bytesNew, 0, idListBytes.Length);
                            Array.Copy(idBytes, 0, bytesNew, idListBytes.Length, idBytes.Length);
                            subsequenceProteinIds[subsequence] = bytesNew;
                        }
                    }
                }
            }
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"DELETE FROM ProteomeDbSubsequence";
                cmd.ExecuteNonQuery();
            }
            var tuples = subsequenceProteinIds.ToArray();
            Array.Sort(tuples, (t1, t2) => StringComparer.Ordinal.Compare(t1.Key, t2.Key));
            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = @"INSERT INTO ProteomeDbSubsequence (Sequence, ProteinIdBytes) VALUES(?,?)";
                insertCommand.Parameters.Add(new SQLiteParameter());
                insertCommand.Parameters.Add(new SQLiteParameter());
                for (int iTuple = 0; iTuple < tuples.Length; iTuple++)
                {
                    if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status,
                        Resources.ProteomeDb_AddFastaFile_Saving_changes, iTuple*100/tuples.Length))
                    {
                        return false;
                    }
                    var tuple = tuples[iTuple];
                    // Now write to db
                    DbSubsequence dbSubsequence = new DbSubsequence
                    {
                        Sequence = tuple.Key,
                        ProteinIdBytes = tuple.Value
                    };
                    ((SQLiteParameter) insertCommand.Parameters[0]).Value = dbSubsequence.Sequence;
                    ((SQLiteParameter) insertCommand.Parameters[1]).Value = dbSubsequence.ProteinIdBytes;
                    insertCommand.ExecuteNonQuery();
                }
            }
            return true;
        }

        //
        // Minimal amount of protein info sufficient for peptide uniqueness checks
        //
        public class MinimalProteinInfo
        {
            public string Sequence;
            public long Id;
            public string Gene;
            public string Species;
        }

        public IEnumerable<MinimalProteinInfo> GetMinimalProteinInfo()
        {
            using (var session = OpenSession())
            {
                // ReSharper disable LocalizableElement
                var hql = "SELECT p.Sequence, p.Id, pn.Gene, pn.Species \n" +
                          "FROM " + typeof(DbProtein) + " p, " + typeof(DbProteinName) + " pn\n" +
                          // ReSharper restore LocalizableElement
                          @"WHERE p.Id = pn.Protein.Id AND pn.IsPrimary <> 0";
                var query = session.CreateQuery(hql);
                foreach (object[] values in query.List())
                {
                    yield return new MinimalProteinInfo
                    {
                        Sequence = (string)values[0],
                        Id = (long)values[1],
                        Gene = (string)values[2],
                        Species = (string)values[3]
                    };
                }
                yield return null; // Done
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
        public bool LookupProteinMetadata(IProgressMonitor progressMonitor, ref IProgressStatus status, WebEnabledFastaImporter fastaImporter, bool parseOnly, out bool done)
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
                    string sql = @"SELECT Id, LENGTH(Sequence) AS SequenceLength FROM ProteomeDbProtein P";
                    if (proteinIds.Length < 1000)
                    {
                        sql += @" WHERE P.Id IN (" +
                        string.Join(@",", proteinIds) + @")";
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
                                string message = string.Format(Resources.ProteomeDb_LookupProteinMetadata_Retrieving_details_for__0__proteins,
                                                               unsearchedProteins.Count);
                                // Make it clearer when web access is faked during testing
                                if (fastaImporter.IsAccessFaked)
                                    message = @"FAKED: " + message;
                                if (!UpdateProgressAndCheckForCancellation(progressMonitor, ref status, message, 100*resultsCount++/unsearchedCount))
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

        internal IList<Protein> GetProteinsWithIds(IStatelessSession session, IList<long> proteinIds)
        {
            if (proteinIds.Count == 0)
            {
                return new Protein[0];
            }

            return BatchUpArgumentsForFunction(ids =>
            {
                // ReSharper disable LocalizableElement
                var hql = "SELECT p, pn"
                  + "\nFROM " + typeof(DbProtein) + " p, " + typeof(DbProteinName) + " pn"
                  + "\nWHERE p.Id = pn.Protein.Id AND p.Id IN (:Ids)";
                var query = session.CreateQuery(hql);
                query.SetParameterList("Ids", ids);
                // ReSharper restore LocalizableElement
                var proteins = new List<Protein>();

                var rowsByProteinId = query.List().Cast<object[]>().ToLookup(row => ((DbProtein) row[0]).Id.Value);
                foreach (var grouping in rowsByProteinId)
                {
                    var protein = (DbProtein) grouping.First()[0];
                    var names = grouping.Select(row => row[1]).Cast<DbProteinName>();
                    proteins.Add(new Protein(ProteomeDbPath, protein, names));
                }
                return proteins;
            }, proteinIds, 1000);
        }


        internal IList<TResult> BatchUpArgumentsForFunction<TArg, TResult>(Func<ICollection<TArg>, IEnumerable<TResult>> function, 
            IList<TArg> arguments, int maxBatchSize)
        {
            var processedArgumentCount = 0;
            List<TResult> results = new List<TResult>();
            for (var batchSize = Math.Min(maxBatchSize, arguments.Count); ; )  // A retry loop in case our query overwhelms SQLite
            {
                try
                {
                    while (processedArgumentCount < arguments.Count)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        results.AddRange(function(arguments.Skip(processedArgumentCount).Take(batchSize).ToArray()));
                        processedArgumentCount += batchSize;
                    }
                    return results;
                }
                catch (Exception)
                {
                    // Failed - probably due to too-large query
                    batchSize /= 2;
                    if (batchSize < 1)
                    {
                        throw;
                    }
                }
            } // End dynamic query size loop
        }
    }
}
