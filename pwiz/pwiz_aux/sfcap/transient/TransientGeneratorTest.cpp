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


#include "TransientGenerator.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>
#include <iterator>


using namespace pwiz::id;
using namespace pwiz::id::model;
using namespace pwiz::data;
using namespace pwiz::proteome;
using namespace pwiz::util;
using namespace std;


ostream* os_ = 0;


void test()
{
    if (os_) *os_ << "test()\n";

    double abundanceCutoff = .01;
    double massPrecision = .01;
    IsotopeCalculator calculator(abundanceCutoff, massPrecision);

    TransientGenerator tg(calculator);

    const double A_ = 1.075339687500000e+008;
    const double B_ = -3.454602661132810e+008;
    const double observationDuration_ = .768;
    const int sampleCount_ = 1048576;
    ConstantPhaseFunction pf(0);
    ConstantDecayFunction df(observationDuration_);

    InstrumentConfiguration ic;
    ic.calibrationParameters = CalibrationParameters(A_,B_);
    ic.observationDuration = observationDuration_;
    ic.sampleCount = sampleCount_;
    ic.phaseFunction = &pf;
    ic.decayFunction = &df;

    ChargeDistribution chargeDistribution;
    chargeDistribution.push_back(ChargeAbundance(1, 1));
    chargeDistribution.push_back(ChargeAbundance(2, 1));
    chargeDistribution.push_back(ChargeAbundance(3, 1));
 
    Peptide angiotensin("DRVYIHPF");

    ChromatographicFraction cf;
    cf.instrumentConfiguration = ic;
    cf.species.push_back(Species(angiotensin.formula(), chargeDistribution));

    if (os_) *os_ << "creating transient data\n";
    auto_ptr<TransientData> td = tg.createTransientData(cf); 

    if (os_) *os_ << "computing fft\n";
    FrequencyData fd;
    td->computeFFT(1, fd);

    // "peak detection"  

    double threshold = 10000 * fd.noiseFloor();
    vector<FrequencyDatum> peaks;

    for (FrequencyData::iterator it=fd.data().begin()+1; it!=fd.data().end()-1; ++it)
        if (abs(it->y) > threshold &&
            abs(it->y) > abs((it-1)->y) &&
            abs(it->y) > abs((it+1)->y))
            peaks.push_back(*it);

    if (os_) 
    {
        *os_ << "found peaks: " << peaks.size() << endl;
        copy(peaks.begin(), peaks.end(), ostream_iterator<FrequencyDatum>(*os_, "\n"));
    }

    unit_assert(peaks.size() == 9);
    unit_assert_equal(peaks[0].x, 102552, 1);
    unit_assert_equal(peaks[1].x, 102650, 1);
    unit_assert_equal(peaks[2].x, 102749, 1);
    unit_assert_equal(peaks[3].x, 204910, 1);
    unit_assert_equal(peaks[4].x, 205107, 1);
    unit_assert_equal(peaks[5].x, 205302, 1);
    unit_assert_equal(peaks[6].x, 307073, 1);
    unit_assert_equal(peaks[7].x, 307366, 1);
    unit_assert_equal(peaks[8].x, 307660, 1);
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

