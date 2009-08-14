//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
namespace tradata {
namespace examples {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


PWIZ_API_DECL void initializeTiny(TraData& td)
{
    //td.id = "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz";
    td.version = "1.0";
    td.cvs = msdata::defaultCVList();


    InstrumentPtr instrumentPtr(new Instrument);
    instrumentPtr->id = "LCQDeca";
    instrumentPtr->set(MS_LCQ_Deca);
    instrumentPtr->set(MS_instrument_serial_number,"23433");
    td.instrumentPtrs.push_back(instrumentPtr);


    SoftwarePtr softwarePtr(new Software);
    softwarePtr->id = "Xcalibur";
    softwarePtr->set(MS_Xcalibur);
    softwarePtr->version = "2.0.5";

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->set(MS_Bioworks);
    softwareBioworks->version = "3.3.1 sp1";
     
    SoftwarePtr softwarepwiz(new Software);
    softwarepwiz->id = "pwiz";
    softwarepwiz->set(MS_pwiz);
    softwarepwiz->version = "1.0";

    td.softwarePtrs.push_back(softwareBioworks);
    td.softwarePtrs.push_back(softwarepwiz);
    td.softwarePtrs.push_back(softwarePtr);


    ProteinPtr proteinPtr(new Protein);
    proteinPtr->accession = "123";
    proteinPtr->id = "Pro1";
    proteinPtr->name = "Q123";
    proteinPtr->sequence = "ABCD";
    proteinPtr->description = "A short protein.";
    proteinPtr->comment = "Chew on this!";
    proteinPtr->set(UO_dalton, 12345);
    td.proteinPtrs.push_back(proteinPtr);


    Publication publication;
    publication.id = "Al et al";
    publication.userParams.push_back(UserParam("PUBMED ID", "123456"));
    td.publications.push_back(publication);


    PeptidePtr peptide1Ptr(new Peptide);
    peptide1Ptr->id = "Pep1";
    peptide1Ptr->groupLabel = "Pro1";
    peptide1Ptr->unmodifiedSequence = "AB";
    peptide1Ptr->modifiedSequence = "AB[123]";
    peptide1Ptr->set(UO_dalton, 123);
    peptide1Ptr->proteinPtr = proteinPtr;
    peptide1Ptr->retentionTime.localRetentionTime = 123;
    peptide1Ptr->retentionTime.normalizationStandard = "HPINS";
    peptide1Ptr->retentionTime.normalizedRetentionTime = 125;
    peptide1Ptr->retentionTime.predictedRetentionTime = 120;
    peptide1Ptr->retentionTime.predictedRetentionTimeSoftwarePtr = softwareBioworks;
    td.peptidePtrs.push_back(peptide1Ptr);


    CompoundPtr compound1Ptr(new Compound);
    compound1Ptr->id = "Cmp1";
    compound1Ptr->retentionTime.localRetentionTime = 23;
    compound1Ptr->retentionTime.normalizationStandard = "PINS";
    compound1Ptr->retentionTime.normalizedRetentionTime = 25;
    compound1Ptr->retentionTime.predictedRetentionTime = 20;
    compound1Ptr->retentionTime.predictedRetentionTimeSoftwarePtr = softwarepwiz;
    td.compoundPtrs.push_back(compound1Ptr);


    Transition transition1;
    transition1.name = "Pep1T1";
    transition1.precursor.mz = 456.78;
    transition1.precursor.charge = 2;
    transition1.product.mz = 678.90;
    transition1.product.charge = 1;
    transition1.peptidePtr = peptide1Ptr;

    Transition transition2;
    transition2.name = "Cmp1T1";
    transition2.precursor.mz = 456.78;
    transition2.precursor.charge = 2;
    transition2.product.mz = 678.90;
    transition2.product.charge = 1;
    transition2.compoundPtr = compound1Ptr;

    td.transitions.push_back(transition1);
    td.transitions.push_back(transition2);

} // initializeTiny()


PWIZ_API_DECL void addMIAPEExampleMetadata(TraData& td)
{
    //td.id = "urn:lsid:psidev.info:mzML.instanceDocuments.small_miape.pwiz"; //TODO: schema xs:ID -> LSID
    /*td.id = "small_miape_pwiz";
    td.version = "1.0";

    td.cvs = defaultCVList(); // TODO: move this to Reader_Thermo

    FileContent& fc = td.fileDescription.fileContent;
    fc.userParams.push_back(UserParam("ProteoWizard", "Thermo RAW data converted to mzML, with additional MIAPE parameters added for illustration"));

    // fileDescription

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file:///C:/example/"));
    sfp_parameters->set(MS_parameter_file);
    sfp_parameters->set(MS_SHA_1, "unknown");
    sfp_parameters->set(MS_no_nativeID_format);
    td.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    Contact contact;
    contact.set(MS_contact_name, "William Pennington");
    contact.set(MS_contact_organization, "Higglesworth University");
    contact.set(MS_contact_address, "12 Higglesworth Avenue, 12045, HI, USA");
	contact.set(MS_contact_URL, "http://www.higglesworth.edu/");
	contact.set(MS_contact_email, "wpennington@higglesworth.edu");
    td.fileDescription.contacts.push_back(contact);

    // paramGroupList

    ParamGroupPtr pgInstrumentCustomization(new ParamGroup);
    pgInstrumentCustomization->id = "InstrumentCustomization";
    pgInstrumentCustomization->set(MS_customization ,"none");
    td.paramGroupPtrs.push_back(pgInstrumentCustomization);

    ParamGroupPtr pgActivation(new ParamGroup);
    pgActivation->id = "CommonActivationParams";
    pgActivation->set(MS_collision_induced_dissociation);
    pgActivation->set(MS_collision_energy, 35.00, UO_electronvolt);
    pgActivation->set(MS_collision_gas, "nitrogen"); 
    td.paramGroupPtrs.push_back(pgActivation);

    // sampleList

    SamplePtr sample1(new Sample);
    sample1->id = "sample1";
    sample1->name = "Sample 1";
    td.samplePtrs.push_back(sample1);

    SamplePtr sample2(new Sample);
    sample2->id = "sample2";
    sample2->name = "Sample 2";
    td.samplePtrs.push_back(sample2);

    // instrumentConfigurationList

    for (vector<InstrumentConfigurationPtr>::const_iterator it=td.instrumentConfigurationPtrs.begin(),
         end=td.instrumentConfigurationPtrs.end(); it!=end; ++it)
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
    procMIAPE.softwarePtr = td.softwarePtrs.back();
    procMIAPE.set(MS_deisotoping);
    procMIAPE.set(MS_charge_deconvolution);
    procMIAPE.set(MS_peak_picking);
    procMIAPE.set(MS_smoothing);
    procMIAPE.set(MS_baseline_reduction);
    procMIAPE.userParams.push_back(UserParam("signal-to-noise estimation", "none"));
    procMIAPE.userParams.push_back(UserParam("centroiding algorithm", "none"));
    procMIAPE.userParams.push_back(UserParam("charge states calculated", "none"));

    DataProcessingPtr dpMIAPE(new DataProcessing);
    td.dataProcessingPtrs.push_back(dpMIAPE);
    dpMIAPE->id = "MIAPE_example";
    dpMIAPE->processingMethods.push_back(procMIAPE);

    // acquisition settings
    
    ScanSettingsPtr as1(new ScanSettings("acquisition_settings_MIAPE_example"));
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
    td.scanSettingsPtrs.push_back(as1);

    // run
    
    td.run.samplePtr = sample1;*/

} // addMIAPEExampleMetadata()


} // namespace examples
} // namespace tdata
} // namespace pwiz


