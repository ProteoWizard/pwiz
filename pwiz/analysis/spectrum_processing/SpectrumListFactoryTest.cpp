//
// SpectrumListFactoryTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "SpectrumListFactory.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace std;


ostream* os_ = 0;


void testUsage()
{
    if (os_) *os_ << "SpectrumListFactory::usage():\n" <<  SpectrumListFactory::usage() << endl;
}


void testWrap()
{
    MSData msd;
    examples::initializeTiny(msd);

    unit_assert(msd.run.spectrumListPtr.get());
    unit_assert(msd.run.spectrumListPtr->size() > 2);

    SpectrumListFactory::wrap(msd, "scanNumber [19,20]");
    unit_assert(msd.run.spectrumListPtr->size() == 2);

    SpectrumListFactory::wrap(msd, "index [1,1]");
    unit_assert(msd.run.spectrumListPtr->size() == 1);
    unit_assert(msd.run.spectrumListPtr->spectrumIdentity(0).nativeID == "20");
}


void test()
{
    testUsage(); 
    testWrap();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
}

