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
        /// </summary>
        public static MzmlResult LoadAllSpectra(string path)
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

        #region Helper Types

        private class BinaryArrayInfo
        {
            public bool IsMzArray;
            public bool IsIntensityArray;
            public bool Is64Bit;
            public bool IsZlibCompressed;
            public string Base64Data;
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
