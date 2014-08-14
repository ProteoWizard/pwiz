//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#define PWIZ_SOURCE

#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"
namespace pwiz {
namespace msdata {
namespace examples {




PWIZ_API_DECL void initializeTiny(MSData& msd)
{
    msd.id = "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz";

    // cvList

    msd.cvs = defaultCVList();

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);
    fc.set(MS_centroid_spectrum);

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "tiny1.yep";
    sfp->name = "tiny1.yep";
    sfp->location = "file://F:/data/Exp01";
    sfp->set(MS_Bruker_Agilent_YEP_format);
    sfp->set(MS_SHA_1,"1234567890123456789012345678901234567890");
    sfp->set(MS_Bruker_Agilent_YEP_nativeID_format);
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    SourceFilePtr sfp2(new SourceFile);
    sfp2->id = "tiny.wiff";
    sfp2->name = "tiny.wiff";
    sfp2->location = "file://F:/data/Exp01";
    sfp2->set(MS_ABI_WIFF_format);
    sfp2->set(MS_SHA_1,"2345678901234567890123456789012345678901");
    sfp2->set(MS_WIFF_nativeID_format);
    msd.fileDescription.sourceFilePtrs.push_back(sfp2);

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file://C:/settings/"));
    sfp_parameters->set(MS_parameter_file);
    sfp_parameters->set(MS_SHA_1, "3456789012345678901234567890123456789012");
    sfp_parameters->set(MS_no_nativeID_format);
    msd.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    msd.fileDescription.contacts.resize(1);
    Contact& contact = msd.fileDescription.contacts.front();
    contact.set(MS_contact_name, "William Pennington");
	contact.set(MS_contact_affiliation, "Higglesworth University");
    contact.set(MS_contact_address, "12 Higglesworth Avenue, 12045, HI, USA");
	contact.set(MS_contact_URL, "http://www.higglesworth.edu/");
	contact.set(MS_contact_email, "wpennington@higglesworth.edu");

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->set(MS_MS1_spectrum);
    pg1->set(MS_positive_scan);
    msd.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->set(MS_MSn_spectrum);
    pg2->set(MS_negative_scan);
    msd.paramGroupPtrs.push_back(pg2);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "20090101 - Sample 1";
    samplePtr->name = "Sample 1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentConfigurationList

    InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration("LCQ Deca"));
    instrumentConfigurationPtr->set(MS_LCQ_Deca);
    instrumentConfigurationPtr->set(MS_instrument_serial_number,"23433");
    instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_electron_multiplier, 3));

    SoftwarePtr softwareCompassXtract(new Software);
    softwareCompassXtract->id = "CompassXtract";
    softwareCompassXtract->set(MS_CompassXtract);
    softwareCompassXtract->version = "2.0.5";
    instrumentConfigurationPtr->softwarePtr = softwareCompassXtract;

    msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

    // softwareList

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->set(MS_Bioworks);
    softwareBioworks->version = "3.3.1 sp1";
     
    SoftwarePtr softwarepwiz(new Software);
    softwarepwiz->id = "pwiz";
    softwarepwiz->set(MS_pwiz);
    softwarepwiz->version = "1.0";

    msd.softwarePtrs.push_back(softwareBioworks);
    msd.softwarePtrs.push_back(softwarepwiz);
    msd.softwarePtrs.push_back(softwareCompassXtract);

    // dataProcessingList

    DataProcessingPtr dpCompassXtract(new DataProcessing);
    dpCompassXtract->id = "CompassXtract processing";
    
    ProcessingMethod procCXT;
    procCXT.order = 1;
    procCXT.softwarePtr = softwareCompassXtract;
    procCXT.set(MS_deisotoping);
    procCXT.set(MS_charge_deconvolution);
    procCXT.set(MS_peak_picking);

    dpCompassXtract->processingMethods.push_back(procCXT);

    DataProcessingPtr dppwiz(new DataProcessing("pwiz_processing"));

    ProcessingMethod procpwiz;
    procpwiz.order = 2;
    procpwiz.softwarePtr = softwarepwiz;
    procpwiz.set(MS_Conversion_to_mzML);

    dppwiz->processingMethods.push_back(procpwiz);
 
    msd.dataProcessingPtrs.push_back(dpCompassXtract);
    //msd.dataProcessingPtrs.push_back(dppwiz);

    ScanSettingsPtr as1(new ScanSettings("tiny scan settings"));
    as1->sourceFilePtrs.push_back(sfp_parameters);

    Target t1;
    t1.set(MS_selected_ion_m_z, 1000, MS_m_z);
    Target t2;
    t2.set(MS_selected_ion_m_z, 1200, MS_m_z);
    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.scanSettingsPtrs.push_back(as1);


    // run

    msd.run.id = "Experiment 1";
    msd.run.defaultInstrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.defaultSourceFilePtr = sfp;

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    spectrumList->dp = dppwiz;
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s19 = *spectrumList->spectra[0];
    s19.id = "scan=19";
    s19.index = 0;

    s19.set(MS_ms_level, 1);

    s19.set(MS_centroid_spectrum);
    s19.set(MS_lowest_observed_m_z, 400.39, MS_m_z);
    s19.set(MS_highest_observed_m_z, 1795.56, MS_m_z);
    s19.set(MS_base_peak_m_z, 445.347, MS_m_z);
    s19.set(MS_base_peak_intensity, 120053, MS_number_of_detector_counts);
    s19.set(MS_total_ion_current, 1.66755e+007);

    s19.paramGroupPtrs.push_back(pg1);
    s19.scanList.scans.push_back(Scan());
    s19.scanList.set(MS_no_combination);
    Scan& s19scan = s19.scanList.scans.back();
    s19scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s19scan.set(MS_scan_start_time, 5.890500, UO_minute);
    s19scan.set(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]");
    s19scan.set(MS_preset_scan_configuration, 3);
    s19scan.scanWindows.resize(1);
    ScanWindow& window = s19.scanList.scans.back().scanWindows.front();
    window.set(MS_scan_window_lower_limit, 400.000000, MS_m_z);
    window.set(MS_scan_window_upper_limit, 1800.000000, MS_m_z);

    BinaryDataArrayPtr s19_mz(new BinaryDataArray);
    s19_mz->dataProcessingPtr = dpCompassXtract;
    s19_mz->set(MS_m_z_array, "", MS_m_z);
    s19_mz->data.resize(15);
    for (int i=0; i<15; i++)
        s19_mz->data[i] = i;

    BinaryDataArrayPtr s19_intensity(new BinaryDataArray);
    s19_intensity->dataProcessingPtr = dpCompassXtract;
    s19_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    s19_intensity->data.resize(15);
    for (int i=0; i<15; i++)
        s19_intensity->data[i] = 15-i;

    s19.binaryDataArrayPtrs.push_back(s19_mz);
    s19.binaryDataArrayPtrs.push_back(s19_intensity);
    s19.defaultArrayLength = s19_mz->data.size();

    Spectrum& s20 = *spectrumList->spectra[1];
    s20.id = "scan=20";
    s20.index = 1;

    s20.paramGroupPtrs.push_back(pg2);
    s20.set(MS_ms_level, 2);

    s20.set(MS_profile_spectrum);
    s20.set(MS_lowest_observed_m_z, 320.39, MS_m_z);
    s20.set(MS_highest_observed_m_z, 1003.56, MS_m_z);
    s20.set(MS_base_peak_m_z, 456.347, MS_m_z);
    s20.set(MS_base_peak_intensity, 23433, MS_number_of_detector_counts);
    s20.set(MS_total_ion_current, 1.66755e+007);

    s20.precursors.resize(1);
    Precursor& precursor = s20.precursors.front();
    precursor.spectrumID= s19.id;
    precursor.isolationWindow.set(MS_isolation_window_target_m_z, 445.3, MS_m_z);
    precursor.isolationWindow.set(MS_isolation_window_lower_offset, .5, MS_m_z);
    precursor.isolationWindow.set(MS_isolation_window_upper_offset, .5, MS_m_z);
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
    precursor.selectedIons[0].set(MS_peak_intensity, 120053, MS_number_of_detector_counts);
    precursor.selectedIons[0].set(MS_charge_state, 2);
    precursor.activation.set(MS_collision_induced_dissociation);
    precursor.activation.set(MS_collision_energy, 35.00, UO_electronvolt);

    s20.scanList.scans.push_back(Scan());
    s20.scanList.set(MS_no_combination);
    Scan& s20scan = s20.scanList.scans.back();
    s20scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s20scan.set(MS_scan_start_time, 5.990500, UO_minute);
    s20scan.set(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]");
    s20scan.set(MS_preset_scan_configuration, 4);
    s20scan.scanWindows.resize(1);
    ScanWindow& window2 = s20scan.scanWindows.front();
    window2.set(MS_scan_window_lower_limit, 110.000000, MS_m_z);
    window2.set(MS_scan_window_upper_limit, 905.000000, MS_m_z);

    BinaryDataArrayPtr s20_mz(new BinaryDataArray);
    s20_mz->dataProcessingPtr = dpCompassXtract;
    s20_mz->set(MS_m_z_array, "", MS_m_z);
    s20_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s20_mz->data[i] = i*2;

    BinaryDataArrayPtr s20_intensity(new BinaryDataArray);
    s20_intensity->dataProcessingPtr = dpCompassXtract;
    s20_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    s20_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s20_intensity->data[i] = (10-i)*2;

    s20.binaryDataArrayPtrs.push_back(s20_mz);
    s20.binaryDataArrayPtrs.push_back(s20_intensity);
    s20.defaultArrayLength = s20_mz->data.size();

    // spectrum with no data

    Spectrum& s21 = *spectrumList->spectra[2];
    s21.id = "scan=21";
    s21.index = 2;

    s21.paramGroupPtrs.push_back(pg1);
    s21.set(MS_ms_level, 1);
    s21.set(MS_centroid_spectrum);
    s21.userParams.push_back(UserParam("example", "spectrum with no data"));
    s21.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);

    s21.scanList.scans.push_back(Scan());
    s21.scanList.scans.back().instrumentConfigurationPtr = instrumentConfigurationPtr;
    s21.scanList.set(MS_no_combination);

    // Cover ETD, ETD+SA, and ECD precursor activation mode usage

    Spectrum& s22 = *spectrumList->spectra[3];
    s22.id = "scan=22";
    s22.index = 3;

    s22.paramGroupPtrs.push_back(pg2);
    s22.set(MS_ms_level, 2);

    s22.set(MS_profile_spectrum);
    s22.set(MS_lowest_observed_m_z, 320.39, MS_m_z);
    s22.set(MS_highest_observed_m_z, 1003.56, MS_m_z);
    s22.set(MS_base_peak_m_z, 456.347, MS_m_z);
    s22.set(MS_base_peak_intensity, 23433, MS_number_of_detector_counts);
    s22.set(MS_total_ion_current, 1.66755e+007);

    s22.precursors.resize(1);
    Precursor& precursor22 = s22.precursors.front();
    precursor22.spectrumID= s19.id;
    precursor22.isolationWindow.set(MS_isolation_window_target_m_z, 545.3, MS_m_z);
    precursor22.isolationWindow.set(MS_isolation_window_lower_offset, .5, MS_m_z);
    precursor22.isolationWindow.set(MS_isolation_window_upper_offset, .5, MS_m_z);
    precursor22.selectedIons.resize(1);
    precursor22.selectedIons[0].set(MS_selected_ion_m_z, 545.34, MS_m_z);
    precursor22.selectedIons[0].set(MS_peak_intensity, 120053, MS_number_of_detector_counts);
    precursor22.selectedIons[0].set(MS_charge_state, 2);
    precursor22.activation.set(MS_ETD);
    precursor22.activation.set(MS_CID);
    precursor22.activation.set(MS_collision_energy, 60.00, UO_electronvolt);
    s22.scanList.scans.push_back(Scan());
    s22.scanList.set(MS_no_combination);
    Scan& s22scan = s22.scanList.scans.back();
    s22scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s22scan.set(MS_scan_start_time, 6.5, UO_minute);
    s22scan.set(MS_filter_string, "+ c d Full ms2  445.35@etd60.00 [ 110.00-905.00]");
    s22scan.set(MS_preset_scan_configuration, 4);
    s22scan.scanWindows.resize(1);
    window2 = s22scan.scanWindows.front();
    window2.set(MS_scan_window_lower_limit, 110.000000, MS_m_z);
    window2.set(MS_scan_window_upper_limit, 905.000000, MS_m_z);

    BinaryDataArrayPtr s22_mz(new BinaryDataArray);
    s22_mz->dataProcessingPtr = dpCompassXtract;
    s22_mz->set(MS_m_z_array, "", MS_m_z);
    s22_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s22_mz->data[i] = i*2;

    BinaryDataArrayPtr s22_intensity(new BinaryDataArray);
    s22_intensity->dataProcessingPtr = dpCompassXtract;
    s22_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    s22_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s22_intensity->data[i] = (10-i)*2;

    s22.binaryDataArrayPtrs.push_back(s22_mz);
    s22.binaryDataArrayPtrs.push_back(s22_intensity);
    s22.defaultArrayLength = s22_mz->data.size();

    // spectrum with MALDI spot information
    Spectrum& s23 = *spectrumList->spectra[4];
    s23.id = "sample=1 period=1 cycle=23 experiment=1";
    s23.index = 4;
    s23.spotID = "A1,42x42,4242x4242";
    s23.sourceFilePtr = sfp2;

    s23.set(MS_ms_level, 1);
    s23.set(MS_centroid_spectrum);
    s23.set(MS_lowest_observed_m_z, 142.39, MS_m_z);
    s23.set(MS_highest_observed_m_z, 942.56, MS_m_z);
    s23.set(MS_base_peak_m_z, 422.42, MS_m_z);
    s23.set(MS_base_peak_intensity, 42, MS_number_of_detector_counts);
    s23.set(MS_total_ion_current, 4200);
    s23.userParams.push_back(UserParam("alternate source file", "to test a different nativeID format"));
    s23.paramGroupPtrs.push_back(pg1);
    s23.scanList.scans.push_back(Scan());
    s23.scanList.set(MS_no_combination);
    Scan& s23scan = s23.scanList.scans.back();
    s23scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s23scan.set(MS_scan_start_time, 42.0500, UO_second);
    s23scan.set(MS_filter_string, "+ c MALDI Full ms [100.00-1000.00]");
    s23scan.scanWindows.resize(1);
    ScanWindow& window3 = s23scan.scanWindows.front();
    window3.set(MS_scan_window_lower_limit, 100.000000, MS_m_z);
    window3.set(MS_scan_window_upper_limit, 1000.000000, MS_m_z);

    s23.binaryDataArrayPtrs.push_back(s19_mz);
    s23.binaryDataArrayPtrs.push_back(s19_intensity);
    s23.defaultArrayLength = s19_mz->data.size();


    // chromatograms

    shared_ptr<ChromatogramListSimple> chromatogramList(new ChromatogramListSimple);
    msd.run.chromatogramListPtr = chromatogramList;

    chromatogramList->dp = dppwiz;
    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));

    Chromatogram& tic = *chromatogramList->chromatograms[0];
    tic.id = "tic";
    tic.index = 0;
    tic.defaultArrayLength = 15;
    tic.dataProcessingPtr = dpCompassXtract;
    tic.set(MS_total_ion_current_chromatogram);

    BinaryDataArrayPtr tic_time(new BinaryDataArray);
    tic_time->dataProcessingPtr = dppwiz;
    tic_time->set(MS_time_array, "", UO_second);
    tic_time->data.resize(15);
    for (int i=0; i<15; i++)
        tic_time->data[i] = i;

    BinaryDataArrayPtr tic_intensity(new BinaryDataArray);
    tic_intensity->dataProcessingPtr = dppwiz;
    tic_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    tic_intensity->data.resize(15);
    for (int i=0; i<15; i++)
        tic_intensity->data[i] = 15-i;

    tic.binaryDataArrayPtrs.push_back(tic_time);
    tic.binaryDataArrayPtrs.push_back(tic_intensity);

    Chromatogram& sic = *chromatogramList->chromatograms[1];
    sic.id = "sic";
    sic.index = 1;
    sic.defaultArrayLength = 10;
    sic.dataProcessingPtr = dppwiz;
    sic.set(MS_selected_ion_current_chromatogram);

    sic.precursor.isolationWindow.set(MS_isolation_window_target_m_z, 456.7, MS_m_z);
    sic.precursor.activation.set(MS_CID);
    sic.product.isolationWindow.set(MS_isolation_window_target_m_z, 678.9, MS_m_z);

    BinaryDataArrayPtr sic_time(new BinaryDataArray);
    sic_time->dataProcessingPtr = dppwiz;
    sic_time->set(MS_time_array, "", UO_second);
    sic_time->data.resize(10);
    for (int i=0; i<10; i++)
        sic_time->data[i] = i;

    BinaryDataArrayPtr sic_intensity(new BinaryDataArray);
    sic_intensity->dataProcessingPtr = dppwiz;
    sic_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
    sic_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        sic_intensity->data[i] = 10-i;

    sic.binaryDataArrayPtrs.push_back(sic_time);
    sic.binaryDataArrayPtrs.push_back(sic_intensity);

} // initializeTiny()


PWIZ_API_DECL void addMIAPEExampleMetadata(MSData& msd)
{
    msd.id = "urn:lsid:psidev.info:mzML.instanceDocuments.small_miape.pwiz";

    msd.cvs = defaultCVList(); // TODO: move this to Reader_Thermo

    FileContent& fc = msd.fileDescription.fileContent;
    fc.userParams.push_back(UserParam("ProteoWizard", "Thermo RAW data converted to mzML, with additional MIAPE parameters added for illustration"));

    // fileDescription

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file:///C:/example/"));
	sfp_parameters->set(MS_parameter_file);
    sfp_parameters->set(MS_SHA_1, "unknown");
    sfp_parameters->set(MS_no_nativeID_format);
    msd.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    Contact contact;
    contact.set(MS_contact_name, "William Pennington");
    contact.set(MS_contact_affiliation, "Higglesworth University");
    contact.set(MS_contact_address, "12 Higglesworth Avenue, 12045, HI, USA");
	contact.set(MS_contact_URL, "http://www.higglesworth.edu/");
	contact.set(MS_contact_email, "wpennington@higglesworth.edu");
    msd.fileDescription.contacts.push_back(contact);

    // paramGroupList

    ParamGroupPtr pgInstrumentCustomization(new ParamGroup);
    pgInstrumentCustomization->id = "InstrumentCustomization";
    pgInstrumentCustomization->set(MS_customization ,"none");
    msd.paramGroupPtrs.push_back(pgInstrumentCustomization);

    ParamGroupPtr pgActivation(new ParamGroup);
    pgActivation->id = "CommonActivationParams";
    pgActivation->set(MS_collision_induced_dissociation);
    pgActivation->set(MS_collision_energy, 35.00, UO_electronvolt);
    pgActivation->set(MS_collision_gas, "nitrogen"); 
    msd.paramGroupPtrs.push_back(pgActivation);

    // sampleList

    SamplePtr sample1(new Sample);
    sample1->id = "sample1";
    sample1->name = "Sample 1";
    msd.samplePtrs.push_back(sample1);

    SamplePtr sample2(new Sample);
    sample2->id = "sample2";
    sample2->name = "Sample 2";
    msd.samplePtrs.push_back(sample2);

    // instrumentConfigurationList

    for (vector<InstrumentConfigurationPtr>::const_iterator it=msd.instrumentConfigurationPtrs.begin(),
         end=msd.instrumentConfigurationPtrs.end(); it!=end; ++it)
    {
        for (size_t i=0; i < (*it)->componentList.size(); ++i)
        {
            Component& c = (*it)->componentList[i];
            if (c.type == ComponentType_Source)
                c.set(MS_source_potential, "4.20", UO_volt);
        }
    }
 
    // dataProcesingList

    ProcessingMethod procMIAPE;
    procMIAPE.order = 1;
    procMIAPE.softwarePtr = msd.softwarePtrs.back();
    procMIAPE.set(MS_deisotoping);
    procMIAPE.set(MS_charge_deconvolution);
    procMIAPE.set(MS_peak_picking);
    procMIAPE.set(MS_smoothing);
    procMIAPE.set(MS_baseline_reduction);
    procMIAPE.userParams.push_back(UserParam("signal-to-noise estimation", "none"));
    procMIAPE.userParams.push_back(UserParam("centroiding algorithm", "none"));
    procMIAPE.userParams.push_back(UserParam("charge states calculated", "none"));

    DataProcessingPtr dpMIAPE(new DataProcessing);
    msd.dataProcessingPtrs.push_back(dpMIAPE);
    dpMIAPE->id = "MIAPE example";
    dpMIAPE->processingMethods.push_back(procMIAPE);

    // acquisition settings
    
    ScanSettingsPtr as1(new ScanSettings("acquisition settings MIAPE example"));
    as1->sourceFilePtrs.push_back(sfp_parameters);

    Target t1;
    t1.userParams.push_back(UserParam("precursorMz", "123.456")); 
    t1.userParams.push_back(UserParam("fragmentMz", "456.789")); 
    t1.userParams.push_back(UserParam("dwell time", "1", "seconds")); 
    t1.userParams.push_back(UserParam("active time", "0.5", "seconds")); 
    
    Target t2;
    t2.userParams.push_back(UserParam("precursorMz", "231.673")); 
    t2.userParams.push_back(UserParam("fragmentMz", "566.328")); 
    t2.userParams.push_back(UserParam("dwell time", "1", "seconds")); 
    t2.userParams.push_back(UserParam("active time", "0.5", "seconds")); 

    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.scanSettingsPtrs.push_back(as1);

    // run
    
    msd.run.samplePtr = sample1;

} // addMIAPEExampleMetadata()


} // namespace examples
} // namespace msdata
} // namespace pwiz


