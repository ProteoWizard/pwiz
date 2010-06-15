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


#include "PeakFitter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::math;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


void testParabola()
{
    if (os_) *os_ << "testParabola()\n";

    const double center = 2.1;
    const double height = 5;

    vector<OrderedPair> pairs;
    for (double i=0; i<5; i++)
        pairs.push_back(OrderedPair(i, height-(i-center)*(i-center))); // sampled parabola

    PeakFitter_Parabola::Config config;
    config.windowRadius = 1;

    PeakFitter_Parabola fitter(config);
    Peak peak;

    fitter.fitPeak(pairs, 2, peak);

    if (os_) 
    {
        *os_ << peak;
        copy(peak.data.begin(), peak.data.end(), ostream_iterator<OrderedPair>(*os_, " "));
        *os_ << endl;
    }

    const double epsilon = 1e-6;
    unit_assert_equal(peak.mz, center, epsilon);
    unit_assert_equal(peak.intensity, 5, epsilon);
    unit_assert_equal(peak.area, 12.97, epsilon);
    unit_assert_equal(peak.error, 0, epsilon);
    unit_assert_equal(peak.intensity, 5, epsilon);
    unit_assert(!peak.data.empty());
}


void testMultiplePeaks()
{
    if (os_) *os_ << "testMultiplePeaks()\n";

    const double center = 2.1;
    const double height = 5;

    vector<OrderedPair> pairs;
    for (double i=0; i<5; i++)
        pairs.push_back(OrderedPair(i, height-(i-center)*(i-center))); // sampled parabola
    for (double i=0; i<5; i++)
        pairs.push_back(OrderedPair(i+5, height-(i-center)*(i-center))); // sampled parabola
    for (double i=0; i<5; i++)
        pairs.push_back(OrderedPair(i+10, height-(i-center)*(i-center))); // sampled parabola

    vector<size_t> indices;
    indices.push_back(2);
    indices.push_back(7);
    indices.push_back(12);

    PeakFitter_Parabola fitter;
    vector<Peak> peaks;

    fitter.fitPeaks(pairs, indices, peaks);

    if (os_) 
    {
        for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        {
            *os_ << *it;
            copy(it->data.begin(), it->data.end(), ostream_iterator<OrderedPair>(*os_, " "));
            *os_ << endl;
        }
    }

    const double epsilon = 1e-6;
    unit_assert(peaks.size() == 3);
    unit_assert_equal(peaks[0].mz, center, epsilon);
    unit_assert_equal(peaks[1].mz, center+5, epsilon);
    unit_assert_equal(peaks[2].mz, center+10, epsilon);
}


void test()
{
    testParabola();
    testMultiplePeaks();
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

