// Port of pwiz/data/msdata/examples.cpp + examples.hpp. Synthesizes a small example
// MSData document used by round-trip / Diff / writer parity tests.
//
// Verbatim port: every field this method sets matches the cpp version's CVID + value,
// in the same order, so reference mzML / mzXML / MGF baselines diff byte-for-byte
// against pwiz cpp's output. Don't reorder or rename without updating the reference
// fixtures alongside.

using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData;

/// <summary>
/// Builds canonical example <see cref="MSData"/> instances. Used by round-trip /
/// Diff / serializer parity tests. Mirrors <c>pwiz/data/msdata/examples.cpp</c>.
/// </summary>
public static class Examples
{
    /// <summary>Populates <paramref name="msd"/> with the same minimal example data
    /// that cpp's <c>pwiz::msdata::examples::initializeTiny</c> produces — 5 spectra
    /// (MS1 / MS2 / no-data / ETD MS2 / MALDI MS1) plus 2 chromatograms (TIC + SIC),
    /// with a representative slice of metadata (file description, source files,
    /// contact, paramGroups, sample, instrument config, software, dataProcessing,
    /// scanSettings).</summary>
    public static void InitializeTiny(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);

        msd.Id = "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz";

        // cvList
        msd.CVs.Clear();
        foreach (var cv in MSData.DefaultCVList) msd.CVs.Add(cv);

        // fileDescription
        var fc = msd.FileDescription.FileContent;
        fc.Set(CVID.MS_MSn_spectrum);
        fc.Set(CVID.MS_centroid_spectrum);

        var sfp = new SourceFile { Id = "tiny1.yep", Name = "tiny1.yep", Location = "file://F:/data/Exp01" };
        sfp.Set(CVID.MS_Bruker_Agilent_YEP_format);
        sfp.Set(CVID.MS_SHA_1, "1234567890123456789012345678901234567890");
        sfp.Set(CVID.MS_Bruker_Agilent_YEP_nativeID_format);
        msd.FileDescription.SourceFiles.Add(sfp);

        var sfp2 = new SourceFile { Id = "tiny.wiff", Name = "tiny.wiff", Location = "file://F:/data/Exp01" };
        sfp2.Set(CVID.MS_ABI_WIFF_format);
        sfp2.Set(CVID.MS_SHA_1, "2345678901234567890123456789012345678901");
        sfp2.Set(CVID.MS_WIFF_nativeID_format);
        msd.FileDescription.SourceFiles.Add(sfp2);

        var sfpParameters = new SourceFile("sf_parameters", "parameters.par", "file://C:/settings/");
        sfpParameters.Set(CVID.MS_parameter_file);
        sfpParameters.Set(CVID.MS_SHA_1, "3456789012345678901234567890123456789012");
        sfpParameters.Set(CVID.MS_no_nativeID_format);
        msd.FileDescription.SourceFiles.Add(sfpParameters);

        var contact = new Contact();
        contact.Set(CVID.MS_contact_name, "William Pennington");
        contact.Set(CVID.MS_contact_affiliation, "Higglesworth University");
        contact.Set(CVID.MS_contact_address, "12 Higglesworth Avenue, 12045, HI, USA");
        contact.Set(CVID.MS_contact_URL, "http://www.higglesworth.edu/");
        contact.Set(CVID.MS_contact_email, "wpennington@higglesworth.edu");
        msd.FileDescription.Contacts.Add(contact);

        // paramGroupList
        var pg1 = new ParamGroup("CommonMS1SpectrumParams");
        pg1.Set(CVID.MS_MS1_spectrum);
        pg1.Set(CVID.MS_positive_scan);
        msd.ParamGroups.Add(pg1);

        var pg2 = new ParamGroup("CommonMS2SpectrumParams");
        pg2.Set(CVID.MS_MSn_spectrum);
        pg2.Set(CVID.MS_negative_scan);
        msd.ParamGroups.Add(pg2);

        // sampleList
        var sample = new Sample("20090101 - Sample 1", "Sample 1");
        msd.Samples.Add(sample);

        // instrumentConfigurationList
        var ic = new InstrumentConfiguration("LCQ Deca");
        ic.Set(CVID.MS_LCQ_Deca);
        ic.Set(CVID.MS_instrument_serial_number, "23433");
        ic.ComponentList.Add(new Component(CVID.MS_nanoelectrospray, 1));
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole_ion_trap, 2));
        ic.ComponentList.Add(new Component(CVID.MS_electron_multiplier, 3));

        var softwareCompassXtract = new Software { Id = "CompassXtract", Version = "2.0.5" };
        softwareCompassXtract.Set(CVID.MS_CompassXtract);
        ic.Software = softwareCompassXtract;

        msd.InstrumentConfigurations.Add(ic);

        // softwareList
        var softwareBioworks = new Software { Id = "Bioworks", Version = "3.3.1 sp1" };
        softwareBioworks.Set(CVID.MS_Bioworks);

        var softwarePwiz = new Software { Id = "pwiz", Version = "1.0" };
        softwarePwiz.Set(CVID.MS_pwiz);

        msd.Software.Add(softwareBioworks);
        msd.Software.Add(softwarePwiz);
        msd.Software.Add(softwareCompassXtract);

        // dataProcessingList
        var dpCompassXtract = new DataProcessing("CompassXtract processing");
        var procCXT = new ProcessingMethod { Order = 1, Software = softwareCompassXtract };
        procCXT.Set(CVID.MS_deisotoping);
        procCXT.Set(CVID.MS_charge_deconvolution);
        procCXT.Set(CVID.MS_peak_picking);
        dpCompassXtract.ProcessingMethods.Add(procCXT);

        var dpPwiz = new DataProcessing("pwiz_processing");
        var procPwiz = new ProcessingMethod { Order = 2, Software = softwarePwiz };
        procPwiz.Set(CVID.MS_Conversion_to_mzML);
        dpPwiz.ProcessingMethods.Add(procPwiz);

        msd.DataProcessings.Add(dpCompassXtract);
        // cpp leaves dpPwiz off msd.dataProcessingPtrs but assigns it to the spectrum list
        // below; mirror that.

        // scanSettingsList
        var as1 = new ScanSettings("tiny scan settings");
        as1.SourceFiles.Add(sfpParameters);
        var t1 = new Target();
        t1.Set(CVID.MS_selected_ion_m_z, 1000, CVID.MS_m_z);
        var t2 = new Target();
        t2.Set(CVID.MS_selected_ion_m_z, 1200, CVID.MS_m_z);
        as1.Targets.Add(t1);
        as1.Targets.Add(t2);
        msd.ScanSettings.Add(as1);

        // run
        msd.Run.Id = "Experiment 1";
        msd.Run.DefaultInstrumentConfiguration = ic;
        msd.Run.Sample = sample;
        msd.Run.StartTimeStamp = "2007-06-27T15:23:45.00035";
        msd.Run.DefaultSourceFile = sfp;

        var spectrumList = new SpectrumListSimple { Dp = dpPwiz };
        msd.Run.SpectrumList = spectrumList;

        // -- spectrum 19 — MS1 centroid, 15 peaks
        var s19 = new Spectrum { Id = "scan=19", Index = 0 };
        s19.Params.Set(CVID.MS_ms_level, 1);
        s19.Params.Set(CVID.MS_centroid_spectrum);
        s19.Params.Set(CVID.MS_lowest_observed_m_z, 400.39, CVID.MS_m_z);
        s19.Params.Set(CVID.MS_highest_observed_m_z, 1795.56, CVID.MS_m_z);
        s19.Params.Set(CVID.MS_base_peak_m_z, 445.347, CVID.MS_m_z);
        s19.Params.Set(CVID.MS_base_peak_intensity, 120053, CVID.MS_number_of_detector_counts);
        s19.Params.Set(CVID.MS_total_ion_current, 1.66755e+007);
        s19.Params.ParamGroups.Add(pg1);

        var s19Scan = new Scan { InstrumentConfiguration = ic };
        s19Scan.Set(CVID.MS_scan_start_time, 5.890500, CVID.UO_minute);
        s19Scan.Set(CVID.MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]");
        s19Scan.Set(CVID.MS_preset_scan_configuration, 3);
        var s19Window = new ScanWindow();
        s19Window.Set(CVID.MS_scan_window_lower_limit, 400.000000, CVID.MS_m_z);
        s19Window.Set(CVID.MS_scan_window_upper_limit, 1800.000000, CVID.MS_m_z);
        s19Scan.ScanWindows.Add(s19Window);
        s19.ScanList.Scans.Add(s19Scan);
        s19.ScanList.Set(CVID.MS_no_combination);

        var s19Mz = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s19Mz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
        for (int i = 0; i < 15; i++) s19Mz.Data.Add(i);

        var s19Intensity = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s19Intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int i = 0; i < 15; i++) s19Intensity.Data.Add(15 - i);

        s19.BinaryDataArrays.Add(s19Mz);
        s19.BinaryDataArrays.Add(s19Intensity);
        s19.DefaultArrayLength = s19Mz.Data.Count;
        spectrumList.Spectra.Add(s19);

        // -- spectrum 20 — MS2 profile, 10 peaks, CID activation
        var s20 = new Spectrum { Id = "scan=20", Index = 1 };
        s20.Params.ParamGroups.Add(pg2);
        s20.Params.Set(CVID.MS_ms_level, 2);
        s20.Params.Set(CVID.MS_profile_spectrum);
        s20.Params.Set(CVID.MS_lowest_observed_m_z, 320.39, CVID.MS_m_z);
        s20.Params.Set(CVID.MS_highest_observed_m_z, 1003.56, CVID.MS_m_z);
        s20.Params.Set(CVID.MS_base_peak_m_z, 456.347, CVID.MS_m_z);
        s20.Params.Set(CVID.MS_base_peak_intensity, 23433, CVID.MS_number_of_detector_counts);
        s20.Params.Set(CVID.MS_total_ion_current, 1.66755e+007);

        var precursor = new Precursor { SpectrumId = s19.Id };
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 445.3, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, 0.5, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, 0.5, CVID.MS_m_z);
        var selectedIon = new SelectedIon();
        selectedIon.Set(CVID.MS_selected_ion_m_z, 445.34, CVID.MS_m_z);
        selectedIon.Set(CVID.MS_peak_intensity, 120053, CVID.MS_number_of_detector_counts);
        selectedIon.Set(CVID.MS_charge_state, 2);
        precursor.SelectedIons.Add(selectedIon);
        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        precursor.Activation.Set(CVID.MS_collision_energy, 35.00, CVID.UO_electronvolt);
        s20.Precursors.Add(precursor);

        var s20Scan = new Scan { InstrumentConfiguration = ic };
        s20Scan.Set(CVID.MS_scan_start_time, 5.990500, CVID.UO_minute);
        s20Scan.Set(CVID.MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]");
        s20Scan.Set(CVID.MS_preset_scan_configuration, 4);
        var s20Window = new ScanWindow();
        s20Window.Set(CVID.MS_scan_window_lower_limit, 110.000000, CVID.MS_m_z);
        s20Window.Set(CVID.MS_scan_window_upper_limit, 905.000000, CVID.MS_m_z);
        s20Scan.ScanWindows.Add(s20Window);
        s20.ScanList.Scans.Add(s20Scan);
        s20.ScanList.Set(CVID.MS_no_combination);

        var s20Mz = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s20Mz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
        for (int i = 0; i < 10; i++) s20Mz.Data.Add(i * 2);

        var s20Intensity = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s20Intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int i = 0; i < 10; i++) s20Intensity.Data.Add((10 - i) * 2);

        s20.BinaryDataArrays.Add(s20Mz);
        s20.BinaryDataArrays.Add(s20Intensity);
        s20.DefaultArrayLength = s20Mz.Data.Count;
        spectrumList.Spectra.Add(s20);

        // -- spectrum 21 — MS1 centroid with zero peaks (exercises empty-spectrum path)
        var s21 = new Spectrum { Id = "scan=21", Index = 2 };
        s21.Params.ParamGroups.Add(pg1);
        s21.Params.Set(CVID.MS_ms_level, 1);
        s21.Params.Set(CVID.MS_centroid_spectrum);
        s21.Params.UserParams.Add(new UserParam("example", "spectrum with no data"));
        s21.SetMZIntensityArrays(Array.Empty<double>(), Array.Empty<double>(), CVID.MS_number_of_detector_counts);

        var s21Scan = new Scan { InstrumentConfiguration = ic };
        s21.ScanList.Scans.Add(s21Scan);
        s21.ScanList.Set(CVID.MS_no_combination);
        spectrumList.Spectra.Add(s21);

        // -- spectrum 22 — MS2 profile with ETD+CID activation
        var s22 = new Spectrum { Id = "scan=22", Index = 3 };
        s22.Params.ParamGroups.Add(pg2);
        s22.Params.Set(CVID.MS_ms_level, 2);
        s22.Params.Set(CVID.MS_profile_spectrum);
        s22.Params.Set(CVID.MS_lowest_observed_m_z, 320.39, CVID.MS_m_z);
        s22.Params.Set(CVID.MS_highest_observed_m_z, 1003.56, CVID.MS_m_z);
        s22.Params.Set(CVID.MS_base_peak_m_z, 456.347, CVID.MS_m_z);
        s22.Params.Set(CVID.MS_base_peak_intensity, 23433, CVID.MS_number_of_detector_counts);
        s22.Params.Set(CVID.MS_total_ion_current, 1.66755e+007);

        var precursor22 = new Precursor { SpectrumId = s19.Id };
        precursor22.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 545.3, CVID.MS_m_z);
        precursor22.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, 0.5, CVID.MS_m_z);
        precursor22.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, 0.5, CVID.MS_m_z);
        var selectedIon22 = new SelectedIon();
        selectedIon22.Set(CVID.MS_selected_ion_m_z, 545.34, CVID.MS_m_z);
        selectedIon22.Set(CVID.MS_peak_intensity, 120053, CVID.MS_number_of_detector_counts);
        selectedIon22.Set(CVID.MS_charge_state, 2);
        precursor22.SelectedIons.Add(selectedIon22);
        precursor22.Activation.Set(CVID.MS_ETD);
        precursor22.Activation.Set(CVID.MS_CID);
        precursor22.Activation.Set(CVID.MS_collision_energy, 60.00, CVID.UO_electronvolt);
        s22.Precursors.Add(precursor22);

        var s22Scan = new Scan { InstrumentConfiguration = ic };
        s22Scan.Set(CVID.MS_scan_start_time, 6.5, CVID.UO_minute);
        s22Scan.Set(CVID.MS_filter_string, "+ c d Full ms2  445.35@etd60.00 [ 110.00-905.00]");
        s22Scan.Set(CVID.MS_preset_scan_configuration, 4);
        var s22Window = new ScanWindow();
        s22Window.Set(CVID.MS_scan_window_lower_limit, 110.000000, CVID.MS_m_z);
        s22Window.Set(CVID.MS_scan_window_upper_limit, 905.000000, CVID.MS_m_z);
        s22Scan.ScanWindows.Add(s22Window);
        s22.ScanList.Scans.Add(s22Scan);
        s22.ScanList.Set(CVID.MS_no_combination);

        var s22Mz = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s22Mz.Set(CVID.MS_m_z_array, string.Empty, CVID.MS_m_z);
        for (int i = 0; i < 10; i++) s22Mz.Data.Add(i * 2);

        var s22Intensity = new BinaryDataArray { DataProcessing = dpCompassXtract };
        s22Intensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int i = 0; i < 10; i++) s22Intensity.Data.Add((10 - i) * 2);

        s22.BinaryDataArrays.Add(s22Mz);
        s22.BinaryDataArrays.Add(s22Intensity);
        s22.DefaultArrayLength = s22Mz.Data.Count;
        spectrumList.Spectra.Add(s22);

        // -- spectrum 23 — MS1 centroid with MALDI spot info + alternate sourceFile
        var s23 = new Spectrum
        {
            Id = "sample=1 period=1 cycle=23 experiment=1",
            Index = 4,
            SpotId = "A1,42x42,4242x4242",
            SourceFile = sfp2,
        };
        s23.Params.Set(CVID.MS_ms_level, 1);
        s23.Params.Set(CVID.MS_centroid_spectrum);
        s23.Params.Set(CVID.MS_lowest_observed_m_z, 142.39, CVID.MS_m_z);
        s23.Params.Set(CVID.MS_highest_observed_m_z, 942.56, CVID.MS_m_z);
        s23.Params.Set(CVID.MS_base_peak_m_z, 422.42, CVID.MS_m_z);
        s23.Params.Set(CVID.MS_base_peak_intensity, 42, CVID.MS_number_of_detector_counts);
        s23.Params.Set(CVID.MS_total_ion_current, 4200);
        s23.Params.UserParams.Add(new UserParam("alternate source file", "to test a different nativeID format"));
        s23.Params.ParamGroups.Add(pg1);

        var s23Scan = new Scan { InstrumentConfiguration = ic };
        s23Scan.Set(CVID.MS_scan_start_time, 42.0500, CVID.UO_second);
        s23Scan.Set(CVID.MS_filter_string, "+ c MALDI Full ms [100.00-1000.00]");
        var s23Window = new ScanWindow();
        s23Window.Set(CVID.MS_scan_window_lower_limit, 100.000000, CVID.MS_m_z);
        s23Window.Set(CVID.MS_scan_window_upper_limit, 1000.000000, CVID.MS_m_z);
        s23Scan.ScanWindows.Add(s23Window);
        s23.ScanList.Scans.Add(s23Scan);
        s23.ScanList.Set(CVID.MS_no_combination);

        // s23 reuses s19's binary arrays (intentional, mirrors cpp behavior).
        s23.BinaryDataArrays.Add(s19Mz);
        s23.BinaryDataArrays.Add(s19Intensity);
        s23.DefaultArrayLength = s19Mz.Data.Count;
        spectrumList.Spectra.Add(s23);

        // chromatograms
        var chromatogramList = new ChromatogramListSimple { Dp = dpPwiz };
        msd.Run.ChromatogramList = chromatogramList;

        var tic = new Chromatogram
        {
            Id = "tic",
            Index = 0,
            DefaultArrayLength = 15,
            DataProcessing = dpCompassXtract,
        };
        tic.Params.Set(CVID.MS_total_ion_current_chromatogram);

        var ticTime = new BinaryDataArray { DataProcessing = dpPwiz };
        ticTime.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
        for (int i = 0; i < 15; i++) ticTime.Data.Add(i);

        var ticIntensity = new BinaryDataArray { DataProcessing = dpPwiz };
        ticIntensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int i = 0; i < 15; i++) ticIntensity.Data.Add(15 - i);

        tic.BinaryDataArrays.Add(ticTime);
        tic.BinaryDataArrays.Add(ticIntensity);
        chromatogramList.Chromatograms.Add(tic);

        var sic = new Chromatogram
        {
            Id = "sic",
            Index = 1,
            DefaultArrayLength = 10,
            DataProcessing = dpPwiz,
        };
        sic.Params.Set(CVID.MS_selected_ion_current_chromatogram);
        sic.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 456.7, CVID.MS_m_z);
        sic.Precursor.Activation.Set(CVID.MS_CID);
        sic.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 678.9, CVID.MS_m_z);

        var sicTime = new BinaryDataArray { DataProcessing = dpPwiz };
        sicTime.Set(CVID.MS_time_array, string.Empty, CVID.UO_second);
        for (int i = 0; i < 10; i++) sicTime.Data.Add(i);

        var sicIntensity = new BinaryDataArray { DataProcessing = dpPwiz };
        sicIntensity.Set(CVID.MS_intensity_array, string.Empty, CVID.MS_number_of_detector_counts);
        for (int i = 0; i < 10; i++) sicIntensity.Data.Add(10 - i);

        sic.BinaryDataArrays.Add(sicTime);
        sic.BinaryDataArrays.Add(sicIntensity);
        chromatogramList.Chromatograms.Add(sic);
    }

    /// <summary>Adds MIAPE-compliant supplementary metadata (instrument source potentials,
    /// extra paramGroups, sample, dataProcessing, scanSettings) on top of an existing
    /// <paramref name="msd"/>. Mirrors cpp <c>addMIAPEExampleMetadata</c>. Typically called
    /// after <see cref="InitializeTiny"/> or after reading a vendor file.</summary>
    public static void AddMiapeExampleMetadata(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);

        msd.Id = "urn:lsid:psidev.info:mzML.instanceDocuments.small_miape.pwiz";

        msd.CVs.Clear();
        foreach (var cv in MSData.DefaultCVList) msd.CVs.Add(cv);

        var fc = msd.FileDescription.FileContent;
        fc.UserParams.Add(new UserParam("ProteoWizard",
            "Thermo RAW data converted to mzML, with additional MIAPE parameters added for illustration"));

        // fileDescription
        var sfpParameters = new SourceFile("sf_parameters", "parameters.par", "file:///C:/example/");
        sfpParameters.Set(CVID.MS_parameter_file);
        sfpParameters.Set(CVID.MS_SHA_1, "unknown");
        sfpParameters.Set(CVID.MS_no_nativeID_format);
        msd.FileDescription.SourceFiles.Add(sfpParameters);

        var contact = new Contact();
        contact.Set(CVID.MS_contact_name, "William Pennington");
        contact.Set(CVID.MS_contact_affiliation, "Higglesworth University");
        contact.Set(CVID.MS_contact_address, "12 Higglesworth Avenue, 12045, HI, USA");
        contact.Set(CVID.MS_contact_URL, "http://www.higglesworth.edu/");
        contact.Set(CVID.MS_contact_email, "wpennington@higglesworth.edu");
        msd.FileDescription.Contacts.Add(contact);

        // paramGroupList
        var pgInstrumentCustomization = new ParamGroup("InstrumentCustomization");
        pgInstrumentCustomization.Set(CVID.MS_customization, "none");
        msd.ParamGroups.Add(pgInstrumentCustomization);

        var pgActivation = new ParamGroup("CommonActivationParams");
        pgActivation.Set(CVID.MS_collision_induced_dissociation);
        pgActivation.Set(CVID.MS_collision_energy, 35.00, CVID.UO_electronvolt);
        pgActivation.Set(CVID.MS_collision_gas, "nitrogen");
        msd.ParamGroups.Add(pgActivation);

        // sampleList
        var sample1 = new Sample("sample1", "Sample 1");
        msd.Samples.Add(sample1);
        var sample2 = new Sample("sample2", "Sample 2");
        msd.Samples.Add(sample2);

        // instrumentConfigurationList — add a source potential to every existing source component
        foreach (var ic in msd.InstrumentConfigurations)
        {
            foreach (var c in ic.ComponentList)
            {
                if (c.Type == ComponentType.Source)
                    c.Set(CVID.MS_source_potential, "4.20", CVID.UO_volt);
            }
        }

        // dataProcessingList
        var procMiape = new ProcessingMethod
        {
            Order = 1,
            Software = msd.Software.Count > 0 ? msd.Software[^1] : null,
        };
        procMiape.Set(CVID.MS_deisotoping);
        procMiape.Set(CVID.MS_charge_deconvolution);
        procMiape.Set(CVID.MS_peak_picking);
        procMiape.Set(CVID.MS_smoothing);
        procMiape.Set(CVID.MS_baseline_reduction);
        procMiape.UserParams.Add(new UserParam("signal-to-noise estimation", "none"));
        procMiape.UserParams.Add(new UserParam("centroiding algorithm", "none"));
        procMiape.UserParams.Add(new UserParam("charge states calculated", "none"));

        var dpMiape = new DataProcessing("MIAPE example");
        dpMiape.ProcessingMethods.Add(procMiape);
        msd.DataProcessings.Add(dpMiape);

        // scanSettingsList
        var as1 = new ScanSettings("acquisition settings MIAPE example");
        as1.SourceFiles.Add(sfpParameters);

        var t1 = new Target();
        t1.UserParams.Add(new UserParam("precursorMz", "123.456"));
        t1.UserParams.Add(new UserParam("fragmentMz", "456.789"));
        t1.UserParams.Add(new UserParam("dwell time", "1", "seconds"));
        t1.UserParams.Add(new UserParam("active time", "0.5", "seconds"));

        var t2 = new Target();
        t2.UserParams.Add(new UserParam("precursorMz", "231.673"));
        t2.UserParams.Add(new UserParam("fragmentMz", "566.328"));
        t2.UserParams.Add(new UserParam("dwell time", "1", "seconds"));
        t2.UserParams.Add(new UserParam("active time", "0.5", "seconds"));

        as1.Targets.Add(t1);
        as1.Targets.Add(t2);
        msd.ScanSettings.Add(as1);

        // run
        msd.Run.Sample = sample1;
    }
}
