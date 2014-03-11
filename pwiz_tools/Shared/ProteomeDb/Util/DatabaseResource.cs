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
using System.Linq;
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
        private static bool _doDisposeAll;
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

        // Formerly we released the SessionFactory here, but these are large, expensive-to-construct
        // objects that leak a great deal of string space, so we'll hang onto them even if it risks 
        // carrying a bit of useless memory* until we call ReleaseAll at OnClosed.
        // This has a dramatic effect on memory consumption when the background proteinmetadata loader
        // has to repeatedly reestablish a connection to protDB as other higher priority threads
        // interrupt it.
        // *In practice this actually reduces memory consumption, especially in test runs where there 
        // are many short lived protDB instances - protDB creation etc, that are not part of the 
        // document and don't hang around a long time, but which then get added to the doc and 
        // could use the same session factory from their creation
        public void Release(bool isTemporary)
        {
            var refcount = Interlocked.Decrement(ref _refCount);
            if (refcount == 0)
            {
                lock (DatabaseResourcesLock)
                {
                    if (isTemporary || // We're certain we won't want this later
                        _doDisposeAll) // Our thread was still working when ReleaseAll() was called
                    {
                        SessionFactory.Dispose();
                        if ((_dbResources != null) && _dbResources.ContainsKey(Path))
                        {
                            _dbResources.Remove(Path);
                            if (_dbResources.Count == 0)
                                _dbResources = null;
                        }
                    }
                }
            }
        }

        public static void ReleaseAll() 
        {
            lock (DatabaseResourcesLock)
            {
                _doDisposeAll = true; // Stragglers on other threads should actually Dispose
                if (_dbResources != null)
                {
                    var paths = _dbResources.Keys.ToArray();
                    foreach (var path in paths)
                    {
                        var dbResource = _dbResources[path];
                        if (0 == dbResource._refCount)
                        {
                            dbResource.SessionFactory.Dispose();
                            _dbResources.Remove(path);
                        }
                    }
                    if (_dbResources.Count == 0)
                        _dbResources = null;
                }
            }
        }

        private void AddRef()
        {
            Interlocked.Increment(ref _refCount);
        }
    }

    public static class DatabaseResources
    {
        public static void ReleaseAll() 
        {
            DatabaseResource.ReleaseAll();
        }
    }
}
