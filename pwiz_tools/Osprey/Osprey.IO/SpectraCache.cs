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
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Binary spectra cache for fast re-loading of parsed MS2 and MS1 spectra.
    /// After parsing mzML (which can take minutes for large files), this writes a compact
    /// .spectra.bin file that can be reloaded in seconds.
    /// Ported from osprey-io/src/mzml/spectra_cache.rs (VERSION 1-3); VERSION 4
    /// reorganizes the body for window streaming (see below).
    ///
    /// Format (little-endian), VERSION 4:
    /// [magic: 8 bytes "OSPRSPC\0"]
    /// [version: uint32]
    /// [source_size: uint64]   source file length, or 0 when unknown
    /// [source_mtime: int64]   source last-write time, Unix milliseconds UTC, or 0.
    /// [n_ms2: uint32]
    /// [n_ms1: uint32]
    /// [MS2 records -- GROUPED BY ISOLATION WINDOW: all records sharing a rounded
    ///   iso-center key are written contiguously, so SpectraWindowIndex.LoadWindow
    ///   reads a whole window in one sequential run. Each record:
    ///   [scan_number: uint32] [retention_time: float64] [precursor_mz: float64]
    ///   [iso_center: float64] [iso_lower: float64] [iso_upper: float64]
    ///   [n_peaks: uint32] [mzs: float64 x n_peaks] [intensities: float32 x n_peaks]]
    /// [MS1 records, acquisition order. Each record:
    ///   [scan_number: uint32] [retention_time: float64]
    ///   [n_peaks: uint32] [mzs: float64 x n_peaks] [intensities: float32 x n_peaks]]
    /// [index: n_ms2 entries in ACQUISITION order, 40 bytes each:
    ///   [record_offset: int64] [iso_center: float64] [iso_lower: float64]
    ///   [iso_upper: float64] [retention_time: float64]]
    /// [footer: [ms1_section_offset: int64] [index_offset: int64]]  (16 bytes at EOF)
    ///
    /// The acquisition-order index lets a reader rebuild the window map, AllMs2Rts,
    /// and the first-cycle isolation windows from one compact contiguous read (no
    /// record walk), and restores acquisition order for the full LoadSpectraCache
    /// read even though the body is physically window-grouped.
    /// </summary>
    public static class SpectraCache
    {
        private static readonly byte[] MAGIC = new byte[] {
            (byte)'O', (byte)'S', (byte)'P', (byte)'R',
            (byte)'S', (byte)'P', (byte)'C', 0
        };
        // VERSION 2 (2026-05-09): mzML load now sorts non-monotonic centroids
        // before caching, so caches written by VERSION 1 may contain unsorted
        // peaks that produce undefined-behavior divergence in fragment matching.
        // Old caches are invalidated on this version bump so the fresh load
        // path (which sorts) re-populates them.
        // VERSION 3 (2026-06-09): header now carries the source file's size +
        // last-write-time (Unix ms) so a cache that lives apart from its data
        // file (--cache-dir / --output-dir) is rejected when the source changes.
        // Old caches re-populate on this bump.
        // VERSION 4 (2026-07-16): MS2 records are written GROUPED by isolation
        // window (contiguous per window) followed by a per-record acquisition-order
        // index and an EOF footer, so SpectraWindowIndex streams each window in one
        // sequential read. This fixes a ~3x cold-HDD regression the file-order v3
        // layout caused (scattered per-window reads during streamed scoring). Old
        // v3 caches are invalidated (rejected) on this bump and re-populate on
        // first use.
        private const uint VERSION = 4;

        // Fixed EOF footer: [ms1_section_offset: int64][index_offset: int64].
        private const int FOOTER_BYTES = 16;

        // NOTE on FileStream buffering -- do NOT give these a larger explicit buffer
        // thinking it reads faster. It measurably does not, and here is why:
        // A record's peak blob (~31 KB avg on Astral) is LARGER than the default 4 KB
        // FileStream buffer, so BinaryReader.ReadBytes reads it DIRECTLY into the
        // caller's array (one copy), bypassing the internal buffer -- already optimal
        // for the bulk data. A buffer bigger than the peak blob is counterproductive:
        // it drags all ~6 GB of peaks THROUGH the buffer (OS cache -> buffer -> array,
        // a double copy). Measured on Astral file 49: an explicit 64 KB or 1 MB buffer
        // added ~5 s to WARM per-window coelution (42 -> 47 s; 40.6 K -> 35.6 K
        // cand/s). COLD is disk-bound, so it HID the cost -- which is exactly the trap.
        // The cold win comes entirely from the physical window GROUPING (contiguous
        // per-window blocks + OS read-ahead = sequential reads), NOT from the app
        // buffer. This applies to every cache FileStream: SaveSpectraCache/
        // LoadSpectraCache here and SpectraWindowIndex.BuildFromCache/LoadWindow.

        /// <summary>
        /// Save spectra to a binary cache file.
        /// </summary>
        public static void SaveSpectraCache(string path, List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra,
            string sourcePath = null)
        {
            if (ms2Spectra == null)
                ms2Spectra = new List<Spectrum>();
            if (ms1Spectra == null)
                ms1Spectra = new List<MS1Spectrum>();

            ComputeSourceFingerprint(sourcePath, out long sourceSize, out long sourceMtimeMs);

            int nMs2 = ms2Spectra.Count;

            // Group MS2 record indices by isolation-window key, preserving acquisition
            // order within each window and first-encounter order across windows.
            // Writing each window's records contiguously lets SpectraWindowIndex
            // .LoadWindow read a whole window in one sequential run (the cold-HDD fix);
            // the acquisition-order index written after the body restores file order
            // for AllMs2Rts / first-cycle windows and for the full LoadSpectraCache read.
            var windowKeyOrder = new List<int>();
            var windowKeyToIndices = new Dictionary<int, List<int>>();
            for (int i = 0; i < nMs2; i++)
            {
                int key = WindowKey(ms2Spectra[i].IsolationWindow.Center);
                if (!windowKeyToIndices.TryGetValue(key, out var indices))
                {
                    indices = new List<int>();
                    windowKeyToIndices[key] = indices;
                    windowKeyOrder.Add(key);
                }
                indices.Add(i);
            }

            // Byte offset of each MS2 record within the grouped body, keyed by the
            // record's acquisition index -- captured as it is written, then written
            // in acquisition order into the index block.
            var recordOffsets = new long[nMs2];

            using (var saver = new FileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    // Header
                    w.Write(MAGIC);
                    w.Write(VERSION);
                    w.Write((ulong)sourceSize);
                    w.Write(sourceMtimeMs);
                    w.Write((uint)nMs2);
                    w.Write((uint)ms1Spectra.Count);

                    // MS2 records, grouped by window (contiguous per window)
                    foreach (int key in windowKeyOrder)
                    {
                        foreach (int i in windowKeyToIndices[key])
                        {
                            recordOffsets[i] = fs.Position;
                            WriteMs2Record(w, ms2Spectra[i]);
                        }
                    }

                    // MS1 records (acquisition order; every consumer reads them in full)
                    long ms1SectionOffset = fs.Position;
                    foreach (var s in ms1Spectra)
                        WriteMs1Record(w, s);

                    // Per-record index in ACQUISITION order: {offset, iso*, rt}
                    long indexOffset = fs.Position;
                    for (int i = 0; i < nMs2; i++)
                    {
                        var s = ms2Spectra[i];
                        w.Write(recordOffsets[i]);
                        w.Write(s.IsolationWindow.Center);
                        w.Write(s.IsolationWindow.LowerOffset);
                        w.Write(s.IsolationWindow.UpperOffset);
                        w.Write(s.RetentionTime);
                    }

                    // Footer at EOF: the "index at end" locators.
                    w.Write(ms1SectionOffset);
                    w.Write(indexOffset);

                    w.Flush();
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Load spectra from a binary cache file, returning MS2 in acquisition (file)
        /// order even though the body is physically window-grouped.
        /// Returns null if the file does not exist or has invalid magic/version.
        /// </summary>
        public static SpectraCacheResult LoadSpectraCache(string path, string sourcePath = null)
        {
            if (!File.Exists(path))
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // Validate magic / version / source fingerprint and read the record
                // counts. Shared with SpectraWindowIndex so the two readers
                // accept/reject a cache identically.
                if (!TryReadHeader(r, sourcePath, out uint nMs2, out uint nMs1))
                    return null;

                // Read the acquisition-order index (record offsets into the
                // window-grouped body), then decode MS2 records in ASCENDING FILE
                // OFFSET order -- one sequential forward pass over the body, so a
                // full load stays HDD-friendly -- while placing each into its
                // acquisition-order slot. This keeps the full loader (Stage-6
                // rescore) a resident load that returns spectra in the same order the
                // file-order v3 cache did, so grouping needs no Stage-6 streaming.
                SpectraCacheIndex index = ReadIndex(fs, r, nMs2);

                var ms2 = new Spectrum[nMs2];
                foreach (int i in BuildOffsetReadOrder(index.RecordOffsets))
                {
                    fs.Seek(index.RecordOffsets[i], SeekOrigin.Begin);
                    ms2[i] = ReadMs2Record(r);
                }

                // MS1 section: seek to its recorded start and read in full.
                fs.Seek(index.Ms1SectionOffset, SeekOrigin.Begin);
                var ms1 = new List<MS1Spectrum>((int)nMs1);
                for (uint i = 0; i < nMs1; i++)
                    ms1.Add(ReadMs1Record(r));

                return new SpectraCacheResult(new List<Spectrum>(ms2), ms1);
            }
        }

        /// <summary>
        /// Get the spectra cache path for a given input file.
        /// </summary>
        public static string GetCachePath(string inputFile)
        {
            // Preserve the historical filename ("{stem}.spectra.bin", the same
            // result Path.ChangeExtension produced); only the directory is
            // redirected by ArtifactPaths (beside the data file by default, else
            // the configured cache/output dir).
            string fileName = Path.GetFileNameWithoutExtension(inputFile) + ".spectra.bin";
            return Path.Combine(ArtifactPaths.ResolveCacheDir(inputFile), fileName);
        }

        #region Private helpers

        // The isolation-window grouping key: the rounded iso-center that scoring and
        // calibration use to bucket MS2 records into windows. Centralized so the
        // writer's physical grouping, the index rebuild in SpectraWindowIndex, and
        // the resident grouping all derive the identical key.
        internal static int WindowKey(double isoCenter)
        {
            return (int)Math.Round(isoCenter * 10.0);
        }

        // Read and validate the cache header (magic, version, source
        // fingerprint) and return the record counts. On success the reader is
        // left positioned at the first MS2 record; on any mismatch returns
        // false. Shared by LoadSpectraCache and SpectraWindowIndex so both
        // accept/reject a cache by identical rules.
        internal static bool TryReadHeader(BinaryReader r, string sourcePath, out uint nMs2, out uint nMs1)
        {
            nMs2 = 0;
            nMs1 = 0;

            byte[] magic = r.ReadBytes(8);
            if (magic.Length != 8)
                return false;
            for (int i = 0; i < 8; i++)
            {
                if (magic[i] != MAGIC[i])
                    return false;
            }

            uint version = r.ReadUInt32();
            if (version != VERSION)
                return false;

            // Source fingerprint: reject a cache whose data file changed since it
            // was written. Skipped when the cache recorded no fingerprint
            // (storedSize == 0) or the source is unavailable for comparison
            // (e.g. a resume run whose mzML is not beside the cache).
            ulong storedSize = r.ReadUInt64();
            long storedMtimeMs = r.ReadInt64();
            if (storedSize != 0 && !string.IsNullOrEmpty(sourcePath))
            {
                ComputeSourceFingerprint(sourcePath, out long actualSize, out long actualMtimeMs);
                if (actualSize != 0 && ((ulong)actualSize != storedSize || actualMtimeMs != storedMtimeMs))
                    return false;
            }

            nMs2 = r.ReadUInt32();
            nMs1 = r.ReadUInt32();
            return true;
        }

        // Read the EOF footer (MS1 section + index offsets) and the acquisition-order
        // index block. Shared by LoadSpectraCache (to restore acquisition order) and
        // SpectraWindowIndex (to rebuild the window map without a record walk).
        // Throws InvalidDataException on a truncated/corrupt index; callers that can
        // recover treat that as "re-parse the mzML".
        internal static SpectraCacheIndex ReadIndex(FileStream fs, BinaryReader r, uint nMs2)
        {
            if (fs.Length < FOOTER_BYTES)
                throw new InvalidDataException("Spectra cache too small to contain a v4 footer.");

            fs.Seek(-FOOTER_BYTES, SeekOrigin.End);
            long ms1SectionOffset = r.ReadInt64();
            long indexOffset = r.ReadInt64();

            var index = new SpectraCacheIndex
            {
                RecordOffsets = new long[nMs2],
                IsoCenters = new double[nMs2],
                IsoLowers = new double[nMs2],
                IsoUppers = new double[nMs2],
                Rts = new double[nMs2],
                Ms1SectionOffset = ms1SectionOffset,
            };

            fs.Seek(indexOffset, SeekOrigin.Begin);
            for (uint i = 0; i < nMs2; i++)
            {
                index.RecordOffsets[i] = r.ReadInt64();
                index.IsoCenters[i] = r.ReadDouble();
                index.IsoLowers[i] = r.ReadDouble();
                index.IsoUppers[i] = r.ReadDouble();
                index.Rts[i] = r.ReadDouble();
            }
            return index;
        }

        // Record indices ordered by ascending file offset, so a full load walks the
        // window-grouped body as one forward pass instead of seeking in acquisition
        // order (which, post-grouping, is scattered across the whole body). Stable
        // OrderBy over the index (record offsets are unique, so ties never arise);
        // Array.Sort is banned in production for cross-impl tie-stability
        // (Osprey.Test.CodeInspectionTest.TestNoUnstableSort).
        private static int[] BuildOffsetReadOrder(long[] recordOffsets)
        {
            return Enumerable.Range(0, recordOffsets.Length)
                .OrderBy(i => recordOffsets[i])
                .ToArray();
        }

        // Encode one MS2 record at the writer's current position. The inverse of
        // ReadMs2Record; the two must stay byte-for-byte inverses so a streamed
        // window load decodes identically to a full read.
        private static void WriteMs2Record(BinaryWriter w, Spectrum s)
        {
            w.Write(s.ScanNumber);
            w.Write(s.RetentionTime);
            w.Write(s.PrecursorMz);
            w.Write(s.IsolationWindow.Center);
            w.Write(s.IsolationWindow.LowerOffset);
            w.Write(s.IsolationWindow.UpperOffset);
            w.Write((uint)s.Mzs.Length);
            WriteDoubleArray(w, s.Mzs);
            WriteFloatArray(w, s.Intensities);
        }

        // Encode one MS1 record at the writer's current position. Inverse of ReadMs1Record.
        private static void WriteMs1Record(BinaryWriter w, MS1Spectrum s)
        {
            w.Write(s.ScanNumber);
            w.Write(s.RetentionTime);
            w.Write((uint)s.Mzs.Length);
            WriteDoubleArray(w, s.Mzs);
            WriteFloatArray(w, s.Intensities);
        }

        // Decode one MS2 record from a reader positioned at its start. The
        // inverse of WriteMs2Record; shared with SpectraWindowIndex.LoadWindow so a
        // streamed window decodes byte-for-byte identically to a full
        // LoadSpectraCache read.
        internal static Spectrum ReadMs2Record(BinaryReader r)
        {
            var s = new Spectrum();
            s.ScanNumber = r.ReadUInt32();
            s.RetentionTime = r.ReadDouble();
            s.PrecursorMz = r.ReadDouble();
            double isoCenter = r.ReadDouble();
            double isoLower = r.ReadDouble();
            double isoUpper = r.ReadDouble();
            s.IsolationWindow = new IsolationWindow(isoCenter, isoLower, isoUpper);
            uint nPeaks = r.ReadUInt32();
            s.Mzs = ReadDoubleArray(r, (int)nPeaks);
            s.Intensities = ReadFloatArray(r, (int)nPeaks);
            return s;
        }

        // Decode one MS1 record from a reader positioned at its start.
        internal static MS1Spectrum ReadMs1Record(BinaryReader r)
        {
            var s = new MS1Spectrum();
            s.ScanNumber = r.ReadUInt32();
            s.RetentionTime = r.ReadDouble();
            uint nPeaks = r.ReadUInt32();
            s.Mzs = ReadDoubleArray(r, (int)nPeaks);
            s.Intensities = ReadFloatArray(r, (int)nPeaks);
            return s;
        }

        // Source file fingerprint: length + last-write-time as Unix milliseconds
        // UTC. Unix-ms (not .NET ticks) so Osprey and Rust osprey derive the
        // same value for the same file. Returns (0, 0) when the source is null,
        // missing, or cannot be stat'd, which the load path treats as "no
        // fingerprint -- trust the cache".
        private static void ComputeSourceFingerprint(string sourcePath, out long size, out long mtimeMs)
        {
            size = 0;
            mtimeMs = 0;
            if (string.IsNullOrEmpty(sourcePath))
                return;
            try
            {
                var fi = new FileInfo(sourcePath);
                if (!fi.Exists)
                    return;
                size = fi.Length;
                mtimeMs = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds();
            }
            catch (Exception)
            {
                size = 0;
                mtimeMs = 0;
            }
        }

        private static void WriteDoubleArray(BinaryWriter w, double[] values)
        {
            byte[] bytes = new byte[values.Length * 8];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            w.Write(bytes);
        }

        private static void WriteFloatArray(BinaryWriter w, float[] values)
        {
            byte[] bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            w.Write(bytes);
        }

        private static double[] ReadDoubleArray(BinaryReader r, int count)
        {
            int byteCount = count * 8;
            byte[] bytes = r.ReadBytes(byteCount);
            if (bytes.Length != byteCount)
                throw new InvalidDataException("Unexpected end of spectra cache while reading double array.");
            double[] result = new double[count];
            Buffer.BlockCopy(bytes, 0, result, 0, byteCount);
            return result;
        }

        private static float[] ReadFloatArray(BinaryReader r, int count)
        {
            int byteCount = count * 4;
            byte[] bytes = r.ReadBytes(byteCount);
            if (bytes.Length != byteCount)
                throw new InvalidDataException("Unexpected end of spectra cache while reading float array.");
            float[] result = new float[count];
            Buffer.BlockCopy(bytes, 0, result, 0, byteCount);
            return result;
        }

        #endregion

        /// <summary>
        /// The per-record index read back from a v4 cache's EOF index block, in
        /// acquisition order. <see cref="RecordOffsets"/>[i] locates MS2 record i's
        /// peak blob within the window-grouped body; the iso/rt fields let a reader
        /// rebuild the window map, AllMs2Rts, and first-cycle windows without
        /// touching the body.
        /// </summary>
        internal sealed class SpectraCacheIndex
        {
            public long[] RecordOffsets;
            public double[] IsoCenters;
            public double[] IsoLowers;
            public double[] IsoUppers;
            public double[] Rts;
            public long Ms1SectionOffset;
        }
    }

    /// <summary>
    /// Result of loading a spectra cache file. Contains MS2 and MS1 spectrum lists.
    /// </summary>
    public class SpectraCacheResult
    {
        public List<Spectrum> Ms2Spectra { get; private set; }
        public List<MS1Spectrum> Ms1Spectra { get; private set; }

        public SpectraCacheResult(List<Spectrum> ms2, List<MS1Spectrum> ms1)
        {
            Ms2Spectra = ms2;
            Ms1Spectra = ms1;
        }
    }
}
