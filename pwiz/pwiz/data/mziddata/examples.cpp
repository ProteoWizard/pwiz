//
// $Id$ 
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

#include "References.hpp"
#include "examples.hpp"
#include "Version.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace mziddata {
namespace examples {




const char* dbsequenceList[] = {
    "QQRLGNQWAVGHLM",
    "QLYENKPRRPYIL",
    "MAKKTAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVVNDGDKPKVQVNYKGENRSFYPEEISSMVLTKMKEIAEAYLGHPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVSHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRGTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQTFTTYSDNQPGVLIQVYEGERAMTRDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAERYKAEDEVQRERVAAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDSNTLAEKEEFVHKREELERVCNPIISGLYQGAGAPGAGGFGAQAPKGGSGSGPTIEEVD"
};

const char* peptideList[] = {
    "Bombessin B-4272 pGlu-Gln-Arg-Leu-Gly-Asn-Gln-Trp-Ala-Val-Gly-His-Leu-Met-NH2 (C71 H110 N24 O18 S1) ?(C71 H111 N23 O20 S1) H+ Adducts: 1619.8223, 810.4148, 540.6123, 405.7110",
    "Neurotensin: N-6383 pGlu-Leu-Tyr-Glu-Asn-Lys-Pro-Arg-Arg-Pro-Tyr-Ile-Leu  (C78 H121 N21 O20) H+ Adducts: 1672.9170, 836.9621, 558.3105, 418.9847, 335.3892"
};

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    mzid.id="";
    // Look up Matt's recent commits for boost date class
    mzid.creationDate = "2009-06-23T11:04:10";
    
    mzid.cvs = defaultCVList();

    ContactPtr contactPwiz(new Organization("ORG_PWIZ", "ProteoWizard"));
    contactPwiz->email = "support@proteowizard.org";
    mzid.auditCollection.push_back(contactPwiz);

    AnalysisSoftwarePtr analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id = "pwiz_" + Version::str();
    analysisSoftwarePtr->name = "ProteoWizard MzIdentML";
    analysisSoftwarePtr->softwareName.set(MS_pwiz);
    analysisSoftwarePtr->version = Version::str();
    analysisSoftwarePtr->contactRolePtr.reset(new ContactRole);
    analysisSoftwarePtr->contactRolePtr->role.set(MS_software_vendor);
    analysisSoftwarePtr->contactRolePtr->contactPtr = contactPwiz;
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id="AS_mascot_server";
    analysisSoftwarePtr->name="Mascot Server";
    analysisSoftwarePtr->version="2.2.101";
    analysisSoftwarePtr->URI="http://www.matrixscience.com/search_form_select.html";
    ContactRolePtr aspCont = ContactRolePtr(new ContactRole());
    aspCont->contactPtr=ContactPtr(new Contact("ORG_MSL"));
    aspCont->role.set(MS_software_vendor);
    analysisSoftwarePtr->contactRolePtr = aspCont;

    analysisSoftwarePtr->softwareName.set(MS_Mascot);
    analysisSoftwarePtr->customizations ="No customizations";
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id = "AS_mascot_parser";
    analysisSoftwarePtr->name = "Mascot Parser";
    analysisSoftwarePtr->softwareName.set(MS_Mascot_Parser);
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    mzid.provider.id="PROVIDER";
    mzid.provider.contactRole.contactPtr=ContactPtr(new Contact("PERSON_DOC_OWNER"));
    mzid.provider.contactRole.role.set(MS_researcher);

    PersonPtr person(new Person());
    Affiliations aff;
    aff.organizationPtr=OrganizationPtr(new Organization("ORG_MSL"));
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    person = PersonPtr(new Person());
    person->id="PERSON_DOC_OWNER";
    person->firstName="";
    person->lastName="David Creasy";
    person->email="dcreasy@matrixscience.com";
    aff.organizationPtr=OrganizationPtr(new Organization("ORG_DOC_OWNER"));
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
    mzid.auditCollection.push_back(organization);
    
    //SamplePtr sample(new Sample());
    //mzid.analysisSampleCollection.samples.push_back(sample);
    
    DBSequencePtr dbSequence(new DBSequence());
    dbSequence->id="DBSeq_Bombessin";
    dbSequence->length=14;
    dbSequence->searchDatabasePtr=SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix"));
    dbSequence->accession="Bombessin";
    dbSequence->seq=dbsequenceList[0];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[0]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    
    dbSequence = DBSequencePtr(new DBSequence());
    dbSequence->id="DBSeq_Neurotensin";
    dbSequence->length=13;
    dbSequence->searchDatabasePtr=SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix"));
    dbSequence->accession="Neurotensin";
    dbSequence->seq=dbsequenceList[1];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[1]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);
    
    dbSequence = DBSequencePtr(new DBSequence());
    dbSequence->id="DBSeq_HSP71_RAT";
    dbSequence->length=641;
    dbSequence->searchDatabasePtr=SearchDatabasePtr(new SearchDatabase("SDB_SwissProt"));
    dbSequence->accession="HSP71_RAT";
    dbSequence->seq=dbsequenceList[2];
    dbSequence->paramGroup.set(MS_protein_description,
                               "Heat shock 70 kDa protein 1A/1B (Heat "
                               "shock 70 kDa protein 1/2) (HSP70.1/2) - "
                               "Rattus norvegicus (Rat)");
    dbSequence->paramGroup.set(MS_taxonomy__scientific_name,
                               "Rattus norvegicus");
    dbSequence->paramGroup.set(MS_taxonomy__NCBI_TaxID,
                               "10116");
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);
    
    PeptidePtr peptide(new Peptide());
    peptide->id="peptide_1_1";
    peptide->peptideSequence="QLYENKPRRPYIL";
    ModificationPtr modification(new Modification());
    modification->location=0;
    modification->monoisotopicMassDelta=-17.026549;
    modification->paramGroup.set(UNIMOD_Gln__pyro_Glu);
    peptide->modification.push_back(modification);
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
    spectrumIdentificationPtr->spectrumIdentificationProtocolPtr=
        SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol("SIP"));
    spectrumIdentificationPtr->spectrumIdentificationListPtr=
        SpectrumIdentificationListPtr(new SpectrumIdentificationList("SIL_1"));
    spectrumIdentificationPtr->activityDate="2009-05-21T17:01:53";
    spectrumIdentificationPtr->inputSpectra.push_back(SpectraDataPtr(new SpectraData("SD_1")));;
    spectrumIdentificationPtr->searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix")));
    mzid.analysisCollection.spectrumIdentification.push_back(spectrumIdentificationPtr);

    mzid.analysisCollection.proteinDetection.id="PD_1";
    mzid.analysisCollection.proteinDetection.proteinDetectionProtocolPtr=ProteinDetectionProtocolPtr(new ProteinDetectionProtocol("PDP_MascotParser_1"));
    mzid.analysisCollection.proteinDetection.proteinDetectionListPtr=ProteinDetectionListPtr(new ProteinDetectionList("PDL_1"));
    mzid.analysisCollection.proteinDetection.activityDate="2009-06-30T15:36:35";

    SpectrumIdentificationListPtr silp(new SpectrumIdentificationList("SIL_1"));
    mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back(silp);

    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol());
    sip->id="SIP";
    sip->analysisSoftwarePtr=AnalysisSoftwarePtr(new AnalysisSoftware("AS_mascot_server"));
    sip->searchType.set(MS_ms_ms_search);
    sip->additionalSearchParams.set(MS_parent_mass_type_mono);
    sip->additionalSearchParams.set(MS_param__a_ion);
    sip->additionalSearchParams.set(MS_param__a_ion_NH3);
    sip->additionalSearchParams.set(MS_param__b_ion);
    sip->additionalSearchParams.set(MS_param__b_ion_NH3);
    sip->additionalSearchParams.set(MS_param__y_ion);
    sip->additionalSearchParams.set(MS_param__y_ion_NH3);
    SearchModificationPtr smp(new SearchModification());
    smp->modParam.massDelta=-17.026549;
    smp->modParam.residues="Q";
    smp->modParam.cvParams.set(UNIMOD_Gln__pyro_Glu);
    // TODO add UNIMOD:28
    // Use ParamContainer in place of vector<CVParam>
    smp->specificityRules.set(MS_modification_specificity_N_term, string(""));
    sip->modificationParams.push_back(smp);

    EnzymePtr ep(new Enzyme());
    ep->id="ENZ_0";
    ep->cTermGain="OH";
    ep->nTermGain="H";
    ep->missedCleavages=1;
    ep->semiSpecific=false;
    ep->siteRegexp="(?<=[KR])(?!P)";
    ep->enzymeName.set(MS_Trypsin);
    sip->enzymes.enzymes.push_back(ep);

    sip->massTable.id="MT";
    sip->massTable.msLevel="1 2";

    ResiduePtr rp(new Residue());
    rp->Code="A"; rp->Mass=71.037114;
    sip->massTable.residues.push_back(rp);

    AmbiguousResiduePtr arp(new AmbiguousResidue());
    arp->Code="B";
    arp->params.set(MS_alternate_single_letter_codes);
    sip->massTable.ambiguousResidue.push_back(arp);

    sip->fragmentTolerance.set(MS_search_tolerance_plus_value, "0.6", UO_dalton);
    sip->fragmentTolerance.set(MS_search_tolerance_minus_value, "0.6", UO_dalton);

    sip->parentTolerance.set(MS_search_tolerance_plus_value, "3", UO_dalton);
    sip->parentTolerance.set(MS_search_tolerance_minus_value, "3", UO_dalton);

    sip->threshold.set(MS_mascot_SigThreshold, "0.05");
    
    FilterPtr fp(new Filter());
    fp->filterType.set(MS_DB_filter_taxonomy);
    sip->databaseFilters.push_back(fp);

    mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);

    ProteinDetectionProtocolPtr pdp(new ProteinDetectionProtocol());
    pdp->id="PDP_MascotParser_1";
    pdp->analysisSoftwarePtr=AnalysisSoftwarePtr(new AnalysisSoftware("AS_mascot_parser"));
    pdp->analysisParams.set(MS_mascot_SigThreshold, "0.05");
    pdp->analysisParams.set(MS_mascot_MaxProteinHits, "Auto");
    pdp->analysisParams.set(MS_mascot_ProteinScoringMethod, "MudPIT");
    pdp->analysisParams.set(MS_mascot_MinMSMSThreshold, "0");
    pdp->analysisParams.set(MS_mascot_ShowHomologousProteinsWithSamePeptides, "1");
    pdp->analysisParams.set(MS_mascot_ShowHomologousProteinsWithSubsetOfPeptides, "1");
    pdp->analysisParams.set(MS_mascot_RequireBoldRed, "0");
    pdp->analysisParams.set(MS_mascot_UseUnigeneClustering, "false");
    pdp->analysisParams.set(MS_mascot_IncludeErrorTolerantMatches, "1");
    pdp->analysisParams.set(MS_mascot_ShowDecoyMatches, "0");
    pdp->threshold.set(MS_mascot_SigThreshold, "0.05", CVID_Unknown);
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
    searchDb->id="SDB_5peptideMix";
    searchDb->name="5peptideMix";
    searchDb->location="file:///c:/inetpub/mascot/sequence/5peptideMix/current/5peptideMix_20090515.fasta";
    searchDb->numDatabaseSequences=5;
    searchDb->numResidues=52;
    searchDb->releaseDate="5peptideMix_20090515.fasta";
    searchDb->version="5peptideMix_20090515.fasta";
    searchDb->fileFormat.set(MS_FASTA_format);
    searchDb->DatabaseName.userParams.push_back(UserParam("5peptideMix_20090515.fasta"));
    mzid.dataCollection.inputs.searchDatabase.push_back(searchDb);

    searchDb = SearchDatabasePtr(new SearchDatabase());
    searchDb->id="SDB_SwissProt";
    searchDb->name="SwissProt";
    searchDb->location="file:///C:/inetpub/mascot/sequence/SwissProt/current/SwissProt_51.6.fasta";
    searchDb->numDatabaseSequences=5;
    searchDb->numResidues=52;
    searchDb->releaseDate="SwissProt_51.6.fasta";
    searchDb->version="SwissProt_51.6.fasta";
    searchDb->fileFormat.set(MS_FASTA_format);
    searchDb->DatabaseName.userParams.push_back(UserParam("SwissProt_51.6.fasta"));
    searchDb->params.set(MS_database_type_amino_acid);
    mzid.dataCollection.inputs.searchDatabase.push_back(searchDb);

    // Add SpectraDataPtr
    SpectraDataPtr spectraData(new SpectraData());
    spectraData->id="SD_1";
    spectraData->location="file:///small.pwiz.1.1.mzML";
    spectraData->fileFormat.set(MS_mzML_file);
    spectraData->spectrumIDFormat.set(MS_multiple_peak_list_nativeID_format);
    mzid.dataCollection.inputs.spectraData.push_back(spectraData);
    
    // Fill in mzid.analysisData
    // Add SpectrumIdentificationListPtr
    silp = SpectrumIdentificationListPtr(new SpectrumIdentificationList());
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
    measure->paramGroup.set(MS_product_ion_m_z_error, "", MS_m_z);
    silp->fragmentationTable.push_back(measure);

    SpectrumIdentificationResultPtr sirp(new SpectrumIdentificationResult());
    sirp->id="SIR_1";
    sirp->spectrumID="controllerType=0 controllerNumber=1 scan=33" ;
    sirp->spectraDataPtr=SpectraDataPtr(new SpectraData("SD_1"));
    SpectrumIdentificationItemPtr siip(new SpectrumIdentificationItem());
    siip->id="SII_1_1";
    siip->calculatedMassToCharge=557.303212333333;
    siip->chargeState=3;
    siip->experimentalMassToCharge=558.75;
    siip->peptidePtr=PeptidePtr(new Peptide("peptide_1_1"));
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
    pep->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_Neurotensin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id="PE_19_1_Bombessin_0";
    pep->start=1;
    pep->end=14;
    pep->pre="-";
    pep->post="-" ;
    pep->missedCleavages=1;
    pep->frame=0;
    pep->isDecoy=false;
    pep->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id="PE_20_1_Bombessin_0";
    pep->start=1;
    pep->end=14;
    pep->pre="-";
    pep->post="-" ;
    pep->missedCleavages=1;
    pep->frame=0;
    pep->isDecoy=false;
    pep->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id="PE_2_1_HSP71_RAT_0";
    pep->start=37;
    pep->end=49;
    pep->pre="R";
    pep->post="L" ;
    pep->missedCleavages=1;
    pep->frame=0;
    pep->isDecoy=false;
    pep->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_HSP71_RAT"));
    siip->peptideEvidence.push_back(pep);
    
    siip->paramGroup.set(MS_mascot_score, "15.71");
    siip->paramGroup.set(MS_mascot_expectation_value, 0.0268534444565851);

    IonTypePtr ionType(new IonType());
    ionType->setIndex("2 3 4 5 6 7").charge=1;
    ionType->paramGroup.set(MS_frag__a_ion);
    siip->fragmentation.push_back(ionType);
    FragmentArrayPtr fap(new FragmentArray());
    fap->setValues("197.055771 360.124878 489.167847 603.244324 731.075562 828.637207 " );
    fap->measurePtr=MeasurePtr(new Measure("m_mz"));
    ionType->fragmentArray.push_back(fap);
    sirp->spectrumIdentificationItem.push_back(siip);
    
    silp->spectrumIdentificationResult.push_back(sirp);
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silp);

    // Fill in proteinDetectionList
    ProteinDetectionListPtr pdl(new ProteinDetectionList());
    pdl->id="PDL_1";
    ProteinAmbiguityGroupPtr pagp(new ProteinAmbiguityGroup());
    pagp->id="PAG_hit_1";
    ProteinDetectionHypothesisPtr pdhp(new ProteinDetectionHypothesis());
    pdhp->id="PDH_Bombessin";
    pdhp->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    pdhp->passThreshold=true;
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_19_1_Bombessin_0")));
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_20_1_Bombessin_0")));
    pdhp->paramGroup.set(MS_mascot_score, "164.4");
    pdhp->paramGroup.set(MS_sequence_coverage, "100");
    pdhp->paramGroup.set(MS_distinct_peptide_sequences, "7");
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdhp = ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis());
    pdhp->id="PDH_HSP71_RAT";
    pdhp->dbSequencePtr=DBSequencePtr(new DBSequence("DBSeq_HSP71_RAT"));
    pdhp->passThreshold="false";
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_2_1_HSP71_RAT_0")));
    pdhp->paramGroup.set(MS_mascot_score, "40.95");
    pdhp->paramGroup.set(MS_sequence_coverage, "2");
    pdhp->paramGroup.set(MS_distinct_peptide_sequences, "1");
    pdhp->paramGroup.set(MS_manual_validation);
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdl->proteinAmbiguityGroup.push_back(pagp);
    mzid.dataCollection.analysisData.proteinDetectionListPtr = pdl;

    References::resolve(mzid); 
}

} // namespace pwiz
} // namespace mziddata
} // namespace examples 
