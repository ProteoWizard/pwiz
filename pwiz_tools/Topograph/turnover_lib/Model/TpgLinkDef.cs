using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using MySql.Data.MySqlClient;
using NHibernate;
using NHibernate.Dialect;
using NHibernate.Driver;
using Npgsql;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class TpgLinkDef
    {
        public const string Extension = ".tpglnk";
        public String Server { get; set; }
        public String Database { get; set; }
        public String Username { get; set; }
        public String Password { get; set; }
        public String DataDirectory { get; set; }
        public String DatabaseType { get; set; }
        public DatabaseTypeEnum DatabaseTypeEnum 
        {
            get
            {
                return (DatabaseTypeEnum) Enum.Parse(typeof (DatabaseTypeEnum), DatabaseType ?? "mysql");
            }
            set
            {
                DatabaseType = value.ToString();
            }
        }
        
        public bool Readonly { get; set; }
        public String GetConnectionString()
        {
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.mysql:
                    return new MySqlConnectionStringBuilder
                        {
                            Server = Server,
                            UserID = Username,
                            Password = Password,
                            Database = Database,
                        }.ToString();
                case DatabaseTypeEnum.postgresql:
                    return new NpgsqlConnectionStringBuilder
                               {
                                   Host = Server,
                                   UserName = Username,
                                   Password = Password,
                                   Database = Database,
                               }.ToString();
            }
            throw new ArgumentException();
        }
        public Type GetDialectClass()
        {
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.sqlite:
                    return typeof (SQLiteDialect);
                case DatabaseTypeEnum.mysql:
                    return typeof (MySQLDialect);
                case DatabaseTypeEnum.postgresql:
                    return typeof (PostgreSQLDialect);
            }
            throw new ArgumentException();
        }
        public Type GetDriverClass()
        {
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.sqlite:
                    return typeof (SQLite20Driver);
                case DatabaseTypeEnum.mysql:
                    return typeof (MySqlDataDriver);
                case DatabaseTypeEnum.postgresql:
                    return typeof (NpgsqlDriver);
            }
            throw new ArgumentException();
        }
        public String GetConnectionStringNoDatabase()
        {
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.mysql:
                    return new MySqlConnectionStringBuilder
                    {
                        Server = Server,
                        UserID = Username,
                        Password = Password,
                    }.ToString();
                case DatabaseTypeEnum.postgresql:
                    return new NpgsqlConnectionStringBuilder
                    {
                        Host = Server,
                        UserName = Username,
                        Password = Password,
                    }.ToString();
            }
            throw new ArgumentException();
        }
        public void Save(String path)
        {
            var serializer = new XmlSerializer(typeof (TpgLinkDef));
            using (var stream = File.Open(path, FileMode.Create))
            {
                serializer.Serialize(stream, this);
            }
        }
        public IDbConnection OpenConnection()
        {
            return OpenConnection(GetConnectionString());
        }
        public IDbConnection OpenConnection(String connectionString)
        {
            IDbConnection connection;
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.mysql:
                    connection = new MySqlConnection(connectionString);
                    break;
                case DatabaseTypeEnum.postgresql:
                    connection = new NpgsqlConnection(connectionString);
                    break;
                case DatabaseTypeEnum.sqlite:
                    connection = new SQLiteConnection(connectionString);
                    break;
                default:
                    throw new ArgumentException();
            }

            connection.Open();
            return connection;
        }

        public ISessionFactory CreateDatabase()
        {
            using (var connection = OpenConnection(GetConnectionStringNoDatabase()))
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE DATABASE " + Database;
                cmd.ExecuteNonQuery();
            }
            return SessionFactoryFactory.CreateSessionFactory(this, true);
        }

        public ISessionFactory OpenSessionFactory()
        {
            return SessionFactoryFactory.CreateSessionFactory(this, false);
        }

        public static TpgLinkDef Load(String path)
        {
            var serializer = new XmlSerializer(typeof(TpgLinkDef));
            using (var stream = File.OpenRead(path))
            {
                return (TpgLinkDef) serializer.Deserialize(stream);
            }
        }
    }
    public enum DatabaseTypeEnum
    {
        mysql,
        postgresql,
        sqlite,
    }
}
