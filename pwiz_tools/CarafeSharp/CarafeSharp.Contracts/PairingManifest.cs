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
using System.Globalization;
using System.IO;

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// The FDRBench pairing manifest: a tab-separated file with a header naming the five
    /// columns <c>sequence, decoy, proteins, peptide_type, peptide_pair_index</c>, one row per
    /// peptide. Each <c>peptide_pair_index</c> group holds up to four rows - target, p_target,
    /// decoy (of the target), p_decoy (of the p_target) - from which a reader recovers the two
    /// target-decoy pairs. CarafeSharp writes one when it builds an entrapment FASTA and
    /// rewrites one when it reconciles it to a predicted library; Osprey writes one for FDRBench
    /// and reads one to pair a library's decoys and classify its peptides. All of them write and
    /// read through this class, so the columns, the kind spellings and the decoy flag are
    /// defined once.
    ///
    /// <para>Reading has two strictnesses. Osprey's loader is lenient: a row it cannot use (too
    /// short, an unknown kind, a pair index that is not a non-negative integer) is skipped and
    /// counted. CarafeSharp's tools are strict, as Carafe's <c>parseManifest</c> is: such a row
    /// fails the read, naming the line. Both refuse an empty file and a header missing
    /// <c>sequence</c>, <c>peptide_type</c> or <c>peptide_pair_index</c>; <c>decoy</c> and
    /// <c>proteins</c> are optional, and a row ends no earlier than the last column the header
    /// names among the five.</para>
    /// </summary>
    public static class PairingManifest
    {
        public const string COLUMN_SEQUENCE = @"sequence";
        public const string COLUMN_DECOY = @"decoy";
        public const string COLUMN_PROTEINS = @"proteins";
        public const string COLUMN_PEPTIDE_TYPE = @"peptide_type";
        public const string COLUMN_PEPTIDE_PAIR_INDEX = @"peptide_pair_index";

        /// <summary>
        /// Optional sixth column: how an entrapment target was made from its original target -
        /// <see cref="ENTRAPMENT_METHOD_SHUFFLE"/> or <see cref="ENTRAPMENT_METHOD_FOREIGN"/> - on
        /// the <c>p_target</c> rows, empty on the rest. CarafeSharp's entrapment FASTA stage
        /// writes it and its library build copies it to <c>DecoyPairs.Method</c>, beside the
        /// <c>reverse</c> / <c>cycle</c> it computes for a decoy. Every reader finds columns by
        /// name, so a manifest without it reads as before.
        /// </summary>
        public const string COLUMN_ENTRAPMENT_METHOD = @"entrapment_method";
        public const string ENTRAPMENT_METHOD_SHUFFLE = @"shuffle";
        public const string ENTRAPMENT_METHOD_FOREIGN = @"foreign";

        /// <summary>The header line, the five columns in FDRBench's order.</summary>
        public const string HEADER = COLUMN_SEQUENCE + "\t" + COLUMN_DECOY + "\t" + COLUMN_PROTEINS + "\t" +
                                     COLUMN_PEPTIDE_TYPE + "\t" + COLUMN_PEPTIDE_PAIR_INDEX;

        /// <summary>The header line with <see cref="COLUMN_ENTRAPMENT_METHOD"/> after FDRBench's five.</summary>
        public const string HEADER_WITH_ENTRAPMENT_METHOD = HEADER + "\t" + COLUMN_ENTRAPMENT_METHOD;

        /// <summary>The <c>peptide_type</c> spellings; <see cref="PeptideKinds"/> maps them to <see cref="PeptideKind"/>.</summary>
        public const string KIND_TARGET = @"target";
        public const string KIND_DECOY = @"decoy";
        public const string KIND_P_TARGET = @"p_target";
        public const string KIND_P_DECOY = @"p_decoy";

        /// <summary>The <c>decoy</c> column's values; a reader accepts <c>yes</c> in any case.</summary>
        public const string DECOY_YES = @"Yes";
        public const string DECOY_NO = @"No";

        /// <summary>The <c>proteins</c> column lists accessions separated by this; empty or <c>-</c> lists none.</summary>
        public const char PROTEIN_SEPARATOR = ';';

        private const char SEPARATOR = '\t';
        private const string NO_PROTEINS = @"-";

        /// <summary>
        /// Writes the header line with the writer's own line ending: FDRBench's five columns,
        /// and <see cref="COLUMN_ENTRAPMENT_METHOD"/> after them when
        /// <paramref name="withEntrapmentMethod"/>.
        /// </summary>
        public static void WriteHeader(TextWriter writer, bool withEntrapmentMethod = false)
        {
            writer.WriteLine(withEntrapmentMethod ? HEADER_WITH_ENTRAPMENT_METHOD : HEADER);
        }

        /// <summary>
        /// Writes one row with the writer's own line ending. <paramref name="entrapmentMethod"/>
        /// is the sixth cell of a manifest whose header has the column (empty on a row that is
        /// not an entrapment target); null writes FDRBench's five.
        /// </summary>
        public static void WriteRow(TextWriter writer, string sequence, bool isDecoy, string proteins, PeptideKind kind, int pairIndex,
            string entrapmentMethod = null)
        {
            WriteRow(writer, sequence, isDecoy ? DECOY_YES : DECOY_NO, proteins, PeptideKinds.Name(kind), pairIndex, entrapmentMethod);
        }

        /// <summary>
        /// Writes one row from its column texts as given - how a reconciler copies rows of a
        /// manifest it read, whatever their spelling - with the writer's own line ending. A
        /// non-null <paramref name="entrapmentMethodText"/> is the sixth cell.
        /// </summary>
        public static void WriteRow(TextWriter writer, string sequence, string decoyText, string proteinsText, string kindText,
            int pairIndex, string entrapmentMethodText = null)
        {
            writer.Write(sequence);
            writer.Write(SEPARATOR);
            writer.Write(decoyText);
            writer.Write(SEPARATOR);
            writer.Write(proteinsText);
            writer.Write(SEPARATOR);
            writer.Write(kindText);
            writer.Write(SEPARATOR);
            writer.Write(pairIndex.ToString(CultureInfo.InvariantCulture));
            if (entrapmentMethodText != null)
            {
                writer.Write(SEPARATOR);
                writer.Write(entrapmentMethodText);
            }
            writer.WriteLine();
        }

        /// <summary>
        /// Reads a manifest's rows in file order. Columns are found by name in the header (any
        /// order, any case, trimmed); a byte order mark is stripped. With <paramref name="strict"/>
        /// a row that is too short or whose pair index is not an integer fails the read;
        /// without it such a row, or one with an unknown kind or a negative pair index, is
        /// skipped and counted in <paramref name="skippedRows"/>. Empty lines are ignored.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// The file is empty, the header lacks a required column, or (strict) a row is malformed.
        /// </exception>
        public static List<PairingManifestRow> Read(string path, bool strict, out int skippedRows)
        {
            var rows = new List<PairingManifestRow>();
            skippedRows = 0;
            using (var reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                if (header == null)
                    throw new InvalidDataException(ContractsResources.PairingManifest_Read_The_decoy_pairing_manifest_is_empty_);
                var columns = header.Split(SEPARATOR);
                int iSequence = IndexOf(columns, COLUMN_SEQUENCE);
                int iDecoy = IndexOf(columns, COLUMN_DECOY);
                int iProteins = IndexOf(columns, COLUMN_PROTEINS);
                int iKind = IndexOf(columns, COLUMN_PEPTIDE_TYPE);
                int iPair = IndexOf(columns, COLUMN_PEPTIDE_PAIR_INDEX);
                int iMethod = IndexOf(columns, COLUMN_ENTRAPMENT_METHOD);
                if (iSequence < 0 || iKind < 0 || iPair < 0)
                {
                    throw new InvalidDataException(string.Format(
                        ContractsResources.PairingManifest_Read_The_decoy_pairing_manifest_is_missing_required_columns__it_needs__1____2__and__3____Found___0_,
                        header, COLUMN_SEQUENCE, COLUMN_PEPTIDE_TYPE, COLUMN_PEPTIDE_PAIR_INDEX));
                }
                // A row must reach the last of the columns the header names, so a row truncated
                // short of its proteins is malformed rather than read as having none.
                int minColumns = Math.Max(Math.Max(Math.Max(iSequence, iKind), Math.Max(iPair, iDecoy)), Math.Max(iProteins, iMethod)) + 1;
                int lineNumber = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (line.Length == 0)
                        continue;
                    var fields = line.Split(SEPARATOR);
                    if (fields.Length < minColumns)
                    {
                        if (strict)
                        {
                            throw new InvalidDataException(string.Format(
                                ContractsResources.PairingManifest_Read_The_decoy_pairing_manifest__0__is_malformed_at_line__1___it_has__2__columns_where_at_least__3__are_needed_,
                                path, lineNumber, fields.Length, minColumns));
                        }
                        skippedRows++;
                        continue;
                    }
                    if (!int.TryParse(fields[iPair].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int pairIndex))
                    {
                        if (strict)
                        {
                            throw new InvalidDataException(string.Format(
                                ContractsResources.PairingManifest_Read_The_decoy_pairing_manifest__0__is_malformed_at_line__1___the_peptide_pair_index___2___is_not_an_integer_,
                                path, lineNumber, fields[iPair]));
                        }
                        skippedRows++;
                        continue;
                    }
                    string kindText = fields[iKind];
                    PeptideKind? kind = PeptideKinds.TryParse(kindText, out var parsedKind) ? parsedKind : null;
                    if (!strict && (kind == null || pairIndex < 0))
                    {
                        skippedRows++;
                        continue;
                    }
                    string decoyText = iDecoy >= 0 ? fields[iDecoy] : null;
                    string proteinsText = iProteins >= 0 ? fields[iProteins] : null;
                    string methodText = iMethod >= 0 ? fields[iMethod] : null;
                    rows.Add(new PairingManifestRow(fields[iSequence], decoyText, proteinsText, kindText, kind, pairIndex, methodText));
                }
            }
            return rows;
        }

        /// <summary>The accessions a <c>proteins</c> cell lists: none for an absent column, an empty cell or <c>-</c>.</summary>
        public static IReadOnlyList<string> ParseProteins(string proteinsText)
        {
            var proteins = new List<string>();
            string raw = proteinsText?.Trim();
            if (string.IsNullOrEmpty(raw) || raw == NO_PROTEINS)
                return proteins;
            foreach (string accession in raw.Split(PROTEIN_SEPARATOR))
            {
                string trimmed = accession.Trim();
                if (trimmed.Length > 0)
                    proteins.Add(trimmed);
            }
            return proteins;
        }

        private static int IndexOf(string[] columns, string name)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (string.Equals(columns[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// One row of a pairing manifest as <see cref="PairingManifest.Read"/> returns it: the
    /// column texts as they were in the file, beside what they mean.
    /// </summary>
    public sealed class PairingManifestRow
    {
        public PairingManifestRow(string sequence, string decoyText, string proteinsText, string kindText, PeptideKind? kind, int pairIndex,
            string entrapmentMethodText = null)
        {
            Sequence = sequence;
            DecoyText = decoyText;
            ProteinsText = proteinsText;
            KindText = kindText;
            Kind = kind;
            PairIndex = pairIndex;
            EntrapmentMethodText = entrapmentMethodText;
            string method = entrapmentMethodText?.Trim();
            EntrapmentMethod = string.IsNullOrEmpty(method) ? null : method;
            Proteins = PairingManifest.ParseProteins(proteinsText);
            IsDecoy = decoyText != null
                ? string.Equals(decoyText.Trim(), PairingManifest.DECOY_YES, StringComparison.OrdinalIgnoreCase)
                : kind.HasValue && PeptideKinds.IsDecoySide(kind.Value);
        }

        /// <summary>The <c>sequence</c> cell as written.</summary>
        public string Sequence { get; }

        /// <summary>The <c>decoy</c> cell as written, or null when the manifest has no such column.</summary>
        public string DecoyText { get; }

        /// <summary>The <c>decoy</c> cell says yes; without the column, the kind is a decoy kind.</summary>
        public bool IsDecoy { get; }

        /// <summary>The <c>proteins</c> cell as written, or null when the manifest has no such column.</summary>
        public string ProteinsText { get; }

        /// <summary>The accessions the <c>proteins</c> cell lists; empty when it lists none.</summary>
        public IReadOnlyList<string> Proteins { get; }

        /// <summary>The <c>peptide_type</c> cell as written.</summary>
        public string KindText { get; }

        /// <summary>The kind the cell names, or null when it is not one of the four (strict reads only).</summary>
        public PeptideKind? Kind { get; }

        public int PairIndex { get; }

        /// <summary>The <c>entrapment_method</c> cell as written, or null when the manifest has no such column.</summary>
        public string EntrapmentMethodText { get; }

        /// <summary>How this entrapment target was made (<c>shuffle</c>, <c>foreign</c>), or null on any other row or without the column.</summary>
        public string EntrapmentMethod { get; }
    }
}
