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


#include "FrequencyEstimatorSimple.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::frequency;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


void test()
{
    if (os_) *os_ << "**************************************************\ntest()\n";

    // create data with Lorentzian peak at 0, parabolic peak at 7.5 
    FrequencyData fd;
    for (int i=-5; i<=5; i++)
        fd.data().push_back(FrequencyDatum(i, 3/sqrt(i*i+1.)));
    fd.data().push_back(FrequencyDatum(6, 1));
    fd.data().push_back(FrequencyDatum(7, 9));
    fd.data().push_back(FrequencyDatum(8, 9));
    fd.data().push_back(FrequencyDatum(9, 1));

    // create initial peak list
    vector<Peak> peaks(2);
    peaks[0].attributes[Peak::Attribute_Frequency] = .1;
    peaks[1].attributes[Peak::Attribute_Frequency] = 7.4; 

    // storage for results

    vector<Peak> estimatedPeaks;

    // run Parabola estimator

    auto_ptr<FrequencyEstimatorSimple> 
        fe(FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Parabola));

    for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        estimatedPeaks.push_back(fe->estimate(fd, *it));

    // check results 

    if (os_) 
    {
        *os_ << setprecision(10);

        *os_ << "Initial peaks:\n";
        copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;

        *os_ << "Parabola estimated peaks:\n";
        copy(estimatedPeaks.begin(), estimatedPeaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(estimatedPeaks.size() == 2);

    const Peak& pi0 = estimatedPeaks[0];
    unit_assert(pi0.getAttribute(Peak::Attribute_Frequency) == 0);
    unit_assert(pi0.intensity == 3.);

    const Peak& pi1 = estimatedPeaks[1];
    unit_assert_equal(pi1.getAttribute(Peak::Attribute_Frequency), 7.5, 1e-10);
    unit_assert_equal(abs(pi1.intensity-10.), 0., 1e-10);


    // run Lorentzian estimator

    estimatedPeaks.clear();
    fe = FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Lorentzian);

    for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        estimatedPeaks.push_back(fe->estimate(fd, *it));

    // check results 

    if (os_)
    {
        *os_ << "Lorentzian estimated peaks:\n";
        copy(estimatedPeaks.begin(), estimatedPeaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(estimatedPeaks.size() == 2);

    const Peak& pil0 = estimatedPeaks[0];
    unit_assert_equal(pil0.getAttribute(Peak::Attribute_Frequency), 0, 1e-10);
    unit_assert_equal(abs(pil0.intensity-3.), 0, 1e-10);

    const Peak& pil1 = estimatedPeaks[1];
    unit_assert_equal(pil1.getAttribute(Peak::Attribute_Frequency), 7.5, 1e-10);
    unit_assert(pil1.intensity == 0.); // intensity is nan
}


void testData()
{
    if (os_) *os_ << "**************************************************\ntestData()\n";

    FrequencyData fd;
    fd.data().push_back(FrequencyDatum(28558.59375, complex<double>(25243.032972361, -2820.6360692452)));
    fd.data().push_back(FrequencyDatum(28559.895833333, complex<double>(39978.141686921, 291.1363106641)));
    fd.data().push_back(FrequencyDatum(28561.197916667, complex<double>(189200.35822792, -2636.9254689346)));
    fd.data().push_back(FrequencyDatum(28562.5, complex<double>(-62230.480432624, -2546.1033855971)));
    fd.data().push_back(FrequencyDatum(28563.802083333, complex<double>(-32263.08735743, -2769.7946573836)));

    vector<Peak> peaks(1);
    peaks[0].attributes[Peak::Attribute_Frequency] = 28561.2;

    if (os_)
    {
        *os_ << "Initial peaks:\n";
        copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    vector<Peak> estimatedPeaks;

    auto_ptr<FrequencyEstimatorSimple> 
        fe(FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Parabola));

    for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        estimatedPeaks.push_back(fe->estimate(fd, *it));

    if (os_)
    {
        *os_ << setprecision(10);

        *os_ << "Parabola estimated peaks:\n";
        copy(estimatedPeaks.begin(), estimatedPeaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(estimatedPeaks.size() == 1);
    unit_assert_equal(estimatedPeaks[0].getAttribute(Peak::Attribute_Frequency), 28561.25049, 1e-5);
}


struct Datum
{
    double frequency;
    complex<double> intensity;
};


Datum data_[] =
{
    {10981.7708333,   complex<double>(1494.77333435,-827.565405375)},
    {10982.421875,    complex<double>(-522.951336943,-2933.77290646)},
    {10983.0729167,   complex<double>(1026.58070488,-2790.70883417)},
    {10983.7239583,   complex<double>(1002.03072708,-1020.67745139)},
    {10984.375,       complex<double>(-567.573503924,-2220.62261993)},
    {10985.0260417,   complex<double>(-6322.94426498,-3013.78424791)},
    {10985.6770833,   complex<double>(430.465272274,502.150355144)},
    {10986.328125,    complex<double>(2578.81032322,795.379729653)},
    {10986.9791667,   complex<double>(2864.69277204,-470.140696311)},
    {10987.6302083,   complex<double>(2788.00641762,4788.24971282)},
    {10988.28125,     complex<double>(-366.077646703,-6084.91428783)},
    {10988.9322917,   complex<double>(1220.81029308,-3297.88016503)},
    {10989.5833333,   complex<double>(2268.72858986,-646.091997391)},
    {10990.234375,    complex<double>(4681.74708664,2313.31976782)},
    {10990.8854167,   complex<double>(-955.7765424,5903.76925847)},
    {10991.5364583,   complex<double>(3957.71316667,-225.389114599)},
    {10992.1875,      complex<double>(-2159.60123121,3597.12682291)},
    {10992.8385417,   complex<double>(653.493128029,2229.46593497)},
    {10993.4895833,   complex<double>(6037.67518189,-4347.22639235)},
    {10994.140625,    complex<double>(-167.455004321,1848.75455373)}
};


const int dataSize_ = sizeof(data_)/sizeof(Datum);


void testData2_LocalMax()
{
    if (os_) *os_ << "**************************************************\ntestData2_LocalMax()\n";

    FrequencyData fd;

    for (const Datum* p=data_; p!=data_+dataSize_; ++p)
        fd.data().push_back(FrequencyDatum(p->frequency, p->intensity));

    vector<Peak> peaks(1);
    peaks[0].attributes[Peak::Attribute_Frequency] = 10983.74;

    if (os_)
    {
        *os_ << "Initial peaks:\n";
        copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    vector<Peak> estimatedPeaks;

    auto_ptr<FrequencyEstimatorSimple> 
        fe(FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::LocalMax));

    for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        estimatedPeaks.push_back(fe->estimate(fd, *it));

    if (os_)
    {
        *os_ << setprecision(10);

        *os_ << "Local max peaks:\n";
        copy(estimatedPeaks.begin(), estimatedPeaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(estimatedPeaks.size() == 1);
    unit_assert_equal(estimatedPeaks[0].getAttribute(Peak::Attribute_Frequency), 10985.02604, 1e-5);
}


void testData2_Parabola()
{
    if (os_) *os_ << "**************************************************\ntestData2()\n";

    FrequencyData fd;

    for (const Datum* p=data_; p!=data_+dataSize_; ++p)
        fd.data().push_back(FrequencyDatum(p->frequency, p->intensity));

    vector<Peak> peaks(1);
    peaks[0].attributes[Peak::Attribute_Frequency] = 10987.6;

    if (os_)
    {
        *os_ << "Initial peaks:\n";
        copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    vector<Peak> estimatedPeaks;

    auto_ptr<FrequencyEstimatorSimple> 
        fe(FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Parabola));

    for (vector<Peak>::const_iterator it=peaks.begin(); it!=peaks.end(); ++it)
        estimatedPeaks.push_back(fe->estimate(fd, *it));

    if (os_)
    {
        *os_ << setprecision(10);

        *os_ << "Parabola peaks:\n";
        copy(estimatedPeaks.begin(), estimatedPeaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(estimatedPeaks.size() == 1);
    unit_assert_equal(estimatedPeaks[0].getAttribute(Peak::Attribute_Frequency), 10988.07103, 1e-5);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "FrequencyEstimatorSimpleTest\n";
        test();
        testData();
        testData2_LocalMax();
        testData2_Parabola();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

