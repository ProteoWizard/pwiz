using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
#if !NO_VENDOR_SUPPORT
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
#endif

#pragma warning disable CA1707

namespace Pwiz.Vendor.Shimadzu;

/// <summary>
/// <see cref="IReader"/> for Shimadzu LCMS <c>.lcd</c> files. Identifies <c>.lcd</c> by
/// filename extension (cpp <c>Reader_Shimadzu::identify</c> does the same — there is no
/// content sniff because LCDs are an opaque structured-storage container).
/// </summary>
/// <remarks>
/// Port of pwiz <c>Reader_Shimadzu</c>. Initial scope: MS1 / MS2 spectra (Q-TOF / triple-quad
/// scan / product-ion), file-level TIC, SRM transition chromatograms (or as spectra when
/// <c>srmAsSpectra</c> is set), instrument config from system-name lookup. GC-MS quadrupole
/// data, multi-sample LCDs, and PDA/UV channels are follow-ups.
/// </remarks>
public sealed class Reader_Shimadzu : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Shimadzu LCD";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Shimadzu_Biotech_LCD_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".lcd" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        return IsShimadzuLcd(filename) ? CvType : CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        if (!IsShimadzuLcd(filename))
            throw new InvalidDataException($"Not a Shimadzu .lcd file: {filename}");
        if (!File.Exists(filename))
            throw new FileNotFoundException("Shimadzu .lcd file not found", filename);

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Shimadzu .lcd reading requires the vendor SDK. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        result.CVs.AddRange(MSData.DefaultCVList);

        var effectiveConfig = config ?? new ReaderConfig();
        bool srmAsSpectra = effectiveConfig.SrmAsSpectra;
        bool globalChromsAreMs1Only = effectiveConfig.GlobalChromatogramsAreMs1Only;

        var raw = new ShimadzuRawData(filename, srmAsSpectra);

        // cpp Reader_Shimadzu.cpp:200-205: 8060RX has a known SRM-chromatogram bug, so the
        // cpp port reopens the file with srmAsSpectra=true to work around it. Mirror that.
        if (!srmAsSpectra && raw.SystemName.Contains("8060RX", StringComparison.OrdinalIgnoreCase))
        {
            try { raw.Dispose(); } catch { /* best-effort */ }
            raw = new ShimadzuRawData(filename, srmAsSpectra: true);
            srmAsSpectra = true;
        }

        try
        {
            FillMetadata(result, filename, raw, srmAsSpectra, globalChromsAreMs1Only, effectiveConfig);
        }
        catch
        {
            raw.Dispose();
            throw;
        }
#endif
    }

#if !NO_VENDOR_SUPPORT
    private static void FillMetadata(MSData result, string lcdPath, ShimadzuRawData raw,
        bool srmAsSpectra, bool globalChromsAreMs1Only, ReaderConfig config)
    {
        string fileName = Path.GetFileName(lcdPath);
        string runId = Path.GetFileNameWithoutExtension(fileName);
        result.Id = runId;
        result.Run.Id = runId;

        // fileContent: cpp Reader_Shimadzu.cpp:117-131. SRM-only files declare SRM_chromatogram
        // unless srmAsSpectra is set; MS-spectra files declare MS1 / MSn based on getMSLevels().
        var transitions = raw.Transitions;
        if (transitions.Count > 0 && !srmAsSpectra)
        {
            result.FileDescription.FileContent.Set(CVID.MS_SRM_chromatogram);
        }
        else
        {
            if (raw.MsLevels.Contains(1))
                result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
            if (transitions.Count > 0)
                result.FileDescription.FileContent.Set(CVID.MS_SRM_spectrum);
            else if (raw.MsLevels.Contains(2))
                result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);
        }

        // sourceFile: one entry for the .lcd, with Shimadzu Biotech LCD + nativeID format CVs.
        // cpp Reader_Shimadzu.cpp:139 emits the location as "file:///" + path.string() — boost
        // returns native separators on Windows (backslashes), so we keep them too rather than
        // forcing forward slashes. Both forms are valid file:// URIs; matching cpp byte-for-byte
        // keeps msdiff parity clean.
        string parentDir = Path.GetDirectoryName(Path.GetFullPath(lcdPath)) ?? string.Empty;
        var sourceFile = new SourceFile(fileName, fileName, "file:///" + parentDir);
        sourceFile.Set(CVID.MS_Shimadzu_Biotech_QTOF_nativeID_format);
        sourceFile.Set(CVID.MS_Shimadzu_Biotech_LCD_format);
        result.FileDescription.SourceFiles.Add(sourceFile);
        result.Run.DefaultSourceFile = sourceFile;

        // Software entries: Shimadzu (acquisition) + pwiz (conversion). cpp uses a hardcoded
        // "5.0" version since the SDK doesn't expose one cleanly; mirror that.
        var shimadzuSoftware = new Software("Shimadzu software") { Version = "5.0" };
        shimadzuSoftware.Set(CVID.MS_Shimadzu_Corporation_software);
        result.Software.Add(shimadzuSoftware);

        var pwizSoftware = new Software("pwiz") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        // Single DataProcessing entry.
        var dpReader = new DataProcessing("pwiz_Reader_Shimadzu_conversion");
        var pmReader = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmReader.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmReader);
        result.DataProcessings.Add(dpReader);

        // Instrument config: cpp emits one per (parsed) instrument model. We mirror that — the
        // model is parsed from raw.SystemName via the same lookup table the cpp port uses.
        var ic = BuildInstrumentConfiguration(raw, shimadzuSoftware, srmAsSpectra: srmAsSpectra || transitions.Count > 0, config);
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // Acquisition timestamp. Encoding (incl. the cpp-equivalent host-tz adjustment) lives
        // on ReaderConfig so every vendor reader can share the same logic + flag wiring.
        // Shimadzu is one of the three readers where cpp gates the shift on the config flag
        // (Reader_Shimadzu.cpp: getAnalysisDate(config.adjustUnknownTimeZonesToHostTimeZone)).
        //
        // The conversion to UTC, though, is Shimadzu's alone: ShimadzuReader.cpp:370 reads
        // `SampleInfo->AnalysisDate.ToUniversalTime()`, and it is the ONLY ToUniversalTime call
        // in any cpp vendor reader - every other one formats the SDK's naive wall clock. That is
        // why the shared helper does not convert (doing so there put Mobilion 8 hours out), and
        // why leaving it out here put every Shimadzu stamp one host offset behind cpp: 4 hours
        // on the EDT files, 5 on the one acquired in January.
        var analysisDate = raw.AnalysisDateRaw;
        if (analysisDate != default)
            analysisDate = analysisDate.ToUniversalTime();
        string? startTime = ReaderConfig.FormatStartTimeStamp(
            analysisDate, config.AdjustUnknownTimeZonesToHostTimeZone);
        if (startTime is not null)
            result.Run.StartTimeStamp = startTime;

        // Spectrum list owns the raw handle; chromatogram list shares without owning so a single
        // Dispose chain releases the SDK file. cpp uses the same shared_ptr split.
        result.Run.SpectrumList = new SpectrumList_Shimadzu(raw, ownsRaw: true, ic, srmAsSpectra)
        {
            Dp = dpReader,
        };
        result.Run.ChromatogramList = new ChromatogramList_Shimadzu(raw, ownsRaw: false, srmAsSpectra, globalChromsAreMs1Only)
        {
            Dp = dpReader,
        };
    }

    private static InstrumentConfiguration BuildInstrumentConfiguration(
        ShimadzuRawData raw, Software acquisitionSoftware, bool srmAsSpectra, ReaderConfig config)
    {
        var ic = new InstrumentConfiguration("IC1") { Software = acquisitionSoftware };

        // Instrument model: parse from system-name string. cpp Reader_Shimadzu.cpp:82-88 sets
        // the model CVID directly on the IC and falls back to MS_Shimadzu_instrument_model with
        // a "system name" userParam when the lookup fails -- and, unlike Thermo, treats that
        // fallback as an instrumentMetadataError unconditionally (no emptiness precondition).
        var modelCvid = TranslateInstrumentModel(raw.SystemName);
        ic.Set(modelCvid);
        if (modelCvid == CVID.MS_Shimadzu_instrument_model)
        {
            config.InstrumentMetadataError(
                "unable to parse instrument model; make sure you are using the latest version of " +
                "ProteoWizard; if you are, please report this error to the ProteoWizard developers " +
                $"with this information: systemName({raw.SystemName})");
            if (!string.IsNullOrEmpty(raw.SystemName))
                ic.UserParams.Add(new UserParam("system name", raw.SystemName));
        }

        // Source: cpp always emits ESI for Shimadzu LCMS data.
        ic.ComponentList.Add(new Component(CVID.MS_ESI, 1));

        // Mass analyzers + detector: triple-quad path when SRM transitions exist or
        // srmAsSpectra promoted them; QqTOF otherwise. cpp Reader_Shimadzu.cpp:92-107.
        if (srmAsSpectra)
        {
            var q2 = new Component(CVID.MS_quadrupole, 2);
            var q3 = new Component(CVID.MS_quadrupole, 3);
            var q4 = new Component(CVID.MS_quadrupole, 4);
            var detector = new Component(CVID.MS_conversion_dynode_electron_multiplier, 5);
            detector.Set(CVID.MS_pulse_counting);
            ic.ComponentList.Add(q2);
            ic.ComponentList.Add(q3);
            ic.ComponentList.Add(q4);
            ic.ComponentList.Add(detector);
        }
        else
        {
            var q2 = new Component(CVID.MS_quadrupole, 2);
            var q3 = new Component(CVID.MS_quadrupole, 3);
            var tof = new Component(CVID.MS_TOF, 4);
            var detector = new Component(CVID.MS_microchannel_plate_detector, 5);
            detector.Set(CVID.MS_pulse_counting);
            ic.ComponentList.Add(q2);
            ic.ComponentList.Add(q3);
            ic.ComponentList.Add(tof);
            ic.ComponentList.Add(detector);
        }
        return ic;
    }

    /// <summary>
    /// Mirrors cpp <c>parseInstrumentModelType</c> in <c>Reader_Shimadzu_Detail.cpp</c>: longest
    /// (case-insensitive, space/dash-stripped) substring match wins; falls back to the generic
    /// Shimadzu model term. Initial port covers the most common LCMS QTOF / triple-quad models;
    /// extending the table is mechanical and can happen as new fixtures land.
    /// </summary>
    private static CVID TranslateInstrumentModel(string systemName)
    {
        if (string.IsNullOrEmpty(systemName)) return CVID.MS_Shimadzu_instrument_model;
        string normalized = NormalizeName(systemName);

        CVID bestMatch = CVID.MS_Shimadzu_instrument_model;
        int bestLength = 0;
        foreach (var (name, cvid, exact) in NameToModelMapping)
        {
            string candidate = NormalizeName(name);
            if (exact)
            {
                if (candidate == normalized) return cvid;
                continue;
            }
            if (normalized.Contains(candidate, StringComparison.Ordinal) && candidate.Length > bestLength)
            {
                bestMatch = cvid;
                bestLength = candidate.Length;
            }
        }
        return bestMatch;
    }

    private static string NormalizeName(string name)
        => name.ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal)
                                   .Replace("-", string.Empty, StringComparison.Ordinal);

    // Subset of the cpp table, sorted longest-substring-first. cpp file:
    // pwiz/data/vendor_readers/Shimadzu/Reader_Shimadzu_Detail.cpp:35-87.
    private static readonly (string Name, CVID Cvid, bool Exact)[] NameToModelMapping = new (string, CVID, bool)[]
    {
        ("8045RX", CVID.MS_LCMS_8045RX, false),
        ("8050RX", CVID.MS_LCMS_8050RX, false),
        ("8060RX", CVID.MS_LCMS_8060RX, false),
        ("8060NX", CVID.MS_LCMS_8060NX, false),
        ("8045", CVID.MS_LCMS_8045, false),
        ("8050", CVID.MS_LCMS_8050, false),
        ("8060", CVID.MS_LCMS_8060, false),
        ("8040", CVID.MS_LCMS_8040, false),
        ("9030", CVID.MS_LCMS_9030, false),
        ("9050", CVID.MS_LCMS_9050, false),
        ("2020", CVID.MS_LCMS_2020, false),
        ("2050", CVID.MS_LCMS_2050, false),
        ("LCMS", CVID.MS_Shimadzu_Scientific_Instruments_instrument_model, true),
    };
#endif

    /// <summary>
    /// Pure filename test, with no dependency on the Shimadzu SDK. It lives here rather than on
    /// ShimadzuRawData because that type is compiled out in NO_VENDOR_SUPPORT builds, and
    /// Identify() has to keep working in that mode -- that is the whole point of it.
    /// </summary>
    private static bool IsShimadzuLcd(string path)
    {
        return !string.IsNullOrEmpty(path)
            && path.EndsWith(".lcd", StringComparison.OrdinalIgnoreCase);
    }
}
