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


#include "SavitzkyGolaySmoother.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


const double testArray[] =
{
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90,
    1, 15, 29, 20, 10, 40, 1, 50, 3, 40, 3, 25, 23, 90
};


void test()
{
    vector<double> testData(testArray, testArray+(14*4));
    if (os_)
    {
        *os_ << "Unsmoothed data (" << testData.size() << "):\t";
        copy(testData.begin(), testData.end(), ostream_iterator<double>(*os_, "\t"));
        *os_ << endl;
    }

    vector<double> smoothData = SavitzkyGolaySmoother<double>::smooth_copy(testData);

    if (os_)
    {
        *os_ << "Smoothed data (" << smoothData.size() << "):\t";
        copy(smoothData.begin(), smoothData.end(), ostream_iterator<double>(*os_, "\t"));
        *os_ << endl;
    }

    unit_assert(smoothData.size() == testData.size());
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
