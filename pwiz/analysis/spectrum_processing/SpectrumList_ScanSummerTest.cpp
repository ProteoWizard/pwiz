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

};

TestScanSummerCalculator testScanSummerCalculators[] =
{

    { "112 112.0000001 112.1 120 121 123 124 128 129",
      "  3           2     5   0   1   4   2   1   7",
      120.0,
      20.0},

    { "112.0001 119 120 121 122 123 124 127 128 129",
      "       1   4   5   6   7   8   9  10  11  12",
      120.01,
      20.1},

    { "200 200.1 200.2 200.9 202",
      "1.0 3.0 1.0 0.0 3.0",
      401.23,
      21.01},

    { "120 126 127",
      "7 7 7",
      119.96,
      21.05},

    { "200.1 200.2 200.3 200.8 200.9",
      "1.0 3.0 1.0 1.0 4.0",
      401.19,
      21.1},
 
};

TestScanSummerCalculator goldStandard[] =
{

    { "112 112.1 119 120 121 122 123 124 126 127 128 129",
      "6       5   4  12   7   7  12  11   7  17  12  19",
      120.0,
      20.0},

    { "200 200.1 200.2 200.3 200.8 200.9 202",
      "1.0 4.0 4.0 1.0 1.0 4.0 3.0",
      401.23,
      21.01},

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
        s->set(MS_ms_level,2);
        s->index = i;
        s->scanList.scans.push_back(Scan());
        Scan& scanRef = s->scanList.scans[0];
        scanRef.set(MS_scan_start_time,t.rTime,UO_second);
        s->precursors.push_back(Precursor(t.inputPrecursorMZ));
        
        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->defaultArrayLength = inputMZArray.size();

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

        SpectrumListPtr calculator(new SpectrumList_ScanSummer(originalList,0.05,10));

        for (size_t i=0; i < calculator->size(); ++i) 
        {
            SpectrumPtr s = calculator->spectrum(i,true);
            vector<double>& mzs = s->getMZArray()->data;
            vector<double>& intensities = s->getIntensityArray()->data;
            Precursor& precursor = s->precursors[0];
            SelectedIon& selectedIon = precursor.selectedIons[0];
            double precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            double rTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();

            vector<double> goldMZArray = parseDoubleArray(goldStandard[i].inputMZArray);
            vector<double> goldIntensityArray = parseDoubleArray(goldStandard[i].inputIntensityArray);

            unit_assert_operator_equal(goldMZArray.size(), mzs.size());
            unit_assert_operator_equal(goldIntensityArray.size(), intensities.size());
            unit_assert_operator_equal(goldStandard[i].inputPrecursorMZ, precursorMZ);
            unit_assert_operator_equal(goldStandard[i].rTime, rTime);

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
