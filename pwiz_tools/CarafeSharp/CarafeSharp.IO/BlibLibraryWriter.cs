/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Osprey's pwiz.Osprey.IO.BlibWriter (schema, blob encoding) and Carafe
 *   (https://github.com/maccoss/carafe) main.java.ai.SkylineIO (DecoyPairs, LibInfo)
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
using System.Threading.Tasks;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Writes a predicted library as a BiblioSpec .blib (schema 1.11) that Skyline and Osprey
    /// read. Each precursor is one RefSpectra row with its peaks in library order (most intense
    /// first), one RefSpectraPeakAnnotations row per peak naming the ion (<c>b3</c>, <c>y7</c>,
    /// a <c>-loss</c> suffix when there is a neutral loss) with its charge, the modifications,
    /// its proteins, and one RetentionTimes row. Nothing marks decoys in the standard tables;
    /// they are ordinary spectra whose accessions carry the decoy prefix, and the optional
    /// DecoyPairs table (Carafe's) pairs them with their targets.
    /// <para>
    /// Encoding as BiblioSpec: peak m/z little-endian float64 and intensity float32, each
    /// zlib-compressed (Ionic.Zlib level 6, as Skyline and Osprey) only when that is smaller.
    /// Text columns Skyline reads with GetString are empty strings, never NULL, and an
    /// annotation's mzObserved is exactly the stored peak m/z, which Skyline checks to 1e-7.
    /// </para>
    /// </summary>
    public sealed class BlibLibraryWriter : IDisposable
    {
        public const string FILE_NAME = @"carafe_spectral_library.blib";

        public const int MAJOR_VERSION = 1;
        public const int MINOR_VERSION = 11;

        /// <summary>BiblioSpec's UNKNOWN score type (NOT_A_PROBABILITY_VALUE): a prediction has no score.</summary>
        public const int SCORE_TYPE_UNKNOWN = 0;

        /// <summary>SpectrumSourceFiles.workflowType: 1 is DIA, the use these libraries are built for.</summary>
        public const int WORKFLOW_TYPE_DIA = 1;

        /// <summary>
        /// The most annotation rows one INSERT takes; a spectrum with more peaks takes several. Each row binds 5 variables and the spectrum
        /// id 1 more, so 100 rows bind 501: under 999, SQLite's SQLITE_MAX_VARIABLE_NUMBER before
        /// 3.32, let alone the 32766 of the SQLite 3.46.1 in System.Data.SQLite 1.0.119.
        /// </summary>
        public const int MAX_PEAKS_PER_INSERT = 100;

        private const int SOURCE_FILE_ID = 1;

        /// <summary>BiblioSpec's ScoreTypes rows, as Osprey writes them.</summary>
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

        private static readonly string[] ION_MOBILITY_TYPES = { @"none", @"driftTime(msec)", @"inverseK0(Vsec/cm^2)", @"compensation(V)" };

        private readonly PartialFile _file;
        private readonly Dictionary<string, long> _proteinIds = new Dictionary<string, long>(StringComparer.Ordinal);
        // The INSERT of n annotation rows at index n, prepared when first needed.
        private readonly AnnotationInsert[] _insertAnnotations = new AnnotationInsert[MAX_PEAKS_PER_INSERT + 1];
        private SQLiteConnection _connection;
        private PreparedInsert _insertSpectrum;
        private PreparedInsert _insertPeaks;
        private PreparedInsert _insertModification;
        private PreparedInsert _insertProtein;
        private PreparedInsert _insertSpectrumProtein;
        private PreparedInsert _insertRetentionTime;
        private int _spectrumCount;

        /// <summary>
        /// Starts the library that <see cref="Complete"/> writes to <paramref name="path"/>, with
        /// one source file named <paramref name="sourceFileName"/> (Carafe uses the library file
        /// name without its extension). It is written to a <see cref="PartialFile"/>, so until
        /// then any library already at <paramref name="path"/> is left as it is.
        /// </summary>
        public BlibLibraryWriter(string path, string sourceFileName)
        {
            _file = new PartialFile(path);
            try
            {
                var connectionString = new SQLiteConnectionStringBuilder { DataSource = _file.PartialPath, Version = 3 };
                _connection = new SQLiteConnection(connectionString.ToString());
                _connection.Open();
                Execute(@"PRAGMA synchronous=OFF");
                Execute(@"PRAGMA journal_mode=MEMORY");
                CreateSchema(sourceFileName);
                PrepareStatements();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>RefSpectra rows written so far; the next spectrum gets this plus one as its id.</summary>
        public int SpectrumCount
        {
            get { return _spectrumCount; }
        }

        /// <summary>
        /// Writes spectra in order, in one transaction, compressing their peaks in parallel
        /// first. Returns the RefSpectra id of the first.
        /// </summary>
        /// <param name="spectra">The spectra, in id order.</param>
        /// <param name="maxCompressionThreads">
        /// The most threads the compression may use, or -1 for no limit. Each spectrum's blobs are
        /// the same whichever thread compresses them.
        /// </param>
        public int WriteBatch(IReadOnlyList<LibrarySpectrum> spectra, int maxCompressionThreads = -1)
        {
            var mzBlobs = new byte[spectra.Count][];
            var intensityBlobs = new byte[spectra.Count][];
            Parallel.For(0, spectra.Count, new ParallelOptions { MaxDegreeOfParallelism = maxCompressionThreads }, i =>
            {
                EncodePeaks(spectra[i], out mzBlobs[i], out intensityBlobs[i]);
            });
            int firstId = _spectrumCount + 1;
            using (var transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < spectra.Count; i++)
                    InsertSpectrum(++_spectrumCount, spectra[i], mzBlobs[i], intensityBlobs[i]);
                transaction.Commit();
            }
            return firstId;
        }

        /// <summary>Adds Carafe's DecoyPairs table with these rows.</summary>
        public void WriteDecoyPairs(IReadOnlyList<DecoyPairRow> rows)
        {
            Execute(@"CREATE TABLE DecoyPairs (RefSpectraID INTEGER NOT NULL PRIMARY KEY, IsDecoy INTEGER NOT NULL, " +
                    @"IsEntrapment INTEGER NOT NULL, PairID INTEGER NOT NULL, Method TEXT)");
            Execute(@"CREATE INDEX IF NOT EXISTS idx_DecoyPairs_PairID ON DecoyPairs(PairID)");
            using (var transaction = _connection.BeginTransaction())
            using (var insert = new SQLiteCommand(@"INSERT INTO DecoyPairs (RefSpectraID, IsDecoy, IsEntrapment, PairID, Method) " +
                                                  @"VALUES (@id, @decoy, @entrapment, @pair, @method)", _connection))
            {
                var id = insert.Parameters.Add(@"@id", DbType.Int64);
                var decoy = insert.Parameters.Add(@"@decoy", DbType.Int32);
                var entrapment = insert.Parameters.Add(@"@entrapment", DbType.Int32);
                var pair = insert.Parameters.Add(@"@pair", DbType.Int32);
                var method = insert.Parameters.Add(@"@method", DbType.String);
                foreach (var row in rows)
                {
                    id.Value = row.RefSpectraId;
                    decoy.Value = row.IsDecoy ? 1 : 0;
                    entrapment.Value = row.IsEntrapment ? 1 : 0;
                    pair.Value = row.PairId;
                    method.Value = (object)row.Method ?? DBNull.Value;
                    insert.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        /// <summary>
        /// Records the spectrum count, adds BiblioSpec's indexes, closes the file and moves it
        /// over the final name, replacing any library there.
        /// </summary>
        public void Complete()
        {
            using (var update = new SQLiteCommand(@"UPDATE LibInfo SET numSpecs = @count", _connection))
            {
                update.Parameters.AddWithValue(@"@count", _spectrumCount);
                update.ExecuteNonQuery();
            }
            Execute(@"CREATE INDEX idxPeptide ON RefSpectra (peptideSeq, precursorCharge)");
            Execute(@"CREATE INDEX idxPeptideMod ON RefSpectra (peptideModSeq, precursorCharge)");
            Execute(@"CREATE INDEX idxRefIdPeaks ON RefSpectraPeaks (RefSpectraID)");
            Execute(@"CREATE INDEX idxRefIdPeakAnnotations ON RefSpectraPeakAnnotations (RefSpectraID)");
            Execute(@"CREATE INDEX idxMoleculeName ON RefSpectra (moleculeName, precursorAdduct)");
            Execute(@"CREATE INDEX idxInChiKey ON RefSpectra (inchiKey, precursorAdduct)");
            Execute(@"CREATE INDEX idxRefIdProteins ON RefSpectraProteins (RefSpectraId)");
            Execute(@"CREATE INDEX idxRefIdModifications ON Modifications (RefSpectraID)");
            Execute(@"CREATE INDEX idxRefIdRetentionTimes ON RetentionTimes (RefSpectraID)");
            CloseConnection();
            _file.Commit();
        }

        /// <summary>
        /// The BiblioSpec peak blobs of a spectrum: m/z as float64 of the float32 library m/z,
        /// intensity as float32, each compressed when that is smaller.
        /// </summary>
        public static void EncodePeaks(LibrarySpectrum spectrum, out byte[] mzBlob, out byte[] intensityBlob)
        {
            int count = spectrum.Fragments.Count;
            var mz = new double[count];
            var intensity = new float[count];
            for (int i = 0; i < count; i++)
            {
                mz[i] = spectrum.Fragments[i].Mz;
                intensity[i] = spectrum.Fragments[i].RelativeIntensity;
            }
            mzBlob = CompressIfSmaller(ToBytes(mz));
            intensityBlob = CompressIfSmaller(ToBytes(intensity));
        }

        /// <summary>The name of a peak's annotation: ion type, ordinal, and <c>-loss</c> when there is one.</summary>
        public static string AnnotationName(LibraryFragment fragment)
        {
            string name = fragment.IonType + fragment.Ordinal.ToString(CultureInfo.InvariantCulture);
            return fragment.HasLoss ? name + @"-" + fragment.LossType : name;
        }

        /// <summary>Reverses the BiblioSpec blob encoding: raw when the size matches, else zlib.</summary>
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

        /// <summary>Closes the file; without <see cref="Complete"/>, deletes it and leaves the final name as it was.</summary>
        public void Dispose()
        {
            CloseConnection();
            _file.Discard();
        }

        private void CloseConnection()
        {
            _insertSpectrum?.Dispose();
            _insertPeaks?.Dispose();
            _insertModification?.Dispose();
            _insertProtein?.Dispose();
            _insertSpectrumProtein?.Dispose();
            _insertRetentionTime?.Dispose();
            _insertSpectrum = _insertPeaks = _insertModification = null;
            _insertProtein = _insertSpectrumProtein = _insertRetentionTime = null;
            for (int i = 0; i < _insertAnnotations.Length; i++)
            {
                _insertAnnotations[i]?.Dispose();
                _insertAnnotations[i] = null;
            }
            _connection?.Dispose();
            _connection = null;
        }

        private void InsertSpectrum(int id, LibrarySpectrum spectrum, byte[] mzBlob, byte[] intensityBlob)
        {
            _insertSpectrum.Execute(id, spectrum.Sequence, spectrum.PrecursorMz, spectrum.Charge,
                spectrum.SkylineModifiedSequence ?? spectrum.Sequence, spectrum.Fragments.Count, spectrum.RetentionTime);
            _insertPeaks.Execute(id, mzBlob, intensityBlob);
            InsertAnnotations(id, spectrum.Fragments);

            foreach (var modification in spectrum.SkylineModifications)
                _insertModification.Execute(id, modification.Position, modification.Mass);

            if (spectrum.ProteinId != LibrarySpectrum.NO_PROTEIN)
            {
                foreach (string accession in spectrum.ProteinId.Split(';'))
                {
                    if (accession.Length == 0)
                        continue;
                    _insertSpectrumProtein.Execute(id, GetProteinId(accession));
                }
            }

            _insertRetentionTime.Execute(id, spectrum.RetentionTime);
        }

        /// <summary>
        /// One annotation row per peak, in peak order: one INSERT for a spectrum of up to
        /// <see cref="MAX_PEAKS_PER_INSERT"/> peaks (the default top-N is 20), several above that.
        /// </summary>
        private void InsertAnnotations(int id, IReadOnlyList<LibraryFragment> fragments)
        {
            for (int start = 0; start < fragments.Count; start += MAX_PEAKS_PER_INSERT)
            {
                int rows = Math.Min(MAX_PEAKS_PER_INSERT, fragments.Count - start);
                var insert = _insertAnnotations[rows] ??= new AnnotationInsert(_connection, rows);
                insert.Execute(id, fragments, start);
            }
        }

        private long GetProteinId(string accession)
        {
            if (_proteinIds.TryGetValue(accession, out long proteinId))
                return proteinId;
            proteinId = _proteinIds.Count + 1;
            _insertProtein.Execute(proteinId, accession);
            _proteinIds.Add(accession, proteinId);
            return proteinId;
        }

        private void CreateSchema(string sourceFileName)
        {
            Execute(@"CREATE TABLE LibInfo (libLSID TEXT, createTime TEXT, numSpecs INTEGER, majorVersion INTEGER, minorVersion INTEGER)");
            Execute(@"CREATE TABLE RefSpectra (id INTEGER primary key autoincrement not null, peptideSeq VARCHAR(150), " +
                    @"precursorMZ REAL, precursorCharge INTEGER, peptideModSeq VARCHAR(200), prevAA CHAR(1), nextAA CHAR(1), " +
                    @"copies INTEGER, numPeaks INTEGER, ionMobility REAL, collisionalCrossSectionSqA REAL, " +
                    @"ionMobilityHighEnergyOffset REAL, ionMobilityType TINYINT, retentionTime REAL, startTime REAL, " +
                    @"endTime REAL, totalIonCurrent REAL, moleculeName VARCHAR(128), chemicalFormula VARCHAR(128), " +
                    @"precursorAdduct VARCHAR(128), inchiKey VARCHAR(128), otherKeys VARCHAR(128), fileID INTEGER, " +
                    @"SpecIDinFile VARCHAR(256), score REAL, scoreType TINYINT)");
            Execute(@"CREATE TABLE Modifications (id INTEGER primary key autoincrement not null, RefSpectraID INTEGER, " +
                    @"position INTEGER, mass REAL)");
            Execute(@"CREATE TABLE RefSpectraPeaks (RefSpectraID INTEGER, peakMZ BLOB, peakIntensity BLOB)");
            Execute(@"CREATE TABLE Proteins (id INTEGER primary key autoincrement not null, accession VARCHAR(200))");
            Execute(@"CREATE TABLE RefSpectraProteins (RefSpectraId INTEGER not null, ProteinId INTEGER not null)");
            Execute(@"CREATE TABLE RefSpectraPeakAnnotations (id INTEGER primary key autoincrement not null, " +
                    @"RefSpectraID INTEGER not null, peakIndex INTEGER not null, name VARCHAR(256), formula VARCHAR(256), " +
                    @"inchiKey VARCHAR(256), otherKeys VARCHAR(256), charge INTEGER, adduct VARCHAR(256), " +
                    @"comment VARCHAR(256), mzTheoretical REAL not null, mzObserved REAL not null)");
            Execute(@"CREATE TABLE SpectrumSourceFiles (id INTEGER PRIMARY KEY autoincrement not null, fileName VARCHAR(512), " +
                    @"idFileName VARCHAR(512), cutoffScore REAL, workflowType TINYINT)");
            Execute(@"CREATE TABLE ScoreTypes (id INTEGER PRIMARY KEY, scoreType VARCHAR(128), probabilityType VARCHAR(128))");
            Execute(@"CREATE TABLE IonMobilityTypes (id INTEGER PRIMARY KEY, ionMobilityType VARCHAR(128))");
            Execute(@"CREATE TABLE RetentionTimes (RefSpectraID INTEGER, RedundantRefSpectraID INTEGER, " +
                    @"SpectrumSourceID INTEGER, ionMobility REAL, collisionalCrossSectionSqA REAL, " +
                    @"ionMobilityHighEnergyOffset REAL, ionMobilityType TINYINT, retentionTime REAL, startTime REAL, " +
                    @"endTime REAL, score REAL, bestSpectrum INTEGER)");

            using (var transaction = _connection.BeginTransaction())
            {
                using (var info = new SQLiteCommand(@"INSERT INTO LibInfo VALUES (@lsid, @time, 0, @major, @minor)", _connection))
                {
                    info.Parameters.AddWithValue(@"@lsid", @"urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:" + Path.GetFileName(_file.FinalPath));
                    // ctime() format, as BiblioSpec and Carafe write it.
                    info.Parameters.AddWithValue(@"@time", DateTime.Now.ToString(@"ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture));
                    info.Parameters.AddWithValue(@"@major", MAJOR_VERSION);
                    info.Parameters.AddWithValue(@"@minor", MINOR_VERSION);
                    info.ExecuteNonQuery();
                }
                using (var source = new SQLiteCommand(@"INSERT INTO SpectrumSourceFiles (id, fileName, idFileName, cutoffScore, workflowType) " +
                                                      @"VALUES (@id, @file, NULL, NULL, @workflow)", _connection))
                {
                    source.Parameters.AddWithValue(@"@id", SOURCE_FILE_ID);
                    source.Parameters.AddWithValue(@"@file", sourceFileName);
                    source.Parameters.AddWithValue(@"@workflow", WORKFLOW_TYPE_DIA);
                    source.ExecuteNonQuery();
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
            _insertSpectrum = Prepare(@"INSERT INTO RefSpectra (id, peptideSeq, precursorMZ, precursorCharge, peptideModSeq, prevAA, nextAA, " +
                                      @"copies, numPeaks, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, " +
                                      @"retentionTime, startTime, endTime, totalIonCurrent, moleculeName, chemicalFormula, precursorAdduct, " +
                                      @"inchiKey, otherKeys, fileID, SpecIDinFile, score, scoreType) VALUES (@id, @seq, @mz, @charge, @modseq, " +
                                      @"'-', '-', 1, @numPeaks, NULL, NULL, NULL, 0, @rt, NULL, NULL, NULL, '', '', '', '', '', " +
                                      @"@file, NULL, 0, @scoreType)",
                new[] { @"@id", @"@seq", @"@mz", @"@charge", @"@modseq", @"@numPeaks", @"@rt" },
                new[] { DbType.Int64, DbType.String, DbType.Double, DbType.Int32, DbType.String, DbType.Int32, DbType.Double });
            _insertSpectrum.AddConstant(@"@file", DbType.Int32, SOURCE_FILE_ID);
            _insertSpectrum.AddConstant(@"@scoreType", DbType.Int32, SCORE_TYPE_UNKNOWN);
            _insertPeaks = Prepare(@"INSERT INTO RefSpectraPeaks (RefSpectraID, peakMZ, peakIntensity) VALUES (@id, @mz, @intensity)",
                new[] { @"@id", @"@mz", @"@intensity" }, new[] { DbType.Int64, DbType.Binary, DbType.Binary });
            _insertModification = Prepare(@"INSERT INTO Modifications (RefSpectraID, position, mass) VALUES (@id, @position, @mass)",
                new[] { @"@id", @"@position", @"@mass" }, new[] { DbType.Int64, DbType.Int32, DbType.Double });
            _insertProtein = Prepare(@"INSERT INTO Proteins (id, accession) VALUES (@id, @accession)",
                new[] { @"@id", @"@accession" }, new[] { DbType.Int64, DbType.String });
            _insertSpectrumProtein = Prepare(@"INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) VALUES (@id, @protein)",
                new[] { @"@id", @"@protein" }, new[] { DbType.Int64, DbType.Int64 });
            // No start or end time: Skyline would read them as explicit peak boundaries.
            _insertRetentionTime = Prepare(@"INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, SpectrumSourceID, ionMobility, " +
                                           @"collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, " +
                                           @"endTime, score, bestSpectrum) VALUES (@id, 0, @file, NULL, NULL, NULL, 0, @rt, NULL, NULL, 0, 1)",
                new[] { @"@id", @"@rt" }, new[] { DbType.Int64, DbType.Double });
            _insertRetentionTime.AddConstant(@"@file", DbType.Int32, SOURCE_FILE_ID);
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

        /// <summary>
        /// zlib at level 6 through Ionic.Zlib, whose output is stock zlib's, kept only when it
        /// is shorter than the raw bytes (the BiblioSpec reader's test for compression).
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

        private static byte[] ToBytes(double[] values)
        {
            var bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static byte[] ToBytes(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// A prepared single-row INSERT whose parameters are set by position, without the name
        /// lookup of <see cref="SQLiteParameterCollection"/>, plus constants set once.
        /// </summary>
        private sealed class PreparedInsert : IDisposable
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

            /// <summary>Adds a parameter with the same value in every row.</summary>
            public void AddConstant(string name, DbType type, object value)
            {
                _command.Parameters.Add(name, type).Value = value;
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

            /// <summary>Inserts the annotations of the peaks from <paramref name="start"/> of spectrum <paramref name="id"/>.</summary>
            public void Execute(int id, IReadOnlyList<LibraryFragment> fragments, int start)
            {
                _id.Value = id;
                for (int row = 0; row < _name.Length; row++)
                {
                    var fragment = fragments[start + row];
                    _peakIndex[row].Value = start + row;
                    _name[row].Value = AnnotationName(fragment);
                    _charge[row].Value = fragment.Charge;
                    _theoretical[row].Value = fragment.TheoreticalMz;
                    _observed[row].Value = (double)fragment.Mz;
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
