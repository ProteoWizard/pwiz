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

        private readonly string _path;
        private readonly Dictionary<string, long> _proteinIds = new Dictionary<string, long>(StringComparer.Ordinal);
        private SQLiteConnection _connection;
        private SQLiteCommand _insertSpectrum;
        private SQLiteCommand _insertPeaks;
        private SQLiteCommand _insertAnnotation;
        private SQLiteCommand _insertModification;
        private SQLiteCommand _insertProtein;
        private SQLiteCommand _insertSpectrumProtein;
        private SQLiteCommand _insertRetentionTime;
        private int _spectrumCount;
        private bool _completed;

        /// <summary>
        /// Creates <paramref name="path"/>, replacing any file there, with one source file
        /// named <paramref name="sourceFileName"/> (Carafe uses the library file name without
        /// its extension).
        /// </summary>
        public BlibLibraryWriter(string path, string sourceFileName)
        {
            _path = path;
            if (File.Exists(path))
                File.Delete(path);
            _connection = new SQLiteConnection(@"Data Source=" + path + @";Version=3;");
            _connection.Open();
            Execute(@"PRAGMA synchronous=OFF");
            Execute(@"PRAGMA journal_mode=MEMORY");
            CreateSchema(sourceFileName);
            PrepareStatements();
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
        public int WriteBatch(IReadOnlyList<LibrarySpectrum> spectra)
        {
            var mzBlobs = new byte[spectra.Count][];
            var intensityBlobs = new byte[spectra.Count][];
            Parallel.For(0, spectra.Count, i =>
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

        /// <summary>Records the spectrum count, adds BiblioSpec's indexes, and closes the file.</summary>
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
            _completed = true;
            Dispose();
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

        public void Dispose()
        {
            _insertSpectrum?.Dispose();
            _insertPeaks?.Dispose();
            _insertAnnotation?.Dispose();
            _insertModification?.Dispose();
            _insertProtein?.Dispose();
            _insertSpectrumProtein?.Dispose();
            _insertRetentionTime?.Dispose();
            _insertSpectrum = _insertPeaks = _insertAnnotation = _insertModification = null;
            _insertProtein = _insertSpectrumProtein = _insertRetentionTime = null;
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
                if (!_completed && File.Exists(_path))
                    File.Delete(_path);
            }
        }

        private void InsertSpectrum(int id, LibrarySpectrum spectrum, byte[] mzBlob, byte[] intensityBlob)
        {
            Set(_insertSpectrum, @"@id", id);
            Set(_insertSpectrum, @"@seq", spectrum.Sequence);
            Set(_insertSpectrum, @"@mz", spectrum.PrecursorMz);
            Set(_insertSpectrum, @"@charge", spectrum.Charge);
            Set(_insertSpectrum, @"@modseq", spectrum.SkylineModifiedSequence ?? spectrum.Sequence);
            Set(_insertSpectrum, @"@numPeaks", spectrum.Fragments.Count);
            Set(_insertSpectrum, @"@rt", spectrum.RetentionTime);
            _insertSpectrum.ExecuteNonQuery();

            Set(_insertPeaks, @"@id", id);
            Set(_insertPeaks, @"@mz", mzBlob);
            Set(_insertPeaks, @"@intensity", intensityBlob);
            _insertPeaks.ExecuteNonQuery();

            for (int i = 0; i < spectrum.Fragments.Count; i++)
            {
                var fragment = spectrum.Fragments[i];
                Set(_insertAnnotation, @"@id", id);
                Set(_insertAnnotation, @"@index", i);
                Set(_insertAnnotation, @"@name", AnnotationName(fragment));
                Set(_insertAnnotation, @"@charge", fragment.Charge);
                Set(_insertAnnotation, @"@theoretical", fragment.TheoreticalMz);
                Set(_insertAnnotation, @"@observed", (double)fragment.Mz);
                _insertAnnotation.ExecuteNonQuery();
            }

            foreach (var modification in spectrum.SkylineModifications)
            {
                Set(_insertModification, @"@id", id);
                Set(_insertModification, @"@position", modification.Position);
                Set(_insertModification, @"@mass", modification.Mass);
                _insertModification.ExecuteNonQuery();
            }

            if (spectrum.ProteinId != LibrarySpectrum.NO_PROTEIN)
            {
                foreach (string accession in spectrum.ProteinId.Split(';'))
                {
                    if (accession.Length == 0)
                        continue;
                    Set(_insertSpectrumProtein, @"@id", id);
                    Set(_insertSpectrumProtein, @"@protein", GetProteinId(accession));
                    _insertSpectrumProtein.ExecuteNonQuery();
                }
            }

            Set(_insertRetentionTime, @"@id", id);
            Set(_insertRetentionTime, @"@rt", spectrum.RetentionTime);
            _insertRetentionTime.ExecuteNonQuery();
        }

        private long GetProteinId(string accession)
        {
            if (_proteinIds.TryGetValue(accession, out long proteinId))
                return proteinId;
            proteinId = _proteinIds.Count + 1;
            Set(_insertProtein, @"@id", proteinId);
            Set(_insertProtein, @"@accession", accession);
            _insertProtein.ExecuteNonQuery();
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
                    info.Parameters.AddWithValue(@"@lsid", @"urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:" + Path.GetFileName(_path));
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
                new[] { @"@id", @"@seq", @"@mz", @"@charge", @"@modseq", @"@numPeaks", @"@rt", @"@file", @"@scoreType" },
                new[] { DbType.Int64, DbType.String, DbType.Double, DbType.Int32, DbType.String, DbType.Int32, DbType.Double, DbType.Int32, DbType.Int32 });
            Set(_insertSpectrum, @"@file", SOURCE_FILE_ID);
            Set(_insertSpectrum, @"@scoreType", SCORE_TYPE_UNKNOWN);
            _insertPeaks = Prepare(@"INSERT INTO RefSpectraPeaks (RefSpectraID, peakMZ, peakIntensity) VALUES (@id, @mz, @intensity)",
                new[] { @"@id", @"@mz", @"@intensity" }, new[] { DbType.Int64, DbType.Binary, DbType.Binary });
            _insertAnnotation = Prepare(@"INSERT INTO RefSpectraPeakAnnotations (RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, " +
                                        @"charge, adduct, comment, mzTheoretical, mzObserved) VALUES (@id, @index, @name, '', '', '', @charge, '', '', " +
                                        @"@theoretical, @observed)",
                new[] { @"@id", @"@index", @"@name", @"@charge", @"@theoretical", @"@observed" },
                new[] { DbType.Int64, DbType.Int32, DbType.String, DbType.Int32, DbType.Double, DbType.Double });
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
                new[] { @"@id", @"@rt", @"@file" }, new[] { DbType.Int64, DbType.Double, DbType.Int32 });
            Set(_insertRetentionTime, @"@file", SOURCE_FILE_ID);
        }

        private SQLiteCommand Prepare(string sql, string[] names, DbType[] types)
        {
            var command = new SQLiteCommand(sql, _connection);
            for (int i = 0; i < names.Length; i++)
                command.Parameters.Add(names[i], types[i]);
            command.Prepare();
            return command;
        }

        private static void Set(SQLiteCommand command, string name, object value)
        {
            command.Parameters[name].Value = value;
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
    }
}
