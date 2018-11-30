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


#include "MS2Deisotoper.hpp"
#include "SpectrumList_PeakFilter.hpp"
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

struct TestDeisotopeCalculator
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;

};

TestDeisotopeCalculator testDeisotopeCalculators[] =
{

    { "300.0 302.1 303.11 304.12 305.20",
      "1.0 85.0 15.0 3.0 3.0"},

    { "299.5 300.01 300.52 301.03",
    "10.0 75.0 25.0 40.0"},

    { "302.1 302.435 302.77 302.94 303.11",
    "61.0 31.0 8.0 45.0 40.0"},
 
};

TestDeisotopeCalculator goldStandard[] =
{

    { "300.0 302.1 305.20",
    "1.0 85.0 3.0"},

    { "299.5 300.01 301.03",
    "10.0 75.0 40.0"},

    { "302.1 302.94 303.11",
    "61.0 45.0 40.0"},


};

const size_t testDeisotopeSize = sizeof(testDeisotopeCalculators) / sizeof(TestDeisotopeCalculator);
const size_t goldStandardSize = sizeof(goldStandard) / sizeof(TestDeisotopeCalculator);

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

    for (size_t i=0; i < testDeisotopeSize; ++i)
    {
        TestDeisotopeCalculator& t = testDeisotopeCalculators[i];
        // attach all the data to the spectrum list
        SpectrumPtr s(new Spectrum);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level,2);
        s->precursors.push_back(Precursor(100.0)); // dummy precursor m/z
        
        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->defaultArrayLength = inputMZArray.size();

        sl->spectra.push_back(s);

    }

    vector<double> goldMZArray = parseDoubleArray(goldStandard[0].inputMZArray);
    vector<double> goldIntensityArray = parseDoubleArray(goldStandard[0].inputIntensityArray);

    // construct the filter
    bool hires = false;
    MZTolerance mzt(hires? 0.01 : 0.5);
    bool poisson = true;
    int maxCharge = 3, minCharge = 1;
    SpectrumDataFilterPtr filter = SpectrumDataFilterPtr(new MS2Deisotoper(MS2Deisotoper::Config(mzt, hires, poisson, maxCharge, minCharge)));

    // run spectral summation
    try
    {

        SpectrumListPtr calculator(new SpectrumList_PeakFilter(originalList,filter));

        for (size_t i=0; i < calculator->size(); ++i) 
        {
            SpectrumPtr s = calculator->spectrum(i,true);
            BinaryData<double>& mzs = s->getMZArray()->data;
            BinaryData<double>& intensities = s->getIntensityArray()->data;

            vector<double> goldMZArray = parseDoubleArray(goldStandard[i].inputMZArray);
            vector<double> goldIntensityArray = parseDoubleArray(goldStandard[i].inputIntensityArray);

            unit_assert(mzs.size() == goldMZArray.size());
            unit_assert(intensities.size() == goldIntensityArray.size());

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
