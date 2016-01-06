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
                        if (!progressMonitor.Invoke(string.Format(Resources.ProteomeDb_AddFastaFile_Added__0__proteins,proteinCount), iProgress))
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
                    if (!progressMonitor.Invoke(Resources.ProteomeDb_AddFastaFile_Saving_changes, 99))
                    {
                        return;
                    }
                    transaction.Commit();
                }
                AnalyzeDb(session);
                progressMonitor.Invoke(
                    string.Format(Resources.ProteomeDb_AddFastaFile_Finished_importing__0__proteins, proteinCount), 100);
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
                command.CommandText = "Analyze;"; // Not L10N
                command.ExecuteNonQuery();
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
            // get a list of proteins with unresolved metadata websearches
            using (ISession session = OpenSession())
            {
                var dbProteinNames = session.CreateCriteria(typeof (DbProteinName)).List<DbProteinName>();
                // proteins with unresolved searches
                if (dbProteinNames.Any(proteinName => (proteinName.GetProteinMetadata().GetPendingSearchTerm().Length > 0)))
                    return true;
                // proteins which have never been considered for metadata search
                return dbProteinNames.Any(proteinName => proteinName.WebSearchInfo.IsEmpty());
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
                    .Add(Restrictions.Eq("Name", name)) // Not L10N
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
                        if (!progressMonitor.Invoke(Resources.ProteomeDb_Digest_Listing_existing_peptides, 0))
                        {
                            return null;
                        }
                        IQuery query = session.CreateQuery("SELECT P.Sequence FROM " // Not L10N
                                                           + typeof(DbDigestedPeptide) + " P WHERE P.Digestion = :Digestion") // Not L10N
                            .SetParameter("Digestion", dbDigestion); // Not L10N
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
                    if (!progressMonitor.Invoke(Resources.ProteomeDb_Digest_Listing_proteins, 0)) 
                    {
                        return null;
                    }
                    List<DbProtein> proteins = new List<DbProtein>();
                    session.CreateCriteria(typeof(DbProtein)).List(proteins);
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
                        for (int i = 0; i < proteins.Count; i++)
                        {
                            var proteinSequences = new HashSet<string>();
                            if (!progressMonitor.Invoke(string.Format(Resources.ProteomeDb_Digest_Digesting__0__proteins,proteins.Count), 100 * i / proteins.Count))
                            {
                                return null;
                            }
                            Protein protein = new Protein(ProteomeDbPath, proteins[i]);

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
                    if (!progressMonitor.Invoke(Resources.ProteomeDb_AddFastaFile_Saving_changes, 99))
                    {
                        return null;
                    }
                    transaction.Commit();

                    AnalyzeDb(session);
                    progressMonitor.Invoke(
                        string.Format(Resources.ProteomeDb_Digest_Digested__0__proteins_into__1__unique_peptides,
                                      proteins.Count, digestedPeptideIds.Count),
                        100);
                }
                return new Digestion(this, dbDigestion);
            }
        }

        /// <summary>
        /// Access the web to resolve protein metadata not directly found in fasta file.
        /// The fasta text importer will have left search hints in ProteinMetadata.
        /// </summary>
        /// <param name="progressMonitor"></param>
        /// <param name="fastaImporter">object that accesses the web, or pretends to if in a test</param>
        /// <param name="polite">if true, don't try to resolve everything in one go, assume we can come back later</param>
        /// <returns>true on success</returns>
        public bool LookupProteinMetadata(ProgressMonitor progressMonitor, WebEnabledFastaImporter fastaImporter, bool polite = false)
        {
            var unsearchedProteins = new List<ProteinSearchInfo>();
            List<DbProteinName> untaggedProteins;
            using (ISession session = OpenSession())
            {
                if (!progressMonitor.Invoke(Resources.ProteomeDb_LookupProteinMetadata_looking_for_unresolved_protein_details, 0))
                {
                    return false;
                }

                // get a list of proteins with unresolved metadata websearches
                var proteinNames = session.CreateCriteria(typeof (DbProteinName)).List<DbProteinName>();
                var proteinsToSearch =
                    proteinNames.Where(proteinName => (proteinName.GetProteinMetadata().GetPendingSearchTerm().Length > 0))
                        .ToList();
                // and a list of proteins which have never been considered for metadata search
                untaggedProteins =
                    proteinNames.Where(proteinName => proteinName.WebSearchInfo.IsEmpty()).ToList();

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
                        }
                    }
                }
                foreach (var p in proteinsToSearch)
                {
                    int length;
                    proteinLengths.TryGetValue(p.Protein.Id.GetValueOrDefault(), out length);
                    unsearchedProteins.Add(new ProteinSearchInfo(p, length));
                }
            }

            if (untaggedProteins.Any(untagged => !untagged.GetProteinMetadata().NeedsSearch())) // did any get set as unsearchable?
            {
                // Write back the ones that were formerly without search terms, but which now indicate no search is possible
                using (ISession session = OpenWriteSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (var untagged in untaggedProteins.Where(untagged => !untagged.GetProteinMetadata().NeedsSearch()))
                            session.SaveOrUpdate(untagged); // update the metadata
                        transaction.Commit();
                    }
                }
            }

            if (unsearchedProteins.Any())
            {
                int resultsCount = 0;
                int unsearchedCount = unsearchedProteins.Count;
                for (bool success = true; success;)
                {
                    success = false; // Until we see at least one succeed this round
                    var results = new List<DbProteinName>();

                    // The "true" arg means "do just one batch then return"
                    foreach (var result in fastaImporter.DoWebserviceLookup(unsearchedProteins, null, true))
                    {
                        if (result != null)
                        {
                            if (
                            !progressMonitor.Invoke(
                                string.Format(
                                    Resources.ProteomeDb_LookupProteinMetadata_Retrieving_details_for__0__proteins,
                                    unsearchedProteins.Count), 100 * resultsCount++ / unsearchedCount))
                            {
                                return false;
                            }
                            success = true;
                            results.Add(result.ProteinDbInfo);
                        }
                    }
                    if (results.Any()) // save this batch
                    {
                        using (var session = OpenWriteSession())
                        {
                            using (var transaction = session.BeginTransaction())
                            {
                                foreach (var result in results)
                                    session.SaveOrUpdate(result); 
                                transaction.Commit();
                                session.Close();
                            }
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
            return true;
        }
    }
}
