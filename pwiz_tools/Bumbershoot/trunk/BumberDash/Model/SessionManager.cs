using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Collections;
using System.Diagnostics;
using System.IO;
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
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Bumberdash");
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            var dataFile = Path.Combine(root, "Bumbershoot.db");
            var newfactory = CreateSessionFactory(dataFile, true);
            var session = newfactory.OpenSession();
            if (session.QueryOver<ConfigFile>().List().Count<12)
            {
                var baseRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                if (baseRoot == null)
                    throw new Exception("Cannot find base database file");
                if (!File.Exists(Path.Combine(baseRoot, "lib\\Bumbershoot.db")))
                    throw new Exception("Looking for \"" + Path.Combine(baseRoot, "lib\\Bumbershoot.db") + "\", however I cant find it");
                var baseFactory = CreateSessionFactory(Path.Combine(baseRoot, "lib\\Bumbershoot.db"), false);
                var baseSession = baseFactory.OpenSession();
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