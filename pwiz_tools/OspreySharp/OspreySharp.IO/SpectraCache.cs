using System;
using System.Collections.Generic;
using System.IO;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
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
        private const uint VERSION = 1;

        /// <summary>
        /// Save spectra to a binary cache file.
        /// </summary>
        public static void SaveSpectraCache(string path, List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra)
        {
            if (ms2Spectra == null) ms2Spectra = new List<Spectrum>();
            if (ms1Spectra == null) ms1Spectra = new List<MS1Spectrum>();

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                // Header
                w.Write(MAGIC);
                w.Write(VERSION);
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
        }

        /// <summary>
        /// Load spectra from a binary cache file.
        /// Returns null if the file does not exist or has invalid magic/version.
        /// </summary>
        public static SpectraCacheResult LoadSpectraCache(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                // Validate magic
                byte[] magic = r.ReadBytes(8);
                if (magic.Length != 8)
                    return null;
                for (int i = 0; i < 8; i++)
                {
                    if (magic[i] != MAGIC[i])
                        return null;
                }

                // Validate version
                uint version = r.ReadUInt32();
                if (version != VERSION)
                    return null;

                uint nMs2 = r.ReadUInt32();
                uint nMs1 = r.ReadUInt32();

                // Read MS2 spectra
                var ms2 = new List<Spectrum>((int)nMs2);
                for (uint i = 0; i < nMs2; i++)
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
                    ms2.Add(s);
                }

                // Read MS1 spectra
                var ms1 = new List<MS1Spectrum>((int)nMs1);
                for (uint i = 0; i < nMs1; i++)
                {
                    var s = new MS1Spectrum();
                    s.ScanNumber = r.ReadUInt32();
                    s.RetentionTime = r.ReadDouble();
                    uint nPeaks = r.ReadUInt32();
                    s.Mzs = ReadDoubleArray(r, (int)nPeaks);
                    s.Intensities = ReadFloatArray(r, (int)nPeaks);
                    ms1.Add(s);
                }

                return new SpectraCacheResult(ms2, ms1);
            }
        }

        /// <summary>
        /// Get the spectra cache path for a given input file.
        /// </summary>
        public static string GetCachePath(string inputFile)
        {
            return Path.ChangeExtension(inputFile, "spectra.bin");
        }

        #region Private helpers

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
            byte[] bytes = r.ReadBytes(count * 8);
            double[] result = new double[count];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        private static float[] ReadFloatArray(BinaryReader r, int count)
        {
            byte[] bytes = r.ReadBytes(count * 4);
            float[] result = new float[count];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
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
