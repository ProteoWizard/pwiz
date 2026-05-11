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
        if (!File.Exists(filename))
            throw new FileNotFoundException("Thermo .raw file not found", filename);

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
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

        // Reader_Thermo's only DataProcessing entry. cpp's Reader_Thermo doesn't add a second
        // pwiz_Reader_conversion (the earlier C# port did — that produced two near-identical
        // DataProcessing entries on every Thermo conversion, plus msdiff would synthesize a
        // bogus second "pwiz_3.0.<version>" software entry from the duplicate softwareRef).
        var pwizSoftware = GetOrAddPwizSoftware(result);
        var dpThermo = new DataProcessing("pwiz_Reader_Thermo_conversion");
        var pmThermo = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmThermo.Set(CVID.MS_Conversion_to_mzML);
        dpThermo.ProcessingMethods.Add(pmThermo);
        result.DataProcessings.Add(dpThermo);

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
        // The CommonCore SDK populates InstrumentData.Name and InstrumentData.Model
        // inconsistently across instrument families: on Orbitrap-class files Model is the
        // marketing name ("Orbitrap Fusion"); on older Surveyor/MSQ files Model holds a
        // version string ("2.0 SP") and the actual model name is in Name. Try both and use
        // whichever <see cref="ThermoInstrumentModel.Translate"/> recognizes.
        string modelProp = SafeInstrumentProperty(raw, d => d.Model);
        string nameProp = SafeInstrumentProperty(raw, d => d.Name);
        CVID modelCv = TranslateInstrumentModel(modelProp);
        string model = modelProp;
        if (modelCv == CVID.MS_Thermo_Electron_instrument_model && !string.IsNullOrEmpty(nameProp))
        {
            CVID nameCv = TranslateInstrumentModel(nameProp);
            if (nameCv != CVID.MS_Thermo_Electron_instrument_model)
            {
                modelCv = nameCv;
                model = nameProp;
            }
        }
        if (modelCv == CVID.MS_Thermo_Electron_instrument_model && !string.IsNullOrEmpty(model))
            common.UserParams.Add(new UserParam("instrument model", model));
        common.Set(modelCv);
        string serial = SafeInstrumentProperty(raw, d => d.SerialNumber);
        if (!string.IsNullOrEmpty(serial))
            common.Set(CVID.MS_instrument_serial_number, serial);
        result.ParamGroups.Add(common);

        // Build InstrumentConfigurations from the instrument MODEL — same recipe cpp uses
        // (Reader_Thermo_Detail.cpp:258+). The previous analyzer-driven walk was wrong:
        // every "FTMS" filter got mapped to MS_FT_ICR even on Orbitraps, and hybrid
        // instruments (Fusion, LTQ-Orbitrap, LTQ-FT, ...) lost their second IC unless the
        // alternate analyzer fired in the run. Each model has a fixed list of (analyzer, ...
        // detector) chains; we emit every one and bind scans to the IC that contains their
        // runtime analyzer.
        // Source/inlet: cpp reads the FIRST scan's ionization mode and uses that for every
        // IC's commonSource (Reader_Thermo_Detail.cpp:185-194). A run with APCI emits APCI
        // for every IC; nanoESI emits nanoESI + nanospray-inlet; etc.
        var firstIonization = ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.ElectroSpray;
        try
        {
            firstIonization = raw.Raw.GetFilterForScanNumber(raw.FirstScan).IonizationMode;
        }
        catch { /* no scans / SDK quirk — fall back to ESI like cpp */ }
        var (sourceCv, inletCv) = TranslateIonization(firstIonization);

        var icRecipe = GetInstrumentConfigurationRecipe(modelCv);
        var map = new Dictionary<ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType, InstrumentConfiguration>();
        for (int i = 0; i < icRecipe.Count; i++)
        {
            var (analyzerComponents, primaryAnalyzer) = icRecipe[i];
            var ic = new InstrumentConfiguration("IC" + (i + 1).ToString(CultureInfo.InvariantCulture));
            ic.ParamGroups.Add(common);
            ic.Software = xcalibur;

            var source = new Component(sourceCv, 1);
            if (inletCv != CVID.CVID_Unknown) source.Set(inletCv);
            ic.ComponentList.Add(source);

            int order = 2;
            foreach (var componentCv in analyzerComponents)
                ic.ComponentList.Add(new Component(componentCv, order++));

            result.InstrumentConfigurations.Add(ic);
            // Bind every runtime analyzer that resolves to this IC's primary analyzer.
            map[primaryAnalyzer] = ic;
        }
        if (icRecipe.Count == 0)
        {
            // Unknown model — fall back to single IC per observed runtime analyzer.
            var observed = new HashSet<ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType>();
            for (int s = raw.FirstScan; s <= raw.LastScan; s++)
                observed.Add(raw.Raw.GetFilterForScanNumber(s).MassAnalyzer);
            int n = 0;
            foreach (var analyzer in observed)
            {
                var ic = new InstrumentConfiguration("IC" + (++n).ToString(CultureInfo.InvariantCulture));
                ic.ParamGroups.Add(common);
                ic.Software = xcalibur;
                var source = new Component(sourceCv, 1);
                if (inletCv != CVID.CVID_Unknown) source.Set(inletCv);
                ic.ComponentList.Add(source);
                (CVID analyzerCv, CVID detectorCv) = TranslateAnalyzer(analyzer);
                ic.ComponentList.Add(new Component(analyzerCv, 2));
                ic.ComponentList.Add(new Component(detectorCv, 3));
                result.InstrumentConfigurations.Add(ic);
                map[analyzer] = ic;
            }
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
    /// Translates a Thermo instrument model name (e.g. <c>"LTQ FT"</c>, <c>"Orbitrap Fusion"</c>) to
    /// the corresponding CV term. Delegates to <see cref="ThermoInstrumentModel.Translate"/>,
    /// which is a hand-port of the cpp <c>nameToModelMapping</c> + <c>translateAsInstrumentModel</c>
    /// pair (see <c>RawFileTypes.h</c> / <c>Reader_Thermo_Detail.cpp</c>) and recognizes the full
    /// catalog of Thermo instruments (~100 entries with Exact / ExactNoSpaces / Contains /
    /// ContainsNoSpaces match modes).
    /// </summary>
    private static CVID TranslateInstrumentModel(string model) => ThermoInstrumentModel.Translate(model);

    /// <summary>Maps a Thermo SDK <c>IonizationModeType</c> to (source CV term, inlet CV term)
    /// pair. Mirrors cpp <c>translateAsIonizationType</c> + <c>translateAsInletType</c>
    /// (Reader_Thermo_Detail.cpp:535-572). Unknown values fall back to ESI (cpp default).</summary>
    internal static (CVID source, CVID inlet) TranslateIonization(
        ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType m)
    {
        return m switch
        {
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.ElectronImpact =>
                (CVID.MS_electron_ionization, CVID.CVID_Unknown),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.ChemicalIonization =>
                (CVID.MS_chemical_ionization, CVID.CVID_Unknown),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.FastAtomBombardment =>
                (CVID.MS_fast_atom_bombardment_ionization, CVID.MS_continuous_flow_fast_atom_bombardment),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.ElectroSpray =>
                (CVID.MS_electrospray_ionization, CVID.MS_electrospray_inlet),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.NanoSpray =>
                (CVID.MS_nanoelectrospray, CVID.MS_nanospray_inlet),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.AtmosphericPressureChemicalIonization =>
                (CVID.MS_atmospheric_pressure_chemical_ionization, CVID.CVID_Unknown),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.ThermoSpray =>
                (CVID.CVID_Unknown, CVID.MS_thermospray_inlet),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.FieldDesorption =>
                (CVID.MS_field_desorption, CVID.CVID_Unknown),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.MatrixAssistedLaserDesorptionIonization =>
                (CVID.MS_matrix_assisted_laser_desorption_ionization, CVID.CVID_Unknown),
            ThermoFisher.CommonCore.Data.FilterEnums.IonizationModeType.GlowDischarge =>
                (CVID.MS_glow_discharge_ionization, CVID.CVID_Unknown),
            _ => (CVID.MS_electrospray_ionization, CVID.CVID_Unknown), // cpp default for Unknown / Any
        };
    }

    internal static (CVID analyzer, CVID detector) TranslateAnalyzer(
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

    /// <summary>Looks up the canonical instrument-configuration recipe for a Thermo model:
    /// one entry per IC the model exposes, each entry listing the analyzer-plus-detector
    /// chain (orders 2-N; the source at order 1 is added by the caller). Mirrors cpp
    /// <c>createInstrumentConfigurations</c> (Reader_Thermo_Detail.cpp:258-468). Models not
    /// listed here fall through to the legacy runtime-analyzer walk.
    /// </summary>
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        OrbitrapQuadHybridRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_orbitrap, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        ExactiveRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_orbitrap, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        FusionRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_orbitrap, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        LtqFtRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_fourier_transform_ion_cyclotron_resonance, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
            ((IReadOnlyList<CVID>)new[] { CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        LtqOrbitrapRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_orbitrap, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
            ((IReadOnlyList<CVID>)new[] { CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        IonTrapRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole_ion_trap, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        LinearIonTrapRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_radial_ejection_linear_ion_trap, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerITMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        SingleQuadRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerSQMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        TripleQuadRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_quadrupole, CVID.MS_quadrupole, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerTQMS),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        SectorRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_magnetic_sector, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerSector),
        };
    private static readonly IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        AstralRecipe = new[]
        {
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_orbitrap, CVID.MS_inductive_detector },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerFTMS),
            ((IReadOnlyList<CVID>)new[] { CVID.MS_quadrupole, CVID.MS_asymmetric_track_lossless_time_of_flight_analyzer, CVID.MS_electron_multiplier },
             ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType.MassAnalyzerASTMS),
        };


    /// <summary>Test-facing view of <see cref="GetInstrumentConfigurationRecipe"/> that strips
    /// the SDK MassAnalyzerType enum from each entry — returns just the source-less component
    /// chains. The Thermo.Tests parity suite uses this to assert byte-equal recipes against
    /// the cpp <c>createInstrumentConfigurations</c> reference TSV.</summary>
    internal static IReadOnlyList<IReadOnlyList<CVID>> GetInstrumentConfigurationComponents(CVID model)
    {
        var recipe = GetInstrumentConfigurationRecipe(model);
        var result = new IReadOnlyList<CVID>[recipe.Count];
        for (int i = 0; i < recipe.Count; i++) result[i] = recipe[i].Components;
        return result;
    }

    private static IReadOnlyList<(IReadOnlyList<CVID> Components, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType Primary)>
        GetInstrumentConfigurationRecipe(CVID model) => model switch
        {
            // Q-Exactive + Exploris family: quad + orbitrap + inductive (one IC).
            CVID.MS_Q_Exactive
                or CVID.MS_Q_Exactive_Plus
                or CVID.MS_Q_Exactive_HF
                or CVID.MS_Q_Exactive_HF_X
                or CVID.MS_Q_Exactive_UHMR
                or CVID.MS_Q_Exactive_Focus
                or CVID.MS_Q_Exactive_GC_Orbitrap
                or CVID.MS_Orbitrap_Exploris_120
                or CVID.MS_Orbitrap_Exploris_240
                or CVID.MS_Orbitrap_Exploris_GC_240
                or CVID.MS_Orbitrap_Exploris_GC_MS
                or CVID.MS_Orbitrap_Exploris_480
                or CVID.MS_Orbitrap_Excedion_Pro => OrbitrapQuadHybridRecipe,

            // Exactive (no quad pre-filter): orbitrap + inductive.
            CVID.MS_Exactive or CVID.MS_Exactive_Plus => ExactiveRecipe,

            // Orbitrap-Astral / Astral Zoom: two ICs (orbitrap + Astral TOF).
            CVID.MS_Orbitrap_Astral or CVID.MS_Orbitrap_Astral_Zoom => AstralRecipe,

            // LTQ-FT family: FT-ICR + linear trap (two ICs).
            CVID.MS_LTQ_FT or CVID.MS_LTQ_FT_Ultra => LtqFtRecipe,

            // Orbitrap Fusion / Lumos / ETD / Ascend / IDX / IQX / Eclipse: quad+orbitrap +
            // quad+linear-trap (two ICs).
            CVID.MS_Orbitrap_Fusion
                or CVID.MS_Orbitrap_Fusion_Lumos
                or CVID.MS_Orbitrap_Fusion_ETD
                or CVID.MS_Orbitrap_Ascend
                or CVID.MS_Orbitrap_ID_X
                or CVID.MS_Orbitrap_IQ_X
                or CVID.MS_Orbitrap_Eclipse => FusionRecipe,

            // LTQ-Orbitrap family: orbitrap + linear-trap (two ICs, no quad pre-filter).
            // Also includes the MALDI LTQ Orbitrap variants — cpp's switch puts these in the
            // same case as the regular LTQ Orbitrap models (Reader_Thermo_Detail.cpp:334-355).
            CVID.MS_LTQ_Orbitrap
                or CVID.MS_LTQ_Orbitrap_Classic
                or CVID.MS_LTQ_Orbitrap_Discovery
                or CVID.MS_LTQ_Orbitrap_XL
                or CVID.MS_LTQ_Orbitrap_XL_ETD
                or CVID.MS_LTQ_Orbitrap_Velos
                or CVID.MS_LTQ_Orbitrap_Velos_Pro
                or CVID.MS_LTQ_Orbitrap_Velos_ETD
                or CVID.MS_LTQ_Orbitrap_Elite
                or CVID.MS_MALDI_LTQ_Orbitrap
                or CVID.MS_MALDI_LTQ_Orbitrap_XL
                or CVID.MS_MALDI_LTQ_Orbitrap_Discovery => LtqOrbitrapRecipe,

            // 3D ion traps (LCQ/PolarisQ/ITQ): single IC, quadrupole ion trap analyzer.
            // cpp's switch: Reader_Thermo_Detail.cpp:357-369.
            CVID.MS_LCQ_Advantage
                or CVID.MS_LCQ_Classic
                or CVID.MS_LCQ_Deca
                or CVID.MS_LCQ_Deca_XP_Plus
                or CVID.MS_LCQ_Fleet
                or CVID.MS_PolarisQ
                or CVID.MS_ITQ_700
                or CVID.MS_ITQ_900
                or CVID.MS_ITQ => IonTrapRecipe,

            // Linear ion traps (LTQ, LXQ, Velos, Stellar, ...): single IC. cpp's switch at
            // Reader_Thermo_Detail.cpp:372-387 — ITQ 1100 surprisingly groups here in cpp
            // (linear trap, not 3D), as does MALDI LTQ XL.
            CVID.MS_LTQ
                or CVID.MS_LXQ
                or CVID.MS_LTQ_XL
                or CVID.MS_LTQ_XL_ETD
                or CVID.MS_LTQ_Velos
                or CVID.MS_LTQ_Velos_ETD
                or CVID.MS_Velos_Pro
                or CVID.MS_Velos_Plus
                or CVID.MS_ITQ_1100
                or CVID.MS_MALDI_LTQ_XL
                or CVID.MS_Stellar => LinearIonTrapRecipe,

            // TSQ triple-quad family: 3 quadrupoles + EM. cpp's switch covers a long list
            // including GC_Quantum (line 405-431 in cpp).
            CVID.MS_TSQ
                or CVID.MS_TSQ_7000
                or CVID.MS_TSQ_8000
                or CVID.MS_TSQ_8000_Evo
                or CVID.MS_TSQ_9000
                or CVID.MS_TSQ_Quantum
                or CVID.MS_TSQ_Quantum_Access
                or CVID.MS_TSQ_Quantum_Access_MAX
                or CVID.MS_TSQ_Quantum_Ultra
                or CVID.MS_TSQ_Quantum_Ultra_AM
                or CVID.MS_TSQ_Quantum_XLS
                or CVID.MS_TSQ_Vantage
                or CVID.MS_TSQ_Quantiva
                or CVID.MS_TSQ_Endura
                or CVID.MS_TSQ_Altis
                or CVID.MS_TSQ_Altis_Plus
                or CVID.MS_TSQ_Quantis
                or CVID.MS_TSQ_Certis
                or CVID.MS_GC_Quantum => TripleQuadRecipe,

            // Single quad: source + quad + EM. cpp's switch at Reader_Thermo_Detail.cpp:389-403.
            CVID.MS_SSQ_7000
                or CVID.MS_Surveyor_MSQ
                or CVID.MS_DSQ
                or CVID.MS_DSQ_II
                or CVID.MS_ISQ
                or CVID.MS_ISQ_7000
                or CVID.MS_ISQ_LT
                or CVID.MS_GC_IsoLink
                or CVID.MS_TRACE_DSQ
                or CVID.MS_ThermoQuest_Voyager => SingleQuadRecipe,

            // Sector instruments: source + magnetic sector + EM. cpp's switch at
            // Reader_Thermo_Detail.cpp:434-450. Covers MAT, Element, DELTA Plus, DFS.
            CVID.MS_MAT253
                or CVID.MS_MAT900XP
                or CVID.MS_MAT900XP_Trap
                or CVID.MS_MAT95XP
                or CVID.MS_MAT95XP_Trap
                or CVID.MS_Element_2
                or CVID.MS_Element_XR
                or CVID.MS_Element_GD
                or CVID.MS_DELTA_plusAdvantage
                or CVID.MS_DELTAplusXP
                or CVID.MS_DeltaPlus_IRMS
                or CVID.MS_DFS => SectorRecipe,

            _ => System.Array.Empty<(IReadOnlyList<CVID>, ThermoFisher.CommonCore.Data.FilterEnums.MassAnalyzerType)>(),
        };

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
