// 
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#include "TransientData.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <iostream>
#include <cstring>
#include <fstream>
#include <stdexcept>
#include <iterator>


using namespace std;
using namespace pwiz::data;
using namespace pwiz::util;


ostream* os_ = 0;


const double startTime_ = 0.116800;
const double observationDuration_ = .768;
const double A_ = 1.075339687500000e+008;
const double B_ = -3.454602661132810e+008;
const unsigned int sampleCount_ = 100;


string filename_ = "TransientDataTest_test.dat";
string filenameText_ = "TransientDataTest_test.dat.txt";

 
void createTestTransients()
{
    TransientData td;
    td.startTime(startTime_);
    td.observationDuration(observationDuration_);
    td.A(A_);
    td.B(B_);

    td.data().resize(sampleCount_);
    fill(td.data().begin(), td.data().end(), 1);
    
    td.write(filename_);
    td.write(filenameText_, TransientData::Text);
}


void testBasic(const TransientData& td)
{
    unit_assert(td.startTime() == startTime_); 
    unit_assert(td.data().size() == sampleCount_); 
    unit_assert_equal(td.observationDuration(), observationDuration_, 1e-12);
    unit_assert_equal(td.A(), A_, 1e-12);
    unit_assert_equal(td.B(), B_, 1e-3);
    unit_assert_equal(td.bandwidth(), sampleCount_/observationDuration_/2, 1e-10);

    unit_assert(td.data().size() == sampleCount_);
    for (unsigned int i=0; i<sampleCount_; i++)
        unit_assert(td.data()[i] == 1);
    
    if (os_) *os_ 
        << setprecision(12)
        << "Start time: " << td.startTime() << endl
        << "Observation duration: " << td.observationDuration() << endl
        << "Calibration parameter A: " << td.A() << endl
        << "Calibration parameter B: " << td.B() << endl
        << "Number of samples: " << td.data().size() << endl
        << "Bandwidth: " << td.bandwidth() << endl
        << "Magnetic field = " << td.magneticField() << endl << endl;
}


void test()
{
    createTestTransients();

    TransientData td(filename_);
    testBasic(td);

    TransientData tdText(filenameText_);
    testBasic(tdText);

    boost::filesystem::remove(filename_);
    boost::filesystem::remove(filenameText_);
}


class TestSignal : public TransientData::Signal
{
    public:

    virtual double operator()(double t) const
    {
        // sum of two decaying sinusoids at frequencies 100000 and 101000
        return exp(-t)*(cos(100000*2*M_PI*t) + cos(101000*2*M_PI*t));
    }
};


void testAdd()
{
    if (os_) *os_ << "testAdd()\n";

    if (os_) *os_ << "creating signal with two peaks\n";

    TransientData td;
    td.observationDuration(.768);
    td.A(A_);
    td.B(B_);

    td.data().resize(1048576);
    td.add(TestSignal());

    if (os_) *os_ << "computing fft\n";

    FrequencyData fd;
    td.computeFFT(1, fd);

    // "peak detection"  

    double threshold = 100000 * fd.noiseFloor();
    vector<FrequencyDatum> peaks;

    for (FrequencyData::iterator it=fd.data().begin(); it!=fd.data().end(); ++it)
        if (abs(it->y) > threshold)
            peaks.push_back(*it);

    if (os_)
    {
        *os_ << "found peaks: " << peaks.size() << endl;
        copy(peaks.begin(), peaks.end(), ostream_iterator<FrequencyDatum>(*os_, "\n"));
    }
    
    unit_assert(peaks.size() == 2);
    unit_assert(peaks[0].x == 100000);
    unit_assert(peaks[1].x == 101000);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "TransientDataTest\n";
        test();
        testAdd();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


