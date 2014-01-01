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


#include "ChromatogramList_mzML.hpp"
#include "Serializer_mzML.hpp" // depends on Serializer_mzML::write() only
#include "examples.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


ostream* os_ = 0;


void test(bool indexed)
{
    if (os_) *os_ << "test(): indexed=\"" << boolalpha << indexed << "\"\n";

    MSData tiny;
    examples::initializeTiny(tiny);

    Serializer_mzML::Config config;
    config.indexed = indexed;
    Serializer_mzML serializer(config);  

    ostringstream oss;
    serializer.write(oss, tiny);

    if (os_) *os_ << "oss:\n" << oss.str() << endl;

    shared_ptr<istream> is(new istringstream(oss.str()));

    // dummy would normally be read in from file
  
    MSData dummy;

    // so we don't have any dangling references
    //dummy.instrumentPtrs.push_back(InstrumentPtr(new Instrument("LCQ_Deca")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("pwiz_processing")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("CompassXtract processing")));

    Index_mzML_Ptr index(new Index_mzML(is, dummy));
    ChromatogramListPtr sl = ChromatogramList_mzML::create(is, dummy, index);

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 2);
    unit_assert(sl->find("tic") == 0);
    unit_assert(sl->find("sic") == 1);

    // check tic

    ChromatogramPtr s = sl->chromatogram(0); // read without binary data
    unit_assert(s.get());
    unit_assert(s->id == "tic");
    unit_assert(s->binaryDataArrayPtrs.empty());

    unit_assert(sl->chromatogramIdentity(0).index == 0);
    unit_assert(sl->chromatogramIdentity(0).id == "tic");

    s = sl->chromatogram(0, true); // read with binary data

    vector<TimeIntensityPair> pairs;
    s->getTimeIntensityPairs(pairs);
    unit_assert(pairs.size() == 15);
    for (int i=0; i<15; i++)
        unit_assert(pairs[i].time==i && pairs[i].intensity==15-i);

    // check sic

    s = sl->chromatogram(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "sic");

    unit_assert(sl->chromatogramIdentity(1).index == 1);
    unit_assert(sl->chromatogramIdentity(1).id == "sic");

    pairs.clear();
    s->getTimeIntensityPairs(pairs);
    unit_assert(pairs.size() == 10);
    for (int i=0; i<10; i++)
        unit_assert(pairs[i].time==i && pairs[i].intensity==(10-i));
}


void test()
{
    bool indexed = true;
    test(indexed);

    indexed = false;
    test(indexed);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
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


