/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Tool.hbm2ddl;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public static class SessionFactoryFactory
    {
        public static Type GetDialectClass(DatabaseTypeEnum databaseTypeEnum)
        {
            switch (databaseTypeEnum)
            {
                case DatabaseTypeEnum.sqlite:
                    return typeof(SQLiteDialect);
                case DatabaseTypeEnum.mysql:
                    return typeof(MySQLDialect);
                case DatabaseTypeEnum.postgresql:
                    return typeof(PostgreSQL82Dialect);
            }
            throw new ArgumentException();
        }
        public static Type GetDriverClass(DatabaseTypeEnum databaseTypeEnum)
        {
            switch (databaseTypeEnum)
            {
                case DatabaseTypeEnum.sqlite:
                    return typeof(SQLite20Driver);
                case DatabaseTypeEnum.mysql:
                    return typeof(MySqlDataDriver);
                case DatabaseTypeEnum.postgresql:
                    return typeof(NpgsqlDriver);
            }
            throw new ArgumentException();
        }
        public static Configuration GetConfiguration(DatabaseTypeEnum databaseTypeEnum, SessionFactoryFlags flags)
        {
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            var configuration = new Configuration()
                .SetProperty("dialect", GetDialectClass(databaseTypeEnum).AssemblyQualifiedName)
                .SetProperty("connection.driver_class", GetDriverClass(databaseTypeEnum).AssemblyQualifiedName)
                .SetProperty("connection.provider",
                             typeof (NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                .SetProperty("command_timeout", "1800");
            if (databaseTypeEnum == DatabaseTypeEnum.mysql)
            {
                configuration.SetProperty("sessionVariables", "storage_engine=InnoDB");
            }
            configuration.AddInputStream(assembly.GetManifestResourceStream("pwiz.Topograph.Data.mapping.xml"));

            return configuration;
        }

        public static ISessionFactory CreateSessionFactory(String path, SessionFactoryFlags flags)
        {

            var configuration = GetConfiguration(DatabaseTypeEnum.sqlite, flags)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                                                                 {
                                                                     DataSource = path
                                                                 }.ToString());
            if (0 != (flags & SessionFactoryFlags.CreateSchema))
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            return configuration.BuildSessionFactory();
        }

        public static ISessionFactory CreateSessionFactory(TpgLinkDef tpgLinkDef, SessionFactoryFlags flags)
        {
            var configuration = GetConfiguration(tpgLinkDef.DatabaseTypeEnum, flags)
                .SetProperty("connection.connection_string", tpgLinkDef.GetConnectionString());
            var sessionFactory = configuration.BuildSessionFactory();
            if (0 != (flags & SessionFactoryFlags.CreateSchema))
            {
                using (var session = sessionFactory.OpenSession())
                {
                    var schemaExport = new SchemaExport(configuration);
                    if (DatabaseTypeEnum.mysql == tpgLinkDef.DatabaseTypeEnum)
                    {
                        session.CreateSQLQuery("SET storage_engine = 'InnoDB'").ExecuteUpdate();
                    }
                    schemaExport.Execute(false, true, false, session.Connection, null);
                }
            }
            return sessionFactory;
        }
    }
}
