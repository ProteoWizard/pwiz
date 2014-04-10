/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Data.SQLite;
using System.Reflection;
using NHibernate;
using NHibernate.Cfg;

namespace pwiz.ProteomeDatabase.Util
{
    // TODO: Move to Common
    public static class SessionFactoryFactory
    {
        private const string DEFAULT_SCHEMA_FILENAME = "mapping.xml"; // Not L10N

        public static ISessionFactory CreateSessionFactory(String path, Type typeDb, bool createSchema)
        {
            return CreateSessionFactory(path, typeDb, DEFAULT_SCHEMA_FILENAME, createSchema);
        }

        public static ISessionFactory CreateSessionFactory(String path, Type typeDb, string schemaFilename, bool createSchema)
        {
            Configuration configuration = new Configuration()
                //.SetProperty("show_sql", "true")
                //.SetProperty("generate_statistics", "true")
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName) // Not L10N
                .SetProperty("connection.connection_string", SQLiteConnectionStringBuilderFromFilePath(path).ToString()) // Not L10N
                .SetProperty("connection.driver_class", // Not L10N
                typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create"); // Not L10N
            }
            configuration.SetProperty("connection.provider", // Not L10N
                typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            ConfigureMappings(configuration, typeDb, schemaFilename);
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }

        /// <summary>
        /// Returns a ConnectionStringBuilder with the datasource set to the specified path.  This method takes
        /// care of the special settings needed to work with UNC paths.
        /// </summary>
        public static SQLiteConnectionStringBuilder SQLiteConnectionStringBuilderFromFilePath(string path)
        {
            // when SQLite parses the connection string, it treats backslash as an escape character
            // This is not normally an issue, because backslashes followed by a non-reserved character
            // are not treated specially.

            // Also, in order to prevent a drive letter being prepended to UNC paths, we specify ToFullPath=false
            return new SQLiteConnectionStringBuilder
            {
                DataSource = path.Replace("\\", "\\\\"), // Not L10N
                ToFullPath = false,
            };
        }

        public static Configuration ConfigureMappings(Configuration configuration, Type typeDb, string schemaFilename = DEFAULT_SCHEMA_FILENAME)
        {
            Assembly assembly = typeDb.Assembly;
            return configuration.AddInputStream(
                assembly.GetManifestResourceStream(typeDb.Namespace + "." + schemaFilename)); // Not L10N
        }
    }
}
