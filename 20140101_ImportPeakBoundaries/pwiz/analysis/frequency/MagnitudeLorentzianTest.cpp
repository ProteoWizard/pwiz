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


#include "MagnitudeLorentzian.hpp"
#include "MagnitudeLorentzianTestData.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::frequency;
using namespace pwiz::data;


ostream* os_ = 0;
double epsilon_ = numeric_limits<double>::epsilon();


void testBasic()
{
    MagnitudeLorentzian m(1,0,1); // m(x) = 1/sqrt(x^2+1)
    unit_assert_equal(m(0), 1, epsilon_);
    unit_assert_equal(m(1), 1/sqrt(2.), epsilon_);
    unit_assert_equal(m(2), 1/sqrt(5.), epsilon_);
    unit_assert_equal(m(3), 1/sqrt(10.), epsilon_);
    unit_assert_equal(m(4), 1/sqrt(17.), epsilon_);

    // center == 0, alpha == 2*pi, tau == 1/(2*pi)
    unit_assert_equal(m.center(), 0, epsilon_);
    unit_assert_equal(m.alpha(), 2*M_PI, epsilon_);
    unit_assert_equal(m.tau(), 1/(2*M_PI), epsilon_);

    if (os_) *os_ << "testBasic(): success!\n";
}


void testFit()
{
    MagnitudeLorentzian ref(1,0,1);

    // choose sample values near 1!
    // weighting pow(y,6) gives big roundoff errors

    vector< pair<double,double> > samples;
    for (int i=-2; i<3; i++)
        samples.push_back(make_pair(i/10.,ref(i/10.)));

    MagnitudeLorentzian m(samples);

    if (os_)
    {
        *os_ << "coefficients: " << setprecision(14);
        copy(m.coefficients().begin(), m.coefficients().end(), ostream_iterator<double>(*os_, " "));
        *os_ << endl;

        *os_ << "error: " << m(0)-1 << endl;

        for (int i=0; i<5; i++)
            *os_ << i << ", " << m(i) << endl;
    }

    unit_assert_equal(m(0), 1, epsilon_*100);
    unit_assert_equal(m(1), 1/sqrt(2.), epsilon_*100);
    unit_assert_equal(m(2), 1/sqrt(5.), epsilon_*100);
    unit_assert_equal(m(3), 1/sqrt(10.), epsilon_*100);
    unit_assert_equal(m(4), 1/sqrt(17.), epsilon_*100);
    if (os_) *os_ << "testFit(): success!\n";
}


void testData()
{
    string filename = "MagnitudeLorentizianTest.cfd.temp.txt";
    ofstream temp(filename.c_str());
    temp << sampleData_;
    temp.close();

    FrequencyData fd(filename);
    boost::filesystem::remove(filename); 

    FrequencyData::const_iterator max = fd.max();
    if (os_) *os_ << "max: (" << max->x << ", " << abs(max->y) << ")\n";

    // fit MagnitudeLorentzian to 3 points on unnormalized data

    vector< pair<double,double> > samples1;
    transform(fd.max()-1, fd.max()+2, back_inserter(samples1), FrequencyData::magnitudeSample);

    if (os_)
    {
        *os_ << "raw data:\n";
        for (unsigned int i=0; i<samples1.size(); i++)
            *os_ << "sample " << i << ": (" << samples1[i].first << ", " << samples1[i].second << ")\n";
    }

    const MagnitudeLorentzian m1(samples1);

    if (os_)
    {
        *os_ << "m1: ";
        copy(m1.coefficients().begin(), m1.coefficients().end(), ostream_iterator<double>(*os_, " "));
        *os_ << endl;
        *os_ << "error: " << scientific << m1.leastSquaresError() << endl;

        for (unsigned int i=0; i<samples1.size(); i++)
            *os_ << "m1(" << i << ") == " << m1(samples1[i].first) << endl;
    }

    // now on normalized data


    fd.normalize();

    vector< pair<double,double> > samples2;
    transform(fd.max()-1, fd.max()+2, back_inserter(samples2), FrequencyData::magnitudeSample);

    if (os_)
    {
        *os_ << "normalized: \n";
        for (unsigned int i=0; i<samples2.size(); i++)
            *os_ << "sample " << i << ": (" << samples2[i].first << ", " << samples2[i].second << ")\n";
    }

    const MagnitudeLorentzian m2(samples2);

    if (os_)
    {
        *os_ << "m2: ";
        copy(m2.coefficients().begin(), m2.coefficients().end(), ostream_iterator<double>(*os_, " "));
        *os_ << endl;
        *os_ << "error: " << scientific << m2.leastSquaresError() << endl;

        for (unsigned int i=0; i<samples2.size(); i++)
            *os_ << "m2(" << i << ") == " << m2(samples2[i].first) << " [" << fd.scale()*m2(samples2[i].first) << "]\n";
    }

    unit_assert_equal(m2.leastSquaresError(), 0, 1e-15);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "MagnitudeLorentzianTest\n";
        testBasic();
        testFit();
        testData();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

