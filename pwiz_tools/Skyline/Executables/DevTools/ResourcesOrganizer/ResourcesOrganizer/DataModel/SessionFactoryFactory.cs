/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    public static class SessionFactoryFactory
    {
        public static ISessionFactory CreateSessionFactory(string filePath, bool createSchema)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath
            }.ToString();
            var cfg = new Configuration()
                .SetProperty(@"dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty(@"connection.connection_string", connectionString)
                .SetProperty(@"connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                .SetProperty(@"connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            var hbmSerializer = new HbmSerializer
            {
                Validate = true
            };
            cfg.AddInputStream(hbmSerializer.Serialize(typeof(Entity).Assembly));
            if (createSchema)
            {
                cfg.SetProperty(@"hbm2ddl.auto", @"create");
            }
            return cfg.BuildSessionFactory();
        }
    }
}
