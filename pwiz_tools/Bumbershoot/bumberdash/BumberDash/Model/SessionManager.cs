//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NHibernate;
using NHibernate.SqlCommand;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Dialect.Function;
using Environment = System.Environment;

namespace BumberDash.Model
{
    public static class SessionManager
    {
        #region SQLite customizations
        public class DistinctGroupConcat : StandardSQLFunction
        {
            public DistinctGroupConcat() : base("group_concat", NHibernateUtil.String) { }

            public override SqlString Render(IList args, NHibernate.Engine.ISessionFactoryImplementor factory)
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
            public override object Invoke(object[] args)
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
            public CustomSQLiteDialect()
            {
                RegisterFunction("round", new StandardSQLFunction("round"));
                RegisterFunction("group_concat", new StandardSQLFunction("group_concat", NHibernateUtil.String));
                RegisterFunction("distinct_group_concat", new DistinctGroupConcat());
                RegisterFunction("range_concat", new StandardSQLFunction("range_concat", NHibernateUtil.String));
            }
        }
        #endregion

        static object mutex = new object();
        public static ISessionFactory CreateSessionFactory()
        {
            var currentGuiVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

            var newdatabase = false;
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Bumberdash");
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            var dataFile = Path.Combine(root, "Bumbershoot " + currentGuiVersion + ".db");
            var newfactory = CreateSessionFactory(dataFile, true);
            var session = newfactory.OpenSession();

            //check that all preloaded templates are present
            if (session.QueryOver<ConfigFile>().List().Count<16)
            {
                //check for presence of completely empty database
                if (session.QueryOver<HistoryItem>().List().Count == 0 &&
                    session.QueryOver<ConfigFile>().List().Count == 0)
                    newdatabase = true;

                //load base database
                var baseRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                if (baseRoot == null)
                    throw new Exception("Cannot find base database file");
                if (!File.Exists(Path.Combine(baseRoot, "lib\\Bumbershoot.db")))
                    throw new Exception("Looking for \"" + Path.Combine(baseRoot, "lib\\Bumbershoot.db") + "\", however I cant find it");
                var baseFactory = CreateSessionFactory(Path.Combine(baseRoot, "lib\\Bumbershoot.db"), false);
                var baseSession = baseFactory.OpenSession();

                //load base templates
                var connectedBaseConfigs = baseSession.QueryOver<ConfigFile>().List();
                var baseConfigs = new List<ConfigFile>();
                foreach (var config in connectedBaseConfigs)
                {
                    var newInstrument = new ConfigFile
                                            {
                                                Name = config.Name,
                                                DestinationProgram = config.DestinationProgram,
                                                FilePath = config.FilePath,
                                                PropertyList = new List<ConfigProperty>()
                                            };
                    foreach (var property in config.PropertyList)
                    {
                        var newProperty = new ConfigProperty
                                              {
                                                  ConfigAssociation = newInstrument,
                                                  Name = property.Name,
                                                  Type = property.Type,
                                                  Value = property.Value
                                              };
                        newInstrument.PropertyList.Add(newProperty);
                    }
                    baseConfigs.Add(newInstrument);
                }

                //delete old templates (if any remain) and load base template
                foreach (var config in baseConfigs)
                {
                    ConfigFile config1 = config;
                    var deleteList = session.QueryOver<ConfigFile>()
                        .Where(x => x.Name == config1.Name &&
                                    x.DestinationProgram == config1.DestinationProgram)
                        .List();
                    foreach (var item in deleteList)
                        session.Delete(item);
                    session.Flush();
                    var newInstrument = new ConfigFile()
                                          {
                                              Name = config.Name,
                                              DestinationProgram = config.DestinationProgram,
                                              FilePath = config.FilePath
                                          };
                    session.SaveOrUpdate(newInstrument);
                    session.Flush();
                    foreach (var property in config.PropertyList)
                    {
                        var newProperty = new ConfigProperty()
                                              {
                                                  ConfigAssociation = newInstrument,
                                                  Name = property.Name,
                                                  Type = property.Type,
                                                  Value = property.Value
                                              };
                        session.SaveOrUpdate(newProperty);
                    }
                    session.Flush();
                }

                //automatically try to recover old history items if database is flagged as new
                if (newdatabase)
                {
                    //if database structure is ever changed this will need to be updated to include conversion (probably in the form of basic sqlite queries)
                    var directoryInfo = new DirectoryInfo(root);
                    var bdFiles = directoryInfo.GetFiles("Bumbershoot*.db").Select(file => file.Name).ToList();
                    bdFiles.Sort();

                    if (bdFiles.Count > 1)
                    {
                        //reorder to proper location
                        if (bdFiles[bdFiles.Count - 1] == "Bumbershoot.db")
                        {
                            bdFiles.RemoveAt(bdFiles.Count - 1);
                            bdFiles.Insert(0, "Bumbershoot.db");
                        }

                        var restorePath = Path.Combine(root, bdFiles[bdFiles.Count - 2]);
                        var restoreFactory = CreateSessionFactory(restorePath, true);
                        var restoreSession = restoreFactory.OpenSession();
                        var restoreJobs = restoreSession.QueryOver<HistoryItem>().List<HistoryItem>();
                        var restoredConfigs = new Dictionary<int,ConfigFile>();

                        //start restoring jobs
                        foreach (var job in restoreJobs)
                        {
                            var newjob = new HistoryItem
                                             {
                                                 Cpus = job.Cpus, CurrentStatus = job.CurrentStatus,
                                                 EndTime = job.EndTime, JobName = job.JobName,
                                                 JobType = job.JobType, OutputDirectory = job.OutputDirectory,
                                                 ProteinDatabase = job.ProteinDatabase, RowNumber = job.RowNumber,
                                                 SpectralLibrary = job.SpectralLibrary, StartTime = job.StartTime
                                             };
                            if (!restoredConfigs.ContainsKey(job.InitialConfigFile.ConfigId))
                            {
                                var newConfig = new ConfigFile
                                                    {
                                                        DestinationProgram = job.InitialConfigFile.DestinationProgram,
                                                        FilePath = job.InitialConfigFile.FilePath,
                                                        FirstUsedDate = job.InitialConfigFile.FirstUsedDate,
                                                        Name = job.InitialConfigFile.Name
                                                    };
                                session.SaveOrUpdate(newConfig);
                                session.Flush();
                                foreach (var property in job.InitialConfigFile.PropertyList)
                                {
                                    var newProperty = new ConfigProperty
                                                          {
                                                              Name = property.Name,
                                                              ConfigAssociation = newConfig,
                                                              Type = property.Type,
                                                              Value = property.Value
                                                          };
                                    session.SaveOrUpdate(newProperty);
                                }
                                session.Flush();
                                newjob.InitialConfigFile = newConfig;
                                restoredConfigs.Add(job.InitialConfigFile.ConfigId, newConfig);
                            }
                            else
                                newjob.InitialConfigFile = restoredConfigs[job.InitialConfigFile.ConfigId];

                            if (job.TagConfigFile != null)
                            {
                                if (!restoredConfigs.ContainsKey(job.TagConfigFile.ConfigId))
                                {
                                    var newConfig = new ConfigFile
                                                        {
                                                            DestinationProgram = job.TagConfigFile.DestinationProgram,
                                                            FilePath = job.TagConfigFile.FilePath,
                                                            FirstUsedDate = job.TagConfigFile.FirstUsedDate,
                                                            Name = job.TagConfigFile.Name
                                                        };
                                    session.SaveOrUpdate(newConfig);
                                    session.Flush();
                                    foreach (var property in job.TagConfigFile.PropertyList)
                                    {
                                        var newProperty = new ConfigProperty
                                                              {
                                                                  Name = property.Name,
                                                                  ConfigAssociation = newConfig,
                                                                  Type = property.Type,
                                                                  Value = property.Value
                                                              };
                                        session.SaveOrUpdate(newProperty);
                                    }
                                    session.Flush();
                                    newjob.TagConfigFile = newConfig;
                                    restoredConfigs.Add(job.TagConfigFile.ConfigId,newConfig);
                                }
                                else
                                    newjob.TagConfigFile = restoredConfigs[job.TagConfigFile.ConfigId];
                            }
                            session.SaveOrUpdate(newjob);
                            session.Flush();
                            foreach (var file in job.FileList)
                            {
                                var newFile = new InputFile {FilePath = file.FilePath, HistoryItem = newjob};
                                session.SaveOrUpdate(newFile);
                                session.Flush();
                            }
                        }
                    }
                }
            }
            session.Close();
            return newfactory;
        }

        public static ISessionFactory CreateSessionFactory(string filePath, bool mainSession)
        {
            Configuration configuration = new Configuration()
                .SetProperty("dialect", typeof(CustomSQLiteDialect).AssemblyQualifiedName)
                .SetProperty("hibernate.cache.use_query_cache", "true")
                .SetProperty("proxyfactory.factory_class", typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                //.SetProperty("adonet.batch_size", batchSize.ToString())
                .SetProperty("connection.connection_string", "Data Source=" + filePath + ";Version=3;")
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                .SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName)
                .SetProperty("connection.release_mode", "on_close")
                ;

            ConfigureMappings(configuration);

            ISessionFactory sessionFactory = null;
            lock (mutex)
                sessionFactory = configuration.BuildSessionFactory();

            if (mainSession)
            {
                sessionFactory.OpenStatelessSession().CreateSQLQuery(@"PRAGMA default_cache_size=500000;
                                                                   PRAGMA temp_store=MEMORY").ExecuteUpdate();

                var schema = new NHibernate.Tool.hbm2ddl.SchemaUpdate(configuration);
                schema.Execute(false, true);
            }

            return sessionFactory;
        }

        public static Configuration ConfigureMappings(Configuration configuration)
        {
            return configuration.AddAssembly(typeof(SessionManager).Assembly);
        }

    }
}