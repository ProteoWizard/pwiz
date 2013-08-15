//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "RecalibratorSimple.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::pdanalysis;


ostream* os_ = 0;


Scan createScan()
{
    // create a scan by setting peak frequencies, but not m/z values

    Scan scan;

    PeakFamily pf0;
    pf0.peaks.resize(3);
    pf0.peaks[0].frequency = 1;
    pf0.peaks[1].frequency = 10;
    pf0.peaks[2].frequency = 100;

    PeakFamily pf1;
    pf1.peaks.resize(3);
    pf1.peaks[0].frequency = 10;
    pf1.peaks[1].frequency = 100;
    pf1.peaks[2].frequency = 1000;

    scan.peakFamilies.push_back(pf0);
    scan.peakFamilies.push_back(pf1);

    return scan;
}


void test()
{
    Scan scan = createScan();
    if (os_) *os_ << "before: " << scan << endl; 

    // recalibrate
    RecalibratorSimple rs(CalibrationParameters(1e4,0));
    rs.recalibrate(scan);

    // verify m/z values

    const double epsilon_ = 1e-15;
    unit_assert(scan.peakFamilies.size() == 2);

    const PeakFamily& pf0 = scan.peakFamilies[0];
    unit_assert_equal(pf0.mzMonoisotopic, 1e4, epsilon_);
    unit_assert(pf0.peaks.size() == 3);
    unit_assert_equal(pf0.peaks[0].mz, 1e4, epsilon_);
    unit_assert_equal(pf0.peaks[1].mz, 1e3, epsilon_);
    unit_assert_equal(pf0.peaks[2].mz, 1e2, epsilon_);

    const PeakFamily& pf1 = scan.peakFamilies[1];
    unit_assert_equal(pf1.mzMonoisotopic, 1e3, epsilon_);
    unit_assert(pf1.peaks.size() == 3);
    unit_assert_equal(pf1.peaks[0].mz, 1e3, epsilon_);
    unit_assert_equal(pf1.peaks[1].mz, 1e2, epsilon_);
    unit_assert_equal(pf1.peaks[2].mz, 1e1, epsilon_);

    if (os_) *os_ << "after: " << scan << endl; 
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

