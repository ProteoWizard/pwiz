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
using System.Collections;
using System.Text;
using NHibernate;
using NHibernate.SqlCommand;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Dialect.Function;

namespace IDPicker.DataModel
{
    public static class SessionFactoryFactory
    {
        #region SQLite customizations
        public class DistinctGroupConcat : StandardSQLFunction
        {
            public DistinctGroupConcat () : base("group_concat", NHibernateUtil.String) { }

            public override SqlString Render (IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
            {
                var result = base.Render(args, factory);
                return result.Replace("group_concat(", "group_concat(distinct ");
            }
        }

        /// <summary>
        /// Takes two integers and returns the set of integers between them as a string delimited by commas.
        /// Both ends of the set are inclusive.
        /// </summary>
        /// <example>RANGE_CONCAT(2,4) -> 2,3,4</example>
        [SQLiteFunction(Name = "range_concat", Arguments = 2, FuncType = FunctionType.Scalar)]
        public class RangeConcat : SQLiteFunction
        {
            public override object Invoke (object[] args)
            {
                int arg0 = Convert.ToInt32(args[0]);
                int arg1 = Convert.ToInt32(args[1]);
                int min = Math.Min(arg0, arg1);
                int max = Math.Max(arg0, arg1);
                string[] range_values = new string[max - min + 1];
                for (int i = 0; i < range_values.Length; ++i)
                    range_values[i] = (min + i).ToString();
                return String.Join(",", range_values);
            }
        }

        public class CustomSQLiteDialect : SQLiteDialect
        {
            public CustomSQLiteDialect ()
            {
                RegisterFunction("round", new StandardSQLFunction("round"));
                RegisterFunction("group_concat", new StandardSQLFunction("group_concat", NHibernateUtil.String));
                RegisterFunction("distinct_group_concat", new DistinctGroupConcat());
                RegisterFunction("range_concat", new StandardSQLFunction("range_concat", NHibernateUtil.String));
            }
        }
        #endregion

        public static ISessionFactory CreateSessionFactory(string path, bool createSchema, bool showSQL)
        {
            Configuration configuration = new Configuration()
                .SetProperty("show_sql", showSQL ? "true" : "false")
                .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                .SetProperty("hibernate.cache.use_query_cache", "true")
                .SetProperty("proxyfactory.factory_class", typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                //.SetProperty("adonet.batch_size", batchSize.ToString())
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
            return configuration.AddAssembly(typeof(SessionFactoryFactory).Assembly);
        }
    }
}
