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
    msd.accession = "test accession";
    msd.id = "testid";
    msd.version = "test version";

    // cvList

    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "http://psidev.sourceforge.net/ms/xml/mzdata/psi-ms.2.0.2.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "2.0.2";

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);

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
    Source& source = instrumentConfigurationPtr->componentList.source;
    source.order = 1;
    source.set(MS_nanoelectrospray);
    Analyzer& analyzer = instrumentConfigurationPtr->componentList.analyzer;
    analyzer.order = 2;
    analyzer.set(MS_quadrupole_ion_trap);
    Detector& detector = instrumentConfigurationPtr->componentList.detector;
    detector.order = 3;
    detector.set(MS_electron_multiplier);

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
    msd.run.instrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.sourceFilePtrs.push_back(sfp);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

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
    tic.set(MS_total_ion_chromatogram__);

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
    sic.set(MS_total_ion_chromatogram__);

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


namespace {


struct Datum
{
    double mz;
    double intensity;
};


MZIntensityPair datumToMZIntensityPair(const Datum& datum)
{
    MZIntensityPair result;
    result.mz = datum.mz;
    result.intensity = datum.intensity;
    return result;
}


Datum data_5pep_FT_[] =
{
    {11.1, 100.1},
    {12.1, 101.1},
    {13.1, 102.1}
}; // data_5pep_FT_


size_t data_5pep_FT_size_ = sizeof(data_5pep_FT_)/sizeof(Datum);


Datum data_5pep_IT_[] =
{
     {21.1, 100.1},
     {22.1, 101.1},
     {23.1, 102.1}
};


size_t data_5pep_IT_size_ = sizeof(data_5pep_IT_)/sizeof(Datum);


Datum data_5pep_ms2_[] =
{
     {31.1, 100.1},
     {32.1, 101.1},
     {33.1, 102.1}
};


size_t data_5pep_ms2_size_ = sizeof(data_5pep_ms2_)/sizeof(Datum);


void setSpectrumData(Spectrum& spectrum, Datum* data, size_t size)
{
    vector<MZIntensityPair> pairs;
    transform(data, data+size, back_inserter(pairs), datumToMZIntensityPair);
    spectrum.setMZIntensityPairs(pairs);
}


} // namespace


PWIZ_API_DECL SpectrumPtr createSpectrum_5pep_FT(const InstrumentConfigurationPtr& instrumentConfigurationPtr)
{
    SpectrumPtr spectrum(new Spectrum);
    setSpectrumData(*spectrum, data_5pep_FT_, data_5pep_FT_size_);

    spectrum->set(MS_MSn_spectrum);
    spectrum->set(MS_ms_level, 1);

    spectrum->spectrumDescription.set(MS_profile_mass_spectrum);
    spectrum->spectrumDescription.set(MS_lowest_m_z_value, 200);
    spectrum->spectrumDescription.set(MS_highest_m_z_value, 2000);
    spectrum->spectrumDescription.set(MS_base_peak_m_z, 810.415);
    spectrum->spectrumDescription.set(MS_base_peak_intensity, 1.47197e+06);
    spectrum->spectrumDescription.set(MS_total_ion_current, 1.52451e+07);
    spectrum->spectrumDescription.scan.set(MS_scan_time, 0.2961, MS_minute);
    spectrum->spectrumDescription.scan.set(MS_filter_string, "FTMS + p ESI Full ms [200.00-2000.00]");
    spectrum->spectrumDescription.scan.set(MS_preset_scan_configuration, 1);
    spectrum->spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window = spectrum->spectrumDescription.scan.scanWindows.front();
    window.set(MS_scan_m_z_lower_limit, 200.000000);
    window.set(MS_scan_m_z_upper_limit, 2000.000000);

    spectrum->spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;

    return spectrum;
}


PWIZ_API_DECL SpectrumPtr createSpectrum_5pep_IT(const InstrumentConfigurationPtr& instrumentConfigurationPtr)
{
    SpectrumPtr spectrum(new Spectrum);
    setSpectrumData(*spectrum, data_5pep_IT_, data_5pep_IT_size_);

    spectrum->set(MS_MSn_spectrum);
    spectrum->set(MS_ms_level, 1);

    spectrum->spectrumDescription.set(MS_profile_mass_spectrum);
    spectrum->spectrumDescription.set(MS_lowest_m_z_value, 200);
    spectrum->spectrumDescription.set(MS_highest_m_z_value, 2000);
    spectrum->spectrumDescription.set(MS_base_peak_m_z, 810.546);
    spectrum->spectrumDescription.set(MS_base_peak_intensity, 183839);
    spectrum->spectrumDescription.set(MS_total_ion_current, 1.29012e+07);
    spectrum->spectrumDescription.scan.set(MS_scan_time, 0.4738, MS_minute);
    spectrum->spectrumDescription.scan.set(MS_filter_string, "ITMS + p ESI Full ms [200.00-2000.00]");
    spectrum->spectrumDescription.scan.set(MS_preset_scan_configuration, 2);
    spectrum->spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window = spectrum->spectrumDescription.scan.scanWindows.front();
    window.set(MS_scan_m_z_lower_limit, 200.000000);
    window.set(MS_scan_m_z_upper_limit, 2000.000000);

    spectrum->spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;

    return spectrum;
}


PWIZ_API_DECL SpectrumPtr createSpectrum_5pep_ms2(
    const InstrumentConfigurationPtr& instrumentConfigurationPtr,
    const ParamGroupPtr& pgActivation)
{
    SpectrumPtr spectrum(new Spectrum);
    setSpectrumData(*spectrum, data_5pep_ms2_, data_5pep_ms2_size_);

    spectrum->set(MS_MSn_spectrum);
    spectrum->set(MS_ms_level, 2);

    spectrum->spectrumDescription.set(MS_centroid_mass_spectrum);
    spectrum->spectrumDescription.set(MS_lowest_m_z_value, 210);
    spectrum->spectrumDescription.set(MS_highest_m_z_value, 1635);
    spectrum->spectrumDescription.set(MS_base_peak_m_z, 736.637);
    spectrum->spectrumDescription.set(MS_base_peak_intensity, 161141);
    spectrum->spectrumDescription.set(MS_total_ion_current,  586279);
    spectrum->spectrumDescription.scan.set(MS_scan_time, 0.6731, MS_minute);
    spectrum->spectrumDescription.scan.set(MS_filter_string, "ITMS + c ESI d Full ms2 810.79@cid35.00 [210.00-1635.00]");
    spectrum->spectrumDescription.scan.set(MS_preset_scan_configuration, 3);
    spectrum->spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window = spectrum->spectrumDescription.scan.scanWindows.front();
    window.set(MS_scan_m_z_lower_limit, 210);
    window.set(MS_scan_m_z_upper_limit, 1635);

    spectrum->spectrumDescription.precursors.resize(1);
    Precursor& precursor = spectrum->spectrumDescription.precursors.front();
    precursor.spectrumID = "change_me";
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].set(MS_m_z, 810.79);
    precursor.selectedIons[0].set(MS_intensity, 120053);
    precursor.selectedIons[0].set(MS_charge_state, 2);
    precursor.activation.paramGroupPtrs.push_back(pgActivation);
    //precursor.isolationWindow.set(MS_m_z, 810.80);
    precursor.isolationWindow.userParams.push_back(UserParam("isolation center m/z", "810.8"));
    precursor.isolationWindow.userParams.push_back(UserParam("isolation half width", "2.0"));

    spectrum->spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;

    return spectrum;
}


PWIZ_API_DECL void initializeTiny2(MSData& msd)
{
    msd.accession = "test accession";
    msd.id = "testid";
    msd.version = "test version";

    // cvList

    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "http://psidev.sourceforge.net/ms/xml/mzdata/psi-ms.2.0.2.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "2.0.2";

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);

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

    ParamGroupPtr pgInstrument(new ParamGroup);
    pgInstrument->id = "CommonInstrumentParams";
    pgInstrument->set(MS_LTQ_FT);
    pgInstrument->set(MS_instrument_serial_number,"23433");
    pgInstrument->set(MS_customization ,"none");
    msd.paramGroupPtrs.push_back(pgInstrument);

    ParamGroupPtr pgActivation(new ParamGroup);
    pgActivation->id = "CommonActivationParams";
    pgActivation->set(MS_collision_induced_dissociation);
    pgActivation->set(MS_collision_energy, 35.00, MS_electron_volt);
    pgActivation->set(MS_collision_gas, "nitrogen"); 
    msd.paramGroupPtrs.push_back(pgActivation);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "sample1";
    samplePtr->name = "Sample1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentConfigurationList

    InstrumentConfigurationPtr icFT(new InstrumentConfiguration);
    icFT->paramGroupPtrs.push_back(pgInstrument);
    icFT->id = "LTQFT";
    Source& source = icFT->componentList.source;

    source.order = 1;
    source.set(MS_ESI);
    source.set(MS_source_potential, "3.86", UO_kilovolt); 
    Analyzer& analyzer = icFT->componentList.analyzer;
    analyzer.order = 2;
    analyzer.set(MS_FT_ICR);
    Detector& detector = icFT->componentList.detector;
    detector.order = 3;
    detector.set(MS_electron_multiplier); // TODO: what should this be?
    
    InstrumentConfigurationPtr icIT(new InstrumentConfiguration);
    icIT->id = "LTQIonTrap";
    icIT->paramGroupPtrs.push_back(pgInstrument);
    Source& sourceIT = icIT->componentList.source;
    sourceIT.order = 1;
    sourceIT.set(MS_ESI);
    Analyzer& analyzerIT = icIT->componentList.analyzer;
    analyzerIT.order = 2;
    analyzerIT.set(MS_ion_trap);
    Detector& detectorIT = icIT->componentList.detector;
    detectorIT.order = 3;
    detectorIT.set(MS_electron_multiplier);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = "2.0.5";
    icFT->softwarePtr = softwareXcalibur;
    icIT->softwarePtr = softwareXcalibur;

    msd.instrumentConfigurationPtrs.push_back(icFT);
    msd.instrumentConfigurationPtrs.push_back(icIT);

    // softwareList

    SoftwarePtr softwarepwiz(new Software);
    softwarepwiz->id = "pwiz";
    softwarepwiz->softwareParam = MS_pwiz;
    softwarepwiz->softwareParamVersion = "1.0";

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
    procXcal.set(MS_smoothing, false);
    procXcal.set(MS_baseline_reduction, false);
    procXcal.userParams.push_back(UserParam("signal-to-noise estimation", "none"));
    procXcal.userParams.push_back(UserParam("centroiding algorithm", "none"));
    procXcal.userParams.push_back(UserParam("charge states calculated", "none"));

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

    // acquisition settings
    
    AcquisitionSettingsPtr as1(new AcquisitionSettings("as1"));
    as1->instrumentConfigurationPtr = icFT;
    as1->sourceFilePtrs.push_back(sfp_parameters);

    Target t1;
    //t1.set(MS_m_z, 1000);
    t1.userParams.push_back(UserParam("precursorMz", "123.456")); 
    t1.userParams.push_back(UserParam("fragmentMz", "456.789")); 
    t1.userParams.push_back(UserParam("dwell time", "1", "seconds")); 
    t1.userParams.push_back(UserParam("active time", "0.5", "seconds")); 
    
    Target t2;
    //t2.set(MS_m_z, 1200);
    t2.userParams.push_back(UserParam("precursorMz", "231.673")); 
    t2.userParams.push_back(UserParam("fragmentMz", "566.328")); 
    t2.userParams.push_back(UserParam("dwell time", "1", "seconds")); 
    t2.userParams.push_back(UserParam("active time", "0.5", "seconds")); 

    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.acquisitionSettingsPtrs.push_back(as1);


    // run

    msd.run.id = "Exp01";
    msd.run.instrumentConfigurationPtr = icFT; // TODO: global ref?
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.sourceFilePtrs.push_back(sfp);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    spectrumList->spectra.push_back(createSpectrum_5pep_FT(icFT));
    spectrumList->spectra.push_back(createSpectrum_5pep_IT(icIT));
    spectrumList->spectra.push_back(createSpectrum_5pep_ms2(icIT, pgActivation));

    for (size_t i=0; i<spectrumList->size(); i++)
    {
        spectrumList->spectra[i]->index = i;
        spectrumList->spectra[i]->id = "S" + lexical_cast<string>(i);
    }

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
    tic.set(MS_total_ion_chromatogram__);

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
    sic.set(MS_total_ion_chromatogram__);

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

} // initializeTiny2()


} // namespace examples
} // namespace msdata
} // namespace pwiz


