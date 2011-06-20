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
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/common/Unimod.hpp"
#include "MzIdentML.hpp"
#include "Serializer_mzid.hpp"
#include "examples.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"


using namespace pwiz::mziddata;
using namespace pwiz::mziddata::examples;
using namespace pwiz::util;
using namespace pwiz::data;
namespace proteome = pwiz::proteome;


ostream* os_;


void testDigestedPeptides()
{
    using namespace pwiz::proteome;

    MzIdentML mzid;
    initializeBasicSpectrumIdentification(mzid);

    SpectrumIdentificationProtocolPtr sip = mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];
    SpectrumIdentificationListPtr sil = mzid.dataCollection.analysisData.spectrumIdentificationList[0];

    SpectrumIdentificationResultPtr result2 = sil->spectrumIdentificationResult[1];

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 423);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 2);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "K");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "A");

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(result2_rank2_digestedPeptides[i] == digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]));
            unit_assert(result2_rank2_digestedPeptides[i].offset() == 415);
            unit_assert(result2_rank2_digestedPeptides[i].missedCleavages() == 1);
            unit_assert(result2_rank2_digestedPeptides[i].specificTermini() == 1);
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(!result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusPrefix() == "K");
            unit_assert(result2_rank2_digestedPeptides[i].CTerminusSuffix() == "K");
        }
    }

    // change enzyme from trypsin to Lys-C and test again
    sip->enzymes.enzymes[0]->siteRegexp = "(?<=K)";

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(result2_rank2_digestedPeptides[i] == digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]));
            unit_assert(result2_rank2_digestedPeptides[i].missedCleavages() == 0);
            unit_assert(result2_rank2_digestedPeptides[i].specificTermini() == 1);
            unit_assert(result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(!result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    // change enzyme from Lys-C to Lys-N and test again
    sip->enzymes.enzymes[0]->siteRegexp = "(?=K)";

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 0);
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = digestedPeptides(*sip, *result2_rank2);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(result2_rank2_digestedPeptides[i] == digestedPeptide(*sip, *result2_rank2->peptideEvidencePtr[i]));
            unit_assert(result2_rank2_digestedPeptides[i].missedCleavages() == 0);
            unit_assert(result2_rank2_digestedPeptides[i].specificTermini() == 1);
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
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 617);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "K");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "-");

        // move it to the N terminus
        result2_rank1->peptideEvidencePtr[0]->start = 1;
        result2_rank1->peptideEvidencePtr[0]->pre = '-';
        result2_rank1->peptideEvidencePtr[0]->post = 'A';

        result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "-");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "A");
    }

    // change enzyme back to Lys-C
    sip->enzymes.enzymes[0]->siteRegexp = "(?<=K)";

    // now test with multiple independent enzymes (Lys-C and Arg-C)
    sip->enzymes.enzymes.push_back(EnzymePtr(new Enzyme("ENZ_2")));
    sip->enzymes.enzymes[1]->nTermGain = sip->enzymes.enzymes[0]->nTermGain;
    sip->enzymes.enzymes[1]->cTermGain = sip->enzymes.enzymes[0]->cTermGain;
    sip->enzymes.enzymes[1]->semiSpecific = sip->enzymes.enzymes[0]->semiSpecific;
    sip->enzymes.enzymes[1]->missedCleavages = sip->enzymes.enzymes[0]->missedCleavages;
    sip->enzymes.enzymes[1]->minDistance = sip->enzymes.enzymes[0]->minDistance;
    sip->enzymes.enzymes[1]->siteRegexp = "(?<=R)";

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // reassign the original prefix residue
        result2_rank1->peptideEvidencePtr[0]->pre = 'K';

        // there are two semi-specific peptides, one cut by Lys-C and the other cut by Arg-C;
        // only the first one will be returned because they have the same "best specificity"

        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
    }

    // now test with multiple simultaneous enzymes (Lys-C and Arg-C)
    sip->enzymes.independent = false;
    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A

        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // both termini are specific now, one cut from each enzyme
        vector<DigestedPeptide> result2_rank1_digestedPeptides = digestedPeptides(*sip, *result2_rank1);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0] == digestedPeptide(*sip, *result2_rank1->peptideEvidencePtr[0]));
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 2);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
    }
}

void testSnapModifications()
{
    MzIdentML mzid, mzid2;
    initializeBasicSpectrumIdentification(mzid);
    initializeBasicSpectrumIdentification(mzid2);

    BOOST_FOREACH(SpectrumIdentificationProtocolPtr& sip, mzid2.analysisProtocolCollection.spectrumIdentificationProtocol)
    BOOST_FOREACH(SearchModificationPtr& mod, sip->modificationParams)
        mod->cvParams.clear();

    BOOST_FOREACH(PeptidePtr& pep, mzid2.sequenceCollection.peptides)
    BOOST_FOREACH(ModificationPtr& mod, pep->modification)
        mod->cvParams.clear();

    Diff<MzIdentML, DiffConfig> diff(mzid, mzid2);
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

    MzIdentML mzid;
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
}

void testSpectraDataFileFormat()
{
    {ofstream fs("testSpectraDataFile.mzedML"); fs << "<?xml?><mzML>";}
    unit_assert_operator_equal(MS_mzML_file, spectraDataFileFormat("testSpectraDataFile.mzedML"));
    bfs::remove("testSpectraDataFile.mzedML");

    {ofstream fs("testSpectraDataFile.mzedXML"); fs << "<?xml?><mzXML>";}
    unit_assert_operator_equal(MS_ISB_mzXML_file, spectraDataFileFormat("testSpectraDataFile.mzedXML"));
    bfs::remove("testSpectraDataFile.mzedXML");

    {ofstream fs("testSpectraDataFile.mGF"); fs << "MGF";}
    unit_assert_operator_equal(MS_Mascot_MGF_file, spectraDataFileFormat("testSpectraDataFile.mGF"));
    bfs::remove("testSpectraDataFile.mGF");
    
    {ofstream fs("testSpectraDataFile.Ms2"); fs << "MS2";}
    unit_assert_operator_equal(MS_MS2_file, spectraDataFileFormat("testSpectraDataFile.Ms2"));
    bfs::remove("testSpectraDataFile.Ms2");
    
    {ofstream fs("testSpectraDataFile.wiFF"); fs << "WIFF";}
    unit_assert_operator_equal(MS_ABI_WIFF_file, spectraDataFileFormat("testSpectraDataFile.wiFF"));
    bfs::remove("testSpectraDataFile.wiFF");

    {ofstream fs("_FUNC42.DAT"); fs << "Life, the Universe, and Everything";}
    unit_assert_operator_equal(MS_Waters_raw_file, spectraDataFileFormat("."));
    bfs::remove("_FUNC42.DAT");
}


int main(int argc, char** argv)
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "MzIdentMLTest\n";

    try
    {
        testDigestedPeptides();
        testSnapModifications();
        testConversion();
        testSpectraDataFileFormat();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }

    return 1;
}
