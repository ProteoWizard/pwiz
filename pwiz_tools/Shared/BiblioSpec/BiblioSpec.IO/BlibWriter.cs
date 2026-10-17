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
using System.Runtime.InteropServices;
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
    /// whatever tables the writing tool keeps beside them (<see cref="IBlibExtensionTables"/>).
    /// BiblioSpec's own BlibBuild and BlibFilter, Osprey's search output and CarafeSharp's
    /// predicted libraries are written through it, so a library any of them writes is the same
    /// database for every reader.
    ///
    /// <para>The constructor starts a new non-redundant library with every table. BiblioSpec
    /// takes the longer way in through <see cref="Open"/>: the database with no schema yet, so
    /// that it can attach its input libraries first (a filtered library's LSID comes from the
    /// redundant one) and then <see cref="CreateSchema"/>; or, when appending, the library it
    /// is adding to, written in place. A redundant library has no <c>RetentionTimes</c> table
    /// and a library carries <c>LibMetadata</c> once something is recorded in it.</para>
    ///
    /// <para>A new library is written to a <see cref="SimpleFileSaver"/> temp beside
    /// <c>path</c> and moved over the final name by <see cref="Complete"/>, so
    /// until then any library already there is left as it is, and a run that stops short
    /// leaves no half-written library. An appended library is written in place, as BiblioSpec
    /// has always appended: its own rollback journal covers a run that stops short, and a copy
    /// of a redundant library of some gigabytes to add one more file's spectra is not worth
    /// what it would buy. Peak blobs are little-endian float64 m/z and float32
    /// intensity, zlib-compressed (Ionic.Zlib level 6, stock zlib's bytes, as Skyline's own
    /// writer does) only when that is smaller - the BiblioSpec reader's test for compression.
    /// Row ids are SQLite's autoincrement ids, in insertion order.</para>
    /// </summary>
    public sealed class BlibWriter : IDisposable
    {
        /// <summary>The library revision a new library gets (<c>LibInfo.majorVersion</c>) unless <see cref="SetRevision"/> says otherwise.</summary>
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

        /// <summary>
        /// Indexes on the per-spectrum tables beyond BiblioSpec's own, so a reader that walks a
        /// spectrum's rows by RefSpectraID does not scan the table; each with the table it is
        /// on, since a redundant library has no RetentionTimes.
        /// </summary>
        private static readonly (string Table, string Name, string Statement)[] EXTRA_INDEXES =
        {
            (@"RefSpectraProteins", @"idxRefIdProteins", @"CREATE INDEX idxRefIdProteins ON RefSpectraProteins (RefSpectraId)"),
            (@"Modifications", @"idxRefIdModifications", @"CREATE INDEX idxRefIdModifications ON Modifications (RefSpectraID)"),
            (@"RetentionTimes", @"idxRefIdRetentionTimes", @"CREATE INDEX idxRefIdRetentionTimes ON RetentionTimes (RefSpectraID)"),
        };

        // The temp a new library is written to; null for a library appended to in place.
        private readonly SimpleFileSaver _file;
        private readonly string _path;
        private IBlibExtensionTables _extensions;
        private readonly Dictionary<string, long> _proteinIds = new Dictionary<string, long>(StringComparer.Ordinal);
        // The INSERT of n annotation rows at index n, prepared when first needed.
        private readonly AnnotationInsert[] _insertAnnotations = new AnnotationInsert[MAX_ANNOTATIONS_PER_INSERT + 1];
        private SQLiteConnection _connection;
        private SQLiteTransaction _batch;
        // Prepared on the first row written, once the schema exists.
        private PreparedInsert _insertSpectrum;
        private PreparedInsert _insertPeaks;
        private PreparedInsert _insertModification;
        private PreparedInsert _insertProtein;
        private PreparedInsert _insertSpectrumProtein;
        // Prepared, with its table created, by EnsureRetentionTimes: a redundant library has none.
        private PreparedInsert _insertRetentionTime;
        // Prepared, with its table created, on the first AddMetadata.
        private PreparedInsert _insertMetadata;
        // Prepared, with its table created, on the first AddDecoyPair: a library without
        // supplied decoys has no DecoyPairs table.
        private PreparedInsert _insertDecoyPair;
        private int _spectrumCount;
        private int _revision = MAJOR_VERSION;

        /// <summary>
        /// Starts the non-redundant library that <see cref="Complete"/> writes to
        /// <paramref name="path"/>, with every table. <paramref name="libraryName"/> names it in
        /// <c>LibInfo</c> (the file name by default), and <paramref name="extensions"/>, when
        /// given, adds the writing tool's own tables before any row is written.
        /// </summary>
        public BlibWriter(string path, string libraryName = null, IBlibExtensionTables extensions = null)
            : this(path, false)
        {
            _extensions = extensions;
            try
            {
                CreateSchema(BlibSchema.NonRedundantLsid(libraryName ?? Path.GetFileName(path)));
                EnsureRetentionTimes();
                EnsureMetadata();
                _extensions?.CreateTables(_connection);
                EnsurePrepared();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private BlibWriter(string path, bool append)
        {
            _path = Path.GetFullPath(path);
            _file = append ? null : new SimpleFileSaver(path);
            try
            {
                // A ';' in the path must not end the connection string early: the builder quotes it.
                var connectionString = new SQLiteConnectionStringBuilder { DataSource = TempPath, Version = 3 };
                _connection = new SQLiteConnection(connectionString.ToString());
                _connection.Open();
                // No sync per transaction, as BiblioSpec has always written. A new library is a
                // temp until Complete moves it into place, so a crash loses nothing durable and
                // there is no journal to replay either; an appended library keeps its rollback
                // journal, which is what makes writing it in place safe.
                Execute(@"PRAGMA synchronous=OFF");
                if (!append)
                    Execute(@"PRAGMA journal_mode=MEMORY");
                if (append)
                {
                    _spectrumCount = (int)Scalar(@"SELECT COUNT(*) FROM RefSpectra");
                    LoadProteinIds();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Opens the library BiblioSpec's BlibBuild or BlibFilter writes to <paramref name="path"/>
        /// without a schema, for the tool to attach its input libraries to
        /// <see cref="Connection"/> and then <see cref="CreateSchema"/>, with
        /// <see cref="Complete"/> moving the result over <paramref name="path"/>; or, with
        /// <paramref name="append"/>, the library at <paramref name="path"/> itself, schema and
        /// rows included, for the tool to add to in place (it brings a library of an older schema
        /// up to date itself, and drops the indexes with <see cref="DropIndexes"/> before the bulk
        /// inserts).
        /// </summary>
        public static BlibWriter Open(string path, bool append)
        {
            return new BlibWriter(path, append);
        }

        /// <summary>
        /// The database being written, for the writing tool's own tables and for BiblioSpec's
        /// transfers between an attached library and this one. Rows written through it inside
        /// <see cref="BeginBatch"/> / <see cref="CommitBatch"/> commit with the batch.
        /// </summary>
        public SQLiteConnection Connection
        {
            get { return _connection; }
        }

        /// <summary>The file being written: the temp <see cref="Complete"/> moves over the final name, or the library itself when appending.</summary>
        public string TempPath
        {
            get { return _file != null ? _file.SafeName : _path; }
        }

        /// <summary><c>RefSpectra</c> rows so far, those of an appended library included; the next spectrum gets this plus one as its id.</summary>
        public int SpectrumCount
        {
            get { return _spectrumCount; }
        }

        /// <summary>
        /// Creates the tables of <see cref="BlibSchema"/> in a library <see cref="Open"/> began
        /// without them, with their seed rows: <c>LibInfo</c> under <paramref name="lsid"/>, the
        /// spectrum count and revision to be set by <see cref="Complete"/>, the <c>ScoreTypes</c>
        /// and the <c>IonMobilityTypes</c>. Not <c>RetentionTimes</c>, which
        /// <see cref="EnsureRetentionTimes"/> adds, nor the optional tables.
        /// </summary>
        public void CreateSchema(string lsid)
        {
            if (TableExists(@"LibInfo"))
                throw new InvalidOperationException(@"The library already has its schema");
            foreach (string statement in BlibSchema.AllCreateTableStatements)
                Execute(statement);
            using (var transaction = _connection.BeginTransaction())
            {
                // -1 spectra and revision 0 until Complete counts and stamps the library, as
                // BiblioSpec has always written a library in progress.
                using (var info = new SQLiteCommand(@"INSERT INTO LibInfo VALUES (@lsid, @time, -1, 0, @minor)", _connection))
                {
                    info.Parameters.AddWithValue(@"@lsid", lsid);
                    info.Parameters.AddWithValue(@"@time", BlibSchema.FormatCreateTime(DateTime.Now));
                    info.Parameters.AddWithValue(@"@minor", BlibSchema.CurrentMinorVersion);
                    info.ExecuteNonQuery();
                }
                for (int i = 0; i < BlibSchema.ScoreTypes.Length; i++)
                {
                    using (var score = new SQLiteCommand(@"INSERT INTO ScoreTypes VALUES (@id, @name, @probability)", _connection))
                    {
                        score.Parameters.AddWithValue(@"@id", i);
                        score.Parameters.AddWithValue(@"@name", BlibSchema.ScoreTypes[i].ScoreType);
                        score.Parameters.AddWithValue(@"@probability", BlibSchema.ScoreTypes[i].ProbabilityType);
                        score.ExecuteNonQuery();
                    }
                }
                for (int i = 0; i < BlibSchema.IonMobilityTypes.Length; i++)
                {
                    using (var mobility = new SQLiteCommand(@"INSERT INTO IonMobilityTypes VALUES (@id, @name)", _connection))
                    {
                        mobility.Parameters.AddWithValue(@"@id", i);
                        mobility.Parameters.AddWithValue(@"@name", BlibSchema.IonMobilityTypes[i]);
                        mobility.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        /// <summary>
        /// Adds the <c>RetentionTimes</c> table (<see cref="BlibSchema.CreateRetentionTimes"/>)
        /// when the library has none: the per-observation table of a non-redundant library, which
        /// BlibFilter and a DIA-NN library build add to a library <see cref="Open"/> began.
        /// </summary>
        public void EnsureRetentionTimes()
        {
            if (_insertRetentionTime != null)
                return;
            if (!TableExists(@"RetentionTimes"))
                Execute(BlibSchema.CreateRetentionTimes);
            _insertRetentionTime = Prepare(@"INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, SpectrumSourceID, ionMobility, " +
                                           @"collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, " +
                                           @"endTime, score, bestSpectrum) VALUES (@id, @redundant, @file, @im, @ccs, @imOffset, @imType, @rt, " +
                                           @"@start, @end, @score, @best)",
                new[] { @"@id", @"@redundant", @"@file", @"@im", @"@ccs", @"@imOffset", @"@imType", @"@rt", @"@start", @"@end", @"@score", @"@best" },
                new[]
                {
                    DbType.Int64, DbType.Int64, DbType.Int64, DbType.Double, DbType.Double, DbType.Double, DbType.Int32, DbType.Double,
                    DbType.Double, DbType.Double, DbType.Double, DbType.Int32,
                });
        }

        /// <summary>
        /// The revision <see cref="Complete"/> stamps in <c>LibInfo.majorVersion</c>:
        /// <see cref="MAJOR_VERSION"/> for a new library unless the tool says otherwise, as
        /// BiblioSpec does for a filtered library (the redundant library's) and an appended one
        /// (its own plus one).
        /// </summary>
        public void SetRevision(int majorVersion)
        {
            _revision = majorVersion;
        }

        /// <summary>
        /// Drops every index <see cref="Complete"/> creates, where it exists, before a bulk
        /// insert into an appended library; <see cref="Complete"/> creates them again.
        /// </summary>
        public void DropIndexes()
        {
            foreach (string name in BlibSchema.AllIndexNames)
                Execute(@"DROP INDEX IF EXISTS " + name);
            foreach (var index in EXTRA_INDEXES)
                Execute(@"DROP INDEX IF EXISTS " + index.Name);
            Execute(@"DROP INDEX IF EXISTS " + BlibSchema.IdxDecoyPairsPairIdName);
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

        /// <summary>Rolls back the batch <see cref="BeginBatch"/> began, so none of its rows reach the library; without one, does nothing.</summary>
        public void RollbackBatch()
        {
            if (_batch == null)
                return;
            _batch.Rollback();
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
        /// modifications and its protein mappings. Returns the <c>RefSpectra</c> id. A spectrum
        /// with a null <see cref="BlibSpectrumRecord.MzBlob"/> gets no <c>RefSpectraPeaks</c> row,
        /// as BiblioSpec writes a precursor-only entry; one with empty blobs gets a row of them.
        /// </summary>
        public long AddSpectrum(BlibSpectrumRecord spectrum)
        {
            if (spectrum == null)
                throw new ArgumentNullException(nameof(spectrum));
            EnsurePrepared();
            _insertSpectrum.Execute(spectrum.PeptideSeq, spectrum.PrecursorMz, spectrum.PrecursorCharge, spectrum.PeptideModSeq,
                spectrum.PrevAA, spectrum.NextAA, spectrum.Copies, spectrum.NumPeaks,
                Nullable(spectrum.IonMobility), Nullable(spectrum.CollisionalCrossSectionSqA), Nullable(spectrum.IonMobilityHighEnergyOffset),
                spectrum.IonMobilityType, Nullable(spectrum.RetentionTime), Nullable(spectrum.StartTime), Nullable(spectrum.EndTime),
                Nullable(spectrum.TotalIonCurrent), spectrum.MoleculeName, spectrum.ChemicalFormula, spectrum.PrecursorAdduct,
                spectrum.InchiKey, spectrum.OtherKeys, spectrum.FileId, (object)spectrum.SpecIdInFile ?? DBNull.Value,
                spectrum.Score, spectrum.ScoreType);
            long refId = _connection.LastInsertRowId;
            _spectrumCount++;
            if (spectrum.MzBlob != null)
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
        /// <paramref name="redundantRefId"/> is the observation's own spectrum in the redundant
        /// library this one was filtered from, when it was (BlibFilter), and 0 otherwise.
        /// </summary>
        public void AddRetentionTime(long refId, long sourceFileId, double? retentionTime, double? startTime, double? endTime,
            double score, bool bestSpectrum, double? ionMobility = null, double? collisionalCrossSectionSqA = null,
            double? ionMobilityHighEnergyOffset = null, int ionMobilityType = ION_MOBILITY_TYPE_NONE, long redundantRefId = 0)
        {
            EnsureRetentionTimes();
            _insertRetentionTime.Execute(refId, redundantRefId, sourceFileId, Nullable(ionMobility), Nullable(collisionalCrossSectionSqA),
                Nullable(ionMobilityHighEnergyOffset), ionMobilityType, Nullable(retentionTime), Nullable(startTime), Nullable(endTime),
                score, bestSpectrum ? 1 : 0);
        }

        /// <summary>
        /// Adds or replaces a <c>LibMetadata</c> row (<see cref="BlibMetadata"/>): a value about
        /// the library as a whole, under a key the writing tool namespaces
        /// (<c>osprey.command</c>). A null <paramref name="value"/> is stored as NULL. The table
        /// is created on the first call in a library <see cref="Open"/> began.
        /// </summary>
        public void AddMetadata(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException(@"A metadata key is required", nameof(key));
            EnsureMetadata();
            _insertMetadata.Execute(key, (object)value ?? DBNull.Value);
        }

        /// <summary>
        /// Adds a <c>DecoyPairs</c> row (<see cref="BlibSchema.CreateDecoyPairs"/>): spectrum
        /// <paramref name="refId"/> is a decoy or a target, of the entrapment partition or not,
        /// and shares <paramref name="pairId"/> with the one other row of its pair (its target or
        /// its decoy, at the same charge). <paramref name="method"/> names how a decoy was made
        /// (<c>reverse</c>, <c>cycle</c>) and is null on a target or when unknown. The table is
        /// created on the first call, so a library that supplies no decoys has none: a reader
        /// takes the table's presence to mean the library pairs its own decoys.
        /// </summary>
        public void AddDecoyPair(long refId, bool isDecoy, bool isEntrapment, int pairId, string method)
        {
            if (_insertDecoyPair == null)
            {
                if (!TableExists(@"DecoyPairs"))
                    Execute(BlibSchema.CreateDecoyPairs);
                _insertDecoyPair = Prepare(@"INSERT INTO DecoyPairs (RefSpectraID, IsDecoy, IsEntrapment, PairID, Method) " +
                                           @"VALUES (@id, @decoy, @entrapment, @pair, @method)",
                    new[] { @"@id", @"@decoy", @"@entrapment", @"@pair", @"@method" },
                    new[] { DbType.Int64, DbType.Int32, DbType.Int32, DbType.Int32, DbType.String });
            }
            _insertDecoyPair.Execute(refId, isDecoy ? 1 : 0, isEntrapment ? 1 : 0, pairId, (object)method ?? DBNull.Value);
        }

        /// <summary>
        /// Records the spectrum count and the revision in <c>LibInfo</c>, adds BiblioSpec's
        /// indexes and the writing tool's, closes the file and moves it over the final name,
        /// replacing any library there.
        /// </summary>
        public void Complete()
        {
            CommitBatch();
            // Counted from the table rather than from AddSpectrum: BiblioSpec transfers spectra
            // between an attached library and this one by SQL on the connection.
            using (var update = new SQLiteCommand(@"UPDATE LibInfo SET numSpecs = (SELECT COUNT(*) FROM RefSpectra), majorVersion = @revision", _connection))
            {
                update.Parameters.AddWithValue(@"@revision", _revision);
                update.ExecuteNonQuery();
            }
            foreach (string statement in BlibSchema.AllIndexStatements)
                Execute(statement);
            foreach (var index in EXTRA_INDEXES)
            {
                if (TableExists(index.Table))
                    Execute(index.Statement);
            }
            if (TableExists(@"DecoyPairs"))
                Execute(BlibSchema.CreateIdxDecoyPairsPairId);
            _extensions?.CreateIndexes(_connection);
            CloseConnection();
            _file?.Commit();
        }

        /// <summary>
        /// Moves the completed library to <paramref name="path"/>, which nothing may hold, when
        /// <see cref="Complete"/> could not move it over the final name. Nothing for a library
        /// appended in place, which is already where it belongs.
        /// </summary>
        public void CommitAs(string path)
        {
            _file?.CommitAs(path);
        }

        /// <summary>Closes the file; without <see cref="Complete"/>, deletes a new library's temp and leaves the final name as it was.</summary>
        public void Dispose()
        {
            CloseConnection();
            _file?.Dispose();
        }

        /// <summary>The <c>RefSpectraPeaks.peakMZ</c> blob of these m/z values: little-endian float64, compressed when that is smaller.</summary>
        public static byte[] CompressMzs(double[] mzs)
        {
            return EncodeMzs(mzs, true);
        }

        /// <summary>The <c>RefSpectraPeaks.peakIntensity</c> blob of these intensities: little-endian float32, compressed when that is smaller.</summary>
        public static byte[] CompressIntensities(float[] intensities)
        {
            return EncodeIntensities(intensities, true);
        }

        /// <summary>
        /// The <c>peakMZ</c> blob of these m/z values: little-endian float64, compressed when
        /// <paramref name="compress"/> and that is smaller, else as they are (BlibBuild's
        /// compression level 0).
        /// </summary>
        public static byte[] EncodeMzs(ReadOnlySpan<double> mzs, bool compress)
        {
            var bytes = MemoryMarshal.AsBytes(mzs).ToArray();
            return compress ? CompressIfSmaller(bytes) : bytes;
        }

        /// <summary>The <c>peakIntensity</c> blob of these intensities: little-endian float32, compressed when <paramref name="compress"/> and that is smaller.</summary>
        public static byte[] EncodeIntensities(ReadOnlySpan<float> intensities, bool compress)
        {
            var bytes = MemoryMarshal.AsBytes(intensities).ToArray();
            return compress ? CompressIfSmaller(bytes) : bytes;
        }

        /// <summary>
        /// Reverses the blob encoding: raw when the length matches <paramref name="expectedLength"/>,
        /// else zlib, which must inflate to that length or the blob does not hold the peaks the
        /// spectrum's <c>numPeaks</c> says it does.
        /// </summary>
        public static byte[] DecodeBlob(byte[] blob, int expectedLength)
        {
            if (blob.Length == expectedLength)
                return blob;
            byte[] bytes;
            using (var input = new MemoryStream(blob))
            using (var zlib = new Ionic.Zlib.ZlibStream(input, Ionic.Zlib.CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedLength))
            {
                zlib.CopyTo(output);
                bytes = output.ToArray();
            }
            if (bytes.Length != expectedLength)
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture,
                    @"A peak blob of {0} bytes inflated to {1} bytes where {2} were expected", blob.Length, bytes.Length, expectedLength));
            }
            return bytes;
        }

        /// <summary>The m/z values of a <c>peakMZ</c> blob holding <paramref name="count"/> peaks.</summary>
        public static double[] DecodeMzs(byte[] blob, int count)
        {
            var bytes = DecodeBlob(blob, count * sizeof(double));
            var values = new double[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        /// <summary>
        /// The intensities of a <c>peakIntensity</c> blob holding <paramref name="count"/> peaks.
        /// A blob of eight bytes per peak holds float64 intensities, which a writer before this
        /// one stored uncompressed and the Rust osprey reader accepted; the format's float32 is
        /// read raw or inflated as the m/z values are.
        /// </summary>
        public static float[] DecodeIntensities(byte[] blob, int count)
        {
            var values = new float[count];
            if (count > 0 && blob.Length == count * sizeof(double))
            {
                for (int i = 0; i < count; i++)
                    values[i] = (float)BitConverter.ToDouble(blob, i * sizeof(double));
                return values;
            }
            var bytes = DecodeBlob(blob, count * sizeof(float));
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private void EnsurePrepared()
        {
            if (_insertSpectrum != null)
                return;
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
        }

        private void EnsureMetadata()
        {
            if (_insertMetadata != null)
                return;
            if (!TableExists(BlibMetadata.TABLE))
                Execute(BlibSchema.CreateLibMetadata);
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

        /// <summary>The proteins an appended library already has, so a spectrum of one maps to the existing row.</summary>
        private void LoadProteinIds()
        {
            if (!TableExists(@"Proteins"))
                return;
            using (var command = new SQLiteCommand(@"SELECT id, accession FROM Proteins", _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                        _proteinIds[reader.GetString(1)] = reader.GetInt64(0);
                }
            }
        }

        private bool TableExists(string table)
        {
            return BlibSqlite.TableExists(_connection, table);
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

        private long Scalar(string sql)
        {
            using (var command = new SQLiteCommand(sql, _connection))
                return Convert.ToInt64(command.ExecuteScalar());
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
            _insertDecoyPair?.Dispose();
            _insertSpectrum = _insertPeaks = _insertModification = null;
            _insertProtein = _insertSpectrumProtein = _insertRetentionTime = _insertMetadata = _insertDecoyPair = null;
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
        /// writer, the C++ BlibBuild and Rust osprey's flate2 produce), kept only when it is
        /// shorter than the raw bytes - the BiblioSpec reader's test for compression. .NET's own
        /// ZLibStream is not used: its deflate bytes differ, which would split the parity both
        /// tools' regressions hold against their goldens.
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
