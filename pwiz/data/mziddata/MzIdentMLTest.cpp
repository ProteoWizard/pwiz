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
#include "MzIdentML.hpp"
#include "Serializer_mzid.hpp"
#include "examples.hpp"
#include "Diff.hpp"


using namespace pwiz::mziddata;
using namespace pwiz::mziddata::examples;
using namespace pwiz::util;


ostream* os_;


void testCreation()
{
    MzIdentML mzid;
    initializeTiny(mzid);

    Serializer_mzIdentML ser;
    ostringstream oss;
    ser.write(oss, mzid);

    if (os_) *os_ << oss.str() << endl;

    MzIdentML mzid2;
    boost::shared_ptr<istream> iss(new istringstream(oss.str()));
    ser.read(iss, mzid2);
    Diff<MzIdentML, DiffConfig> diff(mzid, mzid2);

    if (os_ && diff) *os_ << diff << endl;
    unit_assert(!diff);
}

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
        vector<DigestedPeptide> result2_rank1_digestedPeptides = result2_rank1->digestedPeptides(*sip);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 423);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 2);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "K");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "A");

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = result2_rank2->digestedPeptides(*sip);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
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
        vector<DigestedPeptide> result2_rank1_digestedPeptides = result2_rank1->digestedPeptides(*sip);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = result2_rank2->digestedPeptides(*sip);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
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
        vector<DigestedPeptide> result2_rank1_digestedPeptides = result2_rank1->digestedPeptides(*sip);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 0);
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());

        // result 2 rank 2: K.RNSTIPT.K
        SpectrumIdentificationItemPtr result2_rank2 = result2->spectrumIdentificationItem[1];
        vector<DigestedPeptide> result2_rank2_digestedPeptides = result2_rank2->digestedPeptides(*sip);
        unit_assert(result2_rank2_digestedPeptides.size() == 2);

        // both PeptideEvidences have the same values
        for (int i=0; i < 2; ++i)
        {
            unit_assert(result2_rank2_digestedPeptides[i].missedCleavages() == 0);
            unit_assert(result2_rank2_digestedPeptides[i].specificTermini() == 1);
            unit_assert(!result2_rank2_digestedPeptides[i].NTerminusIsSpecific());
            unit_assert(result2_rank2_digestedPeptides[i].CTerminusIsSpecific());
        }
    }

    {
        // result 2 rank 1: K.QTQTFTTYSDNQPGVLIQVYEGER.A
        
        SpectrumIdentificationItemPtr result2_rank1 = result2->spectrumIdentificationItem[0];

        // move it to the N terminus
        result2_rank1->peptideEvidence[0]->start = 1;
        result2_rank1->peptideEvidence[0]->pre = "-";

        vector<DigestedPeptide> result2_rank1_digestedPeptides = result2_rank1->digestedPeptides(*sip);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(!result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "-");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "A");

        // move it to the C terminus
        result2_rank1->peptideEvidence[0]->start = 618;
        result2_rank1->peptideEvidence[0]->pre = "K";
        result2_rank1->peptideEvidence[0]->post = "-";

        result2_rank1_digestedPeptides = result2_rank1->digestedPeptides(*sip);
        unit_assert(result2_rank1_digestedPeptides.size() == 1);
        unit_assert(result2_rank1_digestedPeptides[0].offset() == 617);
        unit_assert(result2_rank1_digestedPeptides[0].missedCleavages() == 0);
        unit_assert(result2_rank1_digestedPeptides[0].specificTermini() == 1);
        unit_assert(!result2_rank1_digestedPeptides[0].NTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusIsSpecific());
        unit_assert(result2_rank1_digestedPeptides[0].NTerminusPrefix() == "K");
        unit_assert(result2_rank1_digestedPeptides[0].CTerminusSuffix() == "-");
    }
}


int main(int argc, char** argv)
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "MzIdentMLTest\n";

    try
    {
        testCreation();
        testDigestedPeptides();
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
