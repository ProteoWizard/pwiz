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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Loads spectral libraries from EncyclopeDIA elib format (SQLite).
    /// Ported from osprey-io/src/library/elib.rs.
    /// </summary>
    public class ElibLoader
    {
        /// <summary>
        /// Load library entries from an elib file.
        /// </summary>
        public List<LibraryEntry> Load(string path)
        {
            string connStr = string.Format("Data Source={0};Read Only=True;", path);
            using (var conn = new SQLiteConnection(connStr))
            {
                conn.Open();

                if (TableExists(conn, "entries"))
                    return LoadStandardElib(conn);
                if (TableExists(conn, "peptidetoprotein"))
                    return LoadChromatogramElib(conn);

                throw new InvalidOperationException(
                    "Unknown elib schema - no recognized tables found");
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

        /// <summary>
        /// Load from standard elib format (EncyclopeDIA library).
        /// </summary>
        private List<LibraryEntry> LoadStandardElib(SQLiteConnection conn)
        {
            var entries = new List<LibraryEntry>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        e.PeptideModSeq,
                        e.PrecursorCharge,
                        e.PrecursorMz,
                        e.RTInSeconds,
                        e.ProteinAccession,
                        p.MassArray,
                        p.IntensityArray
                    FROM entries e
                    LEFT JOIN peaks p ON e.PeptideModSeq = p.PeptideModSeq
                        AND e.PrecursorCharge = p.PrecursorCharge
                    ORDER BY e.PeptideModSeq, e.PrecursorCharge";

                using (var reader = cmd.ExecuteReader())
                {
                    uint libId = 0;
                    while (reader.Read())
                    {
                        string peptideModSeq = reader.GetString(0);
                        int precursorCharge = reader.GetInt32(1);
                        double precursorMz = reader.GetDouble(2);
                        double rtSeconds = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3);
                        string protein = reader.IsDBNull(4) ? null : reader.GetString(4);

                        byte[] massArray = reader.IsDBNull(5) ? null : (byte[])reader[5];
                        byte[] intensityArray = reader.IsDBNull(6) ? null : (byte[])reader[6];

                        string sequence;
                        List<Modification> modifications;
                        ParseModifiedSequence(peptideModSeq, out sequence, out modifications);

                        List<LibraryFragment> fragments;
                        if (massArray != null && intensityArray != null)
                            fragments = DecodePeaks(massArray, intensityArray);
                        else
                            fragments = new List<LibraryFragment>();

                        // Convert RT from seconds to minutes
                        double rtMinutes = rtSeconds / 60.0;

                        var entry = new LibraryEntry(libId, sequence, peptideModSeq,
                            (byte)precursorCharge, precursorMz, rtMinutes);
                        entry.Modifications = modifications;
                        entry.Fragments = fragments;

                        if (protein != null)
                            entry.ProteinIds.Add(protein);

                        entries.Add(entry);
                        libId++;
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Load from chromatogram library elib format.
        /// </summary>
        private List<LibraryEntry> LoadChromatogramElib(SQLiteConnection conn)
        {
            var entries = new List<LibraryEntry>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT DISTINCT
                        PeptideModSeq,
                        PrecursorCharge,
                        PrecursorMz
                    FROM peptidetoprotein
                    ORDER BY PeptideModSeq, PrecursorCharge";

                using (var reader = cmd.ExecuteReader())
                {
                    uint libId = 0;
                    while (reader.Read())
                    {
                        string peptideModSeq = reader.GetString(0);
                        int precursorCharge = reader.GetInt32(1);
                        double precursorMz = reader.GetDouble(2);

                        string sequence;
                        List<Modification> modifications;
                        ParseModifiedSequence(peptideModSeq, out sequence, out modifications);

                        var entry = new LibraryEntry(libId, sequence, peptideModSeq,
                            (byte)precursorCharge, precursorMz, 0.0);
                        entry.Modifications = modifications;

                        entries.Add(entry);
                        libId++;
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Parse a modified sequence string (e.g. "PEPTC[+57.021]IDE") into
        /// bare sequence and modifications list.
        /// </summary>
        internal static void ParseModifiedSequence(string modSeq,
            out string sequence, out List<Modification> modifications)
        {
            var seqBuilder = new System.Text.StringBuilder();
            modifications = new List<Modification>();
            int position = 0;
            int i = 0;

            while (i < modSeq.Length)
            {
                char c = modSeq[i];

                if (char.IsLetter(c))
                {
                    seqBuilder.Append(char.ToUpperInvariant(c));
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
                        modifications.Add(new Modification
                        {
                            Position = modPosition,
                            MassDelta = mass
                        });
                    }
                }
                else if (c == '(')
                {
                    i++; // consume '('
                    int start = i;
                    while (i < modSeq.Length && modSeq[i] != ')')
                        i++;
                    string massStr = modSeq.Substring(start, i - start);
                    if (i < modSeq.Length)
                        i++; // consume ')'

                    string toParse = massStr.TrimStart('+');
                    double mass;
                    if (double.TryParse(toParse, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                    {
                        int modPosition = position > 0 ? position - 1 : 0;
                        modifications.Add(new Modification
                        {
                            Position = modPosition,
                            MassDelta = mass
                        });
                    }
                }
                else
                {
                    // Ignore other characters (underscores, periods for flanking residues)
                    i++;
                }
            }

            sequence = seqBuilder.ToString();
        }

        /// <summary>
        /// Decode peaks from elib blob format (little-endian doubles or floats).
        /// </summary>
        internal static List<LibraryFragment> DecodePeaks(byte[] massBlob, byte[] intensityBlob)
        {
            if (massBlob.Length % 8 == 0 && intensityBlob.Length % 8 == 0)
                return DecodePeaksF64(massBlob, intensityBlob);

            if (massBlob.Length % 4 == 0 && intensityBlob.Length % 4 == 0)
                return DecodePeaksF32(massBlob, intensityBlob);

            throw new InvalidOperationException("Invalid peak blob size");
        }

        private static List<LibraryFragment> DecodePeaksF64(byte[] massBlob, byte[] intensityBlob)
        {
            int nMasses = massBlob.Length / 8;
            int nIntensities = intensityBlob.Length / 8;

            if (nMasses != nIntensities)
                throw new InvalidOperationException(string.Format(
                    "Mismatched peak arrays: {0} masses, {1} intensities", nMasses, nIntensities));

            var fragments = new List<LibraryFragment>(nMasses);

            for (int i = 0; i < nMasses; i++)
            {
                double mz = BitConverter.ToDouble(massBlob, i * 8);
                double intensity = BitConverter.ToDouble(intensityBlob, i * 8);

                fragments.Add(new LibraryFragment
                {
                    Mz = mz,
                    RelativeIntensity = (float)intensity,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.Unknown,
                        Ordinal = (byte)(i + 1),
                        Charge = 1
                    }
                });
            }

            NormalizeIntensities(fragments);
            return fragments;
        }

        private static List<LibraryFragment> DecodePeaksF32(byte[] massBlob, byte[] intensityBlob)
        {
            int nMasses = massBlob.Length / 4;
            int nIntensities = intensityBlob.Length / 4;

            if (nMasses != nIntensities)
                throw new InvalidOperationException(string.Format(
                    "Mismatched peak arrays: {0} masses, {1} intensities", nMasses, nIntensities));

            var fragments = new List<LibraryFragment>(nMasses);

            for (int i = 0; i < nMasses; i++)
            {
                float mz = BitConverter.ToSingle(massBlob, i * 4);
                float intensity = BitConverter.ToSingle(intensityBlob, i * 4);

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

            NormalizeIntensities(fragments);
            return fragments;
        }

        private static void NormalizeIntensities(List<LibraryFragment> fragments)
        {
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
        }

        #endregion
    }
}
