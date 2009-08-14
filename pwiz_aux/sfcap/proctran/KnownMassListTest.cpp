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


#include "KnownMassList.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>
#include <algorithm>


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


const double masses_[] =
{
    1046.53, // Angiotensin +1
    810.42,  // Bombessin +2
    449.91,  // Substance P +3
    1672.91, // Neurotensin +1
    441.72,  // Alpha1-6 +2
    667,     // Beast +1 
    524.26,  // MRFA +1
    195.08,  // Caffeine +1
    1421.98, // Ultramark(28) +1
};


const unsigned int massesSize_ = sizeof(masses_)/sizeof(double);


Scan createTestScan()
{
    Scan scan;

    for (const double* p=masses_; p!=masses_+massesSize_; ++p)
    {
        PeakFamily f;
        f.mzMonoisotopic = *p; 
        scan.peakFamilies.push_back(f);
    }

    sort(scan.peakFamilies.begin(), scan.peakFamilies.end(), hasLesserMZMonoisotopic);

    return scan;
}


void test_5pep()
{
    if (os_) *os_ << "test_5pep()\n";

    KnownMassList kml;
    kml.insert_5pep();

    if (os_)
    {
        *os_ << setprecision(8);
        *os_ << kml << endl;    
    }

    Scan scan = createTestScan();

    if (os_) *os_ << "scan: " << scan << endl;

    KnownMassList::MatchResult matchResult = kml.match(scan, 100); // ppm

    if (os_)
        *os_ << matchResult << endl; 

    unit_assert(matchResult.matchCount == 5);
}


void test_calmix()
{
    if (os_) *os_ << "test_calmix()\n";

    KnownMassList kml;
    kml.insert_calmix();

    if (os_)
    {
        *os_ << setprecision(8);
        *os_ << kml << endl;    
    }

    Scan scan = createTestScan();

    if (os_) *os_ << "scan: " << scan << endl;

    KnownMassList::MatchResult matchResult = kml.match(scan, 100); // ppm

    if (os_)
        *os_ << matchResult << endl; 

    unit_assert(matchResult.matchCount == 3);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test_5pep();
        test_calmix();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

