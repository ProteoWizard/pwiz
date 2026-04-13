using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Streaming mzML file parser using XmlReader.
    /// Ported from osprey-io/src/mzml/parser.rs.
    ///
    /// Parses MS1 and MS2 spectra including isolation windows, binary data arrays
    /// (base64-encoded, optionally zlib-compressed), and CV parameters.
    /// </summary>
    public class MzmlReader
    {
        #region CV Accessions

        private const string CV_MS_LEVEL = "MS:1000511";
        private const string CV_SCAN_START_TIME_MINUTES = "MS:1000016";
        private const string CV_RETENTION_TIME_SECONDS = "MS:1000894";
        private const string CV_SELECTED_ION_MZ = "MS:1000744";
        private const string CV_PEAK_INTENSITY = "MS:1000042";
        private const string CV_ISOLATION_WINDOW_TARGET = "MS:1000827";
        private const string CV_ISOLATION_WINDOW_LOWER = "MS:1000828";
        private const string CV_ISOLATION_WINDOW_UPPER = "MS:1000829";
        private const string CV_MZ_ARRAY = "MS:1000514";
        private const string CV_INTENSITY_ARRAY = "MS:1000515";
        private const string CV_64BIT_FLOAT = "MS:1000523";
        private const string CV_32BIT_FLOAT = "MS:1000521";
        private const string CV_ZLIB_COMPRESSION = "MS:1000574";
        private const string CV_NO_COMPRESSION = "MS:1000576";

        private const double DEFAULT_ISOLATION_HALF_WIDTH = 12.5;

        #endregion

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML file in a single pass.
        /// Uses a two-phase approach for parallel decompression:
        ///   1. Sequential XML parse: extract metadata + raw base64 strings
        ///   2. Parallel decode: base64 + zlib decompression across all cores
        /// </summary>
        public static MzmlResult LoadAllSpectra(string path)
        {
            // Phase 1: Sequential XML parse - extract raw data
            var rawSpectra = new List<RawSpectrumData>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 1024 * 1024)) // 1 MB buffer
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (var reader = XmlReader.Create(stream, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "spectrum")
                            continue;

                        uint spectrumIndex = 0;
                        string indexAttr = reader.GetAttribute("index");
                        if (indexAttr != null)
                            uint.TryParse(indexAttr, out spectrumIndex);

                        var raw = ParseSpectrumRaw(reader, spectrumIndex);
                        if (raw != null)
                            rawSpectra.Add(raw);
                    }
                }
            }

            // Phase 2: Parallel decode - base64 + zlib decompression
            var decoded = new DecodedSpectrum[rawSpectra.Count];
            System.Threading.Tasks.Parallel.For(0, rawSpectra.Count, i =>
            {
                decoded[i] = DecodeSpectrum(rawSpectra[i]);
            });

            // Phase 3: Collect into typed lists (sequential, fast)
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();
            for (int i = 0; i < decoded.Length; i++)
            {
                var d = decoded[i];
                if (d == null) continue;
                if (d.MsLevel == 1)
                    ms1Spectra.Add(d.ToMs1Spectrum());
                else if (d.MsLevel == 2)
                {
                    var s = d.ToMs2Spectrum();
                    if (s != null) ms2Spectra.Add(s);
                }
            }

            return new MzmlResult(ms2Spectra, ms1Spectra);
        }

        /// <summary>
        /// Legacy single-pass loader (sequential). Kept for compatibility.
        /// </summary>
        public static MzmlResult LoadAllSpectraSequential(string path)
        {
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (var reader = XmlReader.Create(stream, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "spectrum")
                            continue;

                        uint spectrumIndex = 0;
                        string indexAttr = reader.GetAttribute("index");
                        if (indexAttr != null)
                            uint.TryParse(indexAttr, out spectrumIndex);

                        ParseSpectrumElement(reader, spectrumIndex, ms2Spectra, ms1Spectra);
                    }
                }
            }

            return new MzmlResult(ms2Spectra, ms1Spectra);
        }

        #region Spectrum Parsing

        private static void ParseSpectrumElement(XmlReader reader, uint spectrumIndex,
            List<Spectrum> ms2List, List<MS1Spectrum> ms1List)
        {
            int msLevel = 0;
            double retentionTime = 0.0;
            double precursorMz = 0.0;
            double precursorIntensity = 0.0;
            double isoTarget = 0.0;
            double isoLower = 0.0;
            double isoUpper = 0.0;
            bool hasPrecursor = false;
            bool hasIsolationWindow = false;

            var binaryArrays = new List<BinaryArrayInfo>();

            // Read the subtree of this <spectrum> element
            using (var subtree = reader.ReadSubtree())
            {
                string currentContext = null; // track parent context

                while (subtree.Read())
                {
                    if (subtree.NodeType != XmlNodeType.Element)
                    {
                        if (subtree.NodeType == XmlNodeType.EndElement)
                        {
                            if (subtree.LocalName == "binaryDataArray")
                                currentContext = null;
                        }
                        continue;
                    }

                    string localName = subtree.LocalName;

                    if (localName == "cvParam")
                    {
                        string accession = subtree.GetAttribute("accession");
                        string value = subtree.GetAttribute("value");

                        if (accession == null)
                            continue;

                        if (currentContext == "binaryDataArray")
                        {
                            // Binary data array CV params
                            var currentArray = binaryArrays.Count > 0
                                ? binaryArrays[binaryArrays.Count - 1]
                                : null;
                            if (currentArray != null)
                                ApplyBinaryArrayCvParam(currentArray, accession);
                        }
                        else if (currentContext == "selectedIon")
                        {
                            if (accession == CV_SELECTED_ION_MZ && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out precursorMz);
                            else if (accession == CV_PEAK_INTENSITY && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out precursorIntensity);
                        }
                        else if (currentContext == "isolationWindow")
                        {
                            if (accession == CV_ISOLATION_WINDOW_TARGET && value != null)
                            {
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out isoTarget);
                                hasIsolationWindow = true;
                            }
                            else if (accession == CV_ISOLATION_WINDOW_LOWER && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out isoLower);
                            else if (accession == CV_ISOLATION_WINDOW_UPPER && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out isoUpper);
                        }
                        else if (currentContext == "scan")
                        {
                            if (accession == CV_SCAN_START_TIME_MINUTES && value != null)
                            {
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out retentionTime);
                                // Check unitName for seconds
                                string unitName = subtree.GetAttribute("unitName");
                                if (unitName != null && unitName.IndexOf("second", StringComparison.OrdinalIgnoreCase) >= 0)
                                    retentionTime /= 60.0;
                            }
                            else if (accession == CV_RETENTION_TIME_SECONDS && value != null)
                            {
                                double seconds;
                                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out seconds))
                                    retentionTime = seconds / 60.0;
                            }
                            else if (accession == CV_MS_LEVEL && value != null)
                                int.TryParse(value, out msLevel);
                        }
                        else
                        {
                            // Top-level spectrum cvParams
                            if (accession == CV_MS_LEVEL && value != null)
                                int.TryParse(value, out msLevel);
                        }
                    }
                    else if (localName == "scan")
                    {
                        currentContext = "scan";
                    }
                    else if (localName == "precursor")
                    {
                        hasPrecursor = true;
                    }
                    else if (localName == "selectedIon")
                    {
                        currentContext = "selectedIon";
                    }
                    else if (localName == "isolationWindow")
                    {
                        currentContext = "isolationWindow";
                    }
                    else if (localName == "binaryDataArray")
                    {
                        currentContext = "binaryDataArray";
                        binaryArrays.Add(new BinaryArrayInfo());
                    }
                    else if (localName == "binary" && currentContext == "binaryDataArray")
                    {
                        var currentArray = binaryArrays.Count > 0
                            ? binaryArrays[binaryArrays.Count - 1]
                            : null;
                        if (currentArray != null)
                        {
                            string base64 = subtree.ReadElementContentAsString();
                            if (!string.IsNullOrEmpty(base64))
                                currentArray.Base64Data = base64;
                        }
                    }
                }
            }

            // Decode binary arrays
            double[] mzArray = null;
            float[] intensityArray = null;

            foreach (var arrayInfo in binaryArrays)
            {
                if (arrayInfo.Base64Data == null)
                    continue;

                if (arrayInfo.IsMzArray)
                    mzArray = DecodeBinaryArrayAsDouble(arrayInfo);
                else if (arrayInfo.IsIntensityArray)
                    intensityArray = DecodeBinaryArrayAsFloat(arrayInfo);
            }

            if (mzArray == null) mzArray = new double[0];
            if (intensityArray == null) intensityArray = new float[0];

            // Build spectrum based on MS level
            if (msLevel == 1)
            {
                ms1List.Add(new MS1Spectrum
                {
                    ScanNumber = spectrumIndex,
                    RetentionTime = retentionTime,
                    Mzs = mzArray,
                    Intensities = intensityArray,
                });
            }
            else if (msLevel == 2 && hasPrecursor)
            {
                // Use selected ion m/z as center if no isolation window target
                double center = hasIsolationWindow && isoTarget > 0 ? isoTarget : precursorMz;
                if (center <= 0)
                    return; // Skip spectra without precursor info

                double lowerOffset = isoLower > 0 ? isoLower : DEFAULT_ISOLATION_HALF_WIDTH;
                double upperOffset = isoUpper > 0 ? isoUpper : DEFAULT_ISOLATION_HALF_WIDTH;

                var isoWindow = new IsolationWindow(center, lowerOffset, upperOffset);

                ms2List.Add(new Spectrum
                {
                    ScanNumber = spectrumIndex,
                    RetentionTime = retentionTime,
                    PrecursorMz = precursorMz > 0 ? precursorMz : center,
                    IsolationWindow = isoWindow,
                    Mzs = mzArray,
                    Intensities = intensityArray,
                });
            }
        }

        #endregion

        #region Binary Data Decoding

        private static void ApplyBinaryArrayCvParam(BinaryArrayInfo info, string accession)
        {
            switch (accession)
            {
                case CV_MZ_ARRAY:
                    info.IsMzArray = true;
                    break;
                case CV_INTENSITY_ARRAY:
                    info.IsIntensityArray = true;
                    break;
                case CV_64BIT_FLOAT:
                    info.Is64Bit = true;
                    break;
                case CV_32BIT_FLOAT:
                    info.Is64Bit = false;
                    break;
                case CV_ZLIB_COMPRESSION:
                    info.IsZlibCompressed = true;
                    break;
                case CV_NO_COMPRESSION:
                    info.IsZlibCompressed = false;
                    break;
            }
        }

        private static byte[] DecodeAndDecompress(BinaryArrayInfo info)
        {
            byte[] raw = Convert.FromBase64String(info.Base64Data);

            if (!info.IsZlibCompressed)
                return raw;

            // Zlib format: skip 2-byte header, use DeflateStream
            if (raw.Length < 2)
                return new byte[0];

            using (var input = new MemoryStream(raw, 2, raw.Length - 2))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static double[] DecodeBinaryArrayAsDouble(BinaryArrayInfo info)
        {
            byte[] bytes = DecodeAndDecompress(info);

            if (info.Is64Bit)
            {
                int count = bytes.Length / 8;
                double[] result = new double[count];
                Buffer.BlockCopy(bytes, 0, result, 0, count * 8);
                return result;
            }
            else
            {
                // 32-bit float -> promote to double
                int count = bytes.Length / 4;
                float[] temp = new float[count];
                Buffer.BlockCopy(bytes, 0, temp, 0, count * 4);
                double[] result = new double[count];
                for (int i = 0; i < count; i++)
                    result[i] = temp[i];
                return result;
            }
        }

        private static float[] DecodeBinaryArrayAsFloat(BinaryArrayInfo info)
        {
            byte[] bytes = DecodeAndDecompress(info);

            if (info.Is64Bit)
            {
                // 64-bit float -> demote to float
                int count = bytes.Length / 8;
                double[] temp = new double[count];
                Buffer.BlockCopy(bytes, 0, temp, 0, count * 8);
                float[] result = new float[count];
                for (int i = 0; i < count; i++)
                    result[i] = (float)temp[i];
                return result;
            }
            else
            {
                int count = bytes.Length / 4;
                float[] result = new float[count];
                Buffer.BlockCopy(bytes, 0, result, 0, count * 4);
                return result;
            }
        }

        #endregion

        #region Phase 1: Raw XML parsing (sequential)

        /// <summary>
        /// Parse a spectrum element into raw metadata + base64 strings.
        /// No binary decoding or decompression happens here.
        /// </summary>
        private static RawSpectrumData ParseSpectrumRaw(XmlReader reader, uint spectrumIndex)
        {
            var raw = new RawSpectrumData { Index = spectrumIndex };
            var binaryArrays = new List<BinaryArrayInfo>();

            using (var subtree = reader.ReadSubtree())
            {
                string currentContext = null;

                while (subtree.Read())
                {
                    if (subtree.NodeType != XmlNodeType.Element)
                    {
                        if (subtree.NodeType == XmlNodeType.EndElement && subtree.LocalName == "binaryDataArray")
                            currentContext = null;
                        continue;
                    }

                    string localName = subtree.LocalName;

                    if (localName == "cvParam")
                    {
                        string accession = subtree.GetAttribute("accession");
                        string value = subtree.GetAttribute("value");
                        if (accession == null) continue;

                        if (currentContext == "binaryDataArray")
                        {
                            var cur = binaryArrays.Count > 0 ? binaryArrays[binaryArrays.Count - 1] : null;
                            if (cur != null) ApplyBinaryArrayCvParam(cur, accession);
                        }
                        else if (currentContext == "selectedIon")
                        {
                            if (accession == CV_SELECTED_ION_MZ && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out raw.PrecursorMz);
                        }
                        else if (currentContext == "isolationWindow")
                        {
                            if (accession == CV_ISOLATION_WINDOW_TARGET && value != null)
                            {
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out raw.IsoTarget);
                                raw.HasIsolationWindow = true;
                            }
                            else if (accession == CV_ISOLATION_WINDOW_LOWER && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out raw.IsoLower);
                            else if (accession == CV_ISOLATION_WINDOW_UPPER && value != null)
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out raw.IsoUpper);
                        }
                        else if (currentContext == "scan")
                        {
                            if (accession == CV_SCAN_START_TIME_MINUTES && value != null)
                            {
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out raw.RetentionTime);
                                string unitName = subtree.GetAttribute("unitName");
                                if (unitName != null && unitName.IndexOf("second", StringComparison.OrdinalIgnoreCase) >= 0)
                                    raw.RetentionTime /= 60.0;
                            }
                            else if (accession == CV_RETENTION_TIME_SECONDS && value != null)
                            {
                                double seconds;
                                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out seconds))
                                    raw.RetentionTime = seconds / 60.0;
                            }
                            else if (accession == CV_MS_LEVEL && value != null)
                                int.TryParse(value, out raw.MsLevel);
                        }
                        else
                        {
                            if (accession == CV_MS_LEVEL && value != null)
                                int.TryParse(value, out raw.MsLevel);
                        }
                    }
                    else if (localName == "scan") currentContext = "scan";
                    else if (localName == "precursor") raw.HasPrecursor = true;
                    else if (localName == "selectedIon") currentContext = "selectedIon";
                    else if (localName == "isolationWindow") currentContext = "isolationWindow";
                    else if (localName == "binaryDataArray")
                    {
                        currentContext = "binaryDataArray";
                        binaryArrays.Add(new BinaryArrayInfo());
                    }
                    else if (localName == "binary" && currentContext == "binaryDataArray")
                    {
                        var cur = binaryArrays.Count > 0 ? binaryArrays[binaryArrays.Count - 1] : null;
                        if (cur != null)
                        {
                            string base64 = subtree.ReadElementContentAsString();
                            if (!string.IsNullOrEmpty(base64))
                                cur.Base64Data = base64;
                        }
                    }
                }
            }

            raw.BinaryArrays = binaryArrays;
            if (raw.MsLevel == 0) return null; // unknown MS level
            return raw;
        }

        #endregion

        #region Phase 2: Parallel binary decoding

        /// <summary>
        /// Decode base64 + decompress zlib for a single spectrum.
        /// Thread-safe - no shared mutable state.
        /// </summary>
        private static DecodedSpectrum DecodeSpectrum(RawSpectrumData raw)
        {
            double[] mzArray = null;
            float[] intensityArray = null;

            foreach (var arrayInfo in raw.BinaryArrays)
            {
                if (arrayInfo.Base64Data == null) continue;
                if (arrayInfo.IsMzArray)
                    mzArray = DecodeBinaryArrayAsDouble(arrayInfo);
                else if (arrayInfo.IsIntensityArray)
                    intensityArray = DecodeBinaryArrayAsFloat(arrayInfo);
            }

            return new DecodedSpectrum
            {
                Index = raw.Index,
                MsLevel = raw.MsLevel,
                RetentionTime = raw.RetentionTime,
                PrecursorMz = raw.PrecursorMz,
                IsoTarget = raw.IsoTarget,
                IsoLower = raw.IsoLower,
                IsoUpper = raw.IsoUpper,
                HasPrecursor = raw.HasPrecursor,
                HasIsolationWindow = raw.HasIsolationWindow,
                MzArray = mzArray ?? new double[0],
                IntensityArray = intensityArray ?? new float[0],
            };
        }

        #endregion

        #region Helper Types

        private class BinaryArrayInfo
        {
            public bool IsMzArray;
            public bool IsIntensityArray;
            public bool Is64Bit;
            public bool IsZlibCompressed;
            public string Base64Data;
        }

        /// <summary>Raw spectrum data from Phase 1 (metadata + base64 strings).</summary>
        private class RawSpectrumData
        {
            public uint Index;
            public int MsLevel;
            public double RetentionTime;
            public double PrecursorMz;
            public double IsoTarget, IsoLower, IsoUpper;
            public bool HasPrecursor, HasIsolationWindow;
            public List<BinaryArrayInfo> BinaryArrays;
        }

        /// <summary>Decoded spectrum from Phase 2 (typed arrays ready for use).</summary>
        private class DecodedSpectrum
        {
            public uint Index;
            public int MsLevel;
            public double RetentionTime;
            public double PrecursorMz;
            public double IsoTarget, IsoLower, IsoUpper;
            public bool HasPrecursor, HasIsolationWindow;
            public double[] MzArray;
            public float[] IntensityArray;

            public MS1Spectrum ToMs1Spectrum()
            {
                return new MS1Spectrum
                {
                    ScanNumber = Index,
                    RetentionTime = RetentionTime,
                    Mzs = MzArray,
                    Intensities = IntensityArray,
                };
            }

            public Spectrum ToMs2Spectrum()
            {
                if (!HasPrecursor) return null;
                double center = HasIsolationWindow && IsoTarget > 0 ? IsoTarget : PrecursorMz;
                if (center <= 0) return null;

                double lowerOffset = IsoLower > 0 ? IsoLower : DEFAULT_ISOLATION_HALF_WIDTH;
                double upperOffset = IsoUpper > 0 ? IsoUpper : DEFAULT_ISOLATION_HALF_WIDTH;

                return new Spectrum
                {
                    ScanNumber = Index,
                    RetentionTime = RetentionTime,
                    PrecursorMz = PrecursorMz > 0 ? PrecursorMz : center,
                    IsolationWindow = new IsolationWindow(center, lowerOffset, upperOffset),
                    Mzs = MzArray,
                    Intensities = IntensityArray,
                };
            }
        }

        #endregion
    }

    /// <summary>
    /// Result of loading spectra from an mzML file.
    /// Contains separate lists for MS2 and MS1 spectra.
    /// </summary>
    public class MzmlResult
    {
        public List<Spectrum> Ms2Spectra { get; private set; }
        public List<MS1Spectrum> Ms1Spectra { get; private set; }

        public MzmlResult(List<Spectrum> ms2, List<MS1Spectrum> ms1)
        {
            Ms2Spectra = ms2;
            Ms1Spectra = ms1;
        }
    }
}
