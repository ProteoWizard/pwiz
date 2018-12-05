﻿/*
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
using NHibernate;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
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

        public static string FILTER_IRTDB
        {
            get { return TextUtil.FileDialogFilter(Resources.IrtDb_FILTER_IRTDB_iRT_Database_Files, EXT); }
        }

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
            get { return _dictStandards; }
            set { _dictStandards = new TargetMap<double>(value); }
        }

        private IDictionary<Target, double> DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = new TargetMap<double>(value); }
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<Target> StandardPeptides
        {
            get { return DictStandards.Keys; }
        }

        public bool IsStandard(Target seq)
        {
            return DictStandards.ContainsKey(seq);
        }

        public int StandardPeptideCount
        {
            get { return DictStandards.Count; }
        }

        public IEnumerable<Target> LibraryPeptides
        {
            get { return DictLibrary.Keys; }
        }

        public int LibraryPeptideCount
        {
            get { return DictLibrary.Count; }
        }

        public double? ScoreSequence(Target seq)
        {
            double irt;
            if (seq != null && (DictStandards.TryGetValue(seq, out irt) || DictLibrary.TryGetValue(seq, out irt)))
                return irt;
            return null;
        }

        public IList<DbIrtPeptide> GetPeptides()
        {
            using (var session = new StatelessSessionWithLock(_sessionFactory.OpenStatelessSession(), _databaseLock,
                    false, CancellationToken.None))
            {
                return session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>();
            }
        }

        #region Property change methods

        private IrtDb Load(IProgressMonitor loadMonitor, ProgressStatus status, out IList<DbIrtPeptide> dbPeptides)
        {
            var rawPeptides = dbPeptides = GetPeptides();
            var result = ChangeProp(ImClone(this), im => im.LoadPeptides(rawPeptides));
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
            if (monitor != null)
                monitor.UpdateProgress(status.Complete());

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
                    DbIrtPeptide peptideOld;
                    if (dictOld.TryGetValue(peptideNew.ModifiedTarget, out peptideOld) &&
                            Equals(peptideNew, peptideOld))
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
            IList<DbIrtPeptide> dbPeptides;
            return GetIrtDb(path, loadMonitor, out dbPeptides);
        }

        public static IrtDb GetIrtDb(string path, IProgressMonitor loadMonitor, out IList<DbIrtPeptide> dbPeptides)
        {
            var status = new ProgressStatus(string.Format(Resources.IrtDb_GetIrtDb_Loading_iRT_database__0_, path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new DatabaseOpeningException(Resources.IrtDb_GetIrtDb_Database_path_cannot_be_null);

                if (!File.Exists(path))
                    throw new DatabaseOpeningException(String.Format(Resources.IrtDb_GetIrtDb_The_file__0__does_not_exist_, path));

                string message;
                Exception xInner = null;
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
    }
}
