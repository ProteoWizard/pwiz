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
    /// records by isolation-window key WITHOUT loading their peaks. Built with a
    /// single header-only pass (read each record's fixed 48-byte prefix, then seek
    /// past its variable peak blob), after which a window's spectra are decoded on
    /// demand via <see cref="LoadWindow"/> and released once that window is scored.
    /// This lets scoring hold only a few windows' peaks resident at a time instead
    /// of the whole ~6 GB MS2 list.
    ///
    /// The window key reproduces the in-memory grouping scoring uses exactly:
    /// <c>(int)Math.Round(iso_center * 10.0)</c> taken from each record's
    /// self-declared isolation center (no isolation-scheme model is inferred).
    /// Decoding reuses <see cref="SpectraCache.ReadMs2Record"/>, so a streamed
    /// window is byte-for-byte identical to the same records read by
    /// <see cref="SpectraCache.LoadSpectraCache"/>.
    ///
    /// Thread-safety: <see cref="LoadWindow"/> opens its own <see cref="FileStream"/>
    /// per call and shares no mutable state, so windows load concurrently (scoring
    /// runs one Parallel.For body per window). <see cref="AllMs2Rts"/> and the
    /// offset map are immutable after <see cref="BuildFromCache"/>.
    /// </summary>
    public sealed class SpectraWindowIndex
    {
        private readonly string _cachePath;
        private readonly Dictionary<int, List<long>> _windowKeyToOffsets;

        private SpectraWindowIndex(string cachePath,
            Dictionary<int, List<long>> windowKeyToOffsets, double[] allMs2Rts)
        {
            _cachePath = cachePath;
            _windowKeyToOffsets = windowKeyToOffsets;
            AllMs2Rts = allMs2Rts;
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
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
                return null;

            using (var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // Validate + read counts identically to LoadSpectraCache. On
                // success the reader is positioned at the first MS2 record.
                if (!SpectraCache.TryReadHeader(r, sourcePath, out uint nMs2, out _))
                    return null;

                var windowKeyToOffsets = new Dictionary<int, List<long>>();
                var allMs2Rts = new double[nMs2];

                for (uint i = 0; i < nMs2; i++)
                {
                    long recordOffset = fs.Position;

                    r.ReadUInt32();                     // scan_number (unused here)
                    double rt = r.ReadDouble();
                    r.ReadDouble();                     // precursor_mz (unused here)
                    double isoCenter = r.ReadDouble();
                    r.ReadDouble();                     // iso_lower (unused here)
                    r.ReadDouble();                     // iso_upper (unused here)
                    uint nPeaks = r.ReadUInt32();
                    // Skip the variable-length peak blob (f64 m/z + f32 intensity
                    // per peak) without reading it -- the whole point of the pass.
                    fs.Seek((long)nPeaks * SpectraCache.PEAK_BYTES_PER_POINT, SeekOrigin.Current);

                    int key = (int)Math.Round(isoCenter * 10.0);
                    if (!windowKeyToOffsets.TryGetValue(key, out var offsets))
                    {
                        offsets = new List<long>();
                        windowKeyToOffsets[key] = offsets;
                    }
                    offsets.Add(recordOffset);
                    allMs2Rts[i] = rt;
                }

                // MS1 records are deliberately not indexed: MS1 access is a global
                // RT search that stays fully resident (small in DIA), so streaming
                // leaves it untouched.
                return new SpectraWindowIndex(cachePath, windowKeyToOffsets, allMs2Rts);
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
            if (!_windowKeyToOffsets.TryGetValue(windowKey, out var offsets) || offsets.Count == 0)
                return new List<Spectrum>();

            var result = new List<Spectrum>(offsets.Count);
            using (var fs = new FileStream(_cachePath, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                foreach (long offset in offsets)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    result.Add(SpectraCache.ReadMs2Record(r));
                }
            }
            return result;
        }
    }
}
