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
using System.Collections.Generic;
using System.Data.SQLite;
using System.Reflection;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Data.Linq;
using System.IO;
using NHibernate;
using NHibernate.SqlCommand;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Dialect.Function;
using System.Data.Common;

namespace IDPicker.DataModel
{
    public class SessionFactoryConfig
    {
        public SessionFactoryConfig ()
        {
            CreateSchema = false;
            UseUnfilteredTables = false;
            WriteSqlToConsoleOut = false;
        }

        public bool CreateSchema { get; set; }
        public bool UseUnfilteredTables { get; set; }
        public bool WriteSqlToConsoleOut { get; set; }
    }

    public static class SessionFactoryFactory
    {
        #region SQLite customizations
        public class DistinctGroupConcat : StandardSQLFunction
        {
            public DistinctGroupConcat () : base("group_concat_ex", NHibernateUtil.String) { }

            public override SqlString Render (IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
            {
                var result = base.Render(args, factory);
                return result.Replace("group_concat_ex(", "group_concat_ex(distinct ");
            }
        }

        public class DistinctSum : StandardSQLFunction
        {
            public DistinctSum() : base("sum") { }

            public override SqlString Render (IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
            {
                var result = base.Render(args, factory);
                return result.Replace("sum(", "sum(distinct ");
            }
        }

        public class Parens : StandardSQLFunction
        {
            public Parens() : base("parens", NHibernateUtil.String) { }

            public override SqlString Render(IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
            {
                var result = base.Render(args, factory);
                return result.Replace("parens(", "(");
            }
        }

        public class RoundToInteger : StandardSQLFunction
        {
            public RoundToInteger() : base("round_to_integer", NHibernateUtil.Double) { }

            public override SqlString Render(IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
            {
                var result = base.Render(args, factory);
                return result.Replace("round_to_integer(", "cast(round(").Append(" as integer)");
            }
        }
        
        public class CustomSQLiteDialect : SQLiteDialect
        {
            public CustomSQLiteDialect ()
            {
                RegisterFunction("round", new StandardSQLFunction("round"));
                RegisterFunction("round_to_integer", new RoundToInteger());
                RegisterFunction("group_concat", new StandardSQLFunction("group_concat_ex", NHibernateUtil.String));
                RegisterFunction("sort_unmapped_last", new StandardSQLFunction("sort_unmapped_last", NHibernateUtil.String));
                RegisterFunction("distinct_group_concat", new DistinctGroupConcat());
                RegisterFunction("distinct_sum", new DistinctSum());
                RegisterFunction("distinct_double_array_sum", new StandardSQLFunction("distinct_double_array_sum", NHibernateUtil.BinaryBlob));
                RegisterFunction("distinct_double_array_mean", new StandardSQLFunction("distinct_double_array_mean", NHibernateUtil.BinaryBlob));
                RegisterFunction("distinct_double_array_median", new StandardSQLFunction("distinct_double_array_median", NHibernateUtil.BinaryBlob));
                RegisterFunction("distinct_double_array_tukey_biweight_average", new StandardSQLFunction("distinct_double_array_tukey_biweight_average", NHibernateUtil.BinaryBlob));
                RegisterFunction("distinct_double_array_tukey_biweight_log_average", new StandardSQLFunction("distinct_double_array_tukey_biweight_log_average", NHibernateUtil.BinaryBlob));
                RegisterFunction("parens", new Parens());
            }
        }

        public class CustomSQLiteDriver : NHibernate.Driver.SQLite20Driver
        {
            public override DbConnection CreateConnection()
            {
                var con = base.CreateConnection() as SQLiteConnection;
                con.StateChange += Con_StateChange;
                return con;
            }

            private void Con_StateChange(object sender, System.Data.StateChangeEventArgs e)
            {
                if (e.CurrentState == System.Data.ConnectionState.Open)
                {
                    (sender as SQLiteConnection).EnableExtensions(true);
                    (sender as SQLiteConnection).LoadExtension("idpsqlextensions");
                }
            }
        }

        #endregion

        public static ISessionFactory CreateSessionFactory (string path) { return SessionFactoryFactory.CreateSessionFactory(path, new SessionFactoryConfig()); }

        static object mutex = new object();
        public static ISessionFactory CreateSessionFactory (string path, SessionFactoryConfig config)
        {
            string uncCompatiblePath = Util.GetSQLiteUncCompatiblePath(path);

            // update the existing database's schema if necessary, and if updated, recreate the indexes
            if (path != ":memory:" &&
                File.Exists(path) &&
                IsValidFile(path) &&
                SchemaUpdater.Update(path, null))
            {
                using (var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3", uncCompatiblePath)))
                {
                    conn.Open();
                    conn.ExecuteNonQuery(@"PRAGMA journal_mode=DELETE;
                                           PRAGMA synchronous=OFF;
                                           PRAGMA automatic_indexing=OFF;
                                           PRAGMA cache_size=30000;
                                           PRAGMA temp_store=MEMORY;
                                           PRAGMA page_size=32768;
                                           PRAGMA mmap_size=70368744177664; -- 2^46");
                    DropIndexes(conn);
                    CreateIndexes(conn);
                }
            }

            bool pooling = path == ":memory:";

            var configuration = new Configuration()
                .SetProperty("show_sql", config.WriteSqlToConsoleOut ? "true" : "false")
                .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                .SetProperty("hibernate.cache.use_query_cache", "true")
                //.SetProperty("adonet.batch_size", batchSize.ToString())
                .SetProperty("connection.connection_string", String.Format("Data Source={0};Version=3;{1}", uncCompatiblePath, (pooling ? "Pooling=True;Max Pool Size=1;" : "")))
                .SetProperty("connection.driver_class", typeof(CustomSQLiteDriver).AssemblyQualifiedName)
                .SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                .SetProperty("connection.release_mode", "on_close")
                ;

            ConfigureMappings(configuration);

            if (config.UseUnfilteredTables)
            {
                configuration.ClassMappings.Single(o => o.Table.Name == "Protein").Table.Name = "UnfilteredProtein";
                configuration.ClassMappings.Single(o => o.Table.Name == "Peptide").Table.Name = "UnfilteredPeptide";
                configuration.ClassMappings.Single(o => o.Table.Name == "PeptideInstance").Table.Name = "UnfilteredPeptideInstance";
                configuration.ClassMappings.Single(o => o.Table.Name == "PeptideSpectrumMatch").Table.Name = "UnfilteredPeptideSpectrumMatch";
                configuration.ClassMappings.Single(o => o.Table.Name == "Spectrum").Table.Name = "UnfilteredSpectrum";
            }

            ISessionFactory sessionFactory = null;
            lock(mutex)
                sessionFactory = configuration.BuildSessionFactory();

            sessionFactory.OpenStatelessSession().CreateSQLQuery(@"PRAGMA cache_size=10000;
                                                                   PRAGMA temp_store=MEMORY;
                                                                   PRAGMA page_size=32768;
                                                                   PRAGMA mmap_size=70368744177664; -- 2^46").ExecuteUpdate();

            if (config.CreateSchema)
                CreateFile(path);

            return sessionFactory;
        }

        public static Configuration ConfigureMappings(Configuration configuration)
        {
            return configuration.AddAssembly(typeof(SessionFactoryFactory).Assembly);
        }

        static IStatelessSession newSession;
        static string[] createSql;
        public static System.Data.Common.DbConnection CreateFile (string path)
        {
            lock (mutex)
                if (newSession == null)
                {
                    Configuration configuration = new Configuration()
                        .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                        .SetProperty("connection.connection_string", "Data Source=:memory:;Version=3;")
                        .SetProperty("connection.driver_class", typeof(CustomSQLiteDriver).AssemblyQualifiedName)
                        .SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                        .SetProperty("connection.release_mode", "on_close")
                        ;

                    ConfigureMappings(configuration);

                    var sessionFactory = configuration.BuildSessionFactory();
                    newSession = sessionFactory.OpenStatelessSession();
                    createSql = configuration.GenerateSchemaCreationScript(Dialect.GetDialect(configuration.Properties));
                }

            string uncCompatiblePath = Util.GetSQLiteUncCompatiblePath(path);
            bool pooling = false;// path == ":memory:";
            var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3;{1}", uncCompatiblePath, (pooling ? "Pooling=True;Max Pool Size=1;" : "")));
            conn.Open();

            var journal_mode = conn.ExecuteQuery("PRAGMA journal_mode").Single()[0];
            var synchronous = conn.ExecuteQuery("PRAGMA synchronous").Single()[0];
            conn.ExecuteNonQuery(@"PRAGMA journal_mode=OFF;
                                   PRAGMA synchronous=OFF;
                                   PRAGMA automatic_indexing=OFF;
                                   PRAGMA cache_size=30000;
                                   PRAGMA temp_store=MEMORY;
                                   PRAGMA page_size=32768;
                                   PRAGMA mmap_size=70368744177664; -- 2^46");

            var transaction = conn.BeginTransaction();
            var cmd = conn.CreateCommand();
            foreach (string sql in createSql)
                cmd.ExecuteNonQuery(sql);

            cmd.ExecuteNonQuery(String.Format("INSERT INTO About VALUES (1, 'IDPicker', '{0}', datetime('now'), {1})",
                                              Util.Version, SchemaUpdater.CurrentSchemaRevision));

            cmd.ExecuteNonQuery(@"CREATE TABLE PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);
                                  CREATE TABLE DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);
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
                string uncCompatiblePath = Util.GetSQLiteUncCompatiblePath(path);
                using (var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3", uncCompatiblePath)))
                {
                    conn.Open();

                    // in a valid file, this will throw "already exists"
                    conn.ExecuteNonQuery("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)");
                }
            }
            catch (SQLiteException e)
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
                                   CREATE INDEX PeptideInstance_PeptideProtein ON PeptideInstance (Peptide, Protein);
                                   CREATE UNIQUE INDEX PeptideInstance_ProteinOffsetLength ON PeptideInstance (Protein, Offset, Length);
                                   CREATE UNIQUE INDEX SpectrumSourceGroupLink_SourceGroup ON SpectrumSourceGroupLink (Source, Group_);
                                   CREATE INDEX Spectrum_SourceIndex ON Spectrum (Source, Index_);
                                   CREATE UNIQUE INDEX Spectrum_SourceNativeID ON Spectrum (Source, NativeID);
                                   CREATE INDEX PeptideSpectrumMatch_PeptideSpectrumAnalysis ON PeptideSpectrumMatch (Peptide, Spectrum, Analysis);
                                   CREATE INDEX PeptideSpectrumMatch_SpectrumAnalysisPeptide ON PeptideSpectrumMatch (Spectrum, Analysis, Peptide);
                                   CREATE INDEX PeptideSpectrumMatch_QValue ON PeptideSpectrumMatch (QValue);
                                   CREATE INDEX PeptideSpectrumMatch_Rank ON PeptideSpectrumMatch (Rank);
                                   CREATE INDEX PeptideModification_PeptideSpectrumMatchModification ON PeptideModification (PeptideSpectrumMatch, Modification);
                                   CREATE INDEX PeptideModification_ModificationPeptideSpectrumMatch ON PeptideModification (Modification, PeptideSpectrumMatch);
                                  ");
        }

        public static void DropIndexes (System.Data.IDbConnection conn)
        {
            conn.ExecuteNonQuery(@"DROP INDEX IF EXISTS Protein_Accession;
                                   DROP INDEX IF EXISTS PeptideInstance_PeptideProtein;
                                   DROP INDEX IF EXISTS PeptideInstance_ProteinOffsetLength;
                                   DROP INDEX IF EXISTS SpectrumSourceGroupLink_SourceGroup;
                                   DROP INDEX IF EXISTS Spectrum_SourceIndex;
                                   DROP INDEX IF EXISTS Spectrum_SourceNativeID;
                                   DROP INDEX IF EXISTS PeptideSpectrumMatch_PeptideSpectrumAnalysis;
                                   DROP INDEX IF EXISTS PeptideSpectrumMatch_SpectrumAnalysisPeptide;
                                   DROP INDEX IF EXISTS PeptideSpectrumMatch_QValue;
                                   DROP INDEX IF EXISTS PeptideSpectrumMatch_Rank;
                                   DROP INDEX IF EXISTS PeptideModification_PeptideSpectrumMatchModification;
                                   DROP INDEX IF EXISTS PeptideModification_ModificationPeptideSpectrumMatch;
                                  ");
        }
    }
}
