//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "IsotopeEnvelopeEstimator.hpp"
#include "IsotopeCalculator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::chemistry;


ostream* os_ = 0;


void testInstantiationWithNull()
{
    try 
    {
        IsotopeEnvelopeEstimator::Config config;
        IsotopeEnvelopeEstimator estimator(config);
    }
    catch (...)
    {
        if (os_) *os_ << "Null IsotopeCalculator* check ok.\n";
        return;
    }

    throw runtime_error("Failed to check for null IsotopeCalculator*.");
}


void test()
{
    const double abundanceCutoff = .01;
    const double massPrecision = .1; 
    IsotopeCalculator isotopeCalculator(abundanceCutoff, massPrecision);

    IsotopeEnvelopeEstimator::Config config;
    config.isotopeCalculator = &isotopeCalculator;

    IsotopeEnvelopeEstimator estimator(config);
 
    if (os_)
        for (int mass=100; mass<=3000; mass+=100)
            *os_ << mass << ":\n" << estimator.isotopeEnvelope(mass) << endl;

    // TODO: external verification of these estimates
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "IsotopeEnvelopeEstimatorTest\n";
        testInstantiationWithNull();
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

