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
    /// The tables Osprey keeps in its libraries beside BiblioSpec's, created and indexed by
    /// <see cref="BlibWriter"/> through <see cref="IBlibExtensionTables"/>: <c>OspreyMetadata</c>
    /// (key-value rows, among them the validity stamp), and one row per <c>RefSpectra</c> in
    /// <c>OspreyPeakBoundaries</c>, <c>OspreyRunScores</c> and <c>OspreyExperimentScores</c>,
    /// mirroring Rust osprey's <c>BlibWriter</c>. <c>OspreyCoefficients</c> is created and
    /// indexed but written with no rows: the per-scan coefficient series is not plumbed through
    /// Stage 7, and the table is reserved for the chromatogram-export feature.
    /// </summary>
    public sealed class OspreyBlibTables : IBlibExtensionTables
    {
        private static readonly string[] TABLE_STATEMENTS =
        {
            ArtifactStamp.BLIB_TABLE_DDL,
            @"CREATE TABLE OspreyPeakBoundaries (
                id INTEGER PRIMARY KEY,
                RefSpectraID INTEGER,
                FileName TEXT,
                StartRT REAL,
                EndRT REAL,
                ApexRT REAL,
                ApexIntensity REAL,
                IntegratedArea REAL,
                FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id))",
            @"CREATE TABLE OspreyRunScores (
                id INTEGER PRIMARY KEY,
                RefSpectraID INTEGER,
                FileName TEXT,
                RunQValue REAL,
                DiscriminantScore REAL,
                PosteriorErrorProb REAL,
                FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id))",
            @"CREATE TABLE OspreyExperimentScores (
                id INTEGER PRIMARY KEY,
                RefSpectraID INTEGER,
                ExperimentQValue REAL,
                NRunsDetected INTEGER,
                NRunsSearched INTEGER,
                FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id))",
            @"CREATE TABLE OspreyCoefficients (
                id INTEGER PRIMARY KEY,
                RefSpectraID INTEGER,
                FileName TEXT,
                ScanNumber INTEGER,
                RT REAL,
                Coefficient REAL,
                FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id))",
        };

        private static readonly string[] INDEX_STATEMENTS =
        {
            @"CREATE INDEX idx_boundaries_refid ON OspreyPeakBoundaries(RefSpectraID)",
            @"CREATE INDEX idx_runscores_refid ON OspreyRunScores(RefSpectraID)",
            @"CREATE INDEX idx_expscores_refid ON OspreyExperimentScores(RefSpectraID)",
            @"CREATE INDEX idx_coefficients_refid ON OspreyCoefficients(RefSpectraID)",
        };

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

        private BlibWriter.PreparedInsert _insertPeakBoundaries;
        private BlibWriter.PreparedInsert _insertRunScores;
        private BlibWriter.PreparedInsert _insertExperimentScores;
        private BlibWriter.PreparedInsert _insertCoefficient;
        private BlibWriter.PreparedInsert _insertMetadata;

        public void CreateTables(SQLiteConnection connection)
        {
            foreach (string statement in TABLE_STATEMENTS)
                Execute(connection, statement);
            _insertPeakBoundaries = new BlibWriter.PreparedInsert(connection,
                @"INSERT INTO OspreyPeakBoundaries (RefSpectraID, FileName, StartRT, EndRT, ApexRT, ApexIntensity, IntegratedArea) VALUES (@r, @f, @s, @e, @a, @ai, @ia)",
                new[] { @"@r", @"@f", @"@s", @"@e", @"@a", @"@ai", @"@ia" },
                new[] { DbType.Int64, DbType.String, DbType.Double, DbType.Double, DbType.Double, DbType.Double, DbType.Double });
            _insertRunScores = new BlibWriter.PreparedInsert(connection,
                @"INSERT INTO OspreyRunScores (RefSpectraID, FileName, RunQValue, DiscriminantScore, PosteriorErrorProb) VALUES (@r, @f, @q, @d, @p)",
                new[] { @"@r", @"@f", @"@q", @"@d", @"@p" },
                new[] { DbType.Int64, DbType.String, DbType.Double, DbType.Double, DbType.Double });
            _insertExperimentScores = new BlibWriter.PreparedInsert(connection,
                @"INSERT INTO OspreyExperimentScores (RefSpectraID, ExperimentQValue, NRunsDetected, NRunsSearched) VALUES (@r, @q, @nd, @ns)",
                new[] { @"@r", @"@q", @"@nd", @"@ns" },
                new[] { DbType.Int64, DbType.Double, DbType.Int32, DbType.Int32 });
            _insertCoefficient = new BlibWriter.PreparedInsert(connection,
                @"INSERT INTO OspreyCoefficients (RefSpectraID, FileName, ScanNumber, RT, Coefficient) VALUES (@r, @f, @s, @t, @c)",
                new[] { @"@r", @"@f", @"@s", @"@t", @"@c" },
                new[] { DbType.Int64, DbType.String, DbType.Int32, DbType.Double, DbType.Double });
            _insertMetadata = new BlibWriter.PreparedInsert(connection, ArtifactStamp.BLIB_INSERT_SQL,
                new[] { @"@key", @"@value" },
                new[] { DbType.String, DbType.String });
        }

        public void CreateIndexes(SQLiteConnection connection)
        {
            foreach (string statement in INDEX_STATEMENTS)
                Execute(connection, statement);
        }

        /// <summary>
        /// One <c>OspreyPeakBoundaries</c> row for a passing precursor's best run. Mirrors Rust
        /// <c>BlibWriter::add_peak_boundaries</c>; one row per <c>RefSpectra</c>.
        /// </summary>
        public void AddPeakBoundaries(long refId, string fileName,
            double startRt, double endRt, double apexRt,
            double apexIntensity, double integratedArea)
        {
            _insertPeakBoundaries.Execute(refId, fileName, startRt, endRt, apexRt, apexIntensity, integratedArea);
        }

        /// <summary>
        /// One <c>OspreyRunScores</c> row for a passing precursor's best run. Mirrors Rust
        /// <c>BlibWriter::add_run_scores</c>; one row per <c>RefSpectra</c>.
        /// </summary>
        public void AddRunScores(long refId, string fileName,
            double runQValue, double discriminantScore, double posteriorErrorProb)
        {
            _insertRunScores.Execute(refId, fileName, runQValue, discriminantScore, posteriorErrorProb);
        }

        /// <summary>
        /// One <c>OspreyExperimentScores</c> row per passing precursor. Mirrors Rust
        /// <c>BlibWriter::add_experiment_scores</c>.
        /// </summary>
        public void AddExperimentScores(long refId,
            double experimentQValue, int nRunsDetected, int nRunsSearched)
        {
            _insertExperimentScores.Execute(refId, experimentQValue, nRunsDetected, nRunsSearched);
        }

        /// <summary>
        /// One <c>OspreyCoefficients</c> row for a single scan within a chromatographic peak's
        /// coefficient time series. Mirrors Rust <c>BlibWriter::add_coefficient</c>; many rows
        /// per <c>RefSpectra</c>. Not called by the pipeline today (see the class summary).
        /// </summary>
        public void AddCoefficient(long refId, string fileName,
            uint scanNumber, double rt, double coefficient)
        {
            _insertCoefficient.Execute(refId, fileName, (int)scanNumber, rt, coefficient);
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
            _insertPeakBoundaries?.Dispose();
            _insertRunScores?.Dispose();
            _insertExperimentScores?.Dispose();
            _insertCoefficient?.Dispose();
            _insertMetadata?.Dispose();
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection))
                command.ExecuteNonQuery();
        }
    }
}
