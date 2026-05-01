using System.Globalization;
using System.Text;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;
using SystemEncoding = System.Text.Encoding;

namespace Pwiz.Data.MsData.MzXml;

/// <summary>
/// Serializes an <see cref="MSData"/> to mzXML 3.2 XML.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::Serializer_mzXML (write path). mzXML 3.2 is a flat-attribute format
/// (one <c>&lt;scan&gt;</c> per spectrum) that predates mzML; binary data is base64-encoded
/// big-endian doubles (or floats) optionally zlib-compressed, with m/z and intensity interleaved
/// as pairs in a single <c>&lt;peaks&gt;</c> element.
/// <para>
/// Skips spectra with non-default sourceFile and (for Thermo) non-MS controllers, matching cpp.
/// Emits an <c>&lt;index&gt;</c> + <c>&lt;indexOffset&gt;</c> + <c>&lt;sha1&gt;</c> footer when
/// <see cref="Indexed"/> is true.
/// </para>
/// </remarks>
#pragma warning disable CA1001 // _stream is a per-call wrapper disposed inside Write.
public sealed class MzxmlWriter
#pragma warning restore CA1001
{
    private readonly BinaryEncoderConfig _encoderConfig;

    private HashingCountingStream? _stream;
    private List<(int ScanNumber, long Offset)>? _scanOffsets;

    /// <summary>When true, emit the <c>&lt;index&gt;</c> + <c>&lt;indexOffset&gt;</c> footer.
    /// Always emit a <c>&lt;sha1&gt;</c> trailer regardless (matches cpp).</summary>
    public bool Indexed { get; set; } = true;

    /// <summary>Optional listener registry that receives <see cref="IterationUpdate"/> messages
    /// once per scan during the scan-list write loop.</summary>
    public IterationListenerRegistry? IterationListenerRegistry { get; set; }

    /// <summary>Creates a writer that encodes binary arrays per <paramref name="encoderConfig"/>.</summary>
    /// <remarks>
    /// mzXML always uses big-endian byte order regardless of caller config; we clone the config
    /// and force <see cref="BinaryByteOrder.BigEndian"/> internally.
    /// </remarks>
    public MzxmlWriter(BinaryEncoderConfig? encoderConfig = null)
    {
        _encoderConfig = (encoderConfig ?? new BinaryEncoderConfig()).Clone();
        _encoderConfig.ByteOrder = BinaryByteOrder.BigEndian;
        // Numpress doesn't apply to mzXML's interleaved <peaks> stream.
        _encoderConfig.Numpress = BinaryNumpress.None;
    }

    /// <summary>Writes <paramref name="msd"/> as mzXML XML to a new string.</summary>
    public string Write(MSData msd)
    {
        using var ms = new MemoryStream();
        Write(msd, ms);
        return SystemEncoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Writes <paramref name="msd"/> as mzXML XML to <paramref name="stream"/>.</summary>
    public void Write(MSData msd, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(stream);

        _stream = new HashingCountingStream(stream);
        _scanOffsets = new List<(int, long)>();
        try
        {
            using var w = CreateWriter(_stream);
            WriteDocument(msd, w);
        }
        finally
        {
            _stream.Dispose();
            _stream = null;
            _scanOffsets = null;
        }
    }

    private static XmlWriter CreateWriter(Stream s) =>
        XmlWriter.Create(s, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            // cpp emits ISO-8859-1 in the declaration; sharp uses UTF-8 as recommended by the
            // spec author's own TODO comment in Serializer_mzXML.cpp (and to avoid encoding
            // mismatches with non-ASCII metadata).
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            ConformanceLevel = ConformanceLevel.Document,
            CloseOutput = false,
        });

    private void WriteDocument(MSData msd, XmlWriter w)
    {
        w.WriteStartDocument();

        w.WriteStartElement("mzXML", "http://sashimi.sourceforge.net/schema_revision/mzXML_3.2");
        w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        w.WriteAttributeString("xsi", "schemaLocation", null,
            "http://sashimi.sourceforge.net/schema_revision/mzXML_3.2 http://sashimi.sourceforge.net/schema_revision/mzXML_3.2/mzXML_idx_3.2.xsd");

        WriteMsRun(msd, w);

        w.Flush();
        long indexOffset = _stream!.BytesWritten;
        if (Indexed && _scanOffsets!.Count > 0)
        {
            WriteIndex(w);
            w.WriteStartElement("indexOffset");
            w.WriteString(indexOffset.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        // Cpp always emits <sha1>, even without an index. Snapshot SHA-1 of bytes written so far,
        // stop hashing, then emit the hex digest.
        w.WriteStartElement("sha1");
        w.Flush();
        byte[] digest = _stream.GetCurrentHash();
        _stream.StopHashing();
        w.WriteString(ToHex(digest));
        w.WriteEndElement();

        w.WriteEndElement(); // mzXML
        w.WriteEndDocument();
    }

    private void WriteMsRun(MSData msd, XmlWriter w)
    {
        var sl = msd.Run.SpectrumList;
        int scanCount = sl?.Count ?? 0;
        string startTime = "PT0S";
        string? endTime = null;
        if (sl is not null && sl.Count > 0)
        {
            try { startTime = GetRetentionTimeAttr(sl.GetSpectrum(0, getBinaryData: false)) ?? "PT0S"; }
            catch { /* ignore — fallback to PT0S */ }
            try { endTime = GetRetentionTimeAttr(sl.GetSpectrum(sl.Count - 1, getBinaryData: false)); }
            catch { /* ignore */ }
        }

        w.WriteStartElement("msRun");
        w.WriteAttributeString("scanCount", scanCount.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("startTime", startTime);
        if (!string.IsNullOrEmpty(endTime))
            w.WriteAttributeString("endTime", endTime);

        WriteParentFiles(w, msd);
        var instrumentIndex = WriteInstruments(w, msd);
        WriteDataProcessing(w, msd);
        WriteScans(w, msd, instrumentIndex);

        w.WriteEndElement(); // msRun
    }

    // ---------- parentFile ----------

    private static void WriteParentFiles(XmlWriter w, MSData msd)
    {
        foreach (var sf in msd.FileDescription.SourceFiles)
        {
            CVID sourceFileType = sf.CvParamChild(CVID.MS_mass_spectrometer_file_format).Cvid;
            if (sourceFileType == CVID.CVID_Unknown) continue;
            CVID nativeIdFormat = sf.CvParamChild(CVID.MS_nativeID_format).Cvid;
            if (nativeIdFormat == CVID.MS_no_nativeID_format) continue;

            string fileName = (string.IsNullOrEmpty(sf.Location) ? string.Empty : sf.Location.TrimEnd('/', '\\') + "/")
                              + sf.Name;
            string fileType = nativeIdFormat switch
            {
                // Processed-data formats — others count as raw.
                CVID.MS_scan_number_only_nativeID_format
                    or CVID.MS_spectrum_identifier_nativeID_format
                    or CVID.MS_multiple_peak_list_nativeID_format
                    or CVID.MS_single_peak_list_nativeID_format => "processedData",
                _ => "RAWData",
            };
            string sha1 = sf.CvParam(CVID.MS_SHA_1).Value;

            w.WriteStartElement("parentFile");
            w.WriteAttributeString("fileName", fileName);
            w.WriteAttributeString("fileType", fileType);
            w.WriteAttributeString("fileSha1", sha1);
            w.WriteEndElement();
        }
    }

    // ---------- msInstrument ----------

    private static Dictionary<InstrumentConfiguration, int> WriteInstruments(XmlWriter w, MSData msd)
    {
        var index = new Dictionary<InstrumentConfiguration, int>();
        foreach (var ic in msd.InstrumentConfigurations)
        {
            int n = index.Count + 1;
            index[ic] = n;
            w.WriteStartElement("msInstrument");
            w.WriteAttributeString("msInstrumentID", n.ToString(CultureInfo.InvariantCulture));

            // msManufacturer + msModel: derive from MS_instrument_model child term.
            var modelParam = FindParam(ic, CVID.MS_instrument_model);
            string manufacturer = ResolveManufacturer(modelParam.Cvid);
            string model = modelParam.IsEmpty ? "" : modelParam.Name;
            WriteCategoryValue(w, "msManufacturer", manufacturer);
            WriteCategoryValue(w, "msModel", model);

            // Components: pick first source / analyzer / detector.
            string ionisation = FirstComponentName(ic, ComponentType.Source);
            string analyzer = FirstComponentName(ic, ComponentType.Analyzer);
            string detector = FirstComponentName(ic, ComponentType.Detector);
            if (!string.IsNullOrEmpty(ionisation)) WriteCategoryValue(w, "msIonisation", ionisation);
            if (!string.IsNullOrEmpty(analyzer)) WriteCategoryValue(w, "msMassAnalyzer", analyzer);
            if (!string.IsNullOrEmpty(detector)) WriteCategoryValue(w, "msDetector", detector);

            if (ic.Software is not null)
                WriteSoftware(w, ic.Software, "acquisition");

            w.WriteEndElement(); // msInstrument
        }
        return index;
    }

    private static void WriteCategoryValue(XmlWriter w, string category, string value)
    {
        w.WriteStartElement(category);
        w.WriteAttributeString("category", category);
        w.WriteAttributeString("value", value);
        w.WriteEndElement();
    }

    private static void WriteSoftware(XmlWriter w, Software sw, string type)
    {
        w.WriteStartElement("software");
        w.WriteAttributeString("type", type);
        // Name comes from a CVParam child of MS_software (e.g. MS_pwiz, MS_Xcalibur).
        var nameParam = FindParam(sw, CVID.MS_software);
        w.WriteAttributeString("name", nameParam.IsEmpty ? sw.Id : nameParam.Name);
        w.WriteAttributeString("version", sw.Version ?? "");
        w.WriteEndElement();
    }

    // ---------- dataProcessing ----------

    private static void WriteDataProcessing(XmlWriter w, MSData msd)
    {
        foreach (var dp in msd.DataProcessings)
        {
            foreach (var pm in dp.ProcessingMethods)
            {
                w.WriteStartElement("dataProcessing");
                if (pm.HasCVParamChild(CVID.MS_peak_picking)) w.WriteAttributeString("centroided", "1");
                if (pm.HasCVParamChild(CVID.MS_deisotoping)) w.WriteAttributeString("deisotoped", "1");
                if (pm.HasCVParamChild(CVID.MS_charge_deconvolution)) w.WriteAttributeString("chargeDeconvoluted", "1");
                if (pm.HasCVParamChild(CVID.MS_thresholding))
                {
                    var threshold = pm.CvParam(CVID.MS_low_intensity_threshold);
                    if (!threshold.IsEmpty)
                        w.WriteAttributeString("intensityCutoff", threshold.Value);
                }

                bool isConversion = pm.HasCVParamChild(CVID.MS_file_format_conversion);
                if (pm.Software is not null)
                    WriteSoftware(w, pm.Software, isConversion ? "conversion" : "processing");

                foreach (var actionParam in pm.CvParamChildren(CVID.MS_data_transformation))
                {
                    w.WriteStartElement("processingOperation");
                    w.WriteAttributeString("name", actionParam.Name);
                    w.WriteEndElement();
                }

                foreach (var up in pm.UserParams)
                {
                    w.WriteStartElement("comment");
                    w.WriteString(string.IsNullOrEmpty(up.Value) ? up.Name : up.Name + ": " + up.Value);
                    w.WriteEndElement();
                }

                w.WriteEndElement(); // dataProcessing
            }
        }
    }

    // ---------- scans ----------

    private void WriteScans(XmlWriter w, MSData msd, IReadOnlyDictionary<InstrumentConfiguration, int> instrumentIndex)
    {
        var sl = msd.Run.SpectrumList;
        if (sl is null) return;

        CVID nativeIdFormat = GetDefaultNativeIdFormat(msd);
        var defaultSourceFile = msd.Run.DefaultSourceFile;

        int count = sl.Count;
        var registry = IterationListenerRegistry;
        for (int i = 0; i < count; i++)
        {
            Spectrum spec;
            try { spec = sl.GetSpectrum(i, getBinaryData: true); }
            catch { registry?.Broadcast(new IterationUpdate(i, count, "writing spectra")); continue; }

            // Skip Thermo non-MS spectra (cpp Serializer_mzXML.cpp:775-777).
            if (nativeIdFormat == CVID.MS_Thermo_nativeID_format
                && !spec.Id.StartsWith("controllerType=0 controllerNumber=1", StringComparison.Ordinal))
            { registry?.Broadcast(new IterationUpdate(i, count, "writing spectra")); continue; }

            // Skip spectra from a non-default source file.
            if (spec.SourceFile is not null && defaultSourceFile is not null
                && !ReferenceEquals(spec.SourceFile, defaultSourceFile))
            { registry?.Broadcast(new IterationUpdate(i, count, "writing spectra")); continue; }

            int scanNumber = ResolveScanNumber(nativeIdFormat, spec);

            // Record offset before the start tag of <scan>. RecordOffset emits the indent
            // whitespace and flushes so position points at the upcoming '<'.
            RecordOffset(w, scanNumber, indentDepth: 2);

            WriteScan(w, spec, scanNumber, instrumentIndex);
            registry?.Broadcast(new IterationUpdate(i, count, "writing spectra"));
        }
    }

    private static int ResolveScanNumber(CVID nativeIdFormat, Spectrum spec)
    {
        // mzXML "num" — if the source format has no scan number (e.g. MGF "index="), fall back to
        // (spectrum.Index + 1). Otherwise translate the nativeID.
        if (nativeIdFormat == CVID.MS_multiple_peak_list_nativeID_format)
            return spec.Index + 1;
        string s = Id.TranslateNativeIdToScanNumber(nativeIdFormat, spec.Id);
        if (string.IsNullOrEmpty(s) || !int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            return spec.Index + 1;
        return n;
    }

    private void WriteScan(XmlWriter w, Spectrum spec, int scanNumber,
        IReadOnlyDictionary<InstrumentConfiguration, int> instrumentIndex)
    {
        var ic = CultureInfo.InvariantCulture;
        var firstScan = spec.ScanList.Scans.Count > 0 ? spec.ScanList.Scans[0] : null;

        var spectrumType = spec.Params.CvParamChild(CVID.MS_spectrum_type);
        string scanType = spectrumType.Cvid switch
        {
            CVID.MS_MS1_spectrum or CVID.MS_MSn_spectrum => "Full",
            CVID.MS_CRM_spectrum => "CRM",
            CVID.MS_SIM_spectrum => "SIM",
            CVID.MS_SRM_spectrum => "SRM",
            CVID.MS_precursor_ion_spectrum => "Q1",
            CVID.MS_constant_neutral_gain_spectrum or CVID.MS_constant_neutral_loss_spectrum => "Q3",
            _ => "",
        };

        string msLevel = spec.Params.CvParam(CVID.MS_ms_level).Value;
        string polarity = GetPolarity(spec);
        string retentionTime = GetRetentionTimeAttr(spec) ?? "PT0S";
        string lowMz = spec.Params.CvParam(CVID.MS_lowest_observed_m_z).Value;
        string highMz = spec.Params.CvParam(CVID.MS_highest_observed_m_z).Value;
        string startMz = "", endMz = "";
        if (firstScan is not null && firstScan.ScanWindows.Count > 0)
        {
            startMz = firstScan.ScanWindows[0].CvParam(CVID.MS_scan_window_lower_limit).Value;
            endMz = firstScan.ScanWindows[0].CvParam(CVID.MS_scan_window_upper_limit).Value;
        }
        string basePeakMz = spec.Params.CvParam(CVID.MS_base_peak_m_z).Value;
        string basePeakIntensity = spec.Params.CvParam(CVID.MS_base_peak_intensity).Value;
        string totIonCurrent = spec.Params.CvParam(CVID.MS_total_ion_current).Value;
        string filterLine = firstScan?.CvParam(CVID.MS_filter_string).Value ?? "";
        string compensationVoltage = spec.Params.HasCVParam(CVID.MS_FAIMS_compensation_voltage)
            ? spec.Params.CvParam(CVID.MS_FAIMS_compensation_voltage).Value : "";
        bool isCentroided = spec.Params.HasCVParam(CVID.MS_centroid_spectrum);

        var precursors = GetPrecursorInfo(spec, GetDefaultNativeIdFormatFromSpec(spec));

        // Pull peak arrays (already loaded with getBinaryData=true).
        var mzArray = spec.GetMZArray()?.Data ?? new List<double>();
        var intArray = spec.GetIntensityArray()?.Data ?? new List<double>();
        int peaksCount = Math.Min(mzArray.Count, intArray.Count);

        w.WriteStartElement("scan");
        w.WriteAttributeString("num", scanNumber.ToString(ic));
        if (!string.IsNullOrEmpty(scanType)) w.WriteAttributeString("scanType", scanType);
        if (!string.IsNullOrEmpty(filterLine)) w.WriteAttributeString("filterLine", filterLine);
        w.WriteAttributeString("centroided", isCentroided ? "1" : "0");
        if (!string.IsNullOrEmpty(msLevel)) w.WriteAttributeString("msLevel", msLevel);
        w.WriteAttributeString("peaksCount", peaksCount.ToString(ic));
        if (!string.IsNullOrEmpty(polarity)) w.WriteAttributeString("polarity", polarity);
        w.WriteAttributeString("retentionTime", retentionTime);
        if (precursors.Count > 0 && !string.IsNullOrEmpty(precursors[0].CollisionEnergy))
            w.WriteAttributeString("collisionEnergy", precursors[0].CollisionEnergy);
        if (!string.IsNullOrEmpty(lowMz)) w.WriteAttributeString("lowMz", lowMz);
        if (!string.IsNullOrEmpty(highMz)) w.WriteAttributeString("highMz", highMz);
        if (!string.IsNullOrEmpty(startMz)) w.WriteAttributeString("startMz", startMz);
        if (!string.IsNullOrEmpty(endMz)) w.WriteAttributeString("endMz", endMz);
        if (!string.IsNullOrEmpty(basePeakMz)) w.WriteAttributeString("basePeakMz", basePeakMz);
        if (!string.IsNullOrEmpty(basePeakIntensity)) w.WriteAttributeString("basePeakIntensity", basePeakIntensity);
        if (!string.IsNullOrEmpty(totIonCurrent)) w.WriteAttributeString("totIonCurrent", totIonCurrent);
        if (!string.IsNullOrEmpty(compensationVoltage)) w.WriteAttributeString("compensationVoltage", compensationVoltage);
        if (firstScan?.InstrumentConfiguration is not null
            && instrumentIndex.TryGetValue(firstScan.InstrumentConfiguration, out int icIndex))
            w.WriteAttributeString("msInstrumentID", icIndex.ToString(ic));

        WritePrecursorMz(w, precursors);
        WritePeaks(w, mzArray, intArray, peaksCount);

        foreach (var up in spec.Params.UserParams)
        {
            w.WriteStartElement("nameValue");
            w.WriteAttributeString("name", up.Name);
            w.WriteAttributeString("value", up.Value);
            w.WriteEndElement();
        }

        w.WriteEndElement(); // scan
    }

    private static void WritePrecursorMz(XmlWriter w, List<PrecursorInfo> precursors)
    {
        foreach (var p in precursors)
        {
            w.WriteStartElement("precursorMz");
            if (!string.IsNullOrEmpty(p.ScanNum)) w.WriteAttributeString("precursorScanNum", p.ScanNum);
            // mzXML requires precursorIntensity; emit "0" when absent.
            w.WriteAttributeString("precursorIntensity",
                string.IsNullOrEmpty(p.Intensity) ? "0" : p.Intensity);
            if (!string.IsNullOrEmpty(p.Charge)) w.WriteAttributeString("precursorCharge", p.Charge);
            if (!string.IsNullOrEmpty(p.Activation)) w.WriteAttributeString("activationMethod", p.Activation);
            if (p.WindowWideness != 0)
                w.WriteAttributeString("windowWideness",
                    p.WindowWideness.ToString("R", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(p.Ccs)) w.WriteAttributeString("CCS", p.Ccs);
            w.WriteString(p.Mz);
            w.WriteEndElement();
        }
    }

    private void WritePeaks(XmlWriter w, List<double> mz, List<double> intensity, int n)
    {
        w.WriteStartElement("peaks");

        string encoded = "";
        int compressedLen = 0;

        if (n > 0)
        {
            // Interleave m/z + intensity as alternating doubles, then encode big-endian.
            var pairs = new double[n * 2];
            for (int i = 0; i < n; i++)
            {
                pairs[i * 2] = mz[i];
                pairs[i * 2 + 1] = intensity[i];
            }
            var encoder = new BinaryDataEncoder(_encoderConfig);
            encoded = encoder.Encode(pairs, out compressedLen);
        }

        if (n == 0)
            w.WriteAttributeString("xsi", "nil", "http://www.w3.org/2001/XMLSchema-instance", "true");

        bool zlib = _encoderConfig.Compression == BinaryCompression.Zlib;
        w.WriteAttributeString("compressionType", zlib ? "zlib" : "none");
        w.WriteAttributeString("compressedLen",
            (zlib ? compressedLen : 0).ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("precision",
            _encoderConfig.Precision == BinaryPrecision.Bits32 ? "32" : "64");
        w.WriteAttributeString("byteOrder", "network");
        w.WriteAttributeString("contentType", "m/z-int");

        if (!string.IsNullOrEmpty(encoded)) w.WriteString(encoded);
        w.WriteEndElement(); // peaks
    }

    // ---------- precursors ----------

    private readonly struct PrecursorInfo
    {
        public string ScanNum { get; init; }
        public string Mz { get; init; }
        public string Intensity { get; init; }
        public string Charge { get; init; }
        public string CollisionEnergy { get; init; }
        public string Activation { get; init; }
        public string Ccs { get; init; }
        public double WindowWideness { get; init; }
    }

    private static List<PrecursorInfo> GetPrecursorInfo(Spectrum spec, CVID nativeIdFormat)
    {
        var result = new List<PrecursorInfo>();
        foreach (var pre in spec.Precursors)
        {
            string scanNum = string.IsNullOrEmpty(pre.SpectrumId) ? ""
                : Id.TranslateNativeIdToScanNumber(nativeIdFormat, pre.SpectrumId);
            string mz = "", intensity = "", charge = "", ccs = "";
            if (pre.SelectedIons.Count > 0)
            {
                var si = pre.SelectedIons[0];
                mz = si.CvParam(CVID.MS_selected_ion_m_z).Value;
                intensity = si.CvParam(CVID.MS_peak_intensity).Value;
                charge = si.CvParam(CVID.MS_charge_state).Value;
                ccs = si.CvParam(CVID.MS_collisional_cross_sectional_area).Value;
            }
            string activation = "", collisionEnergy = "";
            if (pre.Activation.HasCVParam(CVID.MS_ETD))
            {
                activation = "ETD";
                if (pre.Activation.HasCVParam(CVID.MS_CID)) activation += "+SA";
            }
            else if (pre.Activation.HasCVParam(CVID.MS_ECD)) activation = "ECD";
            else if (pre.Activation.HasCVParam(CVID.MS_CID)) activation = "CID";
            else if (pre.Activation.HasCVParam(CVID.MS_HCD)) activation = "HCD";
            if (pre.Activation.HasCVParam(CVID.MS_CID) || pre.Activation.HasCVParam(CVID.MS_HCD))
                collisionEnergy = pre.Activation.CvParam(CVID.MS_collision_energy).Value;

            double windowWideness = 0;
            var lowOff = pre.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset);
            var highOff = pre.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset);
            if (!lowOff.IsEmpty && !highOff.IsEmpty
                && double.TryParse(lowOff.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lo)
                && double.TryParse(highOff.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double hi))
                windowWideness = Math.Abs(lo) + hi;

            bool empty = string.IsNullOrEmpty(scanNum) && string.IsNullOrEmpty(mz)
                && string.IsNullOrEmpty(intensity) && string.IsNullOrEmpty(charge)
                && string.IsNullOrEmpty(collisionEnergy) && string.IsNullOrEmpty(activation)
                && string.IsNullOrEmpty(ccs) && windowWideness == 0;
            if (empty) continue;

            result.Add(new PrecursorInfo
            {
                ScanNum = scanNum, Mz = mz, Intensity = intensity, Charge = charge,
                CollisionEnergy = collisionEnergy, Activation = activation, Ccs = ccs,
                WindowWideness = windowWideness,
            });
        }
        return result;
    }

    // ---------- index ----------

    private void WriteIndex(XmlWriter w)
    {
        w.WriteStartElement("index");
        w.WriteAttributeString("name", "scan");
        foreach (var (n, off) in _scanOffsets!)
        {
            w.WriteStartElement("offset");
            w.WriteAttributeString("id", n.ToString(CultureInfo.InvariantCulture));
            w.WriteString(off.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement(); // index
    }

    /// <summary>
    /// Writes pretty-print indent then flushes so the recorded position is exactly the byte
    /// where the next start tag's '&lt;' will land. Mirrors the equivalent helper in MzmlWriter.
    /// </summary>
    private void RecordOffset(XmlWriter w, int scanNumber, int indentDepth)
    {
        if (_scanOffsets is null || _stream is null) return;
        w.WriteWhitespace("\r\n" + new string(' ', indentDepth * 2));
        w.Flush();
        _scanOffsets.Add((scanNumber, _stream.BytesWritten));
    }

    // ---------- helpers ----------

    private static string? GetRetentionTimeAttr(Spectrum spec)
    {
        if (spec.ScanList.Scans.Count == 0) return null;
        var startTime = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
        if (startTime.IsEmpty) return null;
        // Convert to seconds based on the param's unit and emit as "PT<seconds>S".
        if (!double.TryParse(startTime.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return null;
        double seconds = startTime.Units switch
        {
            CVID.UO_minute => v * 60.0,
            CVID.UO_millisecond => v / 1000.0,
            CVID.UO_microsecond => v / 1_000_000.0,
            CVID.UO_hour => v * 3600.0,
            _ => v, // assume seconds
        };
        return "PT" + seconds.ToString("R", CultureInfo.InvariantCulture) + "S";
    }

    private static string GetPolarity(Spectrum spec)
    {
        var pol = spec.Params.CvParamChild(CVID.MS_scan_polarity);
        if (pol.IsEmpty) pol = spec.Params.CvParamChild(CVID.MS_polarity_OBSOLETE);
        return pol.Cvid switch
        {
            CVID.MS_positive_scan => "+",
            CVID.MS_negative_scan => "-",
            _ => "",
        };
    }

    private static CVID GetDefaultNativeIdFormat(MSData msd)
    {
        SourceFile? sf = msd.Run.DefaultSourceFile;
        sf ??= msd.FileDescription.SourceFiles.Count > 0 ? msd.FileDescription.SourceFiles[0] : null;
        if (sf is null) return CVID.CVID_Unknown;
        return sf.CvParamChild(CVID.MS_nativeID_format).Cvid;
    }

    private static CVID GetDefaultNativeIdFormatFromSpec(Spectrum spec)
    {
        // Best-effort: peek at the spectrum's own SourceFile when set, else infer from id format.
        if (spec.SourceFile is not null)
        {
            var cv = spec.SourceFile.CvParamChild(CVID.MS_nativeID_format).Cvid;
            if (cv != CVID.CVID_Unknown) return cv;
        }
        // Inference: a "controllerType=" prefix is Thermo; otherwise unknown.
        return spec.Id.StartsWith("controllerType=", StringComparison.Ordinal)
            ? CVID.MS_Thermo_nativeID_format
            : CVID.CVID_Unknown;
    }

    private static CVParam FindParam(ParamContainer pc, CVID parentCvid)
    {
        var p = pc.CvParamChild(parentCvid);
        return p;
    }

    /// <summary>Returns the human-readable name of the first source/analyzer/detector component.</summary>
    private static string FirstComponentName(InstrumentConfiguration ic, ComponentType type)
    {
        foreach (var c in ic.ComponentList)
        {
            if (c.Type != type) continue;
            // Pick the first non-empty CV term in the component (skip plain ParamContainer headers).
            foreach (var p in c.CVParams)
                if (!p.IsEmpty) return p.Name;
        }
        return "";
    }

    /// <summary>Best-effort manufacturer for an instrument-model CVID.</summary>
    private static string ResolveManufacturer(CVID model)
    {
        // The mzML CV organizes vendor instrument-model terms under per-vendor parents
        // (e.g. MS_Thermo_Electron_instrument_model). CvIsA walks is_a transitively so we can
        // probe each vendor root and pick the matching mzXML category string.
        if (model == CVID.CVID_Unknown) return "unknown";
        if (CvLookup.CvIsA(model, CVID.MS_Thermo_Fisher_Scientific_instrument_model)
            || CvLookup.CvIsA(model, CVID.MS_Thermo_Electron_instrument_model))
            return "Thermo Finnigan";
        if (CvLookup.CvIsA(model, CVID.MS_Bruker_Daltonics_instrument_model)) return "Bruker Daltonics";
        if (CvLookup.CvIsA(model, CVID.MS_Waters_instrument_model)) return "Waters";
        if (CvLookup.CvIsA(model, CVID.MS_SCIEX_instrument_model)) return "ABI / SCIEX";
        if (CvLookup.CvIsA(model, CVID.MS_Agilent_instrument_model)) return "Agilent";
        if (CvLookup.CvIsA(model, CVID.MS_Shimadzu_instrument_model)) return "Shimadzu";
        return "unknown";
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
