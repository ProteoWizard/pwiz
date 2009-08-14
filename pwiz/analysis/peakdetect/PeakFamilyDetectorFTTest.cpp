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


#include "PeakFamilyDetectorFT.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz;
using namespace pwiz::msdata;


ostream* os_ = 0;


extern double peakFamilyDetectorFTTestData_[];
extern int peakFamilyDetectorFTTestDataSize_;


void test()
{
    // instantiate PeakFamilyDetectorFT

    PeakFamilyDetectorFT::Config config;
    config.log = os_;
    config.cp = CalibrationParameters::thermo_FT();
    PeakFamilyDetectorFT detector(config); 

    // detect 

    vector<PeakFamily> result;
    const MZIntensityPair* begin = 
        reinterpret_cast<const MZIntensityPair*>(&peakFamilyDetectorFTTestData_[0]);
    const MZIntensityPair* end = begin + peakFamilyDetectorFTTestDataSize_/2;

    detector.detect(begin, end, result);

    if (os_)
    {
        *os_ << setprecision(10) << "result: " << result.size() << endl;
        copy(result.begin(), result.end(), ostream_iterator<PeakFamily>(*os_, "\n"));
    }

    unit_assert(result.size() == 1);
    unit_assert_equal(result[0].mzMonoisotopic, 810.4148, .005);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeakFamilyDetectorFTTest\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

