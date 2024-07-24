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

        public static string FILTER_IRTDB => TextUtil.FileDialogFilter(IrtResources.IrtDb_FILTER_IRTDB_iRT_Database_Files, EXT);

        public const int SCHEMA_VERSION_CURRENT = 2;

        private readonly string _path;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;
        private TargetMap<double> _dictStandards;
        private TargetMap<TargetInfo> _dictLibrary;

        private IrtDb(string path)
        {
            _path = path;
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

        public IEnumerable<KeyValuePair<Target, double>> PeptideScores => DictStandards.Concat(
            DictLibrary.Select(kvp => new KeyValuePair<Target, double>(kvp.Key, kvp.Value.Irt)));

        private IDictionary<Target, double> DictStandards
        {
            get => _dictStandards;
            set => _dictStandards = new TargetMap<double>(value);
        }

        private IDictionary<Target, TargetInfo> DictLibrary
        {
            get => _dictLibrary;
            set => _dictLibrary = new TargetMap<TargetInfo>(value);
        }

        private ISession OpenWriteSession(ISessionFactory sessionFactory)
        {
            return new SessionWithLock(sessionFactory.OpenSession(), _databaseLock, true);
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
            if (seq == null)
                return null;
            if (DictStandards.TryGetValue(seq, out var irt))
                return irt;
            if (DictLibrary.TryGetValue(seq, out var info))
                return info.Irt;
            return null;
        }

        public IEnumerable<double> GetHistory(Target seq)
        {
            return seq != null && DictLibrary.TryGetValue(seq, out var info) ? info.PrevIrts : null;
        }

        public bool TryGetLatestHistory(Target seq, out double history)
        {
            if (seq != null && DictLibrary.TryGetValue(seq, out var info))
            {
                var latest = info.LatestIrt;
                if (latest.HasValue)
                {
                    history = latest.Value;
                    return true;
                }
            }
            history = 0;
            return false;
        }

        public IList<DbIrtPeptide> ReadPeptides()
        {
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = new StatelessSessionWithLock(sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                return session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>();
            }
        }

        public IList<DbIrtHistory> ReadHistories()
        {
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = new StatelessSessionWithLock(sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                return SqliteOperations.TableExists(session.Connection, @"IrtHistory")
                    ? session.CreateCriteria(typeof(DbIrtHistory)).List<DbIrtHistory>()
                    : null;
            }
        }

        public string ReadDocumentXml()
        {
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = new StatelessSessionWithLock(sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
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

        public IrtRegressionType ReadRegressionType()
        {
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = new StatelessSessionWithLock(sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
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
            var rawPeptides = dbPeptides = ReadPeptides();
            var history = ReadHistories();
            var result = ChangeProp(ImClone(this), im =>
            {
                im.LoadPeptides(rawPeptides, history);
                im.Redundant = history != null;
                im.DocumentXml = ReadDocumentXml();
                im.RegressionType = ReadRegressionType();
            });
            // Not really possible to show progress, unless we switch to raw reading
            loadMonitor?.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public IrtDb UpdatePeptides(ICollection<DbIrtPeptide> newPeptides, IProgressMonitor monitor = null)
        {
            var docType = newPeptides.Any(p => !(p?.Target?.IsProteomic ?? true))
                ? SrmDocument.DOCUMENT_TYPE.mixed
                : SrmDocument.DOCUMENT_TYPE.proteomic;

            var msg = Helpers.PeptideToMoleculeTextMapper.Translate(IrtResources.IrtDb_UpdatePeptides_Updating_peptides, docType); // Perform "peptide"->"molecule" translation as needed

            IProgressStatus status = new ProgressStatus(msg);
            monitor?.UpdateProgress(status);
            return UpdatePeptides(newPeptides, monitor, ref status);
        }

        public IrtDb UpdatePeptides(ICollection<DbIrtPeptide> newPeptides, IProgressMonitor monitor, ref IProgressStatus status)
        {
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = OpenWriteSession(sessionFactory))
            using (var transaction = session.BeginTransaction())
            {
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

                if (Redundant)
                    RemoveUnusedHistories(session);

                transaction.Commit();
            }

            monitor?.UpdateProgress(status = status.Complete());
            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides, Redundant ? ReadHistories() : null));
        }

        public IrtDb AddHistories(IDictionary<Target, double> histories)
        {
            if (histories == null || histories.Count == 0)
                return this;
            else if (!Redundant)
                throw new InvalidOperationException();

            var saveTime = new TimeStampISO8601();

            if (!(histories is TargetMap<double>))
                histories = new TargetMap<double>(histories.Select(h => new KeyValuePair<Target, double>(h.Key, h.Value)));

            var newDict = new Dictionary<Target, TargetInfo>(DictLibrary.Count);
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = OpenWriteSession(sessionFactory))
            using (var transaction = session.BeginTransaction())
            {
                foreach (var existing in DictLibrary)
                {
                    var info = existing.Value;
                    if (histories.TryGetValue(existing.Key, out var addHistory))
                    {
                        var histObj = new DbIrtHistory(info.DbId, addHistory, saveTime);
                        session.Save(histObj);

                        info = new TargetInfo(info, addHistory);
                    }
                    newDict[existing.Key] = info;
                }
                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => DictLibrary = newDict);
        }

        private void LoadPeptides(ICollection<DbIrtPeptide> peptides, IEnumerable<DbIrtHistory> histories)
        {
            var dictStandards = new Dictionary<Target, double>();
            var dictLibrary = new Dictionary<Target, TargetInfo>(peptides.Count);

            var dictHistory = new Dictionary<long, List<DbIrtHistory>>();
            foreach (var history in histories ?? Enumerable.Empty<DbIrtHistory>())
            {
                if (!dictHistory.TryGetValue(history.PeptideId, out var list))
                    dictHistory.Add(history.PeptideId, new List<DbIrtHistory> { history });
                else
                    list.Add(history);
            }

            foreach (var pep in peptides)
            {
                try
                {
                    // Unnormalized modified sequences will not match anything.  The user interface
                    // attempts to enforce only normalized modified sequences, but this extra protection
                    // handles irtdb files created before normalization was implemented, or edited outside
                    // Skyline.
                    if (pep.Standard)
                    {
                        dictStandards.Add(pep.GetNormalizedModifiedSequence(), pep.Irt);
                    }
                    else
                    {
                        dictHistory.TryGetValue(pep.Id.Value, out var hist);
                        dictLibrary.Add(pep.GetNormalizedModifiedSequence(), new TargetInfo(pep.Id.Value, pep.Irt, hist));
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            DictStandards = dictStandards;
            DictLibrary = dictLibrary;
        }

        public IrtDb SetDocumentXml(SrmDocument doc, string oldXml)
        {
            var documentXml = GenerateDocumentXml(StandardPeptides, doc, oldXml);

            using var sessionFactory = GetSessionFactory(_path);
            using (var session = OpenWriteSession(sessionFactory))
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
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = OpenWriteSession(sessionFactory))
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

        public IrtDb SetRedundant(bool redundant)
        {
            if (redundant == Redundant)
                return this;

            if (redundant)
            {
                // Create the IrtHistory table
                var config = SessionFactoryFactory.GetConfiguration(_path, typeof(IrtDb), false);
                new SchemaUpdate(config).Execute(false, true);
            }
            else
            {
                using var sessionFactory = GetSessionFactory(_path);
                using (var session = OpenWriteSession(sessionFactory))
                {
                    SqliteOperations.DropTable(session.Connection, @"IrtHistory");
                }
            }

            return ChangeProp(ImClone(this), im => im.Redundant = redundant);
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
            var status = new ProgressStatus(string.Format(IrtResources.IrtDb_GetIrtDb_Loading_iRT_database__0_, path));
            loadMonitor?.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new DatabaseOpeningException(IrtResources.IrtDb_GetIrtDb_Database_path_cannot_be_null);

                if (!File.Exists(path))
                    throw new DatabaseOpeningException(string.Format(IrtResources.IrtDb_GetIrtDb_The_file__0__does_not_exist_, path));

                string message;
                Exception xInner;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    return new IrtDb(path).Load(loadMonitor, status, out dbPeptides);
                }
                catch (UnauthorizedAccessException x)
                {
                    message = string.Format(IrtResources.IrtDb_GetIrtDb_You_do_not_have_privileges_to_access_the_file__0_, path);
                    xInner = x;
                }
                catch (DirectoryNotFoundException x)
                {
                    message = string.Format(IrtResources.IrtDb_GetIrtDb_The_path_containing__0__does_not_exist, path);
                    xInner = x;
                }
                catch (FileNotFoundException x)
                {
                    message = string.Format(IrtResources.IrtDb_GetIrtDb_The_file__0__could_not_be_created_Perhaps_you_do_not_have_sufficient_privileges, path);
                    xInner = x;
                }
                catch (SQLiteException x)
                {
                    message = string.Format(IrtResources.IrtDb_GetIrtDb_The_file__0__is_not_a_valid_iRT_database_file, path);
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
            using var sessionFactory = GetSessionFactory(_path);
            using (var session = OpenWriteSession(sessionFactory))
            {
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = @"DELETE FROM IrtLibrary WHERE Standard = 0 and PeptideModSeq IN (SELECT PeptideModSeq FROM IrtLibrary WHERE Standard = 1)";
                    cmd.ExecuteNonQuery();
                }
                RemoveUnusedHistories(session);
            }
            return ChangeProp(ImClone(this), im => { im.LoadPeptides(ReadPeptides(), ReadHistories()); });
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

        private class TargetInfo
        {
            public TargetInfo(long dbId, double irt, IEnumerable<DbIrtHistory> prevIrts)
            {
                DbId = dbId;
                Irt = irt;

                // SaveTimes are ISO 8601 strings, which can be sorted lexicographically.
                _prevIrts = prevIrts != null
                    ? new List<double>(prevIrts.OrderBy(hist => hist.SaveTime).Select(hist => hist.Irt))
                    : null;
            }

            public TargetInfo(TargetInfo other, double newIrt)
            {
                DbId = other.DbId;
                Irt = other.Irt;
                _prevIrts = new List<double>();
                if (other._prevIrts != null)
                    _prevIrts.AddRange(other._prevIrts);
                _prevIrts.Add(newIrt);
            }

            public long DbId { get; }
            public double Irt { get; }

            private readonly List<double> _prevIrts;
            public IEnumerable<double> PrevIrts => _prevIrts;
            public double? LatestIrt => _prevIrts != null && _prevIrts.Count > 0 ? _prevIrts.Last() : (double?)null;
        }
    }
}
