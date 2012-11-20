//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "SpectrumInfo.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;
const double epsilon_ = 1e-6;


void test()
{
    if (os_) *os_ << "test()\n"; 

    MSData tiny;
    examples::initializeTiny(tiny);

    SpectrumInfo info;
    info.update(*tiny.run.spectrumListPtr->spectrum(0));
    
    unit_assert(info.index == 0);
    unit_assert(info.id == "scan=19");
    unit_assert(info.scanNumber == 19);
    unit_assert(info.massAnalyzerType == MS_QIT);
    unit_assert(info.msLevel == 1);
    unit_assert_equal(info.retentionTime, 353.43, epsilon_);
    unit_assert_equal(info.mzLow, 400.39, epsilon_);
    unit_assert_equal(info.mzHigh, 1795.56, epsilon_);
    unit_assert(info.precursors.empty());

    info.update(*tiny.run.spectrumListPtr->spectrum(0), true);
    unit_assert(info.data.size() == 15);

    info.update(*tiny.run.spectrumListPtr->spectrum(0), false);
    unit_assert(info.data.size() == 0);
    unit_assert(info.data.capacity() == 0);

    info.update(*tiny.run.spectrumListPtr->spectrum(1), true);
    unit_assert(info.index == 1);
    unit_assert(info.id == "scan=20");
    unit_assert(info.scanNumber == 20);
    unit_assert(info.massAnalyzerType == MS_QIT);
    unit_assert(info.msLevel == 2);
    unit_assert_equal(info.retentionTime, 359.43, epsilon_);
    unit_assert_equal(info.mzLow, 320.39, epsilon_);
    unit_assert_equal(info.mzHigh, 1003.56, epsilon_);
    unit_assert(info.precursors.size() == 1);
    unit_assert(info.precursors[0].index == 0);
    unit_assert_equal(info.precursors[0].mz, 445.34, epsilon_);
    unit_assert_equal(info.precursors[0].intensity, 120053, epsilon_);
    unit_assert(info.precursors[0].charge == 2);
    unit_assert(info.data.size() == 10);

    info.clearBinaryData();
    unit_assert(info.data.size() == 0);
    unit_assert(info.data.capacity() == 0);

    if (os_) *os_ << "ok\n";
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

