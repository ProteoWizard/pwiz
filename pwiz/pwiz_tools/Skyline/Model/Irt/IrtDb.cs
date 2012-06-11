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
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Irt
{
    public class DatabaseOpeningException : CalculatorException
    {
        public DatabaseOpeningException(string message)
            : base(message)
        {
        }
    }

    public class IrtDb : Immutable, IValidating
    {
        public const string EXT = ".irtdb";

        public const int SCHEMA_VERSION_CURRENT = 1;

        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;
        private ImmutableDictionary<string, double> _dictStandards;
        private ImmutableDictionary<string, double> _dictLibrary;

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

        public IEnumerable<KeyValuePair<string, double>> PeptideScores
        {
            get { return new[] {DictStandards, DictLibrary}.SelectMany(dict => dict); }
        }

        private IEnumerable<double> Scores
        {
            get { return new[] {DictStandards, DictLibrary}.SelectMany(dict => dict.Values); }
        }

        private IDictionary<string, double> DictStandards
        {
            get { return _dictStandards; }
            set { _dictStandards = new ImmutableDictionary<string, double>(value); }
        }

        private IDictionary<string, double> DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = new ImmutableDictionary<string, double>(value);}
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<string> StandardPeptides
        {
            get { return DictStandards.Keys; }
        }

        public bool IsStandard(string seq)
        {
            return DictStandards.ContainsKey(seq);
        }

        public int StandardPeptideCount
        {
            get { return DictStandards.Count; }
        }

        public IEnumerable<string> LibraryPeptides
        {
            get { return DictLibrary.Keys; }
        }

        public int LibraryPeptideCount
        {
            get { return DictLibrary.Count; }
        }

        public double? ScoreSequence(string seq)
        {
            double irt;
            if (DictStandards.TryGetValue(seq, out irt) || DictLibrary.TryGetValue(seq, out irt))
                return irt;
            return null;
        }

        public IEnumerable<DbIrtPeptide> GetPeptides()
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                return session.CreateCriteria(typeof (DbIrtPeptide)).List<DbIrtPeptide>();
            }
        }

        #region Property change methods

        private IrtDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            var result = ChangeProp(ImClone(this), im => im.LoadPeptides(im.GetPeptides()));
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public IrtDb UpdatePeptides(IList<DbIrtPeptide> newPeptides, IList<DbIrtPeptide> oldPeptides)
        {
            var setNew = new HashSet<long>(newPeptides.Select(pep => pep.Id.HasValue ? pep.Id.Value : 0));
            var dictOld = oldPeptides.ToDictionary(pep => pep.PeptideModSeq);

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
                    if (dictOld.TryGetValue(peptideNew.PeptideModSeq, out peptideOld) &&
                            Equals(peptideNew, peptideOld))
                        continue;

                    session.SaveOrUpdate(peptideNew);
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides));
        }

        private void LoadPeptides(IEnumerable<DbIrtPeptide> peptides)
        {
            var dictStandards = new Dictionary<string, double>();
            var dictLibrary = new Dictionary<string, double>();

            foreach (var pep in peptides)
            {
                var dict = pep.Standard ? dictStandards : dictLibrary;
                dict.Add(pep.PeptideModSeq, pep.Irt);
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

        private static readonly Dictionary<string, ISessionFactory> DICT_PATH_SESSION_FACTORY =
            new Dictionary<string, ISessionFactory>();

        /// <summary>
        /// Maintains a single string globally for each path, in order to keep from
        /// having two threads accessing the same database at the same time.
        /// </summary>
        private static ISessionFactory GetSessionFactory(string path)
        {
            lock (DICT_PATH_SESSION_FACTORY)
            {
                ISessionFactory sessionFactory;
                if (!DICT_PATH_SESSION_FACTORY.TryGetValue(path, out sessionFactory))
                {
                    sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(IrtDb), false);
                    DICT_PATH_SESSION_FACTORY.Add(path, sessionFactory);
                }
                return sessionFactory;
            }
        }

        public static void ClearCache()
        {
            lock (DICT_PATH_SESSION_FACTORY)
            {
                foreach (var sessionFactory in DICT_PATH_SESSION_FACTORY.Values)
                    sessionFactory.Dispose();
                DICT_PATH_SESSION_FACTORY.Clear();
            }
        }

        //Throws DatabaseOpeningException
        public static IrtDb GetIrtDb(string path, IProgressMonitor loadMonitor)
        {
            var status = new ProgressStatus(string.Format("Loading iRT database {0}", path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new DatabaseOpeningException("Database path cannot be null");

                if (!File.Exists(path))
                    throw new DatabaseOpeningException(String.Format("The file {0} does not exist.", path));

                string message;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    var sessionFactory = GetSessionFactory(path);
                    lock (sessionFactory)
                    {
                        return new IrtDb(path, sessionFactory).Load(loadMonitor, status);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    message = String.Format("You do not have privileges to access the file {0}", path);
                }
                catch (DirectoryNotFoundException)
                {
                    message = String.Format("The path containing {0} does not exist", path);
                }
                catch (FileNotFoundException)
                {
                    message =
                        String.Format("The file {0} could not be created. Perhaps you do not have sufficient privileges.",
                                      path);
                }
                catch (SQLiteException)
                {
                    message = String.Format("The file {0} is not a valid iRT database file.", path);
                }
                catch (Exception)
                {
                    message = String.Format("The file {0} could not be opened.", path);
                }

                throw new DatabaseOpeningException(message);
            }
            catch (DatabaseOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                return null;
            }
        }
    }
}
