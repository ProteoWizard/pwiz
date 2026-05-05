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

        // fileDescription/fileContent: per-spectrum-type CV terms based on actual scan content,
        // plus a representation tag (centroid / profile / mixed) from the storage mode, plus
        // chromatogram CV terms for TIC / SIM / SRM. Mirrors cpp Reader_Agilent::fillInMetadata.
        // cpp Reader_Agilent.cpp:124-129 + 154-157 reads scan types from the file's bitmask
        // and emits one fileContent CVID per type seen. Match exactly so e.g. NL files don't
        // misclassify as plain MSn, MRM/SIM files declare their chromatogram types, etc.
        var scanTypes = raw.ScanTypes;
        bool anySpectrumScanType = false;
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.Scan))
        { result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum); anySpectrumScanType = true; }
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.ProductIon))
        { result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum); anySpectrumScanType = true; }
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.PrecursorIon))
        { result.FileDescription.FileContent.Set(CVID.MS_precursor_ion_spectrum); anySpectrumScanType = true; }
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.NeutralLoss))
        { result.FileDescription.FileContent.Set(CVID.MS_constant_neutral_loss_spectrum); anySpectrumScanType = true; }
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.NeutralGain))
        { result.FileDescription.FileContent.Set(CVID.MS_constant_neutral_gain_spectrum); anySpectrumScanType = true; }

        // Spectrum representation: storage mode tells us whether the file has profile,
        // centroid, or both. cpp Reader_Agilent.cpp:132 guards this on fileContent being
        // non-empty — the storage tag only describes the spectrum content, so for chromatogram-
        // only files (MRM/SIM that declare no MS scan types) it must be skipped, otherwise
        // we end up with fileContent listing centroid_spectrum + SRM_chromatogram and no
        // matching MS scan-type cvParam.
        if (anySpectrumScanType)
        {
            try
            {
                var storage = raw.MSScanFileInformation.SpectraFormat;
                if (storage == global::Agilent.MassSpectrometry.DataAnalysis.MSStorageMode.Mixed)
                {
                    result.FileDescription.FileContent.Set(CVID.MS_centroid_spectrum);
                    result.FileDescription.FileContent.Set(CVID.MS_profile_spectrum);
                }
                else if (storage == global::Agilent.MassSpectrometry.DataAnalysis.MSStorageMode.ProfileSpectrum)
                    result.FileDescription.FileContent.Set(CVID.MS_profile_spectrum);
                else if (storage == global::Agilent.MassSpectrometry.DataAnalysis.MSStorageMode.PeakDetectedSpectrum)
                    result.FileDescription.FileContent.Set(CVID.MS_centroid_spectrum);
            }
            catch { /* SDK may not expose SpectraFormat for some file types */ }
        }

        // cpp always tags TIC chromatogram in fileContent regardless of whether one is emitted.
        result.FileDescription.FileContent.Set(CVID.MS_TIC_chromatogram);
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.SelectedIon))
            result.FileDescription.FileContent.Set(CVID.MS_SIM_chromatogram);
        if (scanTypes.HasFlag(global::Agilent.MassSpectrometry.DataAnalysis.MSScanType.MultipleReaction))
            result.FileDescription.FileContent.Set(CVID.MS_SRM_chromatogram);

        // sourceFileList: one entry per file under AcqData/, mirroring cpp. The .bin file
        // (typically MSScan.bin or MSPeak.bin) is selected as the run's defaultSourceFile.
        // Skips the post-processing artifact extensions cpp also skips.
        string acqDataPath = Path.Combine(Path.GetFullPath(dotDPath), "AcqData");
        SourceFile? defaultSf = null;
        if (Directory.Exists(acqDataPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(acqDataPath))
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is ".mzxml" or ".mzdata" or ".mgf" or ".ms2" or ".txt")
                    continue;

                string filename = Path.GetFileName(filePath);
                // cpp Reader_Agilent.cpp:176 uses BFS_GENERIC_STRING (forward slashes) where
                // Shimadzu / Sciex / Bruker / Thermo / Waters use native (backslashes). The
                // reference mzMLs were written with forward slashes for this reader, so keep
                // the slash translation here. The choice is per-reader in cpp; we mirror it.
                var srcFile = new SourceFile(filename, filename,
                    "file:///" + Path.GetFullPath(acqDataPath).Replace('\\', '/'));
                srcFile.Set(CVID.MS_Agilent_MassHunter_nativeID_format);
                srcFile.Set(CVID.MS_Agilent_MassHunter_format);
                result.FileDescription.SourceFiles.Add(srcFile);
                if (ext == ".bin") defaultSf = srcFile;
            }
        }
        if (defaultSf is not null) result.Run.DefaultSourceFile = defaultSf;

        // Software entries: MassHunter (acquisition) + pwiz (conversion). Version comes from the
        // SDK; cpp uses the SchemaDefaultDirectory string but rawfile->Version is more useful.
        var massHunter = new Software("MassHunter") { Version = raw.Version };
        massHunter.Set(CVID.MS_MassHunter_Data_Acquisition);
        result.Software.Add(massHunter);
        var pwizSoftware = new Software("pwiz") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        // Single DataProcessing entry — cpp emits exactly one (pwiz_Reader_Agilent_conversion).
        var dpReader = new DataProcessing("pwiz_Reader_Agilent_conversion");
        var pmReader = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmReader.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmReader);
        result.DataProcessings.Add(dpReader);

        // CommonInstrumentParams ParamGroup — cpp Reader_Agilent.cpp:82-100. Registered at
        // the document level so it serializes as <referenceableParamGroupList> AND referenced
        // by every InstrumentConfiguration via paramGroupRef. cpp's content:
        //   - cvParam: Agilent instrument model (or specific model CVID)
        //   - userParam: "instrument model" = device-name string (only when cvid is the generic
        //     MS_Agilent_instrument_model)
        //   - cvParam: instrument serial number = SerialNumber from AcqData/Devices.xml
        var commonInstrumentParams = new ParamGroup("CommonInstrumentParams");
        commonInstrumentParams.Set(CVID.MS_Agilent_instrument_model);
        var deviceName = raw.GetDeviceName(raw.DeviceType);
        if (!string.IsNullOrEmpty(deviceName))
            commonInstrumentParams.UserParams.Add(new UserParam("instrument model", deviceName));
        var serialNumber = raw.GetDeviceSerialNumber(raw.DeviceType);
        if (!string.IsNullOrEmpty(serialNumber))
            commonInstrumentParams.Set(CVID.MS_instrument_serial_number, serialNumber);
        result.ParamGroups.Add(commonInstrumentParams);

        // Instrument config: cpp emits one per ionization mode; we emit one combined config
        // matching the device type. Multi-source files become a follow-up port. softwareRef
        // points at MassHunter so the mzML emits instrumentConfiguration/@softwareRef="MassHunter".
        var ic = BuildInstrumentConfiguration(raw, commonInstrumentParams);
        ic.Software = massHunter;
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // Acquisition timestamp from the file info. cpp emits the local-clock components
        // unmodified with a Z suffix (adjustToHostTime=false in the test config), so we do
        // the same — AgilentRawData.AcquisitionTime selects MIDAC's FileInfo.AcquisitionDate
        // for IM files vs MassSpec SDK FileInformation.AcquisitionTime for non-IM, mirroring
        // cpp's MidacDataImpl::getAcquisitionTime / MassHunterDataImpl::getAcquisitionTime split.
        try
        {
            var t = raw.AcquisitionTime;
            result.Run.StartTimeStamp =
                $"{t.Year:D4}-{t.Month:D2}-{t.Day:D2}T{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}Z";
        }
        catch { /* not all files expose a parseable timestamp */ }

        // Spectrum list owns the raw handle; chromatogram list shares without owning so a
        // single Dispose chain releases the SDK file. cpp uses the same shared_ptr split.
        bool simAsSpectra = config?.SimAsSpectra ?? false;
        bool srmAsSpectra = config?.SrmAsSpectra ?? false;
        bool globalChromsAreMs1Only = config?.GlobalChromatogramsAreMs1Only ?? false;
        bool combineIms = config?.CombineIonMobilitySpectra ?? false;
        result.Run.SpectrumList = new SpectrumList_Agilent(raw, ownsRaw: true, ic, simAsSpectra, srmAsSpectra, combineIms)
        {
            Dp = dpReader,
        };
        result.Run.ChromatogramList = new ChromatogramList_Agilent(raw, ownsRaw: false, globalChromsAreMs1Only)
        {
            Dp = dpReader,
        };
    }

    private static InstrumentConfiguration BuildInstrumentConfiguration(AgilentRawData raw, ParamGroup commonInstrumentParams)
    {
        var ic = new InstrumentConfiguration("IC1");
        ic.ParamGroups.Add(commonInstrumentParams);

        // Ion source: pick the first ionization mode that's set in the bitmask (cpp emits one
        // IC per mode; we collapse to the first for now). If the SDK reports a mode none of the
        // cpp translators recognize (e.g. Agilent's MMI / "Multi Mode Ionization"), cpp's
        // Reader_Agilent_Detail.cpp:55-57 loop over the recognized-mode set is empty, so cpp
        // returns no configuration; Reader_Agilent.cpp:104 then resize(1)s the vector to a
        // single empty IC (paramGroupRef + softwareRef only). Mirror that — an unknown source
        // also means we don't have a defensible analyzer/detector chain to emit either.
        //
        // IM files take the same empty-IC path: cpp's MidacDataImpl::getIonModes reads from
        // imsReader->FileInfo->TfsMsDetails->IonizationMode (a different field than the
        // MassSpec SDK's MSScanFileInformation.IonModes that sharp reads), and the value
        // typically isn't one of the recognized bits — so cpp's ionModeSet ends up empty.
        // The MassSpec SDK doesn't have visibility into the IM-specific TFS details, so we
        // can't make the bitmask match cpp; force-empty IC for IM files instead.
        if (raw.HasIonMobilityData)
            return ic;
        var ionMode = raw.MSScanFileInformation.IonModes;
        var sourceCv = TranslateIonization(ionMode);
        if (sourceCv == CVID.CVID_Unknown)
            return ic;

        var inletCv = TranslateInlet(ionMode);
        var src = new Component(sourceCv, 1);
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
