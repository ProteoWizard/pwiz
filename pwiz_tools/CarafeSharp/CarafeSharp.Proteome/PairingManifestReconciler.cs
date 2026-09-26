/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/PairingManifestReconciler.java
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
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Prunes an FDRBench pairing manifest to the peptides a predicted library actually
    /// contains, so it describes exactly the searched library. Manifest rows whose peptide the
    /// library lacks are dropped, a pair group whose target did not survive is dropped whole,
    /// and the kept groups are renumbered from 0 in their original order. Sequences are compared
    /// upper-cased and I-to-L normalized, so an I2L mismatch between the files cannot drop
    /// everything.
    /// </summary>
    public static class PairingManifestReconciler
    {
        /// <summary>The DIA-NN library TSV column holding the unmodified sequence.</summary>
        public const string LIBRARY_SEQUENCE_COLUMN = @"StrippedPeptide";

        private static readonly UTF8Encoding UTF8_NO_BOM = new UTF8Encoding(false);

        /// <summary>
        /// Reconciles <paramref name="manifestIn"/> against <paramref name="library"/> (a DIA-NN
        /// TSV, or a BiblioSpec .blib by extension) and writes <paramref name="manifestOut"/>.
        /// </summary>
        public static PairingReconciliationResult Run(string manifestIn, string library, string manifestOut, TextWriter log = null)
        {
            log?.WriteLine(@"Reconciling pairing manifest '" + manifestIn + @"' against predicted library '" + library + @"'");
            var result = new PairingReconciliationResult();
            var librarySequences = ReadLibrarySequences(library);
            result.LibraryPeptides = librarySequences.Count;

            var groups = ReadManifestGroups(manifestIn, result);
            result.GroupsIn = groups.Count;
            var manifestSequences = new HashSet<string>();
            foreach (var group in groups)
            {
                foreach (var row in group)
                    manifestSequences.Add(Normalize(row.Sequence));
            }
            foreach (string sequence in librarySequences)
            {
                if (!manifestSequences.Contains(sequence))
                    result.LibraryPeptidesNotInManifest++;
            }

            using (var writer = EntrapmentFastaBuilder.CreateWriter(manifestOut))
            {
                writer.Write(EntrapmentFastaBuilder.MANIFEST_HEADER + "\n");
                int newPairIndex = 0;
                foreach (var group in groups)
                {
                    var target = group.Find(row => row.PeptideType == EntrapmentFastaBuilder.PEPTIDE_TYPE_TARGET);
                    if (target == null || !librarySequences.Contains(Normalize(target.Sequence)))
                    {
                        result.GroupsDropped++;
                        result.RowsDropped += group.Count;
                        continue;
                    }
                    bool decoyKept = false;
                    var keptRows = new List<ManifestRow>(group.Count);
                    foreach (var row in group)
                    {
                        if (librarySequences.Contains(Normalize(row.Sequence)))
                        {
                            keptRows.Add(row);
                            if (row.PeptideType == EntrapmentFastaBuilder.PEPTIDE_TYPE_DECOY)
                                decoyKept = true;
                        }
                        else
                        {
                            result.RowsDropped++;
                        }
                    }
                    foreach (var row in keptRows)
                    {
                        writer.Write(row.Sequence + "\t" + row.Decoy + "\t" + row.Proteins + "\t" + row.PeptideType + "\t" +
                                     newPairIndex.ToString(CultureInfo.InvariantCulture) + "\n");
                        result.RowsKept++;
                    }
                    if (!decoyKept)
                        result.KeptTargetsWithoutDecoy++;
                    result.GroupsKept++;
                    newPairIndex++;
                }
            }

            log?.WriteLine(string.Format(CultureInfo.InvariantCulture,
                @"Reconciled manifest: {0}/{1} pair groups kept ({2} dropped), {3}/{4} rows kept ({5} dropped). Library peptides: {6}; not in manifest: {7}; kept targets missing a decoy: {8}",
                result.GroupsKept, result.GroupsIn, result.GroupsDropped, result.RowsKept, result.RowsIn, result.RowsDropped,
                result.LibraryPeptides, result.LibraryPeptidesNotInManifest, result.KeptTargetsWithoutDecoy));
            if (result.LibraryPeptidesNotInManifest > 0)
            {
                log?.WriteLine(@"WARNING: " + result.LibraryPeptidesNotInManifest.ToString(CultureInfo.InvariantCulture) +
                               @" library peptide(s) are absent from the pairing manifest; FDRBench will drop them. This is expected to be 0 when the peptide FASTA is built with the same rules as the library prediction.");
            }
            return result;
        }

        /// <summary>Java-trimmed, upper-cased and I-to-L normalized; null becomes empty.</summary>
        public static string Normalize(string sequence)
        {
            return sequence == null ? string.Empty : JavaText.ToUpper(JavaText.Trim(sequence)).Replace('I', 'L');
        }

        /// <summary>Manifest rows grouped by peptide_pair_index, groups in first-seen order.</summary>
        private static List<List<ManifestRow>> ReadManifestGroups(string manifestIn, PairingReconciliationResult result)
        {
            var groups = new List<List<ManifestRow>>();
            var groupByIndex = new Dictionary<string, List<ManifestRow>>();
            using (var reader = new StreamReader(manifestIn, UTF8_NO_BOM, false))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new IOException(@"empty manifest: " + manifestIn);
                string[] header = headerLine.Split('\t');
                int iSeq = ColumnIndex(header, @"sequence", manifestIn);
                int iDecoy = ColumnIndex(header, @"decoy", manifestIn);
                int iProteins = ColumnIndex(header, @"proteins", manifestIn);
                int iType = ColumnIndex(header, @"peptide_type", manifestIn);
                int iPair = ColumnIndex(header, @"peptide_pair_index", manifestIn);
                // Columns are found by name, so any of them could be the last field.
                int maxIndex = Math.Max(Math.Max(Math.Max(iSeq, iDecoy), Math.Max(iProteins, iType)), iPair);
                int lineNo = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    if (line.Length == 0)
                        continue;
                    string[] c = line.Split('\t');
                    if (c.Length <= maxIndex)
                    {
                        // Fail loudly rather than silently dropping a peptide or group.
                        throw new IOException(string.Format(CultureInfo.InvariantCulture,
                            @"malformed manifest {0} at line {1}: expected at least {2} tab-separated columns, found {3}",
                            manifestIn, lineNo, maxIndex + 1, c.Length));
                    }
                    result.RowsIn++;
                    if (!groupByIndex.TryGetValue(c[iPair], out var group))
                    {
                        group = new List<ManifestRow>();
                        groupByIndex.Add(c[iPair], group);
                        groups.Add(group);
                    }
                    group.Add(new ManifestRow(c[iSeq], c[iDecoy], c[iProteins], c[iType]));
                }
            }
            return groups;
        }

        /// <summary>
        /// The unique normalized peptides of a predicted library: the StrippedPeptide column of
        /// a DIA-NN TSV, or RefSpectra.peptideSeq of a .blib.
        /// </summary>
        private static HashSet<string> ReadLibrarySequences(string library)
        {
            if (library == null)
                throw new IOException(@"null predicted-library path");
            if (library.EndsWith(@".blib", StringComparison.OrdinalIgnoreCase))
                return ReadBlibSequences(library);
            var sequences = new HashSet<string>();
            using (var reader = new StreamReader(library, UTF8_NO_BOM, false))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new IOException(@"empty library: " + library);
                int iSeq = ColumnIndex(headerLine.Split('\t'), LIBRARY_SEQUENCE_COLUMN, library);
                int lineNo = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    if (line.Length == 0)
                        continue;
                    string[] c = line.Split('\t');
                    if (c.Length <= iSeq)
                    {
                        // A short row would undercount the library and over-prune the manifest.
                        throw new IOException(string.Format(CultureInfo.InvariantCulture,
                            @"malformed library {0} at line {1}: missing the {2} column (found {3} tab-separated columns)",
                            library, lineNo, LIBRARY_SEQUENCE_COLUMN, c.Length));
                    }
                    sequences.Add(Normalize(c[iSeq]));
                }
            }
            return sequences;
        }

        private static HashSet<string> ReadBlibSequences(string blib)
        {
            var sequences = new HashSet<string>();
            try
            {
                var builder = new SQLiteConnectionStringBuilder { DataSource = blib, ReadOnly = true };
                using (var connection = new SQLiteConnection(builder.ToString()))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(@"SELECT peptideSeq FROM RefSpectra", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sequence = reader.IsDBNull(0) ? null : reader.GetString(0);
                            if (!string.IsNullOrEmpty(sequence))
                                sequences.Add(Normalize(sequence));
                        }
                    }
                }
            }
            catch (SQLiteException e)
            {
                throw new IOException(@"failed to read peptides from blib: " + blib, e);
            }
            return sequences;
        }

        private static int ColumnIndex(string[] header, string name, string file)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(JavaText.Trim(header[i]), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            throw new IOException(@"column '" + name + @"' not found in " + file);
        }

        /// <summary>One manifest row's raw column values.</summary>
        private sealed class ManifestRow
        {
            public ManifestRow(string sequence, string decoy, string proteins, string peptideType)
            {
                Sequence = sequence;
                Decoy = decoy;
                Proteins = proteins;
                PeptideType = peptideType;
            }

            public string Sequence { get; }
            public string Decoy { get; }
            public string Proteins { get; }
            public string PeptideType { get; }
        }
    }
}
