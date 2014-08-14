//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, California
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


#include "Std.hpp"
#include "almost_equal.hpp"
#include "pwiz/utility/misc/unit.hpp"


using namespace pwiz::util;


void test_default_float()
{
    float a = 2.0f;
    a += numeric_limits<float>::epsilon();
    a *= 10.0f;
    unit_assert(almost_equal(a, 20.0f));
}


void test_default_double()
{
    double a = 2.0;
    a += numeric_limits<double>::epsilon();
    a *= 10.0;
    unit_assert(almost_equal(a, 20.0));
}


void test_multiplier()
{
    float a = 1.0f;
    a += numeric_limits<float>::epsilon() * 2;
    a *= 10.0f;
    unit_assert(!almost_equal(a, 10.0f));
    unit_assert(almost_equal(a, 10.0f, 2));
}


void test()
{
    test_default_float();
    test_default_double();
    test_multiplier();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
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


