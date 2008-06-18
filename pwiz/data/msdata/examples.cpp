//
// examples.cpp 
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


namespace pwiz {
namespace msdata {
namespace examples {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


PWIZ_API_DECL void initializeTiny(MSData& msd)
{
    msd.id = "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz";
    msd.version = "1.0";

    // cvList

    msd.cvs = defaultCVList();

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);
    fc.set(MS_centroid_mass_spectrum);

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "sf1";
    sfp->name = "tiny1.RAW";
    sfp->location = "file:///F:/data/Exp01";
    sfp->set(MS_Xcalibur_RAW_file);
    sfp->set(MS_SHA_1,"71be39fb2700ab2f3c8b2234b91274968b6899b1");
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file:///C:/settings/"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    msd.fileDescription.contacts.resize(1);
    Contact& contact = msd.fileDescription.contacts.front();
    contact.set(MS_contact_name, "William Pennington");
    contact.set(MS_contact_address, 
                               "Higglesworth University, 12 Higglesworth Avenue, 12045, HI, USA");
	contact.set(MS_contact_URL, "http://www.higglesworth.edu/");
	contact.set(MS_contact_email, "wpennington@higglesworth.edu");

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->set(MS_positive_scan);
    pg1->set(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->set(MS_positive_scan);
    pg2->set(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg2);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "sample1";
    samplePtr->name = "Sample1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentConfigurationList

    InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration);
    instrumentConfigurationPtr->id = "LCQDeca";
    instrumentConfigurationPtr->set(MS_LCQ_Deca);
    instrumentConfigurationPtr->set(MS_instrument_serial_number,"23433");
    instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_electron_multiplier, 3));

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = "2.0.5";
    instrumentConfigurationPtr->softwarePtr = softwareXcalibur;

    msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

    // softwareList

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->softwareParam = MS_Bioworks;
    softwareBioworks->softwareParamVersion = "3.3.1 sp1";
     
    SoftwarePtr softwarepwiz(new Software);
    softwarepwiz->id = "pwiz";
    softwarepwiz->softwareParam = MS_pwiz;
    softwarepwiz->softwareParamVersion = "1.0";

    msd.softwarePtrs.push_back(softwareBioworks);
    msd.softwarePtrs.push_back(softwarepwiz);
    msd.softwarePtrs.push_back(softwareXcalibur);

    // dataProcessingList

    DataProcessingPtr dpXcalibur(new DataProcessing);
    dpXcalibur->id = "XcaliburProcessing";
    dpXcalibur->softwarePtr = softwareXcalibur;
    
    ProcessingMethod procXcal;
    procXcal.order = 1;
    procXcal.set(MS_deisotoping, false);
    procXcal.set(MS_charge_deconvolution, false);
    procXcal.set(MS_peak_picking, true);

    dpXcalibur->processingMethods.push_back(procXcal);

    DataProcessingPtr dppwiz(new DataProcessing);
    dppwiz->id = "pwizconversion";
    dppwiz->softwarePtr = softwarepwiz;

    ProcessingMethod procpwiz;
    procpwiz.order = 2;
    procpwiz.set(MS_Conversion_to_mzML);

    dppwiz->processingMethods.push_back(procpwiz);
 
    msd.dataProcessingPtrs.push_back(dpXcalibur);
    msd.dataProcessingPtrs.push_back(dppwiz);

    AcquisitionSettingsPtr as1(new AcquisitionSettings("as1"));
    as1->instrumentConfigurationPtr = instrumentConfigurationPtr;
    as1->sourceFilePtrs.push_back(sfp_parameters);

    Target t1;
    t1.set(MS_m_z, 1000);
    Target t2;
    t2.set(MS_m_z, 1200);
    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.acquisitionSettingsPtrs.push_back(as1);


    // run

    msd.run.id = "Exp01";
    msd.run.defaultInstrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.sourceFilePtrs.push_back(sfp);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s19 = *spectrumList->spectra[0];
    s19.id = "S19";
    s19.index = 0;
    s19.nativeID = "19";

    s19.set(MS_MSn_spectrum);
    s19.set(MS_ms_level, 1);

    s19.spectrumDescription.set(MS_centroid_mass_spectrum);
    s19.spectrumDescription.set(MS_lowest_m_z_value, 400.39);
    s19.spectrumDescription.set(MS_highest_m_z_value, 1795.56);
    s19.spectrumDescription.set(MS_base_peak_m_z, 445.347);
    s19.spectrumDescription.set(MS_base_peak_intensity, 120053);
    s19.spectrumDescription.set(MS_total_ion_current, 1.66755e+007);
    s19.spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s19.spectrumDescription.scan.paramGroupPtrs.push_back(pg1);
    s19.spectrumDescription.scan.set(MS_scan_time, 5.890500, MS_minute);
    s19.spectrumDescription.scan.set(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]");
    s19.spectrumDescription.scan.set(MS_preset_scan_configuration, 3);
    s19.spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window = s19.spectrumDescription.scan.scanWindows.front();
    window.set(MS_scan_m_z_lower_limit, 400.000000);
    window.set(MS_scan_m_z_upper_limit, 1800.000000);

    BinaryDataArrayPtr s19_mz(new BinaryDataArray);
    s19_mz->dataProcessingPtr = dpXcalibur;
    s19_mz->set(MS_m_z_array);
    s19_mz->data.resize(15);
    for (int i=0; i<15; i++)
        s19_mz->data[i] = i;

    BinaryDataArrayPtr s19_intensity(new BinaryDataArray);
    s19_intensity->dataProcessingPtr = dpXcalibur;
    s19_intensity->set(MS_intensity_array);
    s19_intensity->data.resize(15);
    for (int i=0; i<15; i++)
        s19_intensity->data[i] = 15-i;

    s19.binaryDataArrayPtrs.push_back(s19_mz);
    s19.binaryDataArrayPtrs.push_back(s19_intensity);
    s19.defaultArrayLength = s19_mz->data.size();

    Spectrum& s20 = *spectrumList->spectra[1];
    s20.id = "S20";
    s20.index = 1;
    s20.nativeID = "20";

    s20.set(MS_MSn_spectrum);
    s20.set(MS_ms_level, 2);

    s20.spectrumDescription.set(MS_centroid_mass_spectrum);
    s20.spectrumDescription.set(MS_lowest_m_z_value, 320.39);
    s20.spectrumDescription.set(MS_highest_m_z_value, 1003.56);
    s20.spectrumDescription.set(MS_base_peak_m_z, 456.347);
    s20.spectrumDescription.set(MS_base_peak_intensity, 23433);
    s20.spectrumDescription.set(MS_total_ion_current, 1.66755e+007);

    s20.spectrumDescription.precursors.resize(1);
    Precursor& precursor = s20.spectrumDescription.precursors.front();
    precursor.spectrumID= s19.id;
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].set(MS_m_z, 445.34);
    precursor.selectedIons[0].set(MS_intensity, 120053);
    precursor.selectedIons[0].set(MS_charge_state, 2);
    precursor.activation.set(MS_collision_induced_dissociation);
    precursor.activation.set(MS_collision_energy, 35.00, MS_electron_volt);

    s20.spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s20.spectrumDescription.scan.paramGroupPtrs.push_back(pg2);
    s20.spectrumDescription.scan.set(MS_scan_time, 5.990500, MS_minute);
    s20.spectrumDescription.scan.set(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]");
    s20.spectrumDescription.scan.set(MS_preset_scan_configuration, 4);
    s20.spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window2 = s20.spectrumDescription.scan.scanWindows.front();
    window2.set(MS_scan_m_z_lower_limit, 110.000000);
    window2.set(MS_scan_m_z_upper_limit, 905.000000);

    BinaryDataArrayPtr s20_mz(new BinaryDataArray);
    s20_mz->dataProcessingPtr = dpXcalibur;
    s20_mz->set(MS_m_z_array);
    s20_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s20_mz->data[i] = i*2;

    BinaryDataArrayPtr s20_intensity(new BinaryDataArray);
    s20_intensity->dataProcessingPtr = dpXcalibur;
    s20_intensity->set(MS_intensity_array);
    s20_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s20_intensity->data[i] = (10-i)*2;

    s20.binaryDataArrayPtrs.push_back(s20_mz);
    s20.binaryDataArrayPtrs.push_back(s20_intensity);
    s20.defaultArrayLength = s20_mz->data.size();

    // spectrum with no data

    Spectrum& s21 = *spectrumList->spectra[2]; 
    s21.id = "S21";
    s21.index = 2;
    s21.nativeID = "21";

    s21.set(MS_MSn_spectrum);
    s21.set(MS_ms_level, 1);

    s21.spectrumDescription.userParams.push_back(UserParam("example", "spectrum with no data"));
    s21.setMZIntensityArrays(vector<double>(), vector<double>());

    // spectrum with MALDI spot information
    Spectrum& s22 = *spectrumList->spectra[3];
    s22.id = "S22";
    s22.index = 3;
    s22.nativeID = "22";
    s22.spotID = "A1,42x42,4242x4242";

    s22.set(MS_MSn_spectrum);
    s22.set(MS_ms_level, 1);
    
    s22.spectrumDescription.set(MS_centroid_mass_spectrum);
    s22.spectrumDescription.set(MS_lowest_m_z_value, 142.39);
    s22.spectrumDescription.set(MS_highest_m_z_value, 942.56);
    s22.spectrumDescription.set(MS_base_peak_m_z, 422.42);
    s22.spectrumDescription.set(MS_base_peak_intensity, 42);
    s22.spectrumDescription.set(MS_total_ion_current, 4200);
    s22.spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s22.spectrumDescription.scan.paramGroupPtrs.push_back(pg1);
    s22.spectrumDescription.scan.set(MS_scan_time, 42.0500, MS_second);
    s22.spectrumDescription.scan.set(MS_filter_string, "+ c MALDI Full ms [100.00-1000.00]");
    s22.spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window3 = s22.spectrumDescription.scan.scanWindows.front();
    window3.set(MS_scan_m_z_lower_limit, 100.000000);
    window3.set(MS_scan_m_z_upper_limit, 1000.000000);

    BinaryDataArrayPtr s22_mz(new BinaryDataArray);
    s22_mz->dataProcessingPtr = dpXcalibur;
    s22_mz->set(MS_m_z_array);
    s22_mz->data.resize(15);
    for (int i=0; i<15; i++)
        s22_mz->data[i] = i;

    BinaryDataArrayPtr s22_intensity(new BinaryDataArray);
    s22_intensity->dataProcessingPtr = dpXcalibur;
    s22_intensity->set(MS_intensity_array);
    s22_intensity->data.resize(15);
    for (int i=0; i<15; i++)
        s22_intensity->data[i] = 15-i;

    s22.binaryDataArrayPtrs.push_back(s22_mz);
    s22.binaryDataArrayPtrs.push_back(s22_intensity);
    s22.defaultArrayLength = s22_mz->data.size();

    // chromatograms

    shared_ptr<ChromatogramListSimple> chromatogramList(new ChromatogramListSimple);
    msd.run.chromatogramListPtr = chromatogramList;

    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));

    Chromatogram& tic = *chromatogramList->chromatograms[0];
    tic.id = "tic";
    tic.index = 0;
    tic.nativeID = "tic native";
    tic.defaultArrayLength = 15;
    tic.dataProcessingPtr = dpXcalibur;
    tic.set(MS_total_ion_current_chromatogram);

    BinaryDataArrayPtr tic_time(new BinaryDataArray);
    tic_time->dataProcessingPtr = dppwiz;
    tic_time->set(MS_time_array);
    tic_time->data.resize(15);
    for (int i=0; i<15; i++)
        tic_time->data[i] = i;

    BinaryDataArrayPtr tic_intensity(new BinaryDataArray);
    tic_intensity->dataProcessingPtr = dppwiz;
    tic_intensity->set(MS_intensity_array);
    tic_intensity->data.resize(15);
    for (int i=0; i<15; i++)
        tic_intensity->data[i] = 15-i;

    tic.binaryDataArrayPtrs.push_back(tic_time);
    tic.binaryDataArrayPtrs.push_back(tic_intensity);

    Chromatogram& sic = *chromatogramList->chromatograms[1];
    sic.id = "sic";
    sic.index = 1;
    sic.nativeID = "sic native";
    sic.defaultArrayLength = 10;
    sic.dataProcessingPtr = dppwiz;
    sic.set(MS_total_ion_current_chromatogram);

    BinaryDataArrayPtr sic_time(new BinaryDataArray);
    sic_time->dataProcessingPtr = dppwiz;
    sic_time->set(MS_time_array);
    sic_time->data.resize(10);
    for (int i=0; i<10; i++)
        sic_time->data[i] = i;

    BinaryDataArrayPtr sic_intensity(new BinaryDataArray);
    sic_intensity->dataProcessingPtr = dppwiz;
    sic_intensity->set(MS_intensity_array);
    sic_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        sic_intensity->data[i] = 10-i;

    sic.binaryDataArrayPtrs.push_back(sic_time);
    sic.binaryDataArrayPtrs.push_back(sic_intensity);

} // initializeTiny()


PWIZ_API_DECL void addMIAPEExampleMetadata(MSData& msd)
{
    //msd.id = "urn:lsid:psidev.info:mzML.instanceDocuments.small_miape.pwiz"; //TODO: schema xs:ID -> LSID
    msd.id = "small_miape_pwiz";
    msd.version = "1.0";

    msd.cvs = defaultCVList(); // TODO: move this to Reader_Thermo

    FileContent& fc = msd.fileDescription.fileContent;
    fc.userParams.push_back(UserParam("ProteoWizard", "Thermo RAW data converted to mzML, with additional MIAPE parameters added for illustration"));

    // fileDescription

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file:///C:/example/"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    Contact contact;
    contact.set(MS_contact_name, "William Pennington");
    contact.set(MS_contact_address, "Higglesworth University, 12 Higglesworth Avenue, 12045, HI, USA");
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
    pgActivation->set(MS_collision_energy, 35.00, MS_electron_volt);
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
                c.set(MS_source_potential, "4.20", UO_kilovolt);
        }
    }
 
    // dataProcesingList

    ProcessingMethod procMIAPE;
    procMIAPE.order = 1;
    procMIAPE.set(MS_deisotoping, false);
    procMIAPE.set(MS_charge_deconvolution, false);
    procMIAPE.set(MS_peak_picking, true);
    procMIAPE.set(MS_smoothing, false);
    procMIAPE.set(MS_baseline_reduction, false);
    procMIAPE.userParams.push_back(UserParam("signal-to-noise estimation", "none"));
    procMIAPE.userParams.push_back(UserParam("centroiding algorithm", "none"));
    procMIAPE.userParams.push_back(UserParam("charge states calculated", "none"));

    DataProcessingPtr dpMIAPE(new DataProcessing);
    msd.dataProcessingPtrs.push_back(dpMIAPE);
    dpMIAPE->id = "MIAPE_example";
    dpMIAPE->softwarePtr = msd.softwarePtrs.back();
    dpMIAPE->processingMethods.push_back(procMIAPE);

    // acquisition settings
    
    AcquisitionSettingsPtr as1(new AcquisitionSettings("acquisition_settings_MIAPE_example"));
    as1->instrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0]; 
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
    msd.acquisitionSettingsPtrs.push_back(as1);

    // run
    
    msd.run.samplePtr = sample1;

} // addMIAPEExampleMetadata()


} // namespace examples
} // namespace msdata
} // namespace pwiz


