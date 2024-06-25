using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    public static class SessionFactoryFactory
    {
        public static ISessionFactory CreateSessionFactory(string filePath, bool createSchema)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath
            }.ToString();
            var cfg = new Configuration()
                .SetProperty(@"dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty(@"connection.connection_string", connectionString)
                .SetProperty(@"connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName)
                .SetProperty(@"connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            var hbmSerializer = new HbmSerializer
            {
                Validate = true
            };
            cfg.AddInputStream(hbmSerializer.Serialize(typeof(Entity).Assembly));
            if (createSchema)
            {
                cfg.SetProperty(@"hbm2ddl.auto", @"create");
            }
            return cfg.BuildSessionFactory();
        }
    }
}
