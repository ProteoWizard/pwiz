/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using MySql.Data.MySqlClient;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
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
        [XmlIgnore]
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
        public IDbConnection OpenConnectionNoDatabase()
        {
            return OpenConnection(GetConnectionStringNoDatabase());
        }

        public ISessionFactory CreateDatabase()
        {
            using (var connection = OpenConnection(GetConnectionStringNoDatabase()))
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE DATABASE " + Database;
                cmd.ExecuteNonQuery();
            }
            using (var connection = OpenConnection(GetConnectionString())) 
            {
                if (DatabaseTypeEnum == DatabaseTypeEnum.mysql)
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SET storage_engine=INNODB";
                    cmd.ExecuteNonQuery();
                }
                var configuration = SessionFactoryFactory.GetConfiguration(DatabaseTypeEnum, 0);
                var schemaExport = new SchemaExport(configuration);
                schemaExport.Execute(false, true, false, connection, null);
            }
            return SessionFactoryFactory.CreateSessionFactory(this, SessionFactoryFlags.CreateSchema);
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
        public IList<string> ListDatabaseNames()
        {
            using (var connection = OpenConnection(GetConnectionStringNoDatabase()))
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SHOW DATABASES";
                var reader = cmd.ExecuteReader();
                var result = new List<string>();
                while (reader.Read())
                {
                    var value = reader[0] as string;
                    if (null == value)
                    {
                        continue;
                    }
                    result.Add(value);
                }
                return result;
            }
        }
        private static readonly Regex RegexDatabasePrefix = new Regex("ON `([^`%]*)%`");
        /// <summary>
        /// Checks whether the user has permissions on databases with a wildcard name
        /// beginning with a particular prefix.  If so, returns that prefix, otherwise null.
        /// </summary>
        public string GetDatabaseNamePrefixForUser()
        {
            using (var connection = OpenConnection(GetConnectionStringNoDatabase()))
            {
                if (DatabaseTypeEnum != DatabaseTypeEnum.mysql)
                {
                    return null;
                }
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SHOW GRANTS FOR CURRENT_USER()";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var value = reader[0] as string;
                    if (value == null)
                    {
                        continue;
                    }
                    var match = RegexDatabasePrefix.Match(value);
                    if (!match.Success)
                    {
                        continue;
                    }
                    return match.Groups[1].Value;
                }
            }
            return null;
        }
    }
    // These enum values are saved in .tpglnk files, so don't change them
    // ReSharper disable InconsistentNaming
    public enum DatabaseTypeEnum
    {
        mysql,
        postgresql,
        sqlite,
    }
    // ReSharper restore InconsistentNaming

    [Flags]
    public enum SessionFactoryFlags
    {
        ShowSql = 0x1,
        CreateSchema = 0x2,
    }
}
