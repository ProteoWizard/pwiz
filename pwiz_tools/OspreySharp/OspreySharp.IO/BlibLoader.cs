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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
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

        /// <summary>
        /// Load library entries from a blib file.
        /// </summary>
        public List<LibraryEntry> Load(string path)
        {
            string connStr = string.Format("Data Source={0};Read Only=True;", path);
            using (var conn = new SQLiteConnection(connStr))
            {
                conn.Open();

                if (!TableExists(conn, "RefSpectra"))
                    throw new InvalidOperationException("Invalid blib file: RefSpectra table not found");

                var entries = LoadSpectra(conn);
                LoadProteinMappings(conn, entries);
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
                long count = (long)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        private List<LibraryEntry> LoadSpectra(SQLiteConnection conn)
        {
            var entries = new List<LibraryEntry>();

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

                        var modifications = ParseBlibModifications(peptideModSeq);

                        List<LibraryFragment> fragments;
                        if (peakMzBlob != null && peakIntBlob != null)
                            fragments = DecodeBlibPeaks(peakMzBlob, peakIntBlob, numPeaks);
                        else
                            fragments = new List<LibraryFragment>();

                        var entry = new LibraryEntry((uint)id, peptideSeq, peptideModSeq,
                            (byte)precursorCharge, precursorMz, retentionTime);
                        entry.Modifications = modifications;
                        entry.Fragments = fragments;

                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private void LoadProteinMappings(SQLiteConnection conn, List<LibraryEntry> entries)
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
                if (proteinMap.TryGetValue(entry.Id, out proteins))
                    entry.ProteinIds = proteins;
            }
        }

        /// <summary>
        /// Parse modifications from BiblioSpec modified sequence format.
        /// Handles "PEPTC[+57.0]IDE" (mass shift) and "PEPTC[160.0]IDE" (absolute mass).
        /// </summary>
        internal static List<Modification> ParseBlibModifications(string modSeq)
        {
            var modifications = new List<Modification>();
            int position = 0;
            int i = 0;

            while (i < modSeq.Length)
            {
                char c = modSeq[i];

                if (char.IsLetter(c))
                {
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
                        IdentifyModification(mass, modPosition == 0, out massDelta, out unimodId, out name);

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
        /// </summary>
        internal static void IdentifyModification(double mass, bool isNterm,
            out double massDelta, out int? unimodId, out string name)
        {
            // Check if this is likely an absolute mass (for Cys: subtract residue mass)
            double delta = mass;
            if (mass > 100.0 && mass < 200.0)
                delta = mass - 103.009185; // Cys residue mass

            if (Math.Abs(delta - CARBAMIDOMETHYL_MASS) < MOD_TOLERANCE)
            {
                massDelta = CARBAMIDOMETHYL_MASS;
                unimodId = 4;
                name = "Carbamidomethyl";
            }
            else if (Math.Abs(delta - OXIDATION_MASS) < MOD_TOLERANCE)
            {
                massDelta = OXIDATION_MASS;
                unimodId = 35;
                name = "Oxidation";
            }
            else if (Math.Abs(delta - ACETYL_MASS) < MOD_TOLERANCE && isNterm)
            {
                massDelta = ACETYL_MASS;
                unimodId = 1;
                name = "Acetyl";
            }
            else if (Math.Abs(delta - PHOSPHO_MASS) < MOD_TOLERANCE)
            {
                massDelta = PHOSPHO_MASS;
                unimodId = 21;
                name = "Phospho";
            }
            else if (Math.Abs(delta - DEAMIDATION_MASS) < MOD_TOLERANCE)
            {
                massDelta = DEAMIDATION_MASS;
                unimodId = 7;
                name = "Deamidated";
            }
            else if (Math.Abs(delta - TMT6PLEX_MASS) < MOD_TOLERANCE)
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
                foreach (var f in fragments)
                    f.RelativeIntensity /= maxIntensity;
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

        #endregion
    }
}
