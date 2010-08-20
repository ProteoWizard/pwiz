//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Appled Proteomics 
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


#include "FrequencyEstimatorPhysicalModel.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::frequency;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


struct Datum
{
    double frequency;
    complex<double> intensity;
};


Datum data_[] =
{
    {159442.7083,   complex<double>(4877.101315, 44697.12422)}, 
    {159444.0104,   complex<double>(4080.603141, 50558.42071)}, 
    {159445.3125,   complex<double>(6577.789977, 58423.29765)}, 
    {159446.6146,   complex<double>(12831.99571, 62206.40467)}, 
    {159447.9167,   complex<double>(12432.57475, 78692.36757)}, 
    {159449.2188,   complex<double>(14863.21774, 97002.26961)}, 
    {159450.5208,   complex<double>(20799.47308, 118598.6778)}, 
    {159451.8229,   complex<double>(22593.31198, 165638.8917)}, 
    {159453.125,    complex<double>(47599.33584, 277486.3998)}, 
    {159454.4271,   complex<double>(286144.9904, 833086.4972)}, 
    {159455.7292,   complex<double>(185071.6796, -646557.3157)}, 
    {159457.0312,   complex<double>(-17704.58144, -233633.2989)}, 
    {159458.3333,   complex<double>(12582.54006, -142740.2498)}, 
    {159459.6354,   complex<double>(-4281.026921, -119490.1607)}, 
    {159460.9375,   complex<double>(-2407.375413, -104118.8209)}, 
    {159462.2396,   complex<double>(-6020.466709, -71343.6045)}, 
    {159463.5417,   complex<double>(-6861.637568, -64726.61834)}, 
    {159464.8438,   complex<double>(4448.264865, -50486.19487)}, 
    {159466.1458,   complex<double>(-2683.225884, -43254.46692)}, 
    {159467.4479,   complex<double>(-1409.582306, -46362.11256)}, 
    {159468.75,     complex<double>(-901.9171424, -39197.02914)}, 
};


const int dataSize_ = sizeof(data_)/sizeof(Datum);


void test()
{
    if (os_) *os_ << setprecision(14);

    // initialize frequency data

    FrequencyData fd;
    for (const Datum* p=data_; p!=data_+dataSize_; ++p)
        fd.data().push_back(FrequencyDatum(p->frequency, p->intensity));
    fd.observationDuration(.768);
    fd.analyze();
    
    // "peak detection"

    Peak detected;
    detected.attributes[Peak::Attribute_Frequency] = fd.max()->x;
    if (os_) *os_ << "detected: " << detected << endl;

    // create estimator
    
    FrequencyEstimatorPhysicalModel::Config config;
    auto_ptr<FrequencyEstimatorPhysicalModel> fe(FrequencyEstimatorPhysicalModel::create(config));

    // get estimate and check answer

    Peak estimate = fe->estimate(fd, detected);
    if (os_) *os_ << "estimate: " << estimate << endl;

    unit_assert_equal(estimate.attributes[Peak::Attribute_Frequency], 159454.98465, 1e-4);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "FrequencyEstimatorPhysicalModelTest\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

