using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="IReader"/> for Bruker <c>.d</c> analysis directories. Supports the timsTOF
/// TDF format (<c>analysis.tdf</c>) and the non-mobility timsTOF TSF format (<c>analysis.tsf</c>).
/// Other Bruker formats (BAF, YEP, FID) are recognized by <see cref="Identify"/> and produce
/// the corresponding <see cref="CVID"/>, but reading them throws <see cref="NotSupportedException"/>
/// until their backends are ported.
/// </summary>
/// <remarks>Port of pwiz::msdata::Reader_Bruker.</remarks>
public sealed class Reader_Bruker : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Bruker";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Bruker_BAF_format; // placeholder; Identify returns the specific format

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".d" };

    /// <summary>
    /// When true, the produced <see cref="SpectrumList_Bruker"/> emits one combined spectrum
    /// per MS1 frame (summed across mobility) and per PASEF/DIA-PASEF precursor isolation
    /// window, rather than per-(frame, scan). Mirrors pwiz C++ <c>--combineIonMobilitySpectra</c>.
    /// If <see cref="ReaderConfig.CombineIonMobilitySpectra"/> is set on the passed config,
    /// that value overrides this instance property.
    /// </summary>
    public bool CombineIonMobilitySpectra { get; set; }

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        var format = DetectFormat(filename);
        return format switch
        {
            BrukerFormat.Tdf => CVID.MS_Bruker_TDF_format,
            BrukerFormat.Tsf => CVID.MS_Bruker_TSF_format,
            BrukerFormat.Baf => CVID.MS_Bruker_BAF_format,
            BrukerFormat.Yep => CVID.MS_Bruker_Agilent_YEP_format,
            BrukerFormat.Fid => CVID.MS_Bruker_FID_format,
            _ => CVID.CVID_Unknown,
        };
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        int preferOnlyMsLevel = config?.PreferOnlyMsLevel ?? 0;
        bool combineIms = config?.CombineIonMobilitySpectra ?? CombineIonMobilitySpectra;
        bool sortAndJitter = config?.SortAndJitter ?? false;
        bool peakPicking = config?.PeakPicking ?? false;
        var format = DetectFormat(filename);
        if (format != BrukerFormat.Tdf && format != BrukerFormat.Tsf && format != BrukerFormat.Baf)
            throw new NotSupportedException(
                $"Bruker format {format} is not yet supported by msconvert-sharp (TDF, TSF, BAF are ported; YEP / FID still pending).");

        string analysisDir = Directory.Exists(filename)
            ? filename
            : (Path.GetDirectoryName(filename) ?? throw new ArgumentException("Bruker path must be a .d directory or file inside one."));
        analysisDir = Path.GetFullPath(analysisDir);

        var data = BrukerData.Create(analysisDir);
        try
        {
            ReadImpl(result, data, analysisDir, preferOnlyMsLevel, combineIms, sortAndJitter, peakPicking);
        }
        catch
        {
            data.Dispose();
            throw;
        }
    }

    private static void ReadImpl(MSData result, IBrukerData data, string analysisDir, int preferOnlyMsLevel, bool combineIonMobilitySpectra, bool sortAndJitter, bool peakPicking)
    {
        result.CVs.AddRange(MSData.DefaultCVList);
        result.Id = Path.GetFileNameWithoutExtension(analysisDir);

        AddSourceFiles(result, analysisDir, data.Format);

        // fileContent reflects the spectra we'll actually emit: preferOnlyMsLevel narrows it.
        if (preferOnlyMsLevel != 2 && data.HasMs1Frames) result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (preferOnlyMsLevel != 1 && data.HasMsNFrames) result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        _ = AddTimsSdkSoftware(result);
        var acqSoftware = AddAcquisitionSoftware(result, data.GlobalMetadata);
        var pwizSoftware = GetOrAddPwizSoftware(result, "pwiz_Reader_Bruker");

        var dpReader = MakeDataProcessing("pwiz_Reader_Bruker_conversion", pwizSoftware);
        result.DataProcessings.Add(dpReader);
        result.DataProcessings.Add(MakeDataProcessing("pwiz_Reader_conversion", pwizSoftware));

        FillInstrumentMetadata(result, data, acqSoftware);

        result.Run.Id = result.Id;
        result.Run.DefaultSourceFile = result.FileDescription.SourceFiles.FirstOrDefault();
        result.Run.StartTimeStamp = ConvertTimestamp(data.GlobalMetadata.GetValueOrDefault("AcquisitionDateTime", ""));
        if (result.InstrumentConfigurations.Count > 0)
            result.Run.DefaultInstrumentConfiguration = result.InstrumentConfigurations[0];

        var spectrumList = new SpectrumList_Bruker(
            data, owns: true,
            combineIonMobilitySpectra: combineIonMobilitySpectra,
            preferOnlyMsLevel: preferOnlyMsLevel,
            sortAndJitter: sortAndJitter)
        { Dp = dpReader };
        result.Run.SpectrumList = spectrumList;

        // pwiz C++ non-centroid combineIMS reference mzMLs omit the chromatogramList entirely
        // (each combined spectrum already carries the TIC of its merged frame range), but the
        // centroid-combineIMS refs include it — so suppress only when combine is on AND peak
        // picking is off.
        if (!combineIonMobilitySpectra || peakPicking)
            result.Run.ChromatogramList = new ChromatogramList_Bruker(data, spectrumList, preferOnlyMsLevel) { Dp = dpReader };
    }

    private static void AddSourceFiles(MSData result, string analysisDir, BrukerFormat format)
    {
        var (baseName, nativeIdFormat, fileFormat) = format switch
        {
            BrukerFormat.Tdf => ("analysis.tdf", CVID.MS_Bruker_TDF_nativeID_format, CVID.MS_Bruker_TDF_format),
            BrukerFormat.Tsf => ("analysis.tsf", CVID.MS_Bruker_TSF_nativeID_format, CVID.MS_Bruker_TSF_format),
            BrukerFormat.Baf => ("analysis.baf", CVID.MS_Bruker_BAF_nativeID_format, CVID.MS_Bruker_BAF_format),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        AddPairedSourceFiles(result, analysisDir, baseName, nativeIdFormat, fileFormat);
    }

    private static void AddPairedSourceFiles(MSData result, string analysisDir,
        string baseName, CVID nativeIdFormat, CVID fileFormat)
    {
        string dirName = Path.GetFileName(analysisDir);
        string location = "file://" + analysisDir.Replace('/', '\\');

        foreach (var fname in new[] { baseName, baseName + "_bin" })
        {
            string path = Path.Combine(analysisDir, fname);
            if (!File.Exists(path)) continue;
            // pwiz uses capital-A "Analysis." in its sourceFile id/name.
            string leaf = "Analysis" + fname["analysis".Length..];
            var sf = new SourceFile(dirName + "\\" + leaf, leaf, location);
            sf.Set(nativeIdFormat);
            sf.Set(fileFormat);
            result.FileDescription.SourceFiles.Add(sf);
        }
    }

    private static Software AddTimsSdkSoftware(MSData result)
    {
        var s = new Software("TIMS_SDK") { Version = "2.21.104.32" };
        s.Set(CVID.MS_Bruker_software);
        s.UserParams.Add(new UserParam("software name", "TIMS SDK"));
        result.Software.Add(s);
        return s;
    }

    private static Software AddAcquisitionSoftware(MSData result, IReadOnlyDictionary<string, string> globalMetadata)
    {
        string name = globalMetadata.GetValueOrDefault("AcquisitionSoftware", "");
        string version = globalMetadata.GetValueOrDefault("AcquisitionSoftwareVersion", "");

        // Map the acquisition software name to a CV term. pwiz C++ derives the software.id from
        // cvTermInfo(cvid).shortName() and defaults to MS_Compass when the name is unrecognized.
        CVID cv = TranslateAcquisitionSoftware(name);
        string id = cv switch
        {
            CVID.MS_Compass => "Compass",
            CVID.MS_micrOTOFcontrol => "micrOTOFcontrol",
            CVID.MS_HCTcontrol => "HCTcontrol",
            CVID.MS_apexControl => "apexControl",
            CVID.MS_FlexControl => "FlexControl",
            _ => "acquisition_software",
        };

        var s = new Software(id) { Version = version };
        s.Set(cv);
        result.Software.Add(s);
        return s;
    }

    private static CVID TranslateAcquisitionSoftware(string name)
    {
        // Port of translateAsAcquisitionSoftware() in Reader_Bruker_Detail.cpp.
        if (name.Contains("HCT", StringComparison.OrdinalIgnoreCase)) return CVID.MS_HCTcontrol;
        if (name.Contains("oTOFcontrol", StringComparison.OrdinalIgnoreCase)) return CVID.MS_micrOTOFcontrol;
        if (name.Contains("Compass", StringComparison.OrdinalIgnoreCase)) return CVID.MS_Compass;
        if (name.Contains("Apex", StringComparison.OrdinalIgnoreCase)) return CVID.MS_apexControl;
        if (name.Contains("Flex", StringComparison.OrdinalIgnoreCase)) return CVID.MS_FlexControl;
        return CVID.MS_Compass; // C++ default when name is empty or unrecognized.
    }

    private static Software GetOrAddPwizSoftware(MSData msd, string id)
    {
        foreach (var s in msd.Software)
            if (s.HasCVParam(CVID.MS_pwiz) && s.Id == id) return s;
        var pwiz = new Software(id) { Version = MSData.PwizVersion };
        pwiz.Set(CVID.MS_pwiz);
        msd.Software.Add(pwiz);
        return pwiz;
    }

    private static DataProcessing MakeDataProcessing(string id, Software software)
    {
        var dp = new DataProcessing(id);
        var pm = new ProcessingMethod { Order = 0, Software = software };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dp.ProcessingMethods.Add(pm);
        return dp;
    }

    private static void FillInstrumentMetadata(MSData result, IBrukerData data, Software acqSoftware)
    {
        CVID sourceCv;
        CVID? inletCv = null;
        if (data.IsMaldiSource)
            sourceCv = CVID.MS_matrix_assisted_laser_desorption_ionization;
        else
            (sourceCv, inletCv) = TranslateInstrumentSource(data.GlobalMetadata);

        var ic = BuildTimsTofInstrumentConfiguration(result, data.GlobalMetadata, acqSoftware, sourceCv, inletCv);

        // DIA-PASEF window groups are a TDF-only annotation on the instrument config.
        if (data is TdfData tdf)
            AddDiaPasefWindowGroupUserParams(ic, tdf.Metadata, tdf.TimsBinaryData);

        result.InstrumentConfigurations.Add(ic);
    }

    private static InstrumentConfiguration BuildTimsTofInstrumentConfiguration(
        MSData result, IReadOnlyDictionary<string, string> globalMetadata, Software acqSoftware,
        CVID sourceCv, CVID? inletCv)
    {
        string serial = globalMetadata.GetValueOrDefault("InstrumentSerialNumber", "");

        var common = new ParamGroup("CommonInstrumentParams");
        common.Set(CVID.MS_Bruker_Daltonics_timsTOF_series);
        result.ParamGroups.Add(common);

        var ic = new InstrumentConfiguration("IC1");
        ic.ParamGroups.Add(common);
        ic.Software = acqSoftware;
        if (!string.IsNullOrEmpty(serial))
            ic.Set(CVID.MS_instrument_serial_number, serial);

        var source = new Component(sourceCv, 1);
        if (inletCv.HasValue)
            source.Set(inletCv.Value);
        ic.ComponentList.Add(source);
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
        ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 3));
        ic.ComponentList.Add(new Component(CVID.MS_microchannel_plate_detector, 4));
        ic.ComponentList.Add(new Component(CVID.MS_photomultiplier, 5));
        return ic;
    }

    /// <summary>
    /// Port of <c>Reader_Bruker_Detail::createInstrumentConfigurations</c> for the source /
    /// inlet pair. Maps Bruker's <c>InstrumentSourceType</c> numeric code to CVIDs.
    /// </summary>
    private static (CVID Source, CVID? Inlet) TranslateInstrumentSource(IReadOnlyDictionary<string, string> globalMetadata)
    {
        int sourceType = 255; // Unknown
        if (globalMetadata.TryGetValue("InstrumentSourceType", out var v)
            && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            sourceType = parsed;

        // Numeric values from CompassDataEnums.hpp (InstrumentSource enum).
        return sourceType switch
        {
            6 => (CVID.MS_atmospheric_pressure_matrix_assisted_laser_desorption_ionization, null), // AP_MALDI
            7 => (CVID.MS_matrix_assisted_laser_desorption_ionization, null),                      // MALDI
            1 or 8 or 10 or 18 => (CVID.MS_electrospray_ionization, CVID.MS_electrospray_inlet),   // ESI / MULTI_MODE / Ultraspray / VIP_HESI
            3 or 4 or 9 or 11 => (CVID.MS_nanoelectrospray, CVID.MS_nanospray_inlet),              // NANO_ESI_OFFLINE / ONLINE / NANO_FLOW_ESI / CaptiveSpray
            2 or 17 or 19 => (CVID.MS_atmospheric_pressure_chemical_ionization, null),             // APCI / GC_APCI / VIP_APCI
            5 => (CVID.MS_atmospheric_pressure_photoionization, null),                             // APPI
            16 => (CVID.MS_electron_ionization, null),                                             // EI
            // Fallback for AlsoUnknown/Unknown: C++ uses instrument family to decide. For the
            // timsTOF family (9) that's ESI; match that.
            _ => (CVID.MS_electrospray_ionization, CVID.MS_electrospray_inlet),
        };
    }

    /// <summary>
    /// For diaPASEF acquisitions, pwiz C++ attaches the DiaFrameMsMsWindows table as userParams on
    /// the default InstrumentConfiguration (one <c>DiaFrameMsMsWindowsTable</c> header + one
    /// <c>WindowGroup</c> row per window).
    /// </summary>
    private static void AddDiaPasefWindowGroupUserParams(InstrumentConfiguration ic, TdfMetadata meta, TimsBinaryData tims)
    {
        if (!meta.HasDiaPasefData) return;
        // TdfMetadata.EnumerateDiaWindowGroups currently returns scan numbers in the InvK0 fields;
        // convert them to actual 1/K0 values using the first frame's calibration.
        var raw = meta.EnumerateDiaWindowGroups().ToList();
        if (raw.Count == 0) return;
        long firstFrame = meta.EnumerateFrames().First().FrameId;

        var scans = new double[raw.Count * 2];
        for (int i = 0; i < raw.Count; i++)
        {
            scans[2 * i] = raw[i].InvK0Begin;
            scans[2 * i + 1] = raw[i].InvK0End;
        }
        var k0 = tims.ScanNumberToOneOverK0(firstFrame, scans);

        ic.UserParams.Add(new UserParam(
            "DiaFrameMsMsWindowsTable",
            "WindowGroup,invK0Begin,invK0End,IsolationMz,IsolationWidth,CollisionEnergy"));
        for (int i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            // G17 gives the 17-digit representation that matches boost::lexical_cast<string>(double).
            string line = string.Join(',',
                r.WindowGroup.ToString(CultureInfo.InvariantCulture),
                k0[2 * i].ToString("G17", CultureInfo.InvariantCulture),
                k0[2 * i + 1].ToString("G17", CultureInfo.InvariantCulture),
                r.IsolationMz.ToString("G17", CultureInfo.InvariantCulture),
                r.IsolationWidth.ToString("G17", CultureInfo.InvariantCulture),
                r.CollisionEnergy.ToString("G17", CultureInfo.InvariantCulture));
            ic.UserParams.Add(new UserParam("WindowGroup", line));
        }

        // pwiz C++ builds the table string with a trailing ';', then splits by ';' — which yields
        // an empty element at the end and emits it as a blank userParam. Match that.
        ic.UserParams.Add(new UserParam("WindowGroup", string.Empty));
    }

    /// <summary>
    /// Normalizes Bruker's timestamp to mzML's <c>yyyy-MM-ddTHH:mm:ssZ</c>. To match pwiz C++'s
    /// output, the local clock time is preserved verbatim — the <c>Z</c> suffix is appended
    /// without UTC conversion. That's arguably wrong per ISO-8601 but matches pwiz byte-for-byte.
    /// </summary>
    private static string ConvertTimestamp(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeLocal, out var dto))
            return raw;
        return dto.DateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    // ---------- format detection (port of Reader_Bruker_Detail::format) ----------

    // Moved to a top-level enum so SpectrumList_Bruker / ChromatogramList_Bruker can key on it.

    private static BrukerFormat DetectFormat(string path)
    {
        if (string.IsNullOrEmpty(path)) return BrukerFormat.Unknown;

        // If the path points at a file, map it to the enclosing format.
        if (File.Exists(path))
        {
            string leaf = Path.GetFileName(path).ToLowerInvariant();
            return leaf switch
            {
                "analysis.tdf" or "analysis.tdf_bin" => BrukerFormat.Tdf,
                "analysis.tsf" or "analysis.tsf_bin" => BrukerFormat.Tsf,
                "analysis.baf" => BrukerFormat.Baf,
                "analysis.yep" => BrukerFormat.Yep,
                "fid" => BrukerFormat.Fid,
                _ => BrukerFormat.Unknown,
            };
        }

        if (!Directory.Exists(path)) return BrukerFormat.Unknown;

        if (File.Exists(Path.Combine(path, "analysis.tdf"))
            || File.Exists(Path.Combine(path, "Analysis.tdf")))
            return BrukerFormat.Tdf;
        if (File.Exists(Path.Combine(path, "analysis.tsf"))
            || File.Exists(Path.Combine(path, "Analysis.tsf")))
            return BrukerFormat.Tsf;
        if (File.Exists(Path.Combine(path, "analysis.baf"))
            || File.Exists(Path.Combine(path, "Analysis.baf")))
            return BrukerFormat.Baf;
        if (File.Exists(Path.Combine(path, "analysis.yep"))
            || File.Exists(Path.Combine(path, "Analysis.yep")))
            return BrukerFormat.Yep;
        return BrukerFormat.Unknown;
    }
}
