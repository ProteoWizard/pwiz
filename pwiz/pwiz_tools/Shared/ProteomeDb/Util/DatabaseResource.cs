/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Threading;
using NHibernate;
using pwiz.ProteomeDatabase.API;

namespace pwiz.ProteomeDatabase.Util
{
    /// <summary>
    /// Holds the SessionFactory for the ProteomeDb objects that all use the same database file.
    /// Is reference counted, and disposes the SessionFactory when the last reference is released.
    /// </summary>
    internal class DatabaseResource
    {
        private static IDictionary<string, DatabaseResource> _dbResources;
        private static readonly object DatabaseResourcesLock = typeof (DatabaseResource);

        public static DatabaseResource GetDbResource(string path)
        {
            lock (DatabaseResourcesLock)
            {
                DatabaseResource databaseResource;
                if (_dbResources != null && _dbResources.TryGetValue(path, out databaseResource))
                {
                    databaseResource.AddRef();
                    return databaseResource;
                }
                databaseResource = new DatabaseResource(path);
                if (null == _dbResources)
                {
                    _dbResources = new Dictionary<string, DatabaseResource>();
                }
                _dbResources.Add(path, databaseResource);
                return databaseResource;
            }
        }

        private int _refCount;
        private DatabaseResource(string path)
        {
            Path = path;
            _refCount = 1;
            SessionFactory = SessionFactoryFactory.CreateSessionFactory(path, ProteomeDb.TYPE_DB, false);
            DatabaseLock = new ReaderWriterLock();
        }
        public string Path { get; private set; }
        public ISessionFactory SessionFactory { get; private set; }
        public ReaderWriterLock DatabaseLock { get; private set; }

        public void Release()
        {
            int refCount = Interlocked.Decrement(ref _refCount);
            if (0 != refCount)
            {
                return;
            }
            SessionFactory.Dispose();
            lock (DatabaseResourcesLock)
            {
                _dbResources.Remove(Path);
                if (_dbResources.Count == 0)
                {
                    _dbResources = null;
                }
            }
        }

        private void AddRef()
        {
            Interlocked.Increment(ref _refCount);
        }
    }
}
