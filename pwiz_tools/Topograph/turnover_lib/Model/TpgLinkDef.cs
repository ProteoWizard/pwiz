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
        public int? Port { get; set; }
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
                    {
                        var builder = new MySqlConnectionStringBuilder
                                          {
                                              Server = Server,
                                              UserID = Username,
                                              Password = Password,
                                              Database = Database,
                                          };
                        if (Port != null)
                        {
                            builder.Port = (uint) Port.Value;
                        }
                        return builder.ToString();
                    }
                case DatabaseTypeEnum.postgresql:
                    {
                        var builder = new NpgsqlConnectionStringBuilder
                                          {
                                              Host = Server,
                                              UserName = Username,
                                              Password = Password,
                                              Database = Database,
                                          };
                        if (Port != null)
                        {
                            builder.Port = Port.Value;
                        }
                        return builder.ToString();
                    }
            }
            throw new ArgumentException();
        }

        public String GetConnectionStringNoDatabase()
        {
            switch (DatabaseTypeEnum)
            {
                case DatabaseTypeEnum.mysql:
                    {
                    var builder = new MySqlConnectionStringBuilder
                                      {
                                          Server = Server,
                                          UserID = Username,
                                          Password = Password,
                                      };
                    if (Port != null)
                    {
                        builder.Port = (uint) Port;
                    }
                    return builder.ToString();
                    }
                case DatabaseTypeEnum.postgresql:{
                    var builder = new NpgsqlConnectionStringBuilder
                                      {
                                          Host = Server,
                                          UserName = Username,
                                          Password = Password,
                                      };
                    if (Port != null)
                    {
                        builder.Port = Port.Value;
                    }
                    return builder.ToString();
                    }
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
            return SessionFactoryFactory.CreateSessionFactory(this, SessionFactoryFlags.create_schema);
        }

        public ISessionFactory OpenSessionFactory()
        {
            return SessionFactoryFactory.CreateSessionFactory(this, 0);
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

    [Flags]
    public enum SessionFactoryFlags
    {
        show_sql = 0x1,
        remove_binary_columns = 0x2,
        create_schema = 0x4,
    }
}
