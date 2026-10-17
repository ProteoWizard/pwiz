/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// The SQLite questions every reader and writer of a library asks: open it read-only, does
    /// it have a table, which columns does a table have. One place, so the connection string and
    /// the <c>sqlite_master</c> query are written once.
    /// </summary>
    internal static class BlibSqlite
    {
        /// <summary>Opens the library at <paramref name="path"/> read-only; the caller disposes it.</summary>
        public static SQLiteConnection OpenReadOnly(string path)
        {
            // A ';' in the path must not end the connection string early: the builder quotes it.
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3, ReadOnly = true };
            var connection = new SQLiteConnection(connectionString.ToString());
            try
            {
                connection.Open();
            }
            catch
            {
                connection.Dispose();
                throw;
            }
            return connection;
        }

        public static bool TableExists(SQLiteConnection connection, string table)
        {
            using (var command = new SQLiteCommand(@"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name", connection))
            {
                command.Parameters.AddWithValue(@"@name", table);
                return Convert.ToInt64(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>The names of <paramref name="table"/>'s columns, compared without case as SQLite compares them; empty for a table the library lacks.</summary>
        public static HashSet<string> ColumnNames(SQLiteConnection connection, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TableExists(connection, table))
                return columns;
            using (var command = new SQLiteCommand(@"PRAGMA table_info(" + table + @")", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    columns.Add(reader.GetString(1));
            }
            return columns;
        }
    }
}
