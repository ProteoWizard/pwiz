/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Data;
using System.Data.SQLite;

namespace pwiz.Common.Database
{
    public static class SqliteOperations
    {
        public static bool TableExists(IDbConnection connection, string tableName)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT 1 FROM sqlite_master WHERE type='table' AND name=?";
                cmd.Parameters.Add(new SQLiteParameter { Value = tableName });
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read();
                }
            }
        }

        public static bool ColumnExists(IDbConnection connection, string tableName, string columnName)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"PRAGMA table_info(" + tableName + ")";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (columnName.Equals(reader.GetString(1)))
                            return true;
                    }
                }
            }
            return false;
        }

        public static void DropTable(IDbConnection connection, string tableName)
        {
            using (var cmd = connection.CreateCommand())
            {
                // Newly created IrtDbs start without IrtHistory table
                cmd.CommandText = @"DROP TABLE IF EXISTS " + tableName;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
