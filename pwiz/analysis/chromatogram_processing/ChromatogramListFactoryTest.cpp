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


#include "ChromatogramListFactory.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testUsage()
{
    if (os_) *os_ << "ChromatogramListFactory::usage():\n" <<  ChromatogramListFactory::usage() << endl;
}


void testWrap()
{
    MSData msd;
    examples::initializeTiny(msd);

    ChromatogramListPtr& sl = msd.run.chromatogramListPtr;

    unit_assert(sl.get());
    unit_assert_operator_equal(2, sl->size());

    // CompassXtract and pwiz data processing
    unit_assert_operator_equal(2, msd.allDataProcessingPtrs().size());
    unit_assert_operator_equal(1, msd.allDataProcessingPtrs()[1]->processingMethods.size());

    // make sure we can handle config file lines copied from commandline
    // with quotes intact
    ChromatogramListFactory::wrap(msd, "'index [1,1]'");
    unit_assert_operator_equal(1, sl->size());
    unit_assert_operator_equal("sic", sl->chromatogramIdentity(0).id);

    unit_assert_operator_equal(2, msd.allDataProcessingPtrs().size());
    unit_assert_operator_equal(1, msd.allDataProcessingPtrs()[1]->processingMethods.size());
}

/*void testWrapPolarity()
{
    // test filter by positive polarity
    {
        MSData msd;
        examples::initializeTiny(msd);

        ChromatogramListPtr& sl = msd.run.chromatogramListPtr;
        unit_assert(sl.get());
        unit_assert(sl->size() == 5);

        ChromatogramListFactory::wrap(msd, "polarity positive");
        unit_assert(sl->size() == 3);
    }
    // test filter by + polarity
    {
        MSData msd;
        examples::initializeTiny(msd);

        ChromatogramListPtr& sl = msd.run.chromatogramListPtr;
        unit_assert(sl.get());
        unit_assert(sl->size() == 5);

        ChromatogramListFactory::wrap(msd, "polarity +");
        unit_assert(sl->size() == 3);
    }
    // test filter by negative polarity
    {
        MSData msd;
        examples::initializeTiny(msd);

        ChromatogramListPtr& sl = msd.run.chromatogramListPtr;
        unit_assert(sl.get());
        unit_assert(sl->size() == 5);

        ChromatogramListFactory::wrap(msd, "polarity -");
        unit_assert(sl->size() == 2);
    }
    // test invalid argument
    {
        MSData msd;
        examples::initializeTiny(msd);

        ChromatogramListPtr& sl = msd.run.chromatogramListPtr;
        unit_assert(sl.get());
        unit_assert(sl->size() == 5);
        unit_assert_throws(ChromatogramListFactory::wrap(msd, "polarity UNEXPECTED_INPUT"), runtime_error)
    }
}*/

void test()
{
    testUsage(); 
    testWrap();
    //testWrapPolarity();
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

