using System;
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
            return CreateSessionFactory(Path.Combine(root, "Bumbershoot.db"));
        }

        public static ISessionFactory CreateSessionFactory(string filePath)
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

            sessionFactory.OpenStatelessSession().CreateSQLQuery(@"PRAGMA default_cache_size=500000;
                                                                   PRAGMA temp_store=MEMORY").ExecuteUpdate();

            var schema = new NHibernate.Tool.hbm2ddl.SchemaUpdate(configuration);
            schema.Execute(false, true);

            return sessionFactory;
        }

        public static Configuration ConfigureMappings(Configuration configuration)
        {
            return configuration.AddAssembly(typeof(SessionManager).Assembly);
        }

        

    }
}