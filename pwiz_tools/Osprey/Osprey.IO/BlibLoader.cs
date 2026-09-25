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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads spectral libraries from BiblioSpec blib format (SQLite).
    /// Ported from osprey-io/src/library/blib.rs.
    /// </summary>
    public class BlibLoader
    {
        private const double CARBAMIDOMETHYL_MASS = 57.021464;
        private const double OXIDATION_MASS = 15.994915;
        private const double ACETYL_MASS = 42.010565;
        private const double PHOSPHO_MASS = 79.966331;
        private const double DEAMIDATION_MASS = 0.984016;
        private const double TMT6PLEX_MASS = 229.162932;
        private const double MOD_TOLERANCE = 0.01;
        private const double CYSTEINE_RESIDUE_MASS = 103.009185;

        // The two reader-version probes, by file version (full path, size, mtime), so the
        // validity keys and the library cache that ask on every task and load open the file
        // once per process.
        private static readonly ConcurrentDictionary<string, bool> _annotationProbes =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, bool> _modificationProbes =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Whether the blib at <paramref name="path"/> has <c>RefSpectraPeakAnnotations</c>
        /// rows, which this reader types fragments from (<see cref="BlibPeakAnnotations"/>).
        /// A blib without them is read exactly as it was before the reader did. False for a
        /// missing or unreadable file.
        /// </summary>
        public static bool HasPeakAnnotations(string path)
        {
            return ProbeOnce(_annotationProbes, path, conn => HasRows(conn, BlibPeakAnnotations.TABLE_NAME));
        }

        /// <summary>
        /// Whether any modification text in the blib can read differently since
        /// <see cref="IdentifyModification"/> became residue- and precision-aware: a value
        /// printed with fewer than two decimals, or one between 100 and 200 Da. A superset of
        /// the libraries whose masses moved - BiblioSpec's one-decimal text is always in it -
        /// so a blib outside it is read exactly as before. False for a missing or unreadable
        /// file.
        /// </summary>
        public static bool HasPrecisionSensitiveModifications(string path)
        {
            return ProbeOnce(_modificationProbes, path, HasPrecisionSensitiveModificationText);
        }

        /// <summary>
        /// Load library entries from a blib file.
        /// </summary>
        public List<LibraryEntry> Load(string path, Action<string> logInfo = null)
        {
            string connStr = string.Format("Data Source={0};Read Only=True;", path);
            using (var conn = new SQLiteConnection(connStr))
            {
                conn.Open();

                if (!TableExists(conn, "RefSpectra"))
                    throw new InvalidOperationException("Invalid blib file: RefSpectra table not found");

                // Intern the repeated strings (sequences, modification names,
                // protein accessions) as the interned arrays are filled, so no
                // member is mutated after assignment. One pool spans both the
                // spectra and the protein-mapping pass; only object identity
                // changes, so output is unchanged.
                var interner = new LibraryStringInterner();
                var annotationStats = HasRows(conn, BlibPeakAnnotations.TABLE_NAME) ? new BlibAnnotationStats() : null;
                var entries = LoadSpectra(conn, interner, annotationStats);
                LoadProteinMappings(conn, entries, interner);
                interner.LogSummary(logInfo);
                if (annotationStats != null)
                    logInfo?.Invoke(annotationStats.Summary());
                return entries;
            }
        }

        #region Private helpers

        private static bool TableExists(SQLiteConnection conn, string tableName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
                cmd.Parameters.AddWithValue("@name", tableName);
                long count = (long)(cmd.ExecuteScalar() ?? 0L);
                return count > 0;
            }
        }

        /// <summary>True when the table exists and holds at least one row.</summary>
        private static bool HasRows(SQLiteConnection conn, string tableName)
        {
            if (!TableExists(conn, tableName))
                return false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = string.Format(@"SELECT EXISTS (SELECT 1 FROM [{0}])", tableName);
                return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) != 0;
            }
        }

        /// <summary>
        /// <paramref name="probe"/> run once per version of the file, read-only; false for a
        /// file that is missing or cannot be opened as a blib.
        /// </summary>
        private static bool ProbeOnce(ConcurrentDictionary<string, bool> cache, string path,
            Func<SQLiteConnection, bool> probe)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            var info = new FileInfo(path);
            string key = string.Format(CultureInfo.InvariantCulture, @"{0}|{1}|{2}",
                info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
            return cache.GetOrAdd(key, _ =>
            {
                try
                {
                    using (var conn = new SQLiteConnection(string.Format(@"Data Source={0};Read Only=True;", path)))
                    {
                        conn.Open();
                        return probe(conn);
                    }
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Streams the modified sequences that carry a bracket and stops at the first
        /// precision-sensitive one, which for a BiblioSpec library is the first row.
        /// </summary>
        private static bool HasPrecisionSensitiveModificationText(SQLiteConnection conn)
        {
            if (!TableExists(conn, "RefSpectra"))
                return false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT peptideModSeq FROM RefSpectra WHERE instr(peptideModSeq, '[') > 0";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0) && IsPrecisionSensitive(reader.GetString(0)))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Whether a modified sequence holds a bracket value printed with fewer than two
        /// decimals or lying between 100 and 200 Da - the two cases the residue- and
        /// precision-aware <see cref="IdentifyModification"/> can read differently.
        /// </summary>
        internal static bool IsPrecisionSensitive(string modSeq)
        {
            int open = modSeq.IndexOf('[');
            while (open >= 0)
            {
                int close = modSeq.IndexOf(']', open + 1);
                if (close < 0)
                    close = modSeq.Length;
                string text = modSeq.Substring(open + 1, close - open - 1).TrimStart('+');
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                    (PrintedDecimals(text) < 2 || (value > 100.0 && value < 200.0)))
                {
                    return true;
                }
                open = close < modSeq.Length ? modSeq.IndexOf('[', close + 1) : -1;
            }
            return false;
        }

        /// <summary>
        /// Reads every spectrum with its peaks. When <paramref name="annotationStats"/> is non-null
        /// the <c>RefSpectraPeakAnnotations</c> table has rows, and a second cursor over it,
        /// in the same RefSpectraID order, is merge-joined with the spectra so each spectrum's
        /// peaks are typed as they are read (<see cref="BlibPeakAnnotations"/>).
        /// </summary>
        private List<LibraryEntry> LoadSpectra(SQLiteConnection conn, LibraryStringInterner interner,
            BlibAnnotationStats annotationStats)
        {
            var entries = new List<LibraryEntry>();

            using (var annotations = annotationStats != null ? new AnnotationCursor(conn) : null)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        r.id,
                        r.peptideSeq,
                        r.peptideModSeq,
                        r.precursorMZ,
                        r.precursorCharge,
                        r.retentionTime,
                        r.numPeaks,
                        p.peakMZ,
                        p.peakIntensity
                    FROM RefSpectra r
                    LEFT JOIN RefSpectraPeaks p ON r.id = p.RefSpectraID
                    ORDER BY r.id";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long id = reader.GetInt64(0);
                        string peptideSeq = reader.GetString(1);
                        string peptideModSeq = reader.GetString(2);
                        double precursorMz = reader.GetDouble(3);
                        int precursorCharge = reader.GetInt32(4);
                        double retentionTime = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5);
                        int numPeaks = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                        byte[] peakMzBlob = reader.IsDBNull(7) ? null : (byte[])reader[7];
                        byte[] peakIntBlob = reader.IsDBNull(8) ? null : (byte[])reader[8];

                        // The same bound the TSV loader enforces. An invariant only one
                        // format checks is not an invariant, and DecoyGenerator's
                        // fragment-overlap gate relies on this one whatever the library
                        // was built from.
                        LibraryValidation.ValidatePeptideLength(peptideSeq);

                        var modifications = BuildInternedModifications(
                            ParseBlibModifications(peptideModSeq), interner);

                        LibraryFragment[] fragments;
                        if (peakMzBlob != null && peakIntBlob != null)
                            fragments = DecodeBlibPeaks(peakMzBlob, peakIntBlob, numPeaks).ToArray();
                        else
                            fragments = Array.Empty<LibraryFragment>();
                        var rows = annotations?.RowsFor(id);
                        if (rows != null && rows.Count > 0)
                            BlibPeakAnnotations.Apply(peptideSeq, modifications, fragments, rows, annotationStats);

                        var entry = new LibraryEntry((uint)id,
                            interner.Intern(peptideSeq), interner.Intern(peptideModSeq),
                            (byte)precursorCharge, precursorMz, retentionTime);
                        entry.Modifications = modifications;
                        entry.Fragments = fragments;

                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private void LoadProteinMappings(SQLiteConnection conn, List<LibraryEntry> entries,
            LibraryStringInterner interner)
        {
            if (!TableExists(conn, "RefSpectraProteins") || !TableExists(conn, "Proteins"))
                return;

            var proteinMap = new Dictionary<uint, List<string>>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT rsp.RefSpectraID, p.accession
                    FROM RefSpectraProteins rsp
                    JOIN Proteins p ON rsp.ProteinID = p.id
                    ORDER BY rsp.RefSpectraID";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uint refId = (uint)reader.GetInt64(0);
                        string accession = reader.GetString(1);

                        List<string> proteins;
                        if (!proteinMap.TryGetValue(refId, out proteins))
                        {
                            proteins = new List<string>();
                            proteinMap[refId] = proteins;
                        }
                        proteins.Add(accession);
                    }
                }
            }

            foreach (var entry in entries)
            {
                List<string> proteins;
                if (proteinMap.TryGetValue(entry.Id, out proteins) && proteins.Count > 0)
                {
                    var interned = new string[proteins.Count];
                    for (int i = 0; i < proteins.Count; i++)
                        interned[i] = interner.Intern(proteins[i]);
                    entry.ProteinIds = interned;
                }
            }
        }

        /// <summary>
        /// Intern each modification's <see cref="Modification.Name"/> and return
        /// the mods as an array (empty -> shared empty array). Only string
        /// identity changes.
        /// </summary>
        private static Modification[] BuildInternedModifications(
            List<Modification> mods, LibraryStringInterner interner)
        {
            if (mods == null || mods.Count == 0)
                return Array.Empty<Modification>();
            var result = new Modification[mods.Count];
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                m.Name = interner.Intern(m.Name);
                result[i] = m;
            }
            return result;
        }

        /// <summary>
        /// Parse modifications from BiblioSpec modified sequence format.
        /// Handles "PEPTC[+57.0]IDE" (mass shift) and "PEPTC[160.0]IDE" (absolute mass).
        /// </summary>
        internal static List<Modification> ParseBlibModifications(string modSeq)
        {
            var modifications = new List<Modification>();
            int position = 0;
            char residue = '\0';
            int i = 0;

            while (i < modSeq.Length)
            {
                char c = modSeq[i];

                if (char.IsLetter(c))
                {
                    residue = c;
                    position++;
                    i++;
                }
                else if (c == '[')
                {
                    i++; // consume '['
                    int start = i;
                    while (i < modSeq.Length && modSeq[i] != ']')
                        i++;
                    string massStr = modSeq.Substring(start, i - start);
                    if (i < modSeq.Length)
                        i++; // consume ']'

                    string toParse = massStr.TrimStart('+');
                    double mass;
                    if (double.TryParse(toParse, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                    {
                        int modPosition = position > 0 ? position - 1 : 0;
                        double massDelta;
                        int? unimodId;
                        string name;
                        bool isSigned = massStr.Length > 0 && (massStr[0] == '+' || massStr[0] == '-');
                        IdentifyModification(mass, modPosition == 0, residue, isSigned,
                            PrintedDecimals(toParse), out massDelta, out unimodId, out name);

                        modifications.Add(new Modification
                        {
                            Position = modPosition,
                            UnimodId = unimodId,
                            MassDelta = massDelta,
                            Name = name
                        });
                    }
                }
                else
                {
                    i++;
                }
            }

            return modifications;
        }

        /// <summary>
        /// Identify a modification by its mass. Recognizes common modifications and
        /// handles both mass shift and absolute mass formats.
        ///
        /// <para>An UNSIGNED value between 100 and 200 Da on cysteine is taken as the absolute
        /// mass of the modified residue (<c>C[160.0]</c>). A signed value, or one on any other
        /// residue, is a mass shift: GlyGly on lysine is <c>K[+114.042927]</c> and
        /// N-ethylmaleimide on cysteine <c>C[+125.047679]</c>, and reading either as an
        /// absolute cysteine mass left an 11 or 22 Da shift on every fragment that carries
        /// it.</para>
        ///
        /// <para>A value snaps to a known modification within half its last printed digit, and
        /// never within less than <see cref="MOD_TOLERANCE"/>: BiblioSpec prints one decimal
        /// (<c>C[+57.0]</c>, <c>[+42.0]</c>, <c>S[+80.0]</c>), which is the known mass rounded,
        /// so the fragments get the known mass rather than one 0.02-0.03 Da off it. Text with
        /// two or more decimals keeps the 0.01 Da snap it always had.</para>
        /// </summary>
        /// <param name="mass">The bracket value.</param>
        /// <param name="isNterm">The modification is on the first residue or before it.</param>
        /// <param name="residue">The residue the bracket follows, or '\0' before the first.</param>
        /// <param name="isSigned">The text carries an explicit '+' or '-'.</param>
        /// <param name="decimals">Digits printed after the decimal point.</param>
        /// <param name="massDelta">The mass shift: the known modification's mass when snapped.</param>
        /// <param name="unimodId">The known modification's UniMod id, or null.</param>
        /// <param name="name">The known modification's name, or null.</param>
        internal static void IdentifyModification(double mass, bool isNterm, char residue, bool isSigned,
            int decimals, out double massDelta, out int? unimodId, out string name)
        {
            double delta = mass;
            if (residue == 'C' && !isSigned && mass > 100.0 && mass < 200.0)
                delta = mass - CYSTEINE_RESIDUE_MASS;
            double tolerance = Math.Max(MOD_TOLERANCE, 0.5 * Math.Pow(10, -decimals));

            if (Math.Abs(delta - CARBAMIDOMETHYL_MASS) < tolerance)
            {
                massDelta = CARBAMIDOMETHYL_MASS;
                unimodId = 4;
                name = "Carbamidomethyl";
            }
            else if (Math.Abs(delta - OXIDATION_MASS) < tolerance)
            {
                massDelta = OXIDATION_MASS;
                unimodId = 35;
                name = "Oxidation";
            }
            else if (Math.Abs(delta - ACETYL_MASS) < tolerance && isNterm)
            {
                massDelta = ACETYL_MASS;
                unimodId = 1;
                name = "Acetyl";
            }
            else if (Math.Abs(delta - PHOSPHO_MASS) < tolerance)
            {
                massDelta = PHOSPHO_MASS;
                unimodId = 21;
                name = "Phospho";
            }
            else if (Math.Abs(delta - DEAMIDATION_MASS) < tolerance)
            {
                massDelta = DEAMIDATION_MASS;
                unimodId = 7;
                name = "Deamidated";
            }
            else if (Math.Abs(delta - TMT6PLEX_MASS) < tolerance)
            {
                massDelta = TMT6PLEX_MASS;
                unimodId = 737;
                name = "TMT6plex";
            }
            else
            {
                massDelta = delta;
                unimodId = null;
                name = null;
            }
        }

        /// <summary>
        /// Digits printed after the decimal point of a bracket value, or
        /// <see cref="int.MaxValue"/> for exponent notation, whose precision the text does not
        /// show.
        /// </summary>
        internal static int PrintedDecimals(string number)
        {
            if (number.IndexOfAny(new[] { 'e', 'E' }) >= 0)
                return int.MaxValue;
            int dot = number.IndexOf('.');
            return dot < 0 ? 0 : number.Length - dot - 1;
        }

        /// <summary>
        /// Attempt zlib decompression of a blob. Returns null if decompression fails.
        /// </summary>
        internal static byte[] TryZlibDecompress(byte[] data, int expectedSize)
        {
            try
            {
                // zlib format: 2-byte header + deflate data + 4-byte checksum
                // Skip the 2-byte zlib header, use DeflateStream on the rest
                if (data.Length < 3)
                    return null;

                using (var input = new MemoryStream(data, 2, data.Length - 2))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream(expectedSize))
                {
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decode peak blobs from blib format. Handles both compressed (zlib) and
        /// uncompressed formats. m/z is stored as f64, intensity as f32 or f64.
        /// </summary>
        internal static List<LibraryFragment> DecodeBlibPeaks(byte[] mzBlob, byte[] intensityBlob, int numPeaks)
        {
            byte[] mzData;
            byte[] intData;
            DecompressPeakBlobs(mzBlob, intensityBlob, numPeaks, out mzData, out intData);

            if (mzData.Length % 8 != 0)
                throw new InvalidOperationException(string.Format(
                    "Invalid peak m/z blob size: {0} bytes (not a multiple of 8)", mzData.Length));

            int nPeaks = mzData.Length / 8;

            // Intensity can be float (4 bytes) or double (8 bytes)
            int intensitySize;
            if (intData.Length == nPeaks * 4)
                intensitySize = 4;
            else if (intData.Length == nPeaks * 8)
                intensitySize = 8;
            else
                throw new InvalidOperationException(string.Format(
                    "Invalid peak intensity blob size: expected {0} or {1} bytes, got {2}",
                    nPeaks * 4, nPeaks * 8, intData.Length));

            var fragments = new List<LibraryFragment>(nPeaks);

            for (int i = 0; i < nPeaks; i++)
            {
                double mz = BitConverter.ToDouble(mzData, i * 8);

                float intensity;
                if (intensitySize == 4)
                    intensity = BitConverter.ToSingle(intData, i * 4);
                else
                    intensity = (float)BitConverter.ToDouble(intData, i * 8);

                fragments.Add(new LibraryFragment
                {
                    Mz = mz,
                    RelativeIntensity = intensity,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.Unknown,
                        Ordinal = (byte)(i + 1),
                        Charge = 1
                    }
                });
            }

            // Normalize intensities
            float maxIntensity = 0f;
            foreach (var f in fragments)
            {
                if (f.RelativeIntensity > maxIntensity)
                    maxIntensity = f.RelativeIntensity;
            }

            if (maxIntensity > 0f)
            {
                for (int i = 0; i < fragments.Count; i++)
                {
                    var f = fragments[i];
                    f.RelativeIntensity /= maxIntensity;
                    fragments[i] = f;
                }
            }

            return fragments;
        }

        private static void DecompressPeakBlobs(byte[] mzBlob, byte[] intensityBlob, int numPeaks,
            out byte[] mzData, out byte[] intData)
        {
            int expectedMzSize = numPeaks * 8; // f64

            // If raw m/z blob is already the right size, assume uncompressed
            if (mzBlob.Length == expectedMzSize && mzBlob.Length % 8 == 0)
            {
                mzData = mzBlob;

                int expectedIntF32 = numPeaks * 4;
                int expectedIntF64 = numPeaks * 8;
                if (intensityBlob.Length == expectedIntF32 || intensityBlob.Length == expectedIntF64)
                {
                    intData = intensityBlob;
                    return;
                }

                // Try decompressing intensity only
                byte[] decInt = TryZlibDecompress(intensityBlob, expectedIntF32);
                if (decInt != null && (decInt.Length == expectedIntF32 || decInt.Length == expectedIntF64))
                {
                    intData = decInt;
                    return;
                }

                intData = intensityBlob;
                return;
            }

            // m/z blob doesn't match expected raw size - try zlib decompression
            if (numPeaks > 0)
            {
                byte[] mzDec = TryZlibDecompress(mzBlob, expectedMzSize);
                if (mzDec != null)
                {
                    mzData = mzDec;

                    if (intensityBlob.Length == numPeaks * 4 || intensityBlob.Length == numPeaks * 8)
                    {
                        intData = intensityBlob;
                    }
                    else
                    {
                        byte[] intDec = TryZlibDecompress(intensityBlob, numPeaks * 4);
                        intData = intDec ?? intensityBlob;
                    }
                    return;
                }
            }

            // Try decompressing m/z and infer n_peaks from result
            if (mzBlob.Length % 8 != 0)
            {
                byte[] mzDec = TryZlibDecompress(mzBlob, mzBlob.Length * 4);
                if (mzDec != null)
                {
                    mzData = mzDec;
                    int inferredN = mzDec.Length / 8;

                    if (intensityBlob.Length == inferredN * 4 || intensityBlob.Length == inferredN * 8)
                    {
                        intData = intensityBlob;
                    }
                    else
                    {
                        byte[] intDec = TryZlibDecompress(intensityBlob, inferredN * 4);
                        intData = intDec ?? intensityBlob;
                    }
                    return;
                }
            }

            // Fall through - use raw blobs
            mzData = mzBlob;
            intData = intensityBlob;
        }

        /// <summary>
        /// A forward-only cursor over <c>RefSpectraPeakAnnotations</c> in RefSpectraID order,
        /// advanced in step with the spectra reader so only one spectrum's rows are held.
        /// </summary>
        private sealed class AnnotationCursor : IDisposable
        {
            private readonly SQLiteCommand _command;
            private readonly SQLiteDataReader _reader;
            private readonly List<BlibAnnotationRow> _rows = new List<BlibAnnotationRow>();
            private bool _hasRow;

            public AnnotationCursor(SQLiteConnection conn)
            {
                _command = conn.CreateCommand();
                _command.CommandText = @"
                    SELECT RefSpectraID, peakIndex, name, charge
                    FROM RefSpectraPeakAnnotations
                    ORDER BY RefSpectraID, peakIndex, id";
                _reader = _command.ExecuteReader();
                _hasRow = _reader.Read();
            }

            /// <summary>
            /// The rows for <paramref name="refSpectraId"/>, which must not be smaller than the
            /// id of the previous call. The list is reused by the next call.
            /// </summary>
            public List<BlibAnnotationRow> RowsFor(long refSpectraId)
            {
                _rows.Clear();
                while (_hasRow && _reader.GetInt64(0) < refSpectraId)
                    _hasRow = _reader.Read();
                while (_hasRow && _reader.GetInt64(0) == refSpectraId)
                {
                    _rows.Add(new BlibAnnotationRow
                    {
                        PeakIndex = _reader.IsDBNull(1) ? -1 : _reader.GetInt32(1),
                        Name = _reader.IsDBNull(2) ? null : _reader.GetString(2),
                        Charge = _reader.IsDBNull(3) ? 0 : _reader.GetInt32(3),
                    });
                    _hasRow = _reader.Read();
                }
                return _rows;
            }

            public void Dispose()
            {
                _reader.Dispose();
                _command.Dispose();
            }
        }

        #endregion
    }
}
