//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2018 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
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

#include "pwiz/utility/misc/unit.hpp"
#include "ChromatogramList_triMS5.hpp"
#include "Serializer_triMS5.hpp"
#include "examples.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


ostream* os_ = 0;
const char* testFilename = "ChromatogramList_triMS5_Test.triMS5";


void test()
{
	{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSDataFile::WriteConfig writeConfig;
    Serializer_triMS5 serializer(writeConfig);

    IterationListenerRegistry ilr;
    serializer.write(testFilename, tiny, &ilr);

    MSData dummy;
    serializer.read(testFilename, dummy);

    // so we don't have any dangling references
    //dummy.instrumentPtrs.push_back(InstrumentPtr(new Instrument("LCQ_Deca")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("pwiz_processing")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("CompassXtract processing")));

    ChromatogramListPtr cl = dummy.run.chromatogramListPtr;

    // check easy functions

    unit_assert(cl.get());
    unit_assert(cl->size() == 2);
    unit_assert(cl->find("tic") == 0);
    unit_assert(cl->find("sic") == 1);

    // check tic

    ChromatogramPtr c = cl->chromatogram(0); // read without binary data
    unit_assert(c.get());
    unit_assert(c->id == "tic");
    unit_assert(c->binaryDataArrayPtrs.empty());

    unit_assert(cl->chromatogramIdentity(0).index == 0);
    unit_assert(cl->chromatogramIdentity(0).id == "tic");

    c = cl->chromatogram(0, true); // read with binary data

    vector < TimeIntensityPair > pairs;
    c->getTimeIntensityPairs(pairs);
    unit_assert(pairs.size() == 4);

	//check values
	vector<double> c_time { 5.8905, 5.9905, 6.5, 42.05 };
	vector<double> c_inten{ 120.0, 110.0, 110.0, 120.0 };

    for (int i = 0; i < 4; i++)
        unit_assert(pairs[i].time == c_time[i] && pairs[i].intensity == c_inten[i]);

	}
    bfs::remove(testFilename);
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc > 1 && !strcmp(argv[1], "-v")) os_ = &cout;
        test();
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

