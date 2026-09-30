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
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads spectral libraries from BiblioSpec blib format (SQLite).
    /// Ported from osprey-io/src/library/blib.rs.
    /// </summary>
    public class BlibLoader
    {
        private const double CYSTEINE_RESIDUE_MASS = 103.009185;

        /// <summary>
        /// Version of what this reader makes of a blib. 2: every peak is typed from m/z
        /// (<see cref="FragmentTyping"/>), where version 1 left every peak Unknown, and
        /// modification text is read residue- and precision-aware
        /// (<see cref="IdentifyModification"/>). Every blib reads differently under it, so the
        /// <c>.libcache</c> composition term and the task-key term both carry it for every blib,
        /// and a change to either moves both by editing this one value.
        /// </summary>
        public const string READER_VERSION = @"2";

        private readonly FragmentToleranceConfig _fragmentTolerance;

        /// <summary>
        /// A reader that types each spectrum's peaks within <paramref name="fragmentTolerance"/>,
        /// the search's fragment tolerance, so a peak it calls y6 is one chromatogram extraction
        /// would count as y6.
        /// </summary>
        public BlibLoader(FragmentToleranceConfig fragmentTolerance)
        {
            _fragmentTolerance = fragmentTolerance;
        }

        /// <summary>
        /// The <c>.libcache</c> composition term of a blib read by this reader: its version and
        /// the tolerance it types within, which the cached fragment types carry.
        /// </summary>
        public static string CacheTerm(FragmentToleranceConfig fragmentTolerance)
        {
            // ReSharper disable LocalizableElement
            return string.Format(CultureInfo.InvariantCulture, "blib_reader:{0},{1},{2}\n",
                READER_VERSION, fragmentTolerance.Tolerance, fragmentTolerance.Unit);
            // ReSharper restore LocalizableElement
        }

        /// <summary>
        /// Load library entries from a blib file, every peak typed from m/z
        /// (<see cref="FragmentTyping"/>). A <c>RefSpectraPeakAnnotations</c> table is not read:
        /// Skyline designed it for small molecules, and no proteomics software is known to write
        /// peptide fragment ions into it.
        /// </summary>
        public List<LibraryEntry> Load(string path, Action<string> logInfo = null)
        {
            string connStr = string.Format(@"Data Source={0};Read Only=True;", path);
            using (var conn = new SQLiteConnection(connStr))
            {
                conn.Open();

                if (!TableExists(conn, @"RefSpectra"))
                    throw new InvalidOperationException(OspreyIOResources.BlibLoader_Load_Invalid_BiblioSpec_library__the_RefSpectra_table_was_not_found_);

                // Intern the repeated strings (sequences, modification names,
                // protein accessions) as the interned arrays are filled, so no
                // member is mutated after assignment. One pool spans both the
                // spectra and the protein-mapping pass; only object identity
                // changes, so output is unchanged.
                var interner = new LibraryStringInterner();
                var typingStats = new FragmentTypingStats();
                var entries = LoadSpectra(conn, interner, typingStats);
                LoadProteinMappings(conn, entries, interner);
                interner.LogSummary(logInfo);
                logInfo?.Invoke(string.Format(
                    OspreyIOResources.BlibLoader_Load_Typed__0_N0__of__1_N0__library_peaks_as_b_or_y_ions_within__2___3_,
                    typingStats.Typed, typingStats.Total, _fragmentTolerance.Tolerance,
                    _fragmentTolerance.Unit.GetLocalizedString()));
                return entries;
            }
        }

        #region Private helpers

        private static bool TableExists(SQLiteConnection conn, string tableName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
                cmd.Parameters.AddWithValue(@"@name", tableName);
                long count = (long)(cmd.ExecuteScalar() ?? 0L);
                return count > 0;
            }
        }

        /// <summary>
        /// Reads every spectrum with its peaks, typing them as they are read. A library with any
        /// modification that cannot be identified is refused, listing every such modification.
        /// </summary>
        private List<LibraryEntry> LoadSpectra(SQLiteConnection conn, LibraryStringInterner interner,
            FragmentTypingStats typingStats)
        {
            var entries = new List<LibraryEntry>();
            var unidentifiedMods = new UnidentifiedModifications();

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

                        var parsed = ParseBlibModifications(peptideModSeq, out string unidentified);
                        if (parsed == null)
                        {
                            unidentifiedMods.Add(unidentified, id, peptideModSeq);
                            continue;
                        }
                        var modifications = BuildInternedModifications(parsed, interner);

                        LibraryFragment[] fragments;
                        if (peakMzBlob != null && peakIntBlob != null)
                            fragments = DecodeBlibPeaks(peakMzBlob, peakIntBlob, numPeaks).ToArray();
                        else
                            fragments = Array.Empty<LibraryFragment>();
                        FragmentTyping.TypeFragments(peptideSeq, modifications, precursorCharge, fragments,
                            _fragmentTolerance, typingStats);

                        var entry = new LibraryEntry((uint)id,
                            interner.Intern(peptideSeq), interner.Intern(peptideModSeq),
                            (byte)precursorCharge, precursorMz, retentionTime);
                        entry.Modifications = modifications;
                        entry.Fragments = fragments;

                        entries.Add(entry);
                    }
                }
            }

            if (unidentifiedMods.Any)
                throw unidentifiedMods.ToException();
            return entries;
        }

        private void LoadProteinMappings(SQLiteConnection conn, List<LibraryEntry> entries,
            LibraryStringInterner interner)
        {
            if (!TableExists(conn, @"RefSpectraProteins") || !TableExists(conn, @"Proteins"))
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
        /// Parse modifications from BiblioSpec modified sequence format: a mass shift
        /// (<c>PEPTC[+57.0]IDE</c>), an absolute cysteine mass (<c>PEPTC[160.0]IDE</c>) or a
        /// UniMod id (<c>PEPTC[UniMod:4]IDE</c>, or in parentheses as DIA-NN writes it), each
        /// resolved by <see cref="IdentifyModification"/>. Returns null when any cannot be, with
        /// the first such bracket in <paramref name="unidentified"/>.
        /// </summary>
        internal static List<Modification> ParseBlibModifications(string modSeq, out string unidentified)
        {
            unidentified = null;
            var modifications = new List<Modification>();
            int firstResidue = FirstResidueIndex(modSeq, 0);
            int lastResidue = PrecedingResidueIndex(modSeq, modSeq.Length);
            int position = 0;
            for (int i = 0; i < modSeq.Length; i++)
            {
                char c = modSeq[i];
                if (char.IsLetter(c))
                    position++;
                if (c != '[' && c != '(')
                    continue;
                int close = CloseOf(modSeq, i);
                // A bracket before the first residue modifies the first residue.
                int residueIndex = position > 0 ? PrecedingResidueIndex(modSeq, i) : firstResidue;
                char residue = residueIndex >= 0 ? modSeq[residueIndex] : '\0';
                var modification = IdentifyModification(modSeq.Substring(i + 1, close - i - 1), residue,
                    residueIndex == firstResidue, residueIndex == lastResidue);
                if (modification == null)
                {
                    unidentified = modSeq.Substring(i, Math.Min(close + 1, modSeq.Length) - i);
                    return null;
                }
                modification.Position = Math.Max(position - 1, 0);
                modifications.Add(modification);
                i = close;
            }
            return modifications;
        }

        /// <summary>
        /// Identify the modification a bracket's <paramref name="text"/> names on
        /// <paramref name="residue"/>, or null when it cannot be identified.
        ///
        /// <para>A UniMod id must be one Osprey knows (<see cref="UniMod"/>). A mass is matched
        /// against the known modifications allowed on the residue as Skyline matches it, at the
        /// precision it is printed with (<see cref="UniMod.Match"/>): BiblioSpec prints one
        /// decimal (<c>C[+57.0]</c>, <c>K[+8.0]</c>), and the fragments need the modification's
        /// exact mass, not one up to 0.05 Da off it. A mass printed with
        /// <see cref="UniMod.MAX_PRECISION_TO_MATCH"/> or more decimals that matches nothing is
        /// used as printed; one printed with fewer cannot be, and is not identified.</para>
        ///
        /// <para>An UNSIGNED value between 100 and 200 Da on cysteine is taken as the absolute
        /// mass of the modified residue (<c>C[160.0]</c>). A signed value, or one on any other
        /// residue, is a mass shift: GlyGly on lysine is <c>K[+114.042927]</c> and
        /// N-ethylmaleimide on cysteine <c>C[+125.047679]</c>.</para>
        /// </summary>
        internal static Modification IdentifyModification(string text, char residue, bool isNTerm, bool isCTerm)
        {
            if (text.StartsWith(UniMod.PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(text.Substring(UniMod.PREFIX.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                    return null;
                var byId = UniMod.Find(id);
                return byId == null ? null : new Modification { UnimodId = byId.Id, MassDelta = byId.Mass, Name = byId.Name };
            }

            string number = text.TrimStart('+');
            if (!double.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out double mass))
            {
                return null;
            }
            bool isSigned = text.Length > 0 && (text[0] == '+' || text[0] == '-');
            double delta = mass;
            if (residue == 'C' && !isSigned && mass > 100.0 && mass < 200.0)
                delta = mass - CYSTEINE_RESIDUE_MASS;
            int precision = PrintedDecimals(number);
            var known = UniMod.Match(delta, precision, residue, isNTerm, isCTerm);
            if (known != null)
                return new Modification { UnimodId = known.Id, MassDelta = known.Mass, Name = known.Name };
            if (precision < UniMod.MAX_PRECISION_TO_MATCH)
                return null;
            return new Modification { MassDelta = delta };
        }

        /// <summary>Digits printed after the decimal point of a bracket value.</summary>
        internal static int PrintedDecimals(string number)
        {
            int dot = number.IndexOf('.');
            return dot < 0 ? 0 : number.Length - dot - 1;
        }

        /// <summary>The index of the bracket closing the one at <paramref name="open"/>, or the end of the text.</summary>
        private static int CloseOf(string modSeq, int open)
        {
            char closing = modSeq[open] == '[' ? ']' : ')';
            int close = modSeq.IndexOf(closing, open + 1);
            return close < 0 ? modSeq.Length : close;
        }

        /// <summary>The index of the first residue after <paramref name="start"/>, skipping brackets, or -1.</summary>
        private static int FirstResidueIndex(string modSeq, int start)
        {
            for (int j = start; j < modSeq.Length; j++)
            {
                if (char.IsLetter(modSeq[j]))
                    return j;
                if (modSeq[j] == '[' || modSeq[j] == '(')
                    j = CloseOf(modSeq, j);
            }
            return -1;
        }

        /// <summary>The index of the last residue before <paramref name="end"/>, skipping brackets, or -1.</summary>
        private static int PrecedingResidueIndex(string modSeq, int end)
        {
            int depth = 0;
            for (int j = end - 1; j >= 0; j--)
            {
                char c = modSeq[j];
                if (c == ']' || c == ')')
                    depth++;
                else if (c == '[' || c == '(')
                    depth--;
                else if (depth == 0 && char.IsLetter(c))
                    return j;
            }
            return -1;
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
                    OspreyIOResources.BlibLoader_DecodeBlibPeaks_Invalid_peak_m_z_blob_size___0__bytes__not_a_multiple_of_8_, mzData.Length));

            int nPeaks = mzData.Length / 8;

            // Intensity can be float (4 bytes) or double (8 bytes)
            int intensitySize;
            if (intData.Length == nPeaks * 4)
                intensitySize = 4;
            else if (intData.Length == nPeaks * 8)
                intensitySize = 8;
            else
                throw new InvalidOperationException(string.Format(
                    OspreyIOResources.BlibLoader_DecodeBlibPeaks_Invalid_peak_intensity_blob_size__expected__0__or__1__bytes__got__2_,
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
        /// The modifications a library carries that cannot be identified, each listed once with
        /// the number of spectra carrying it and the first of them.
        /// </summary>
        private sealed class UnidentifiedModifications
        {
            private const int MAX_LISTED = 100;

            private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
            private readonly List<string> _order = new List<string>();
            private readonly Dictionary<string, string> _firstSpectrum = new Dictionary<string, string>();
            private int _spectrumCount;

            public bool Any => _spectrumCount > 0;

            public void Add(string modification, long refSpectraId, string peptideModSeq)
            {
                _spectrumCount++;
                if (_counts.TryGetValue(modification, out int count))
                {
                    _counts[modification] = count + 1;
                    return;
                }
                _counts.Add(modification, 1);
                _order.Add(modification);
                _firstSpectrum.Add(modification, string.Format(CultureInfo.InvariantCulture, @"{0} (id {1})",
                    peptideModSeq, refSpectraId));
            }

            public InvalidDataException ToException()
            {
                var sb = new StringBuilder(string.Format(
                    OspreyIOResources.BlibLoader_ToException__0__library_spectra_have_modifications_Osprey_cannot_identify,
                    _spectrumCount, UniMod.MAX_PRECISION_TO_MATCH));
                for (int i = 0; i < _order.Count && i < MAX_LISTED; i++)
                {
                    string modification = _order[i];
                    sb.AppendLine().Append(@"  ").Append(string.Format(
                        OspreyIOResources.BlibLoader_ToException__0__in__1__spectra__first__2_,
                        modification, _counts[modification], _firstSpectrum[modification]));
                }
                if (_order.Count > MAX_LISTED)
                {
                    sb.AppendLine().Append(@"  ").Append(string.Format(
                        OspreyIOResources.BlibLoader_ToException____and__0__more_modifications, _order.Count - MAX_LISTED));
                }
                return new InvalidDataException(sb.ToString());
            }
        }

        #endregion
    }
}
