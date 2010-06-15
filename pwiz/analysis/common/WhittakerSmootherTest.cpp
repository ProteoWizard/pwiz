//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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


#include "WhittakerSmoother.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


const double testArrayX[] =
{
    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
    15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
    29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42,
    43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56
};

const double testArrayY[] =
{
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90
};


void test()
{
    // test that invalid value exceptions are thrown

    // lambda too low
    unit_assert_throws_what(WhittakerSmoother(1), runtime_error, \
        "[WhittakerSmoother::ctor()] Invalid value for lamda coefficient; valid range is [2, infinity)");

    WhittakerSmoother(100001); // lambda is valid up to numeric limits

    vector<double> testX(testArrayX, testArrayX+(14*4));
    vector<double> testY(testArrayY, testArrayY+(14*4));

    if (os_)
    {
        *os_ << "Unsmoothed data (" << testY.size() << "):\t";
        copy(testY.begin(), testY.end(), ostream_iterator<double>(*os_, "\t"));
        *os_ << endl;
    }

    WhittakerSmoother smoother(10);
    vector<double> smoothedX, smoothedY;
    smoother.smooth(testX, testY, smoothedX, smoothedY);

    if (os_)
    {
        *os_ << "Smoothed data (" << smoothedY.size() << "):\t";
        copy(smoothedY.begin(), smoothedY.end(), ostream_iterator<double>(*os_, "\t"));
        *os_ << endl;
    }

    // smoothed data should be same size as the unsmoothed data
    //unit_assert(smoothData.size() == testY.size());

    // TODO: add output testing
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
    }
    
    return 1;
}
