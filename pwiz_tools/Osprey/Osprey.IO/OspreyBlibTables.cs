/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Data;
using System.Data.SQLite;
using System.IO;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Pipeline;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The one table Osprey keeps in its libraries beside BiblioSpec's, created by
    /// <see cref="BlibWriter"/> through <see cref="IBlibExtensionTables"/>: <c>OspreyMetadata</c>,
    /// key-value rows about the library as a whole, among them the validity stamp. Every
    /// per-spectrum value Osprey reports is in the standard tables: the experiment q-value is
    /// <c>RefSpectra.score</c>, the run count <c>RefSpectra.copies</c>, and each run's q-value
    /// and peak boundaries its <c>RetentionTimes</c> row. The <c>Osprey*</c> score and boundary
    /// tables Rust osprey writes beside them were dropped (2026-10-15): tool information in
    /// tables no reader supports is not to be encouraged, and they repeated those columns.
    /// </summary>
    public sealed class OspreyBlibTables : IBlibExtensionTables
    {
        /// <summary>
        /// The validity stamp recorded in a blib's <c>OspreyMetadata</c> table, or null when the
        /// file is missing, unreadable or unstamped. Never throws.
        /// </summary>
        public static ArtifactStamp ReadStamp(string path)
        {
            return ArtifactStamp.Parse(ReadMetadata(path, ArtifactStamp.PARQUET_KEY));
        }

        /// <summary>
        /// The value of <paramref name="key"/> in a blib's <c>OspreyMetadata</c> table, or null
        /// when the file is missing, unreadable, has no such table or no such key. Never throws.
        /// </summary>
        public static string ReadMetadata(string path, string key)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3, ReadOnly = true };
                using (var connection = new SQLiteConnection(connectionString.ToString()))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(ArtifactStamp.BLIB_SELECT_SQL, connection))
                    {
                        command.Parameters.AddWithValue(@"@key", key);
                        return command.ExecuteScalar() as string;
                    }
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }

        private BlibWriter.PreparedInsert _insertMetadata;

        public void CreateTables(SQLiteConnection connection)
        {
            using (var command = new SQLiteCommand(ArtifactStamp.BLIB_TABLE_DDL, connection))
                command.ExecuteNonQuery();
            _insertMetadata = new BlibWriter.PreparedInsert(connection, ArtifactStamp.BLIB_INSERT_SQL,
                new[] { @"@key", @"@value" },
                new[] { DbType.String, DbType.String });
        }

        /// <summary>The metadata table is keyed by its primary key and needs no index.</summary>
        public void CreateIndexes(SQLiteConnection connection)
        {
        }

        /// <summary>Adds or replaces an <c>OspreyMetadata</c> key-value row.</summary>
        public void AddMetadata(string key, string value)
        {
            _insertMetadata.Execute(key, value);
        }

        /// <summary>
        /// Records <paramref name="stamp"/> in <c>OspreyMetadata</c> under
        /// <see cref="ArtifactStamp.PARQUET_KEY"/>, so the blib carries its own validity record.
        /// </summary>
        public void AddStamp(ArtifactStamp stamp)
        {
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));
            AddMetadata(ArtifactStamp.PARQUET_KEY, stamp.ToString());
        }

        public void Dispose()
        {
            _insertMetadata?.Dispose();
        }
    }
}
