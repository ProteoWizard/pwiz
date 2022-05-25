/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using NHibernate;
using pwiz.Common.Database;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using NHibernate.Tool.hbm2ddl;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public class DatabaseOpeningException : CalculatorException
    {
        public DatabaseOpeningException(string message) : base(message)
        {
        }

        public DatabaseOpeningException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class IrtDb : Immutable, IValidating
    {
        public const string EXT = ".irtdb";

        public static string FILTER_IRTDB => TextUtil.FileDialogFilter(Resources.IrtDb_FILTER_IRTDB_iRT_Database_Files, EXT);

        public const int SCHEMA_VERSION_CURRENT = 1;

        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;
        private TargetMap<double> _dictStandards;
        private TargetMap<double> _dictLibrary;

        private IrtDb(string path, ISessionFactory sessionFactory)
        {
            _path = path;
            _sessionFactory = sessionFactory;
            _databaseLock = new ReaderWriterLock();
        }

        public void Validate()
        {
            // Set the modified time to the modified time for the database path
            try
            {
                _modifiedTime = File.GetLastWriteTime(_path);
            }
            catch (Exception)
            {
                _modifiedTime = new DateTime();
            }

            double min = double.MaxValue, minNext = double.MaxValue;
            foreach (var score in PeptideScores.Select(score => score.Value))
            {
                if (score < min)
                {
                    minNext = min;
                    min = score;
                }
                else if (min < score && score < minNext)
                {
                    minNext = score;
                }
            }
            if (min == double.MaxValue)
                UnknownScore = 0;
            else if (minNext == double.MaxValue)
                UnknownScore = min - 5;
            else
                UnknownScore = min - (minNext - min)*2;
        }

        public double UnknownScore { get; private set; }

        public IEnumerable<KeyValuePair<Target, double>> PeptideScores => DictStandards.Keys.Concat(DictLibrary.Keys)
            .Select(target => new KeyValuePair<Target, double>(target, ScoreSequence(target).Value));

        private IDictionary<Target, double> DictStandards
        {
            get => _dictStandards;
            set => _dictStandards = new TargetMap<double>(value);
        }

        private IDictionary<Target, double> DictLibrary
        {
            get => _dictLibrary;
            set => _dictLibrary = new TargetMap<double>(value);
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<Target> StandardPeptides => DictStandards.Keys;

        public bool IsStandard(Target seq)
        {
            return DictStandards.ContainsKey(seq);
        }

        public int StandardPeptideCount => DictStandards.Count;

        public IEnumerable<Target> LibraryPeptides => DictLibrary.Keys;

        public int LibraryPeptideCount => DictLibrary.Count;

        public bool Redundant {get; private set; }

        public string DocumentXml { get; private set; }

        public IrtRegressionType RegressionType { get; private set; }

        public double? ScoreSequence(Target seq)
        {
            return seq != null && (DictStandards.TryGetValue(seq, out var irt) || DictLibrary.TryGetValue(seq, out irt))
                ? (double?)irt
                : null;
        }

        public IList<DbIrtPeptide> GetPeptides()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                return session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>();
            }
        }

        public IList<DbIrtHistorical> GetHistory()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                return SqliteOperations.TableExists(session.Connection, @"IrtHistory")
                    ? session.CreateCriteria(typeof(DbIrtHistorical)).List<DbIrtHistorical>()
                    : null;
            }
        }

        public string GetDocumentXml()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                if (!SqliteOperations.TableExists(session.Connection, @"DocumentXml"))
                    return null;

                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT Xml FROM DocumentXml";
                    return Convert.ToString(cmd.ExecuteScalar());
                }
            }
        }

        public IrtRegressionType GetRegressionType()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                if (!SqliteOperations.TableExists(session.Connection, @"DocumentXml") ||
                    !SqliteOperations.ColumnExists(session.Connection, @"DocumentXml", @"RegressionType"))
                    return null;

                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT RegressionType FROM DocumentXml";
                    return IrtRegressionType.FromName(Convert.ToString(cmd.ExecuteScalar()));
                }
            }
        }

        #region Property change methods

        private IrtDb Load(IProgressMonitor loadMonitor, IProgressStatus status, out IList<DbIrtPeptide> dbPeptides)
        {
            var rawPeptides = dbPeptides = GetPeptides();
            var history = GetHistory();
            var result = ChangeProp(ImClone(this), im =>
            {
                im.LoadPeptides(rawPeptides, history);
                im.Redundant = history != null;
                im.DocumentXml = GetDocumentXml();
                im.RegressionType = GetRegressionType();
            });
            // Not really possible to show progress, unless we switch to raw reading
            loadMonitor?.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public IrtDb UpdatePeptides(IList<DbIrtPeptide> newPeptides, bool redundant, IProgressMonitor monitor = null)
        {
            IProgressStatus status = new ProgressStatus(Resources.IrtDb_UpdatePeptides_Updating_peptides);
            return UpdatePeptides(newPeptides, redundant, monitor, ref status);
        }

        public IrtDb UpdatePeptides(IList<DbIrtPeptide> newPeptides, bool redundant, IProgressMonitor monitor, ref IProgressStatus status)
        {
            if (redundant && !Redundant)
            {
                // Create the IrtHistory table
                var config = SessionFactoryFactory.GetConfiguration(_path, typeof(IrtDb), false);
                new SchemaUpdate(config).Execute(false, true);
            }

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                if (!redundant)
                    SqliteOperations.DropTable(session.Connection, @"IrtHistory");

                var newTargets = newPeptides.Select(pep => pep.ModifiedTarget).ToHashSet();
                var existingPeps = new Dictionary<Target, DbIrtPeptide>();
                foreach (var pep in session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>())
                {
                    // Remove peptides that are no longer in the list
                    if (!newTargets.Contains(pep.ModifiedTarget))
                    {
                        session.Delete(pep);
                        continue;
                    }

                    if (!existingPeps.TryGetValue(pep.ModifiedTarget, out var tryGetPep))
                    {
                        existingPeps[pep.ModifiedTarget] = pep;
                    }
                    else if (pep.Standard && !tryGetPep.Standard)
                    {
                        // Delete library peptide if it is also a standard
                        session.Delete(tryGetPep);
                        existingPeps[pep.ModifiedTarget] = pep;
                    }
                }

                var existingHistories = new Dictionary<long, List<double>>();
                if (redundant)
                {
                    foreach (var history in session.CreateCriteria(typeof(DbIrtHistorical)).List<DbIrtHistorical>())
                    {
                        if (!existingHistories.TryGetValue(history.PeptideId, out var list))
                            existingHistories.Add(history.PeptideId, new List<double> { history.Irt });
                        else
                            list.Add(history.Irt);
                    }
                }

                // Add or update peptides that have changed from the old list
                var i = 0;
                foreach (var pep in newPeptides)
                {
                    if (monitor != null)
                    {
                        if (monitor.IsCanceled)
                            return null;
                        monitor.UpdateProgress(status = status.ChangePercentComplete(i++ * 100 / newPeptides.Count));
                    }
                    
                    if (existingPeps.TryGetValue(pep.ModifiedTarget, out var pepExisting))
                    {
                        pep.Id = pepExisting.Id;
                        if (Equals(pep, pepExisting))
                            continue;

                        if (redundant && !pep.Standard && !Equals(pep.Irt, pepExisting.Irt))
                        {
                            var history = new DbIrtHistorical(pep.Id.Value, pepExisting.Irt);
                            session.Save(history);
                        }
                        pepExisting.Irt = pep.Irt;
                        pepExisting.Standard = pep.Standard;
                        pepExisting.TimeSource = pep.TimeSource;
                        session.Update(pepExisting);
                    }
                    else
                    {
                        // Create a new instance, because not doing this causes a BindingSource leak
                        var pepDisconnected = new DbIrtPeptide(pep) { Id = null };
                        pep.Id = (long?)session.Save(pepDisconnected);
                    }
                }

                if (redundant)
                    RemoveUnusedHistories(session);

                transaction.Commit();
            }

            monitor?.UpdateProgress(status.Complete());
            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides, GetHistory()));
        }

        private void LoadPeptides(ICollection<DbIrtPeptide> peptides, IEnumerable<DbIrtHistorical> histories)
        {
            var dictStandards = new Dictionary<Target, double>();
            var dictLibrary = new Dictionary<Target, double>(peptides.Count);

            var dictHistory = new Dictionary<long, List<double>>();
            foreach (var history in histories ?? Enumerable.Empty<DbIrtHistorical>())
            {
                if (!dictHistory.TryGetValue(history.PeptideId, out var list))
                    dictHistory.Add(history.PeptideId, new List<double> { history.Irt });
                else
                    list.Add(history.Irt);
            }

            foreach (var pep in peptides)
            {
                var dict = pep.Standard ? dictStandards : dictLibrary;
                try
                {
                    // Unnormalized modified sequences will not match anything.  The user interface
                    // attempts to enforce only normalized modified sequences, but this extra protection
                    // handles irtdb files created before normalization was implemented, or edited outside
                    // Skyline.
                    var irt = pep.Irt;
                    if (!pep.Standard && pep.Id.HasValue && dictHistory.TryGetValue(pep.Id.Value, out var hist))
                        irt = new Statistics(hist.Append(irt)).Median();
                    dict.Add(pep.GetNormalizedModifiedSequence(), irt);
                }
                catch (ArgumentException)
                {
                }
            }

            DictStandards = dictStandards;
            DictLibrary = dictLibrary;
        }

        #endregion

        #region object overrides

        public bool Equals(IrtDb other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other._path, _path) &&
                   other._modifiedTime.Equals(_modifiedTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(IrtDb)) return false;
            return Equals((IrtDb)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_path.GetHashCode() * 397) ^ _modifiedTime.GetHashCode();
            }
        }

        #endregion

        public static IrtDb CreateIrtDb(string path)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(IrtDb), true))
            {
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
                using (var transaction = session.BeginTransaction())
                {
                    // Newly created IrtDbs start without IrtHistory table
                    SqliteOperations.DropTable(session.Connection, @"IrtHistory");

                    session.Save(new DbVersionInfo { SchemaVersion = SCHEMA_VERSION_CURRENT });
                    transaction.Commit();
                }
            }

            return GetIrtDb(path, null);
        }

        /// <summary>
        /// Maintains a single string globally for each path, in order to keep from
        /// having two threads accessing the same database at the same time.
        /// </summary>
        private static ISessionFactory GetSessionFactory(string path)
        {
            return SessionFactoryFactory.CreateSessionFactory(path, typeof(IrtDb), false);
        }

        //Throws DatabaseOpeningException
        public static IrtDb GetIrtDb(string path, IProgressMonitor loadMonitor)
        {
            return GetIrtDb(path, loadMonitor, out _);
        }

        public static IrtDb GetIrtDb(string path, IProgressMonitor loadMonitor, out IList<DbIrtPeptide> dbPeptides)
        {
            var status = new ProgressStatus(string.Format(Resources.IrtDb_GetIrtDb_Loading_iRT_database__0_, path));
            loadMonitor?.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new DatabaseOpeningException(Resources.IrtDb_GetIrtDb_Database_path_cannot_be_null);

                if (!File.Exists(path))
                    throw new DatabaseOpeningException(string.Format(Resources.IrtDb_GetIrtDb_The_file__0__does_not_exist_, path));

                string message;
                Exception xInner;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    using (var sessionFactory = GetSessionFactory(path))
                    {
                        lock (sessionFactory)
                        {
                            return new IrtDb(path, sessionFactory).Load(loadMonitor, status, out dbPeptides);
                        }
                    }
                }
                catch (UnauthorizedAccessException x)
                {
                    message = string.Format(Resources.IrtDb_GetIrtDb_You_do_not_have_privileges_to_access_the_file__0_, path);
                    xInner = x;
                }
                catch (DirectoryNotFoundException x)
                {
                    message = string.Format(Resources.IrtDb_GetIrtDb_The_path_containing__0__does_not_exist, path);
                    xInner = x;
                }
                catch (FileNotFoundException x)
                {
                    message = string.Format(Resources.IrtDb_GetIrtDb_The_file__0__could_not_be_created_Perhaps_you_do_not_have_sufficient_privileges, path);
                    xInner = x;
                }
                catch (SQLiteException x)
                {
                    message = string.Format(Resources.IrtDb_GetIrtDb_The_file__0__is_not_a_valid_iRT_database_file, path);
                    xInner = x;
                }
                catch (Exception x)
                {
                    message = string.Format(Resources.IrtDb_GetIrtDb_The_file__0__could_not_be_opened, path);
                    xInner = x;
                }

                throw new DatabaseOpeningException(message, xInner);
            }
            catch (DatabaseOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                dbPeptides = Array.Empty<DbIrtPeptide>();
                return null;
            }
        }

        public static string GenerateDocumentXml(IEnumerable<Target> standards, SrmDocument doc, string oldXml)
        {
            if (doc == null)
                return null;

            // Minimize document to only the peptides we need
            var minimalPeptides = standards.ToHashSet();

            var oldPeptides = new Dictionary<Target, PeptideDocNode>();
            if (!string.IsNullOrEmpty(oldXml))
            {
                try
                {
                    using (var reader = new StringReader(oldXml))
                    {
                        var oldDoc = (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(reader);
                        oldPeptides = oldDoc.Molecules.Where(pep => minimalPeptides.Contains(pep.Target)).ToDictionary(pep => pep.Target, pep => pep);
                    }
                }
                catch
                {
                    // ignored
                }
            }

            var addPeptides = new List<PeptideDocNode>();
            foreach (var nodePep in doc.Molecules.Where(pep => minimalPeptides.Contains(pep.Target)))
            {
                if (oldPeptides.TryGetValue(nodePep.Target, out var nodePepOld))
                {
                    addPeptides.Add(nodePep.Merge(nodePepOld));
                    oldPeptides.Remove(nodePep.Target);
                }
                else
                {
                    addPeptides.Add(nodePep);
                }
            }
            addPeptides.AddRange(oldPeptides.Values);

            var peptides = new List<PeptideDocNode>();
            foreach (var nodePep in addPeptides)
            {
                var precursors = new List<DocNode>();
                foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                {
                    var transitions = nodeTranGroup.Transitions.Where(tran => tran.ResultsRank.HasValue)
                        .OrderBy(tran => tran.ResultsRank.Value)
                        .Select(tran => tran.ChangeResults(null))
                        .Cast<DocNode>().ToList();
                    if (transitions.Count > 0)
                        precursors.Add(nodeTranGroup.ChangeResults(null).ChangeChildren(transitions));
                }
                if (precursors.Count > 0)
                {
                    peptides.Add((PeptideDocNode)nodePep.ChangeResults(null).ChangeChildren(precursors));
                }
            }
            if (peptides.Count == 0)
                return null;

            // Clear some settings to make the document smaller and so that they won't get imported into a document
            doc = doc.ChangeMeasuredResults(null);
            doc = (SrmDocument)doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), Resources.IrtDb_MakeDocumentXml_iRT_standards, string.Empty,
                    Array.Empty<PeptideDocNode>())
            });
            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(libs => libs.ChangeLibraries(new List<LibrarySpec>(), new List<Library>())));

            peptides.Sort((nodePep1, nodePep2) => nodePep1.ModifiedTarget.CompareTo(nodePep2.ModifiedTarget));
            doc = (SrmDocument)doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY,
                    Resources.IrtDb_MakeDocumentXml_iRT_standards, string.Empty, peptides.ToArray(), false)
            });

            using (var writer = new StringWriter())
            using (var writer2 = new XmlTextWriter(writer))
            {
                doc.Serialize(writer2, null, SkylineVersion.CURRENT, null);
                return writer.ToString();
            }
        }

        public IrtDb SetDocumentXml(SrmDocument doc, string oldXml)
        {
            var documentXml = GenerateDocumentXml(StandardPeptides, doc, oldXml);

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                EnsureDocumentXmlTable(session);

                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"UPDATE DocumentXml SET Xml = ?";
                    cmd.Parameters.Add(new SQLiteParameter { Value = documentXml });
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.DocumentXml = documentXml);
        }

        public IrtDb SetRegressionType(IrtRegressionType regressionType)
        {
            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                EnsureDocumentXmlTable(session);

                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE DocumentXml SET RegressionType = ?";
                    cmd.Parameters.Add(new SQLiteParameter { Value = regressionType.Name });
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.RegressionType = regressionType);
        }

        private static void EnsureDocumentXmlTable(ISession session)
        {
            if (!SqliteOperations.TableExists(session.Connection, @"DocumentXml"))
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS DocumentXml (Xml TEXT, RegressionType TEXT)";
                    cmd.ExecuteNonQuery();
                }
            }
            else if (!SqliteOperations.ColumnExists(session.Connection, @"DocumentXml", @"RegressionType"))
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"ALTER TABLE DocumentXml ADD COLUMN RegressionType TEXT";
                    cmd.ExecuteNonQuery();
                }
            }

            // create row if it doesn't exist
            bool rowExists;
            using (var cmd = session.Connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT COUNT(*) FROM DocumentXml";
                rowExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            if (!rowExists)
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO DocumentXml (Xml, RegressionType) VALUES (?, ?)";
                    cmd.Parameters.Add(new SQLiteParameter { Value = null });
                    cmd.Parameters.Add(new SQLiteParameter { Value = null });
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static HashSet<Target> CheckForDuplicates(IEnumerable<DbIrtPeptide> standards, IEnumerable<DbIrtPeptide> library)
        {
            return standards.Select(pep => pep.ModifiedTarget)
                .Intersect(library.Select(pep => pep.ModifiedTarget)).ToHashSet();
        }

        public IrtDb RemoveDuplicateLibraryPeptides()
        {
            using (var session = OpenWriteSession())
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM IrtLibrary WHERE Standard = 0 and PeptideModSeq IN (SELECT PeptideModSeq FROM IrtLibrary WHERE Standard = 1)";
                    cmd.ExecuteNonQuery();
                }
                RemoveUnusedHistories(session);
            }
            return ChangeProp(ImClone(this), im => { im.LoadPeptides(GetPeptides(), GetHistory()); });
        }

        private static void RemoveUnusedHistories(ISession session)
        {
            if (SqliteOperations.TableExists(session.Connection, @"IrtHistory"))
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM IrtHistory WHERE PeptideId NOT IN (SELECT Id FROM IrtLibrary WHERE Standard = 0)";
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
