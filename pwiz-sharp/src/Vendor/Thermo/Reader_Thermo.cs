using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// <see cref="IReader"/> for Thermo .raw files. Identifies by the magic header
/// (<c>0x01 0xA1</c> + "Finnigan" in UTF-16) or by <c>.raw</c> extension; parses via
/// the Net8 <c>ThermoFisher.CommonCore.RawFileReader</c> assemblies.
/// </summary>
/// <remarks>Port of pwiz::msdata::Reader_Thermo.</remarks>
public sealed class Reader_Thermo : IReader
{
    // '\x01\xA1' prefix + "Finnigan" encoded as little-endian UTF-16 (each char followed by \0).
    private static readonly byte[] s_rawHeader =
    {
        0x01, 0xA1,
        (byte)'F', 0, (byte)'i', 0, (byte)'n', 0, (byte)'n', 0,
        (byte)'i', 0, (byte)'g', 0, (byte)'a', 0, (byte)'n', 0,
    };

    /// <inheritdoc/>
    public string TypeName => "Thermo RAW";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Thermo_RAW_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".raw" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        // Content sniff first: the magic bytes are unambiguous.
        if (head is not null && HasThermoHeader(head))
            return CvType;

        // Fall back to extension match — Thermo .raw files use that suffix.
        if (filename.EndsWith(".raw", StringComparison.OrdinalIgnoreCase)
            && File.Exists(filename)
            && !Directory.Exists(filename)) // Waters .raw is a *directory*, not a file
        {
            return CvType;
        }

        return CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

#if NO_VENDOR_SUPPORT
        throw new NotSupportedException(
            "Thermo .raw reading requires the vendor SDK. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        result.CVs.AddRange(MSData.DefaultCVList);
        result.Id = Path.GetFileNameWithoutExtension(filename);

        // Document metadata: emit only MS levels actually present. pwiz C++ walks scans up
        // front for the same reason — RAW files often contain only one MS level.
        bool hasMs1 = false, hasMsn = false;

        // C++ uses the id "RAW<n>" (1-based) and the location is the *parent directory* of the
        // .raw file with a "file:///" prefix. SHA-1 is computed post-read by MsDataFileChecksums.
        string location = "file:///" + (Path.GetDirectoryName(Path.GetFullPath(filename)) ?? string.Empty);
        var sourceFile = new SourceFile("RAW1", Path.GetFileName(filename), location);
        sourceFile.Set(CVID.MS_Thermo_nativeID_format);
        sourceFile.Set(CVID.MS_Thermo_RAW_format);
        result.FileDescription.SourceFiles.Add(sourceFile);

        // Delegate spectrum-list access to a lazy-reading SpectrumList_Thermo.
        // The .raw file stays open for the lifetime of the list.
        var raw = new ThermoRawFile(filename);

        // Walk scan filters for the ms-level sniff — fast (doesn't decode peaks).
        for (int scan = raw.FirstScan; scan <= raw.LastScan; scan++)
        {
            int ms = (int)raw.Raw.GetFilterForScanNumber(scan).MSOrder;
            if (ms == 1) hasMs1 = true;
            else if (ms > 1) hasMsn = true;
            if (hasMs1 && hasMsn) break;
        }
        if (hasMs1) result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (hasMsn) result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        // Cpp emits MS_EMR_spectrum (electromagnetic radiation spectrum) when the file has any
        // PDA-as-spectra entries — Reader_Thermo.cpp:228-229.
        if (raw.PdaControllerCount > 0)
            result.FileDescription.FileContent.Set(CVID.MS_EMR_spectrum);

        var icByAnalyzer = FillInstrumentConfiguration(result, raw, out var pdaIc);

        // Sample list: Thermo exposes a single SampleId; emit a Sample entry matching pwiz C++.
        string sampleId = TryGetSampleId(raw);
        if (!string.IsNullOrEmpty(sampleId))
        {
            var sample = new Sample(sampleId);
            sample.Set(CVID.MS_sample_name, sampleId);
            result.Samples.Add(sample);
        }

        // DataProcessing entries: pwiz_Reader_Thermo_conversion (from Reader_Thermo) followed by
        // pwiz_Reader_conversion (mirrors fillInCommonMetadata from pwiz's DefaultReaderList.cpp).
        var pwizSoftware = GetOrAddPwizSoftware(result);
        var dpThermo = new DataProcessing("pwiz_Reader_Thermo_conversion");
        var pmThermo = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmThermo.Set(CVID.MS_Conversion_to_mzML);
        dpThermo.ProcessingMethods.Add(pmThermo);
        result.DataProcessings.Add(dpThermo);

        var dpCommon = new DataProcessing("pwiz_Reader_conversion");
        var pmCommon = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmCommon.Set(CVID.MS_Conversion_to_mzML);
        dpCommon.ProcessingMethods.Add(pmCommon);
        result.DataProcessings.Add(dpCommon);

        result.Run.Id = Path.GetFileNameWithoutExtension(filename);
        result.Run.DefaultSourceFile = sourceFile;
        result.Run.StartTimeStamp = raw.CreationDate;
        if (result.InstrumentConfigurations.Count > 0)
            result.Run.DefaultInstrumentConfiguration = result.InstrumentConfigurations[0];
        bool simAsSpectra = config?.SimAsSpectra ?? false;
        bool srmAsSpectra = config?.SrmAsSpectra ?? false;
        var list = new SpectrumList_Thermo(raw, ownsRaw: true,
            result.Run.DefaultInstrumentConfiguration, icByAnalyzer, simAsSpectra, srmAsSpectra, pdaIc)
        {
            Dp = dpThermo,
        };
        result.Run.SpectrumList = list;
        var chromList = new ChromatogramList_Thermo(raw, simAsSpectra, srmAsSpectra) { Dp = dpThermo };
        result.Run.ChromatogramList = chromList;
        // Advertise SIM/SRM chromatograms in fileContent when emitted (matches cpp's reference
        // mzML metadata for files that produce these chromatogram types).
        if (chromList.HasSimChromatograms)
            result.FileDescription.FileContent.Set(CVID.MS_selected_ion_monitoring_chromatogram);
        if (chromList.HasSrmChromatograms)
            result.FileDescription.FileContent.Set(CVID.MS_selected_reaction_monitoring_chromatogram);
#endif
    }

#if !NO_VENDOR_SUPPORT
    private static Software GetOrAddPwizSoftware(MSData msd)
    {
        foreach (var s in msd.Software)
        {
            if (s.HasCVParam(CVID.MS_pwiz)) return s;
        }
        var pwiz = new Software("pwiz") { Version = MSData.PwizVersion };
        pwiz.Set(CVID.MS_pwiz);
        msd.Software.Add(pwiz);
        return pwiz;
    }

    /// <summary>
    /// Fills the document's <see cref="MSData.InstrumentConfigurations"/> from <paramref name="raw"/>.
    /// Returns a dictionary mapping each MS analyzer type to its configuration plus an
    /// out parameter for the PDA configuration (null if the file has no PDA controller).
    /// </summary>
    private static Dictionary<ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType, InstrumentConfiguration>
        FillInstrumentConfiguration(MSData result, ThermoRawFile raw, out InstrumentConfiguration? pdaIc)
    {
        // Software: Xcalibur (acquisition). pwiz Software entry is added separately by the caller.
        var xcalibur = new Software("Xcalibur")
        {
            Version = SafeInstrumentProperty(raw, d => d.SoftwareVersion),
        };
        xcalibur.Set(CVID.MS_Xcalibur);
        result.Software.Add(xcalibur);

        // Common params for every InstrumentConfiguration: instrument model + serial number.
        // Emitted as a referenceable ParamGroup named "CommonInstrumentParams" (mirrors pwiz C++).
        var common = new ParamGroup("CommonInstrumentParams");
        string model = SafeInstrumentProperty(raw, d => d.Model);
        CVID modelCv = TranslateInstrumentModel(model);
        if (modelCv == CVID.MS_Thermo_Electron_instrument_model && !string.IsNullOrEmpty(model))
            common.UserParams.Add(new UserParam("instrument model", model));
        common.Set(modelCv);
        string serial = SafeInstrumentProperty(raw, d => d.SerialNumber);
        if (!string.IsNullOrEmpty(serial))
            common.Set(CVID.MS_instrument_serial_number, serial);
        result.ParamGroups.Add(common);

        // Walk every scan's filter once to discover the distinct analyzer types used in this run.
        // pwiz C++ emits one InstrumentConfiguration per distinct analyzer (IC1 for FT, IC2 for
        // ion trap, etc.) and each spectrum's scan links to the matching IC.
        var analyzers = new List<ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType>();
        for (int s = raw.FirstScan; s <= raw.LastScan; s++)
        {
            var analyzer = raw.Raw.GetFilterForScanNumber(s).MassAnalyzer;
            if (!analyzers.Contains(analyzer))
                analyzers.Add(analyzer);
        }
        if (analyzers.Count == 0)
            analyzers.Add(ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS);

        var map = new Dictionary<ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType, InstrumentConfiguration>();
        for (int i = 0; i < analyzers.Count; i++)
        {
            var ic = new InstrumentConfiguration("IC" + (i + 1).ToString(CultureInfo.InvariantCulture));
            ic.ParamGroups.Add(common);
            ic.Software = xcalibur;

            // pwiz C++ returns nanoelectrospray as the first ionization type for every Orbitrap-class
            // (and most LTQ-class) instrument in the Thermo model table. Since our current coverage
            // is Orbitrap-only, default to nanoESI. A full port of
            // Reader_Thermo_Detail::getIonSourcesForInstrumentModel() is a follow-up.
            var source = new Component(CVID.MS_nanoelectrospray, 1);
            source.Set(CVID.MS_nanospray_inlet);
            ic.ComponentList.Add(source);

            (CVID analyzerCv, CVID detectorCv) = TranslateAnalyzer(analyzers[i]);
            ic.ComponentList.Add(new Component(analyzerCv, 2));
            ic.ComponentList.Add(new Component(detectorCv, 3));

            result.InstrumentConfigurations.Add(ic);
            map[analyzers[i]] = ic;
        }

        // Append a separate "PDA" IC when a PDA controller is present (mirror of cpp
        // Reader_Thermo_Detail.cpp:198-203). Single component: MS_PDA detector, order 1.
        pdaIc = null;
        if (raw.PdaControllerCount > 0)
        {
            pdaIc = new InstrumentConfiguration("PDA");
            pdaIc.ComponentList.Add(new Component(CVID.MS_PDA, 1));
            result.InstrumentConfigurations.Add(pdaIc);
        }
        return map;
    }

    /// <summary>
    /// Translates a Thermo instrument model name (e.g. "LTQ FT") to the corresponding CV term.
    /// Subset of pwiz's <c>translateAsInstrumentModel</c>; falls back to the generic
    /// MS_Thermo_Electron_instrument_model for unknown strings.
    /// </summary>
    private static CVID TranslateInstrumentModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return CVID.MS_Thermo_Electron_instrument_model;
        string m = model.Trim().ToUpperInvariant();
        return m switch
        {
            "LTQ FT" => CVID.MS_LTQ_FT,
            "LTQ FT ULTRA" => CVID.MS_LTQ_FT_Ultra,
            "LTQ ORBITRAP" => CVID.MS_LTQ_Orbitrap,
            "LTQ ORBITRAP DISCOVERY" => CVID.MS_LTQ_Orbitrap_Discovery,
            "LTQ ORBITRAP XL" => CVID.MS_LTQ_Orbitrap_XL,
            "LTQ ORBITRAP VELOS" => CVID.MS_LTQ_Orbitrap_Velos,
            "LTQ ORBITRAP ELITE" => CVID.MS_LTQ_Orbitrap_Elite,
            "ORBITRAP FUSION" => CVID.MS_Orbitrap_Fusion,
            "ORBITRAP FUSION LUMOS" => CVID.MS_Orbitrap_Fusion_Lumos,
            "ORBITRAP ECLIPSE" => CVID.MS_Orbitrap_Eclipse,
            "ORBITRAP EXPLORIS 240" => CVID.MS_Orbitrap_Exploris_240,
            "ORBITRAP EXPLORIS 480" => CVID.MS_Orbitrap_Exploris_480,
            "Q EXACTIVE" => CVID.MS_Q_Exactive,
            "Q EXACTIVE PLUS" => CVID.MS_Q_Exactive_Plus,
            "Q EXACTIVE HF" => CVID.MS_Q_Exactive_HF,
            "Q EXACTIVE HF-X" => CVID.MS_Q_Exactive_HF_X,
            "LTQ" => CVID.MS_LTQ,
            "LTQ XL" => CVID.MS_LTQ_XL,
            "LTQ VELOS" => CVID.MS_LTQ_Velos,
            _ => CVID.MS_Thermo_Electron_instrument_model,
        };
    }

    private static (CVID analyzer, CVID detector) TranslateAnalyzer(
        ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType t)
    {
        return t switch
        {
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS =>
                (CVID.MS_fourier_transform_ion_cyclotron_resonance, CVID.MS_inductive_detector),
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS =>
                (CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier),
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerTQMS =>
                (CVID.MS_quadrupole, CVID.MS_electron_multiplier),
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerSQMS =>
                (CVID.MS_quadrupole, CVID.MS_electron_multiplier),
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerTOFMS =>
                (CVID.MS_time_of_flight, CVID.MS_microchannel_plate_detector),
            ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerSector =>
                (CVID.MS_magnetic_sector, CVID.MS_electron_multiplier),
            _ => (CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier),
        };
    }

    private static string TryGetSampleId(ThermoRawFile raw)
    {
        try { return raw.Raw.SampleInformation?.SampleId ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafeInstrumentProperty(ThermoRawFile raw, Func<ThermoFisher.CommonCore.Data.Business.InstrumentData, string> selector)
    {
        try
        {
            var data = raw.Raw.GetInstrumentData();
            return selector(data) ?? string.Empty;
        }
        catch { return string.Empty; }
    }
#endif

    internal static bool HasThermoHeader(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        // `head` is a UTF-8-ish string where bytes 0x80+ may have been substituted. For robustness,
        // do a byte-level comparison on the raw string bytes (Latin-1 gives us a 1:1 char↔byte mapping).
        if (head.Length < s_rawHeader.Length) return false;
        for (int i = 0; i < s_rawHeader.Length; i++)
        {
            if ((byte)head[i] != s_rawHeader[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Overload for byte-span sniffing: avoids the <see cref="string"/> round-trip that can
    /// mangle the 0xA1 byte when the caller's encoding isn't Latin-1 / raw.
    /// </summary>
    public static bool HasThermoHeader(ReadOnlySpan<byte> head)
    {
        if (head.Length < s_rawHeader.Length) return false;
        for (int i = 0; i < s_rawHeader.Length; i++)
            if (head[i] != s_rawHeader[i]) return false;
        return true;
    }
}
