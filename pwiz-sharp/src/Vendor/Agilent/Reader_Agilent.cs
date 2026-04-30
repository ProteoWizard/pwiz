using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
#if !NO_VENDOR_SUPPORT
using System.Globalization;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
using AgDeviceType = Agilent.MassSpectrometry.DataAnalysis.DeviceType;
using AgIonization = Agilent.MassSpectrometry.DataAnalysis.IonizationMode;
#endif

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// <see cref="IReader"/> for Agilent MassHunter <c>.d</c> directories. Identifies a directory
/// as Agilent by the presence of <c>AcqData/MSScan.bin</c> (or <c>AcqData/MSPeak.bin</c>) and
/// builds an MSData via <c>AgilentRawData</c> + <c>SpectrumList_Agilent</c>.
/// </summary>
/// <remarks>
/// Port of pwiz <c>Reader_Agilent</c>. Initial scope: Q-TOF / TQ / single-quad / TOF MS scans
/// with peaks, basic instrument config, source files, run timestamp. Ion mobility (MIDAC),
/// non-MS UV/DAD spectra, and LC chromatograms are follow-ups.
/// </remarks>
public sealed class Reader_Agilent : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Agilent";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Agilent_MassHunter_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".d" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        return IsAgilentDirectory(filename) ? CvType : CVID.CVID_Unknown;
    }

    /// <summary>
    /// Pure filesystem check used by both <see cref="Identify"/> and <see cref="Read"/>.
    /// Lives here (rather than <c>AgilentRawData</c>) so it stays callable when the SDK isn't
    /// compiled in (NO_VENDOR_SUPPORT mode).
    /// </summary>
    private static bool IsAgilentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
        string acqData = Path.Combine(path, "AcqData");
        if (!Directory.Exists(acqData)) return false;
        return File.Exists(Path.Combine(acqData, "MSScan.bin"))
            || File.Exists(Path.Combine(acqData, "MSPeak.bin"));
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        if (!IsAgilentDirectory(filename))
            throw new InvalidDataException($"Not an Agilent .d directory: {filename}");

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Agilent .d reading requires the vendor SDK. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        result.CVs.AddRange(MSData.DefaultCVList);
        var raw = new AgilentRawData(filename);
        try
        {
            FillMetadata(result, filename, raw, config);
        }
        catch
        {
            raw.Dispose();
            throw;
        }
#endif
    }

#if !NO_VENDOR_SUPPORT
    private static void FillMetadata(MSData result, string dotDPath, AgilentRawData raw, ReaderConfig? config)
    {
        string dirName = Path.GetFileName(dotDPath.TrimEnd('/', '\\'));
        result.Id = Path.GetFileNameWithoutExtension(dirName);
        result.Run.Id = result.Id;

        // sourceFile points at AcqData/MSScan.bin (matches cpp Serializer_mzXML translation
        // table).
        var sf = new SourceFile("MSScan.bin", "MSScan.bin",
            "file:///" + Path.Combine(Path.GetFullPath(dotDPath), "AcqData").Replace('\\', '/'));
        sf.Set(CVID.MS_Agilent_MassHunter_format);
        sf.Set(CVID.MS_Agilent_MassHunter_nativeID_format);
        result.FileDescription.SourceFiles.Add(sf);
        result.Run.DefaultSourceFile = sf;

        // fileContent: if the file has any MS spectra, declare both MS1 and MSn (cheap heuristic).
        // Cpp walks scan records; doing the same keeps parity with reference mzMLs.
        bool hasMs1 = false, hasMsn = false;
        for (int i = 0, end = (int)raw.TotalScansPresent; i < end; i++)
        {
            int level;
            try { level = raw.GetScanRecord(i).MSLevel == global::Agilent.MassSpectrometry.DataAnalysis.MSLevel.MSMS ? 2 : 1; }
            catch { continue; }
            if (level == 1) hasMs1 = true;
            else if (level >= 2) hasMsn = true;
            if (hasMs1 && hasMsn) break;
        }
        if (hasMs1) result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (hasMsn) result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        // Software entries: MassHunter (acquisition) + pwiz (conversion). Version comes from the
        // SDK; cpp uses the SchemaDefaultDirectory string but rawfile->Version is more useful.
        var massHunter = new Software("MassHunter") { Version = raw.Version };
        massHunter.Set(CVID.MS_MassHunter_Data_Acquisition);
        result.Software.Add(massHunter);
        var pwizSoftware = new Software("pwiz") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        // dataProcessing: pwiz conversion.
        var dpReader = new DataProcessing("pwiz_Reader_Agilent_conversion");
        var pmReader = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmReader.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmReader);
        result.DataProcessings.Add(dpReader);

        var dpCommon = new DataProcessing("pwiz_Reader_conversion");
        var pmCommon = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmCommon.Set(CVID.MS_Conversion_to_mzML);
        dpCommon.ProcessingMethods.Add(pmCommon);
        result.DataProcessings.Add(dpCommon);

        // Instrument config: cpp emits one per ionization mode; we emit one combined config
        // matching the device type. Multi-source files become a follow-up port.
        var ic = BuildInstrumentConfiguration(raw);
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // Acquisition timestamp from the file info.
        try { result.Run.StartTimeStamp = raw.AcquisitionTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture); }
        catch { /* not all files expose a parseable timestamp */ }

        // Spectrum list: hand the raw object to the SpectrumList; SpectrumList owns it.
        bool simAsSpectra = config?.SimAsSpectra ?? false;
        bool srmAsSpectra = config?.SrmAsSpectra ?? false;
        var list = new SpectrumList_Agilent(raw, ownsRaw: true, ic, simAsSpectra, srmAsSpectra)
        {
            Dp = dpReader,
        };
        result.Run.SpectrumList = list;
    }

    private static InstrumentConfiguration BuildInstrumentConfiguration(AgilentRawData raw)
    {
        var ic = new InstrumentConfiguration("IC1");
        var common = new ParamGroup("CommonInstrumentParams");
        common.Set(CVID.MS_Agilent_instrument_model);
        ic.ParamGroups.Add(common);

        // Ion source: pick the first ionization mode that's set in the bitmask (cpp emits one
        // IC per mode; we collapse to the first for now).
        var ionMode = raw.MSScanFileInformation.IonModes;
        var sourceCv = TranslateIonization(ionMode);
        var inletCv = TranslateInlet(ionMode);
        var src = new Component(sourceCv == CVID.CVID_Unknown ? CVID.MS_electrospray_ionization : sourceCv, 1);
        if (inletCv != CVID.CVID_Unknown) src.Set(inletCv);
        ic.ComponentList.Add(src);

        // Mass analyzer + detector — Q-TOF default if unknown.
        switch (raw.DeviceType)
        {
            case AgDeviceType.Quadrupole:
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 3));
                break;
            case AgDeviceType.IonTrap:
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole_ion_trap, 2));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 3));
                break;
            case AgDeviceType.TimeOfFlight:
                ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 2));
                ic.ComponentList.Add(new Component(CVID.MS_multichannel_plate, 3));
                ic.ComponentList.Add(new Component(CVID.MS_photomultiplier, 4));
                break;
            case AgDeviceType.TandemQuadrupole:
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 4));
                ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 5));
                break;
            case AgDeviceType.QuadrupoleTimeOfFlight:
            default:
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
                ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
                ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 4));
                ic.ComponentList.Add(new Component(CVID.MS_multichannel_plate, 5));
                ic.ComponentList.Add(new Component(CVID.MS_photomultiplier, 6));
                break;
        }
        return ic;
    }

    /// <summary>Mirrors cpp <c>translateAsIonizationType</c>.</summary>
    private static CVID TranslateIonization(AgIonization mode)
    {
        if (mode.HasFlag(AgIonization.JetStream)) return CVID.MS_nanoelectrospray;
        if (mode.HasFlag(AgIonization.NanoEsi)) return CVID.MS_nanoelectrospray;
        if (mode.HasFlag(AgIonization.Esi)) return CVID.MS_microelectrospray;
        if (mode.HasFlag(AgIonization.Apci)) return CVID.MS_atmospheric_pressure_chemical_ionization;
        if (mode.HasFlag(AgIonization.Appi)) return CVID.MS_atmospheric_pressure_photoionization;
        if (mode.HasFlag(AgIonization.Maldi)) return CVID.MS_matrix_assisted_laser_desorption_ionization;
        if (mode.HasFlag(AgIonization.CI)) return CVID.MS_chemical_ionization;
        if (mode.HasFlag(AgIonization.EI)) return CVID.MS_electron_ionization;
        if (mode.HasFlag(AgIonization.MsChip)) return CVID.MS_nanoelectrospray;
        if (mode.HasFlag(AgIonization.ICP)) return CVID.MS_plasma_desorption_ionization;
        return CVID.CVID_Unknown;
    }

    /// <summary>Mirrors cpp <c>translateAsInletType</c>.</summary>
    private static CVID TranslateInlet(AgIonization mode)
    {
        if (mode.HasFlag(AgIonization.JetStream)) return CVID.MS_nanospray_inlet;
        if (mode.HasFlag(AgIonization.NanoEsi)) return CVID.MS_nanospray_inlet;
        if (mode.HasFlag(AgIonization.Esi)) return CVID.MS_electrospray_inlet;
        if (mode.HasFlag(AgIonization.Apci)) return CVID.MS_direct_inlet;
        if (mode.HasFlag(AgIonization.Appi)) return CVID.MS_direct_inlet;
        if (mode.HasFlag(AgIonization.Maldi)) return CVID.MS_particle_beam;
        if (mode.HasFlag(AgIonization.CI) || mode.HasFlag(AgIonization.EI)) return CVID.MS_direct_inlet;
        if (mode.HasFlag(AgIonization.MsChip)) return CVID.MS_nanospray_inlet;
        if (mode.HasFlag(AgIonization.ICP)) return CVID.MS_inductively_coupled_plasma;
        return CVID.CVID_Unknown;
    }
#endif
}
