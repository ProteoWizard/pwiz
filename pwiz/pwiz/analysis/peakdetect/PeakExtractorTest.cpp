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


#include "PeakExtractor.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>
#include <iterator>


using namespace std;
using boost::shared_ptr;
using namespace pwiz::math;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


const double testData_[] =
{
    807.9820,	      0.0000,
    807.9860,	      0.0000,
    807.9899,	      0.0000,
    807.9939,	      0.0000,
    808.9221,	      0.0000,
    808.9261,	      0.0000,
    808.9300,	      0.0000,
    808.9340,	      0.0000,
    808.9379,	     46.1869,
    808.9419,	     68.1574,
    808.9459,	     74.2945,
    808.9498,	     67.5736,
    808.9538,	     55.4186,
    808.9577,	      0.0000,
    808.9617,	      0.0000,
    808.9656,	      0.0000,
    808.9696,	      0.0000,
    810.3800,	      0.0000,
    810.3840,	      0.0000,
    810.3880,	      0.0000,
    810.3919,	      0.0000,
    810.3959,	      0.0000,
    810.3999,	     72.5160,
    810.4038,	    450.4998,
    810.4078,	   1138.1459,
    810.4118,	   1834.3859,
    810.4158,	   2075.0105,
    810.4197,	   1699.1493,
    810.4237,	   1021.4493,
    810.4277,	    500.8886,
    810.4316,	    276.2554,
    810.4356,	    179.6894,
    810.4396,	    127.4826,
    810.4435,	     91.4053,
    810.4475,	     55.3596,
    810.4515,	      0.0000,
    810.4554,	      0.0000,
    810.4594,	      0.0000,
    810.4634,	      0.0000,
    810.8248,	      0.0000,
    810.8288,	      0.0000,
    810.8327,	      0.0000,
    810.8367,	      0.0000,
    810.8407,	     32.4939,
    810.8447,	     66.3772,
    810.8486,	     89.7902,
    810.8526,	     70.5686,
    810.8566,	     16.7061,
    810.8605,	      0.0000,
    810.8645,	     32.4340,
    810.8685,	     71.6267,
    810.8725,	     91.5372,
    810.8764,	     81.2383,
    810.8804,	     52.8255,
    810.8844,	      0.0000,
    810.8884,	      0.0000,
    810.8923,	      0.0000,
    810.8963,	      0.0000,
    810.9003,	      0.0000,
    810.9043,	     54.5496,
    810.9082,	    532.4702,
    810.9122,	   1171.1777,
    810.9162,	   1586.9846,
    810.9202,	   1490.4944,
    810.9241,	    979.2804,
    810.9281,	    434.2267,
    810.9321,	    162.3475,
    810.9361,	    128.5575,
    810.9400,	    134.1554,
    810.9440,	    123.5086,
    810.9480,	     88.7253,
    810.9520,	     48.2328,
    810.9559,	      0.0000,
    810.9599,	      0.0000,
    810.9639,	      0.0000,
    810.9678,	      0.0000,
    811.3854,	      0.0000,
    811.3894,	      0.0000,
    811.3934,	      0.0000,
    811.3973,	      0.0000,
    811.4013,	      9.2748,
    811.4053,	    133.5402,
    811.4093,	    298.1690,
    811.4132,	    463.7706,
    811.4172,	    554.1553,
    811.4212,	    503.8234,
    811.4252,	    333.9661,
    811.4292,	    149.2269,
    811.4331,	     48.4688,
    811.4371,	      0.0000,
    811.4411,	      0.0000,
    811.4451,	      0.0000,
    811.4491,	      0.0000,
    811.7675,	      0.0000,
    811.7715,	      0.0000,
    811.7755,	      0.0000,
    811.7795,	      0.0000,
    811.7835,	     41.8127,
    811.7874,	     69.9106,
    811.7914,	     87.5734,
    811.7954,	     91.7424,
    811.7994,	     90.7267,
    811.8034,	     87.8043,
    811.8074,	     74.6657,
    811.8113,	     46.1904,
    811.8153,	      0.0000,
    811.8193,	      0.0000,
    811.8233,	      0.0000,
    811.8273,	      0.0000,
    812.3853,	      0.0000,
    812.3893,	      0.0000,
    812.3933,	      0.0000,
    812.3972,	      0.0000,
    812.4012,	     23.7360,
    812.4052,	     85.1701,
    812.4092,	    124.7133,
    812.4132,	    118.7524,
    812.4172,	     69.4944,
    812.4212,	      9.8729,
    812.4252,	      0.0000,
    812.4292,	      0.0000,
    812.4331,	      0.0000,
    812.4371,	      0.0000
};


const size_t testDataSize_ = sizeof(testData_)/sizeof(double);


void test()
{
    if (os_) *os_ << "test()\n";

    shared_ptr<NoiseCalculator> noiseCalculator(new NoiseCalculator_2Pass);

    PeakFinder_SNR::Config pfsnrConfig;
    pfsnrConfig.windowRadius = 2;
    pfsnrConfig.zValueThreshold = 2;

    shared_ptr<PeakFinder> peakFinder(new PeakFinder_SNR(noiseCalculator, pfsnrConfig));

    PeakFitter_Parabola::Config pfpConfig;
    pfpConfig.windowRadius = 1; // (windowRadius != 1) is not good for real data
    shared_ptr<PeakFitter> peakFitter(new PeakFitter_Parabola(pfpConfig));

    PeakExtractor peakExtractor(peakFinder, peakFitter);

    OrderedPairContainerRef data(testData_, testData_+testDataSize_);
    vector<Peak> peaks;
    peakExtractor.extractPeaks(data, peaks);

    if (os_)
    {
        *os_ << "peaks: " << peaks.size() << endl; 
        copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
    }
    
    const double epsilon = .01;
    unit_assert(peaks.size() == 3);
    unit_assert_equal(peaks[0].mz, 810.41, epsilon);
    unit_assert_equal(peaks[1].mz, 810.91, epsilon);
    unit_assert_equal(peaks[2].mz, 811.41, epsilon);
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

