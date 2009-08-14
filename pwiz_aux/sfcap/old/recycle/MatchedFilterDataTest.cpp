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


#include "MatchedFilterData.hpp"
#include "data/FrequencyData.hpp"
#include "extstd/unit.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::extstd;
using namespace pwiz::data;


class SimpleKernel : public MatchedFilterData::Kernel
{
    // upside-down abs, vertex (0,1.5), clamped at 0    

    public:

    virtual complex<double> operator()(double frequency) const
    {
        return max(1.5-abs(frequency), 0.); 
    }
};


void test()
{
    FrequencyData fd;
    for (double frequency=0; frequency<=10; frequency+=1.)
    {
        double intensity = (frequency==5 || frequency==6) ? 1 : 0; //      ..
        fd.data().push_back(FrequencyDatum(frequency, intensity)); // .....  ....
    }
    fd.observationDuration(1);
    fd.analyze();

    SimpleKernel k;
    const int sampleMultiple = 4;
    const int sampleRadius = 2;
    MatchedFilterData mfd(fd, k, sampleMultiple, sampleRadius); 
    mfd.compute();

    mfd.printFilters();
    mfd.printCorrelationMatrix();

    vector<double> peaks;
    mfd.findPeaks(.5, 10, peaks);
    cout << "peaks found: " << peaks.size() << endl;
    copy(peaks.begin(), peaks.end(), ostream_iterator<double>(cout, "\n"));

    unit_assert(peaks.size() == 1);
    unit_assert(peaks[0] == 5.5);
    unit_assert_equal(norm(mfd.correlationValue(5.5)), 2, 1e-15);
}


void testZeroObservationDuration()
{
    FrequencyData fd;
    for (double frequency=0; frequency<=10; frequency+=1.)
    {
        double intensity = (frequency==5 || frequency==6) ? 1 : 0; //      ..
        fd.data().push_back(FrequencyDatum(frequency, intensity)); // .....  ....
    }
    fd.observationDuration(0);
    fd.analyze();

    SimpleKernel k;
    const int sampleMultiple = 4;
    const int sampleRadius = 2;
    MatchedFilterData mfd(fd, k, sampleMultiple, sampleRadius); 
    mfd.compute();

    vector<double> peaks;
    mfd.findPeaks(.5, 10, peaks);

    unit_assert(peaks.size() == 0);
    cout << "T == 0 test ok.\n";
}


int main()
{
    try
    {
        cerr << "MatchedFilterDataTest\n";
        test();
        testZeroObservationDuration();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

