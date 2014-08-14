//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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
#include "SpectrumList_mz5.hpp"
#include "Serializer_mz5.hpp"
#include "examples.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


ostream* os_ = 0;
const char* testFilename = "SpectrumList_mz5_Test.mz5";


void test()
{
    {
    MSData tiny;
    examples::initializeTiny(tiny);

    MSDataFile::WriteConfig writeConfig;
    Serializer_mz5 serializer(writeConfig);

    IterationListenerRegistry ilr;
    serializer.write(testFilename, tiny, &ilr);

    MSData dummy;
    serializer.read(testFilename, dummy);

    dummy.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("tiny1.yep")));
    dummy.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("tiny.wiff")));

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->cvParams.push_back(MS_positive_scan);
    dummy.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->cvParams.push_back(MS_positive_scan);
    dummy.paramGroupPtrs.push_back(pg2);

    // so we don't have any dangling references
    dummy.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(
            new InstrumentConfiguration("LCQ Deca")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing(
            "CompassXtract processing")));

    SpectrumListPtr sl = dummy.run.spectrumListPtr;

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 5);
    unit_assert(sl->find("scan=19") == 0);
    IndexList indexList = sl->findNameValue("scan", "19");
    unit_assert(indexList.size() == 1 && indexList[0] == 0);
    unit_assert(sl->find("scan=20") == 1);
    indexList = sl->findNameValue("scan", "20");
    unit_assert(indexList.size() == 1 && indexList[0] == 1);
    unit_assert(sl->find("scan=21") == 2);
    indexList = sl->findNameValue("scan", "21");
    unit_assert(indexList.size() == 1 && indexList[0] == 2);
    unit_assert(sl->find("sample=1 period=1 cycle=23 experiment=1") == 4);
    indexList = sl->findNameValue("sample", "1");
    unit_assert(indexList.size() == 1 && indexList[0] == 4);
    indexList = sl->findNameValue("period", "1");
    unit_assert(indexList.size() == 1 && indexList[0] == 4);
    indexList = sl->findNameValue("cycle", "23");
    unit_assert(indexList.size() == 1 && indexList[0] == 4);
    indexList = sl->findNameValue("experiment", "1");
    unit_assert(indexList.size() == 1 && indexList[0] == 4);

    unit_assert(sl->findSpotID("A1").empty());
    IndexList spotIndexList = sl->findSpotID("A1,42x42,4242x4242");
    unit_assert(spotIndexList.size() == 1);
    unit_assert(spotIndexList[0] == 4);

    // check scan 19

    SpectrumPtr s = sl->spectrum(0); // read without binary data
    unit_assert(s.get());
    unit_assert(s->id == "scan=19");
    unit_assert(s->spotID.empty());
    unit_assert(s->cvParam(MS_ms_level).valueAs<int> () == 1);
    unit_assert(s->binaryDataArrayPtrs.empty());

    unit_assert(sl->spectrumIdentity(0).index == 0);
    unit_assert(sl->spectrumIdentity(0).id == "scan=19");
    unit_assert(sl->spectrumIdentity(0).spotID.empty());

    s = sl->spectrum(0, true); // read with binary data

    vector < MZIntensityPair > pairs;
    s->getMZIntensityPairs(pairs);
    unit_assert(pairs.size() == 15);
    for (int i = 0; i < 15; i++)
        unit_assert(pairs[i].mz == i && pairs[i].intensity == 15 - i);

    unit_assert(s->scanList.scans.size() == 1);
    unit_assert(s->paramGroupPtrs.size() == 1);
    unit_assert(s->paramGroupPtrs.back()->id == "CommonMS1SpectrumParams");
    unit_assert(s->paramGroupPtrs.back()->cvParams.size() == 2);

    // check scan 20

    s = sl->spectrum(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "scan=20");
    unit_assert(s->spotID.empty());
    unit_assert(s->cvParam(MS_ms_level).valueAs<int> () == 2);

    unit_assert(sl->spectrumIdentity(1).index == 1);
    unit_assert(sl->spectrumIdentity(1).id == "scan=20");
    unit_assert(sl->spectrumIdentity(1).spotID.empty());

    pairs.clear();
    s->getMZIntensityPairs(pairs);
    unit_assert(pairs.size() == 10);
    for (int i = 0; i < 10; i++)
        unit_assert(pairs[i].mz == 2 * i && pairs[i].intensity == (10 - i) * 2);

    unit_assert(s->scanList.scans.size() == 1);
    unit_assert(s->paramGroupPtrs.size() == 1);
    unit_assert(s->paramGroupPtrs.back()->id == "CommonMS2SpectrumParams");
    unit_assert(s->paramGroupPtrs.back()->cvParams.size() == 2);

    // check scan 22 (MALDI)
    s = sl->spectrum(4, true);
    unit_assert(s.get());
    unit_assert(s->id == "sample=1 period=1 cycle=23 experiment=1");
    unit_assert(s->spotID == "A1,42x42,4242x4242");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int> () == 1);

    unit_assert(sl->spectrumIdentity(4).index == 4);
    unit_assert(sl->spectrumIdentity(4).id == "sample=1 period=1 cycle=23 experiment=1");
    unit_assert(sl->spectrumIdentity(4).spotID == "A1,42x42,4242x4242");

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

