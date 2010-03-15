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
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping;
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
                .SetProperty("proxyfactory.factory_class",
                typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                .AddInputStream(assembly.GetManifestResourceStream("pwiz.Topograph.Data.mapping.xml"));
            if (0 == (flags & SessionFactoryFlags.remove_binary_columns))
            {
                var classMapping = configuration.GetClassMapping(typeof (DbPeptideFileAnalysis));
                var timesColumn = new Column("TimesBytes")
                                 {
                                     Length = 1000000,
                                     SqlType = "BinaryBlob",
                                 };
                classMapping.Table.AddColumn(timesColumn);
                var timesValue = new SimpleValue(classMapping.Table)
                                     {
                                         TypeName = "BinaryBlob"
                                     };
                timesValue.AddColumn(timesColumn);
                classMapping.AddProperty(new Property(timesValue)
                                             {
                                                 Name = "TimesBytes"
                                             });
                var scanIndexesColumn = new Column("ScanIndexesBytes")
                                 {
                                     Length = 1000000,
                                     SqlType = "BinaryBlob",
                                 };
                classMapping.Table.AddColumn(scanIndexesColumn);
                var scanIndexesValue = new SimpleValue(classMapping.Table)
                                           {
                                               TypeName = "BinaryBlob"
                                           };
                scanIndexesValue.AddColumn(scanIndexesColumn);
                classMapping.AddProperty(new Property(scanIndexesValue)
                                             {
                                                 Name = "ScanIndexesBytes"
                                             });
            }
            return configuration;
        }

        public static ISessionFactory CreateSessionFactory(String path, SessionFactoryFlags flags)
        {

            var configuration = GetConfiguration(DatabaseTypeEnum.sqlite, flags)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                                                                 {
                                                                     DataSource = path
                                                                 }.ToString());
            if (0 != (flags & SessionFactoryFlags.create_schema))
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            return configuration.BuildSessionFactory();
        }

        public static ISessionFactory CreateSessionFactory(TpgLinkDef tpgLinkDef, SessionFactoryFlags flags)
        {
            var configuration = GetConfiguration(tpgLinkDef.DatabaseTypeEnum, flags)
                .SetProperty("connection.connection_string", tpgLinkDef.GetConnectionString());
            if (0 != (flags & SessionFactoryFlags.create_schema))
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            return configuration.BuildSessionFactory();
        }
    }
}
