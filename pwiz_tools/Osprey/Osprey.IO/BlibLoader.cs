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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.BiblioSpec.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads spectral libraries from BiblioSpec blib format through <see cref="BlibReader"/>,
    /// which reads the file; this class makes Osprey's library entries of its records: the
    /// modifications parsed from the modified sequence, the peaks typed from m/z, and the
    /// intensities scaled to the base peak. Ported from osprey-io/src/library/blib.rs.
    /// </summary>
    public class BlibLoader
    {

        /// <summary>
        /// Version of what this reader makes of a blib. 3: the residue masses the typing computes
        /// from are the shared chemistry's (<c>pwiz.Common.Chemistry</c>) rather than a
        /// six-decimal table. 2: every peak is typed from m/z (<see cref="FragmentTyping"/>),
        /// where version 1 left every peak Unknown, and modification text is read residue- and
        /// precision-aware (<see cref="IdentifyModification"/>). Every blib reads differently
        /// under it, so the <c>.libcache</c> composition term and the task-key term both carry
        /// it for every blib, and a change to either moves both by editing this one value.
        /// </summary>
        public const string READER_VERSION = @"3";

        private readonly FragmentToleranceConfig _fragmentTolerance;
        // The whole of the last load's row and protein work, and the part of it spent making
        // entries of the rows; the rest is the reader's.
        private readonly Stopwatch _total = new Stopwatch();
        private readonly Stopwatch _type = new Stopwatch();

        /// <summary>
        /// A reader that types each spectrum's peaks within <paramref name="fragmentTolerance"/>,
        /// the search's fragment tolerance, so a peak it calls y6 is one chromatogram extraction
        /// would count as y6.
        /// </summary>
        public BlibLoader(FragmentToleranceConfig fragmentTolerance)
        {
            _fragmentTolerance = fragmentTolerance;
        }

        /// <summary>Seconds of the last <see cref="Load"/> spent in <see cref="BlibReader"/>: reading the rows and decoding the peak blobs.</summary>
        public double ReadSeconds
        {
            get { return (_total.Elapsed - _type.Elapsed).TotalSeconds; }
        }

        /// <summary>Seconds of the last <see cref="Load"/> spent making entries of the rows: parsing modifications, typing and interning.</summary>
        public double TypeSeconds
        {
            get { return _type.Elapsed.TotalSeconds; }
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
            _total.Reset();
            _type.Reset();
            using (var reader = new BlibReader(path))
            {
                if (!reader.HasTable(BlibReader.TABLE_REF_SPECTRA))
                {
                    throw new InvalidOperationException(string.Format(
                        OspreyIOResources.BlibLoader_Load_Invalid_BiblioSpec_library__the_RefSpectra_table_was_not_found_,
                        BlibReader.TABLE_REF_SPECTRA));
                }

                // Intern the repeated strings (sequences, modification names,
                // protein accessions) as the interned arrays are filled, so no
                // member is mutated after assignment. One pool spans both the
                // spectra and the protein-mapping pass; only object identity
                // changes, so output is unchanged.
                var interner = new LibraryStringInterner();
                var typingStats = new FragmentTypingStats();
                var entries = LoadSpectra(reader, interner, typingStats);
                LoadProteinMappings(reader, entries, interner);
                interner.LogSummary(logInfo);
                logInfo?.Invoke(string.Format(
                    OspreyIOResources.BlibLoader_Load_Typed__0_N0__of__1_N0__library_peaks_as_b_or_y_ions_within__2___3_,
                    typingStats.Typed, typingStats.Total, _fragmentTolerance.Tolerance,
                    _fragmentTolerance.Unit.GetLocalizedString()));
                return entries;
            }
        }

        #region Private helpers

        /// <summary>
        /// Reads every spectrum with its peaks, typing them as they are read. A library with any
        /// modification that cannot be identified is refused, listing every such modification.
        /// The time goes into two buckets, <see cref="ReadSeconds"/> for the reader's row and
        /// blob work and <see cref="TypeSeconds"/> for everything made of the row, so a slow
        /// load says which side it is on.
        /// </summary>
        private List<LibraryEntry> LoadSpectra(BlibReader reader, LibraryStringInterner interner,
            FragmentTypingStats typingStats)
        {
            var entries = new List<LibraryEntry>();
            var unidentifiedMods = new UnidentifiedModifications();

            _total.Start();
            foreach (var spectrum in reader.ReadSpectra(true))
            {
                // A row without a sequence (a small molecule) is refused as a peptide too
                // short to search; one without a modified sequence is the unmodified peptide.
                string peptideSeq = spectrum.PeptideSeq ?? string.Empty;
                string peptideModSeq = spectrum.PeptideModSeq ?? peptideSeq;

                _type.Start();
                // The same bound the TSV loader enforces. An invariant only one
                // format checks is not an invariant, and DecoyGenerator's
                // fragment-overlap gate relies on this one whatever the library
                // was built from.
                LibraryValidation.ValidatePeptideLength(peptideSeq);

                var parsed = ParseBlibModifications(peptideModSeq, out string unidentified);
                if (parsed == null)
                {
                    unidentifiedMods.Add(unidentified, spectrum.Id, peptideModSeq);
                    _type.Stop();
                    continue;
                }
                var modifications = BuildInternedModifications(parsed, interner);
                _type.Stop();

                // A spectrum without a peaks row has no fragments.
                var peaks = BlibReader.DecodePeaks(spectrum);

                _type.Start();
                var fragments = peaks != null ? ToFragments(peaks) : Array.Empty<LibraryFragment>();
                FragmentTyping.TypeFragments(peptideSeq, modifications, spectrum.PrecursorCharge, fragments,
                    _fragmentTolerance, typingStats);

                var entry = new LibraryEntry((uint)spectrum.Id,
                    interner.Intern(peptideSeq), interner.Intern(peptideModSeq),
                    (byte)spectrum.PrecursorCharge, spectrum.PrecursorMz, spectrum.RetentionTime ?? 0.0);
                entry.Modifications = modifications;
                entry.Fragments = fragments;

                entries.Add(entry);
                _type.Stop();
            }
            _total.Stop();

            if (unidentifiedMods.Any)
                throw unidentifiedMods.ToException();
            return entries;
        }

        private void LoadProteinMappings(BlibReader reader, List<LibraryEntry> entries,
            LibraryStringInterner interner)
        {
            _total.Start();
            var proteins = reader.ReadProteins();
            _total.Stop();
            foreach (var entry in entries)
            {
                // The lookup is empty for a spectrum without a protein, which keeps its default.
                var accessions = proteins[entry.Id].Select(interner.Intern).ToArray();
                if (accessions.Length > 0)
                    entry.ProteinIds = accessions;
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
            if (residue == 'C' && !isSigned && mass > 100.0 && mass < 200.0 &&
                PeptideFragmentMass.TryGetResidueMass('C', out double cysteine))
            {
                delta = mass - cysteine;
            }
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
        /// A spectrum's decoded peaks as library fragments, untyped (every one Unknown until
        /// <see cref="FragmentTyping"/> types it) and with the intensities scaled to the base
        /// peak, as a DIA-NN library already has them.
        /// </summary>
        internal static LibraryFragment[] ToFragments(BlibPeaks peaks)
        {
            var fragments = new LibraryFragment[peaks.Count];
            float maxIntensity = 0f;
            for (int i = 0; i < fragments.Length; i++)
            {
                float intensity = peaks.Intensities[i];
                if (intensity > maxIntensity)
                    maxIntensity = intensity;
                fragments[i] = new LibraryFragment
                {
                    Mz = peaks.Mzs[i],
                    RelativeIntensity = intensity,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.Unknown,
                        Ordinal = (byte)(i + 1),
                        Charge = 1
                    }
                };
            }

            if (maxIntensity > 0f)
            {
                for (int i = 0; i < fragments.Length; i++)
                    fragments[i].RelativeIntensity /= maxIntensity;
            }

            return fragments;
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
