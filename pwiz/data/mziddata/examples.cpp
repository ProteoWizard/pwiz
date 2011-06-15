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

#include "pwiz/utility/misc/Std.hpp"
#include "References.hpp"
#include "examples.hpp"
#include "Version.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
//#include <boost/random.hpp>


using namespace pwiz::proteome;
using namespace pwiz::chemistry;
using namespace pwiz::util;


namespace pwiz {
namespace mziddata {
namespace examples {


/* Some fun code for generating random examples
namespace {
        
    boost::mt19937 rng;
    boost::uniform_int<> oneToFive(1, 5);
    boost::variate_generator<boost::mt19937&, boost::uniform_int<> > roll(rng, oneToFive);

    Formula acetylation("C2H2O1");
    Formula oxidation("O1");
    Formula carbamylation("C1H1N1O1");
    Formula phosphorylation("P1H1O3");
    Formula cam("C2H3N1O1");

    Digestion digestion(dbSequence->seq, Digestion::Config(1, 10, 25, Digestion::SemiSpecific));
    BOOST_FOREACH(const DigestedPeptide& digestedPeptide, digestion)
    {
        // 20% of semispecific peptides identified
        if (roll() > 1)
            continue;

        PeptidePtr peptide(new Peptide);
        peptide->id = "PEP_" + lexical_cast<string>(mzid.sequenceCollection.peptides.size() + 1);
        peptide->peptideSequence = digestedPeptide.sequence();

        // 20% of N-termini acetylated
        if (roll() == 1)
        {
            ModificationPtr mod(new Modification);
            mod->location = 0;
            mod->avgMassDelta = acetylation.molecularWeight();
            mod->monoisotopicMassDelta = acetylation.monoisotopicMass();
        }

        // 20% of C-termini carbamylated
        if (roll() == 1)
        {
            ModificationPtr mod(new Modification);
            mod->location = peptide->peptideSequence.length()+1;
            mod->avgMassDelta = carbamylation.molecularWeight();
            mod->monoisotopicMassDelta = carbamylation.monoisotopicMass();
        }

        for (size i=0; i < peptide->peptideSequence.length(); ++i)
        {
            char aa = peptide->peptideSequence[i];
            Formula* modFormula = NULL;

            // all cysteines modified
            if (aa == 'C')
                modFormula = &cam;
            // 40% of methionines oxidized
            else if (aa == 'M' && roll() < 3)
                modFormula = &oxidiation;
            // 20% of serine/threonine phosphorylated
            else if ((aa == 'S' || aa == 'T') && roll() < 2)
                modFormula = &phosphorylation;

            if (modFormula == NULL)
                continue;
            
            ModificationPtr mod(new Modification);
            mod->location = i+1;
            mod->avgMassDelta = modFormula->molecularWeight();
            mod->monoisotopicMassDelta = modFormula->monoisotopicMass();
        }

        mzid.sequenceCollection.peptides.push_back(peptide);

        // add 1 to 5 spectra for this peptide
        int numSpectra = roll();
    }
}*/ // namespace


struct ExampleDBSequence
{
    const char* accession;
    const char* sequence;
    const char* description;
};

const ExampleDBSequence bombessin = {"Bombessin B-4272", "QQRLGNQWAVGHLM", "pGlu-Gln-Arg-Leu-Gly-Asn-Gln-Trp-Ala-Val-Gly-His-Leu-Met-NH2 (C71 H110 N24 O18 S1) ?(C71 H111 N23 O20 S1) H+ Adducts: 1619.8223, 810.4148, 540.6123, 405.7110"};
const ExampleDBSequence neurotensin = {"Neurotensin N-6383", "QLYENKPRRPYIL", "pGlu-Leu-Tyr-Glu-Asn-Lys-Pro-Arg-Arg-Pro-Tyr-Ile-Leu  (C78 H121 N21 O20) H+ Adducts: 1672.9170, 836.9621, 558.3105, 418.9847, 335.3892"};
const ExampleDBSequence HSP71_RAT = {"HSP71_RAT",     "MAKKTAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVVNDGDKPKVQVNYKGENRSFYPEEISSMVLTKMKEIAEAYLGHPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVSHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRGTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQTFTTYSDNQPGVLIQVYEGERAMTRDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAERYKAEDEVQRERVAAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDSNTLAEKEEFVHKREELERVCNPIISGLYQGAGAPGAGGFGAQAPKGGSGSGPTIEEVD", "Heat shock 70 kDa protein 1A/1B (Heat shock 70 kDa protein 1/2) (HSP70.1/2) - Rattus norvegicus (Rat)"};
const ExampleDBSequence HSP71_HUMAN = {"HSP71_HUMAN", "MAKAAAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVINDGDKPKVQVSYKGETKAFYPEEISSMVLTKMKEIAEAYLGYPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVNHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRSTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQIFTTYSDNQPGVLIQVYEGERAMTKDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAEKYKAEDEVQRERVSAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDANTLAEKDEFEHKRKELEQVCNPIISGLYQGAGGPGPGGFGAQGPKGGSGSGPTIEEVD", "Heat shock 70 kDa protein 1A/1B OS=Homo sapiens GN=HSPA1A PE=1 SV=5"};


PWIZ_API_DECL void initializeBasicSpectrumIdentification(MzIdentML& mzid)
{
    mzid.creationDate = "2009-06-23T11:04:10";
    mzid.cvs = defaultCVList();

    AnalysisSoftwarePtr as(new AnalysisSoftware);
    as->id = "AS_ProteoWizard_" + Version::str();
    as->name = "ProteoWizard";
    as->softwareName.set(MS_pwiz);
    as->version = Version::str();
    mzid.analysisSoftwareList.push_back(as);

    OrganizationPtr pwizOrg(new Organization("ORG_PWIZ", "ProteoWizard"));
    pwizOrg->set(MS_contact_email, "support@proteowizard.org");
    mzid.auditCollection.push_back(pwizOrg);

    as->contactRolePtr.reset(new ContactRole);
    as->contactRolePtr->cvid = MS_software_vendor;
    as->contactRolePtr->contactPtr = pwizOrg;

    as.reset(new AnalysisSoftware("AS_Mascot_2.2.101", "Mascot"));
    as->version = "2.2.101";
    as->softwareName.set(MS_Mascot);
    as->URI = "http://www.matrixscience.com/search_form_select.html";
    mzid.analysisSoftwareList.push_back(as);

    OrganizationPtr mslOrg(new Organization("ORG_MSL", "Matrix Science Limited"));
    mslOrg->set(MS_contact_address, "64 Baker Street, London W1U 7GB, UK");
    mslOrg->set(MS_contact_email, "support@matrixscience.com");
    mslOrg->set(MS_contact_fax_number, "+44 (0)20 7224 1344");
    mslOrg->set(MS_contact_phone_number, "+44 (0)20 7486 1050");
    mzid.auditCollection.push_back(mslOrg);

    as->contactRolePtr.reset(new ContactRole);
    as->contactRolePtr->cvid = MS_software_vendor;
    as->contactRolePtr->contactPtr = mslOrg;

    OrganizationPtr ownerOrg(new Organization("ORG_DOC_OWNER", "Some Lab Owner"));
    mzid.auditCollection.push_back(ownerOrg);

    PersonPtr owner = PersonPtr(new Person("PERSON_DOC_OWNER"));
    owner->firstName = "Some";
    owner->lastName = "Person";
    owner->set(MS_contact_email, "somebody@somewhere.com");
    owner->affiliations.push_back(ownerOrg);
    mzid.auditCollection.push_back(owner);

    mzid.provider.id = "PROVIDER";
    mzid.provider.contactRole.contactPtr = owner;
    mzid.provider.contactRole.cvid = MS_researcher;

    SearchDatabasePtr sdb(new SearchDatabase);
    sdb->id = "SDB_SwissProt";
    sdb->name = "SwissProt";
    sdb->version = "SwissProt_51.6.fasta";
    sdb->releaseDate = "2012-01-02T01:02:03Z";
    sdb->location = "file:///C:/inetpub/Mascot/sequence/SwissProt/current/SwissProt_51.6.fasta";
    sdb->fileFormat.cvid = MS_FASTA_format;
    sdb->numDatabaseSequences = 5;
    sdb->numResidues = 52;
    sdb->set(MS_database_type_amino_acid);
    sdb->databaseName.userParams.push_back(UserParam("SwissProt"));
    mzid.dataCollection.inputs.searchDatabase.push_back(sdb);

    DBSequencePtr dbSequence(new DBSequence);
    dbSequence->accession = HSP71_RAT.accession;
    dbSequence->seq = HSP71_RAT.sequence;
    dbSequence->set(MS_protein_description, HSP71_RAT.description);
    dbSequence->id = "DBSeq_"  + dbSequence->accession;
    dbSequence->length = dbSequence->seq.length();
    dbSequence->searchDatabasePtr = sdb;
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    dbSequence.reset(new DBSequence);
    dbSequence->accession = HSP71_HUMAN.accession;
    dbSequence->seq = HSP71_HUMAN.sequence;
    dbSequence->set(MS_protein_description, HSP71_HUMAN.description);
    dbSequence->id = "DBSeq_"  + dbSequence->accession;
    dbSequence->length = dbSequence->seq.length();
    dbSequence->searchDatabasePtr = sdb;
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    Formula acetylation("C2H2O1");
    Formula amidation("H1N1O-1");
    Formula deamidation("H-1N-1O1");
    Formula pyroglutQ("H-3N-1");
    Formula carbamylation("C1H1N1O1");
    Formula oxidation("O1");
    Formula phosphorylation("P1H1O3");
    Formula cam("C2H3N1O1");
    
    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol);
    sip->id = "SIP";
    sip->analysisSoftwarePtr = as;
    sip->searchType.cvid = MS_ms_ms_search;
    sip->additionalSearchParams.set(MS_parent_mass_type_mono);
    sip->additionalSearchParams.set(MS_fragment_mass_type_mono);
    sip->additionalSearchParams.set(MS_param__a_ion);
    sip->additionalSearchParams.set(MS_param__a_ion_NH3);
    sip->additionalSearchParams.set(MS_param__b_ion);
    sip->additionalSearchParams.set(MS_param__b_ion_NH3);
    sip->additionalSearchParams.set(MS_param__y_ion);
    sip->additionalSearchParams.set(MS_param__y_ion_NH3);
    sip->additionalSearchParams.userParams.push_back(UserParam("INSTRUMENT", "Default"));
    sip->additionalSearchParams.userParams.push_back(UserParam("MASS", "Monoisotopic"));
    sip->additionalSearchParams.userParams.push_back(UserParam("PFA", "1"));
    sip->additionalSearchParams.userParams.push_back(UserParam("TOL", "1"));
    sip->additionalSearchParams.userParams.push_back(UserParam("TOLU", "ppm"));
    sip->additionalSearchParams.userParams.push_back(UserParam("ITOL", "0.6"));
    sip->additionalSearchParams.userParams.push_back(UserParam("ITOLU", "Da"));

    SearchModificationPtr sm(new SearchModification);
    sm->massDelta = acetylation.monoisotopicMass();
    sm->specificityRules.cvid = MS_modification_specificity_N_term;
    sm->set(UNIMOD_Acetyl);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = amidation.monoisotopicMass();
    sm->specificityRules.cvid = MS_modification_specificity_C_term;
    sm->set(UNIMOD_Amidated);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = deamidation.monoisotopicMass();
    sm->residues.push_back('N');
    sm->set(UNIMOD_Deamidated);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = pyroglutQ.monoisotopicMass();
    sm->specificityRules.cvid = MS_modification_specificity_N_term;
    sm->residues.push_back('Q');
    sm->set(UNIMOD_Gln__pyro_Glu);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = carbamylation.monoisotopicMass();
    sm->residues.push_back('K');
    sm->set(UNIMOD_Carbamyl);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = carbamylation.monoisotopicMass();
    sm->residues.push_back('R');
    sm->set(UNIMOD_Carbamyl);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = oxidation.monoisotopicMass();
    sm->residues.push_back('M');
    sm->set(UNIMOD_Oxidation);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = phosphorylation.monoisotopicMass();
    sm->residues.push_back('S');
    sm->set(UNIMOD_Phospho);
    sm->set(UNIMOD_Sulfo);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = phosphorylation.monoisotopicMass();
    sm->residues.push_back('T');
    sm->set(UNIMOD_Phospho);
    sm->set(UNIMOD_Sulfo);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = cam.monoisotopicMass();
    sm->residues.push_back('C');
    sm->fixedMod = true;
    sm->set(UNIMOD_Carbamidomethyl);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->massDelta = 311;
    sm->residues.push_back('H');
    sm->fixedMod = true;
    sm->set(MS_unknown_modification);
    sip->modificationParams.push_back(sm);

    EnzymePtr enzyme(new Enzyme);
    enzyme->id = "ENZ_1";
    enzyme->cTermGain = "OH";
    enzyme->nTermGain = "H";
    enzyme->missedCleavages = 1;
    enzyme->minDistance = 1;
    enzyme->semiSpecific = false;
    enzyme->siteRegexp = "(?<=[KR])";
    enzyme->enzymeName.set(MS_Trypsin_P);
    sip->enzymes.independent = true;
    sip->enzymes.enzymes.push_back(enzyme);

    sip->parentTolerance.set(MS_search_tolerance_plus_value, "1", UO_parts_per_million);
    sip->parentTolerance.set(MS_search_tolerance_minus_value, "1", UO_parts_per_million);

    sip->fragmentTolerance.set(MS_search_tolerance_plus_value, "0.6", UO_dalton);
    sip->fragmentTolerance.set(MS_search_tolerance_minus_value, "0.6", UO_dalton);

    mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);

    SpectraDataPtr sd(new SpectraData);
    sd->id = "SD";
    sd->name = "tiny";
    sd->location = "file:///data/is/here/tiny.mzML";
    sd->fileFormat.cvid = MS_mzML_file;
    sd->spectrumIDFormat.cvid = MS_Thermo_nativeID_format;
    mzid.dataCollection.inputs.spectraData.push_back(sd);

    // Fill in mzid.dataCollection.inputs;
    // Add SourceFilePtr
    /*SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = "SF_1";
    sourceFile->location = "file:///../data/Mascot_mzml_example.dat";
    sourceFile->fileFormat.set(MS_Mascot_DAT_file);
    mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);*/

    SpectrumIdentificationListPtr sil(new SpectrumIdentificationList("SIL"));
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);

    SpectrumIdentificationPtr si(new SpectrumIdentification);
    si->id = "SI";
    si->spectrumIdentificationProtocolPtr = sip;
    si->spectrumIdentificationListPtr = sil;
    si->activityDate = "2009-05-21T17:01:53";
    si->inputSpectra.push_back(sd);
    si->searchDatabase.push_back(sdb);
    mzid.analysisCollection.spectrumIdentification.push_back(si);

    sip->massTable.id = "MT";
    sip->massTable.msLevel = "1 2";

    const char* residueSymbols = "ACDEFGHIKLMNPQRSTUVWY";
    for (int i=0; i < 21; ++i)
    {
        const AminoAcid::Info::Record& record = AminoAcid::Info::record(residueSymbols[i]);       
        ResiduePtr rp(new Residue);
        rp->code = record.symbol;
        rp->mass = record.residueFormula.monoisotopicMass();
        sip->massTable.residues.push_back(rp);
    }
    
    AmbiguousResiduePtr arp(new AmbiguousResidue);
    arp->code = 'B';
    arp->set(MS_alternate_single_letter_codes, "D N");
    sip->massTable.ambiguousResidue.push_back(arp);

    arp.reset(new AmbiguousResidue);
    arp->code = 'Z';
    arp->set(MS_alternate_single_letter_codes, "E Q");
    sip->massTable.ambiguousResidue.push_back(arp);

    sip->threshold.set(MS_Mascot_SigThreshold, "0.05");
    
    FilterPtr fp(new Filter);
    fp->filterType.set(MS_DB_filter_taxonomy);
    sip->databaseFilters.push_back(fp);

    // Fill in mzid.analysisData
    MeasurePtr measure(new Measure);
    measure->id = "m_mz";
    measure->set(MS_product_ion_m_z);
    sil->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure);
    measure->id = "m_intensity";
    measure->set(MS_product_ion_intensity);
    sil->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure);
    measure->id = "m_error";
    measure->set(MS_product_ion_m_z_error, "", MS_m_z);
    sil->fragmentationTable.push_back(measure);

    /*IonTypePtr ionType(new IonType);
    ionType->setIndex("2 3 4 5 6 7").charge = 1;
    ionType->set(MS_frag__a_ion);
    siip->fragmentation.push_back(ionType);
    FragmentArrayPtr fap(new FragmentArray);
    fap->setValues("197.055771 360.124878 489.167847 603.244324 731.075562 828.637207 " );
    fap->measurePtr = MeasurePtr(new Measure("m_mz"));
    ionType->fragmentArray.push_back(fap);
    sirp->spectrumIdentificationItem.push_back(siip);*/

    PeptidePtr peptide;
    ModificationPtr mod;
    PeptideEvidencePtr pe;
    SpectrumIdentificationResultPtr sir;
    SpectrumIdentificationItemPtr sii;

    // result 1
    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_1";
        sir->name = "tiny.42.42";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=42";
        sir->spectraDataPtr = sd;
        sir->set(MS_retention_time, "123.4", UO_second);

        // result 1 rank 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_1";
            peptide->peptideSequence = "MAKKTAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 17;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            mod->set(UNIMOD_Carbamidomethyl);
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_1";
            sii->rank = 1;
            sii->chargeState = 4;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 420.42;
            sii->calculatedMassToCharge = 420.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 42.1);
            sii->set(MS_Mascot_identity_threshold, 11);
            sii->set(MS_Mascot_homology_threshold, 21);
            sii->set(MS_Mascot_expectation_value, 0.01);
            sii->userParams.push_back(UserParam("an extra score", "1.2345e10", "xsd:float"));

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 1; pe->end = 25;
            pe->pre = '-';
            pe->post = 'V';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // result 1 rank 2 modified variant 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_2";
            peptide->peptideSequence = "TAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 9;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            mod->set(UNIMOD_Phospho);
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = 13;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            mod->set(UNIMOD_Carbamidomethyl);
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_2";
            sii->rank = 2;
            sii->chargeState = 3;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 421.42;
            sii->calculatedMassToCharge = 421.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 4.2);
            sii->set(MS_Mascot_identity_threshold, 11);
            sii->set(MS_Mascot_homology_threshold, 21);
            sii->set(MS_Mascot_expectation_value, 0.1);
            sii->userParams.push_back(UserParam("an extra score", "1.2345E10", "xsd:float"));

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 5; pe->end = 25;
            pe->pre = 'K';
            pe->post = 'V';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // result 1 rank 2 modified variant 2
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_3";
            peptide->peptideSequence = "TAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 10;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            mod->set(UNIMOD_Phospho);
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = 13;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            mod->set(UNIMOD_Carbamidomethyl);
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_3";
            sii->rank = 2;
            sii->chargeState = 3;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 421.42;
            sii->calculatedMassToCharge = 421.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 4.2);
            sii->set(MS_Mascot_identity_threshold, 11);
            sii->set(MS_Mascot_homology_threshold, 21);
            sii->set(MS_Mascot_expectation_value, 0.1);
            sii->userParams.push_back(UserParam("an extra score", "-1.2345", "xsd:float"));

            // copy from previous variant and change peptide and id
            pe = PeptideEvidencePtr(new PeptideEvidence(*pe));
            pe->peptidePtr = peptide;
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }

    // result 2
    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_2";
        sir->name = "tiny.420.420";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=420";
        sir->spectraDataPtr = sd;
        sir->set(MS_retention_time, "234.5", UO_second);

        // result 2 rank 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_4";
            peptide->peptideSequence = "QTQTFTTYSDNQPGVLIQVYEGER";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 1;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = pyroglutQ.molecularWeight();
            mod->monoisotopicMassDelta = pyroglutQ.monoisotopicMass();
            mod->set(UNIMOD_Gln__pyro_Glu);
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_4";
            sii->rank = 1;
            sii->chargeState = 2;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 1420.42;
            sii->calculatedMassToCharge = 1420.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 24.1);
            sii->set(MS_Mascot_identity_threshold, 12);
            sii->set(MS_Mascot_homology_threshold, 22);
            sii->set(MS_Mascot_expectation_value, 0.02);
            sii->userParams.push_back(UserParam("an extra score", "+1E10", "xsd:float"));

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 424; pe->end = 447;
            pe->pre = 'K';
            pe->post = 'A';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // result 2 rank 2
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_5";
            peptide->peptideSequence = "RNSTIPT";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 3;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            mod->set(UNIMOD_Phospho);
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = peptide->peptideSequence.length() + 1;
            mod->avgMassDelta = amidation.molecularWeight();
            mod->monoisotopicMassDelta = amidation.monoisotopicMass();
            mod->set(UNIMOD_Amidated);
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_5";
            sii->rank = 2;
            sii->chargeState = 5;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 422.42;
            sii->calculatedMassToCharge = 422.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 2.4);
            sii->set(MS_Mascot_identity_threshold, 12);
            sii->set(MS_Mascot_homology_threshold, 22);
            sii->set(MS_Mascot_expectation_value, 0.2);
            sii->userParams.push_back(UserParam("an extra score", "-2", "xsd:float"));

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 416; pe->end = 422;
            pe->pre = 'K';
            pe->post = 'K';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[1];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 416; pe->end = 422;
            pe->pre = 'K';
            pe->post = 'K';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }

    // result 3
    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_3";
        sir->name = "tiny.421.421";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=421";
        sir->spectraDataPtr = sd;
        sir->set(MS_retention_time, "345.6", UO_second);

        // result 3 rank 1
        {
            peptide = mzid.sequenceCollection.peptides[4];

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_6";
            sii->rank = 1;
            sii->chargeState = 4;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 424.42;
            sii->calculatedMassToCharge = 424.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 44);
            sii->set(MS_Mascot_identity_threshold, 13);
            sii->set(MS_Mascot_homology_threshold, 23);
            sii->set(MS_Mascot_expectation_value, 0.03);
            sii->userParams.push_back(UserParam("an extra score", "3.", "xsd:float"));
            
            sii->peptideEvidencePtr = sil->spectrumIdentificationResult[1]->spectrumIdentificationItem[1]->peptideEvidencePtr;

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // result 3 rank 2
        {
            peptide = mzid.sequenceCollection.peptides[3];

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_7";
            sii->rank = 2;
            sii->chargeState = 5;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 422.42;
            sii->calculatedMassToCharge = 422.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 4.4);
            sii->set(MS_Mascot_identity_threshold, 13);
            sii->set(MS_Mascot_homology_threshold, 23);
            sii->set(MS_Mascot_expectation_value, 0.3);
            sii->userParams.push_back(UserParam("an extra score", "4.56", "xsd:float"));

            sii->peptideEvidencePtr = sil->spectrumIdentificationResult[1]->spectrumIdentificationItem[0]->peptideEvidencePtr;

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }

    // result 4
    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_4";
        sir->name = "tiny.422.422";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=422";
        sir->spectraDataPtr = sd;
        sir->set(MS_retention_time, "456.7", UO_second);

        // result 4 rank 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_6";
            peptide->peptideSequence = "VEIIANDQGNR";
            mzid.sequenceCollection.peptides.push_back(peptide);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_8";
            sii->rank = 1;
            sii->chargeState = 3;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 424.42;
            sii->calculatedMassToCharge = 424.24;
            sii->peptidePtr = peptide;
            sii->set(MS_Mascot_score, 54);
            sii->set(MS_Mascot_identity_threshold, 23);
            sii->set(MS_Mascot_homology_threshold, 33);
            sii->set(MS_Mascot_expectation_value, 0.003);

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 26; pe->end = 37;
            pe->pre = 'K';
            pe->post = 'T';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->peptidePtr = peptide;
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[1];
            pe->id = pe->dbSequencePtr->accession + "_" + peptide->id;
            pe->start = 26; pe->end = 37;
            pe->pre = 'K';
            pe->post = 'T';
            mzid.sequenceCollection.peptideEvidence.push_back(pe);
            sii->peptideEvidencePtr.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }
}

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    initializeBasicSpectrumIdentification(mzid);

    SpectrumIdentificationListPtr& sil = mzid.dataCollection.analysisData.spectrumIdentificationList[0];

    mzid.id = "";
    
    //SamplePtr sample(new Sample);
    //mzid.analysisSampleCollection.samples.push_back(sample);

    ProteinDetectionProtocolPtr pdp(new ProteinDetectionProtocol("PDP_Mascot_1"));
    pdp->analysisSoftwarePtr = mzid.analysisSoftwareList[1];
    pdp->analysisParams.set(MS_Mascot_SigThreshold, "0.05");
    pdp->analysisParams.set(MS_Mascot_MaxProteinHits, "Auto");
    pdp->analysisParams.set(MS_Mascot_ProteinScoringMethod, "MudPIT");
    pdp->analysisParams.set(MS_Mascot_MinMSMSThreshold, "0");
    pdp->analysisParams.set(MS_Mascot_ShowHomologousProteinsWithSamePeptides, "1");
    pdp->analysisParams.set(MS_Mascot_ShowHomologousProteinsWithSubsetOfPeptides, "1");
    pdp->analysisParams.set(MS_Mascot_RequireBoldRed, "0");
    pdp->analysisParams.set(MS_Mascot_UseUnigeneClustering, "false");
    pdp->analysisParams.set(MS_Mascot_IncludeErrorTolerantMatches, "1");
    pdp->analysisParams.set(MS_Mascot_ShowDecoyMatches, "0");
    pdp->threshold.set(MS_Mascot_SigThreshold, "0.05", CVID_Unknown);
    mzid.analysisProtocolCollection.proteinDetectionProtocol.push_back(pdp);

    ProteinDetectionListPtr pdl(new ProteinDetectionList("PDL_1"));
    ProteinAmbiguityGroupPtr pag(new ProteinAmbiguityGroup("PAG_1"));

    ProteinDetectionHypothesisPtr pdh(new ProteinDetectionHypothesis("PDH_HSP71_RAT"));
    pdh->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
    pdh->passThreshold = true;
    pdh->peptideHypothesis.resize(4, PeptideHypothesis());

    pdh->peptideHypothesis[0].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[0];
    pdh->peptideHypothesis[0].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[0]->spectrumIdentificationItem[0]);

    pdh->peptideHypothesis[1].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[2];
    pdh->peptideHypothesis[1].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[1]->spectrumIdentificationItem[0]);

    pdh->peptideHypothesis[2].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[4];
    pdh->peptideHypothesis[2].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[2]->spectrumIdentificationItem[0]);

    pdh->peptideHypothesis[3].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[6];
    pdh->peptideHypothesis[3].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[3]->spectrumIdentificationItem[0]);

    pdh->set(MS_Mascot_score, "164.4");
    pdh->set(MS_sequence_coverage, "20");
    pdh->set(MS_distinct_peptide_sequences, "4");
    pag->proteinDetectionHypothesis.push_back(pdh);
    
    pdh = ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis("PDH_HSP71_HUMAN"));
    pdh->dbSequencePtr = mzid.sequenceCollection.dbSequences[1];
    pdh->passThreshold = false;
    pdh->peptideHypothesis.resize(2, PeptideHypothesis());    

    pdh->peptideHypothesis[0].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[4];
    pdh->peptideHypothesis[0].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[2]->spectrumIdentificationItem[0]);

    pdh->peptideHypothesis[1].peptideEvidencePtr = mzid.sequenceCollection.peptideEvidence[6];
    pdh->peptideHypothesis[1].spectrumIdentificationItemPtr.push_back(
        sil->spectrumIdentificationResult[3]->spectrumIdentificationItem[0]);

    pdh->set(MS_Mascot_score, "40.95");
    pdh->set(MS_sequence_coverage, "10");
    pdh->set(MS_distinct_peptide_sequences, "2");
    pag->proteinDetectionHypothesis.push_back(pdh);
    
    pdl->proteinAmbiguityGroup.push_back(pag);
    mzid.dataCollection.analysisData.proteinDetectionListPtr = pdl;
    
    mzid.analysisCollection.proteinDetection.id = "PD_1";
    mzid.analysisCollection.proteinDetection.proteinDetectionProtocolPtr = pdp;
    mzid.analysisCollection.proteinDetection.proteinDetectionListPtr = pdl;
    mzid.analysisCollection.proteinDetection.activityDate = "2009-06-30T15:36:35";
    mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back(sil);

    References::resolve(mzid);
}

} // namespace pwiz
} // namespace mziddata
} // namespace examples 
