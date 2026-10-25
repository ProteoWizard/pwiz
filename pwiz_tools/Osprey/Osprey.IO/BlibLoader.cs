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
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pwiz.BiblioSpec.IO;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads spectral libraries from BiblioSpec blib format through <see cref="BlibReader"/>,
    /// which reads the file; this class makes Osprey's library entries of its records: the
    /// modifications parsed from the modified sequence, the peaks typed from m/z, and the
    /// intensities scaled to the base peak. Ported from osprey-io/src/library/blib.rs.
    ///
    /// <para>The spectra are read in partitions by RefSpectra id (<see cref="BlibReader.ReadSpectra(bool,int,int)"/>),
    /// one reader on its own connection per thread, each thread decoding and typing its rows;
    /// the partitions are then merged in id order, so the entries, their ids and the interned
    /// strings are the same whatever the thread count. The .blib is the library's transfer
    /// format and its derived form, so its load has to be close to a binary cache read: a
    /// 242,841-precursor library read on one thread spent 2.4 of its 3.4 seconds typing peaks.</para>
    /// </summary>
    public class BlibLoader
    {

        /// <summary>
        /// Version of what this reader makes of a blib. 4: peaks in m/z order, one peak per m/z
        /// (<see cref="FragmentPeaks"/>); ids from 0 in reading order and the modified sequence
        /// rebuilt from the modification masses, as the TSV loader's are, so an entry reads the
        /// same from either file. 3: the residue masses the typing computes
        /// from are the shared chemistry's (<c>pwiz.Common.Chemistry</c>) rather than a
        /// six-decimal table. 2: every peak is typed from m/z (<see cref="FragmentTyping"/>),
        /// where version 1 left every peak Unknown, and modification text is read residue- and
        /// precision-aware (<see cref="IdentifyModification"/>). Every blib reads differently
        /// under it, so the library composition term (in the hash of a .tsv's derived .blib)
        /// and the task-key term both carry it for every blib, and a change to either moves
        /// both by editing this one value.
        /// </summary>
        public const string READER_VERSION = @"4";

        /// <summary>
        /// Spectra per partition below which fewer threads than asked for are used: a reader per
        /// thread is a connection and a scan of the id column each, which a small library does
        /// not repay.
        /// </summary>
        private const int MIN_SPECTRA_PER_PARTITION = 4000;

        /// <summary>
        /// The most partitions a load reads on, whatever the thread count. SQLite's row and blob
        /// fetch does not scale past a few connections - on the 1.57M-precursor Astral library
        /// the scan took 8.5s on 1 thread, 3.8s on 4, 2.8s on 8 and 4.4s on 16, with the time
        /// outside typing growing from 4.8s to 61s summed over the threads - while the typing
        /// on each thread's rows does scale. Eight is where the two cross on this hardware; the
        /// next step, if the load must get nearer the old binary cache's read, is a few reader
        /// threads feeding every core's typing rather than one thread doing both.
        /// </summary>
        private const int MAX_PARTITIONS = 8;

        private readonly FragmentToleranceConfig _fragmentTolerance;
        private readonly int _nThreads;
        // The last load's phases: the parallel scan (rows read, peaks decoded and typed), the
        // typing within it summed over the threads, the serial merge that interns and numbers
        // the entries, and the protein mapping.
        private readonly Stopwatch _scan = new Stopwatch();
        private long _typingTicks;
        private readonly Stopwatch _merge = new Stopwatch();
        private readonly Stopwatch _proteins = new Stopwatch();
        private int _partitionCount;
        // The RefSpectra id of each entry of the last load, in the entries' order.
        private readonly List<long> _refSpectraIds = new List<long>();
        // The RefSpectra ids whose peaks the current load decodes, when RetainFragmentsFor names
        // a set; null decodes every spectrum's.
        private HashSet<long> _retainedRefIds;

        /// <summary>
        /// A reader that types each spectrum's peaks within <paramref name="fragmentTolerance"/>,
        /// the search's fragment tolerance, so a peak it calls y6 is one chromatogram extraction
        /// would count as y6, reading on up to <paramref name="nThreads"/> threads (the
        /// processor count when 0 or less).
        /// </summary>
        public BlibLoader(FragmentToleranceConfig fragmentTolerance, int nThreads = 0)
        {
            _fragmentTolerance = fragmentTolerance;
            _nThreads = nThreads > 0 ? nThreads : Environment.ProcessorCount;
        }

        /// <summary>
        /// The RefSpectra id of each entry of the last <see cref="Load"/>, in the entries' order:
        /// what the library's other tables (proteins, decoy pairs) refer to, where
        /// <see cref="LibraryEntry.Id"/> runs from 0 as every loader's ids do.
        /// </summary>
        public IReadOnlyList<long> RefSpectraIds
        {
            get { return _refSpectraIds; }
        }

        /// <summary>
        /// When true, the scan reads no peak blob at all (<see cref="BlibReader.ReadSpectra(bool,int,int)"/>
        /// without peaks) and every entry is returned with a readable empty
        /// <see cref="LibraryEntry.Fragments"/>: the lean load of a FirstPassFDR /
        /// <c>StopAfterStage5</c> worker, whose FDR stages read only the scalars. Set it only
        /// for a FINISHED library (one Osprey derived from a .tsv): the peak-less check and the
        /// deduplicator's fragment-count tie-break have already run on what it holds, and it
        /// claims no contract to check.
        /// </summary>
        public bool OmitFragments { get; set; }

        /// <summary>
        /// Decode and type peaks ONLY for the spectra whose FINAL base id is in this set, and
        /// return every other entry with its spectrum RELEASED
        /// (<see cref="LibraryEntry.ReleaseSpectrum"/>), the state loading everything and then
        /// releasing the same set leaves (<c>LibraryLoadOptions.RetainFragmentsFor</c> states
        /// the contract). Null retains everything. Only meaningful for a FINISHED library Osprey
        /// derived from a .tsv, where the final id of a target is its 0-based index in RefSpectra
        /// id order and a decoy's base id is its target's (<c>DecoyPairs.TargetRefSpectraID</c>),
        /// which is how the set is resolved to RefSpectra ids before the scan. Ignored when
        /// <see cref="OmitFragments"/> is set.
        /// </summary>
        public HashSet<uint> RetainFragmentsFor { get; set; }

        /// <summary>Threads the last <see cref="Load"/> read on.</summary>
        public int PartitionCount
        {
            get { return _partitionCount; }
        }

        /// <summary>Seconds of the last <see cref="Load"/> in the parallel scan: rows read, peaks decoded and typed, on every thread at once.</summary>
        public double ScanSeconds
        {
            get { return _scan.Elapsed.TotalSeconds; }
        }

        /// <summary>Seconds of the last <see cref="Load"/> spent typing peaks, summed over the threads.</summary>
        public double TypingThreadSeconds
        {
            get { return TimeSpan.FromTicks(_typingTicks).TotalSeconds; }
        }

        /// <summary>Seconds of the last <see cref="Load"/> merging the partitions in id order and interning the strings.</summary>
        public double MergeSeconds
        {
            get { return _merge.Elapsed.TotalSeconds; }
        }

        /// <summary>Seconds of the last <see cref="Load"/> mapping the protein accessions onto the entries; the tables themselves are read during the scan.</summary>
        public double ProteinSeconds
        {
            get { return _proteins.Elapsed.TotalSeconds; }
        }

        /// <summary>
        /// The library composition term of a blib read by this reader: its version and the
        /// tolerance it types within, which the typed fragments carry. In the hash of every
        /// library this reader reads back, a .tsv's derived .blib among them.
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
            _scan.Reset();
            _typingTicks = 0;
            _merge.Reset();
            _proteins.Reset();
            _refSpectraIds.Clear();
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
                // changes, so output is unchanged. The pool is single-threaded, so
                // the interning is the merge's, after the parallel scan.
                var interner = new LibraryStringInterner();
                var typingStats = new FragmentTypingStats();
                _retainedRefIds = OmitFragments || RetainFragmentsFor == null ? null : RetainedRefIds(reader);
                // The protein tables are read on a connection of their own while the spectra
                // scan runs: the two meet only at the mapping, after the merge.
                var proteins = Task.Run(() => ReadProteins(path));
                List<LibraryEntry> entries;
                try
                {
                    entries = LoadSpectra(path, reader, interner, typingStats);
                }
                catch
                {
                    // The protein read holds a reader of its own on the file until it finishes,
                    // and a caller that deletes the refused library on the fault would race it.
                    try
                    {
                        proteins.Wait();
                    }
                    catch (AggregateException)
                    {
                        // The spectra fault is the one reported.
                    }
                    throw;
                }
                LoadProteinMappings(proteins.GetAwaiter().GetResult(), entries, interner);
                interner.LogSummary(logInfo);
                // Nothing was typed on a lean load, so the line would say so of every library.
                if (!OmitFragments)
                {
                    logInfo?.Invoke(string.Format(
                        OspreyIOResources.BlibLoader_Load_Typed__0_N0__of__1_N0__library_peaks_as_b_or_y_ions_within__2___3_,
                        typingStats.Typed, typingStats.Total, _fragmentTolerance.Tolerance,
                        _fragmentTolerance.Unit.GetLocalizedString()));
                }
                return entries;
            }
        }

        #region Private helpers

        /// <summary>
        /// The RefSpectra ids whose peaks <see cref="RetainFragmentsFor"/> keeps, resolved before
        /// the scan: a spectrum's final id is its 0-based index in id order, and a decoy's base
        /// id is its target's index when its <c>DecoyPairs</c> row names one (an unpaired decoy
        /// keeps its own). The same rule the loaded entries end up numbered by, so the set of
        /// final base ids addresses the rows before any entry exists.
        /// </summary>
        private HashSet<long> RetainedRefIds(BlibReader reader)
        {
            var ids = reader.ReadSpectrumIds();
            var pairs = reader.ReadDecoyPairs();
            var retained = new HashSet<long>();
            for (int i = 0; i < ids.Count; i++)
            {
                long refId = ids[i];
                int baseIndex = i;
                if (pairs != null && pairs.TryGetValue(refId, out var pair) && pair.IsDecoy && pair.TargetRefSpectraId.HasValue)
                {
                    int targetIndex = BinarySearch(ids, pair.TargetRefSpectraId.Value);
                    if (targetIndex >= 0)
                        baseIndex = targetIndex;
                }
                if (RetainFragmentsFor.Contains((uint)baseIndex))
                    retained.Add(refId);
            }
            return retained;
        }

        /// <summary>The index of <paramref name="id"/> in the ascending <paramref name="ids"/>, or -1.</summary>
        private static int BinarySearch(IReadOnlyList<long> ids, long id)
        {
            int lo = 0, hi = ids.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                long midId = ids[mid];
                if (midId == id)
                    return mid;
                if (midId < id)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            return -1;
        }

        /// <summary>
        /// Reads every spectrum with its peaks: in partitions by id on the threads, each
        /// decoding and typing its own rows (<see cref="ParseSpectrum"/>), then merged in id
        /// order on this thread, where the entries are numbered and their strings interned
        /// (<see cref="MergePartitions"/>). A library with any modification that cannot be
        /// identified is refused, listing every such modification.
        /// </summary>
        private List<LibraryEntry> LoadSpectra(string path, BlibReader reader, LibraryStringInterner interner,
            FragmentTypingStats typingStats)
        {
            int count = reader.CountSpectra();
            _partitionCount = Math.Max(1, Math.Min(Math.Min(_nThreads, MAX_PARTITIONS), count / MIN_SPECTRA_PER_PARTITION));
            var partitions = new List<ParsedSpectrum>[_partitionCount];

            _scan.Start();
            // A 6.2M-precursor library takes seconds here with nothing else on the console.
            using (var progress = new ProgressReporter(
                       string.Format(OspreyIOResources.BlibLoader_LoadSpectra_Reading__0_N0__library_spectra, count), count,
                       string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                long done = 0;
                var options = new ParallelOptions { MaxDegreeOfParallelism = _partitionCount };
                try
                {
                    Parallel.For(0, _partitionCount, options, partition =>
                    {
                        partitions[partition] = ReadPartition(path, partition, count, typingStats, progress, ref done);
                    });
                }
                catch (AggregateException ex) when (ex.InnerExceptions.Count > 0)
                {
                    // The fault as the row reader threw it, not wrapped: a corrupt peak blob is
                    // reported as the file problem it is, as a one-thread read reported it.
                    ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                }
            }
            _scan.Stop();

            _merge.Start();
            var entries = MergePartitions(partitions, interner);
            _merge.Stop();
            return entries;
        }

        /// <summary>
        /// One partition's rows, in id order, each decoded and typed, read on a reader of this
        /// thread's own. Progress is reported in blocks, since the reporter's lock is shared by
        /// every thread.
        /// </summary>
        private List<ParsedSpectrum> ReadPartition(string path, int partition, int count, FragmentTypingStats typingStats,
            ProgressReporter progress, ref long done)
        {
            const int progressBlock = 1024;
            var typing = new Stopwatch();
            var parsed = new List<ParsedSpectrum>(count / _partitionCount + 1);
            // This thread's own pool, so the partition holds one instance of each sequence and
            // modification name until the merge interns them into the library's pool; without it
            // every row kept three strings of its own, a whole library's worth at the scan's end.
            var interner = new LibraryStringInterner();
            int sinceReport = 0;
            using (var reader = new BlibReader(path))
            {
                // A lean load selects no blob column, so the peaks are never read.
                foreach (var spectrum in reader.ReadSpectra(!OmitFragments, _partitionCount, partition))
                {
                    bool keepPeaks = _retainedRefIds == null || _retainedRefIds.Contains(spectrum.Id);
                    parsed.Add(ParseSpectrum(spectrum, typingStats, typing, keepPeaks, interner));
                    if (++sinceReport == progressBlock)
                    {
                        progress.Report(Interlocked.Add(ref done, sinceReport));
                        sinceReport = 0;
                    }
                }
            }
            if (sinceReport > 0)
                progress.Report(Interlocked.Add(ref done, sinceReport));
            Interlocked.Add(ref _typingTicks, typing.Elapsed.Ticks);
            return parsed;
        }

        /// <summary>
        /// A spectrum's row made into the parts of an entry: its modifications parsed, its peaks
        /// decoded, in m/z order and typed, and its modified sequence rebuilt from the
        /// modification masses in the one text every loader gives it, whatever precision the
        /// file printed. Nothing is interned or numbered here, since the threads share neither.
        /// A modification that cannot be identified leaves <see cref="ParsedSpectrum.Unidentified"/>
        /// naming it, for the merge to collect. Without <paramref name="keepPeaks"/> the blobs are
        /// neither decoded nor typed and the entry's spectrum is released at the merge
        /// (<see cref="RetainFragmentsFor"/>); under <see cref="OmitFragments"/> there are no
        /// blobs and the fragments are a readable empty array.
        /// </summary>
        private ParsedSpectrum ParseSpectrum(BlibSpectrumRecord spectrum, FragmentTypingStats typingStats, Stopwatch typing,
            bool keepPeaks, LibraryStringInterner partitionInterner)
        {
            // A row without a sequence (a small molecule) is refused as a peptide too
            // short to search; one without a modified sequence is the unmodified peptide.
            string peptideSeq = spectrum.PeptideSeq ?? string.Empty;
            string peptideModSeq = spectrum.PeptideModSeq ?? peptideSeq;
            // The same bound the TSV loader enforces, checked before any work on the row so a
            // library that will be refused is refused at once, not after every row is typed. An
            // invariant only one format checks is not an invariant, and DecoyGenerator's
            // fragment-overlap gate relies on this one whatever the library was built from.
            LibraryValidation.ValidatePeptideLength(peptideSeq);
            var parsed = new ParsedSpectrum
            {
                RefId = spectrum.Id,
                PeptideSeq = partitionInterner.Intern(peptideSeq),
                Charge = spectrum.PrecursorCharge,
                PrecursorMz = spectrum.PrecursorMz,
                RetentionTime = spectrum.RetentionTime ?? 0.0,
            };
            var modifications = ParseBlibModifications(peptideModSeq, out string unidentified);
            if (modifications == null)
            {
                parsed.Unidentified = unidentified;
                parsed.PeptideModSeq = peptideModSeq;
                return parsed;
            }
            parsed.Modifications = modifications.Count == 0 ? Array.Empty<Modification>() : modifications.ToArray();
            foreach (var m in parsed.Modifications)
                m.Name = partitionInterner.Intern(m.Name);
            parsed.ModifiedSequence = partitionInterner.Intern(BlibSpectrum.FormatModifiedSequence(peptideSeq,
                PeptideFragmentMass.ModMassesByPosition(parsed.Modifications)));

            if (!keepPeaks)
            {
                parsed.Released = true;
                return parsed;
            }
            if (OmitFragments)
            {
                parsed.Fragments = Array.Empty<LibraryFragment>();
                return parsed;
            }
            // A spectrum without a peaks row has no fragments.
            var peaks = BlibReader.DecodePeaks(spectrum);
            typing.Start();
            var fragments = peaks != null ? ToFragments(peaks) : Array.Empty<LibraryFragment>();
            FragmentTyping.TypeFragments(peptideSeq, parsed.Modifications, spectrum.PrecursorCharge, fragments,
                _fragmentTolerance, typingStats);
            typing.Stop();
            parsed.Fragments = fragments;
            return parsed;
        }

        /// <summary>
        /// The partitions merged in RefSpectra id order - each is in id order already, so the
        /// smallest head is next - into entries numbered from 0 in that order, as the TSV
        /// loader's are (the RefSpectra id is kept beside the entry for the tables that refer to
        /// it), with their strings interned into the library's pool (each partition pooled its own
        /// on its thread, so this is a lookup per distinct string rather than per row).
        /// </summary>
        private List<LibraryEntry> MergePartitions(List<ParsedSpectrum>[] partitions, LibraryStringInterner interner)
        {
            int total = partitions.Sum(p => p.Count);
            var entries = new List<LibraryEntry>(total);
            var heads = new int[partitions.Length];
            var unidentifiedMods = new UnidentifiedModifications();
            for (int n = 0; n < total; n++)
            {
                int next = -1;
                for (int p = 0; p < partitions.Length; p++)
                {
                    if (heads[p] < partitions[p].Count &&
                        (next < 0 || partitions[p][heads[p]].RefId < partitions[next][heads[next]].RefId))
                    {
                        next = p;
                    }
                }
                var parsed = partitions[next][heads[next]++];

                if (parsed.Unidentified != null)
                {
                    unidentifiedMods.Add(parsed.Unidentified, parsed.RefId, parsed.PeptideModSeq);
                    continue;
                }
                var entry = new LibraryEntry((uint)entries.Count,
                    interner.Intern(parsed.PeptideSeq),
                    interner.Intern(parsed.ModifiedSequence),
                    (byte)parsed.Charge, parsed.PrecursorMz, parsed.RetentionTime);
                _refSpectraIds.Add(parsed.RefId);
                entry.Modifications = InternModificationNames(parsed.Modifications, interner);
                // RELEASED, not empty, for a spectrum the retain set skipped: the state a load of
                // everything followed by LibraryFragmentRelease leaves, tripwire included (see
                // LibraryEntry.ReleaseSpectrum).
                if (parsed.Released)
                    entry.ReleaseSpectrum();
                else
                    entry.Fragments = parsed.Fragments;
                entries.Add(entry);
            }

            if (unidentifiedMods.Any)
                throw unidentifiedMods.ToException();
            return entries;
        }

        /// <summary>The protein accessions of every spectrum (<see cref="BlibReader.ReadProteins"/>), read on a reader of its own.</summary>
        private static ILookup<long, string> ReadProteins(string path)
        {
            using (var reader = new BlibReader(path))
                return reader.ReadProteins();
        }

        private void LoadProteinMappings(ILookup<long, string> proteins, List<LibraryEntry> entries,
            LibraryStringInterner interner)
        {
            _proteins.Start();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // The lookup is empty for a spectrum without a protein, which keeps its default.
                var accessions = proteins[_refSpectraIds[i]].Select(interner.Intern).ToArray();
                if (accessions.Length > 0)
                    entry.ProteinIds = accessions;
            }
            _proteins.Stop();
        }

        /// <summary>
        /// Intern each modification's <see cref="Modification.Name"/>, in place; the array is
        /// this load's own until the entry is published. Only string identity changes.
        /// </summary>
        private static Modification[] InternModificationNames(Modification[] mods, LibraryStringInterner interner)
        {
            foreach (var m in mods)
                m.Name = interner.Intern(m.Name);
            return mods;
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
            for (int i = 0; i < fragments.Length; i++)
            {
                // Untyped until the typing names an ion; a peak it cannot name stays exactly
                // this, the annotation the TSV loader leaves on such a peak.
                fragments[i] = new LibraryFragment
                {
                    Mz = peaks.Mzs[i],
                    RelativeIntensity = peaks.Intensities[i],
                    Annotation = new FragmentAnnotation { IonType = IonType.Unknown }
                };
            }
            // In m/z order (a BiblioSpec writer's order, so the sort runs only for a .blib
            // written otherwise), one peak per m/z, scaled to the base peak as a DIA-NN library
            // already is.
            for (int i = 1; i < fragments.Length; i++)
            {
                if (fragments[i].Mz < fragments[i - 1].Mz)
                {
                    fragments = fragments.OrderBy(f => f.Mz).ToArray();
                    break;
                }
            }
            return FragmentPeaks.MergeEqualMz(fragments);
        }

        /// <summary>
        /// One <c>RefSpectra</c> row read, decoded and typed on its partition's thread: the parts
        /// of a <see cref="LibraryEntry"/> before the merge numbers it and interns its strings.
        /// </summary>
        private sealed class ParsedSpectrum
        {
            public long RefId;
            public string PeptideSeq;
            /// <summary>The file's own modified-sequence text, kept only for a row with an unidentified modification, which it names.</summary>
            public string PeptideModSeq;
            /// <summary>The modified sequence rebuilt from the modification masses; null when one could not be identified.</summary>
            public string ModifiedSequence;
            public Modification[] Modifications;
            public LibraryFragment[] Fragments;
            /// <summary>True when the retain set skipped this spectrum's peaks, so the entry's spectrum is released.</summary>
            public bool Released;
            public int Charge;
            public double PrecursorMz;
            public double RetentionTime;
            /// <summary>The first bracket of the modified sequence that names no known modification, or null.</summary>
            public string Unidentified;
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
