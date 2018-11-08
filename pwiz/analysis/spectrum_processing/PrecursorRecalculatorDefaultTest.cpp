//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "PrecursorRecalculatorDefault.hpp"
#include "pwiz/data/msdata/BinaryDataEncoder.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::analysis;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::data;
namespace bfs = boost::filesystem;


ostream* os_ = 0;


double testData_[] =
{
    818.0578,	      0.0000,
    818.0618,	      0.0000,
    818.0659,	      0.0000,
    818.0699,	      0.0000,
    818.0740,	    554.0963,
    818.0781,	    676.5923,
    818.0821,	    560.7537,
    818.0862,	      0.0000,
    818.0902,	      0.0000,
    818.0942,	      0.0000,
    818.0983,	      0.0000,
    818.1105,	      0.0000,
    818.1145,	      0.0000,
    818.1186,	      0.0000,
    818.1226,	      0.0000,
    818.1267,	    391.2166,
    818.1307,	    697.9452,
    818.1348,	    593.9573,
    818.1389,	      0.0000,
    818.1429,	      0.0000,
    818.1470,	    272.1141,
    818.1510,	    693.6737,
    818.1550,	    727.4882,
    818.1591,	    411.9992,
    818.1631,	      0.0000,
    818.1672,	      0.0000,
    818.1713,	      0.0000,
    818.1753,	      0.0000,
    818.3740,	      0.0000,
    818.3780,	      0.0000,
    818.3821,	      0.0000,
    818.3861,	      0.0000,
    818.3902,	    220.8158,
    818.3942,	    649.2535,
    818.3983,	   1322.3580,
    818.4023,	   2346.6816,
    818.4064,	   5577.4443,
    818.4105,	  15628.4590,
    818.4145,	  28139.2852,
    818.4186,	  34538.0156,
    818.4226,	  29967.1211,
    818.4267,	  17854.7773,
    818.4307,	   6258.7852,
    818.4348,	    336.7964,
    818.4388,	      0.0000,
    818.4429,	      0.0000,
    818.4470,	      0.0000,
    818.4510,	      0.0000,
    818.8811,	      0.0000,
    818.8852,	      0.0000,
    818.8892,	      0.0000,
    818.8933,	      0.0000,
    818.8973,	    493.9565,
    818.9014,	   1365.4312,
    818.9055,	   2507.0815,
    818.9095,	   6813.2627,
    818.9136,	  13756.5684,
    818.9177,	  18748.5176,
    818.9217,	  18208.9883,
    818.9258,	  12877.9766,
    818.9298,	   6632.2642,
    818.9339,	   2455.7969,
    818.9380,	    518.3702,
    818.9420,	      0.0000,
    818.9461,	      0.0000,
    818.9501,	      0.0000,
    818.9542,	      0.0000,
    818.9583,	      0.0000,
    818.9623,	    416.6718,
    818.9664,	    835.6812,
    818.9705,	    899.3900,
    818.9745,	    565.6674,
    818.9786,	      0.0000,
    818.9826,	      0.0000,
    818.9867,	      0.0000,
    818.9907,	      0.0000,
    819.3401,	      0.0000,
    819.3442,	      0.0000,
    819.3483,	      0.0000,
    819.3524,	      0.0000,
    819.3564,	    537.5367,
    819.3605,	    666.3043,
    819.3645,	    707.9114,
    819.3686,	    560.4056,
    819.3727,	      0.0000,
    819.3767,	      0.0000,
    819.3808,	      0.0000,
    819.3848,	      0.0000,
    819.3889,	      0.0000,
    819.3930,	      0.0000,
    819.3970,	      0.0000,
    819.4011,	    248.0490,
    819.4052,	    983.9253,
    819.4092,	   2492.4019,
    819.4133,	   4937.9619,
    819.4174,	   7837.6245,
    819.4214,	   9702.0254,
    819.4255,	   9001.9619,
    819.4296,	   6028.9702,
    819.4337,	   2715.7598,
    819.4377,	    881.8906,
    819.4418,	    979.8033,
    819.4458,	   1142.8175,
    819.4499,	    901.4358,
    819.4540,	    509.0410,
    819.4580,	      0.0000,
    819.4621,	      0.0000,
    819.4661,	      0.0000,
    819.4702,	      0.0000,
    819.8810,	      0.0000,
    819.8851,	      0.0000,
    819.8892,	      0.0000,
    819.8932,	      0.0000,
    819.8973,	     38.7442,
    819.9014,	    719.8252,
    819.9055,	   1409.7166,
    819.9095,	   1759.1530,
    819.9136,	   1186.1797,
    819.9177,	    834.6477,
    819.9218,	   2120.9097,
    819.9258,	   2723.4282,
    819.9299,	   2148.7354,
    819.9340,	    951.6946,
    819.9380,	      0.0000,
    819.9421,	      0.0000,
    819.9462,	      0.0000,
    819.9503,	      0.0000,
    819.9543,	      0.0000,
    820.1131,	      0.0000,
    820.1172,	      0.0000,
    820.1212,	      0.0000,
    820.1253,	      0.0000,
    820.1294,	    283.9149,
    820.1335,	    685.0024,
    820.1375,	    841.5573,
    820.1416,	    831.9690,
    820.1457,	    724.9828,
    820.1498,	    478.1599,
    820.1538,	      0.0000,
    820.1579,	      0.0000,
    820.1620,	      0.0000,
    820.1660,	      0.0000,
    820.3942,	      0.0000,
    820.3983,	      0.0000,
    820.4023,	      0.0000,
    820.4064,	      0.0000,
    820.4105,	      0.0000,
    820.4146,	    733.8157,
    820.4186,	   1578.8794,
    820.4227,	   1832.4481,
    820.4268,	   1322.1443,
    820.4308,	    489.9802,
    820.4349,	      0.0000,
    820.4390,	      0.0000,
    820.4431,	      0.0000,
    820.4471,	    259.0050,
    820.4512,	    654.6262,
    820.4553,	    731.2765,
    820.4594,	    517.5179,
    820.4634,	      0.0000,
    820.4675,	      0.0000,
    820.4716,	      0.0000,
    820.4756,	      0.0000,
    820.5205,	      0.0000,
    820.5246,	      0.0000,
    820.5287,	      0.0000,
    820.5327,	      0.0000,
    820.5368,	    618.2869,
    820.5409,	    684.1355,
    820.5450,	    464.5241,
    820.5491,	      0.0000,
    820.5531,	      0.0000,
    820.5572,	      0.0000,
    820.5613,	      0.0000,
    820.5654,	      0.0000,
    820.5694,	      0.0000,
    820.5735,	    302.8770,
    820.5776,	    748.6038,
    820.5817,	    961.3633,
    820.5858,	    820.3262,
    820.5898,	    413.4973,
    820.5939,	      0.0000,
    820.5980,	      0.0000,
    820.6021,	      0.0000,
    820.6061,	      0.0000,
    820.6102,	      0.0000,
    820.6143,	    354.7731,
    820.6183,	    890.8882,
    820.6224,	   1160.5521,
    820.6265,	   1128.5698,
    820.6306,	    893.9106,
    820.6346,	    579.9231,
    820.6387,	      0.0000,
    820.6428,	      0.0000,
    820.6469,	      0.0000,
    820.6509,	      0.0000,
    820.8589,	      0.0000,
    820.8630,	      0.0000,
    820.8671,	      0.0000,
    820.8712,	      0.0000,
    820.8753,	    567.8625,
    820.8793,	    953.4827,
    820.8834,	   1072.7717,
    820.8875,	   1019.1711,
    820.8916,	    946.2395,
    820.8957,	    748.0505,
    820.8998,	    448.6019,
    820.9039,	      0.0000,
    820.9079,	      0.0000,
    820.9120,	      0.0000,
    820.9161,	      0.0000,
    821.3365,	      0.0000,
    821.3406,	      0.0000,
    821.3447,	      0.0000,
    821.3488,	      0.0000,
    821.3529,	    551.2963,
    821.3569,	    717.1707,
    821.3610,	    837.5309,
    821.3651,	    841.7739,
    821.3692,	    261.5813,
    821.3733,	    498.2640,
    821.3774,	   2032.2089,
    821.3815,	   2452.4067,
    821.3856,	   1783.2299,
    821.3896,	    696.4254,
    821.3937,	    955.2690,
    821.3978,	   3954.5977,
    821.4019,	  19850.8086,
    821.4060,	  46906.4688,
    821.4100,	  68569.3750,
    821.4141,	  68448.7812,
    821.4182,	  46811.6289,
    821.4223,	  19901.8672,
    821.4264,	   3090.5479,
    821.4305,	    862.4839,
    821.4346,	    326.3895,
    821.4387,	      0.0000,
    821.4427,	      0.0000,
    821.4468,	      0.0000,
    821.4509,	      0.0000,
    821.8556,	      0.0000,
    821.8597,	      0.0000,
    821.8638,	      0.0000,
    821.8679,	      0.0000,
    821.8719,	    633.9686,
    821.8760,	   1388.3333,
    821.8801,	   1965.9994,
    821.8842,	   1568.3851,
    821.8883,	    617.3872,
    821.8924,	    471.6464,
    821.8965,	   2934.9033,
    821.9006,	   6675.8296,
    821.9047,	  23122.4727,
    821.9088,	  47305.5195,
    821.9128,	  62059.1055,
    821.9169,	  55725.9336,
    821.9210,	  33587.5078,
    821.9251,	  11589.8770,
    821.9292,	    368.7498,
    821.9333,	    725.5962,
    821.9374,	     80.9717,
    821.9415,	      0.0000,
    821.9456,	      0.0000,
    821.9496,	      0.0000,
    821.9537,	      0.0000,
    822.3548,	      0.0000,
    822.3589,	      0.0000,
    822.3630,	      0.0000,
    822.3671,	      0.0000,
    822.3712,	    106.4319,
    822.3752,	    698.2700,
    822.3793,	   1279.7435,
    822.3834,	   1498.7074,
    822.3875,	   1715.3507,
    822.3916,	   2368.6270,
    822.3957,	   2623.0996,
    822.3998,	    570.4650,
    822.4039,	   5261.7271,
    822.4080,	  15413.5098,
    822.4121,	  23855.4492,
    822.4162,	  25214.1484,
    822.4203,	  19019.5293,
    822.4244,	   9904.5566,
    822.4285,	   3034.5713,
    822.4326,	     13.8116,
    822.4366,	      0.0000,
    822.4407,	      0.0000,
    822.4449,	      0.0000,
    822.4490,	      0.0000,
    822.8710,	      0.0000,
    822.8751,	      0.0000,
    822.8792,	      0.0000,
    822.8833,	      0.0000,
    822.8874,	    635.9196,
    822.8915,	   1131.2902,
    822.8956,	   1693.5773,
    822.8997,	   1612.8446,
    822.9038,	   1345.5366,
    822.9079,	   3657.9766,
    822.9120,	   6275.4512,
    822.9161,	   7365.7505,
    822.9202,	   6641.2046,
    822.9243,	   4600.4551,
    822.9284,	   2155.3687,
    822.9325,	    336.5125,
    822.9366,	      0.0000,
    822.9407,	      0.0000,
    822.9448,	      0.0000,
    822.9489,	      0.0000,
    823.3468,	      0.0000,
    823.3509,	      0.0000,
    823.3550,	      0.0000,
    823.3591,	      0.0000,
    823.3632,	    506.6892,
    823.3673,	    877.7867,
    823.3714,	   1072.1282,
    823.3755,	   1128.3158,
    823.3796,	   1120.0167,
    823.3837,	    939.9150,
    823.3878,	    394.1900,
    823.3920,	    113.9174,
    823.3961,	    787.8625,
    823.4001,	    978.4752,
    823.4042,	    616.5432,
    823.4084,	      0.0000,
    823.4125,	      0.0000,
    823.4166,	      0.0000,
    823.4207,	    269.5316,
    823.4248,	    978.9325,
    823.4289,	   1613.0895,
    823.4330,	   1762.3575,
    823.4371,	   1326.9281,
    823.4412,	    624.3387,
    823.4453,	      0.0000,
    823.4494,	      0.0000,
    823.4536,	      0.0000,
    823.4576,	      0.0000,
}; // testData_


const size_t testDataSize_ = sizeof(testData_)/sizeof(double);
const MZIntensityPair* testDataBegin_ = reinterpret_cast<MZIntensityPair*>(testData_);
const MZIntensityPair* testDataEnd_ = reinterpret_cast<MZIntensityPair*>(testData_+testDataSize_);


void test()
{
    if (os_) *os_ << "test()\n" << flush;

    // instantiate PeakFamilyDetector

    PeakFamilyDetectorFT::Config pfdftConfig;
    pfdftConfig.cp = CalibrationParameters::thermo_FT();
    shared_ptr<PeakFamilyDetector> pfd(new PeakFamilyDetectorFT(pfdftConfig));

    // instantiate PrecursorRecalculatorDefault

    PrecursorRecalculatorDefault::Config config;
    config.peakFamilyDetector = pfd;
    config.mzLeftWidth = 1;
    config.mzRightWidth = 2.5;
    PrecursorRecalculatorDefault pr(config);

    // recalculate

    PrecursorRecalculator::PrecursorInfo initialEstimate;
    initialEstimate.mz = 821.92;
    vector<PrecursorRecalculator::PrecursorInfo> result;
    pr.recalculate(testDataBegin_, testDataEnd_, initialEstimate, result);

    // validate result

    unit_assert(result.size() == 1);
    unit_assert_equal(result[0].mz, 821.41, 1e-2);
}


void test2()
{
    if (os_) *os_ << "test2()\n" << flush;

    // instantiate PeakFamilyDetector

    PeakFamilyDetectorFT::Config pfdftConfig;
    pfdftConfig.cp = CalibrationParameters::thermo_FT();
    shared_ptr<PeakFamilyDetector> pfd(new PeakFamilyDetectorFT(pfdftConfig));

    // instantiate PrecursorRecalculatorDefault

    PrecursorRecalculatorDefault::Config config;
    config.peakFamilyDetector = pfd;
    config.mzLeftWidth = 4;
    config.mzRightWidth = 2.5;
    PrecursorRecalculatorDefault pr(config);

    // recalculate

    PrecursorRecalculator::PrecursorInfo initialEstimate;
    initialEstimate.mz = 821.92;
    vector<PrecursorRecalculator::PrecursorInfo> result;
    pr.recalculate(testDataBegin_, testDataEnd_, initialEstimate, result);

    // validate result

    unit_assert(result.size() == 2);
    unit_assert_equal(result[0].mz, 821.41, 1e-2);
    unit_assert_equal(result[1].mz, 818.42, 1e-2);
}


vector<MZIntensityPair> readData(const bfs::path& filename) 
{
    // data stored as 32-bit big-endian zlib m/z-intensity pairs (mzXML with zlib)

    bfs::ifstream is(filename);
    if (!is) throw runtime_error(("[PrecursorRecalculatorDefaultTest::readData()] Unable to open file " + filename.string()).c_str());
    string encoded;
    is >> encoded;
    
    BinaryDataEncoder::Config bdeConfig;
    bdeConfig.precision = BinaryDataEncoder::Precision_32;
    bdeConfig.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
    bdeConfig.compression = BinaryDataEncoder::Compression_Zlib;

    BinaryDataEncoder encoder(bdeConfig);     
    BinaryData<double> data;
    encoder.decode(encoded, data);

    unit_assert(!data.empty() && data.size()%2 == 0);
    vector<MZIntensityPair> result(data.size()/2);
    copy(data.begin(), data.end(), reinterpret_cast<double*>(&result[0]));
    return result;
}


shared_ptr<PrecursorRecalculatorDefault> createPrecursorRecalculator_msprefix()
{
    // instantiate PeakFamilyDetector

    PeakFamilyDetectorFT::Config pfdftConfig;
    pfdftConfig.cp = CalibrationParameters::thermo_FT();
    shared_ptr<PeakFamilyDetector> pfd(new PeakFamilyDetectorFT(pfdftConfig));

    // instantiate PrecursorRecalculatorDefault

    PrecursorRecalculatorDefault::Config config;
    config.peakFamilyDetector = pfd;
    config.mzLeftWidth = 3;
    config.mzRightWidth = 1.6;
    return shared_ptr<PrecursorRecalculatorDefault>(new PrecursorRecalculatorDefault(config));
}


struct TestInfo
{
    double mzInitialEstimate;
    double mzTrue;
    int chargeTrue;
    
    TestInfo(double _mzInitialEstimate,
             double _mzTrue,
             int _chargeTrue) 
    :   mzInitialEstimate(_mzInitialEstimate),
        mzTrue(_mzTrue),
        chargeTrue(_chargeTrue)
    {}
};


void validateRecalculation(const MZIntensityPair* begin,
                           const MZIntensityPair* end,
                           PrecursorRecalculatorDefault& pr,
                           const TestInfo& testInfo)
{
    // recalculate
    
    PrecursorRecalculator::PrecursorInfo initialEstimate;
    initialEstimate.mz = testInfo.mzInitialEstimate;

    vector<PrecursorRecalculator::PrecursorInfo> result;
    pr.recalculate(begin, end, initialEstimate, result);

    // validate result

    if (os_)
        for (vector<PrecursorRecalculator::PrecursorInfo>::const_iterator it=result.begin(), end=result.end(); it!=end; ++it)
            *os_ << "  " << it->mz << " " << it->charge << endl;

    unit_assert(result.size() >= 1);
    unit_assert_equal(result[0].mz, testInfo.mzTrue, 1e-2);
    unit_assert(result[0].charge == testInfo.chargeTrue);
}


void test5peptide(const bfs::path& datadir)
{
    if (os_) *os_ << "test5peptide()\n" << flush;

    vector<MZIntensityPair> data = readData(datadir / "5peptide.b64");
    unit_assert(data.size() == 19914);

    shared_ptr<PrecursorRecalculatorDefault> pr = createPrecursorRecalculator_msprefix();

    const MZIntensityPair* begin = &data[0];
    const MZIntensityPair* end = begin + data.size();

    validateRecalculation(begin, end, *pr, TestInfo(810.79, 810.42, 2));
    validateRecalculation(begin, end, *pr, TestInfo(837.34, 836.96, 2));
    validateRecalculation(begin, end, *pr, TestInfo(725.36, 724.91, 2));
    validateRecalculation(begin, end, *pr, TestInfo(558.87, 558.31, 3));
    validateRecalculation(begin, end, *pr, TestInfo(812.33, 810.42, 2));
    validateRecalculation(begin, end, *pr, TestInfo(810.75, 810.42, 2));
    validateRecalculation(begin, end, *pr, TestInfo(837.96, 836.96, 2));
    validateRecalculation(begin, end, *pr, TestInfo(644.06, 643.37, 2));
    validateRecalculation(begin, end, *pr, TestInfo(725.68, 724.91, 2));
    validateRecalculation(begin, end, *pr, TestInfo(559.19, 558.31, 3));
    validateRecalculation(begin, end, *pr, TestInfo(811.41, 810.42, 2));
    validateRecalculation(begin, end, *pr, TestInfo(674.64, 674.37, 2));
    validateRecalculation(begin, end, *pr, TestInfo(882.45, 882.47, 1));
}


void runSpecialTest(const bfs::path& filename, size_t pairCount, const TestInfo& testInfo)
{
    if (os_) *os_ << "runSpecialTest: " << filename << " " << testInfo.mzInitialEstimate << " " 
                  << testInfo.mzTrue << " " << testInfo.chargeTrue << endl;

    vector<MZIntensityPair> data = readData(filename);
    unit_assert(data.size() == pairCount);
    shared_ptr<PrecursorRecalculatorDefault> pr = createPrecursorRecalculator_msprefix();
    validateRecalculation(&*data.begin(), &*data.begin()+data.size(), *pr, testInfo);
}


void runTests(const bfs::path& datadir)
{
    test();
    test2();
    test5peptide(datadir);

    runSpecialTest(datadir / "special_1a.b64", 12118, TestInfo(484.2727357, 484.28, 0));
    runSpecialTest(datadir / "special_1b.b64", 17767, TestInfo(930.0000218, 929.99, 2));

    // noise floor calculation issue (due to big neighbor)    
    runSpecialTest(datadir / "special_2a.b64", 4802, TestInfo(705.0000091, 704.32, 2));

    // charge state determination (window must be > 1.5amu to the right) 
    runSpecialTest(datadir / "special_2b.b64", 8897, TestInfo(961.0000167, 960.9639, 2)); 

    // monoisotopic peak threshold must be lenient
    runSpecialTest(datadir / "special_2c.b64", 7006, TestInfo(731.090919, 730.36, 3));
    runSpecialTest(datadir / "special_2d.b64", 12512, TestInfo(730.3599854,730.36, 3));

    // charge state calculation issues due to small 1-neutron peak
    runSpecialTest(datadir / "special_3a.b64", 5721, TestInfo(560.3636411, 560.28, 2));
    runSpecialTest(datadir / "special_3b.b64", 5342, TestInfo(820.6363762, 820.47, 2));

    // charge state calculation issues due to small 1-neutron peak
    runSpecialTest(datadir / "special_4a.b64", 4142, TestInfo(791.5454722, 791.37, 2));

    // charge state regression due to generous acceptance of charge 2 scores 
    runSpecialTest(datadir / "special_5a.b64", 12324, TestInfo(445.0000073, 445.12, 1));
    runSpecialTest(datadir / "special_5a.b64", 12324, TestInfo(407.9090971, 408.31, 1));
    runSpecialTest(datadir / "special_5a.b64", 12324, TestInfo(462.0000078, 462.14, 1));
    runSpecialTest(datadir / "special_5a.b64", 12324, TestInfo(536.0909191, 536.16, 1));
    runSpecialTest(datadir / "special_5a.b64", 12324, TestInfo(519.0909186, 519.14, 1));

    // lonely peaks
    runSpecialTest(datadir / "special_6a.b64", 12358, TestInfo(1682.636408, 1683.39, 0));
    runSpecialTest(datadir / "special_6b.b64", 12280, TestInfo(1565.636404, 1563.74, 0));
    runSpecialTest(datadir / "special_6c.b64", 12245, TestInfo(1668.545498, 1667.55, 0));
    runSpecialTest(datadir / "special_6d.b64", 12386, TestInfo(1851.545504, 1849.69, 0));
    runSpecialTest(datadir / "special_6e.b64", 12221, TestInfo(1444.636401, 1442.54, 0));
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        bfs::path datadir = ".";

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datadir = bfs::path(argv[i]).branch_path(); 
        }   

        runTests(datadir);

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


