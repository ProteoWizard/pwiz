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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads spectral libraries from DIA-NN TSV format.
    /// Ported from osprey-io/src/library/diann.rs.
    /// </summary>
    public class DiannTsvLoader
    {
        /// <summary>
        /// The reader's version, in the <c>.libcache</c> composition hash. 2: an invalid value in
        /// any column the library has (or a modification with no known mass) refuses the whole
        /// library, where version 1 read it as a default or dropped it.
        /// </summary>
        public const int READER_VERSION = 2;

        private const int DEFAULT_MIN_FRAGMENTS = 3;
        private const string UNIMOD_PREFIX = @"UniMod:";

        private readonly int _minFragments;

        public DiannTsvLoader() : this(DEFAULT_MIN_FRAGMENTS)
        {
        }

        public DiannTsvLoader(int minFragments)
        {
            _minFragments = minFragments;
        }

        /// <summary>
        /// Load library entries from a DIA-NN TSV file.
        /// </summary>
        public List<LibraryEntry> Load(string path, Action<string> logInfo = null)
        {
            // Report progress over the file's BYTES rather than its rows: a 13 GB entrapment
            // TSV otherwise runs for over a minute with nothing on the console between
            // LibraryLoader's "Loading spectral library from ..." and the interning summary.
            // Byte progress needs the stream, so it is wired here rather than in ParseReader,
            // which stays a plain TextReader entry point for tests.
            // bufferSize 1 disables FileStream's own buffering: StreamReader below asks in 1 MB
            // blocks, so a second buffer underneath would copy every byte twice (and a 16 MB one
            // would sit on the LOH for the whole parse, including for the ~200-byte TSVs the unit
            // tests load). The reader's buffer is what must be large - at the BCL default of 1 KB
            // this issues a ProgressStream.Read, and therefore a locking Report, once per KB:
            // ~13.6M calls on the 13 GB entrapment library to print about a dozen lines.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 1))
            {
                // NOT a using: this reporter measures the READ, which finishes partway through
                // ParseReader. Disposing it with the stack would force its 100% out after phase 2
                // and after the interning summary - a completion line for a phase that ended
                // minutes earlier. ParseReader disposes it when the stream is exhausted.
                var readProgress = new ProgressReporter(
                    string.Format(OspreyIOResources.DiannTsvLoader_Load_Parsing__0_, Path.GetFileName(path)), stream.Length,
                    string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);
                using (var progressStream = new ProgressStream(stream, readProgress))
                // leaveOpen so ownership of progressStream is explicit rather than resting on
                // ProgressStream declining to override Dispose (it does not own the inner stream).
                using (var reader = new StreamReader(progressStream, Encoding.UTF8, true, 1 << 20, true))
                {
                    return ParseReader(reader, logInfo, readProgress);
                }
            }
        }

        /// <summary>
        /// Parse library entries from a text reader (for testability).
        /// </summary>
        public List<LibraryEntry> ParseReader(TextReader reader, Action<string> logInfo = null)
        {
            return ParseReader(reader, logInfo, null);
        }

        /// <summary>
        /// As <see cref="ParseReader(TextReader,Action{string})"/>, but disposes
        /// <paramref name="readProgress"/> when the READ finishes rather than when the caller's
        /// scope ends - the row loop below is only the first half of this method.
        /// </summary>
        private List<LibraryEntry> ParseReader(TextReader reader, Action<string> logInfo,
            IDisposable readProgress)
        {
            string headerLine = reader.ReadLine();
            if (headerLine == null)
                throw new InvalidDataException(OspreyIOResources.DiannTsvLoader_ParseReader_The_library_file_is_empty__it_has_no_header_row_);

            string[] headers = headerLine.Split(TextUtil.SEPARATOR_TSV);
            var cols = ColumnIndices.FromHeaders(headers);

            var precursorMap = new Dictionary<string, PrecursorData>();
            var errors = new LibraryLineErrors();

            // 1-based line numbers of the file, the header being line 1, so the first data line
            // a message names is line 2 - the number a text editor or a spreadsheet shows.
            // Blank lines are counted before they are skipped, for the same reason.
            string line;
            int lineNum = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] fields = line.Split(TextUtil.SEPARATOR_TSV);
                ParseRow(fields, cols, lineNum, precursorMap, errors);
            }
            // Stream exhausted: the byte progress is complete and must say so HERE.
            readProgress?.Dispose();

            // A library with any invalid line is refused whole, after every line has been read, so
            // one run names every line to fix. Searching with a value guessed for a bad cell
            // would produce results that look valid and are not.
            if (errors.Any)
                throw errors.ToException();

            // Convert to LibraryEntry list. Intern the repeated strings
            // (sequences, modification names, protein / gene accessions) as the
            // interned arrays are filled, so no member is mutated after
            // assignment. One pool per load call; only object identity changes,
            // so output is unchanged.
            var entries = new List<LibraryEntry>(precursorMap.Count);
            var interner = new LibraryStringInterner();
            uint id = 0;

            // Phase 2. The byte progress above ends when the stream is exhausted, so without this
            // the console sat at 100% through the whole materialization pass. Constructed rather
            // than `using`d so the loop needs no re-indent and so an exception here does not print
            // a completed-looking 100% while unwinding.
            var progress = new ProgressReporter(OspreyIOResources.DiannTsvLoader_ParseReader_Building_library_precursors, precursorMap.Count,
                    string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);
            long nBuilt = 0;

            foreach (var data in precursorMap.Values)
            {
                progress.Report(++nBuilt);
                if (data.Fragments.Count < _minFragments)
                    continue;

                // Checked here, after the min-fragment filter, so a row that is not going
                // into the library at all cannot fail the run.
                LibraryValidation.ValidatePeptideLength(data.Sequence);

                var modifications = BuildInternedModifications(data.Modifications, interner);

                var entry = new LibraryEntry(id,
                    interner.Intern(data.Sequence),
                    interner.Intern(data.ModifiedSequence),
                    data.Charge, data.PrecursorMz, data.RetentionTime);
                entry.Modifications = modifications;
                entry.Fragments = data.Fragments.ToArray();
                entry.ProteinIds = interner.InternToArray(data.ProteinIds);
                entry.GeneNames = interner.InternToArray(data.GeneNames);
                entry.IsDecoy = data.IsDecoy;

                entries.Add(entry);
                id++;
            }

            progress.Dispose();
            interner.LogSummary(logInfo);
            return entries;
        }

        /// <summary>
        /// Intern each modification's <see cref="Modification.Name"/> and return
        /// the mods as an array (empty -> shared empty array). Values other than
        /// string identity are unchanged.
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
        /// Parse one fragment line into <paramref name="precursorMap"/>. Nothing is assumed for a
        /// value the line states badly: every column the library HAS must parse, and each
        /// failure is added to <paramref name="errors"/> - all of them, from every line - so the
        /// load can refuse the library once, naming every line to fix. A column the library does
        /// not have takes its format's convention (no fragment charge column: charge 1).
        /// </summary>
        private void ParseRow(string[] fields, ColumnIndices cols, int lineNum,
            Dictionary<string, PrecursorData> precursorMap, LibraryLineErrors errors)
        {
            var fieldReader = new LineReader(fields, lineNum, errors);
            double precursorMz = fieldReader.Double(cols.PrecursorMz, @"PrecursorMz");
            byte charge = fieldReader.PositiveByte(cols.PrecursorCharge, @"PrecursorCharge");
            string modifiedPeptide = fieldReader.Text(cols.ModifiedPeptide, @"ModifiedPeptide");
            double fragmentMz = fieldReader.Double(cols.FragmentMz, @"FragmentMz");
            float relativeIntensity = fieldReader.Float(cols.RelativeIntensity, @"RelativeIntensity");

            // Retention time from multiple possible columns
            double retentionTime = 0.0;
            if (cols.IRT >= 0)
                retentionTime = fieldReader.Double(cols.IRT, @"iRT");
            else if (cols.NormalizedRT >= 0)
                retentionTime = fieldReader.Double(cols.NormalizedRT, @"NormalizedRetentionTime");

            // Fragment annotation
            IonType ionType = IonType.Unknown;
            if (cols.FragmentType >= 0)
                ionType = fieldReader.FragmentIonType(cols.FragmentType, @"FragmentType");

            byte ordinal = 0;
            if (cols.FragmentSeriesNumber >= 0)
                ordinal = fieldReader.PositiveByte(cols.FragmentSeriesNumber, @"FragmentSeriesNumber");

            byte fragmentCharge = 1;
            if (cols.FragmentCharge >= 0)
                fragmentCharge = fieldReader.PositiveByte(cols.FragmentCharge, @"FragmentCharge");

            NeutralLossCode lossCode = NeutralLossCode.None;
            double lossMass = 0.0;
            if (cols.FragmentLossType >= 0)
                (lossCode, lossMass) = fieldReader.Loss(cols.FragmentLossType, @"FragmentLossType");

            // Optional Decoy column. A missing column means target; a cell it cannot read is an
            // error like any other, never a guess.
            bool isDecoyRow = false;
            if (cols.Decoy >= 0)
                isDecoyRow = fieldReader.DecoyFlag(cols.Decoy, @"Decoy");

            if (!fieldReader.IsValid)
                return;

            string modifiedSequence = StripFlankingChars(modifiedPeptide);

            // Stripped sequence
            string strippedSequence = GetFieldOrNull(fields, cols.StrippedPeptide);
            if (string.IsNullOrEmpty(strippedSequence))
                strippedSequence = StripModifications(modifiedSequence);

            var annotation = new FragmentAnnotation
            {
                IonType = ionType,
                Ordinal = ordinal,
                Charge = fragmentCharge,
                NeutralLoss = lossCode,
                CustomLossMass = lossMass
            };

            // Protein and gene info
            List<string> proteinIds = new List<string>();
            string proteinStr = GetFieldOrNull(fields, cols.ProteinId);
            if (!string.IsNullOrEmpty(proteinStr))
                proteinIds = SplitList(proteinStr);

            List<string> geneNames = new List<string>();
            string geneStr = GetFieldOrNull(fields, cols.GeneName);
            if (!string.IsNullOrEmpty(geneStr))
                geneNames = SplitList(geneStr);

            // Group by precursor key
            string key = modifiedSequence + @"_" + charge;

            PrecursorData precursor;
            if (!precursorMap.TryGetValue(key, out precursor))
            {
                // Modifications are read once per precursor, on its first line, so a modification
                // whose mass cannot be known is reported once rather than on every fragment line.
                var unrecognized = new List<string>();
                var modifications = ParseModifications(modifiedSequence, unrecognized);
                foreach (string modStr in unrecognized)
                {
                    errors.Add(lineNum, InvalidValueMessage(lineNum, cols.ModifiedPeptide,
                        @"ModifiedPeptide", modStr));
                }
                precursor = new PrecursorData
                {
                    Sequence = strippedSequence,
                    ModifiedSequence = modifiedSequence,
                    Modifications = modifications,
                    Charge = charge,
                    PrecursorMz = precursorMz,
                    RetentionTime = retentionTime,
                    ProteinIds = proteinIds,
                    GeneNames = geneNames,
                    Fragments = new List<LibraryFragment>(),
                    IsDecoy = isDecoyRow,
                };
                precursorMap[key] = precursor;
            }
            else if (isDecoyRow)
            {
                // Defensive OR -- any row of a precursor flagging decoy
                // promotes the precursor.
                precursor.IsDecoy = true;
            }

            precursor.Fragments.Add(new LibraryFragment
            {
                Mz = fragmentMz,
                RelativeIntensity = relativeIntensity,
                Annotation = annotation
            });
        }

        /// <summary>
        /// Parse modifications from a modified peptide sequence.
        /// Handles bracket notation e.g. "PEPTM[+15.9949]IDE" and
        /// parenthetical notation e.g. "M(UniMod:35)PEPTIDE".
        /// </summary>
        public static List<Modification> ParseModifications(string modified)
        {
            return ParseModifications(modified, null);
        }

        /// <summary>
        /// As <see cref="ParseModifications(string)"/>, and every modification whose mass cannot
        /// be determined is added to <paramref name="unrecognized"/> (when not null) - the load
        /// refuses a library holding one, because dropping it would leave the peptide the wrong
        /// mass on every ion that spans it.
        /// </summary>
        internal static List<Modification> ParseModifications(string modified, List<string> unrecognized)
        {
            var modifications = new List<Modification>();
            int position = 0;
            int i = 0;

            while (i < modified.Length)
            {
                char c = modified[i];

                if (char.IsLetter(c))
                {
                    i++;
                    // Check for bracket modification after this residue
                    if (i < modified.Length && modified[i] == '[')
                    {
                        string modStr = ReadEnclosed(modified, ref i, ']');
                        AddModification(modifications, unrecognized, modStr, position);
                    }
                    position++;
                }
                else if (c == '(')
                {
                    string modStr = ReadEnclosed(modified, ref i, ')');
                    AddModification(modifications, unrecognized, modStr, Math.Max(0, position - 1));
                }
                else if (c == '[')
                {
                    // N-terminal bracket modification before any residue
                    string modStr = ReadEnclosed(modified, ref i, ']');
                    AddModification(modifications, unrecognized, modStr, 0);
                }
                else
                {
                    i++;
                }
            }

            return modifications;
        }

        /// <summary>
        /// The text between the opening character at <paramref name="i"/> and
        /// <paramref name="close"/>, leaving <paramref name="i"/> past the closing character (or
        /// at the end of an unclosed one).
        /// </summary>
        private static string ReadEnclosed(string modified, ref int i, char close)
        {
            i++; // consume the opening character
            int start = i;
            while (i < modified.Length && modified[i] != close)
                i++;
            string enclosed = modified.Substring(start, i - start);
            if (i < modified.Length)
                i++; // consume the closing character
            return enclosed;
        }

        private static void AddModification(List<Modification> modifications, List<string> unrecognized,
            string modStr, int position)
        {
            double? mass = ParseModMass(modStr);
            if (!mass.HasValue)
            {
                unrecognized?.Add(modStr);
                return;
            }
            modifications.Add(new Modification
            {
                Position = position,
                UnimodId = ParseUnimodId(modStr),
                MassDelta = mass.Value,
                Name = modStr
            });
        }

        /// <summary>
        /// Strip modifications from a modified peptide sequence, returning bare amino acid letters.
        /// </summary>
        public static string StripModifications(string modified)
        {
            var result = new StringBuilder(modified.Length);
            bool inBracket = false;
            bool inParen = false;

            foreach (char c in modified)
            {
                switch (c)
                {
                    case '[':
                        inBracket = true;
                        break;
                    case ']':
                        inBracket = false;
                        break;
                    case '(':
                        inParen = true;
                        break;
                    case ')':
                        inParen = false;
                        break;
                    default:
                        if (!inBracket && !inParen && char.IsLetter(c))
                            result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Parse mass value from a modification string.
        /// Handles numeric values, +/- prefixed values, UniMod notation, and named modifications.
        /// </summary>
        public static double? ParseModMass(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            // Try parsing as a number directly
            double mass;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                return mass;

            // Handle +/- prefix
            if (s.Length > 1 && (s[0] == '+' || s[0] == '-'))
            {
                if (double.TryParse(s.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                    return s[0] == '-' ? -mass : mass;
            }

            // Try UniMod notation (e.g. "UniMod:4" or "UNIMOD:4" - the prefix match ignores case)
            if (s.StartsWith(UNIMOD_PREFIX, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(s.Substring(UNIMOD_PREFIX.Length), out int unimodId))
            {
                double? unimodMass = UnimodIdToMass(unimodId);
                if (unimodMass.HasValue)
                    return unimodMass;
            }

            // Known modifications by name
            switch (s.ToUpperInvariant())
            {
                case @"OXIDATION": return 15.9949;
                case @"CARBAMIDOMETHYL":
                case @"CAM": return 57.0215;
                case @"PHOSPHO": return 79.9663;
                case @"ACETYL": return 42.0106;
                case @"DEAMIDATED":
                case @"DEAMIDATION": return 0.9840;
                default: return null;
            }
        }

        /// <summary>
        /// Parse UniMod ID from a modification string, if present.
        /// </summary>
        public static int? ParseUnimodId(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            int idx = s.IndexOf(UNIMOD_PREFIX, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            string rest = s.Substring(idx + UNIMOD_PREFIX.Length);
            int end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
                end++;

            if (end == 0)
                return null;

            int id;
            if (int.TryParse(rest.Substring(0, end), out id))
                return id;

            return null;
        }

        /// <summary>
        /// Look up mass delta for a UniMod ID.
        /// </summary>
        public static double? UnimodIdToMass(int id)
        {
            switch (id)
            {
                case 1: return 42.010565;    // Acetyl
                case 4: return 57.021464;    // Carbamidomethyl
                case 5: return 43.005814;    // Carbamyl
                case 7: return 0.984016;     // Deamidated
                case 21: return 79.966331;   // Phospho
                case 28: return -18.010565;  // Glu->pyro-Glu
                case 34: return 14.015650;   // Methyl
                case 35: return 15.994915;   // Oxidation
                case 36: return 28.031300;   // Dimethyl
                case 37: return 42.046950;   // Trimethyl
                case 121: return 114.042927; // Ubiquitin (GlyGly)
                case 122: return 383.228102; // SUMO
                case 214: return 44.985078;  // Nitro
                case 312: return -17.026549; // Ammonia loss
                case 385: return 229.162932; // TMT6plex
                case 737: return 229.162932; // TMT6plex (alternate)
                case 747: return 304.207146; // TMTpro
                default: return null;
            }
        }

        /// <summary>
        /// Split a semicolon or comma separated list, trimming whitespace.
        /// </summary>
        public static List<string> SplitList(string s)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s))
                return result;

            string[] parts = s.Split(new[] { ';', ',' }, StringSplitOptions.None);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
            return result;
        }

        /// <summary>
        /// Strip flanking characters from peptide sequences.
        /// Handles "_PEPTIDE_", "K.PEPTIDE.R", "-PEPTIDE-" formats.
        /// </summary>
        internal static string StripFlankingChars(string seq)
        {
            string trimmed = seq.Trim('_', '.', '-');

            // Handle internal patterns like "K.PEPTIDE.R" -> "PEPTIDE"
            int firstDot = trimmed.IndexOf('.');
            int lastDot = trimmed.LastIndexOf('.');
            if (firstDot >= 0 && lastDot > firstDot)
                return trimmed.Substring(firstDot + 1, lastDot - firstDot - 1);

            return trimmed;
        }

        #region Private helpers

        private static string GetFieldOrNull(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
                return null;
            return fields[index];
        }

        /// <summary>
        /// The message for an invalid <paramref name="value"/> in column <paramref name="name"/>,
        /// located as Skyline's transition list import locates one: the 1-based line of the
        /// file and the 1-based column.
        /// </summary>
        private static string InvalidValueMessage(int lineNum, int columnIndex, string name, string value)
        {
            return string.Format(OspreyIOResources.DiannTsvLoader_LineReader___line__0___column__1___Invalid__2____3__,
                lineNum, columnIndex + 1, name, value);
        }

        #endregion

        /// <summary>
        /// Reads the columns of one line, adding a message to the load's
        /// <see cref="LibraryLineErrors"/> for each value that is missing or cannot be read, and
        /// returning a placeholder for it that <see cref="ParseRow"/> discards once
        /// <see cref="IsValid"/> is false. Every column is read, so a line with several bad
        /// values reports all of them.
        /// </summary>
        private class LineReader
        {
            private readonly string[] _fields;
            private readonly int _lineNum;
            private readonly LibraryLineErrors _errors;

            public LineReader(string[] fields, int lineNum, LibraryLineErrors errors)
            {
                _fields = fields;
                _lineNum = lineNum;
                _errors = errors;
                IsValid = true;
            }

            /// <summary>False once any column of this line failed to read.</summary>
            public bool IsValid { get; private set; }

            /// <summary>The non-empty text of column <paramref name="index"/>, else null and an error.</summary>
            public string Text(int index, string name)
            {
                string s = GetFieldOrNull(_fields, index);
                if (string.IsNullOrEmpty(s))
                {
                    AddError(string.Format(OspreyIOResources.DiannTsvLoader_LineReader___line__0___column__1___Missing__2_,
                        _lineNum, index + 1, name));
                    return null;
                }
                return s;
            }

            public double Double(int index, string name)
            {
                // XmlConvert.ToDouble is IEEE-754 correct (XML schema spec requires
                // correct rounding) whereas .NET Framework 4.7.2's double.TryParse
                // can be off by a few ULPs on 16-digit scientific values. For TSV
                // library values like "400.1277954005887", .NET Framework parses
                // to a different f64 than the IEEE-correct round-to-nearest-even,
                // producing cross-impl drift in mz_min/mz_max and downstream bin
                // widths. Rust's `str::parse::<f64>` is IEEE-correct, so using
                // XmlConvert brings the two parsers into bit-for-bit agreement.
                return Parse(index, name, XmlConvert.ToDouble);
            }

            public float Float(int index, string name)
            {
                // XmlConvert for IEEE-754 correct parsing - see Double.
                return Parse(index, name, XmlConvert.ToSingle);
            }

            /// <summary>A count from 1 to 255 - a charge or an ion ordinal; 0 is never valid.</summary>
            public byte PositiveByte(int index, string name)
            {
                string s = Text(index, name);
                if (s == null)
                    return 0;
                if (!byte.TryParse(s, out byte value) || value == 0)
                    AddInvalid(index, name, s);
                return value;
            }

            /// <summary>One of the ion-type letters b, y, a, c, x or z.</summary>
            public IonType FragmentIonType(int index, string name)
            {
                string s = Text(index, name);
                if (s == null)
                    return IonType.Unknown;
                var ionType = s.Length == 1 ? IonTypeExtensions.FromChar(s[0]) : IonType.Unknown;
                if (ionType == IonType.Unknown)
                    AddInvalid(index, name, s);
                return ionType;
            }

            /// <summary>"noloss", a named loss, or a loss mass.</summary>
            public (NeutralLossCode Code, double CustomMass) Loss(int index, string name)
            {
                string s = Text(index, name);
                if (s == null)
                    return (NeutralLossCode.None, 0.0);
                var loss = NeutralLoss.Parse(s);
                if (!loss.HasValue)
                {
                    AddInvalid(index, name, s);
                    return (NeutralLossCode.None, 0.0);
                }
                return loss.Value;
            }

            /// <summary>A decoy flag <see cref="ParseDecoyFlag"/> recognizes.</summary>
            public bool DecoyFlag(int index, string name)
            {
                string s = Text(index, name);
                if (s == null)
                    return false;
                bool? flag = ParseDecoyFlag(s);
                if (!flag.HasValue)
                    AddInvalid(index, name, s);
                return flag ?? false;
            }

            /// <summary>
            /// <paramref name="parse"/> applied to column <paramref name="index"/>, with its
            /// format and overflow failures reported as an invalid value.
            /// </summary>
            private T Parse<T>(int index, string name, Func<string, T> parse)
            {
                string s = Text(index, name);
                if (s == null)
                    return default;
                try
                {
                    return parse(s);
                }
                catch (FormatException)
                {
                    AddInvalid(index, name, s);
                }
                catch (OverflowException)
                {
                    AddInvalid(index, name, s);
                }
                return default;
            }

            private void AddInvalid(int index, string name, string value)
            {
                AddError(InvalidValueMessage(_lineNum, index, name, value));
            }

            private void AddError(string message)
            {
                IsValid = false;
                _errors.Add(_lineNum, message);
            }
        }

        /// <summary>
        /// Every invalid value of one load. The load reads to the end before refusing the
        /// library, so one run reports every line to fix; the message lists the first
        /// <see cref="MAX_LISTED"/> errors and counts the rest, because a column that is wrong
        /// throughout a library would otherwise list every line of it.
        /// </summary>
        private class LibraryLineErrors
        {
            private const int MAX_LISTED = 100;

            private readonly List<string> _listed = new List<string>();
            private int _errorCount;
            private int _lineCount;
            private int _lastLine = -1;

            public bool Any => _errorCount > 0;

            public void Add(int lineNum, string message)
            {
                _errorCount++;
                if (lineNum != _lastLine)
                {
                    _lineCount++;
                    _lastLine = lineNum;
                }
                if (_listed.Count < MAX_LISTED)
                    _listed.Add(message);
            }

            public InvalidDataException ToException()
            {
                var sb = new StringBuilder(string.Format(
                    OspreyIOResources.DiannTsvLoader_ToException__0__library_lines_have_errors__Fix_the_library_and_load_it_again_,
                    _lineCount));
                foreach (string message in _listed)
                    sb.AppendLine().Append(@"  ").Append(message);
                if (_errorCount > _listed.Count)
                {
                    sb.AppendLine().Append(@"  ").Append(string.Format(
                        OspreyIOResources.DiannTsvLoader_ToException____and__0__more_errors, _errorCount - _listed.Count));
                }
                return new InvalidDataException(sb.ToString());
            }
        }

        /// <summary>
        /// Column index lookup for DIA-NN TSV headers.
        /// </summary>
        private class ColumnIndices
        {
            public int PrecursorMz = -1;
            public int PrecursorCharge = -1;
            public int ModifiedPeptide = -1;
            public int StrippedPeptide = -1;
            public int FragmentMz = -1;
            public int RelativeIntensity = -1;
            public int FragmentType = -1;
            public int FragmentSeriesNumber = -1;
            public int FragmentCharge = -1;
            public int FragmentLossType = -1;
            public int IRT = -1;
            public int NormalizedRT = -1;
            public int ProteinId = -1;
            public int GeneName = -1;
            // Optional Decoy column (DIA-NN convention: 0=target, 1=decoy).
            // When present, rows whose value is 1 / true / yes / y / t
            // (case-insensitive) are flagged at load time. Catches library
            // decoys that lack the decoy_ / rev_ / DECOY_ protein-accession
            // prefix and vice versa; some generators set only one signal.
            public int Decoy = -1;

            public static ColumnIndices FromHeaders(string[] headers)
            {
                var indices = new ColumnIndices();

                indices.PrecursorMz = FindColumn(headers, @"PrecursorMz", @"Precursor.Mz", @"Q1");
                indices.PrecursorCharge = FindColumn(headers, @"PrecursorCharge", @"Precursor.Charge");
                indices.ModifiedPeptide = FindColumn(headers, @"ModifiedPeptide", @"Modified.Peptide", @"FullPeptideName");
                indices.StrippedPeptide = FindColumn(headers, @"StrippedPeptide", @"Stripped.Peptide", @"PeptideSequence");
                indices.FragmentMz = FindColumn(headers, @"FragmentMz", @"Fragment.Mz", @"ProductMz", @"Q3");
                indices.RelativeIntensity = FindColumn(headers, @"RelativeIntensity", @"Relative.Intensity", @"LibraryIntensity");
                indices.FragmentType = FindColumn(headers, @"FragmentType", @"Fragment.Type", @"IonType");
                indices.FragmentSeriesNumber = FindColumn(headers, @"FragmentSeriesNumber", @"FragmentNumber", @"IonNumber");
                indices.FragmentCharge = FindColumn(headers, @"FragmentCharge", @"Fragment.Charge", @"ProductCharge");
                indices.FragmentLossType = FindColumn(headers, @"FragmentLossType", @"LossType", @"NeutralLoss");
                indices.IRT = FindColumn(headers, @"iRT", @"iRt");
                indices.NormalizedRT = FindColumn(headers, @"NormalizedRetentionTime", @"Tr_recalibrated", @"RT");
                indices.ProteinId = FindColumn(headers, @"ProteinId", @"Protein.Id", @"ProteinName", @"Protein", @"ProteinIds", @"Protein.Ids");
                indices.GeneName = FindColumn(headers, @"GeneName", @"Gene.Name", @"Genes", @"Protein.Names");
                indices.Decoy = FindColumn(headers, @"Decoy", @"IsDecoy", @"Is.Decoy");

                // Validate required columns, named by the first spelling each lookup accepts.
                RequireColumn(indices.PrecursorMz, @"PrecursorMz");
                RequireColumn(indices.PrecursorCharge, @"PrecursorCharge");
                RequireColumn(indices.ModifiedPeptide, @"ModifiedPeptide");
                RequireColumn(indices.FragmentMz, @"FragmentMz");
                RequireColumn(indices.RelativeIntensity, @"RelativeIntensity");

                return indices;
            }

            private static void RequireColumn(int index, string columnName)
            {
                if (index < 0)
                    throw new InvalidDataException(string.Format(OspreyIOResources.ColumnIndices_RequireColumn_Missing_required_column___0_, columnName));
            }

            private static int FindColumn(string[] headers, params string[] names)
            {
                foreach (string name in names)
                {
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Intermediate data for grouping fragments by precursor.
        /// </summary>
        private class PrecursorData
        {
            public string Sequence;
            public string ModifiedSequence;
            public List<Modification> Modifications;
            public byte Charge;
            public double PrecursorMz;
            public double RetentionTime;
            public List<string> ProteinIds;
            public List<string> GeneNames;
            public List<LibraryFragment> Fragments;
            // True when any fragment row in this precursor had a truthy
            // Decoy column. DIA-NN writes the same Decoy value on every
            // row of a given precursor, so the OR is defensive.
            public bool IsDecoy;
        }

        /// <summary>
        /// Parse the optional Decoy column: <c>1</c>, <c>true</c>, <c>yes</c>, <c>y</c>,
        /// <c>t</c> are decoy and <c>0</c>, <c>false</c>, <c>no</c>, <c>n</c>, <c>f</c> are
        /// target (case-insensitive, ASCII-only, surrounding whitespace ignored). Anything else -
        /// empty included - is null, which the loader reports rather than reading as target:
        /// a row it cannot classify is a bad library, not a target. The truthy set matches Rust
        /// <c>parse_decoy_flag</c>, including its <c>to_ascii_lowercase</c> - non-ASCII input
        /// (Turkish dotted-I, fullwidth digits) passes through unchanged, so neither side
        /// produces a spurious match; Rust reads every other value as target.
        /// </summary>
        internal static bool? ParseDecoyFlag(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            string trimmed = s.Trim();
            if (trimmed.Length == 0)
                return null;
            string lower = AsciiLowerInvariant(trimmed);
            if (lower == @"1" || lower == @"true" || lower == @"yes" || lower == @"y" || lower == @"t")
                return true;
            if (lower == @"0" || lower == @"false" || lower == @"no" || lower == @"n" || lower == @"f")
                return false;
            return null;
        }

        /// <summary>
        /// Lowercase only the ASCII A-Z range; pass everything else
        /// (including non-ASCII Unicode) through unchanged. Mirrors
        /// Rust's <c>str::to_ascii_lowercase</c>. Avoids the divergence
        /// trap of C# <c>string.ToLowerInvariant()</c> which uses
        /// Unicode case-folding and can produce different bytes for
        /// non-ASCII input than Rust's byte-only mapping.
        /// </summary>
        private static string AsciiLowerInvariant(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 'A' && c <= 'Z')
                    sb.Append((char)(c + 32));
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
