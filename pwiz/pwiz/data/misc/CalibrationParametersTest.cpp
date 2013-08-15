//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#include "CalibrationParameters.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_ = 0;


void test()
{
    CalibrationParameters p = CalibrationParameters::thermo_FT();
    CalibrationParameters q(0,1);

    unit_assert(p!=q);
    q.A = thermoA_FT_;
    q.B = thermoB_FT_;
    unit_assert(p==q);

    double dummy = 420;
    double epsilon = 1e-13;
    unit_assert_equal(dummy, p.mz(p.frequency(dummy)), epsilon);
    unit_assert_equal(dummy, p.frequency(p.mz(dummy)), epsilon);

    CalibrationParameters p2 = CalibrationParameters::thermo_Orbitrap();
    unit_assert_equal(dummy, p2.mz(p2.frequency(dummy)), epsilon);
    unit_assert_equal(dummy, p2.frequency(p2.mz(dummy)), epsilon);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "CalibrationParametersTest\n";
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

