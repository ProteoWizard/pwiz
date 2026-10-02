/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// A seekable index over a <c>.spectra.bin</c> cache that groups the MS2
    /// records by isolation-window key WITHOUT loading their peaks. Built from the
    /// cache's acquisition-order EOF index (one compact contiguous read, no record
    /// walk), after which a window's spectra are decoded on demand via
    /// <see cref="LoadWindow"/> and released once that window is scored. This lets
    /// scoring hold only a few windows' peaks resident at a time instead of the
    /// whole ~6 GB MS2 list. Because the v4 cache body is written window-grouped,
    /// each window's records are contiguous, so <see cref="LoadWindow"/> reads a
    /// window in one sequential run.
    ///
    /// The window key reproduces the in-memory grouping scoring uses exactly:
    /// <see cref="SpectraCache.WindowKey"/> of each record's self-declared isolation
    /// center (no isolation-scheme model is inferred). Decoding reuses
    /// <see cref="SpectraCache.ReadMs2Record"/>, so a streamed window is
    /// byte-for-byte identical to the same records read by
    /// <see cref="SpectraCache.LoadSpectraCache"/>.
    ///
    /// Thread-safety: <see cref="LoadWindow"/> opens its own <see cref="FileStream"/>
    /// per call and shares no mutable state, so windows load concurrently (scoring
    /// runs one Parallel.For body per window). <see cref="AllMs2Rts"/> and the
    /// offset map are immutable after <see cref="BuildFromCache(string,string)"/>.
    /// </summary>
    public sealed class SpectraWindowIndex
    {
        // Shared by every index under OSPREY_SERIAL_READ_SCOPE=process.
        private static readonly object s_processBlockReadLock = new object();

        private readonly string _cachePath;
        private readonly Dictionary<int, List<long>> _windowKeyToOffsets;
        private readonly Dictionary<int, IsolationWindow> _windowKeyToFirstIso;
        private readonly List<int> _windowKeysInFileOrder;
        private readonly long _ms1SectionOffset;
        // Held by LoadWindowSerialRead only while it reads a block from disk.
        private readonly object _blockReadLock = new object();

        private SpectraWindowIndex(string cachePath,
            Dictionary<int, List<long>> windowKeyToOffsets, double[] allMs2Rts,
            Dictionary<int, IsolationWindow> windowKeyToFirstIso, List<int> windowKeysInFileOrder,
            long ms1SectionOffset, List<MS1Spectrum> ms1Spectra, List<IsolationWindow> isolationWindows)
        {
            _cachePath = cachePath;
            _windowKeyToOffsets = windowKeyToOffsets;
            _windowKeyToFirstIso = windowKeyToFirstIso;
            _windowKeysInFileOrder = windowKeysInFileOrder;
            _ms1SectionOffset = ms1SectionOffset;
            AllMs2Rts = allMs2Rts;
            Ms1Spectra = ms1Spectra;
            IsolationWindows = isolationWindows;
        }

        /// <summary>
        /// The MS1 spectra, loaded in full (small in DIA and needed resident for the global
        /// precursor RT search). Read from the cache's MS1 section (located via the EOF
        /// index), so streaming Stages 1-4 get MS1 without materializing the full MS2 list.
        /// Byte-identical to <see cref="SpectraCache.LoadSpectraCache"/>'s MS1 (shared
        /// <see cref="SpectraCache.ReadMs1Record"/>).
        /// </summary>
        public IReadOnlyList<MS1Spectrum> Ms1Spectra { get; }

        /// <summary>
        /// The first DIA cycle's isolation windows, deduplicated on the rounded center key
        /// and sorted by center -- reconstructed byte-identically to
        /// <c>ScoringTaskShared.ExtractIsolationWindows</c> (same first-appearance dedup,
        /// same <c>Center</c> sort) from the index, so scoring's window fan-out is
        /// unchanged without materializing the full MS2 list.
        /// </summary>
        public IReadOnlyList<IsolationWindow> IsolationWindows { get; }

        /// <summary>
        /// The window keys in first-encounter (file) order -- the same order the resident
        /// calibration grouping (<c>spectraByWindowKey</c>) inserts them, so a linear-scan
        /// window fallback enumerates them identically. Used only by the streaming
        /// calibration resolution pre-pass.
        /// </summary>
        public IReadOnlyList<int> WindowKeysInFileOrder { get { return _windowKeysInFileOrder; } }

        /// <summary>
        /// The isolation window of the FIRST spectrum recorded for a key (file order) --
        /// the same one the resident grouping exposes as <c>windowSpectra[0].IsolationWindow</c>
        /// for its <c>.Contains(precursorMz)</c> key-collision and linear-scan checks.
        /// Reconstructed byte-identically to <see cref="SpectraCache.ReadMs2Record"/>
        /// (<c>new IsolationWindow(isoCenter, isoLower, isoUpper)</c>), so streaming
        /// calibration resolves each entry's window exactly as the resident path does,
        /// WITHOUT loading any peaks. Returns false for an absent key.
        /// </summary>
        public bool TryGetWindowIsolation(int windowKey, out IsolationWindow isolationWindow)
        {
            return _windowKeyToFirstIso.TryGetValue(windowKey, out isolationWindow);
        }

        /// <summary>
        /// Retention time of every MS2 record, in file order. The double-counting
        /// dedup needs only the RT multiset (it sorts internally), so this is the
        /// resident MS2 list's sole surviving dependency once scoring is streamed.
        /// </summary>
        public IReadOnlyList<double> AllMs2Rts { get; }

        /// <summary>
        /// The number of MS2 records indexed (matches the cache's <c>n_ms2</c>).
        /// </summary>
        public int Ms2Count { get { return AllMs2Rts.Count; } }

        /// <summary>
        /// Build the index from a <c>.spectra.bin</c> cache. Returns null when the
        /// file is absent or its header fails validation (bad magic/version, or the
        /// source fingerprint no longer matches) -- the SAME rejection rules as
        /// <see cref="SpectraCache.LoadSpectraCache"/>, so a caller can fall back to
        /// a full resident load when the index cannot be built.
        /// </summary>
        public static SpectraWindowIndex BuildFromCache(string cachePath, string sourcePath = null)
        {
            return BuildFromCache(cachePath, sourcePath, out _);
        }

        /// <summary>
        /// Same, reporting WHICH validation rule refused the cache. A caller that recovers by
        /// re-parsing the mzML has no use for the distinction and takes the overload above; a
        /// caller that FAILS needs it, because the six refusals have different remedies.
        /// </summary>
        public static SpectraWindowIndex BuildFromCache(string cachePath, string sourcePath,
            out SpectraCacheRejection reason)
        {
            return BuildFromCache(cachePath, sourcePath, true, out reason);
        }

        /// <summary>
        /// Same, optionally WITHOUT the MS1 section: a consumer that reads only MS2 windows
        /// passes <paramref name="loadMs1"/> false and gets an empty <see cref="Ms1Spectra"/>,
        /// skipping the one part of the index build that decodes peaks. Every other member is
        /// built identically.
        /// </summary>
        public static SpectraWindowIndex BuildFromCache(string cachePath, string sourcePath,
            bool loadMs1, out SpectraCacheRejection reason)
        {
            reason = SpectraCacheRejection.None;
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                reason = SpectraCacheRejection.Absent;
                return null;
            }

            using (var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // Validate + read counts identically to LoadSpectraCache.
                if (!SpectraCache.TryReadHeader(r, sourcePath, out uint nMs2, out uint nMs1, out reason))
                    return null;

                // Read the acquisition-order index in one compact contiguous EOF
                // read -- no record walk. Everything the resident grouping derives is
                // rebuilt from the index's per-record {offset, iso*, rt}, so the maps
                // are byte-identical to the old header walk while touching only
                // ~40 B/record instead of seeking across the whole 6 GB body.
                SpectraCache.SpectraCacheIndex index = SpectraCache.ReadIndex(fs, r, nMs2);

                var windowKeyToOffsets = new Dictionary<int, List<long>>();
                var windowKeyToFirstIso = new Dictionary<int, IsolationWindow>();
                var windowKeysInFileOrder = new List<int>();
                var allMs2Rts = new double[nMs2];

                // First DIA cycle's isolation windows, reproducing
                // ScoringTaskShared.ExtractIsolationWindows: add each distinct rounded-center
                // key's window in first-appearance order until a key repeats (the cycle
                // wraps), then sort by center below.
                var firstCycleWindows = new List<IsolationWindow>();
                var firstCycleSeen = new HashSet<int>();
                bool firstCycleDone = false;

                for (uint i = 0; i < nMs2; i++)
                {
                    double isoCenter = index.IsoCenters[i];
                    double isoLower = index.IsoLowers[i];
                    double isoUpper = index.IsoUppers[i];
                    allMs2Rts[i] = index.Rts[i];

                    int key = SpectraCache.WindowKey(isoCenter);
                    if (!windowKeyToOffsets.TryGetValue(key, out var offsets))
                    {
                        offsets = new List<long>();
                        windowKeyToOffsets[key] = offsets;
                        // First record for this key (file order) -- its isolation window
                        // is what the resident grouping exposes as windowSpectra[0].
                        // Built exactly as SpectraCache.ReadMs2Record does.
                        windowKeyToFirstIso[key] = new IsolationWindow(isoCenter, isoLower, isoUpper);
                        windowKeysInFileOrder.Add(key);
                    }
                    // First-cycle windows: add on first sight of a key, stop once one repeats.
                    if (!firstCycleDone)
                    {
                        if (!firstCycleSeen.Add(key))
                            firstCycleDone = true;
                        else
                            firstCycleWindows.Add(new IsolationWindow(isoCenter, isoLower, isoUpper));
                    }
                    offsets.Add(index.RecordOffsets[i]);
                }

                // Reproduce ExtractIsolationWindows' final sort by center.
                firstCycleWindows.Sort((a, b) => a.Center.CompareTo(b.Center)); // Array.Sort OK: dedup on the rounded center key leaves distinct centers, so the comparator never ties (mirror of ExtractIsolationWindows)

                // MS1 in full from the recorded section offset (small in DIA and needed
                // resident for the global precursor RT search) -- so streaming Stages 1-4
                // get MS1 without ever building the full MS2 list. Same decode as
                // LoadSpectraCache (shared ReadMs1Record).
                var ms1Spectra = new List<MS1Spectrum>(loadMs1 ? (int)nMs1 : 0);
                if (loadMs1)
                {
                    fs.Seek(index.Ms1SectionOffset, SeekOrigin.Begin);
                    for (uint i = 0; i < nMs1; i++)
                        ms1Spectra.Add(SpectraCache.ReadMs1Record(r));
                }

                return new SpectraWindowIndex(cachePath, windowKeyToOffsets, allMs2Rts,
                    windowKeyToFirstIso, windowKeysInFileOrder, index.Ms1SectionOffset, ms1Spectra, firstCycleWindows);
            }
        }

        /// <summary>
        /// Decode the raw (uncalibrated) MS2 spectra for one window key, in file
        /// order. Returns an empty list for an absent key -- identical to the
        /// in-memory grouping's dictionary miss. Opens its own <see cref="FileStream"/>
        /// so concurrent window loads never contend on a shared read position.
        /// </summary>
        public List<Spectrum> LoadWindow(int windowKey)
        {
            if (!_windowKeyToOffsets.TryGetValue(windowKey, out var offsets))
                return new List<Spectrum>();

            // Default FileStream buffer is deliberate on this hot per-window path: a
            // larger explicit buffer SLOWS it (the >4 KB peak blobs would be copied
            // through the buffer instead of read direct). See the NOTE in SpectraCache.cs.
            using (var fs = new FileStream(_cachePath, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // The window's records are written contiguously (v4 grouping) and its
                // offsets ascend, so read them as ONE forward sequential pass: seek once,
                // then read straight through. The per-record position check asserts that
                // contiguity -- reads stay in-sequence within the group, and a mis-grouped
                // cache faults instead of silently seeking out of the block.
                fs.Seek(offsets[0], SeekOrigin.Begin);
                return ReadRecords(r, offsets, 0);
            }
        }

        /// <summary>
        /// The same spectra as <see cref="LoadWindow"/>, read as one block while holding a lock
        /// that every call on this index shares, then decoded outside it. Threads that load
        /// windows in parallel this way take turns at the disk and decode concurrently.
        ///
        /// <para>For a consumer that reads every window of a run from a cold spinning disk:
        /// concurrent LoadWindow calls each walk their own window in small reads, so the disk
        /// seeks between as many streams as there are threads. When the spectra are already in
        /// the file cache, LoadWindow is faster. A window too large for one array is read by
        /// LoadWindow.</para>
        /// </summary>
        public List<Spectrum> LoadWindowSerialRead(int windowKey)
        {
            if (!OspreyEnvironment.SerialWindowReads)
                return LoadWindow(windowKey);
            if (!_windowKeyToOffsets.TryGetValue(windowKey, out var offsets))
                return new List<Spectrum>();

            long start = offsets[0];
            long end = BlockEnd(start);
            byte[] block;
            // Buffer size 1 turns off FileStream's own buffer: one read straight into the block.
            using (var fs = new FileStream(_cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1))
            {
                // The last window ends where the footer says the MS1 section starts, which no
                // other check reads when MS1 is not loaded.
                if (end <= start || end > fs.Length)
                {
                    throw new InvalidDataException(string.Format(
                        OspreyIOResources.SpectraWindowIndex_LoadWindowSerialRead_The_spectra_cache_is_damaged__an_isolation_window_would_end_at_byte__0___outside_the_file_, end));
                }
                if (end - start > Array.MaxLength)
                    return LoadWindow(windowKey);
                // Allocated before taking the lock, so no other thread's read waits on it.
                block = new byte[end - start];
                lock (OspreyEnvironment.SerialReadsProcessWide ? s_processBlockReadLock : _blockReadLock)
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    fs.ReadExactly(block);
                }
            }
            using (var r = new BinaryReader(new MemoryStream(block, false)))
            {
                var result = ReadRecords(r, offsets, start);
                // The records must fill the block exactly: one that ends early or late was cut
                // at the wrong place.
                if (r.BaseStream.Position != block.Length)
                {
                    throw new InvalidDataException(string.Format(
                        OspreyIOResources.SpectraWindowIndex_LoadWindow_The_spectra_cache_is_damaged__a_record_is_not_where_its_index_says__expected_byte__0___, end, start + r.BaseStream.Position));
                }
                return result;
            }
        }

        // Decode a window's records from a reader positioned at its first record. origin is the
        // file offset of the reader's position 0, so each record's position can be checked
        // against the index: the window's records are contiguous (v4 grouping), and a
        // mis-grouped cache faults instead of silently reading past its window.
        private static List<Spectrum> ReadRecords(BinaryReader r, List<long> offsets, long origin)
        {
            var result = new List<Spectrum>(offsets.Count);
            foreach (long offset in offsets)
            {
                long position = origin + r.BaseStream.Position;
                if (position != offset)
                {
                    throw new InvalidDataException(string.Format(
                        OspreyIOResources.SpectraWindowIndex_LoadWindow_The_spectra_cache_is_damaged__a_record_is_not_where_its_index_says__expected_byte__0___, offset, position));
                }
                result.Add(SpectraCache.ReadMs2Record(r));
            }
            return result;
        }

        // A window's block runs from its first record to the next window's first record, or to
        // the MS1 section, which follows the last window in the body.
        private long BlockEnd(long start)
        {
            long end = _ms1SectionOffset;
            foreach (var offsets in _windowKeyToOffsets.Values)
            {
                long other = offsets[0];
                if (other > start && other < end)
                    end = other;
            }
            return end;
        }
    }
}
