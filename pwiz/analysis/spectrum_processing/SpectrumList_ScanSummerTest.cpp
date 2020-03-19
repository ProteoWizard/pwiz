//
// $Id$
//
//
// Original author: William French <william.r.french <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_ScanSummer.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;

ostream& operator<< (ostream& os, const vector<double>& v)
{
    os << "(";
    for (size_t i=0; i < v.size(); ++i)
        os << " " << v[i];
    os << " )";
    return os;
}

struct TestScanSummerCalculator
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    double inputPrecursorMZ;
    double rTime;
    double ionMobility;
    int msLevel;
};

TestScanSummerCalculator testScanSummerCalculators[] =
{
    { "112 112.0000001 112.1 120 121 123 124 128 129 112 112.0000001 112.1 120 121 123 124 128 129",
      "  3           2     6   0   1   4   2   1   7   3           2     6   0   1   4   2   1   7",
      0,
      20.0,
      0.0,
      1},

    { "112.0001 119 120 121 122 123 124 127 128 129",
      "       1   4   5   6   7   8   9  10  11  12",
      0,
      20.1,
      0.0,
      1},

    { "112 112.0000001 112.1 120 121 123 124 128 129",
      "  3           2     5   0   1   4   2   1   7",
      120.0,
      20.0,
      0.0,
      2},

    { "112.0001 119 120 121 122 123 124 127 128 129",
      "       1   4   5   6   7   8   9  10  11  12",
      120.01,
      20.1,
      0.0,
      2},

    { "200 200.1 200.2 200.9 202",
      "1.0 3.0 1.0 0.0 3.0",
      401.23,
      21.1,
      1.0,
      2},

    { "120 126 127",
      "7 7 7",
      119.96,
      21.05,
      0.0,
      2},

    { "200.1 200.2 200.3 200.8 200.9",
      "1.0 3.0 1.0 1.0 4.0",
      401.19,
      21.2,
      1.01,
      2},

    { "200.1 200.2 200.3 200.8 200.9",
      "1.0 3.0 1.0 1.0 4.0",
      401.21,
      21.3,
      2.0,
      2},
};

TestScanSummerCalculator goldStandard[] =
{
    { "112 112.1 120 121 123 124 128 129",
      " 10    12   0   2   8   4   2  14",
      0,
      20.0,
      0.0,
      1},

    { "112.0001 119 120 121 122 123 124 127 128 129",
      "       1   4   5   6   7   8   9  10  11  12",
      0,
      20.1,
      0.0,
      1},

    { "112 112.1 119 120 121 122 123 124 126 127 128 129",
      "6       5   4  12   7   7  12  11   7  17  12  19",
      120.0,
      20.1, // median of 20 20.1 21.05
      0.0,
      2},

    { "200 200.1 200.2 200.3 200.8 200.9 202",
      "1.0 4.0 4.0 1.0 1.0 4.0 3.0",
      401.21, // median of 401.19 401.23
      21.15, // median of 21.1 21.2
      1.005, // median of 1 1.01
      2},

    { "200.1 200.2 200.3 200.8 200.9",
      "1.0 3.0 1.0 1.0 4.0",
      401.21,
      21.3,
      2.0,
      2},
};

const size_t testScanSummerSize = sizeof(testScanSummerCalculators) / sizeof(TestScanSummerCalculator);
const size_t goldStandardSize = sizeof(goldStandard) / sizeof(TestScanSummerCalculator);

vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space(), bal::token_compress_on);
    if (!tokens.empty())
        for (size_t i=0; i < tokens.size(); ++i)
            if (!tokens[i].empty())
                doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}

int test()
{
    int failedTests = 0;

    // create the spectrum list
    SpectrumListSimple* sl = new SpectrumListSimple;
    SpectrumListPtr originalList(sl);

    for (size_t i=0; i < testScanSummerSize; ++i)
    {
        TestScanSummerCalculator& t = testScanSummerCalculators[i];
        SpectrumPtr s(new Spectrum);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, t.msLevel);
        s->index = sl->spectra.size();
        s->scanList.scans.push_back(Scan());
        Scan& scanRef = s->scanList.scans[0];
        scanRef.set(MS_scan_start_time,t.rTime,UO_second);
        if (t.ionMobility > 0)
            scanRef.set(MS_inverse_reduced_ion_mobility, t.ionMobility, MS_volt_second_per_square_centimeter);

        if (t.msLevel > 1)
            s->precursors.push_back(Precursor(t.inputPrecursorMZ));
        
        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->defaultArrayLength = inputMZArray.size();

        auto mobilityArray = boost::make_shared<BinaryDataArray>();
        mobilityArray->data.resize(inputMZArray.size(), 0);
        mobilityArray->set(MS_raw_ion_mobility_array);
        s->binaryDataArrayPtrs.push_back(mobilityArray);

        scanRef.scanWindows.push_back(ScanWindow());
        scanRef.scanWindows[0].set(MS_scan_window_lower_limit,inputMZArray[0]);
        scanRef.scanWindows[0].set(MS_scan_window_upper_limit,inputMZArray[inputMZArray.size()-1]);

        sl->spectra.push_back(s);
    }


    vector<double> goldMZArray = parseDoubleArray(goldStandard[0].inputMZArray);
    vector<double> goldIntensityArray = parseDoubleArray(goldStandard[0].inputIntensityArray);

    // run spectral summation
    try
    {

        SpectrumListPtr calculator(new SpectrumList_ScanSummer(originalList, 0.05, 10, 0.5, true));

        for (size_t i=0; i < calculator->size(); ++i) 
        {
            SpectrumPtr s = calculator->spectrum(i,true);
            BinaryData<double>& mzs = s->getMZArray()->data;
            BinaryData<double>& intensities = s->getIntensityArray()->data;
            double rTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();
            double ionMobility = s->scanList.scans[0].cvParamValueOrDefault(MS_inverse_reduced_ion_mobility, 0.0);

            vector<double> goldMZArray = parseDoubleArray(goldStandard[i].inputMZArray);
            vector<double> goldIntensityArray = parseDoubleArray(goldStandard[i].inputIntensityArray);

            unit_assert_operator_equal(2, s->binaryDataArrayPtrs.size()); // mobility array dropped
            unit_assert_operator_equal(goldMZArray.size(), mzs.size());
            unit_assert_operator_equal(goldIntensityArray.size(), intensities.size());
            unit_assert_equal(goldStandard[i].rTime, rTime, 1e-8);
            unit_assert_equal(goldStandard[i].ionMobility, ionMobility, 1e-8);

            if (goldStandard[i].msLevel > 1)
            {
                Precursor& precursor = s->precursors[0];
                SelectedIon& selectedIon = precursor.selectedIons[0];
                double precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
                unit_assert_equal(goldStandard[i].inputPrecursorMZ, precursorMZ, 1e-8);
            }

            for (size_t j=0; j < mzs.size(); ++j)
            {
                unit_assert_equal(mzs[j], goldMZArray[j], 1e-5);
                unit_assert_equal(intensities[j], goldIntensityArray[j], 1e-5);
            }

        }

        
    
            
    }
    catch (exception& e)
    {
        cerr << "Test failed:\n" << e.what() << endl;
        ++failedTests;
    }
    return failedTests;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        int failedTests = test();
        unit_assert_operator_equal(0, failedTests);
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
