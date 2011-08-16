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

namespace pwiz.ProteomeDatabase.DataModel
{
    public static class SessionFactoryFactory
    {
        public static ISessionFactory CreateSessionFactory(String path, bool createSchema)
        {
            Configuration configuration = new Configuration()
                //.SetProperty("show_sql", "true")
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                {
                    DataSource = path
                }.ToString())
                .SetProperty("connection.driver_class", 
                typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create");
            }
            configuration.SetProperty("connection.provider", 
                typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            ConfigureMappings(configuration);
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }
        public static Configuration ConfigureMappings(Configuration configuration)
        {
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            return configuration.AddInputStream(
                assembly.GetManifestResourceStream(typeof(SessionFactoryFactory).Namespace + ".mapping.xml"));
        }
    }
}
