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

        /// <summary>
        /// Takes one or more binary vectors of doubles (as BLOBs) and returns the summed array.
        /// </summary>
        /// <example>DOUBLE_ARRAY_SUM([1,2,3] [4,5,6]) -> [5,7,9]</example>
        [SQLiteFunction(Name = "double_array_sum", Arguments = -1, FuncType = FunctionType.Aggregate)]
        public class DoubleArraySum : SQLiteFunction
        {
            public override void Step(object[] args, int stepNumber, ref object contextData)
            {
                if (args[0] == null || args[0] == DBNull.Value)
                    return;

                byte[] arrayBytes = args[0] as byte[];
                if (arrayBytes == null || arrayBytes.Length % 8 > 0)
                    throw new ArgumentException("double_array_sum only works with BLOBs of double precision floats");

                int arrayLength = arrayBytes.Length / 8;

                if (stepNumber == 1)
                    contextData = Enumerable.Repeat(0.0, arrayLength).ToArray();

                double[] arrayValues = contextData as double[];
                var arrayStream = new BinaryReader(new MemoryStream(arrayBytes));
                for (int i = 0; i < arrayLength; ++i)
                    arrayValues[i] += arrayStream.ReadDouble();
            }

            public override object Final(object contextData)
            {
                double[] arrayValues = contextData as double[];
                if (arrayValues == null)
                    return DBNull.Value;

                var bytes = new List<byte>(sizeof(double) * arrayValues.Length);
                for (int i = 0; i < arrayValues.Length; ++i)
                    bytes.AddRange(BitConverter.GetBytes(arrayValues[i]));
                return bytes.ToArray();
            }
        }

        [SQLiteFunction(Name = "double_array_sum2", Arguments = -1, FuncType = FunctionType.Aggregate)]
        public class DoubleArraySum2 : SQLiteFunction
        {
            public override void Step(object[] args, int stepNumber, ref object contextData)
            {
                if (args[0] == null)
                    return;

                byte[] arrayBytes = args[0] as byte[];
                if (arrayBytes == null || arrayBytes.Length % 8 > 0)
                    throw new ArgumentException("double_array_sum only works with BLOBs of double precision floats");

                int arrayLength = arrayBytes.Length / 8;

                if (stepNumber == 1)
                    contextData = Enumerable.Repeat(0.0, arrayLength).ToArray();

                double[] arrayValues = contextData as double[];
                ArrayCaster.AsFloatArray(arrayBytes, floats =>
                {
                    for (int i = 0; i < arrayLength; ++i)
                        arrayValues[i] += floats[i];
                });
            }

            public override object Final(object contextData)
            {
                double[] arrayValues = contextData as double[];
                if (arrayValues == null)
                    return DBNull.Value;

                byte[] result = new byte[arrayValues.Length * sizeof(double)];
                ArrayCaster.AsByteArray(arrayValues, bytes => bytes.CopyTo(result, 0));
                return result;
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
                RegisterFunction("double_array_sum", new StandardSQLFunction("double_array_sum", NHibernateUtil.BinaryBlob));
                RegisterFunction("double_array_sum2", new StandardSQLFunction("double_array_sum2", NHibernateUtil.BinaryBlob));
            }
        }
        #endregion

        public static ISessionFactory CreateSessionFactory (string path) { return SessionFactoryFactory.CreateSessionFactory(path, new SessionFactoryConfig()); }

        static object mutex = new object();
        public static ISessionFactory CreateSessionFactory (string path, SessionFactoryConfig config)
        {
            // update the existing database's schema if necessary, and if updated, recreate the indexes
            if (File.Exists(path) &&
                IsValidFile(path) &&
                SchemaUpdater.Update(path, null))
            {
                using (var conn = new SQLiteConnection(String.Format("Data Source={0};Version=3", path)))
                {
                    conn.Open();
                    DropIndexes(conn);
                    CreateIndexes(conn);
                }
            }

            bool pooling = path == ":memory:";

            var configuration = new Configuration()
                .SetProperty("show_sql", config.WriteSqlToConsoleOut ? "true" : "false")
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
                                                                   PRAGMA page_size=32768").ExecuteUpdate();

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
                                   PRAGMA cache_size=30000;
                                   PRAGMA temp_store=MEMORY;
                                   PRAGMA page_size=32768");

            var transaction = conn.BeginTransaction();
            var cmd = conn.CreateCommand();
            foreach (string sql in createSql)
                cmd.ExecuteNonQuery(sql);

            cmd.ExecuteNonQuery(String.Format("INSERT INTO About VALUES (1, 'IDPicker', '{0}', datetime('now'), {1})",
                                              Util.Version, SchemaUpdater.CurrentSchemaRevision));

            cmd.ExecuteNonQuery(@"CREATE TABLE PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);
                                  CREATE TABLE DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);
                                  CREATE TABLE DistinctMatch (PsmId INTEGER PRIMARY KEY, DistinctMatchId INT, DistinctMatchKey TEXT);
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
