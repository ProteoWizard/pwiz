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
/// <see cref="IReader"/> for Bruker <c>.d</c> analysis directories: the timsTOF TDF format
/// (<c>analysis.tdf</c>), the non-mobility timsTOF TSF format (<c>analysis.tsf</c>) and BAF
/// (<c>analysis.baf</c>) through Bruker's native SDKs, plus YEP (<c>analysis.yep</c>) and FID
/// through the CompassXtract COM server — the last two being Windows-only.
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

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Bruker .d reading requires the vendor SDK. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        int preferOnlyMsLevel = config?.PreferOnlyMsLevel ?? 0;
        bool combineIms = config?.CombineIonMobilitySpectra ?? CombineIonMobilitySpectra;
        bool sortAndJitter = config?.SortAndJitter ?? false;
        bool peakPicking = config?.PeakPicking ?? false;
        // diaPASEF whole-frame emission: one combined spectrum per frame (all isolation windows'
        // peaks + per-peak isolation arrays), mirroring pwiz C++ SpectrumList_Bruker.cpp:432-490.
        // includeIsolationArrays adds the two scanning-quadrupole m/z arrays (only used by the
        // whole-frame path). Both are honored only for TDF diaPASEF combined mode.
        bool passEntireDiaPasefFrame = config?.PassEntireDiaPasefFrame ?? false;
        // `?? true` matches a default-constructed cpp Reader::Config (Reader.cpp:56), so a null
        // config behaves like the default one rather than opting out.
        bool includeIsolationArrays = config?.IncludeIsolationArrays ?? true;
        // NB: this flag is only the CALLER's request. pwiz C++ auto-enables it for
        // DiagonalPASEF/MiDIA when (maxNumScans - maxWindowsPerGroup) < 10 and does so with `|=`
        // in the TimsData ctor (TimsData.cpp:311), so an explicit `false` from the caller is
        // intentionally overridden on diagonal data; every consumer then reads the OR-ed value via
        // isPassEntireDiaPasefFrame() (TimsData.cpp:816). We match that in TdfData: the OR against
        // TdfMetadata.IsDiagonalPasef happens where the flag is consumed (BuildSpectrumIndex and
        // EnumerateChromatogramPoints), covering spectra and chromatograms alike. Do NOT "fix"
        // this to honor a caller's explicit false — that would be the divergence from C++.
        string analysisDir = Directory.Exists(filename)
            ? filename
            : (Path.GetDirectoryName(filename) ?? throw new ArgumentException("Bruker path must be a .d directory or file inside one."));
        analysisDir = Path.GetFullPath(analysisDir);

        var data = BrukerData.Create(analysisDir);
        try
        {
            ReadImpl(result, data, analysisDir, preferOnlyMsLevel, combineIms, sortAndJitter, peakPicking,
                passEntireDiaPasefFrame, includeIsolationArrays);
        }
        catch
        {
            data.Dispose();
            throw;
        }
#endif
    }

    private static void ReadImpl(MSData result, IBrukerData data, string analysisDir, int preferOnlyMsLevel, bool combineIonMobilitySpectra, bool sortAndJitter, bool peakPicking, bool passEntireDiaPasefFrame, bool includeIsolationArrays)
    {
        result.CVs.AddRange(MSData.DefaultCVList);
        result.Id = Path.GetFileNameWithoutExtension(analysisDir);

        AddSourceFiles(result, analysisDir, data.Format);

        // fileContent reflects the spectra we'll actually emit: preferOnlyMsLevel narrows it.
        if (preferOnlyMsLevel != 2 && data.HasMs1Frames) result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (preferOnlyMsLevel != 1 && data.HasMsNFrames) result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        _ = AddApiSoftware(result, data.Format);
        var acqSoftware = AddAcquisitionSoftware(result, data);
        var pwizSoftware = GetOrAddPwizSoftware(result, "pwiz_Reader_Bruker");

        var dpReader = MakeDataProcessing("pwiz_Reader_Bruker_conversion", pwizSoftware);
        result.DataProcessings.Add(dpReader);
        result.DataProcessings.Add(MakeDataProcessing("pwiz_Reader_conversion", pwizSoftware));

        FillInstrumentMetadata(result, data, acqSoftware);

        result.Run.Id = result.Id;
        // Every format's fillSourceList branch ends by pointing run.defaultSourceFilePtr at the
        // source file it just added - except FID, whose branch does not
        // (SpectrumList_Bruker.cpp:598-613), because a FID run has one source file per spectrum
        // and none of them is "the" default.
        if (data.Format != BrukerFormat.Fid)
            result.Run.DefaultSourceFile = result.FileDescription.SourceFiles.FirstOrDefault();
        result.Run.StartTimeStamp = ConvertTimestamp(data.GlobalMetadata.GetValueOrDefault("AcquisitionDateTime", ""));
        if (result.InstrumentConfigurations.Count > 0)
            result.Run.DefaultInstrumentConfiguration = result.InstrumentConfigurations[0];

        var spectrumList = new SpectrumList_Bruker(
            data, owns: true,
            combineIonMobilitySpectra: combineIonMobilitySpectra,
            preferOnlyMsLevel: preferOnlyMsLevel,
            sortAndJitter: sortAndJitter,
            passEntireDiaPasefFrame: passEntireDiaPasefFrame,
            includeIsolationArrays: includeIsolationArrays)
        { Dp = dpReader };
        result.Run.SpectrumList = spectrumList;

        // Always emit the chromatogram list, as cpp does unconditionally (Reader_Bruker.cpp:255-257).
        // This used to be suppressed for combineIMS-without-peak-picking to match the
        // -combineIMS/-combineIMS-ms1/-combineIMS-ms2 reference mzMLs, which have no
        // chromatogramList. Those references are leftovers from an older revision of
        // Reader_Bruker_Test.cpp: line 131 sets config.peakPicking = true before every combineIMS
        // tier and never clears it, so cpp only ever writes the -centroid ones now and nothing
        // regenerates or checks the others. Live msconvert emits 22 chromatograms for exactly the
        // config the suppression covered, so the suppression made ordinary
        // `--combineIonMobilitySpectra` conversions differ from msconvert.
        result.Run.ChromatogramList = new ChromatogramList_Bruker(data, spectrumList, preferOnlyMsLevel, passEntireDiaPasefFrame) { Dp = dpReader };
    }

    private static void AddSourceFiles(MSData result, string analysisDir, BrukerFormat format)
    {
        if (format == BrukerFormat.Fid)
        {
            AddFidSourceFiles(result, analysisDir);
            return;
        }

        var (baseName, nativeIdFormat, fileFormat) = format switch
        {
            BrukerFormat.Tdf => ("analysis.tdf", CVID.MS_Bruker_TDF_nativeID_format, CVID.MS_Bruker_TDF_format),
            BrukerFormat.Tsf => ("analysis.tsf", CVID.MS_Bruker_TSF_nativeID_format, CVID.MS_Bruker_TSF_format),
            BrukerFormat.Baf => ("analysis.baf", CVID.MS_Bruker_BAF_nativeID_format, CVID.MS_Bruker_BAF_format),
            // A YEP has no "_bin" sibling, so AddPairedSourceFiles emits exactly one entry.
            BrukerFormat.Yep => ("analysis.yep", CVID.MS_Bruker_Agilent_YEP_nativeID_format, CVID.MS_Bruker_Agilent_YEP_format),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        AddPairedSourceFiles(result, analysisDir, baseName, nativeIdFormat, fileFormat);
    }

    /// <summary>
    /// One <c>sourceFile</c> per fid in the tree. Port of the FID branch of
    /// <c>SpectrumList_Bruker::fillSourceList</c> (<c>SpectrumList_Bruker.cpp:598-613</c>) plus
    /// the <c>addSource</c> helper it calls (<c>:568-582</c>): the id is the fid's path relative
    /// to the root directory's parent with forward slashes, the name is just <c>fid</c>, and the
    /// location is the absolute directory the fid sits in.
    /// </summary>
    private static void AddFidSourceFiles(MSData result, string analysisDir)
    {
        foreach (string fidDirectory in BrukerData.EnumerateFidDirectories(analysisDir))
        {
            var sf = new SourceFile(
                BrukerData.FidRelativeId(analysisDir, fidDirectory), "fid", "file://" + fidDirectory);
            sf.Set(CVID.MS_Bruker_FID_nativeID_format);
            sf.Set(CVID.MS_Bruker_FID_format);
            result.FileDescription.SourceFiles.Add(sf);
        }
    }

    private static void AddPairedSourceFiles(MSData result, string analysisDir,
        string baseName, CVID nativeIdFormat, CVID fileFormat)
    {
        string dirName = Path.GetFileName(analysisDir);
        // C++ builds this from bfs::path::string(), which is the NATIVE spelling - backslashes
        // on Windows, forward slashes elsewhere. Forcing backslashes unconditionally turned a
        // POSIX path into one long unusable filename, so nothing downstream could resolve the
        // source file back to disk (the SHA-1 was silently skipped).
        string location = "file://" + (Path.DirectorySeparatorChar == '\\'
            ? analysisDir.Replace('/', '\\')
            : analysisDir);

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

    /// <summary>
    /// Names whichever vendor SDK actually read the file. Port of the <c>format</c> switch at the
    /// top of <c>fillInMetadata</c> in <c>Reader_Bruker.cpp</c>. The versions are hardcoded there
    /// too - neither SDK exposes a version accessor.
    /// </summary>
    private static Software AddApiSoftware(MSData result, BrukerFormat format)
    {
        (string id, CVID cv, string? softwareName, string version) = format switch
        {
            BrukerFormat.Baf => ("BAF2SQL", CVID.MS_Bruker_software, "BAF2SQL", "2.7.300.20-112"),
            BrukerFormat.Tdf or BrukerFormat.Tsf => ("TIMS_SDK", CVID.MS_Bruker_software, "TIMS SDK", "2.21.104.32"),
            // YEP / FID go through CompassXtract (Windows-only in-process COM); cpp's default
            // branch, Reader_Bruker.cpp:144-148, hardcodes the same 3.1.7.
            _ => ("CompassXtract", CVID.MS_CompassXtract, null, "3.1.7"),
        };

        var s = new Software(id) { Version = version };
        s.Set(cv);
        if (softwareName is not null)
            s.UserParams.Add(new UserParam("software name", softwareName));
        result.Software.Add(s);
        return s;
    }

    private static Software AddAcquisitionSoftware(MSData result, IBrukerData data)
    {
        var globalMetadata = data.GlobalMetadata;
        string name = globalMetadata.GetValueOrDefault("AcquisitionSoftware", "");
        string version = globalMetadata.GetValueOrDefault("AcquisitionSoftwareVersion", "");

        // Map the acquisition software name to a CV term. pwiz C++ derives the software.id from
        // cvTermInfo(cvid).shortName() and defaults to MS_Compass when the name is unrecognized.
        CVID cv = TranslateAcquisitionSoftware(name, data.InstrumentFamily);
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

    /// <summary>
    /// Port of <c>translateAsAcquisitionSoftware</c> (<c>Reader_Bruker_Detail.cpp:311-346</c>).
    /// </summary>
    /// <remarks>
    /// The instrument-family fallback is what an empty name selects, and CompassXtract always
    /// gives an empty one (<c>CompassData.cpp:685</c>) — it is the only reason a YEP ion trap
    /// comes out as <c>HCTcontrol</c> and a flex MALDI run as <c>FlexControl</c> rather than the
    /// generic <c>Compass</c>. The SQLite-backed formats do report a name and therefore never
    /// reach it (which is why they were unaffected by its absence here).
    /// </remarks>
    private static CVID TranslateAcquisitionSoftware(string name, BrukerInstrumentFamily family)
    {
        if (name.Length == 0)
            return family switch
            {
                BrukerInstrumentFamily.Trap => CVID.MS_HCTcontrol,
                BrukerInstrumentFamily.Otof or BrukerInstrumentFamily.OtofQ => CVID.MS_micrOTOFcontrol,
                BrukerInstrumentFamily.MaldiTof => CVID.MS_FlexControl,
                BrukerInstrumentFamily.Ftms or BrukerInstrumentFamily.SolariX => CVID.MS_apexControl,
                // BioTOF / BioTOFQ / maXis / compact / impact / Unknown all land on Compass.
                _ => CVID.MS_Compass,
            };

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
        var family = data.InstrumentFamily;

        CVID sourceCv;
        CVID? inletCv = null;
        if (data.IsMaldiSource)
            sourceCv = CVID.MS_matrix_assisted_laser_desorption_ionization;
        else
            (sourceCv, inletCv) = TranslateInstrumentSource(data.GlobalMetadata, family);

        var ic = BuildInstrumentConfiguration(
            result, data.GlobalMetadata, acqSoftware, family, sourceCv, inletCv, data.InstrumentDescription);

        // DIA-PASEF window groups are a TDF-only annotation on the instrument config.
        if (data is TdfData tdf)
            AddDiaPasefWindowGroupUserParams(ic, tdf.Metadata, tdf.TimsBinaryData);

        result.InstrumentConfigurations.Add(ic);
    }

    private static InstrumentConfiguration BuildInstrumentConfiguration(
        MSData result, IReadOnlyDictionary<string, string> globalMetadata, Software acqSoftware,
        BrukerInstrumentFamily family, CVID sourceCv, CVID? inletCv, string instrumentDescription)
    {
        string serial = globalMetadata.GetValueOrDefault("InstrumentSerialNumber", "");

        var common = new ParamGroup("CommonInstrumentParams");
        common.Set(TranslateInstrumentSeries(family));
        // cpp Reader_Bruker.cpp:97-98 - only CompassXtract reports a model string, so this is a
        // YEP / FID-only userParam in practice.
        if (!string.IsNullOrEmpty(instrumentDescription))
            common.UserParams.Add(new UserParam("instrument model", instrumentDescription));
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
        foreach (var analyzerOrDetector in AnalyzerAndDetectorComponents(family))
            ic.ComponentList.Add(new Component(analyzerOrDetector, ic.ComponentList.Count + 1));
        return ic;
    }

    /// <summary>
    /// The analyzer / detector chain that follows the ion source, keyed by instrument family.
    /// Port of the second <c>getInstrumentFamily</c> switch in
    /// <c>Reader_Bruker_Detail::createInstrumentConfigurations</c>.
    /// </summary>
    private static CVID[] AnalyzerAndDetectorComponents(BrukerInstrumentFamily family) => family switch
    {
        BrukerInstrumentFamily.Trap =>
            new[] { CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier },

        BrukerInstrumentFamily.Otof or BrukerInstrumentFamily.MaldiTof =>
            new[] { CVID.MS_time_of_flight, CVID.MS_microchannel_plate_detector, CVID.MS_photomultiplier },

        BrukerInstrumentFamily.OtofQ or BrukerInstrumentFamily.BioTofQ or BrukerInstrumentFamily.Maxis
            or BrukerInstrumentFamily.Impact or BrukerInstrumentFamily.Compact or BrukerInstrumentFamily.TimsTof =>
            new[] { CVID.MS_quadrupole, CVID.MS_time_of_flight, CVID.MS_microchannel_plate_detector, CVID.MS_photomultiplier },

        BrukerInstrumentFamily.Ftms or BrukerInstrumentFamily.SolariX =>
            new[] { CVID.MS_FT_ICR, CVID.MS_inductive_detector },

        // Unknown family - C++ leaves the configuration with just the source component.
        _ => Array.Empty<CVID>(),
    };

    /// <summary>
    /// The instrument-model CV term that goes into the <c>CommonInstrumentParams</c> group.
    /// Port of <c>Reader_Bruker_Detail::translateAsInstrumentSeries</c>.
    /// </summary>
    private static CVID TranslateInstrumentSeries(BrukerInstrumentFamily family) => family switch
    {
        BrukerInstrumentFamily.Trap => CVID.MS_Bruker_Daltonics_HCT_Series,
        BrukerInstrumentFamily.Otof or BrukerInstrumentFamily.OtofQ => CVID.MS_Bruker_Daltonics_micrOTOF_series,
        BrukerInstrumentFamily.BioTof or BrukerInstrumentFamily.BioTofQ => CVID.MS_Bruker_Daltonics_BioTOF_series,
        BrukerInstrumentFamily.MaldiTof => CVID.MS_Bruker_Daltonics_flex_series,
        BrukerInstrumentFamily.Ftms => CVID.MS_Bruker_Daltonics_apex_series,
        BrukerInstrumentFamily.SolariX => CVID.MS_Bruker_Daltonics_solarix_series,
        BrukerInstrumentFamily.TimsTof => CVID.MS_Bruker_Daltonics_timsTOF_series,
        BrukerInstrumentFamily.Maxis or BrukerInstrumentFamily.Compact or BrukerInstrumentFamily.Impact =>
            CVID.MS_Bruker_Daltonics_maXis_series,
        _ => CVID.MS_Bruker_Daltonics_instrument_model,
    };

    /// <summary>
    /// Port of <c>Reader_Bruker_Detail::createInstrumentConfigurations</c> for the source /
    /// inlet pair. Maps Bruker's <c>InstrumentSourceType</c> numeric code to CVIDs.
    /// </summary>
    private static (CVID Source, CVID? Inlet) TranslateInstrumentSource(
        IReadOnlyDictionary<string, string> globalMetadata, BrukerInstrumentFamily family)
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
            // AlsoUnknown (0) / Unknown (255): C++ decides on the instrument family instead.
            _ => TranslateInstrumentSourceFromFamily(family),
        };
    }

    /// <summary>
    /// Source term for instruments that do not report a source type. Port of the
    /// <c>InstrumentSource_Unknown</c> branch of <c>createInstrumentConfigurations</c>.
    /// </summary>
    /// <remarks>
    /// C++ additionally promotes FTMS / solariX to MALDI when the first spectrum's
    /// "Mobile Hexapole Position" parameter reads "MALDI" (its own comment calls that a hack).
    /// Here that case is already covered upstream by <see cref="IBrukerData.IsMaldiSource"/>,
    /// which <see cref="FillInstrumentMetadata"/> checks before calling this.
    /// </remarks>
    private static (CVID Source, CVID? Inlet) TranslateInstrumentSourceFromFamily(BrukerInstrumentFamily family) => family switch
    {
        BrukerInstrumentFamily.MaldiTof or BrukerInstrumentFamily.BioTof or BrukerInstrumentFamily.BioTofQ =>
            (CVID.MS_matrix_assisted_laser_desorption_ionization, null),

        // Trap / OTOF / OTOFQ / maXis / compact / impact / FTMS / solariX all default to ESI, as
        // does timsTOF - which C++ does not list here only because it always reports a source.
        _ => (CVID.MS_electrospray_ionization, CVID.MS_electrospray_inlet),
    };

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

    // ---------- format detection ----------

    /// <summary>
    /// Detection lives on <see cref="BrukerData"/> so the factory and the reader cannot drift
    /// apart — they classify a directory identically, which matters most for the FID heuristic:
    /// it has to leave a BAF acquisition that happens to ship a top-level <c>fid</c> classified
    /// as BAF.
    /// </summary>
    private static BrukerFormat DetectFormat(string path) => BrukerData.DetectFormat(path);
}
