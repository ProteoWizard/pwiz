/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using pwiz.Common.Database.NHibernate;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    internal class PooledSqliteConnection : ConnectionId<SQLiteConnection>, IPooledStream
    {
        public PooledSqliteConnection(ConnectionPool connectionPool, string filePath) : base(connectionPool)
        {
            FilePath = filePath;
            FileTime = File.GetLastWriteTime(FilePath);
        }

        private string FilePath { get; set; }
        private DateTime FileTime { get; set; }

        protected override IDisposable Connect()
        {
            DbProviderFactory fact = new SQLiteFactory();
            SQLiteConnection conn = (SQLiteConnection) fact.CreateConnection();
            if (conn != null)
            {
                var connectionStringBuilder =
                    SessionFactoryFactory.SQLiteConnectionStringBuilderFromFilePath(FilePath);
                connectionStringBuilder.Version = 3;

                conn.ConnectionString = connectionStringBuilder.ToString();
                conn.Open();
            }
            return conn;
        }

        Stream IPooledStream.Stream
        {
            get { throw new InvalidOperationException(); }
        }

        public bool IsModified
        {
            get
            {
                // If it is still in the pool, then it can't have been modified
                return !IsOpen && !Equals(FileTime, File.GetLastWriteTime(FilePath));
            }
        }

        public string ModifiedExplanation
        {
            get
            {
                if (!IsModified)
                    return @"Unmodified";
                return FileEx.GetElapsedTimeExplanation(FileTime, File.GetLastWriteTime(FilePath));
            }
        }

        public bool IsOpen
        {
            get { return ConnectionPool.IsInPool(this); }
        }

        public void CloseStream()
        {
            Disconnect();
        }

        /// <summary>
        /// Invoke a function passing in the Connection while holding a lock on this.
        /// If an error happens, close the Connection, so that it will be reopened later.
        /// </summary>
        public T ExecuteWithConnection<T>(Func<SQLiteConnection, T> function)
        {
            lock (this)
            {
                try
                {
                    return function(Connection);
                }
                catch (SQLiteException x)
                {
                    // If an exception is thrown, close the stream in case the failure is something
                    // like a network failure that can be remedied by re-opening the stream.
                    CloseStream();
                    throw new IOException(string.Format(Resources.BiblioSpecLiteLibrary_ReadSpectrum_Unexpected_SQLite_failure_reading__0__,
                        FilePath), x);
                }
            }
        }
    }
}
