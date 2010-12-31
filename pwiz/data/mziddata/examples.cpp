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

    AnalysisSoftwarePtr as;/* = AnalysisSoftwarePtr(new AnalysisSoftware);
    as->id = "AS ProteoWizard " + Version::str();
    as->name = "ProteoWizard MzIdentML";
    as->softwareName.set(MS_pwiz);
    as->version = Version::str();
    mzid.analysisSoftwareList.push_back(as);*/

    as = AnalysisSoftwarePtr(new AnalysisSoftware);
    as->id = "AS_Mascot";
    as->name = "Mascot";
    as->version = "2.2.101";
    as->softwareName.set(MS_Mascot);
    mzid.analysisSoftwareList.push_back(as);

    SearchDatabasePtr sdb = SearchDatabasePtr(new SearchDatabase);
    sdb->id = "SDB_SwissProt";
    sdb->name = "SwissProt";
    sdb->version = "SwissProt_51.6.fasta";
    sdb->releaseDate = "SwissProt_51.6.fasta";
    sdb->location = "file:///C:/inetpub/Mascot/sequence/SwissProt/current/SwissProt_51.6.fasta";
    sdb->fileFormat.set(MS_FASTA_format);
    sdb->numDatabaseSequences = 5;
    sdb->numResidues = 52;
    sdb->params.set(MS_database_type_amino_acid);
    mzid.dataCollection.inputs.searchDatabase.push_back(sdb);

    DBSequencePtr dbSequence = DBSequencePtr(new DBSequence);
    dbSequence->accession = HSP71_RAT.accession;
    dbSequence->seq = HSP71_RAT.sequence;
    dbSequence->id = "DBSeq_"  + dbSequence->accession;
    dbSequence->length = dbSequence->seq.length();
    dbSequence->searchDatabasePtr = sdb;
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    dbSequence = DBSequencePtr(new DBSequence);
    dbSequence->accession = HSP71_HUMAN.accession;
    dbSequence->seq = HSP71_HUMAN.sequence;
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
    sip->searchType.set(MS_ms_ms_search);
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
    sip->additionalSearchParams.userParams.push_back(UserParam("TOL", "10"));
    sip->additionalSearchParams.userParams.push_back(UserParam("TOLU", "ppm"));
    sip->additionalSearchParams.userParams.push_back(UserParam("ITOL", "0.6"));
    sip->additionalSearchParams.userParams.push_back(UserParam("ITOLU", "Da"));

    SearchModificationPtr sm(new SearchModification);
    sm->modParam.massDelta = acetylation.monoisotopicMass();
    sm->specificityRules.set(MS_modification_specificity_N_term);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = amidation.monoisotopicMass();
    sm->specificityRules.set(MS_modification_specificity_C_term);
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = deamidation.monoisotopicMass();
    sm->modParam.residues = "N";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = pyroglutQ.monoisotopicMass();
    sm->specificityRules.set(MS_modification_specificity_N_term);
    sm->modParam.residues = "Q";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = carbamylation.monoisotopicMass();
    sm->modParam.residues = "K";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = carbamylation.monoisotopicMass();
    sm->modParam.residues = "R";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = oxidation.monoisotopicMass();
    sm->modParam.residues = "M";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = phosphorylation.monoisotopicMass();
    sm->modParam.residues = "S";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = phosphorylation.monoisotopicMass();
    sm->modParam.residues = "T";
    sip->modificationParams.push_back(sm);

    sm = SearchModificationPtr(new SearchModification);
    sm->modParam.massDelta = cam.monoisotopicMass();
    sm->modParam.residues = "C";
    sm->fixedMod = true;
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
    sip->enzymes.enzymes.push_back(enzyme);

    sip->parentTolerance.set(MS_search_tolerance_plus_value, "10", UO_parts_per_million);
    sip->parentTolerance.set(MS_search_tolerance_minus_value, "10", UO_parts_per_million);

    sip->fragmentTolerance.set(MS_search_tolerance_plus_value, "0.6", UO_dalton);
    sip->fragmentTolerance.set(MS_search_tolerance_minus_value, "0.6", UO_dalton);

    mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);

    SpectraDataPtr sd(new SpectraData);
    sd->id = "SD";
    sd->name = "tiny";
    sd->location = "file:///data/is/here/tiny.mzML";
    sd->fileFormat.set(MS_mzML_file);
    sd->spectrumIDFormat.set(MS_Thermo_nativeID_format);
    mzid.dataCollection.inputs.spectraData.push_back(sd);

    // Fill in mzid.dataCollection.inputs;
    // Add SourceFilePtr
    /*SourceFilePtr sourceFile(new SourceFile());
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

    PeptidePtr peptide;
    ModificationPtr mod;
    PeptideEvidencePtr pe;
    SpectrumIdentificationResultPtr sir;
    SpectrumIdentificationItemPtr sii;
    int distinctPeptides = 0;

    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_1";
        sir->name = "tiny.42.42";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=42";
        sir->spectraDataPtr = sd;

        // rank 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_1";
            peptide->peptideSequence = "MAKKTAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 17;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_1";
            sii->rank = 1;
            sii->chargeState = 4;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 420.42;
            sii->calculatedMassToCharge = 420.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 42.1);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 11);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 21);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.01);
            
            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_PEP_" + lexical_cast<string>(++distinctPeptides);
            pe->start = 1; pe->end = 25;
            pe->pre = "-";
            pe->post = "V";
            pe->missedCleavages = 2;
            sii->peptideEvidence.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // rank 2 modified variant 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_2";
            peptide->peptideSequence = "TAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 9;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = 17;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_2";
            sii->rank = 2;
            sii->chargeState = 3;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 421.42;
            sii->calculatedMassToCharge = 421.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 4.2);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 11);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 21);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.1);
            
            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_PEP_" + lexical_cast<string>(++distinctPeptides);
            pe->start = 5; pe->end = 25;
            pe->pre = "K";
            pe->post = "V";
            pe->missedCleavages = 0;
            sii->peptideEvidence.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // rank 2 modified variant 2
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_3";
            peptide->peptideSequence = "TAIGIDLGTTYSCVGVFQHGK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 10;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = 17;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = cam.molecularWeight();
            mod->monoisotopicMassDelta = cam.monoisotopicMass();
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_3";
            sii->rank = 2;
            sii->chargeState = 3;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 421.42;
            sii->calculatedMassToCharge = 421.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 4.2);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 11);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 21);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.1);

            // reuse the same peptide evidence from variant 1
            sii->peptideEvidence.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }

    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_2";
        sir->name = "tiny.420.420";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=420";
        sir->spectraDataPtr = sd;

        // rank 1
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_4";
            peptide->peptideSequence = "QTQTFTTYSDNQPGVLIQVYEGER";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 1;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = pyroglutQ.molecularWeight();
            mod->monoisotopicMassDelta = pyroglutQ.monoisotopicMass();
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_4";
            sii->rank = 1;
            sii->chargeState = 2;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 1420.42;
            sii->calculatedMassToCharge = 1420.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 24.1);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 12);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 22);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.02);
            
            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_PEP_" + lexical_cast<string>(++distinctPeptides);
            pe->start = 424; pe->end = 447;
            pe->pre = "K";
            pe->post = "A";
            pe->missedCleavages = 0;
            sii->peptideEvidence.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // rank 2
        {
            peptide = PeptidePtr(new Peptide);
            peptide->id = "PEP_5";
            peptide->peptideSequence = "RNSTIPTK";
            mzid.sequenceCollection.peptides.push_back(peptide);

            mod = ModificationPtr(new Modification);
            mod->location = 3;
            mod->residues = peptide->peptideSequence[mod->location-1];
            mod->avgMassDelta = phosphorylation.molecularWeight();
            mod->monoisotopicMassDelta = phosphorylation.monoisotopicMass();
            peptide->modification.push_back(mod);

            mod = ModificationPtr(new Modification);
            mod->location = peptide->peptideSequence.length() + 1;
            mod->avgMassDelta = amidation.molecularWeight();
            mod->monoisotopicMassDelta = amidation.monoisotopicMass();
            peptide->modification.push_back(mod);

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_5";
            sii->rank = 2;
            sii->chargeState = 5;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 422.42;
            sii->calculatedMassToCharge = 422.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 2.4);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 12);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 22);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.2);
            
            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[0];
            pe->id = pe->dbSequencePtr->accession + "_PEP_" + lexical_cast<string>(++distinctPeptides);
            pe->start = 416; pe->end = 423;
            pe->pre = "K";
            pe->post = "Q";
            pe->missedCleavages = 1;
            sii->peptideEvidence.push_back(pe);
            
            pe = PeptideEvidencePtr(new PeptideEvidence);
            pe->dbSequencePtr = mzid.sequenceCollection.dbSequences[1];
            pe->id = pe->dbSequencePtr->accession + "_PEP_" + lexical_cast<string>(distinctPeptides);
            pe->start = 416; pe->end = 423;
            pe->pre = "K";
            pe->post = "Q";
            pe->missedCleavages = 1;
            sii->peptideEvidence.push_back(pe);

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }

    {
        sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
        sir->id = "SIR_3";
        sir->name = "tiny.421.421";
        sir->spectrumID = "controllerType=0 controllerNumber=1 scan=421";
        sir->spectraDataPtr = sd;

        // rank 1
        {
            peptide = mzid.sequenceCollection.peptides[3];

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_6";
            sii->rank = 1;
            sii->chargeState = 4;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 424.42;
            sii->calculatedMassToCharge = 424.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 44);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 13);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 23);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.03);
            
            sii->peptideEvidence = sil->spectrumIdentificationResult[1]->spectrumIdentificationItem[0]->peptideEvidence;

            sir->spectrumIdentificationItem.push_back(sii);
        }

        // rank 2
        {
            peptide = mzid.sequenceCollection.peptides[4];

            sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
            sii->id = "SII_7";
            sii->rank = 2;
            sii->chargeState = 5;
            sii->passThreshold = true;
            sii->experimentalMassToCharge = 422.42;
            sii->calculatedMassToCharge = 422.24;
            sii->peptidePtr = peptide;
            sii->paramGroup.set(MS_Mascot_score, 4.4);
            sii->paramGroup.set(MS_Mascot_identity_threshold, 13);
            sii->paramGroup.set(MS_Mascot_homology_threshold, 23);
            sii->paramGroup.set(MS_Mascot_expectation_value, 0.3);

            sii->peptideEvidence = sil->spectrumIdentificationResult[1]->spectrumIdentificationItem[1]->peptideEvidence;

            sir->spectrumIdentificationItem.push_back(sii);
        }
        sil->spectrumIdentificationResult.push_back(sir);
    }
}

PWIZ_API_DECL void initializeTiny(MzIdentML& mzid)
{
    /*mzid.id = "";

    ContactPtr contactPwiz(new Organization("ORG_PWIZ", "ProteoWizard"));
    contactPwiz->email = "support@proteowizard.org";
    mzid.auditCollection.push_back(contactPwiz);

    analysisSoftwarePtr->contactRolePtr.reset(new ContactRole);
    analysisSoftwarePtr->contactRolePtr->role.set(MS_software_vendor);
    analysisSoftwarePtr->contactRolePtr->contactPtr = contactPwiz;

    analysisSoftwarePtr->URI = "http://www.matrixscience.com/search_form_select.html";
    ContactRolePtr aspCont = ContactRolePtr(new ContactRole());
    aspCont->contactPtr = ContactPtr(new Contact("ORG_MSL"));
    aspCont->role.set(MS_software_vendor);
    analysisSoftwarePtr->contactRolePtr = aspCont;

    analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware());
    analysisSoftwarePtr->id = "AS_Mascot_parser";
    analysisSoftwarePtr->name = "Mascot Parser";
    analysisSoftwarePtr->softwareName.set(MS_Mascot_Parser);
    mzid.analysisSoftwareList.push_back(analysisSoftwarePtr);

    mzid.provider.id = "PROVIDER";
    mzid.provider.contactRole.contactPtr = ContactPtr(new Contact("PERSON_DOC_OWNER"));
    mzid.provider.contactRole.role.set(MS_researcher);

    PersonPtr person(new Person());
    Affiliations aff;
    aff.organizationPtr = OrganizationPtr(new Organization("ORG_MSL"));
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    person = PersonPtr(new Person());
    person->id = "PERSON_DOC_OWNER";
    person->firstName = "";
    person->lastName = "David Creasy";
    person->email = "dcreasy@matrixscience.com";
    aff.organizationPtr = OrganizationPtr(new Organization("ORG_DOC_OWNER"));
    person->affiliations.push_back(aff);
    mzid.auditCollection.push_back(person);

    OrganizationPtr organization(new Organization());
    organization->id = "ORG_MSL";
    organization->name = "Matrix Science Limited";
    organization->address = "64 Baker Street, London W1U 7GB, UK";
    organization->email = "support@matrixscience.com";
    organization->fax = "+44 (0)20 7224 1344";
    organization->phone = "+44 (0)20 7486 1050";
    mzid.auditCollection.push_back(organization);

    organization = OrganizationPtr(new Organization());
    organization->id = "ORG_DOC_OWNER";
    mzid.auditCollection.push_back(organization);
    
    //SamplePtr sample(new Sample());
    //mzid.analysisSampleCollection.samples.push_back(sample);
    
    DBSequencePtr dbSequence(new DBSequence());
    dbSequence->id = "DBSeq_Bombessin";
    dbSequence->length = 14;
    dbSequence->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix"));
    dbSequence->accession = "Bombessin";
    dbSequence->seq = dbsequenceList[0];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[0]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

    
    dbSequence = DBSequencePtr(new DBSequence());
    dbSequence->id = "DBSeq_Neurotensin";
    dbSequence->length = 13;
    dbSequence->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix"));
    dbSequence->accession = "Neurotensin";
    dbSequence->seq = dbsequenceList[1];
    dbSequence->paramGroup.set(MS_protein_description, peptideList[1]);
    mzid.sequenceCollection.dbSequences.push_back(dbSequence);
    
    dbSequence = DBSequencePtr(new DBSequence());
    dbSequence->paramGroup.set(MS_protein_description, HSP71_RAT.description);
    dbSequence->paramGroup.set(MS_taxonomy__scientific_name, "Rattus norvegicus");
    dbSequence->paramGroup.set(MS_taxonomy__NCBI_TaxID, "10116");
    
    PeptidePtr peptide(new Peptide());
    peptide->id = "peptide_1_1";
    peptide->peptideSequence = "QLYENKPRRPYIL";
    ModificationPtr modification(new Modification());
    modification->location = 0;
    modification->monoisotopicMassDelta = -17.026549;
    modification->paramGroup.set(UNIMOD_Gln__pyro_Glu);
    peptide->modification.push_back(modification);
    mzid.sequenceCollection.peptides.push_back(peptide);

    peptide = PeptidePtr(new Peptide());
    peptide->id = "peptide_11_1";
    peptide->peptideSequence = "RPKPQQFFGLM";
    mzid.sequenceCollection.peptides.push_back(peptide);
    
    peptide = PeptidePtr(new Peptide());    
    peptide->id = "peptide_13_1";
    peptide->peptideSequence = "RPKPQQFFGLM";
    mzid.sequenceCollection.peptides.push_back(peptide);
    
    SpectrumIdentificationPtr spectrumIdentificationPtr(
        new SpectrumIdentification());
    spectrumIdentificationPtr->id = "SI";
    spectrumIdentificationPtr->spectrumIdentificationProtocolPtr = 
        SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol("SIP"));
    spectrumIdentificationPtr->spectrumIdentificationListPtr = 
        SpectrumIdentificationListPtr(new SpectrumIdentificationList("SIL_1"));
    spectrumIdentificationPtr->activityDate = "2009-05-21T17:01:53";
    spectrumIdentificationPtr->inputSpectra.push_back(SpectraDataPtr(new SpectraData("SD_1")));;
    spectrumIdentificationPtr->searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase("SDB_5peptideMix")));
    mzid.analysisCollection.spectrumIdentification.push_back(spectrumIdentificationPtr);

    mzid.analysisCollection.proteinDetection.id = "PD_1";
    mzid.analysisCollection.proteinDetection.proteinDetectionProtocolPtr = ProteinDetectionProtocolPtr(new ProteinDetectionProtocol("PDP_MascotParser_1"));
    mzid.analysisCollection.proteinDetection.proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList("PDL_1"));
    mzid.analysisCollection.proteinDetection.activityDate = "2009-06-30T15:36:35";

    SpectrumIdentificationListPtr silp(new SpectrumIdentificationList("SIL_1"));
    mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back(silp);

    SpectrumIdentificationProtocolPtr sip(new SpectrumIdentificationProtocol());
    sip->id = "SIP";
    sip->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("AS_Mascot_server"));
    sip->searchType.set(MS_ms_ms_search);
    sip->additionalSearchParams.set(MS_parent_mass_type_mono);
    sip->additionalSearchParams.set(MS_param__a_ion);
    sip->additionalSearchParams.set(MS_param__a_ion_NH3);
    sip->additionalSearchParams.set(MS_param__b_ion);
    sip->additionalSearchParams.set(MS_param__b_ion_NH3);
    sip->additionalSearchParams.set(MS_param__y_ion);
    sip->additionalSearchParams.set(MS_param__y_ion_NH3);
    SearchModificationPtr smp(new SearchModification());
    smp->modParam.massDelta = -17.026549;
    smp->modParam.residues = "Q";
    smp->modParam.cvParams.set(UNIMOD_Gln__pyro_Glu);
    // TODO add UNIMOD:28
    // Use ParamContainer in place of vector<CVParam>
    smp->specificityRules.set(MS_modification_specificity_N_term, string(""));
    sip->modificationParams.push_back(smp);

    EnzymePtr ep(new Enzyme());
    ep->id = "ENZ_0";
    ep->cTermGain = "OH";
    ep->nTermGain = "H";
    ep->missedCleavages = 1;
    ep->semiSpecific = false;
    ep->siteRegexp = "(?< = [KR])(?!P)";
    ep->enzymeName.set(MS_Trypsin);
    sip->enzymes.enzymes.push_back(ep);

    sip->massTable.id = "MT";
    sip->massTable.msLevel = "1 2";

    ResiduePtr rp(new Residue());
    rp->Code = "A"; rp->Mass = 71.037114;
    sip->massTable.residues.push_back(rp);

    AmbiguousResiduePtr arp(new AmbiguousResidue());
    arp->Code = "B";
    arp->params.set(MS_alternate_single_letter_codes);
    sip->massTable.ambiguousResidue.push_back(arp);

    sip->fragmentTolerance.set(MS_search_tolerance_plus_value, "0.6", UO_dalton);
    sip->fragmentTolerance.set(MS_search_tolerance_minus_value, "0.6", UO_dalton);

    sip->parentTolerance.set(MS_search_tolerance_plus_value, "3", UO_dalton);
    sip->parentTolerance.set(MS_search_tolerance_minus_value, "3", UO_dalton);

    sip->threshold.set(MS_Mascot_SigThreshold, "0.05");
    
    FilterPtr fp(new Filter());
    fp->filterType.set(MS_DB_filter_taxonomy);
    sip->databaseFilters.push_back(fp);

    mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);

    ProteinDetectionProtocolPtr pdp(new ProteinDetectionProtocol());
    pdp->id = "PDP_MascotParser_1";
    pdp->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("AS_Mascot_parser"));
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


    // Fill in mzid.dataCollection.inputs;
    // Add SourceFilePtr
    SourceFilePtr sourceFile(new SourceFile());
    sourceFile->id = "SF_1";
    sourceFile->location = "file:///../data/Mascot_mzml_example.dat";
    sourceFile->fileFormat.set(MS_Mascot_DAT_file);
    mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);

    // Add SearchDatabasePtr
    SearchDatabasePtr searchDb(new SearchDatabase());
    searchDb->id = "SDB_5peptideMix";
    searchDb->name = "5peptideMix";
    searchDb->location = "file:///c:/inetpub/Mascot/sequence/5peptideMix/current/5peptideMix_20090515.fasta";
    searchDb->numDatabaseSequences = 5;
    searchDb->numResidues = 52;
    searchDb->releaseDate = "5peptideMix_20090515.fasta";
    searchDb->version = "5peptideMix_20090515.fasta";
    searchDb->fileFormat.set(MS_FASTA_format);
    searchDb->DatabaseName.userParams.push_back(UserParam("5peptideMix_20090515.fasta"));
    mzid.dataCollection.inputs.searchDatabase.push_back(searchDb);

    searchDb = SearchDatabasePtr(new SearchDatabase());
    searchDb->id = "SDB_SwissProt";
    searchDb->name = "SwissProt";
    searchDb->location = "file:///C:/inetpub/Mascot/sequence/SwissProt/current/SwissProt_51.6.fasta";
    searchDb->numDatabaseSequences = 5;
    searchDb->numResidues = 52;
    searchDb->releaseDate = "SwissProt_51.6.fasta";
    searchDb->version = "SwissProt_51.6.fasta";
    searchDb->fileFormat.set(MS_FASTA_format);
    searchDb->DatabaseName.userParams.push_back(UserParam("SwissProt_51.6.fasta"));
    searchDb->params.set(MS_database_type_amino_acid);
    mzid.dataCollection.inputs.searchDatabase.push_back(searchDb);

    // Add SpectraDataPtr
    SpectraDataPtr spectraData(new SpectraData());
    spectraData->id = "SD_1";
    spectraData->location = "file:///small.pwiz.1.1.mzML";
    spectraData->fileFormat.set(MS_mzML_file);
    spectraData->spectrumIDFormat.set(MS_multiple_peak_list_nativeID_format);
    mzid.dataCollection.inputs.spectraData.push_back(spectraData);
    
    // Fill in mzid.analysisData
    // Add SpectrumIdentificationListPtr
    silp = SpectrumIdentificationListPtr(new SpectrumIdentificationList());
    silp->id = "SIL_1";
    silp->numSequencesSearched = 5;
    
    MeasurePtr measure(new Measure());
    measure->id = "m_mz";
    measure->paramGroup.set(MS_product_ion_m_z);
    silp->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure());
    measure->id = "m_intensity";
    measure->paramGroup.set(MS_product_ion_intensity);
    silp->fragmentationTable.push_back(measure);

    measure = MeasurePtr(new Measure());
    measure->id = "m_error";
    measure->paramGroup.set(MS_product_ion_m_z_error, "", MS_m_z);
    silp->fragmentationTable.push_back(measure);

    SpectrumIdentificationResultPtr sirp(new SpectrumIdentificationResult());
    sirp->id = "SIR_1";
    sirp->spectrumID = "controllerType = 0 controllerNumber = 1 scan = 33" ;
    sirp->spectraDataPtr = SpectraDataPtr(new SpectraData("SD_1"));
    SpectrumIdentificationItemPtr siip(new SpectrumIdentificationItem());
    siip->id = "SII_1_1";
    siip->calculatedMassToCharge = 557.303212333333;
    siip->chargeState = 3;
    siip->experimentalMassToCharge = 558.75;
    siip->peptidePtr = PeptidePtr(new Peptide("peptide_1_1"));
    siip->rank = 1;
    siip->passThreshold = true;
    siip->paramGroup.set(MS_Mascot_score, "15.71");
    siip->paramGroup.set(MS_Mascot_expectation_value, "0.0268534444565851");

    PeptideEvidencePtr pep(new PeptideEvidence());
    pep->id = "PE_1_1_Neurotensin";
    pep->start = 1;
    pep->end = 13;
    pep->pre = "-";
    pep->post = "-" ;
    pep->missedCleavages = 1;
    pep->frame = 0;
    pep->isDecoy = false;
    pep->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_Neurotensin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id = "PE_19_1_Bombessin_0";
    pep->start = 1;
    pep->end = 14;
    pep->pre = "-";
    pep->post = "-" ;
    pep->missedCleavages = 1;
    pep->frame = 0;
    pep->isDecoy = false;
    pep->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id = "PE_20_1_Bombessin_0";
    pep->start = 1;
    pep->end = 14;
    pep->pre = "-";
    pep->post = "-" ;
    pep->missedCleavages = 1;
    pep->frame = 0;
    pep->isDecoy = false;
    pep->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    siip->peptideEvidence.push_back(pep);

    pep = PeptideEvidencePtr(new PeptideEvidence());
    pep->id = "PE_2_1_HSP71_RAT_0";
    pep->start = 37;
    pep->end = 49;
    pep->pre = "R";
    pep->post = "L" ;
    pep->missedCleavages = 1;
    pep->frame = 0;
    pep->isDecoy = false;
    pep->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_HSP71_RAT"));
    siip->peptideEvidence.push_back(pep);
    
    siip->paramGroup.set(MS_Mascot_score, "15.71");
    siip->paramGroup.set(MS_Mascot_expectation_value, 0.0268534444565851);

    IonTypePtr ionType(new IonType());
    ionType->setIndex("2 3 4 5 6 7").charge = 1;
    ionType->paramGroup.set(MS_frag__a_ion);
    siip->fragmentation.push_back(ionType);
    FragmentArrayPtr fap(new FragmentArray());
    fap->setValues("197.055771 360.124878 489.167847 603.244324 731.075562 828.637207 " );
    fap->measurePtr = MeasurePtr(new Measure("m_mz"));
    ionType->fragmentArray.push_back(fap);
    sirp->spectrumIdentificationItem.push_back(siip);
    
    silp->spectrumIdentificationResult.push_back(sirp);
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silp);

    // Fill in proteinDetectionList
    ProteinDetectionListPtr pdl(new ProteinDetectionList());
    pdl->id = "PDL_1";
    ProteinAmbiguityGroupPtr pagp(new ProteinAmbiguityGroup());
    pagp->id = "PAG_hit_1";
    ProteinDetectionHypothesisPtr pdhp(new ProteinDetectionHypothesis());
    pdhp->id = "PDH_Bombessin";
    pdhp->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_Bombessin"));
    pdhp->passThreshold = true;
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_19_1_Bombessin_0")));
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_20_1_Bombessin_0")));
    pdhp->paramGroup.set(MS_Mascot_score, "164.4");
    pdhp->paramGroup.set(MS_sequence_coverage, "100");
    pdhp->paramGroup.set(MS_distinct_peptide_sequences, "7");
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdhp = ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis());
    pdhp->id = "PDH_HSP71_RAT";
    pdhp->dbSequencePtr = DBSequencePtr(new DBSequence("DBSeq_HSP71_RAT"));
    pdhp->passThreshold = "false";
    pdhp->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence("PE_2_1_HSP71_RAT_0")));
    pdhp->paramGroup.set(MS_Mascot_score, "40.95");
    pdhp->paramGroup.set(MS_sequence_coverage, "2");
    pdhp->paramGroup.set(MS_distinct_peptide_sequences, "1");
    pdhp->paramGroup.set(MS_manual_validation);
    pagp->proteinDetectionHypothesis.push_back(pdhp);
    
    pdl->proteinAmbiguityGroup.push_back(pagp);
    mzid.dataCollection.analysisData.proteinDetectionListPtr = pdl;

    References::resolve(mzid); */
}

} // namespace pwiz
} // namespace mziddata
} // namespace examples 
