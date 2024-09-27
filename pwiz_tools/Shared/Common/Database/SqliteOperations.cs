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

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using pwiz.Common.SystemUtil;

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

        public static IEnumerable<string> DumpTable(string dbFilepath, string tableName, string columnSeparator = "\t", string[] sortColumns = null, string[] excludeColumns = null)
        {
            using var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = dbFilepath }.ConnectionString);
            connection.Open();
            foreach(string s in DumpTable(connection, tableName, columnSeparator, sortColumns, excludeColumns))
                yield return s;
        }
        public static List<string> GetColumnNamesFromTable(IDbConnection connection, string tableName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM " + tableName + " LIMIT 0";
            using var reader = cmd.ExecuteReader();
            var schemaTable = reader.GetSchemaTable();
            var columnNames = new List<string>();
            if (schemaTable != null)
                columnNames.AddRange(from DataRow row in schemaTable.Rows select row["ColumnName"].ToString());
            return columnNames;
        }

        public static IEnumerable<string> DumpTable(IDbConnection connection, string tableName, string columnSeparator = "\t", string[] sortColumns = null, string[] excludeColumns = null)
        {
            var columns = new HashSet<string>(GetColumnNamesFromTable(connection, tableName));
            if (excludeColumns != null)
                columns.ExceptWith(excludeColumns);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT " + string.Join(@", ", columns) + " FROM " + tableName;
            if (sortColumns != null)
                cmd.CommandText += @" ORDER BY " + string.Join(@",", sortColumns);
            using var reader = cmd.ExecuteReader();
            using var sha1 = SHA1.Create();
            yield return string.Join(columnSeparator,
                Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)));
            object[] row = new object[reader.FieldCount];
            while (reader.Read())
            {
                reader.GetValues(row);
                for (var i = 0; i < row.Length; i++)
                    if (row[i] is byte[] bytes)
                        row[i] = Convert.ToBase64String(sha1.ComputeHash(bytes));

                yield return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture,
                    () => string.Join(columnSeparator, row));
            }
        }
    }
}
