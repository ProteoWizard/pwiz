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


#include "TruncatedLorentzianParameters.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::frequency;


ostream* os_ = 0;


void testParameterConversion()
{
    TruncatedLorentzianParameters tlp;
    tlp.f0 = 666;
    tlp.alpha = complex<double>(100);
    double shift = 666;
    double scale = 50;

    ublas::vector<double> p = tlp.parameters(-shift, 1/scale);
    unit_assert(p(TruncatedLorentzian::AlphaR) == 2); 
    unit_assert(p(TruncatedLorentzian::AlphaI) == 0); 
    unit_assert(p(TruncatedLorentzian::Tau) == 1); 
    unit_assert(p(TruncatedLorentzian::F0) == 0); 
   
    ublas::vector<double> p2(4);
    p2(TruncatedLorentzian::AlphaR) = 3;
    p2(TruncatedLorentzian::AlphaI) = 0;
    p2(TruncatedLorentzian::Tau) = 0;
    p2(TruncatedLorentzian::F0) = 1;

    tlp.parameters(p2, shift, scale); 
    unit_assert(tlp.alpha == 3.*scale);
    unit_assert(tlp.tau == 0);
    unit_assert(tlp.f0 == 1+shift);
}


void testIO()
{
    TruncatedLorentzianParameters tlp;
    tlp.T = 2;
    tlp.tau = 3;
    tlp.f0 = 666;
    tlp.alpha = complex<double>(100);

    const char* filename = "TruncatedLorentzianTest.test.tlp";
    tlp.write(filename);
    TruncatedLorentzianParameters tlp2(filename);

    unit_assert(tlp2.T == tlp.T);
    unit_assert(tlp2.tau == tlp.tau);
    unit_assert(tlp2.f0 == tlp.f0);
    unit_assert(tlp2.alpha == tlp.alpha);

    boost::filesystem::remove(filename);
}


void testEquality()
{
    TruncatedLorentzianParameters tlp;
    TruncatedLorentzianParameters tlp2;

    tlp.f0 = 666;
    unit_assert(tlp != tlp2);
    tlp2.f0 = 666;
    unit_assert(tlp == tlp2);
}


void testSamples()
{
    TruncatedLorentzianParameters tlp;
    tlp.T = 2;
    tlp.tau = 3;
    tlp.f0 = 666;
    tlp.alpha = complex<double>(100);

    double start = 660;
    double step = .2;
    int count = 60;

    tlp.writeSamples(cout, start, step, count);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "TruncatedLorentzianParametersTest\n";
        testParameterConversion();
        testIO();
        testEquality();
        //testSamples();
        return 0; 
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    } 
}


