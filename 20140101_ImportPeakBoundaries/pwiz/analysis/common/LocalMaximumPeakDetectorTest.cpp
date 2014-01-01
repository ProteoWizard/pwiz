//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "LocalMaximumPeakDetector.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


struct TestData
{
    size_t windowSize;
    const char* xRaw;
    const char* yRaw;
    const char* xPeakValues;
    const char* yPeakValues;
};

const TestData testData[] =
{
    {
     3,
     "1 2 3 4 5 6 7 8 9",
     "0 1 0 1 2 3 0 0 4",
     "2 6 9",
     "1 3 4"
    },

    {
     5,
     "1 2 3 4 5 6 7 8 9",
     "0 1 0 1 2 3 0 0 4",
     "2 6 9",
     "1 3 4"
    },

    {
     7,
     "1 2 3 4 5 6 7 8 9",
     "0 1 0 1 2 3 0 0 4",
     "9",
     "4"
    }
};

const size_t testDataSize = sizeof(testData) / sizeof(TestData);


vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space(), bal::token_compress_on);
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}


void test()
{
    for (size_t i=0; i < testDataSize; ++i)
    {
        const TestData& data = testData[i];
        
        vector<double> xRaw = parseDoubleArray(data.xRaw);
        vector<double> yRaw = parseDoubleArray(data.yRaw);
        vector<double> target_xPeakValues = parseDoubleArray(data.xPeakValues);
        vector<double> target_yPeakValues = parseDoubleArray(data.yPeakValues);

        // sanity checks
        unit_assert(xRaw.size() == yRaw.size());
        unit_assert(target_xPeakValues.size() == target_yPeakValues.size());

        LocalMaximumPeakDetector peakDetector(data.windowSize);
        vector<double> xPeakValues, yPeakValues;
        peakDetector.detect(xRaw, yRaw, xPeakValues, yPeakValues);

        unit_assert(xPeakValues.size() == target_xPeakValues.size());

        for (size_t j=0; j < xPeakValues.size(); ++j)
        {
            unit_assert_equal(xPeakValues[j], target_xPeakValues[j], 1e-5);
            unit_assert_equal(yPeakValues[j], target_yPeakValues[j], 1e-5);
        }
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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
