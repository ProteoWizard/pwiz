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

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using NHibernate;
using pwiz.Common.Database;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

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

        private IrtDb(String path, ISessionFactory sessionFactory)
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
            foreach (var score in Scores)
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

        public IEnumerable<KeyValuePair<Target, double>> PeptideScores
        {
            get { return new[] {DictStandards, DictLibrary}.SelectMany(dict => dict); }
        }

        private IEnumerable<double> Scores
        {
            get { return new[] {DictStandards, DictLibrary}.SelectMany(dict => dict.Values); }
        }

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

        public string DocumentXml { get; private set; }

        public IrtRegressionType RegressionType { get; private set; }

        public double? ScoreSequence(Target seq)
        {
            if (seq != null && (DictStandards.TryGetValue(seq, out var irt) || DictLibrary.TryGetValue(seq, out irt)))
                return irt;
            return null;
        }

        public IList<DbIrtPeptide> GetPeptides()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock, false, CancellationToken.None))
            {
                return session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>();
            }
        }

        public string GetDocumentXml()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock,
                false, CancellationToken.None))
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
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock,
                false, CancellationToken.None))
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

        private IrtDb Load(IProgressMonitor loadMonitor, ProgressStatus status, out IList<DbIrtPeptide> dbPeptides)
        {
            var rawPeptides = dbPeptides = GetPeptides();
            var result = ChangeProp(ImClone(this), im =>
            {
                im.LoadPeptides(rawPeptides);
                im.DocumentXml = GetDocumentXml();
                im.RegressionType = GetRegressionType();
            });
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public IrtDb AddPeptides(IProgressMonitor monitor, IList<DbIrtPeptide> newPeptides)
        {
            IProgressStatus status = new ProgressStatus(Resources.IrtDb_AddPeptides_Adding_peptides);
            return AddPeptides(monitor, newPeptides, ref status);
        }

        public IrtDb AddPeptides(IProgressMonitor monitor, IList<DbIrtPeptide> newPeptides, ref IProgressStatus status)
        {
            var total = newPeptides.Count;
            var i = 0;
            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                foreach (var peptideNewDisconnected in newPeptides.Select(peptideNew => new DbIrtPeptide(peptideNew) {Id = null}))
                {
                    session.SaveOrUpdate(peptideNewDisconnected);
                    if (monitor != null)
                    {
                        if (monitor.IsCanceled)
                            return null;
                        monitor.UpdateProgress(status = status.ChangePercentComplete(++i * 100 / total));
                    }
                }

                transaction.Commit();
            }

            monitor?.UpdateProgress(status.Complete());

            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides));
        }

        public IrtDb UpdatePeptides(IList<DbIrtPeptide> newPeptides, IList<DbIrtPeptide> oldPeptides)
        {
            var setNew = new HashSet<long>(newPeptides.Select(pep => pep.Id.HasValue ? pep.Id.Value : 0));
            var dictOld = oldPeptides.ToDictionary(pep => pep.ModifiedTarget);

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                // Remove peptides that are no longer in the list
                foreach (var peptideOld in oldPeptides)
                {
                    if (!peptideOld.Id.HasValue)
                        continue;

                    if (!setNew.Contains(peptideOld.Id.Value))
                        session.Delete(peptideOld);
                }

                // Add or update peptides that have changed from the old list
                foreach (var peptideNew in newPeptides)
                {
                    if (dictOld.TryGetValue(peptideNew.ModifiedTarget, out var peptideOld) && Equals(peptideNew, peptideOld))
                        continue;

                    // Create a new instance, because not doing this causes a BindingSource leak
                    var peptideNewDisconnected = new DbIrtPeptide(peptideNew);
                    session.SaveOrUpdate(peptideNewDisconnected);
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides));
        }

        private void LoadPeptides(IList<DbIrtPeptide> peptides)
        {
            var dictStandards = new Dictionary<Target, double>();
            var dictLibrary = new Dictionary<Target, double>(peptides.Count);

            foreach (var pep in peptides)
            {
                var dict = pep.Standard ? dictStandards : dictLibrary;
                try
                {
                    // Unnormalized modified sequences will not match anything.  The user interface
                    // attempts to enforce only normalized modified sequences, but this extra protection
                    // handles irtdb files created before normalization was implemented, or edited outside
                    // Skyline.
                    dict.Add(pep.GetNormalizedModifiedSequence(), pep.Irt);
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
            if (obj.GetType() != typeof (IrtDb)) return false;
            return Equals((IrtDb) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_path.GetHashCode()*397) ^ _modifiedTime.GetHashCode();
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
                    throw new DatabaseOpeningException(String.Format(Resources.IrtDb_GetIrtDb_The_file__0__does_not_exist_, path));

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
                dbPeptides = new DbIrtPeptide[0];
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
                    peptides.Add((PeptideDocNode) nodePep.ChangeResults(null).ChangeChildren(precursors));
                }
            }
            if (peptides.Count == 0)
                return null;

            // Clear some settings to make the document smaller and so that they won't get imported into a document
            doc = doc.ChangeMeasuredResults(null);
            doc = (SrmDocument)doc.ChangeChildren(new[] { new PeptideGroupDocNode(new PeptideGroup(), Resources.IrtDb_MakeDocumentXml_iRT_standards, string.Empty, new PeptideDocNode[0]) });
            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(libs => libs.ChangeLibraries(new List<LibrarySpec>(), new List<Library>())));

            peptides.Sort((nodePep1, nodePep2) => nodePep1.ModifiedTarget.CompareTo(nodePep2.ModifiedTarget));
            doc = (SrmDocument) doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, Resources.IrtDb_MakeDocumentXml_iRT_standards, string.Empty, peptides.ToArray(), false)
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
                    cmd.Parameters.Add(new SQLiteParameter {Value = null});
                    cmd.Parameters.Add(new SQLiteParameter {Value = null});
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
                return ChangeProp(ImClone(this), im =>
                {
                    im.LoadPeptides(session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>());
                });
            }
        }
    }
}
