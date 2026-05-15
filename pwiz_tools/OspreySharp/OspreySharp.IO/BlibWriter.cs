/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Writes Osprey detection results to BiblioSpec (blib) SQLite format for Skyline integration.
    /// Ported from osprey-io/src/output/blib.rs.
    /// </summary>
    public class BlibWriter : IDisposable
    {
        private const int BLIB_MAJOR_VERSION = 1;
        private const int BLIB_MINOR_VERSION = 11;
        private const int SCORE_TYPE_GENERIC_QVALUE = 19;

        /// <summary>
        /// Known UniMod accession IDs mapped to their monoisotopic mass deltas.
        /// </summary>
        private static readonly Dictionary<int, double> UNIMOD_MASSES = new Dictionary<int, double>
        {
            { 1, 42.010565 },    // Acetyl
            { 4, 57.021464 },    // Carbamidomethyl
            { 5, 43.005814 },    // Carbamyl
            { 7, 0.984016 },     // Deamidated
            { 21, 79.966331 },   // Phospho
            { 28, -18.010565 },  // Glu->pyro-Glu
            { 34, 14.015650 },   // Methyl
            { 35, 15.994915 },   // Oxidation
            { 36, 28.031300 },   // Dimethyl
            { 37, 42.046950 },   // Trimethyl
            { 121, 114.042927 }, // Ubiquitin (GlyGly)
            { 122, 383.228102 }, // SUMO
            { 214, 44.985078 },  // Nitro
            { 312, -17.026549 }, // Ammonia loss
            { 385, 229.162932 }, // TMT6plex
            { 737, 229.162932 }, // TMT6plex (alternate ID)
            { 747, 304.207146 }, // TMTpro
        };

        private SQLiteConnection _conn;
        private bool _inTransaction;
        private long _nextSpecId;
        private Dictionary<string, long> _proteinCache;
        private bool _disposed;

        // Prepared statements - reused across all inserts to avoid per-row SQL recompilation
        private SQLiteCommand _cmdInsertRefSpectra;
        private SQLiteCommand _cmdInsertRefSpectraPeaks;
        private SQLiteCommand _cmdInsertModification;
        private SQLiteCommand _cmdInsertProtein;
        private SQLiteCommand _cmdInsertRefSpectraProtein;
        private SQLiteCommand _cmdInsertRetentionTime;

        /// <summary>
        /// Creates a new blib file at the given path. If the file already exists, it is removed first.
        /// </summary>
        public BlibWriter(string path)
        {
            if (File.Exists(path))
                File.Delete(path);

            _conn = new SQLiteConnection("Data Source=" + path + ";Version=3;");
            _conn.Open();

            // WAL mode for better write performance
            ExecuteNonQuery("PRAGMA journal_mode=WAL");
            ExecuteNonQuery("PRAGMA synchronous=NORMAL");

            _inTransaction = false;
            _nextSpecId = 0;
            _proteinCache = new Dictionary<string, long>();

            CreateSchema();
            PrepareStatements();
        }

        /// <summary>
        /// Prepare reusable SQL commands for high-volume inserts.
        /// Each command is parsed and compiled once, then reused with new parameter
        /// values for every row. This avoids the per-row overhead of constructing,
        /// parsing, and disposing SQLiteCommand objects in tight insert loops.
        /// </summary>
        private void PrepareStatements()
        {
            _cmdInsertRefSpectra = new SQLiteCommand(_conn);
            _cmdInsertRefSpectra.CommandText = @"INSERT INTO RefSpectra (
                peptideSeq, precursorMZ, precursorCharge, peptideModSeq,
                prevAA, nextAA, copies, numPeaks, ionMobility,
                collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType,
                retentionTime, startTime, endTime, totalIonCurrent,
                moleculeName, chemicalFormula, precursorAdduct, inchiKey, otherKeys,
                fileID, SpecIDinFile, score, scoreType
            ) VALUES (
                @seq, @mz, @charge, @modseq,
                '-', '-', @copies, @numPeaks, 0.0,
                0.0, 0.0, 0,
                @rt, @startTime, @endTime, @tic,
                '', '', '', '', '',
                @fileId, @specId, @score, @scoreType
            )";
            _cmdInsertRefSpectra.Parameters.Add("@seq", System.Data.DbType.String);
            _cmdInsertRefSpectra.Parameters.Add("@mz", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@charge", System.Data.DbType.Int32);
            _cmdInsertRefSpectra.Parameters.Add("@modseq", System.Data.DbType.String);
            _cmdInsertRefSpectra.Parameters.Add("@copies", System.Data.DbType.Int32);
            _cmdInsertRefSpectra.Parameters.Add("@numPeaks", System.Data.DbType.Int32);
            _cmdInsertRefSpectra.Parameters.Add("@rt", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@startTime", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@endTime", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@tic", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@fileId", System.Data.DbType.Int64);
            _cmdInsertRefSpectra.Parameters.Add("@specId", System.Data.DbType.String);
            _cmdInsertRefSpectra.Parameters.Add("@score", System.Data.DbType.Double);
            _cmdInsertRefSpectra.Parameters.Add("@scoreType", System.Data.DbType.Int32);
            _cmdInsertRefSpectra.Prepare();

            _cmdInsertRefSpectraPeaks = new SQLiteCommand(_conn);
            _cmdInsertRefSpectraPeaks.CommandText =
                "INSERT INTO RefSpectraPeaks (RefSpectraID, peakMZ, peakIntensity) VALUES (@id, @mz, @int)";
            _cmdInsertRefSpectraPeaks.Parameters.Add("@id", System.Data.DbType.Int64);
            _cmdInsertRefSpectraPeaks.Parameters.Add("@mz", System.Data.DbType.Binary);
            _cmdInsertRefSpectraPeaks.Parameters.Add("@int", System.Data.DbType.Binary);
            _cmdInsertRefSpectraPeaks.Prepare();

            _cmdInsertModification = new SQLiteCommand(_conn);
            _cmdInsertModification.CommandText =
                "INSERT INTO Modifications (RefSpectraID, position, mass) VALUES (@id, @pos, @mass)";
            _cmdInsertModification.Parameters.Add("@id", System.Data.DbType.Int64);
            _cmdInsertModification.Parameters.Add("@pos", System.Data.DbType.Int32);
            _cmdInsertModification.Parameters.Add("@mass", System.Data.DbType.Double);
            _cmdInsertModification.Prepare();

            _cmdInsertProtein = new SQLiteCommand(_conn);
            _cmdInsertProtein.CommandText = "INSERT INTO Proteins (accession) VALUES (@acc)";
            _cmdInsertProtein.Parameters.Add("@acc", System.Data.DbType.String);
            _cmdInsertProtein.Prepare();

            _cmdInsertRefSpectraProtein = new SQLiteCommand(_conn);
            _cmdInsertRefSpectraProtein.CommandText =
                "INSERT INTO RefSpectraProteins (RefSpectraID, ProteinID) VALUES (@refId, @protId)";
            _cmdInsertRefSpectraProtein.Parameters.Add("@refId", System.Data.DbType.Int64);
            _cmdInsertRefSpectraProtein.Parameters.Add("@protId", System.Data.DbType.Int64);
            _cmdInsertRefSpectraProtein.Prepare();

            _cmdInsertRetentionTime = new SQLiteCommand(_conn);
            _cmdInsertRetentionTime.CommandText = @"INSERT INTO RetentionTimes (
                RefSpectraID, RedundantRefSpectraID, SpectrumSourceID,
                ionMobility, collisionalCrossSectionSqA,
                ionMobilityHighEnergyOffset, ionMobilityType,
                retentionTime, startTime, endTime, score, bestSpectrum
            ) VALUES (@refId, 0, @srcId, 0.0, 0.0, 0.0, 0, @rt, @start, @end, @score, @best)";
            _cmdInsertRetentionTime.Parameters.Add("@refId", System.Data.DbType.Int64);
            _cmdInsertRetentionTime.Parameters.Add("@srcId", System.Data.DbType.Int64);
            _cmdInsertRetentionTime.Parameters.Add("@rt", System.Data.DbType.Double);
            _cmdInsertRetentionTime.Parameters.Add("@start", System.Data.DbType.Double);
            _cmdInsertRetentionTime.Parameters.Add("@end", System.Data.DbType.Double);
            _cmdInsertRetentionTime.Parameters.Add("@score", System.Data.DbType.Double);
            _cmdInsertRetentionTime.Parameters.Add("@best", System.Data.DbType.Int32);
            _cmdInsertRetentionTime.Prepare();
        }

        /// <summary>
        /// Begin a batch transaction for faster writes.
        /// </summary>
        public void BeginBatch()
        {
            if (_inTransaction)
                return;
            ExecuteNonQuery("BEGIN TRANSACTION");
            _inTransaction = true;
        }

        /// <summary>
        /// Commit the batch transaction.
        /// </summary>
        public void Commit()
        {
            if (!_inTransaction)
                return;
            ExecuteNonQuery("COMMIT");
            _inTransaction = false;
        }

        /// <summary>
        /// Add a source file and return its ID.
        /// </summary>
        public long AddSourceFile(string fileName, string idFileName, double cutoffScore)
        {
            using (var cmd = new SQLiteCommand(_conn))
            {
                cmd.CommandText = "INSERT INTO SpectrumSourceFiles (fileName, idFileName, cutoffScore, workflowType) VALUES (@fn, @idfn, @cs, 1)";
                cmd.Parameters.AddWithValue("@fn", fileName);
                cmd.Parameters.AddWithValue("@idfn", idFileName);
                cmd.Parameters.AddWithValue("@cs", cutoffScore);
                cmd.ExecuteNonQuery();
            }
            return _conn.LastInsertRowId;
        }

        /// <summary>
        /// Add a detected peptide spectrum. Returns the RefSpectra row ID.
        /// Score should be a raw q-value (lower is better).
        /// </summary>
        public long AddSpectrum(string peptideSeq, string peptideModSeq,
            double precursorMz, int precursorCharge,
            double retentionTime, double startTime, double endTime,
            double[] mzs, float[] intensities,
            double score, long fileId, int copies, double totalIonCurrent)
        {
            string cleanSeq = StripFlankingChars(peptideSeq);
            string cleanModSeq = ConvertUnimodToMass(StripFlankingChars(peptideModSeq));

            byte[] mzBlob = CompressBytes(DoublesToBytes(mzs));
            byte[] intBlob = CompressBytes(FloatsToBytes(intensities));

            string specIdInFile = _nextSpecId.ToString();
            _nextSpecId++;

            _cmdInsertRefSpectra.Parameters["@seq"].Value = cleanSeq;
            _cmdInsertRefSpectra.Parameters["@mz"].Value = precursorMz;
            _cmdInsertRefSpectra.Parameters["@charge"].Value = precursorCharge;
            _cmdInsertRefSpectra.Parameters["@modseq"].Value = cleanModSeq;
            _cmdInsertRefSpectra.Parameters["@copies"].Value = copies;
            _cmdInsertRefSpectra.Parameters["@numPeaks"].Value = mzs.Length;
            _cmdInsertRefSpectra.Parameters["@rt"].Value = retentionTime;
            _cmdInsertRefSpectra.Parameters["@startTime"].Value = startTime;
            _cmdInsertRefSpectra.Parameters["@endTime"].Value = endTime;
            _cmdInsertRefSpectra.Parameters["@tic"].Value = totalIonCurrent;
            _cmdInsertRefSpectra.Parameters["@fileId"].Value = fileId;
            _cmdInsertRefSpectra.Parameters["@specId"].Value = specIdInFile;
            _cmdInsertRefSpectra.Parameters["@score"].Value = score;
            _cmdInsertRefSpectra.Parameters["@scoreType"].Value = SCORE_TYPE_GENERIC_QVALUE;
            _cmdInsertRefSpectra.ExecuteNonQuery();

            long refId = _conn.LastInsertRowId;

            _cmdInsertRefSpectraPeaks.Parameters["@id"].Value = refId;
            _cmdInsertRefSpectraPeaks.Parameters["@mz"].Value = mzBlob;
            _cmdInsertRefSpectraPeaks.Parameters["@int"].Value = intBlob;
            _cmdInsertRefSpectraPeaks.ExecuteNonQuery();

            return refId;
        }

        /// <summary>
        /// Add a spectrum from a LibraryEntry. Convenience overload.
        /// </summary>
        public long AddSpectrum(LibraryEntry entry, string fileName, double bestRt)
        {
            double[] mzs;
            float[] intensities;
            ExtractFragmentArrays(entry, out mzs, out intensities);

            long fileId = AddSourceFile(fileName, fileName, 0.01);
            long refId = AddSpectrum(
                entry.Sequence, entry.ModifiedSequence,
                entry.PrecursorMz, entry.Charge,
                bestRt, bestRt - 1.0, bestRt + 1.0,
                mzs, intensities,
                0.01, fileId, 1, 0.0);

            if (entry.Modifications != null && entry.Modifications.Count > 0)
                AddModifications(refId, entry.Modifications);

            if (entry.ProteinIds != null && entry.ProteinIds.Count > 0)
                AddProteinMapping(refId, entry.ProteinIds);

            return refId;
        }

        /// <summary>
        /// Write modifications to the Modifications table.
        /// Positions are converted from 0-based (internal) to 1-based (blib/Skyline).
        /// </summary>
        public void AddModifications(long refId, List<Modification> modifications)
        {
            foreach (var mod in modifications)
            {
                int position1Based = mod.Position + 1;
                _cmdInsertModification.Parameters["@id"].Value = refId;
                _cmdInsertModification.Parameters["@pos"].Value = position1Based;
                _cmdInsertModification.Parameters["@mass"].Value = mod.MassDelta;
                _cmdInsertModification.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Add protein accession mappings for a spectrum.
        /// </summary>
        public void AddProteinMapping(long refId, List<string> proteinIds)
        {
            foreach (string accession in proteinIds)
            {
                if (string.IsNullOrEmpty(accession))
                    continue;

                long proteinId;
                if (!_proteinCache.TryGetValue(accession, out proteinId))
                {
                    _cmdInsertProtein.Parameters["@acc"].Value = accession;
                    _cmdInsertProtein.ExecuteNonQuery();
                    proteinId = _conn.LastInsertRowId;
                    _proteinCache[accession] = proteinId;
                }

                _cmdInsertRefSpectraProtein.Parameters["@refId"].Value = refId;
                _cmdInsertRefSpectraProtein.Parameters["@protId"].Value = proteinId;
                _cmdInsertRefSpectraProtein.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Add a retention time entry for per-run peak boundaries.
        /// Pass null for retentionTime when the precursor did not pass run-level FDR.
        /// </summary>
        public void AddRetentionTime(long refId, long sourceFileId,
            double? retentionTime, double startTime, double endTime,
            double score, bool bestSpectrum)
        {
            _cmdInsertRetentionTime.Parameters["@refId"].Value = refId;
            _cmdInsertRetentionTime.Parameters["@srcId"].Value = sourceFileId;
            _cmdInsertRetentionTime.Parameters["@rt"].Value =
                retentionTime.HasValue ? retentionTime.Value : DBNull.Value;
            _cmdInsertRetentionTime.Parameters["@start"].Value = startTime;
            _cmdInsertRetentionTime.Parameters["@end"].Value = endTime;
            _cmdInsertRetentionTime.Parameters["@score"].Value = score;
            _cmdInsertRetentionTime.Parameters["@best"].Value = bestSpectrum ? 1 : 0;
            _cmdInsertRetentionTime.ExecuteNonQuery();
        }

        /// <summary>
        /// Set retention times for multiple runs at once.
        /// Dictionary maps refId to an array of [retentionTime, startTime, endTime] per run.
        /// </summary>
        public void SetRetentionTimes(string fileName, Dictionary<int, double[]> rtsByRefId)
        {
            long sourceFileId = AddSourceFile(fileName, fileName, 0.01);
            foreach (var kvp in rtsByRefId)
            {
                double[] rts = kvp.Value;
                AddRetentionTime(kvp.Key, sourceFileId,
                    rts[0], rts[1], rts[2], 0.01, false);
            }
        }

        /// <summary>
        /// Add metadata key-value pair.
        /// </summary>
        public void AddMetadata(string key, string value)
        {
            using (var cmd = new SQLiteCommand(_conn))
            {
                cmd.CommandText = "INSERT OR REPLACE INTO OspreyMetadata (Key, Value) VALUES (@k, @v)";
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Update spectrum count in LibInfo, create indices, and checkpoint WAL.
        /// </summary>
        public void FinalizeDatabase()
        {
            ExecuteNonQuery("UPDATE LibInfo SET numSpecs = (SELECT COUNT(*) FROM RefSpectra)");

            ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS idx_refspectra_peptide ON RefSpectra(peptideSeq);
                CREATE INDEX IF NOT EXISTS idx_refspectra_modseq ON RefSpectra(peptideModSeq);
                CREATE INDEX IF NOT EXISTS idx_refspectra_mz ON RefSpectra(precursorMZ);
                CREATE INDEX IF NOT EXISTS idx_peaks_refid ON RefSpectraPeaks(RefSpectraID);
                CREATE INDEX IF NOT EXISTS idx_mods_refid ON Modifications(RefSpectraID);
                CREATE INDEX IF NOT EXISTS idx_rettimes_refid ON RetentionTimes(RefSpectraID)");

            ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
            ExecuteNonQuery("PRAGMA journal_mode=DELETE");
        }

        /// <summary>
        /// Close the database connection.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_inTransaction)
                {
                    try { ExecuteNonQuery("ROLLBACK"); }
                    catch { /* best effort */ }
                    _inTransaction = false;
                }
                if (_cmdInsertRefSpectra != null) { _cmdInsertRefSpectra.Dispose(); _cmdInsertRefSpectra = null; }
                if (_cmdInsertRefSpectraPeaks != null) { _cmdInsertRefSpectraPeaks.Dispose(); _cmdInsertRefSpectraPeaks = null; }
                if (_cmdInsertModification != null) { _cmdInsertModification.Dispose(); _cmdInsertModification = null; }
                if (_cmdInsertProtein != null) { _cmdInsertProtein.Dispose(); _cmdInsertProtein = null; }
                if (_cmdInsertRefSpectraProtein != null) { _cmdInsertRefSpectraProtein.Dispose(); _cmdInsertRefSpectraProtein = null; }
                if (_cmdInsertRetentionTime != null) { _cmdInsertRetentionTime.Dispose(); _cmdInsertRetentionTime = null; }
                if (_conn != null)
                {
                    _conn.Close();
                    _conn.Dispose();
                    _conn = null;
                }
                _disposed = true;
            }
        }

        #region Internal access for tests

        /// <summary>
        /// Provides direct SQL access for test verification. Do not use in production code.
        /// </summary>
        internal SQLiteConnection Connection { get { return _conn; } }

        #endregion

        #region Static helpers

        /// <summary>
        /// Strip flanking amino acid characters from peptide sequences.
        /// Handles formats like "K.PEPTIDE.R", "_PEPTIDE_", "_.PEPTIDE._", "-PEPTIDE-".
        /// Dots inside modification brackets (e.g., [+57.021]) are preserved.
        /// </summary>
        public static string StripFlankingChars(string seq)
        {
            string trimmed = seq.Trim('_', '.', '-');

            int firstDot = FindDotOutsideBrackets(trimmed, false);
            int lastDot = FindDotOutsideBrackets(trimmed, true);
            if (firstDot >= 0 && lastDot >= 0 && firstDot < lastDot)
            {
                return trimmed.Substring(firstDot + 1, lastDot - firstDot - 1);
            }

            return trimmed;
        }

        /// <summary>
        /// Convert UniMod notation (e.g., [UniMod:4]) to mass notation (e.g., [+57.0215])
        /// in modified peptide sequences.
        /// </summary>
        public static string ConvertUnimodToMass(string seq)
        {
            var result = new System.Text.StringBuilder(seq.Length);
            int i = 0;
            while (i < seq.Length)
            {
                if (seq[i] == '[')
                {
                    int close = seq.IndexOf(']', i);
                    if (close < 0)
                    {
                        result.Append(seq[i]);
                        i++;
                        continue;
                    }

                    string content = seq.Substring(i + 1, close - i - 1);
                    string idStr = null;

                    if (content.StartsWith("UniMod:"))
                        idStr = content.Substring(7);
                    else if (content.StartsWith("UNIMOD:"))
                        idStr = content.Substring(7);

                    if (idStr != null)
                    {
                        int unimodId;
                        if (int.TryParse(idStr, out unimodId))
                        {
                            double mass;
                            if (TryGetUnimodMass(unimodId, out mass))
                            {
                                result.Append('[');
                                if (mass >= 0.0)
                                    result.AppendFormat("+{0:F4}", mass);
                                else
                                    result.AppendFormat("{0:F4}", mass);
                                result.Append(']');
                                i = close + 1;
                                continue;
                            }
                        }
                    }

                    // Not a recognized UniMod, keep as-is
                    result.Append(seq.Substring(i, close - i + 1));
                    i = close + 1;
                }
                else
                {
                    result.Append(seq[i]);
                    i++;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Look up monoisotopic mass for a UniMod accession ID.
        /// Returns true if found.
        /// </summary>
        public static bool TryGetUnimodMass(int unimodId, out double mass)
        {
            return UNIMOD_MASSES.TryGetValue(unimodId, out mass);
        }

        #endregion

        #region Private helpers

        private void CreateSchema()
        {
            ExecuteNonQuery(@"
                CREATE TABLE LibInfo (
                    libLSID TEXT PRIMARY KEY,
                    createTime TEXT,
                    numSpecs INTEGER,
                    majorVersion INTEGER,
                    minorVersion INTEGER
                );

                CREATE TABLE ScoreTypes (
                    id INTEGER PRIMARY KEY,
                    scoreType VARCHAR(128),
                    probabilityType VARCHAR(128)
                );

                CREATE TABLE IonMobilityTypes (
                    id INTEGER PRIMARY KEY,
                    ionMobilityType TEXT
                );

                CREATE TABLE SpectrumSourceFiles (
                    id INTEGER PRIMARY KEY autoincrement not null,
                    fileName VARCHAR(512),
                    idFileName VARCHAR(512),
                    cutoffScore REAL,
                    workflowType TINYINT
                );

                CREATE TABLE RefSpectra (
                    id INTEGER PRIMARY KEY autoincrement not null,
                    peptideSeq VARCHAR(150),
                    precursorMZ REAL,
                    precursorCharge INTEGER,
                    peptideModSeq VARCHAR(200),
                    prevAA CHAR(1),
                    nextAA CHAR(1),
                    copies INTEGER,
                    numPeaks INTEGER,
                    ionMobility REAL,
                    collisionalCrossSectionSqA REAL,
                    ionMobilityHighEnergyOffset REAL,
                    ionMobilityType TINYINT,
                    retentionTime REAL,
                    startTime REAL,
                    endTime REAL,
                    totalIonCurrent REAL,
                    moleculeName VARCHAR(128),
                    chemicalFormula VARCHAR(128),
                    precursorAdduct VARCHAR(128),
                    inchiKey VARCHAR(128),
                    otherKeys VARCHAR(128),
                    fileID INTEGER,
                    SpecIDinFile VARCHAR(256),
                    score REAL,
                    scoreType TINYINT
                );

                CREATE TABLE RefSpectraPeaks (
                    RefSpectraID INTEGER,
                    peakMZ BLOB,
                    peakIntensity BLOB,
                    FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id)
                );

                CREATE TABLE RefSpectraPeakAnnotations (
                    id INTEGER PRIMARY KEY,
                    RefSpectraID INTEGER,
                    peakIndex INTEGER,
                    name TEXT,
                    formula TEXT,
                    inchiKey TEXT,
                    otherKeys TEXT,
                    charge INTEGER,
                    adduct TEXT,
                    comment TEXT,
                    mzTheoretical REAL,
                    mzObserved REAL,
                    FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id)
                );

                CREATE TABLE Modifications (
                    id INTEGER PRIMARY KEY autoincrement not null,
                    RefSpectraID INTEGER,
                    position INTEGER,
                    mass REAL,
                    FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id)
                );

                CREATE TABLE Proteins (
                    id INTEGER PRIMARY KEY,
                    accession TEXT
                );

                CREATE TABLE RefSpectraProteins (
                    RefSpectraID INTEGER,
                    ProteinID INTEGER,
                    FOREIGN KEY (RefSpectraID) REFERENCES RefSpectra(id),
                    FOREIGN KEY (ProteinID) REFERENCES Proteins(id)
                );

                CREATE TABLE RetentionTimes (
                    RefSpectraID INTEGER,
                    RedundantRefSpectraID INTEGER,
                    SpectrumSourceID INTEGER,
                    ionMobility REAL,
                    collisionalCrossSectionSqA REAL,
                    ionMobilityHighEnergyOffset REAL,
                    ionMobilityType TINYINT,
                    retentionTime REAL,
                    startTime REAL,
                    endTime REAL,
                    score REAL,
                    bestSpectrum INTEGER,
                    FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id)
                );

                CREATE TABLE OspreyMetadata (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );
            ");

            // Insert LibInfo
            using (var cmd = new SQLiteCommand(_conn))
            {
                cmd.CommandText = "INSERT INTO LibInfo (libLSID, createTime, numSpecs, majorVersion, minorVersion) VALUES (@lsid, datetime('now'), 0, @major, @minor)";
                cmd.Parameters.AddWithValue("@lsid", "urn:lsid:osprey:blib:" + Guid.NewGuid().ToString("N"));
                cmd.Parameters.AddWithValue("@major", BLIB_MAJOR_VERSION);
                cmd.Parameters.AddWithValue("@minor", BLIB_MINOR_VERSION);
                cmd.ExecuteNonQuery();
            }

            // Insert score types
            var scoreTypes = new[]
            {
                new { Id = 0, Name = "UNKNOWN", Prob = "NOT_A_PROBABILITY_VALUE" },
                new { Id = 1, Name = "PERCOLATOR QVALUE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 2, Name = "PEPTIDE PROPHET SOMETHING", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
                new { Id = 3, Name = "SPECTRUM MILL", Prob = "NOT_A_PROBABILITY_VALUE" },
                new { Id = 4, Name = "IDPICKER FDR", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 5, Name = "MASCOT IONS SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 6, Name = "TANDEM EXPECTATION VALUE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 7, Name = "PROTEIN PILOT CONFIDENCE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
                new { Id = 8, Name = "SCAFFOLD SOMETHING", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
                new { Id = 9, Name = "WATERS MSE PEPTIDE SCORE", Prob = "NOT_A_PROBABILITY_VALUE" },
                new { Id = 10, Name = "OMSSA EXPECTATION SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 11, Name = "PROTEIN PROSPECTOR EXPECTATION SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 12, Name = "SEQUEST XCORR", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 13, Name = "MAXQUANT SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 14, Name = "MORPHEUS SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 15, Name = "MSGF+ SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 16, Name = "PEAKS CONFIDENCE SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 17, Name = "BYONIC SCORE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 18, Name = "PEPTIDE SHAKER CONFIDENCE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
                new { Id = 19, Name = "GENERIC Q-VALUE", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT" },
                new { Id = 20, Name = "HARDKLOR IDOTP", Prob = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT" },
            };
            foreach (var st in scoreTypes)
            {
                using (var cmd = new SQLiteCommand(_conn))
                {
                    cmd.CommandText = "INSERT INTO ScoreTypes (id, scoreType, probabilityType) VALUES (@id, @name, @prob)";
                    cmd.Parameters.AddWithValue("@id", st.Id);
                    cmd.Parameters.AddWithValue("@name", st.Name);
                    cmd.Parameters.AddWithValue("@prob", st.Prob);
                    cmd.ExecuteNonQuery();
                }
            }

            // Insert ion mobility types
            var imTypes = new[] {
                new { Id = 0, Name = "none" },
                new { Id = 1, Name = "driftTime(msec)" },
                new { Id = 2, Name = "inverseK0(Vsec/cm^2)" },
                new { Id = 3, Name = "compensation(V)" },
            };
            foreach (var imt in imTypes)
            {
                using (var cmd = new SQLiteCommand(_conn))
                {
                    cmd.CommandText = "INSERT INTO IonMobilityTypes (id, ionMobilityType) VALUES (@id, @name)";
                    cmd.Parameters.AddWithValue("@id", imt.Id);
                    cmd.Parameters.AddWithValue("@name", imt.Name);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ExecuteNonQuery(string sql)
        {
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Zlib-compress a byte buffer. Returns raw bytes if compression does not reduce size.
        /// BiblioSpec readers determine compression by comparing blob length to expected uncompressed size.
        /// </summary>
        private static byte[] CompressBytes(byte[] raw)
        {
            using (var ms = new MemoryStream())
            {
                // Write 2-byte zlib header
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    deflate.Write(raw, 0, raw.Length);
                }
                byte[] compressed = ms.ToArray();
                if (compressed.Length >= raw.Length)
                    return raw;
                return compressed;
            }
        }

        /// <summary>
        /// Decompress a zlib-or-raw blob. If the blob length matches expectedLen, it is treated as raw.
        /// </summary>
        internal static byte[] DecompressBlob(byte[] blob, int expectedLen)
        {
            if (blob.Length == expectedLen)
                return blob;

            // Skip 2-byte zlib header
            using (var ms = new MemoryStream(blob, 2, blob.Length - 2))
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] DoublesToBytes(double[] values)
        {
            byte[] result = new byte[values.Length * 8];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
        }

        private static byte[] FloatsToBytes(float[] values)
        {
            byte[] result = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
        }

        internal static double[] BytesToDoubles(byte[] bytes)
        {
            double[] result = new double[bytes.Length / 8];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        internal static float[] BytesToFloats(byte[] bytes)
        {
            float[] result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        private static int FindDotOutsideBrackets(string s, bool reverse)
        {
            int depth = 0;
            if (reverse)
            {
                for (int i = s.Length - 1; i >= 0; i--)
                {
                    if (s[i] == ']')
                        depth++;
                    else if (s[i] == '[')
                        depth--;
                    else if (s[i] == '.' && depth == 0)
                        return i;
                }
                return -1;
            }

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '[')
                    depth++;
                else if (s[i] == ']')
                    depth--;
                else if (s[i] == '.' && depth == 0)
                    return i;
            }
            return -1;
        }

        private static void ExtractFragmentArrays(LibraryEntry entry,
            out double[] mzs, out float[] intensities)
        {
            if (entry.Fragments == null || entry.Fragments.Count == 0)
            {
                mzs = new double[0];
                intensities = new float[0];
                return;
            }

            mzs = new double[entry.Fragments.Count];
            intensities = new float[entry.Fragments.Count];
            for (int i = 0; i < entry.Fragments.Count; i++)
            {
                mzs[i] = entry.Fragments[i].Mz;
                intensities[i] = entry.Fragments[i].RelativeIntensity;
            }
        }

        #endregion
    }
}
