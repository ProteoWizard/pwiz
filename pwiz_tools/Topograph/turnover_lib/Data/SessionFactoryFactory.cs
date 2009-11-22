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
using NHibernate.Mapping;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public static class SessionFactoryFactory
    {
        private static ISessionFactory BuildSessionFactory(Configuration configuration)
        {
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            configuration.SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            configuration.AddInputStream(assembly.GetManifestResourceStream("pwiz.Topograph.Data.mapping.xml"));
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }

        public static ISessionFactory CreateSessionFactory(String path, bool createSchema)
        {
            var configuration = new Configuration()
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                    {
                        DataSource = path
                    }.ToString())
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            return BuildSessionFactory(configuration);
        }

        public static ISessionFactory CreateSessionFactory(TpgLinkDef tpgLinkDef, bool createSchema)
        {
            var configuration = new Configuration()
                .SetProperty("show_sql", "true")
                .SetProperty("dialect", tpgLinkDef.GetDialectClass().AssemblyQualifiedName)
                .SetProperty("connection.connection_string", tpgLinkDef.GetConnectionString())
                .SetProperty("connection.driver_class", tpgLinkDef.GetDriverClass().AssemblyQualifiedName);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            return BuildSessionFactory(configuration);
        }
        public static ISessionFactory GetSessionFactoryWithoutIdGenerators(TpgLinkDef tpgLinkDef)
        {
            var configuration = new Configuration()
                .SetProperty("show_sql", "true")
                .SetProperty("dialect", tpgLinkDef.GetDialectClass().AssemblyQualifiedName)
                .SetProperty("connection.connection_string", tpgLinkDef.GetConnectionString())
                .SetProperty("connection.driver_class", tpgLinkDef.GetDriverClass().AssemblyQualifiedName);
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            configuration.SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            configuration.AddInputStream(assembly.GetManifestResourceStream("pwiz.Topograph.Data.mapping.xml"));
            foreach (var classMapping in configuration.ClassMappings)
            {
                classMapping.IdentifierProperty.Generation = PropertyGeneration.Never;
            }
            return configuration.BuildSessionFactory();
        }
    }
}
