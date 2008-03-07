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


#include "examples.hpp"


namespace pwiz {
namespace msdata {
namespace examples {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


void initializeTiny(MSData& msd)
{
    msd.accession = "test accession";
    msd.id = "test id";
    msd.version = "test version";

    // cvList

    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "http://psidev.sourceforge.net/ms/xml/mzdata/psi-ms.2.0.2.obo"; 
    cv.cvLabel = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "2.0.2";

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.cvParams.push_back(MS_MSn_spectrum);
    fc.userParams.push_back(UserParam("number of cats", "4"));

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "1";
    sfp->name = "tiny1.RAW";
    sfp->location = "file://F:/data/Exp01";
    sfp->cvParams.push_back(MS_Xcalibur_RAW_file);
    sfp->cvParams.push_back(CVParam(MS_SHA_1,"71be39fb2700ab2f3c8b2234b91274968b6899b1"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    msd.fileDescription.contacts.resize(1);
    Contact& contact = msd.fileDescription.contacts.front();
    contact.cvParams.push_back(CVParam(MS_contact_name, "William Pennington"));
    contact.cvParams.push_back(CVParam(MS_contact_address, 
                               "Higglesworth University, 12 Higglesworth Avenue, 12045, HI, USA"));
	contact.cvParams.push_back(CVParam(MS_contact_URL, "http://www.higglesworth.edu/"));
	contact.cvParams.push_back(CVParam(MS_contact_email, "wpennington@higglesworth.edu"));

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->cvParams.push_back(MS_positive_scan);
    pg1->cvParams.push_back(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->cvParams.push_back(MS_positive_scan);
    pg2->cvParams.push_back(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg2);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "1";
    samplePtr->name = "Sample1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentList

    InstrumentPtr instrumentPtr(new Instrument);
    instrumentPtr->id = "LCQ Deca";
    instrumentPtr->cvParams.push_back(MS_LCQ_Deca);
    instrumentPtr->cvParams.push_back(CVParam(MS_instrument_serial_number,"23433"));
    Source& source = instrumentPtr->componentList.source;
    source.order = 1;
    source.cvParams.push_back(MS_nanoelectrospray);
    Analyzer& analyzer = instrumentPtr->componentList.analyzer;
    analyzer.order = 2;
    analyzer.cvParams.push_back(MS_quadrupole_ion_trap);
    Detector& detector = instrumentPtr->componentList.detector;
    detector.order = 3;
    detector.cvParams.push_back(MS_electron_multiplier);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = "2.0.5";
    instrumentPtr->softwarePtr = softwareXcalibur;

    msd.instrumentPtrs.push_back(instrumentPtr);

    // softwareList

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->softwareParam = MS_Bioworks;
    softwareBioworks->softwareParamVersion = "3.3.1 sp1";
     
    SoftwarePtr softwareReAdW(new Software);
    softwareReAdW->id = "ReAdW";
    softwareReAdW->softwareParam = MS_ReAdW;
    softwareReAdW->softwareParamVersion = "1.0";

    msd.softwarePtrs.push_back(softwareBioworks);
    msd.softwarePtrs.push_back(softwareReAdW);
    msd.softwarePtrs.push_back(softwareXcalibur);

    // dataProcessingList

    DataProcessingPtr dpXcalibur(new DataProcessing);
    dpXcalibur->id = "Xcalibur Processing";
    dpXcalibur->softwarePtr = softwareXcalibur;
    
    ProcessingMethod procXcal;
    procXcal.order = 1;
    procXcal.cvParams.push_back(CVParam(MS_deisotoping, false));
    procXcal.cvParams.push_back(CVParam(MS_charge_deconvolution, false));
    procXcal.cvParams.push_back(CVParam(MS_peak_picking, true));

    dpXcalibur->processingMethods.push_back(procXcal);

    DataProcessingPtr dpReAdW(new DataProcessing);
    dpReAdW->id = "ReAdW Conversion";
    dpReAdW->softwarePtr = softwareReAdW;

    ProcessingMethod procReAdW;
    procReAdW.order = 2;
    procReAdW.cvParams.push_back(MS_Conversion_to_mzML);

    dpReAdW->processingMethods.push_back(procReAdW);
 
    msd.dataProcessingPtrs.push_back(dpXcalibur);
    msd.dataProcessingPtrs.push_back(dpReAdW);

    // run

    msd.run.id = "Exp01";
    msd.run.instrumentPtr = instrumentPtr;
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

    s19.spectrumDescription.cvParams.push_back(MS_centroid_mass_spectrum);
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_lowest_m_z_value, 400.39));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_highest_m_z_value, 1795.56));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_m_z, 445.347));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_intensity, 120053));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));
    s19.spectrumDescription.scan.instrumentPtr = instrumentPtr;
    s19.spectrumDescription.scan.paramGroupPtrs.push_back(pg1);
    s19.spectrumDescription.scan.cvParams.push_back(CVParam(MS_scan_time, 5.890500, MS_minute));
    s19.spectrumDescription.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    s19.spectrumDescription.scan.cvParams.push_back(CVParam(MS_preset_scan_configuration, 3));
    s19.spectrumDescription.scan.selectionWindows.resize(1);
    SelectionWindow& window = s19.spectrumDescription.scan.selectionWindows.front();
    window.cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, 400.000000));
    window.cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, 1800.000000));

    BinaryDataArrayPtr s19_mz(new BinaryDataArray);
    s19_mz->dataProcessingPtr = dpXcalibur;
    s19_mz->cvParams.push_back(MS_m_z_array);
    s19_mz->data.resize(15);
    for (int i=0; i<15; i++)
        s19_mz->data[i] = i;

    BinaryDataArrayPtr s19_intensity(new BinaryDataArray);
    s19_intensity->dataProcessingPtr = dpXcalibur;
    s19_intensity->cvParams.push_back(MS_intensity_array);
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

    s20.spectrumDescription.cvParams.push_back(MS_centroid_mass_spectrum);
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_lowest_m_z_value, 320.39));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_highest_m_z_value, 1003.56));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_m_z, 456.347));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_intensity, 23433));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));

    s20.spectrumDescription.precursors.resize(1);
    Precursor& precursor = s20.spectrumDescription.precursors.front();
    precursor.spectrumID= s19.id;
    precursor.ionSelection.cvParams.push_back(CVParam(MS_m_z, 445.34));
    precursor.ionSelection.cvParams.push_back(CVParam(MS_intensity, 120053));
    precursor.ionSelection.cvParams.push_back(CVParam(MS_charge_state, 2));
    precursor.activation.cvParams.push_back(MS_collision_induced_dissociation);
    precursor.activation.cvParams.push_back(CVParam(MS_collision_energy, 35.00, MS_electron_volt));

    s20.spectrumDescription.scan.instrumentPtr = instrumentPtr;
    s20.spectrumDescription.scan.paramGroupPtrs.push_back(pg2);
    s20.spectrumDescription.scan.cvParams.push_back(CVParam(MS_scan_time, 5.990500, MS_minute));
    s20.spectrumDescription.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]"));
    s20.spectrumDescription.scan.cvParams.push_back(CVParam(MS_preset_scan_configuration, 4));
    s20.spectrumDescription.scan.selectionWindows.resize(1);
    SelectionWindow& window2 = s20.spectrumDescription.scan.selectionWindows.front();
    window2.cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, 110.000000));
    window2.cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, 905.000000));

    BinaryDataArrayPtr s20_mz(new BinaryDataArray);
    s20_mz->dataProcessingPtr = dpXcalibur;
    s20_mz->cvParams.push_back(MS_m_z_array);
    s20_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s20_mz->data[i] = i*2;

    BinaryDataArrayPtr s20_intensity(new BinaryDataArray);
    s20_intensity->dataProcessingPtr = dpXcalibur;
    s20_intensity->cvParams.push_back(MS_intensity_array);
    s20_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s20_intensity->data[i] = (10-i)*2;

    s20.binaryDataArrayPtrs.push_back(s20_mz);
    s20.binaryDataArrayPtrs.push_back(s20_intensity);
    s20.defaultArrayLength = s20_mz->data.size();
}


} // namespace examples
} // namespace msdata
} // namespace pwiz


