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


#include "RecalibratorKnownMassList.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <algorithm>
#include <functional>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::pdanalysis;


ostream* os_ = 0;

inline bool hasLesserMZMonoisotopic(const PeakFamily& a, const PeakFamily& b)
{
    return a.mzMonoisotopic < b.mzMonoisotopic;
}


class SetFrequency
{
    public:
    SetFrequency(const CalibrationParameters& cp) : cp_(cp) {}
    
    void operator()(PeakFamily& pf)
    {
        Peak peak;
        peak.mz = pf.mzMonoisotopic;
        peak.frequency = cp_.frequency(peak.mz);
        pf.peaks.push_back(peak);
    }

    private:
    CalibrationParameters cp_;
};


Scan createTestScan()
{
    Scan scan;

    // set monoisotopic m/z values

    PeakFamily f;
    f.mzMonoisotopic = 1046.53;
    scan.peakFamilies.push_back(f);

    f.mzMonoisotopic = 810.42; 
    scan.peakFamilies.push_back(f);

    f.mzMonoisotopic = 449.91;
    scan.peakFamilies.push_back(f);

    f.mzMonoisotopic = 1672.91;
    scan.peakFamilies.push_back(f);

    f.mzMonoisotopic = 441.72; 
    scan.peakFamilies.push_back(f);

    f.mzMonoisotopic = 666;
    scan.peakFamilies.push_back(f);

    sort(scan.peakFamilies.begin(), scan.peakFamilies.end(), hasLesserMZMonoisotopic);

    // set frequencies based on the monoisotopic m/z values

    scan.calibrationParameters = CalibrationParameters::thermo_FT(); 
    for_each(scan.peakFamilies.begin(), scan.peakFamilies.end(), 
             SetFrequency(scan.calibrationParameters)); 

    return scan;
}


void test()
{
    Scan scan = createTestScan();

    KnownMassList kml;
    kml.insert_5pep();

    KnownMassList::MatchResult matchResultBefore = kml.match(scan, 100); 

    if (os_) *os_ << setprecision(10) << "scan before:\n" << scan << endl; 
    if (os_) *os_ << "match result before:\n" << matchResultBefore << endl;

    RecalibratorKnownMassList rkml(kml); 
    rkml.recalibrate(scan);

    KnownMassList::MatchResult matchResultAfter = kml.match(scan, 100); 

    if (os_) *os_ << "scan after:\n" << scan << endl; 
    if (os_) *os_ << "match result after:\n" << matchResultAfter << endl;

    unit_assert(matchResultBefore.dmz2Mean > matchResultAfter.dmz2Mean);
    unit_assert(matchResultBefore.dmzRel2Mean > matchResultAfter.dmzRel2Mean);
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

