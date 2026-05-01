using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.MzXml;

/// <summary>
/// Parser for mzXML 3.x. Eagerly reads the entire document into an <see cref="MSData"/> with a
/// <see cref="SpectrumListSimple"/>. Port of pwiz <c>Serializer_mzXML::read</c> +
/// <c>SpectrumList_mzXML</c>, slimmed to the elements actually emitted by <see cref="MzxmlWriter"/>.
/// </summary>
/// <remarks>
/// Lazy / random-access reads are not supported — for a vendor → mzXML → mzML round-trip in
/// the test harness, eager loading is enough and avoids the seek-based machinery in cpp's
/// <c>SpectrumList_mzXMLImpl</c>. Instrument metadata is preserved at the legacy mzXML category
/// level (<c>msManufacturer</c>/<c>msModel</c>/<c>msIonisation</c>/<c>msMassAnalyzer</c>/<c>msDetector</c>);
/// fine-grained CV terms are not reconstituted because cpp's <c>LegacyAdapter</c>'s reverse
/// translation isn't ported. Round-trip diffs against the writer should run under a config with
/// <c>IgnoreMetadata</c> = true.
/// </remarks>
public sealed class MzxmlReader
{
    private static readonly char[] s_pathSeparators = ['/', '\\'];

    /// <summary>Reads <paramref name="stream"/> into <paramref name="msd"/>.</summary>
    public static void Read(Stream stream, MSData msd)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(msd);

        msd.CVs.AddRange(MSData.DefaultCVList);

        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Ignore,
            CloseInput = false,
        });

        var spectra = new SpectrumListSimple();
        msd.Run.SpectrumList = spectra;

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            switch (reader.LocalName)
            {
                case "mzXML":
                case "msRun":
                    // Skip wrappers; the loop continues into their children naturally.
                    break;
                case "parentFile":
                    ReadParentFile(reader, msd);
                    break;
                case "msInstrument":
                    ReadInstrument(reader, msd);
                    break;
                case "dataProcessing":
                    ReadDataProcessing(reader, msd);
                    break;
                case "scan":
                    ReadScan(reader, spectra);
                    break;
                case "index":
                case "indexOffset":
                case "sha1":
                    // Footer — ignore. The eager parser already has every spectrum.
                    reader.Skip();
                    break;
                // Older mzXML/scan-summary elements we don't need.
                case "operator":
                case "comment":
                case "msManufacturer":
                case "msModel":
                case "msIonisation":
                case "msMassAnalyzer":
                case "msDetector":
                case "software":
                case "processingOperation":
                    reader.Skip();
                    break;
            }
        }

        FillInMetadata(msd);
    }

    // ---------- parentFile ----------

    private static void ReadParentFile(XmlReader reader, MSData msd)
    {
        string fileName = reader.GetAttribute("fileName") ?? string.Empty;
        string fileType = reader.GetAttribute("fileType") ?? string.Empty;
        string fileSha1 = reader.GetAttribute("fileSha1") ?? string.Empty;
        if (fileType != "RAWData" && fileType != "processedData")
            throw new InvalidDataException(
                $"[MzxmlReader] invalid parentFile fileType '{fileType}'");

        SplitFilename(fileName, out string location, out string name);
        var sf = new SourceFile { Id = name, Name = name, Location = location };
        if (!string.IsNullOrEmpty(fileSha1))
            sf.Set(CVID.MS_SHA_1, fileSha1);
        msd.FileDescription.SourceFiles.Add(sf);
    }

    // ---------- msInstrument ----------

    private static void ReadInstrument(XmlReader reader, MSData msd)
    {
        string id = reader.GetAttribute("msInstrumentID") ?? reader.GetAttribute("id") ?? "";
        if (string.IsNullOrEmpty(id)) id = "IC" + (msd.InstrumentConfigurations.Count + 1).ToString(CultureInfo.InvariantCulture);
        var ic = new InstrumentConfiguration(id);
        msd.InstrumentConfigurations.Add(ic);

        string manufacturer = "", model = "", ionisation = "", analyzer = "", detector = "";

        if (reader.IsEmptyElement) return;
        using var sub = reader.ReadSubtree();
        sub.Read(); // position at msInstrument
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "msManufacturer": manufacturer = sub.GetAttribute("value") ?? ""; break;
                case "msModel": model = sub.GetAttribute("value") ?? ""; break;
                case "msIonisation": ionisation = sub.GetAttribute("value") ?? ""; break;
                case "msMassAnalyzer": analyzer = sub.GetAttribute("value") ?? ""; break;
                case "msDetector": detector = sub.GetAttribute("value") ?? ""; break;
                case "software":
                    {
                        var sw = ReadSoftwareElement(sub, msd);
                        ic.Software = sw;
                        break;
                    }
                case "msResolution":
                case "operator":
                case "nameValue":
                case "comment":
                case "instrument":
                    sub.Skip();
                    break;
            }
        }

        // Mirror cpp Handler_msInstrument::endElement: append source / analyzer / detector
        // components in canonical order and stamp each with a UserParam carrying the legacy
        // mzXML category text. cpp uses LegacyAdapter to look up CV terms; we preserve the raw
        // strings — round-trip diffs should run with IgnoreMetadata = true.
        var src = new Component(ComponentType.Source, 1);
        if (!string.IsNullOrEmpty(ionisation))
            src.UserParams.Add(new UserParam("msIonisation", ionisation, "xsd:string"));
        ic.ComponentList.Add(src);

        var ana = new Component(ComponentType.Analyzer, 1);
        if (!string.IsNullOrEmpty(analyzer))
            ana.UserParams.Add(new UserParam("msMassAnalyzer", analyzer, "xsd:string"));
        ic.ComponentList.Add(ana);

        var det = new Component(ComponentType.Detector, 1);
        if (!string.IsNullOrEmpty(detector))
            det.UserParams.Add(new UserParam("msDetector", detector, "xsd:string"));
        ic.ComponentList.Add(det);

        if (!string.IsNullOrEmpty(manufacturer) || !string.IsNullOrEmpty(model))
        {
            var pg = new ParamGroup("InstrumentParams_" + ic.Id);
            if (!string.IsNullOrEmpty(manufacturer))
                pg.UserParams.Add(new UserParam("msManufacturer", manufacturer, "xsd:string"));
            if (!string.IsNullOrEmpty(model))
                pg.UserParams.Add(new UserParam("msModel", model, "xsd:string"));
            msd.ParamGroups.Add(pg);
            ic.ParamGroups.Add(pg);
        }
    }

    private static Software ReadSoftwareElement(XmlReader reader, MSData msd)
    {
        string type = reader.GetAttribute("type") ?? "";
        string name = reader.GetAttribute("name") ?? "";
        string version = reader.GetAttribute("version") ?? "";

        // Reuse if we already have a Software with the same id.
        string id = string.IsNullOrEmpty(name) ? "software_" + (msd.Software.Count + 1) : name;
        var existing = msd.Software.FirstOrDefault(s => s.Id == id && s.Version == version);
        if (existing is not null) return existing;

        var sw = new Software(id) { Version = version };
        if (!string.IsNullOrEmpty(type))
            sw.UserParams.Add(new UserParam("software type", type, "xsd:string"));
        if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(sw.UserParam("software name").Name))
            sw.UserParams.Add(new UserParam("software name", name, "xsd:string"));
        msd.Software.Add(sw);
        return sw;
    }

    // ---------- dataProcessing ----------

    private static void ReadDataProcessing(XmlReader reader, MSData msd)
    {
        string centroided = reader.GetAttribute("centroided") ?? "";
        string deisotoped = reader.GetAttribute("deisotoped") ?? "";
        // chargeDeconvoluted, intensityCutoff are recognized by cpp but not reflected here.

        var dp = new DataProcessing("dataProcessing_" + (msd.DataProcessings.Count + 1).ToString(CultureInfo.InvariantCulture));
        msd.DataProcessings.Add(dp);

        var pm = new ProcessingMethod { Order = 0 };
        if (centroided == "1") pm.Set(CVID.MS_peak_picking);
        if (deisotoped == "1") pm.Set(CVID.MS_deisotoping);
        dp.ProcessingMethods.Add(pm);

        if (reader.IsEmptyElement) return;
        using var sub = reader.ReadSubtree();
        sub.Read(); // position at dataProcessing
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "software":
                    pm.Software = ReadSoftwareElement(sub, msd);
                    break;
                case "processingOperation":
                case "comment":
                    sub.Skip();
                    break;
            }
        }
    }

    // ---------- scan ----------

    private static void ReadScan(XmlReader reader, SpectrumListSimple spectra)
    {
        var spec = new Spectrum
        {
            Index = spectra.Spectra.Count,
        };
        spectra.Spectra.Add(spec);

        string num = reader.GetAttribute("num") ?? "";
        if (!string.IsNullOrEmpty(num))
            spec.Id = "scan=" + num;
        else
            spec.Id = "index=" + spec.Index.ToString(CultureInfo.InvariantCulture);

        string msLevelStr = (reader.GetAttribute("msLevel") ?? "").Trim();
        if (string.IsNullOrEmpty(msLevelStr)) msLevelStr = "1";
        int msLevel = int.Parse(msLevelStr, CultureInfo.InvariantCulture);
        spec.Params.Set(CVID.MS_ms_level, msLevel);

        string scanType = (reader.GetAttribute("scanType") ?? "").ToLowerInvariant();
        switch (scanType)
        {
            case "" or "full":
                spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
                break;
            case "zoom":
                spec.Params.Set(CVID.MS_MSn_spectrum);
                break;
            case "sim":
                spec.Params.Set(CVID.MS_SIM_spectrum);
                break;
            case "srm":
            case "mrm":
            case "multiplereaction":
            case "srm_ionprep":
                spec.Params.Set(CVID.MS_SRM_spectrum);
                break;
            case "crm":
                spec.Params.Set(CVID.MS_CRM_spectrum);
                break;
            case "q1":
                spec.Params.Set(CVID.MS_precursor_ion_spectrum);
                break;
            case "q3":
                spec.Params.Set(CVID.MS_product_ion_spectrum);
                break;
            default:
                spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
                break;
        }

        // Polarity.
        string polarity = reader.GetAttribute("polarity") ?? "";
        if (polarity == "+") spec.Params.Set(CVID.MS_positive_scan);
        else if (polarity == "-") spec.Params.Set(CVID.MS_negative_scan);

        // Centroid / profile.
        string centroided = reader.GetAttribute("centroided") ?? "";
        if (centroided == "1") spec.Params.Set(CVID.MS_centroid_spectrum);
        else if (centroided == "0") spec.Params.Set(CVID.MS_profile_spectrum);

        // Scan list + retention time + scan window + IC ref.
        spec.ScanList.Set(CVID.MS_no_combination);
        var scanEl = new Scan();
        spec.ScanList.Scans.Add(scanEl);

        string retentionTime = reader.GetAttribute("retentionTime") ?? "";
        if (!string.IsNullOrEmpty(retentionTime))
        {
            // mzXML retention time is ISO-8601 duration "PT<seconds>S".
            if (retentionTime.Length > 3 && retentionTime[..2] == "PT" && retentionTime[^1] == 'S')
            {
                string secStr = retentionTime[2..^1];
                if (double.TryParse(secStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double sec))
                    scanEl.Set(CVID.MS_scan_start_time, sec, CVID.UO_second);
            }
        }

        if (TryGetDoubleAttr(reader, "startMz", out double startMz)
            && TryGetDoubleAttr(reader, "endMz", out double endMz)
            && endMz > 0)
        {
            scanEl.ScanWindows.Add(new ScanWindow(startMz, endMz, CVID.MS_m_z));
        }

        // Spectrum-level cv params.
        string lowMz = reader.GetAttribute("lowMz") ?? "";
        string highMz = reader.GetAttribute("highMz") ?? "";
        string basePeakMz = reader.GetAttribute("basePeakMz") ?? "";
        string basePeakIntensity = reader.GetAttribute("basePeakIntensity") ?? "";
        string totIonCurrent = reader.GetAttribute("totIonCurrent") ?? "";
        if (!string.IsNullOrEmpty(lowMz)) spec.Params.Set(CVID.MS_lowest_observed_m_z, lowMz, CVID.MS_m_z);
        if (!string.IsNullOrEmpty(highMz)) spec.Params.Set(CVID.MS_highest_observed_m_z, highMz, CVID.MS_m_z);
        if (!string.IsNullOrEmpty(basePeakMz)) spec.Params.Set(CVID.MS_base_peak_m_z, basePeakMz, CVID.MS_m_z);
        if (!string.IsNullOrEmpty(basePeakIntensity))
            spec.Params.Set(CVID.MS_base_peak_intensity, basePeakIntensity, CVID.MS_number_of_detector_counts);
        if (!string.IsNullOrEmpty(totIonCurrent)) spec.Params.Set(CVID.MS_total_ion_current, totIonCurrent);

        string filterLine = reader.GetAttribute("filterLine") ?? "";
        if (!string.IsNullOrEmpty(filterLine)) scanEl.Set(CVID.MS_filter_string, filterLine);

        string compensationVoltage = reader.GetAttribute("compensationVoltage") ?? "";
        if (!string.IsNullOrEmpty(compensationVoltage))
            spec.Params.Set(CVID.MS_FAIMS_compensation_voltage, compensationVoltage, CVID.UO_volt);

        // Track collisionEnergy at the scan attribute level — cpp attaches it to the next
        // precursor encountered.
        string collisionEnergyStr = reader.GetAttribute("collisionEnergy") ?? "";

        int peaksCount = 0;
        if (TryGetIntAttr(reader, "peaksCount", out int pc)) peaksCount = pc;

        if (reader.IsEmptyElement) return;

        using var sub = reader.ReadSubtree();
        sub.Read(); // position at scan
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "precursorMz":
                    ReadPrecursor(sub, spec, collisionEnergyStr);
                    break;
                case "peaks":
                    ReadPeaks(sub, spec, peaksCount);
                    break;
                case "nameValue":
                    {
                        string n = sub.GetAttribute("name") ?? "";
                        string v = sub.GetAttribute("value") ?? "";
                        spec.Params.UserParams.Add(new UserParam(n, v, "xsd:string"));
                        sub.Skip();
                        break;
                    }
                case "scan":
                    // Nested scan (older mzXML uses nesting for precursor relationships) — skip.
                    sub.Skip();
                    break;
                case "scanOrigin":
                case "nativeScanRef":
                case "coordinate":
                case "comment":
                    sub.Skip();
                    break;
            }
        }
    }

    private static void ReadPrecursor(XmlReader reader, Spectrum spec, string scanCollisionEnergy)
    {
        var pre = new Precursor();
        spec.Precursors.Add(pre);

        string precursorScanNum = reader.GetAttribute("precursorScanNum") ?? "";
        string precursorIntensity = reader.GetAttribute("precursorIntensity") ?? "";
        string precursorCharge = reader.GetAttribute("precursorCharge") ?? "";
        string activationMethod = reader.GetAttribute("activationMethod") ?? "";
        string windowWideness = reader.GetAttribute("windowWideness") ?? "";
        string ccs = reader.GetAttribute("CCS") ?? "";

        if (!string.IsNullOrEmpty(precursorScanNum))
            pre.SpectrumId = "scan=" + precursorScanNum;

        var si = new SelectedIon();
        if (!string.IsNullOrEmpty(precursorIntensity) && precursorIntensity != "0")
            si.Set(CVID.MS_peak_intensity, precursorIntensity, CVID.MS_number_of_detector_counts);
        if (!string.IsNullOrEmpty(precursorCharge))
            si.Set(CVID.MS_charge_state, precursorCharge);
        if (!string.IsNullOrEmpty(ccs))
            si.Set(CVID.MS_collisional_cross_sectional_area, ccs, CVID.UO_square_angstrom);
        pre.SelectedIons.Add(si);

        switch (activationMethod)
        {
            case "" or "CID":
                pre.Activation.Set(CVID.MS_CID);
                break;
            case "ETD":
                pre.Activation.Set(CVID.MS_ETD);
                break;
            case "ETD+SA":
                pre.Activation.Set(CVID.MS_ETD);
                pre.Activation.Set(CVID.MS_CID);
                break;
            case "ECD":
                pre.Activation.Set(CVID.MS_ECD);
                break;
            case "HCD":
                pre.Activation.Set(CVID.MS_HCD);
                break;
        }

        if (!string.IsNullOrEmpty(scanCollisionEnergy))
            pre.Activation.Set(CVID.MS_collision_energy, scanCollisionEnergy, CVID.UO_electronvolt);

        if (!string.IsNullOrEmpty(windowWideness)
            && double.TryParse(windowWideness, NumberStyles.Float, CultureInfo.InvariantCulture, out double ww))
        {
            double half = ww / 2.0;
            pre.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
            pre.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);
        }

        // The text content of <precursorMz> is the m/z; also seeds isolation_window_target_m_z.
        // Walk children manually instead of ReadElementContentAsString — the latter advances
        // past the end tag, which would cause the outer scan loop to skip the next sibling.
        string mz = ReadElementText(reader);
        if (!string.IsNullOrEmpty(mz))
        {
            si.Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
            pre.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, mz, CVID.MS_m_z);
        }
    }

    /// <summary>
    /// Reads the current element's text content while leaving the reader positioned on its
    /// <see cref="XmlNodeType.EndElement"/>. Lets the caller's outer <c>while (sub.Read())</c>
    /// advance to the next sibling correctly. <see cref="XmlReader.ReadElementContentAsString()"/>
    /// would have advanced one node further.
    /// </summary>
    private static string ReadElementText(XmlReader reader)
    {
        if (reader.IsEmptyElement) return string.Empty;
        int startDepth = reader.Depth;
        var sb = new System.Text.StringBuilder();
        while (reader.Read())
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace
                or XmlNodeType.SignificantWhitespace)
            {
                sb.Append(reader.Value);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == startDepth)
            {
                break;
            }
        }
        return sb.ToString();
    }

    private static void ReadPeaks(XmlReader reader, Spectrum spec, int peaksCount)
    {
        string precision = reader.GetAttribute("precision") ?? "32";
        string compressionType = reader.GetAttribute("compressionType") ?? "none";
        string byteOrder = reader.GetAttribute("byteOrder") ?? "network";

        if (byteOrder != "network" && byteOrder != "")
            throw new InvalidDataException(
                $"[MzxmlReader] unsupported byteOrder '{byteOrder}' (only 'network' is valid)");

        var encoderConfig = new BinaryEncoderConfig
        {
            Precision = precision == "64" ? BinaryPrecision.Bits64 : BinaryPrecision.Bits32,
            ByteOrder = BinaryByteOrder.BigEndian,
            Compression = compressionType == "zlib" ? BinaryCompression.Zlib : BinaryCompression.None,
        };

        // Same manual-text trick as ReadPrecursor — leave reader on </peaks> so the outer
        // scan loop's next Read() lands on the following sibling, not the one after.
        string content = ReadElementText(reader);
        if (peaksCount == 0 || string.IsNullOrEmpty(content.Trim()))
        {
            spec.SetMZIntensityArrays(Array.Empty<double>(), Array.Empty<double>(),
                CVID.MS_number_of_detector_counts);
            return;
        }

        var encoder = new BinaryDataEncoder(encoderConfig);
        double[] decoded = encoder.DecodeDoubles(content);

        if (decoded.Length % 2 != 0 || decoded.Length / 2 != peaksCount)
            throw new InvalidDataException(
                $"[MzxmlReader] decoded peak count {decoded.Length / 2} does not match attribute peaksCount={peaksCount}");

        var mz = new double[peaksCount];
        var intensity = new double[peaksCount];
        for (int i = 0; i < peaksCount; i++)
        {
            mz[i] = decoded[i * 2];
            intensity[i] = decoded[i * 2 + 1];
        }
        spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
    }

    // ---------- helpers ----------

    private static void SplitFilename(string fullPath, out string location, out string basename)
    {
        int lastSlash = fullPath.LastIndexOfAny(s_pathSeparators);
        if (lastSlash < 0 || lastSlash == fullPath.Length - 1)
        {
            location = string.Empty;
            basename = fullPath;
            return;
        }
        location = fullPath[..lastSlash];
        basename = fullPath[(lastSlash + 1)..];
    }

    private static bool TryGetDoubleAttr(XmlReader reader, string name, out double value)
    {
        string s = reader.GetAttribute(name) ?? "";
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetIntAttr(XmlReader reader, string name, out int value)
    {
        string s = reader.GetAttribute(name) ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Mirrors cpp <c>fillInMetadata</c>: assigns source-file-format and nativeID-format CV terms
    /// based on each parentFile's extension, sets <see cref="MSData.Id"/> from the run id derived
    /// from the first parent file.
    /// </summary>
    private static void FillInMetadata(MSData msd)
    {
        foreach (var sf in msd.FileDescription.SourceFiles)
        {
            CVID sourceType = TranslateParentFilenameToSourceFileType(sf.Name);
            if (sourceType != CVID.CVID_Unknown)
            {
                sf.Set(sourceType);
                CVID nativeIdFormat = TranslateSourceFileTypeToNativeIdFormat(sourceType);
                sf.Set(nativeIdFormat);
            }
        }

        if (string.IsNullOrEmpty(msd.Id) && msd.FileDescription.SourceFiles.Count > 0)
        {
            // Derive run id from the first source file: drop the extension.
            string name = msd.FileDescription.SourceFiles[0].Name;
            int dot = name.LastIndexOf('.');
            string runId = dot > 0 ? name[..dot] : name;
            msd.Id = runId;
            msd.Run.Id = runId;
        }
    }

    /// <summary>Mirrors cpp <c>translate_parentFilenameToSourceFileType</c>.</summary>
    private static CVID TranslateParentFilenameToSourceFileType(string name)
    {
        string ext = "";
        int dot = name.LastIndexOf('.');
        if (dot >= 0) ext = name[dot..].ToLowerInvariant();

        if (ext == ".raw") return CVID.MS_Thermo_RAW_format;
        if (ext == ".dat") return CVID.MS_Waters_raw_format;
        if (ext == ".wiff" || ext == ".wiff2") return CVID.MS_ABI_WIFF_format;
        if (ext == ".yep") return CVID.MS_Bruker_Agilent_YEP_format;
        if (ext == ".baf") return CVID.MS_Bruker_BAF_format;
        if (ext == ".tdf") return CVID.MS_Bruker_TDF_format;
        if (ext == ".tsf") return CVID.MS_Bruker_TSF_format;
        if (string.Equals(name, "fid", StringComparison.OrdinalIgnoreCase)) return CVID.MS_Bruker_FID_format;
        if (name.Equals("msprofile.bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("mspeak.bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("msscan.bin", StringComparison.OrdinalIgnoreCase))
            return CVID.MS_Agilent_MassHunter_format;
        if (ext == ".t2d") return CVID.MS_SCIEX_TOF_TOF_T2D_format;
        if (ext == ".mzdata") return CVID.MS_PSI_mzData_format;
        if (ext == ".mgf") return CVID.MS_Mascot_MGF_format;
        if (ext == ".dta") return CVID.MS_DTA_format;
        if (ext == ".pkl") return CVID.MS_Micromass_PKL_format;
        if (ext == ".mzxml" || name.EndsWith(".mz.xml", StringComparison.OrdinalIgnoreCase))
            return CVID.MS_ISB_mzXML_format;
        if (ext == ".mzml") return CVID.MS_mzML_format;
        // Bruker / Agilent .d directories — treat like mzXML so consumers don't reject.
        if (ext == ".d") return CVID.MS_ISB_mzXML_format;
        return CVID.CVID_Unknown;
    }

    private static CVID TranslateSourceFileTypeToNativeIdFormat(CVID sourceFileType) => sourceFileType switch
    {
        CVID.MS_Thermo_RAW_format => CVID.MS_Thermo_nativeID_format,
        CVID.MS_Bruker_Agilent_YEP_format => CVID.MS_Bruker_Agilent_YEP_nativeID_format,
        CVID.MS_Bruker_BAF_format => CVID.MS_Bruker_BAF_nativeID_format,
        CVID.MS_Bruker_TDF_format => CVID.MS_Bruker_TDF_nativeID_format,
        CVID.MS_Bruker_TSF_format => CVID.MS_Bruker_TSF_nativeID_format,
        CVID.MS_ISB_mzXML_format => CVID.MS_scan_number_only_nativeID_format,
        CVID.MS_PSI_mzData_format => CVID.MS_spectrum_identifier_nativeID_format,
        CVID.MS_Mascot_MGF_format => CVID.MS_multiple_peak_list_nativeID_format,
        CVID.MS_DTA_format => CVID.MS_scan_number_only_nativeID_format,
        CVID.MS_Agilent_MassHunter_format => CVID.MS_Agilent_MassHunter_nativeID_format,
        CVID.MS_ABI_WIFF_format
            or CVID.MS_Bruker_FID_format
            or CVID.MS_SCIEX_TOF_TOF_T2D_format
            or CVID.MS_Waters_raw_format
            or CVID.MS_Micromass_PKL_format
            or CVID.MS_mzML_format => CVID.MS_scan_number_only_nativeID_format,
        _ => CVID.MS_no_nativeID_format,
    };
}
