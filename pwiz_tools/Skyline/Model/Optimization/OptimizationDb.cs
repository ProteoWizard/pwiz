/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Optimization
{
    public class OptimizationsOpeningException : Exception
    {
        public OptimizationsOpeningException(string message)
            : base(message)
        {
        }
    }

    public class OptimizationDb : Immutable, IValidating
    {
        public const string EXT = ".optdb"; // Not L10N

        public static string FILTER_OPTDB
        {
            get { return TextUtil.FileDialogFilter(Resources.OptimizationDb_FILTER_OPTDB_Optimization_Libraries, EXT); }
        }

        public const int SCHEMA_VERSION_CURRENT = 1;

        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;
        private ImmutableDictionary<OptimizationKey, double> _dictLibrary;

        public OptimizationDb(string path, ISessionFactory sessionFactory)
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
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<DbOptimization> GetOptimizations()
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                return session.CreateCriteria(typeof(DbOptimization)).List<DbOptimization>();
            }
        }

        #region Property change methods

        private OptimizationDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            var result = ChangeProp(ImClone(this), im => im.LoadOptimizations(im.GetOptimizations()));
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public OptimizationDb UpdateOptimizations(IList<DbOptimization> newOptimizations, IList<DbOptimization> oldOptimizations)
        {
            var setNew = new HashSet<long>(newOptimizations.Select(opt => opt.Id.HasValue ? opt.Id.Value : 0));
            var dictOld = oldOptimizations.ToDictionary(opt => opt.Key);

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                // Remove optimizations that are no longer in the list
                foreach (var optimization in oldOptimizations)
                {
                    if (!optimization.Id.HasValue)
                        continue;

                    if (!setNew.Contains(optimization.Id.Value))
                        session.Delete(optimization);
                }

                // Add or update optimizations that have changed from the old list
                foreach (var optimization in newOptimizations)
                {
                    DbOptimization optimizationOld;
                    if (dictOld.TryGetValue(optimization.Key, out optimizationOld) &&
                            Equals(optimization, optimizationOld))
                        continue;

                    // Create a new instance, because not doing this causes a BindingSource leak
                    var optimizationNewDisconnected = new DbOptimization(optimization);
                    session.SaveOrUpdate(optimizationNewDisconnected);
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.LoadOptimizations(newOptimizations));
        }

        public IDictionary<OptimizationKey, double> DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = new ImmutableDictionary<OptimizationKey, double>(value); }
        }

        private void LoadOptimizations(IEnumerable<DbOptimization> optimizations)
        {
            var dictLoad = new Dictionary<OptimizationKey, double>();

            foreach (var optimization in optimizations)
            {
                try
                {
                    dictLoad.Add(optimization.Key, optimization.Value);
                }
                catch (ArgumentException)
                {
                }
            }

            DictLibrary = dictLoad;
        }

        #endregion

        #region object overrides

        public bool Equals(OptimizationDb other)
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
            if (obj.GetType() != typeof(OptimizationDb)) return false;
            return Equals((OptimizationDb)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_path.GetHashCode() * 397) ^ _modifiedTime.GetHashCode();
            }
        }

        #endregion

        public static OptimizationDb CreateOptimizationDb(string path)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(OptimizationDb), true))
            {
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(new DbVersionInfo { SchemaVersion = SCHEMA_VERSION_CURRENT });
                    transaction.Commit();
                }
            }

            return GetOptimizationDb(path, null);
        }

        /// <summary>
        /// Maintains a single string globally for each path, in order to keep from
        /// having two threads accessing the same database at the same time.
        /// </summary>
        private static ISessionFactory GetSessionFactory(string path)
        {
            return SessionFactoryFactory.CreateSessionFactory(path, typeof(OptimizationDb), false);
        }

        //Throws DatabaseOpeningException
        public static OptimizationDb GetOptimizationDb(string path, IProgressMonitor loadMonitor)
        {
            var status = new ProgressStatus(string.Format(Resources.OptimizationDb_GetOptimizationDb_Loading_optimization_library__0_, path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new OptimizationsOpeningException(Resources.OptimizationDb_GetOptimizationDb_Library_path_cannot_be_null_);

                if (!File.Exists(path))
                    throw new OptimizationsOpeningException(String.Format(Resources.OptimizationDb_GetOptimizationDb_The_file__0__does_not_exist_, path));

                string message;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    using (var sessionFactory = GetSessionFactory(path))
                    {
                        lock (sessionFactory)
                        {
                            return new OptimizationDb(path, sessionFactory).Load(loadMonitor, status);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    message = string.Format(Resources.OptimizationDb_GetOptimizationDb_You_do_not_have_privilieges_to_access_the_file__0__, path);
                }
                catch (DirectoryNotFoundException)
                {
                    message = string.Format(Resources.OptimizationDb_GetOptimizationDb_The_path_containing__0__does_not_exist_, path);
                }
                catch (FileNotFoundException)
                {
                    message = string.Format(Resources.OptimizationDb_GetOptimizationDb_The_file__0__could_not_be_created__Perhaps_you_do_not_have_sufficient_privileges_, path);
                }
                catch (SQLiteException)
                {
                    message = string.Format(Resources.OptimizationDb_GetOptimizationDb_The_file__0__is_not_a_valid_optimization_library_file_, path);
                }
                catch (Exception e)
                {
                    message = string.Format(Resources.OptimizationDb_GetOptimizationDb_The_file__0__could_not_be_opened___1_, path, e.Message);
                }

                throw new OptimizationsOpeningException(message);
            }
            catch (OptimizationsOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                return null;
            }
        }
    }
}
