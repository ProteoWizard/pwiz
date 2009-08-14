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


#include "PeakelGrower.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


vector< vector<Peak> > createToyPeaks()
{
    //  rt\mz   1000    1001    1002
    //    0       x               x
    //    1       x       x       x
    //    2               x       x
    //    3       x       x       x
    //    4       x               x

    vector< vector<Peak> > peaks(5);
    Peak peak;

    peak.retentionTime = 0;
    peak.mz = 1000; peaks[0].push_back(peak);
    peak.mz = 1002; peaks[0].push_back(peak);

    peak.retentionTime = 1;
    peak.mz = 1000.01; peaks[1].push_back(peak);
    peak.mz = 1001; peaks[1].push_back(peak);
    peak.mz = 1002.01; peaks[1].push_back(peak);

    peak.retentionTime = 2;
    peak.mz = 1001.01; peaks[2].push_back(peak);
    peak.mz = 1002-.01; peaks[2].push_back(peak);

    peak.retentionTime = 3;
    peak.mz = 1000; peaks[3].push_back(peak);
    peak.mz = 1001-.01; peaks[3].push_back(peak);
    peak.mz = 1002.02; peaks[3].push_back(peak);

    peak.retentionTime = 4;
    peak.mz = 1000.01; peaks[4].push_back(peak);
    peak.mz = 1002-.02; peaks[4].push_back(peak);

    return peaks;
}


void testToyExample()
{
    vector< vector<Peak> > peaks = createToyPeaks();

    PeakelGrower_Proximity::Config config;
    config.mzTolerance = .1;
    config.rtTolerance = 1.5;

    PeakelGrower_Proximity peakelGrower(config);

    PeakelField field;
    peakelGrower.sowPeaks(field, peaks);

    const double epsilon = .1;
    unit_assert(field.size() == 4);

    PeakelField::const_iterator it = field.begin();

    unit_assert_equal((*it)->mz, 1000, epsilon);
    unit_assert_equal((*it)->retentionTime, 0, epsilon);

    ++it;
    unit_assert_equal((*it)->mz, 1000, epsilon);
    unit_assert_equal((*it)->retentionTime, 3, epsilon);
    
    ++it;
    unit_assert_equal((*it)->mz, 1001, epsilon);
    unit_assert_equal((*it)->retentionTime, 1, epsilon);
    
    ++it;
    unit_assert_equal((*it)->mz, 1002, epsilon);
    unit_assert_equal((*it)->retentionTime, 0, epsilon);
}


void test()
{
    testToyExample();
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

