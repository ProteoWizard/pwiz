/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Text;
using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// What a run's MS2 spectra were acquired with that its spectra cache does not keep: each spectrum's ion injection
    /// (fill) time and its mass analyzer, by scan number (the cache's 0-based read index). The demultiplexer counts
    /// each spectrum's ions from them (intensity x fill time over the analyzer's ion response).
    /// </summary>
    public sealed class AcquisitionInfo
    {
        private readonly Dictionary<uint, (float FillTimeMs, byte Analyzer)> _byScan =
            new Dictionary<uint, (float, byte)>();
        private readonly List<string> _analyzers = new List<string>();
        private readonly Dictionary<string, byte> _analyzerIndex = new Dictionary<string, byte>();

        /// <summary>The MS2 spectra recorded.</summary>
        public int Count
        {
            get { return _byScan.Count; }
        }

        /// <summary>The distinct analyzer descriptions, as indexed by the records.</summary>
        public IReadOnlyList<string> Analyzers
        {
            get { return _analyzers; }
        }

        /// <summary>
        /// Records an MS2 spectrum: its fill time in ms (NaN when the file does not report it) and its analyzer, the
        /// PSI-MS analyzer term names of its instrument configuration (null or empty when not known).
        /// </summary>
        public void Add(uint scanNumber, double fillTimeMs, string analyzer)
        {
            analyzer = analyzer ?? string.Empty;
            if (!_analyzerIndex.TryGetValue(analyzer, out byte index))
            {
                if (_analyzers.Count == byte.MaxValue)
                    throw new InvalidOperationException(@"More analyzer configurations than an acquisition sidecar records.");
                index = (byte)_analyzers.Count;
                _analyzers.Add(analyzer);
                _analyzerIndex[analyzer] = index;
            }
            _byScan[scanNumber] = ((float)fillTimeMs, index);
        }

        /// <summary>A spectrum's fill time (ms; NaN when not reported) and analyzer; false when it was not recorded.</summary>
        public bool TryGet(uint scanNumber, out double fillTimeMs, out string analyzer)
        {
            if (!_byScan.TryGetValue(scanNumber, out var record))
            {
                fillTimeMs = double.NaN;
                analyzer = null;
                return false;
            }
            fillTimeMs = record.FillTimeMs;
            analyzer = _analyzers[record.Analyzer];
            return true;
        }

        internal IEnumerable<KeyValuePair<uint, (float FillTimeMs, byte Analyzer)>> Records
        {
            get { return _byScan; }
        }

        internal void AddRecord(uint scanNumber, float fillTimeMs, byte analyzer)
        {
            _byScan[scanNumber] = (fillTimeMs, analyzer);
        }

        internal void AddAnalyzer(string analyzer)
        {
            _analyzerIndex[analyzer] = (byte)_analyzers.Count;
            _analyzers.Add(analyzer);
        }
    }

    /// <summary>
    /// A run's <see cref="AcquisitionInfo"/> on disk, <c>{stem}.acquisition.bin</c> beside its spectra cache: written
    /// when the source is parsed, and fingerprinted to the source as the spectra cache is, so the two are rejected
    /// together. A separate file, so the spectra cache's format, and every cache already written, stays as it is.
    /// <para>Format (little-endian): [magic "OSPRACQ\0"] [version: uint32] [source_size: uint64] [source_mtime:
    /// int64] [n_analyzers: uint8] [analyzers: length-prefixed UTF-8 strings] [n_records: uint32] [records:
    /// [scan_number: uint32] [fill_time_ms: float32, NaN when not reported] [analyzer: uint8]].</para>
    /// </summary>
    public static class AcquisitionSidecar
    {
        public const string EXT = @".acquisition.bin";

        private static readonly byte[] MAGIC = { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'A', (byte)'C', (byte)'Q', 0 };
        private const uint VERSION = 1;

        /// <summary>The sidecar of a spectra cache: its path with the cache's extension replaced.</summary>
        public static string PathFor(string spectraCachePath)
        {
            return spectraCachePath.EndsWith(SpectraCache.EXT, StringComparison.OrdinalIgnoreCase)
                ? spectraCachePath.Substring(0, spectraCachePath.Length - SpectraCache.EXT.Length) + EXT
                : spectraCachePath + EXT;
        }

        /// <summary>Writes the sidecar, fingerprinted to the source as <see cref="SpectraCache"/> fingerprints it.</summary>
        public static void Save(string path, AcquisitionInfo info, string sourcePath)
        {
            if (!SpectraCache.TryComputeSourceFingerprint(sourcePath, out long sourceSize, out long sourceMtimeMs))
            {
                sourceSize = unchecked((long)SpectraCache.FINGERPRINT_UNMEASURABLE);
                sourceMtimeMs = 0;
            }
            using (var saver = new SimpleFileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var w = new BinaryWriter(fs, Encoding.UTF8))
                {
                    w.Write(MAGIC);
                    w.Write(VERSION);
                    w.Write((ulong)sourceSize);
                    w.Write(sourceMtimeMs);
                    w.Write((byte)info.Analyzers.Count);
                    foreach (string analyzer in info.Analyzers)
                        w.Write(analyzer);
                    w.Write((uint)info.Count);
                    foreach (var record in info.Records)
                    {
                        w.Write(record.Key);
                        w.Write(record.Value.FillTimeMs);
                        w.Write(record.Value.Analyzer);
                    }
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// The sidecar's records, or null when it is missing, unreadable, of another version, or written for a
        /// different version of the source. A source that is not there is not compared, as for the spectra cache.
        /// </summary>
        public static AcquisitionInfo Load(string path, string sourcePath)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var r = new BinaryReader(fs, Encoding.UTF8))
                {
                    byte[] magic = r.ReadBytes(MAGIC.Length);
                    if (!magic.SequenceEqual(MAGIC) || r.ReadUInt32() != VERSION)
                        return null;
                    ulong storedSize = r.ReadUInt64();
                    long storedMtimeMs = r.ReadInt64();
                    if (storedSize == SpectraCache.FINGERPRINT_UNMEASURABLE)
                        return null;
                    if (storedSize != 0 && !string.IsNullOrEmpty(sourcePath))
                    {
                        if (!SpectraCache.TryComputeSourceFingerprint(sourcePath, out long size, out long mtimeMs))
                            return null;
                        if (size != 0 && ((ulong)size != storedSize || mtimeMs != storedMtimeMs))
                            return null;
                    }
                    var info = new AcquisitionInfo();
                    int analyzers = r.ReadByte();
                    for (int i = 0; i < analyzers; i++)
                        info.AddAnalyzer(r.ReadString());
                    uint count = r.ReadUInt32();
                    for (uint i = 0; i < count; i++)
                    {
                        uint scan = r.ReadUInt32();
                        float fill = r.ReadSingle();
                        byte analyzer = r.ReadByte();
                        if (analyzer >= analyzers)
                            return null;
                        info.AddRecord(scan, fill, analyzer);
                    }
                    return info;
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
