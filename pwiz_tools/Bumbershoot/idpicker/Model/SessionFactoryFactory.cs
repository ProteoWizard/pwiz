/*
 * $Id$
 *
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
using System.Linq;
using System.Data.Linq;
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

        static object mutex = new object();
        public static ISessionFactory CreateSessionFactory(string path, bool createSchema, bool showSQL)
        {
            bool pooling = path == ":memory:";

            Configuration configuration = new Configuration()
                .SetProperty("show_sql", showSQL ? "true" : "false")
                .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                .SetProperty("hibernate.cache.use_query_cache", "true")
                .SetProperty("proxyfactory.factory_class", typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                //.SetProperty("adonet.batch_size", batchSize.ToString())
                .SetProperty("connection.connection_string", String.Format("Data Source={0};Version=3;{1}", path, (pooling ? "Pooling=True;Max Pool Size=1;" : "")))
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                .SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                .SetProperty("connection.release_mode", "on_close")
                ;

            ConfigureMappings(configuration);

            ISessionFactory sessionFactory = null;
            lock(mutex)
                sessionFactory = configuration.BuildSessionFactory();

            sessionFactory.OpenStatelessSession().CreateSQLQuery(@"PRAGMA default_cache_size=500000;
                                                                   PRAGMA temp_store=MEMORY").ExecuteUpdate();

            if(createSchema)
                CreateFile(path);

            return sessionFactory;
        }

        public static Configuration ConfigureMappings(Configuration configuration)
        {
            return configuration.AddAssembly(typeof(SessionFactoryFactory).Assembly);
        }

        static IStatelessSession newSession;
        static string[] createSql;
        public static System.Data.IDbConnection CreateFile (string path)
        {
            lock (mutex)
                if (newSession == null)
                {
                    Configuration configuration = new Configuration()
                        .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                        .SetProperty("proxyfactory.factory_class", typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                        .SetProperty("connection.connection_string", "Data Source=:memory:;Version=3;")
                        .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                        .SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                        .SetProperty("connection.release_mode", "on_close")
                        ;

                    ConfigureMappings(configuration);

                    var sessionFactory = configuration.BuildSessionFactory();
                    newSession = sessionFactory.OpenStatelessSession();
                    createSql = configuration.GenerateSchemaCreationScript(Dialect.GetDialect(configuration.Properties));
                }

            bool pooling = false;// path == ":memory:";
            var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3;{1}", path, (pooling ? "Pooling=True;Max Pool Size=1;" : "")));
            conn.Open();

            var journal_mode = conn.ExecuteQuery("PRAGMA journal_mode").Single()[0];
            var synchronous = conn.ExecuteQuery("PRAGMA synchronous").Single()[0];
            conn.ExecuteNonQuery(@"PRAGMA journal_mode=OFF;
                                   PRAGMA synchronous=OFF;
                                   PRAGMA automatic_indexing=OFF;
                                   PRAGMA default_cache_size=500000;
                                   PRAGMA temp_store=MEMORY");

            var transaction = conn.BeginTransaction();
            var cmd = conn.CreateCommand();
            foreach (string sql in createSql)
                cmd.ExecuteNonQuery(sql);

            cmd.ExecuteNonQuery(@"CREATE TABLE PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);
                                  CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY);");
            CreateIndexes(conn);
            transaction.Commit();

            conn.ExecuteNonQuery("PRAGMA journal_mode=" + journal_mode + ";" +
                                 "PRAGMA synchronous=" + synchronous);

            return conn;
        }

        public static bool IsValidFile (string path)
        {
            try
            {
                using (var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3", path)))
                {
                    conn.Open();

                    // in a valid file, this will throw "already exists"
                    conn.ExecuteNonQuery("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)");
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("already exists"))
                    return true;
            }

            // creating the table or any other exception indicates an invalid file
            return false;
        }

        public static void CreateIndexes(System.Data.IDbConnection conn)
        {
            conn.ExecuteNonQuery(@"CREATE UNIQUE INDEX Protein_Accession ON Protein (Accession);
                                   CREATE INDEX PeptideInstance_Peptide ON PeptideInstance (Peptide);
                                   CREATE INDEX PeptideInstance_Protein ON PeptideInstance (Protein);
                                   CREATE INDEX PeptideInstance_PeptideProtein ON PeptideInstance (Peptide, Protein);
                                   CREATE UNIQUE INDEX PeptideInstance_ProteinOffsetLength ON PeptideInstance (Protein, Offset, Length);
                                   CREATE UNIQUE INDEX SpectrumSourceGroupLink_SourceGroup ON SpectrumSourceGroupLink (Source, Group_);
                                   CREATE INDEX Spectrum_SourceIndex ON Spectrum (Source, Index_);
                                   CREATE UNIQUE INDEX Spectrum_SourceNativeID ON Spectrum (Source, NativeID);
                                   CREATE INDEX PeptideSpectrumMatch_Analysis ON PeptideSpectrumMatch (Analysis);
                                   CREATE INDEX PeptideSpectrumMatch_Peptide ON PeptideSpectrumMatch (Peptide);
                                   CREATE INDEX PeptideSpectrumMatch_Spectrum ON PeptideSpectrumMatch (Spectrum);
                                   CREATE INDEX PeptideSpectrumMatch_QValue ON PeptideSpectrumMatch (QValue);
                                   CREATE INDEX PeptideSpectrumMatch_Rank ON PeptideSpectrumMatch (Rank);
                                   CREATE INDEX PeptideModification_PeptideSpectrumMatch ON PeptideModification (PeptideSpectrumMatch);
                                   CREATE INDEX PeptideModification_Modification ON PeptideModification (Modification);
                                  ");
        }

        public static void DropIndexes (System.Data.IDbConnection conn)
        {
            conn.ExecuteNonQuery(@"DROP INDEX Protein_Accession;
                                   DROP INDEX PeptideInstance_Peptide;
                                   DROP INDEX PeptideInstance_Protein;
                                   DROP INDEX PeptideInstance_PeptideProtein;
                                   DROP INDEX PeptideInstance_ProteinOffsetLength;
                                   DROP INDEX SpectrumSourceGroupLink_SourceGroup;
                                   DROP INDEX Spectrum_SourceIndex;
                                   DROP INDEX Spectrum_SourceNativeID;
                                   DROP INDEX PeptideSpectrumMatch_Analysis;
                                   DROP INDEX PeptideSpectrumMatch_Peptide;
                                   DROP INDEX PeptideSpectrumMatch_Spectrum;
                                   DROP INDEX PeptideSpectrumMatch_QValue;
                                   DROP INDEX PeptideSpectrumMatch_Rank;
                                   DROP INDEX PeptideModification_PeptideSpectrumMatch;
                                   DROP INDEX PeptideModification_Modification;
                                  ");
        }
    }
}
