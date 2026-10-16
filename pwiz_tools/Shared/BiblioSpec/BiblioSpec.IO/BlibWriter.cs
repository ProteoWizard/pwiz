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
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using pwiz.Common.SystemUtil;

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// Writes a BiblioSpec <c>.blib</c> (schema <see cref="BlibSchema.CurrentMinorVersion"/>)
    /// that Skyline, Osprey and CarafeSharp read: the tables of <see cref="BlibSchema"/>, one
    /// <see cref="BlibSpectrumRecord"/> per <c>RefSpectra</c> row with its peaks, annotations,
    /// modifications and proteins, one <c>RetentionTimes</c> row per observation, the
    /// <c>LibMetadata</c> rows about the library as a whole (<see cref="BlibMetadata"/>), and
    /// whatever tables the writing tool keeps beside them (<see cref="IBlibExtensionTables"/>). Osprey's
    /// search output and CarafeSharp's predicted libraries are written through it, so a
    /// library either tool writes is the same database for every reader.
    ///
    /// <para>The file is written to a <see cref="SimpleFileSaver"/> temp beside
    /// <c>path</c> and moved over the final name by <see cref="Complete"/>, so
    /// until then any library already there is left as it is, and a run that stops short
    /// leaves no half-written library. Peak blobs are little-endian float64 m/z and float32
    /// intensity, zlib-compressed (Ionic.Zlib level 6, stock zlib's bytes, as Skyline's own
    /// writer does) only when that is smaller - the BiblioSpec reader's test for compression.
    /// Row ids are SQLite's autoincrement ids, in insertion order.</para>
    /// </summary>
    public sealed class BlibWriter : IDisposable
    {
        /// <summary>The library revision a new library starts at (<c>LibInfo.majorVersion</c>).</summary>
        public const int MAJOR_VERSION = 1;

        /// <summary><c>ScoreTypes</c> ids: a spectrum without a score, and a generic q-value.</summary>
        public const int SCORE_TYPE_UNKNOWN = 0;
        public const int SCORE_TYPE_GENERIC_QVALUE = 19;

        /// <summary><c>SpectrumSourceFiles.workflowType</c>: 0 for DDA, 1 for DIA.</summary>
        public const int WORKFLOW_TYPE_DDA = 0;
        public const int WORKFLOW_TYPE_DIA = 1;

        /// <summary><c>IonMobilityTypes</c> ids.</summary>
        public const int ION_MOBILITY_TYPE_NONE = 0;
        public const int ION_MOBILITY_TYPE_DRIFT_TIME = 1;
        public const int ION_MOBILITY_TYPE_INVERSE_K0 = 2;
        public const int ION_MOBILITY_TYPE_COMPENSATION_VOLTAGE = 3;

        /// <summary>
        /// The most annotation rows one INSERT takes; a spectrum with more peaks takes several.
        /// Each row binds 5 variables and the spectrum id 1 more, so 100 rows bind 501: under
        /// 999, SQLite's SQLITE_MAX_VARIABLE_NUMBER before 3.32, let alone the 32766 of the
        /// SQLite 3.46.1 in System.Data.SQLite 1.0.119.
        /// </summary>
        public const int MAX_ANNOTATIONS_PER_INSERT = 100;

        /// <summary>BiblioSpec's <c>ScoreTypes</c> rows, in id order.</summary>
        private static readonly string[,] SCORE_TYPES =
        {
            { @"UNKNOWN", @"NOT_A_PROBABILITY_VALUE" },
            { @"PERCOLATOR QVALUE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"PEPTIDE PROPHET SOMETHING", @"PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
            { @"SPECTRUM MILL", @"NOT_A_PROBABILITY_VALUE" },
            { @"IDPICKER FDR", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"MASCOT IONS SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"TANDEM EXPECTATION VALUE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"PROTEIN PILOT CONFIDENCE", @"PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
            { @"SCAFFOLD SOMETHING", @"PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
            { @"WATERS MSE PEPTIDE SCORE", @"NOT_A_PROBABILITY_VALUE" },
            { @"OMSSA EXPECTATION SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"PROTEIN PROSPECTOR EXPECTATION SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"SEQUEST XCORR", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"MAXQUANT SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"MORPHEUS SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"MSGF+ SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"PEAKS CONFIDENCE SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"BYONIC SCORE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"PEPTIDE SHAKER CONFIDENCE", @"PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
            { @"GENERIC Q-VALUE", @"PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
            { @"HARDKLOR IDOTP", @"PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
        };

        /// <summary>BiblioSpec's <c>IonMobilityTypes</c> rows, in id order.</summary>
        private static readonly string[] ION_MOBILITY_TYPES =
        {
            @"none", @"driftTime(msec)", @"inverseK0(Vsec/cm^2)", @"compensation(V)",
        };

        /// <summary>
        /// Indexes on the per-spectrum tables beyond BiblioSpec's own, so a reader that walks a
        /// spectrum's rows by RefSpectraID does not scan the table.
        /// </summary>
        private static readonly string[] EXTRA_INDEX_STATEMENTS =
        {
            @"CREATE INDEX idxRefIdProteins ON RefSpectraProteins (RefSpectraId)",
            @"CREATE INDEX idxRefIdModifications ON Modifications (RefSpectraID)",
            @"CREATE INDEX idxRefIdRetentionTimes ON RetentionTimes (RefSpectraID)",
        };

        private readonly SimpleFileSaver _file;
        private IBlibExtensionTables _extensions;
        private readonly Dictionary<string, long> _proteinIds = new Dictionary<string, long>(StringComparer.Ordinal);
        // The INSERT of n annotation rows at index n, prepared when first needed.
        private readonly AnnotationInsert[] _insertAnnotations = new AnnotationInsert[MAX_ANNOTATIONS_PER_INSERT + 1];
        private SQLiteConnection _connection;
        private SQLiteTransaction _batch;
        private PreparedInsert _insertSpectrum;
        private PreparedInsert _insertPeaks;
        private PreparedInsert _insertModification;
        private PreparedInsert _insertProtein;
        private PreparedInsert _insertSpectrumProtein;
        private PreparedInsert _insertRetentionTime;
        private PreparedInsert _insertMetadata;
        private int _spectrumCount;

        /// <summary>
        /// Starts the library that <see cref="Complete"/> writes to <paramref name="path"/>.
        /// <paramref name="libraryName"/> names it in <c>LibInfo</c> (the file name by default),
        /// and <paramref name="extensions"/>, when given, adds the writing tool's own tables
        /// before any row is written.
        /// </summary>
        public BlibWriter(string path, string libraryName = null, IBlibExtensionTables extensions = null)
        {
            _file = new SimpleFileSaver(path);
            _extensions = extensions;
            try
            {
                // A ';' in the path must not end the connection string early: the builder quotes it.
                var connectionString = new SQLiteConnectionStringBuilder { DataSource = _file.SafeName, Version = 3 };
                _connection = new SQLiteConnection(connectionString.ToString());
                _connection.Open();
                // The file is a temp until Complete moves it into place, so a crash loses nothing
                // durable: no journal to replay and no sync per transaction.
                Execute(@"PRAGMA synchronous=OFF");
                Execute(@"PRAGMA journal_mode=MEMORY");
                CreateSchema(libraryName ?? Path.GetFileName(path));
                PrepareStatements();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// The database being written, for the writing tool's own tables. Rows written through
        /// it inside <see cref="BeginBatch"/> / <see cref="CommitBatch"/> commit with the batch.
        /// </summary>
        public SQLiteConnection Connection
        {
            get { return _connection; }
        }

        /// <summary>The file being written, which <see cref="Complete"/> moves over the final name.</summary>
        public string TempPath
        {
            get { return _file.SafeName; }
        }

        /// <summary><c>RefSpectra</c> rows written so far; the next spectrum gets this plus one as its id.</summary>
        public int SpectrumCount
        {
            get { return _spectrumCount; }
        }

        /// <summary>Begins a transaction over the rows that follow; one per batch of spectra keeps the inserts fast.</summary>
        public void BeginBatch()
        {
            if (_batch == null)
                _batch = _connection.BeginTransaction();
        }

        /// <summary>Commits the batch <see cref="BeginBatch"/> began; without one, does nothing.</summary>
        public void CommitBatch()
        {
            if (_batch == null)
                return;
            _batch.Commit();
            _batch.Dispose();
            _batch = null;
        }

        /// <summary>
        /// Adds a <c>SpectrumSourceFiles</c> row and returns its id. <paramref name="fileName"/>
        /// is the spectrum source (BlibBuild resolves it to a full path),
        /// <paramref name="idFileName"/> the identification file, if any, and
        /// <paramref name="cutoffScore"/> the score threshold the spectra were filtered at, if any.
        /// </summary>
        public long AddSourceFile(string fileName, string idFileName, double? cutoffScore, int workflowType)
        {
            using (var command = new SQLiteCommand(
                       @"INSERT INTO SpectrumSourceFiles (fileName, idFileName, cutoffScore, workflowType) VALUES (@file, @idFile, @cutoff, @workflow)",
                       _connection))
            {
                command.Parameters.AddWithValue(@"@file", fileName);
                command.Parameters.AddWithValue(@"@idFile", (object)idFileName ?? DBNull.Value);
                command.Parameters.AddWithValue(@"@cutoff", cutoffScore.HasValue ? cutoffScore.Value : DBNull.Value);
                command.Parameters.AddWithValue(@"@workflow", workflowType);
                command.ExecuteNonQuery();
            }
            return _connection.LastInsertRowId;
        }

        /// <summary>
        /// Writes a spectrum: its <c>RefSpectra</c> row, its peak blobs, its annotations, its
        /// modifications and its protein mappings. Returns the <c>RefSpectra</c> id.
        /// </summary>
        public long AddSpectrum(BlibSpectrumRecord spectrum)
        {
            if (spectrum == null)
                throw new ArgumentNullException(nameof(spectrum));
            _insertSpectrum.Execute(spectrum.PeptideSeq, spectrum.PrecursorMz, spectrum.PrecursorCharge, spectrum.PeptideModSeq,
                spectrum.PrevAA, spectrum.NextAA, spectrum.Copies, spectrum.NumPeaks,
                Nullable(spectrum.IonMobility), Nullable(spectrum.CollisionalCrossSectionSqA), Nullable(spectrum.IonMobilityHighEnergyOffset),
                spectrum.IonMobilityType, Nullable(spectrum.RetentionTime), Nullable(spectrum.StartTime), Nullable(spectrum.EndTime),
                Nullable(spectrum.TotalIonCurrent), spectrum.MoleculeName, spectrum.ChemicalFormula, spectrum.PrecursorAdduct,
                spectrum.InchiKey, spectrum.OtherKeys, spectrum.FileId, (object)spectrum.SpecIdInFile ?? DBNull.Value,
                spectrum.Score, spectrum.ScoreType);
            long refId = _connection.LastInsertRowId;
            _spectrumCount++;
            _insertPeaks.Execute(refId, spectrum.MzBlob, spectrum.IntensityBlob);
            InsertAnnotations(refId, spectrum.Annotations);
            foreach (var modification in spectrum.Modifications)
                _insertModification.Execute(refId, modification.Position, modification.Mass);
            foreach (string accession in spectrum.Proteins)
            {
                if (string.IsNullOrEmpty(accession))
                    continue;
                _insertSpectrumProtein.Execute(refId, GetProteinId(accession));
            }
            return refId;
        }

        /// <summary>
        /// Adds a <c>RetentionTimes</c> row: one observation of spectrum <paramref name="refId"/>
        /// in source file <paramref name="sourceFileId"/>. A null <paramref name="retentionTime"/>
        /// is an observation without an identification; null <paramref name="startTime"/> and
        /// <paramref name="endTime"/> are an observation without peak boundaries (Skyline reads
        /// them as explicit boundaries when given). The ion mobility columns are null unless given.
        /// </summary>
        public void AddRetentionTime(long refId, long sourceFileId, double? retentionTime, double? startTime, double? endTime,
            double score, bool bestSpectrum, double? ionMobility = null, double? collisionalCrossSectionSqA = null,
            double? ionMobilityHighEnergyOffset = null, int ionMobilityType = ION_MOBILITY_TYPE_NONE)
        {
            _insertRetentionTime.Execute(refId, sourceFileId, Nullable(ionMobility), Nullable(collisionalCrossSectionSqA),
                Nullable(ionMobilityHighEnergyOffset), ionMobilityType, Nullable(retentionTime), Nullable(startTime), Nullable(endTime),
                score, bestSpectrum ? 1 : 0);
        }

        /// <summary>
        /// Adds or replaces a <c>LibMetadata</c> row (<see cref="BlibMetadata"/>): a value about
        /// the library as a whole, under a key the writing tool namespaces
        /// (<c>osprey.command</c>). A null <paramref name="value"/> is stored as NULL.
        /// </summary>
        public void AddMetadata(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException(@"A metadata key is required", nameof(key));
            _insertMetadata.Execute(key, (object)value ?? DBNull.Value);
        }

        /// <summary>
        /// Records the spectrum count, adds BiblioSpec's indexes and the writing tool's, closes
        /// the file and moves it over the final name, replacing any library there.
        /// </summary>
        public void Complete()
        {
            CommitBatch();
            using (var update = new SQLiteCommand(@"UPDATE LibInfo SET numSpecs = @count", _connection))
            {
                update.Parameters.AddWithValue(@"@count", _spectrumCount);
                update.ExecuteNonQuery();
            }
            foreach (string statement in BlibSchema.AllIndexStatements)
                Execute(statement);
            foreach (string statement in EXTRA_INDEX_STATEMENTS)
                Execute(statement);
            _extensions?.CreateIndexes(_connection);
            CloseConnection();
            _file.Commit();
        }

        /// <summary>
        /// Moves the completed library to <paramref name="path"/>, which nothing may hold, when
        /// <see cref="Complete"/> could not move it over the final name.
        /// </summary>
        public void CommitAs(string path)
        {
            _file.CommitAs(path);
        }

        /// <summary>Closes the file; without <see cref="Complete"/>, deletes it and leaves the final name as it was.</summary>
        public void Dispose()
        {
            CloseConnection();
            _file.Dispose();
        }

        /// <summary>The <c>RefSpectraPeaks.peakMZ</c> blob of these m/z values: little-endian float64, compressed when that is smaller.</summary>
        public static byte[] CompressMzs(double[] mzs)
        {
            var bytes = new byte[mzs.Length * sizeof(double)];
            Buffer.BlockCopy(mzs, 0, bytes, 0, bytes.Length);
            return CompressIfSmaller(bytes);
        }

        /// <summary>The <c>RefSpectraPeaks.peakIntensity</c> blob of these intensities: little-endian float32, compressed when that is smaller.</summary>
        public static byte[] CompressIntensities(float[] intensities)
        {
            var bytes = new byte[intensities.Length * sizeof(float)];
            Buffer.BlockCopy(intensities, 0, bytes, 0, bytes.Length);
            return CompressIfSmaller(bytes);
        }

        /// <summary>Reverses the blob encoding: raw when the length matches <paramref name="expectedLength"/>, else zlib.</summary>
        public static byte[] DecodeBlob(byte[] blob, int expectedLength)
        {
            if (blob.Length == expectedLength)
                return blob;
            using (var input = new MemoryStream(blob))
            using (var zlib = new Ionic.Zlib.ZlibStream(input, Ionic.Zlib.CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedLength))
            {
                zlib.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>The m/z values of a <c>peakMZ</c> blob holding <paramref name="count"/> peaks.</summary>
        public static double[] DecodeMzs(byte[] blob, int count)
        {
            var bytes = DecodeBlob(blob, count * sizeof(double));
            var values = new double[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        /// <summary>The intensities of a <c>peakIntensity</c> blob holding <paramref name="count"/> peaks.</summary>
        public static float[] DecodeIntensities(byte[] blob, int count)
        {
            var bytes = DecodeBlob(blob, count * sizeof(float));
            var values = new float[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private void CreateSchema(string libraryName)
        {
            foreach (string statement in BlibSchema.AllCreateTableStatements)
                Execute(statement);
            Execute(BlibSchema.CreateRetentionTimes);
            Execute(BlibSchema.CreateLibMetadata);
            _extensions?.CreateTables(_connection);
            using (var transaction = _connection.BeginTransaction())
            {
                using (var info = new SQLiteCommand(@"INSERT INTO LibInfo VALUES (@lsid, @time, 0, @major, @minor)", _connection))
                {
                    info.Parameters.AddWithValue(@"@lsid", BlibSchema.NonRedundantLsid(libraryName));
                    info.Parameters.AddWithValue(@"@time", BlibSchema.FormatCreateTime(DateTime.Now));
                    info.Parameters.AddWithValue(@"@major", MAJOR_VERSION);
                    info.Parameters.AddWithValue(@"@minor", BlibSchema.CurrentMinorVersion);
                    info.ExecuteNonQuery();
                }
                for (int i = 0; i < SCORE_TYPES.GetLength(0); i++)
                {
                    using (var score = new SQLiteCommand(@"INSERT INTO ScoreTypes VALUES (@id, @name, @probability)", _connection))
                    {
                        score.Parameters.AddWithValue(@"@id", i);
                        score.Parameters.AddWithValue(@"@name", SCORE_TYPES[i, 0]);
                        score.Parameters.AddWithValue(@"@probability", SCORE_TYPES[i, 1]);
                        score.ExecuteNonQuery();
                    }
                }
                for (int i = 0; i < ION_MOBILITY_TYPES.Length; i++)
                {
                    using (var mobility = new SQLiteCommand(@"INSERT INTO IonMobilityTypes VALUES (@id, @name)", _connection))
                    {
                        mobility.Parameters.AddWithValue(@"@id", i);
                        mobility.Parameters.AddWithValue(@"@name", ION_MOBILITY_TYPES[i]);
                        mobility.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        private void PrepareStatements()
        {
            _insertSpectrum = Prepare(@"INSERT INTO RefSpectra (peptideSeq, precursorMZ, precursorCharge, peptideModSeq, prevAA, nextAA, " +
                                      @"copies, numPeaks, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
                                      @"retentionTime, startTime, endTime, totalIonCurrent, moleculeName, chemicalFormula, precursorAdduct, " +
                                      @"inchiKey, otherKeys, fileID, SpecIDinFile, score, scoreType) VALUES (@seq, @mz, @charge, @modSeq, " +
                                      @"@prevAA, @nextAA, @copies, @numPeaks, @im, @ccs, @imOffset, @imType, @rt, @start, @end, @tic, " +
                                      @"@molecule, @formula, @adduct, @inchi, @otherKeys, @file, @specId, @score, @scoreType)",
                new[]
                {
                    @"@seq", @"@mz", @"@charge", @"@modSeq", @"@prevAA", @"@nextAA", @"@copies", @"@numPeaks", @"@im", @"@ccs", @"@imOffset",
                    @"@imType", @"@rt", @"@start", @"@end", @"@tic", @"@molecule", @"@formula", @"@adduct", @"@inchi", @"@otherKeys",
                    @"@file", @"@specId", @"@score", @"@scoreType",
                },
                new[]
                {
                    DbType.String, DbType.Double, DbType.Int32, DbType.String, DbType.String, DbType.String, DbType.Int32, DbType.Int32,
                    DbType.Double, DbType.Double, DbType.Double, DbType.Int32, DbType.Double, DbType.Double, DbType.Double, DbType.Double,
                    DbType.String, DbType.String, DbType.String, DbType.String, DbType.String, DbType.Int64, DbType.String, DbType.Double,
                    DbType.Int32,
                });
            _insertPeaks = Prepare(@"INSERT INTO RefSpectraPeaks (RefSpectraID, peakMZ, peakIntensity) VALUES (@id, @mz, @intensity)",
                new[] { @"@id", @"@mz", @"@intensity" }, new[] { DbType.Int64, DbType.Binary, DbType.Binary });
            _insertModification = Prepare(@"INSERT INTO Modifications (RefSpectraID, position, mass) VALUES (@id, @position, @mass)",
                new[] { @"@id", @"@position", @"@mass" }, new[] { DbType.Int64, DbType.Int32, DbType.Double });
            _insertProtein = Prepare(@"INSERT INTO Proteins (accession) VALUES (@accession)",
                new[] { @"@accession" }, new[] { DbType.String });
            _insertSpectrumProtein = Prepare(@"INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) VALUES (@id, @protein)",
                new[] { @"@id", @"@protein" }, new[] { DbType.Int64, DbType.Int64 });
            _insertRetentionTime = Prepare(@"INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, SpectrumSourceID, ionMobility, " +
                                           @"collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, " +
                                           @"endTime, score, bestSpectrum) VALUES (@id, 0, @file, @im, @ccs, @imOffset, @imType, @rt, @start, " +
                                           @"@end, @score, @best)",
                new[] { @"@id", @"@file", @"@im", @"@ccs", @"@imOffset", @"@imType", @"@rt", @"@start", @"@end", @"@score", @"@best" },
                new[]
                {
                    DbType.Int64, DbType.Int64, DbType.Double, DbType.Double, DbType.Double, DbType.Int32, DbType.Double, DbType.Double,
                    DbType.Double, DbType.Double, DbType.Int32,
                });
            _insertMetadata = Prepare(BlibMetadata.INSERT_SQL,
                new[] { @"@key", @"@value" }, new[] { DbType.String, DbType.String });
        }

        /// <summary>
        /// One annotation row per peak, in peak order: one INSERT for a spectrum of up to
        /// <see cref="MAX_ANNOTATIONS_PER_INSERT"/> peaks, several above that.
        /// </summary>
        private void InsertAnnotations(long refId, IReadOnlyList<BlibPeakAnnotation> annotations)
        {
            for (int start = 0; start < annotations.Count; start += MAX_ANNOTATIONS_PER_INSERT)
            {
                int rows = Math.Min(MAX_ANNOTATIONS_PER_INSERT, annotations.Count - start);
                var insert = _insertAnnotations[rows] ?? (_insertAnnotations[rows] = new AnnotationInsert(_connection, rows));
                insert.Execute(refId, annotations, start);
            }
        }

        private long GetProteinId(string accession)
        {
            if (_proteinIds.TryGetValue(accession, out long proteinId))
                return proteinId;
            _insertProtein.Execute(accession);
            proteinId = _connection.LastInsertRowId;
            _proteinIds.Add(accession, proteinId);
            return proteinId;
        }

        private PreparedInsert Prepare(string sql, string[] names, DbType[] types)
        {
            return new PreparedInsert(_connection, sql, names, types);
        }

        private void Execute(string sql)
        {
            using (var command = new SQLiteCommand(sql, _connection))
                command.ExecuteNonQuery();
        }

        private void CloseConnection()
        {
            if (_batch != null)
            {
                // A batch left open is a write that did not finish: nothing of it reaches the file.
                _batch.Dispose();
                _batch = null;
            }
            _insertSpectrum?.Dispose();
            _insertPeaks?.Dispose();
            _insertModification?.Dispose();
            _insertProtein?.Dispose();
            _insertSpectrumProtein?.Dispose();
            _insertRetentionTime?.Dispose();
            _insertMetadata?.Dispose();
            _insertSpectrum = _insertPeaks = _insertModification = null;
            _insertProtein = _insertSpectrumProtein = _insertRetentionTime = _insertMetadata = null;
            for (int i = 0; i < _insertAnnotations.Length; i++)
            {
                _insertAnnotations[i]?.Dispose();
                _insertAnnotations[i] = null;
            }
            // The tool's prepared statements are on this connection: they go before it does, or
            // System.Data.SQLite keeps the file open until they are finalized.
            _extensions?.Dispose();
            _extensions = null;
            _connection?.Dispose();
            _connection = null;
        }

        private static object Nullable(double? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        /// <summary>
        /// zlib at level 6 through Ionic.Zlib, whose output is stock zlib's (the bytes Skyline's
        /// writer and Rust osprey's flate2 produce), kept only when it is shorter than the raw
        /// bytes - the BiblioSpec reader's test for compression. .NET's own ZLibStream is not
        /// used: its deflate bytes differ, which would split the parity both tools' regressions
        /// hold against their goldens.
        /// </summary>
        private static byte[] CompressIfSmaller(byte[] raw)
        {
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var zlib = new Ionic.Zlib.ZlibStream(output, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.Level6, true))
                    zlib.Write(raw, 0, raw.Length);
                compressed = output.ToArray();
            }
            return compressed.Length < raw.Length ? compressed : raw;
        }

        /// <summary>
        /// A prepared single-row INSERT whose parameters are set by position, without the name
        /// lookup of <see cref="SQLiteParameterCollection"/>.
        /// </summary>
        public sealed class PreparedInsert : IDisposable
        {
            private readonly SQLiteCommand _command;
            private readonly SQLiteParameter[] _parameters;

            public PreparedInsert(SQLiteConnection connection, string sql, string[] names, DbType[] types)
            {
                _command = new SQLiteCommand(sql, connection);
                _parameters = new SQLiteParameter[names.Length];
                for (int i = 0; i < names.Length; i++)
                    _parameters[i] = _command.Parameters.Add(names[i], types[i]);
                _command.Prepare();
            }

            /// <summary>Inserts a row with these values of the parameters, in the order they were named.</summary>
            public void Execute(params object[] values)
            {
                for (int i = 0; i < _parameters.Length; i++)
                    _parameters[i].Value = values[i];
                _command.ExecuteNonQuery();
            }

            public void Dispose()
            {
                _command.Dispose();
            }
        }

        /// <summary>
        /// A prepared multi-row INSERT of a fixed number of consecutive peak annotations of one
        /// spectrum. SQLite inserts the rows in the order listed, so their ids and values are
        /// those of one single-row INSERT per peak, with the same parameter types.
        /// </summary>
        private sealed class AnnotationInsert : IDisposable
        {
            private readonly SQLiteCommand _command;
            private readonly SQLiteParameter _id;
            private readonly SQLiteParameter[] _peakIndex;
            private readonly SQLiteParameter[] _name;
            private readonly SQLiteParameter[] _charge;
            private readonly SQLiteParameter[] _theoretical;
            private readonly SQLiteParameter[] _observed;

            public AnnotationInsert(SQLiteConnection connection, int rows)
            {
                var sql = new StringBuilder(@"INSERT INTO RefSpectraPeakAnnotations (RefSpectraID, peakIndex, name, formula, inchiKey, " +
                                            @"otherKeys, charge, adduct, comment, mzTheoretical, mzObserved) VALUES ");
                for (int row = 0; row < rows; row++)
                {
                    if (row > 0)
                        sql.Append(@", ");
                    sql.AppendFormat(CultureInfo.InvariantCulture,
                        @"(@id, @index{0}, @name{0}, '', '', '', @charge{0}, '', '', @theoretical{0}, @observed{0})", row);
                }
                _command = new SQLiteCommand(sql.ToString(), connection);
                _id = _command.Parameters.Add(@"@id", DbType.Int64);
                _peakIndex = new SQLiteParameter[rows];
                _name = new SQLiteParameter[rows];
                _charge = new SQLiteParameter[rows];
                _theoretical = new SQLiteParameter[rows];
                _observed = new SQLiteParameter[rows];
                for (int row = 0; row < rows; row++)
                {
                    _peakIndex[row] = AddParameter(@"@index", row, DbType.Int32);
                    _name[row] = AddParameter(@"@name", row, DbType.String);
                    _charge[row] = AddParameter(@"@charge", row, DbType.Int32);
                    _theoretical[row] = AddParameter(@"@theoretical", row, DbType.Double);
                    _observed[row] = AddParameter(@"@observed", row, DbType.Double);
                }
                _command.Prepare();
            }

            /// <summary>Inserts the annotations from <paramref name="start"/> of spectrum <paramref name="refId"/>.</summary>
            public void Execute(long refId, IReadOnlyList<BlibPeakAnnotation> annotations, int start)
            {
                _id.Value = refId;
                for (int row = 0; row < _name.Length; row++)
                {
                    var annotation = annotations[start + row];
                    _peakIndex[row].Value = annotation.PeakIndex;
                    _name[row].Value = annotation.Name;
                    _charge[row].Value = annotation.Charge;
                    _theoretical[row].Value = annotation.MzTheoretical;
                    _observed[row].Value = annotation.MzObserved;
                }
                _command.ExecuteNonQuery();
            }

            public void Dispose()
            {
                _command.Dispose();
            }

            private SQLiteParameter AddParameter(string name, int row, DbType type)
            {
                return _command.Parameters.Add(name + row.ToString(CultureInfo.InvariantCulture), type);
            }
        }
    }
}
