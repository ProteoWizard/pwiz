//
// examples.cpp 
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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
namespace mziddata {
namespace examples {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


const char* dbsequenceList[] = {
    "QQRLGNQWAVGHLM"
};

const char* peptideList[] = {
    "Bombessin B-4272 pGlu-Gln-Arg-Leu-Gly-Asn-Gln-Trp-Ala-Val-Gly-His-Leu-Met-NH2 (C71 H110 N24 O18 S1) ?(C71 H111 N23 O20 S1) H+ Adducts: 1619.8223, 810.4148, 540.6123, 405.7110"
};

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    mzid.id="";
    // Look up Matt's recent commits for boost date class
    mzid.creationDate = "2009-06-23T11:04:10";
    
    mzid.cvs = defaultCVList();

    AnalysisSoftwarePtr analysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id="AS_mascot_server";
    analysisSoftwarePtr->name="Mascot Server";
    analysisSoftwarePtr->version="2.2.101";
    analysisSoftwarePtr->URI="http://www.matrixscience.com/search_form_select.html";
    analysisSoftwarePtr->contactRole.Contact_ref="ORG_MSL";
    analysisSoftwarePtr->contactRole.role.set(MS_software_vendor);

    analysisSoftwarePtr->softwareName.set(MS_Mascot);
    analysisSoftwarePtr->customizations ="No customizations";
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    mzid.provider.id="PROVIDER";
    mzid.provider.contactRole.Contact_ref="PERSON_DOC_OWNER";
    mzid.provider.contactRole.role.set(MS_researcher);

    PersonPtr person(new Person());
    Affiliations aff;
    aff.organization_ref="ORG_MSL";
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    person = PersonPtr(new Person());
    person->id="PERSON_DOC_OWNER";
    person->firstName="";
    person->lastName="David Creasy";
    person->email="dcreasy@matrixscience.com";
    aff.organization_ref="ORG_DOC_OWNER";
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    OrganizationPtr organization(new Organization());
    organization->id="ORG_MSL";
    organization->name="Matrix Science Limited";
    organization->address="64 Baker Street, London W1U 7GB, UK";
    organization->email="support@matrixscience.com";
    organization->fax="+44 (0)20 7224 1344";
    organization->phone="+44 (0)20 7486 1050";
    mzid.auditCollection.push_back(organization);

    organization=OrganizationPtr(new Organization());
    organization->id="ORG_DOC_OWNER";
    
    SamplePtr sample(new Sample());
    mzid.analysisSampleCollection.samples.push_back(sample);
    
    DBSequencePtr dbSequence(new DBSequence());
    dbSequence->id="DBSeq_Bombessin";
    dbSequence->length="14";
    dbSequence->SearchDatabase_ref="SDB_5peptideMix";
    dbSequence->accession="Bombessin";
    dbSequence->seq=dbsequenceList[0];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[0]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    PeptidePtr peptide(new Peptide());
    peptide->id="peptide_1_1";
    peptide->peptideSequence="QLYENKPRRPYIL";
    peptide->modification.location="0";
    peptide->modification.monoisotopicMassDelta="-17.026549";
    // set UNIMOD:28 & value w/ peptide->modification.paramGroup.set();
    mzid.sequenceCollection.peptides.push_back(peptide);

    peptide=PeptidePtr(new Peptide());
    peptide->id="peptide_11_1";
    peptide->peptideSequence="RPKPQQFFGLM";
    mzid.sequenceCollection.peptides.push_back(peptide);
    
    peptide=PeptidePtr(new Peptide());    
    peptide->id="peptide_13_1";
    peptide->peptideSequence="RPKPQQFFGLM";
    mzid.sequenceCollection.peptides.push_back(peptide);
    
    SpectrumIdentificationPtr spectrumIdentificationPtr(
        new SpectrumIdentification());
    spectrumIdentificationPtr->id="SI";
    spectrumIdentificationPtr->SpectrumIdentificationProtocol_ref="SIP";
    spectrumIdentificationPtr->SpectrumIdentificationList_ref="SIL_1";
    spectrumIdentificationPtr->activityDate="2009-05-21T17:01:53";
    spectrumIdentificationPtr->inputSpectra.push_back("SD_1");;
    spectrumIdentificationPtr->searchDatabase.push_back("SDB_5peptideMix");
    mzid.analysisCollection.spectrumIdentification.push_back(spectrumIdentificationPtr);

    mzid.analysisCollection.proteinDetection.id="PD_1";
    mzid.analysisCollection.proteinDetection.ProteinDetectionProtocol_ref="PDP_MascotParser_1";
    mzid.analysisCollection.proteinDetection.ProteinDetectionList_ref="PDL_1";
    mzid.analysisCollection.proteinDetection.activityDate="2009-06-30T15:36:35";
    mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back("SIL_1");

    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol());
    sip->id="SIP";
    sip->AnalysisSoftware_ref="AS_mascot_server";
    sip->searchType.set(MS_ms_ms_search);
    sip->additionalSearchParams.set(MS_parent_mass_type_mono);
    sip->additionalSearchParams.set(MS_param__a_ion);
    sip->additionalSearchParams.set(MS_param__a_ion_NH3);
    sip->additionalSearchParams.set(MS_param__b_ion);
    sip->additionalSearchParams.set(MS_param__b_ion_NH3);
    sip->additionalSearchParams.set(MS_param__y_ion);
    sip->additionalSearchParams.set(MS_param__y_ion_NH3);
    SearchModificationPtr smp(new SearchModification());
    smp->modParam.massDelta="-17.026549";
    smp->modParam.residues="Q";
    // TODO add UNIMOD:28
    // Use ParamContainer in place of vector<CVParam>
    smp->specificityRules.push_back(CVParam(MS_modification_specificity_N_term, string("")));
    sip->modificationParams.push_back(smp);

    EnzymePtr ep(new Enzyme());
    ep->id="ENZ_0";
    ep->cTermGain="OH";
    ep->nTermGain="H";
    ep->missedCleavages="1";
    ep->semiSpecific="0";
    ep->siteRegexp="(?<=[KR])(?!P)";
    ep->enzymeName.set(MS_Trypsin);
    sip->enzymes.enzymes.push_back(ep);

    sip->massTable.id="MT";
    sip->massTable.msLevel="1 2";

    ResiduePtr rp(new Residue());
    rp->Code="A"; rp->Mass="71.037114";
    sip->massTable.residues.push_back(rp);

    AmbiguousResiduePtr arp(new AmbiguousResidue());
    arp->Code="B";
    arp->params.set(MS_alternate_single_letter_codes);
    sip->massTable.ambiguousResidue.push_back(arp);

    sip->fragmentTolerance.set(MS_search_tolerance_plus_value);
    sip->fragmentTolerance.set(MS_search_tolerance_minus_value);

    sip->parentTolerance.set(MS_search_tolerance_plus_value);
    sip->parentTolerance.set(MS_search_tolerance_minus_value);

    sip->threshold.set(MS_mascot_SigThreshold);
    
    FilterPtr fp(new Filter());
    fp->filterType.set(MS_DB_filter_taxonomy_OBSOLETE);
    sip->databaseFilters.push_back(fp);

    mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);

    ProteinDetectionProtocolPtr pdp(new ProteinDetectionProtocol());
    pdp->id="PDP_MascotParser_1";
    pdp->AnalysisSoftware_ref="AS_mascot_parser";
    pdp->analysisParams.set(MS_mascot_SigThreshold);
    pdp->analysisParams.set(MS_mascot_MaxProteinHits);
    pdp->analysisParams.set(MS_mascot_ProteinScoringMethod);
    pdp->analysisParams.set(MS_mascot_MinMSMSThreshold);
    pdp->analysisParams.set(MS_mascot_ShowHomologousProteinsWithSamePeptides);
    pdp->analysisParams.set(MS_mascot_ShowHomologousProteinsWithSubsetOfPeptides);
    pdp->analysisParams.set(MS_mascot_RequireBoldRed);
    pdp->analysisParams.set(MS_mascot_UseUnigeneClustering);
    pdp->analysisParams.set(MS_mascot_IncludeErrorTolerantMatches);
    pdp->analysisParams.set(MS_mascot_ShowDecoyMatches);
    pdp->threshold.set(MS_mascot_SigThreshold);
    mzid.analysisProtocolCollection.proteinDetectionProtocol.push_back(pdp);


    // Fill in mzid.dataCollection.inputs;
    // Add SourceFilePtr
    SourceFilePtr sourceFile(new SourceFile());
    sourceFile->id="SF_1";
    sourceFile->location="file:///../data/Mascot_mzml_example.dat";
    sourceFile->fileFormat.set(MS_Mascot_DAT_file);
    mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);

    // Add SearchDatabasePtr
    SearchDatabasePtr searchDb(new SearchDatabase());
    //searchDb->location="file:///c:/inetpub/mascot/sequence/5peptideMix/current/5peptideMix_20090515.fasta";
    searchDb->id="SDB_5peptideMix";
    searchDb->name="5peptideMix";
    searchDb->numDatabaseSequences=5;
    searchDb->numResidues=52;
    searchDb->releaseDate="5peptideMix_20090515.fasta";
    searchDb->version="5peptideMix_20090515.fasta";
    searchDb->fileFormat.set(MS_FASTA_format);
    mzid.dataCollection.inputs.searchDatabase.push_back(searchDb);

    // Add SpectraDataPtr
    SpectraDataPtr spectraData(new SpectraData());
    spectraData->id="SD_1";
    spectraData->location="file:///small.pwiz.1.1.mzML";
    spectraData->fileFormat.set(MS_mzML_file);
    mzid.dataCollection.inputs.spectraData.push_back(spectraData);
    
    // Fill in mzid.analysisData
    // Add SpectrumIdentificationListPtr
    SpectrumIdentificationListPtr silp(new SpectrumIdentificationList());
    silp->id="SIL_1";
    silp->numSequencesSearched=5;
    
    MeasurePtr measure(new Measure());
    measure->id="m_mz";
    measure->paramGroup.set(MS_product_ion_m_z);
    silp->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure());
    measure->id="m_intensity";
    measure->paramGroup.set(MS_product_ion_intensity);
    silp->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure());
    measure->id="m_error";
    measure->paramGroup.set(MS_product_ion_m_z_error);
    silp->fragmentationTable.push_back(measure);

    SpectrumIdentificationResultPtr sirp(new SpectrumIdentificationResult());
    sirp->id="SIR_1";
    sirp->spectrumID="controllerType=0 controllerNumber=1 scan=33" ;
    sirp->SpectraData_ref="SD_1";
    SpectrumIdentificationItemPtr siip(new SpectrumIdentificationItem());
    siip->id="SII_1_1";
    siip->calculatedMassToCharge=557.303212333333;
    siip->chargeState=3;
    siip->experimentalMassToCharge=558.75;
    siip->Peptide_ref="peptide_1_1";
    siip->rank=1;
    siip->passThreshold=true;
    siip->paramGroup.set(MS_mascot_score, "15.71");
    siip->paramGroup.set(MS_mascot_expectation_value, "0.0268534444565851");
    PeptideEvidencePtr pep(new PeptideEvidence());
    pep->id="PE_1_1_Neurotensin";
    pep->start=1;
    pep->end=13;
    pep->pre="-";
    pep->post="-" ;
    pep->missedCleavages=1;
    pep->frame=0;
    pep->isDecoy=false;
    pep->DBSequence_ref="DBSeq_Neurotensin";
    siip->peptideEvidence.push_back(pep);
    siip->paramGroup.set(MS_mascot_score);
    siip->paramGroup.set(MS_mascot_expectation_value);

    IonTypePtr ionType(new IonType());
    ionType->setIndex("2 3 4 5 6 7").charge=1;
    ionType->paramGroup.set(MS_frag__a_ion);
    siip->fragmentation.push_back(ionType);
    FragmentArrayPtr fap(new FragmentArray());
    fap->setValues("197.055771 360.124878 489.167847 603.244324 731.075562 828.637207 " );
    fap->Measure_ref="m_mz";
    ionType->fragmentArray.push_back(fap);
    sirp->spectrumIdentificationItem.push_back(siip);
    
    silp->spectrumIdentificationResult.push_back(sirp);
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silp);

    // Fill in proteinDetectionList
    ProteinDetectionList& pdl = mzid.dataCollection.analysisData.proteinDetectionList;
    pdl.id="PDL_1";
    ProteinAmbiguityGroupPtr pagp(new ProteinAmbiguityGroup());
    pagp->id="PAG_hit_1";
    ProteinDetectionHypothesisPtr pdhp(new ProteinDetectionHypothesis());
    pdhp->id="PDH_Bombessin";
    pdhp->DBSequence_ref="DBSeq_Bombessin";
    pdhp->passThreshold=true;
    pdhp->peptideHypothesis.push_back("PE_19_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_20_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_21_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_22_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_23_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_24_1_Bombessin");
    pdhp->peptideHypothesis.push_back("PE_25_1_Bombessin");
    pdhp->paramGroup.set(MS_mascot_score);
    pdhp->paramGroup.set(MS_coverage);
    pdhp->paramGroup.set(MS_distinct_peptide_sequences);
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdhp = ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis());
    pdhp->id="PDH_HSP71_RAT";
    pdhp->DBSequence_ref="DBSeq_HSP71_RAT";
    pdhp->passThreshold="false";
    pdhp->peptideHypothesis.push_back("PE_2_1_HSP71_RAT");
    pdhp->paramGroup.set(MS_mascot_score, 40.95);
    pdhp->paramGroup.set(MS_coverage, 2);
    pdhp->paramGroup.set(MS_distinct_peptide_sequences, 1);
    pdhp->paramGroup.set(MS_manual_validation);
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdl.proteinAmbiguityGroup.push_back(pagp);
}

PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> result;
    result.resize(3);

    result[0] = cv("MS");
    result[2] = cv("UO");

    CV cv;
    cv.id = "UNIMOD";
    cv.fullName = "UNIMOD" ;
    cv.URI = "http://www.unimod.org/xml/unimod.xml";
    result[1] = cv;
    
    return result;
}

} // namespace pwiz
} // namespace mziddata
} // namespace examples 
