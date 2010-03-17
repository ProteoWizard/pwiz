//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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


#include "MZTolerance.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>
#include <limits>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


const double epsilon_ = numeric_limits<double>::epsilon();


void testMZ()
{
    double x = 1000;
    MZTolerance tolerance(.1);

    x += tolerance;
    unit_assert_equal(x, 1000.1, epsilon_);

    x -= tolerance;
    unit_assert_equal(x, 1000, epsilon_);

    unit_assert_equal(x+tolerance, 1000.1, epsilon_);
    unit_assert_equal(x-tolerance, 999.9, epsilon_);
}


void testPPM()
{
    double x = 1000;
    MZTolerance tolerance(5, MZTolerance::PPM);

    x += tolerance;
    unit_assert_equal(x, 1000.005, epsilon_);

    x -= tolerance;
    const double delta = 1000.005 * 5e-6; // a little more than .005
    unit_assert_equal(x, 1000.005 - delta, epsilon_);

    unit_assert_equal(1000+tolerance, 1000.005, epsilon_);
    unit_assert_equal(1000-tolerance, 999.995, epsilon_);
}


void testIsWithinTolerance()
{
    MZTolerance fiveppm(5, MZTolerance::PPM);
    unit_assert(isWithinTolerance(1000.001, 1000, fiveppm));
    unit_assert(isWithinTolerance(999.997, 1000, fiveppm));
    unit_assert(!isWithinTolerance(1000.01, 1000, fiveppm));
    unit_assert(!isWithinTolerance(999.99, 1000, fiveppm));

    MZTolerance delta(.01);
    unit_assert(isWithinTolerance(1000.001, 1000, delta));
    unit_assert(isWithinTolerance(999.999, 1000, delta));
    unit_assert(!isWithinTolerance(1000.1, 1000, delta));
    unit_assert(!isWithinTolerance(999.9, 1000, .01)); // automatic conversion
}


void testIO()
{
    if (os_) *os_ << "testIO()\n";

    MZTolerance temp;
    if (os_) *os_ << "temp: " << temp << endl; 

    MZTolerance fiveppm(5, MZTolerance::PPM);
    ostringstream oss;
    oss << fiveppm;
    if (os_) *os_ << "fiveppm: " << oss.str() << endl;

    istringstream iss(oss.str());
    iss >> temp;
    if (os_) *os_ << "temp: " << temp << endl; 

    unit_assert(temp == fiveppm);

    MZTolerance blackbirds(4.20, MZTolerance::MZ);
    ostringstream oss2;
    oss2 << blackbirds;
    if (os_) *os_ << "blackbirds: " << oss2.str() << endl;

    istringstream iss2(oss2.str());
    iss2 >> temp;
    if (os_) *os_ << "temp: " << temp << endl; 

    unit_assert(temp == blackbirds);
}


void test()
{
    testMZ();
    testPPM();
    testIsWithinTolerance();
    testIO();
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
}

