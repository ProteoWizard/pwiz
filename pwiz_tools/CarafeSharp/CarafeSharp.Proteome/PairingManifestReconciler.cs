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
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Contracts;
using pwiz.Common.SystemUtil;

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

            // Written under a temporary name, so a failed run leaves no manifest that looks complete.
            var output = new SimpleFileSaver(manifestOut);
            try
            {
                WriteReconciled(groups, librarySequences, output.SafeName, result);
                output.Commit();
            }
            finally
            {
                output.Dispose();
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

        private static void WriteReconciled(List<List<PairingManifestRow>> groups, HashSet<string> librarySequences, string path,
            PairingReconciliationResult result)
        {
            using (var writer = EntrapmentFastaBuilder.CreateWriter(path))
            {
                PairingManifest.WriteHeader(writer);
                int newPairIndex = 0;
                foreach (var group in groups)
                {
                    var target = group.Find(row => row.Kind == PeptideKind.Target);
                    if (target == null || !librarySequences.Contains(Normalize(target.Sequence)))
                    {
                        result.GroupsDropped++;
                        result.RowsDropped += group.Count;
                        continue;
                    }
                    bool decoyKept = false;
                    var keptRows = new List<PairingManifestRow>(group.Count);
                    foreach (var row in group)
                    {
                        if (librarySequences.Contains(Normalize(row.Sequence)))
                        {
                            keptRows.Add(row);
                            if (row.Kind == PeptideKind.Decoy)
                                decoyKept = true;
                        }
                        else
                        {
                            result.RowsDropped++;
                        }
                    }
                    foreach (var row in keptRows)
                    {
                        // The row's cells as they were read, under the new pair index.
                        PairingManifest.WriteRow(writer, row.Sequence,
                            row.DecoyText ?? (row.IsDecoy ? PairingManifest.DECOY_YES : PairingManifest.DECOY_NO),
                            row.ProteinsText ?? string.Join(PairingManifest.PROTEIN_SEPARATOR.ToString(), row.Proteins),
                            row.KindText, newPairIndex);
                        result.RowsKept++;
                    }
                    if (!decoyKept)
                        result.KeptTargetsWithoutDecoy++;
                    result.GroupsKept++;
                    newPairIndex++;
                }
            }
        }

        /// <summary>Java-trimmed, upper-cased and I-to-L normalized; null becomes empty.</summary>
        public static string Normalize(string sequence)
        {
            return sequence == null ? string.Empty : JavaText.ToUpper(JavaText.Trim(sequence)).Replace('I', 'L');
        }

        /// <summary>
        /// Manifest rows grouped by peptide_pair_index, groups in first-seen order. The strict
        /// read fails loudly on a malformed row rather than silently dropping a peptide or group.
        /// </summary>
        private static List<List<PairingManifestRow>> ReadManifestGroups(string manifestIn, PairingReconciliationResult result)
        {
            var groups = new List<List<PairingManifestRow>>();
            var groupByIndex = new Dictionary<int, List<PairingManifestRow>>();
            foreach (var row in PairingManifest.Read(manifestIn, true, out _))
            {
                result.RowsIn++;
                if (!groupByIndex.TryGetValue(row.PairIndex, out var group))
                {
                    group = new List<PairingManifestRow>();
                    groupByIndex.Add(row.PairIndex, group);
                    groups.Add(group);
                }
                group.Add(row);
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
                using (var reader = new BlibReader(blib))
                {
                    foreach (var spectrum in reader.ReadSpectra(false))
                    {
                        if (!string.IsNullOrEmpty(spectrum.PeptideSeq))
                            sequences.Add(Normalize(spectrum.PeptideSeq));
                    }
                }
            }
            catch (Exception e) when (e is SQLiteException || e is InvalidDataException)
            {
                // A file that is not a library, or one without its RefSpectra table.
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
    }
}
