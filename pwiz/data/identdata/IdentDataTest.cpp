//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/common/Unimod.hpp"
#include "IdentData.hpp"
#include "Serializer_mzid.hpp"
#include "examples.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"


using namespace pwiz::identdata;
using namespace pwiz::identdata::examples;
using namespace pwiz::util;
using namespace pwiz::data;
namespace proteome = pwiz::proteome;


ostream* os_;


void testDigestedPeptides()
{
    using namespace pwiz::proteome;

    IdentData mzid;
    initializeBasicSpectrumIdentification(mzid);

    SpectrumIdentificationProtocolPtr sip = mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];
    SpectrumIdentificationListPtr sil = mzid.dataCollection.analysisData.spectrumIdentificationList[0];

    SpectrumIdentificationResultPtr result2 = sil->spectrumIdentificationResult[1];

    // test with multiple simultaneous enzymes (Lys-C/P and Arg-C)
    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A

        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // both termini are specific now, one cut from each enzyme
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(2, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
    }
    
    // test with multiple independent enzymes (Lys-C/P and Arg-C)
    sip->enzymes.independent = true;
    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // reassign the original prefix residue
        result2_rank1->peptideEvidencePtr[0]->pre = 'K';

        // there are two semi-specific peptides, one cut by Lys-C and the other cut by Arg-C;
        // only the first one will be returned because they have the same "best specificity"

        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
    }

    // change from multiple enzymes to trypsin/p and test again
    sip->enzymes.enzymes.clear();
    EnzymePtr trypsin(new Enzyme);
    trypsin->id = "ENZ_1";
    trypsin->cTermGain = "OH";
    trypsin->nTermGain = "H";
    trypsin->missedCleavages = 2;
    trypsin->minDistance = 1;
    trypsin->terminalSpecificity = proteome::Digestion::FullySpecific;
    trypsin->siteRegexp = "(?<=[KR])";
    trypsin->enzymeName.set(MS_Trypsin_P);
    sip->enzymes.enzymes.push_back(trypsin);

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(423, result2_rank1_digestedPeptides[0].offset());
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(2, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert_operator_equal("K", result2_rank1_digestedPeptides[0].NTerminusPrefix());
        unit_assert_operator_equal("A", result2_rank1_digestedPeptides[0].CTerminusSuffix());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert_operator_equal(2, result2_rank2_digestedPeptides.size());

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]) == result2_rank2_digestedPeptides[i]);
            unit_assert_operator_equal(415, result2_rank2_digestedPeptides[i].offset());
            unit_assert_operator_equal(1, result2_rank2_digestedPeptides[i].missedCleavages());
            unit_assert_operator_equal(1, result2_rank2_digestedPeptides[i].specificTermini());
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(!result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
            unit_assert_operator_equal("K", result2_rank2_digestedPeptides[i].NTerminusPrefix());
            unit_assert_operator_equal("K", result2_rank2_digestedPeptides[i].CTerminusSuffix());
        }
    }

    // change enzyme from trypsin to Lys-C and test again
    sip->enzymes.enzymes[0]->enzymeName.clear();
    sip->enzymes.enzymes[0]->siteRegexp = "(?<=K)";

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert_operator_equal(2, result2_rank2_digestedPeptides.size());

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]) == result2_rank2_digestedPeptides[i]);
            unit_assert_operator_equal(0, result2_rank2_digestedPeptides[i].missedCleavages());
            unit_assert_operator_equal(1, result2_rank2_digestedPeptides[i].specificTermini());
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(!result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    // change enzyme from Lys-C to unspecific cleavage and test again
    sip->enzymes.enzymes[0]->enzymeName.set(MS_unspecific_cleavage);
    sip->enzymes.enzymes[0]->siteRegexp.clear();

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert_operator_equal(2, result2_rank2_digestedPeptides.size());

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]) == result2_rank2_digestedPeptides[i]);
            unit_assert_operator_equal(0, result2_rank2_digestedPeptides[i].missedCleavages());
            unit_assert_operator_equal(0, result2_rank2_digestedPeptides[i].specificTermini());
            unit_assert(!result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(!result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    // change enzyme from unspecific cleavage to no cleavage and test again
    sip->enzymes.enzymes[0]->enzymeName.clear();
    sip->enzymes.enzymes[0]->enzymeName.set(MS_no_cleavage);

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(2, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert_operator_equal(2, result2_rank2_digestedPeptides.size());

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]) == result2_rank2_digestedPeptides[i]);
            unit_assert_operator_equal(0, result2_rank2_digestedPeptides[i].missedCleavages());
            unit_assert_operator_equal(2, result2_rank2_digestedPeptides[i].specificTermini());
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    // change enzyme from no cleavage to Lys-N and test again
    sip->enzymes.enzymes[0]->enzymeName.clear();
    sip->enzymes.enzymes[0]->siteRegexp = "(?=K)";

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert_operator_equal(2, result2_rank2_digestedPeptides.size());

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]) == result2_rank2_digestedPeptides[i]);
            unit_assert_operator_equal(0, result2_rank2_digestedPeptides[i].missedCleavages());
            unit_assert_operator_equal(1, result2_rank2_digestedPeptides[i].specificTermini());
            unit_assert(!result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // move it to the C terminus
        result2_rank1->peptideEvidencePtr[0]->start = 618;
        result2_rank1->peptideEvidencePtr[0]->post = '-';

        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(617, result2_rank1_digestedPeptides[0].offset());
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert_operator_equal("K", result2_rank1_digestedPeptides[0].NTerminusPrefix());
        unit_assert_operator_equal("-", result2_rank1_digestedPeptides[0].CTerminusSuffix());

        // move it to the N terminus
        result2_rank1->peptideEvidencePtr[0]->start = 1;
        result2_rank1->peptideEvidencePtr[0]->pre = '-';
        result2_rank1->peptideEvidencePtr[0]->post = 'A';

        result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides.size());
        unit_assert(digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]) == result2_rank1_digestedPeptides[0]);
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].offset());
        unit_assert_operator_equal(0, result2_rank1_digestedPeptides[0].missedCleavages());
        unit_assert_operator_equal(1, result2_rank1_digestedPeptides[0].specificTermini());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert_operator_equal("-", result2_rank1_digestedPeptides[0].NTerminusPrefix());
        unit_assert_operator_equal("A", result2_rank1_digestedPeptides[0].CTerminusSuffix());
    }
}

void testSnapModifications()
{
    IdentData mzid, mzid2;
    initializeBasicSpectrumIdentification(mzid);
    initializeBasicSpectrumIdentification(mzid2);

    BOOST_FOREACH(SpectrumIdentificationProtocolPtr& sip, mzid2.analysisProtocolCollection.spectrumIdentificationProtocol)
    BOOST_FOREACH(SearchModificationPtr& mod, sip->modificationParams)
        mod->cvParams.clear();

    BOOST_FOREACH(PeptidePtr& pep, mzid2.sequenceCollection.peptides)
    BOOST_FOREACH(ModificationPtr& mod, pep->modification)
        mod->cvParams.clear();

    Diff<IdentData, DiffConfig> diff(mzid, mzid2);
    unit_assert(diff);

    BOOST_FOREACH(SpectrumIdentificationPtr& si, mzid2.analysisCollection.spectrumIdentification)
        snapModificationsToUnimod(*si);

    diff(mzid, mzid2);
    if (diff && os_) *os_ << "diff:\n" << diff_string<TextWriter>(diff) << endl;
    unit_assert(!diff);
}

void testConversion()
{
    using proteome::ModificationMap;

    IdentData mzid;
    initializeBasicSpectrumIdentification(mzid);

    // PEP_2: TAIGIDLGT[80]TYSC[57]VGVFQHGK
    proteome::Peptide pep2 = peptide(*mzid.sequenceCollection.peptides[1]);
    unit_assert_operator_equal("TAIGIDLGTTYSCVGVFQHGK", pep2.sequence());
    unit_assert_operator_equal(2, pep2.modifications().size());
    unit_assert_operator_equal(1, pep2.modifications().count(8));
    unit_assert_operator_equal(unimod::modification(UNIMOD_Phospho).deltaMonoisotopicMass(),
                               pep2.modifications().find(8)->second.monoisotopicDeltaMass());
    unit_assert_operator_equal(1, pep2.modifications().count(12));
    unit_assert_operator_equal(unimod::modification(UNIMOD_Carbamidomethyl).deltaMonoisotopicMass(),
                               pep2.modifications().find(12)->second.monoisotopicDeltaMass());

    // PEP_5: RNS[80]TIPT[-1]
    proteome::Peptide pep5 = peptide(*mzid.sequenceCollection.peptides[4]);
    unit_assert_operator_equal("RNSTIPT", pep5.sequence());
    unit_assert_operator_equal(2, pep5.modifications().size());
    unit_assert_operator_equal(1, pep5.modifications().count(2));
    unit_assert_operator_equal(unimod::modification(UNIMOD_Phospho).deltaMonoisotopicMass(),
                               pep5.modifications().find(2)->second.monoisotopicDeltaMass());
    unit_assert_operator_equal(1, pep5.modifications().count(ModificationMap::CTerminus()));
    unit_assert_operator_equal(unimod::modification(UNIMOD_Amidated).deltaMonoisotopicMass(),
                               pep5.modifications().find(ModificationMap::CTerminus())->second.monoisotopicDeltaMass());

    // test an isotope labelled peptide with a UNIMOD cvParam but no supported formula
    Modification* nMod = new Modification();
    Modification* cMod = new Modification();
    nMod->location = 0;
    cMod->location = 11; cMod->residues.push_back('K'); 
    nMod->monoisotopicMassDelta = cMod->monoisotopicMassDelta = 229.1629;
    nMod->avgMassDelta = cMod->avgMassDelta = 229.2634;
    nMod->set(UNIMOD_TMT6plex);
    cMod->set(UNIMOD_TMT6plex);

    Peptide tmtPeptide;
    tmtPeptide.peptideSequence = "ELVISLIVESK";
    tmtPeptide.modification.push_back(ModificationPtr(nMod));
    tmtPeptide.modification.push_back(ModificationPtr(cMod));
    
    proteome::Peptide tmtProteomePeptide = peptide(tmtPeptide);
    unit_assert_operator_equal("ELVISLIVESK", tmtProteomePeptide.sequence());
    unit_assert_operator_equal(2, tmtProteomePeptide.modifications().size());
    unit_assert_operator_equal(1, tmtProteomePeptide.modifications().count(ModificationMap::NTerminus()));
    unit_assert_equal(229.1629, tmtProteomePeptide.modifications().find(ModificationMap::NTerminus())->second.monoisotopicDeltaMass(), 1e-4);
    unit_assert_operator_equal(1, tmtProteomePeptide.modifications().count(10));
    unit_assert_equal(229.1629, tmtProteomePeptide.modifications().find(10)->second.monoisotopicDeltaMass(), 1e-4);
}

void testCleavageAgent()
{
    {
        Enzyme ez;
        ez.enzymeName.set(MS_Trypsin_P);
        unit_assert_operator_equal(MS_Trypsin_P, cleavageAgent(ez));
    }

    {
        Enzyme ez;
        ez.enzymeName.userParams.push_back(UserParam("trypsin/p"));
        unit_assert_operator_equal(MS_Trypsin_P, cleavageAgent(ez));
    }

    {
        Enzyme ez;
        ez.name = "trypsin/p";
        unit_assert_operator_equal(MS_Trypsin_P, cleavageAgent(ez));
    }

    {
        Enzyme ez;
        ez.siteRegexp = "(?<=[KR])(?!P)";
        unit_assert_operator_equal(MS_Trypsin, cleavageAgent(ez));
    }
}


int main(int argc, char** argv)
{
    TEST_PROLOG(argc, argv)

    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "MzIdentMLTest\n";

    try
    {
        testDigestedPeptides();
        testSnapModifications();
        testConversion();
        testCleavageAgent();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
