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
using System.Data.SQLite;
using System.IO;

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// The <c>LibMetadata</c> table of a library (<see cref="BlibSchema.CreateLibMetadata"/>):
    /// single-row text values about the library as a whole, each under a key. Keys are
    /// namespaced by the tool that writes them (<c>osprey.version</c>, <c>osprey.command</c>,
    /// <c>carafe.command</c>), with a small unprefixed set every tool honors
    /// (<c>library_contract</c>). Every tool that makes or transforms a library records its full
    /// command line under <c>&lt;tool&gt;.command</c>, so a library carries how it was made. A
    /// reader tests for the table, never for a schema version. <see cref="BlibWriter"/> creates
    /// the table in every library it writes and fills it through
    /// <see cref="BlibWriter.AddMetadata"/>; this class reads it back.
    /// </summary>
    public static class BlibMetadata
    {
        public const string TABLE = @"LibMetadata";

        /// <summary>
        /// The table's name in libraries Osprey wrote before 2026-10-15. Read, never written, for
        /// one release, so a search started under the old name still resumes.
        /// </summary>
        public const string LEGACY_TABLE = @"OspreyMetadata";

        /// <summary>The <c>INSERT OR REPLACE</c> the writer prepares; bind <c>@key</c> and <c>@value</c>.</summary>
        public const string INSERT_SQL = @"INSERT OR REPLACE INTO LibMetadata (key, value) VALUES (@key, @value)";

        /// <summary>
        /// The value under <paramref name="key"/> in the library at <paramref name="path"/>, or
        /// null when the file is missing, unreadable, has no metadata table or no such key.
        /// Never throws: a reader that cannot tell treats the library as saying nothing.
        /// </summary>
        public static string Read(string path, string key)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                using (var connection = BlibSqlite.OpenReadOnly(path))
                    return Read(connection, TABLE, key) ?? Read(connection, LEGACY_TABLE, key);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }

        private static string Read(SQLiteConnection connection, string table, string key)
        {
            if (!BlibSqlite.TableExists(connection, table))
                return null;
            // The legacy table spelled its columns Key and Value; SQLite names are case-insensitive.
            using (var command = new SQLiteCommand(@"SELECT value FROM " + table + @" WHERE key = @key", connection))
            {
                command.Parameters.AddWithValue(@"@key", key);
                return command.ExecuteScalar() as string;
            }
        }
    }
}
