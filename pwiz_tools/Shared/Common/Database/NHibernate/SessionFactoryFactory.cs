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
using NHibernate;
using NHibernate.Cfg;

namespace pwiz.Common.Database.NHibernate
{
    public static class SessionFactoryFactory
    {
        private const string DEFAULT_SCHEMA_FILENAME = "mapping.xml";

        public static ISessionFactory CreateSessionFactory(string path, Type typeDb, bool createSchema)
        {
            return CreateSessionFactory(path, typeDb, DEFAULT_SCHEMA_FILENAME, createSchema);
        }

        public static ISessionFactory CreateSessionFactory(string path, Type typeDb, string schemaFilename, bool createSchema)
        {
            return GetConfiguration(path, typeDb, schemaFilename, createSchema).BuildSessionFactory();
        }

        public static Configuration GetConfiguration(string path, Type typeDb, bool createSchema)
        {
            return GetConfiguration(path, typeDb, DEFAULT_SCHEMA_FILENAME, createSchema);
        }

        public static Configuration GetConfiguration(string path, Type typeDb, string schemaFilename, bool createSchema)
        {
            var configuration = new Configuration()
                //.SetProperty("show_sql", "true")
                //.SetProperty("generate_statistics", "true")
                .SetProperty(@"dialect", typeof(global::NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty(@"connection.connection_string", SQLiteConnectionStringBuilderFromFilePath(path).ToString())
                .SetProperty(@"connection.driver_class", typeof(global::NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                .SetProperty(@"connection.provider", typeof(global::NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            if (createSchema)
                configuration.SetProperty(@"hbm2ddl.auto", @"create");
            return ConfigureMappings(configuration, typeDb, schemaFilename);
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
                // ReSharper disable LocalizableElement
                DataSource = path.Replace("\\", "\\\\"),
                // ReSharper restore LocalizableElement
                ToFullPath = false,
            };
        }

        public static Configuration ConfigureMappings(Configuration configuration, Type typeDb, string schemaFilename = DEFAULT_SCHEMA_FILENAME)
        {
            var assembly = typeDb.Assembly;
            configuration.SetDefaultAssembly(assembly.FullName);
            configuration.SetDefaultNamespace(typeDb.Namespace);
            using (var stream = assembly.GetManifestResourceStream(typeDb.Namespace + @"." + schemaFilename))
                return configuration.AddInputStream(stream);
        }
    }
}
