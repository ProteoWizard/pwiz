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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Binary spectra cache for fast re-loading of parsed MS2 and MS1 spectra.
    /// After parsing mzML (which can take minutes for large files), this writes a compact
    /// .spectra.bin file that can be reloaded in seconds.
    /// Ported from osprey-io/src/mzml/spectra_cache.rs.
    ///
    /// Format (little-endian):
    /// [magic: 8 bytes "OSPRSPC\0"]
    /// [version: uint32]
    /// [source_size: uint64]   source file length, or 0 when unknown
    /// [source_mtime: int64]   source last-write time, Unix milliseconds UTC,
    ///                         or 0 when unknown. Unix-ms so Osprey and
    ///                         Rust osprey compute identical values for the same
    ///                         file and can share one cache (cross-impl gate).
    /// [n_ms2: uint32]
    /// [n_ms1: uint32]
    /// For each MS2 spectrum:
    ///   [scan_number: uint32] [retention_time: float64] [precursor_mz: float64]
    ///   [iso_center: float64] [iso_lower: float64] [iso_upper: float64]
    ///   [n_peaks: uint32] [mzs: float64 x n_peaks] [intensities: float32 x n_peaks]
    /// For each MS1 spectrum:
    ///   [scan_number: uint32] [retention_time: float64]
    ///   [n_peaks: uint32] [mzs: float64 x n_peaks] [intensities: float32 x n_peaks]
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
        private const uint VERSION = 3;

        // Fixed byte layout of one MS2 record's header prefix (48 bytes, everything
        // before the variable-length peak blob): scan(4) + rt(8) + precursor_mz(8) +
        // iso_center(8) + iso_lower(8) + iso_upper(8) + n_peaks(4). SpectraWindowIndex
        // reads these fields individually during its header-only pass, then skips the
        // peak blob using PEAK_BYTES_PER_POINT.
        // Bytes per peak in a record's blob: one f64 m/z (8) + one f32 intensity (4).
        internal const int PEAK_BYTES_PER_POINT = 12;

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
                    w.Write((uint)ms2Spectra.Count);
                    w.Write((uint)ms1Spectra.Count);

                    // MS2 spectra
                    foreach (var s in ms2Spectra)
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

                    // MS1 spectra
                    foreach (var s in ms1Spectra)
                    {
                        w.Write(s.ScanNumber);
                        w.Write(s.RetentionTime);
                        w.Write((uint)s.Mzs.Length);
                        WriteDoubleArray(w, s.Mzs);
                        WriteFloatArray(w, s.Intensities);
                    }

                    w.Flush();
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Load spectra from a binary cache file.
        /// Returns null if the file does not exist or has invalid magic/version.
        /// </summary>
        public static SpectraCacheResult LoadSpectraCache(string path, string sourcePath = null)
        {
            if (!File.Exists(path))
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // Validate magic / version / source fingerprint and read the
                // record counts. Shared with SpectraWindowIndex so the two
                // readers accept/reject a cache identically. On success the
                // reader is positioned at the first MS2 record.
                if (!TryReadHeader(r, sourcePath, out uint nMs2, out uint nMs1))
                    return null;

                // Read MS2 spectra
                var ms2 = new List<Spectrum>((int)nMs2);
                for (uint i = 0; i < nMs2; i++)
                    ms2.Add(ReadMs2Record(r));

                // Read MS1 spectra
                var ms1 = new List<MS1Spectrum>((int)nMs1);
                for (uint i = 0; i < nMs1; i++)
                    ms1.Add(ReadMs1Record(r));

                return new SpectraCacheResult(ms2, ms1);
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

        // Decode one MS2 record from a reader positioned at its start. The
        // inverse of the MS2 write loop in SaveSpectraCache; shared with
        // SpectraWindowIndex.LoadWindow so a streamed window decodes
        // byte-for-byte identically to a full LoadSpectraCache read.
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
