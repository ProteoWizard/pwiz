//
// SpectrumList_ThresholderTest.Cpp
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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


#include "SpectrumList_Thresholder.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <vector>
#include <iostream>
#include <iterator>
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/String.hpp"


using namespace std;
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

struct TestThresholder
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* outputMZArray;
    const char* outputIntensityArray;

    ThresholdingBy_Type byType;
    double threshold;
    ThresholdingOrientation orientation;
};

TestThresholder testThresholders[] =
{
    // absolute thresholding, keeping the most intense points
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2 1 1", "30 20 20 10 10", ThresholdingBy_AbsoluteIntensity, 5, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_AbsoluteIntensity, 10, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_AbsoluteIntensity, 15, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_AbsoluteIntensity, 30, Orientation_MostIntense },

    // absolute thresholding, keeping the least intense points
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_AbsoluteIntensity, 5, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_AbsoluteIntensity, 10, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_AbsoluteIntensity, 15, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_AbsoluteIntensity, 30, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2 3", "10 10 20 20 30", ThresholdingBy_AbsoluteIntensity, 50, Orientation_LeastIntense },

    // relative thresholding to the base peak, keeping the most intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2 1 1", "30 20 20 10 10", ThresholdingBy_FractionOfBasePeakIntensity, 0.1, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_FractionOfBasePeakIntensity, 0.34, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_FractionOfBasePeakIntensity, 0.65, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_FractionOfBasePeakIntensity, 0.67, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfBasePeakIntensity, 1.0, Orientation_MostIntense },

    // relative thresholding to the base peak, keeping the least intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfBasePeakIntensity, 0.1, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfBasePeakIntensity, 0.32, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_FractionOfBasePeakIntensity, 0.34, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_FractionOfBasePeakIntensity, 0.67, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_FractionOfBasePeakIntensity, 1.0, Orientation_LeastIntense },

    // relative thresholding to total intensity, keeping the most intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2 1 1", "30 20 20 10 10", ThresholdingBy_FractionOfTotalIntensity, 0.1, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_FractionOfTotalIntensity, 0.12, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_FractionOfTotalIntensity, 0.21, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_FractionOfTotalIntensity, 0.23, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfTotalIntensity, 0.34, Orientation_MostIntense },

    // relative thresholding to total intensity, keeping the least intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfTotalIntensity, 0.1, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_FractionOfTotalIntensity, 0.12, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_FractionOfTotalIntensity, 0.21, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_FractionOfTotalIntensity, 0.23, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2 3", "10 10 20 20 30", ThresholdingBy_FractionOfTotalIntensity, 0.34, Orientation_LeastIntense },

    // threshold against cumulative total intensity fraction, keeping the most intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.32, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.34, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.76, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.78, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2 1 1", "30 20 20 10 10", ThresholdingBy_FractionOfTotalIntensityCutoff, 1.0, Orientation_MostIntense },

    // threshold against cumulative total intensity fraction, keeping the least intense peaks
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.21, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.23, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.65, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_FractionOfTotalIntensityCutoff, 0.67, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2 3", "10 10 20 20 30", ThresholdingBy_FractionOfTotalIntensityCutoff, 1.0, Orientation_LeastIntense },

    // keep the <threshold> most intense points, excluding ties
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_Count, 1, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_Count, 2, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_Count, 3, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_Count, 4, Orientation_MostIntense },

    // keep the <threshold> least intense points, excluding ties
    { "1 2 3 2 1", "10 20 30 20 10", "", "", ThresholdingBy_Count, 1, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_Count, 2, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_Count, 3, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_Count, 4, Orientation_LeastIntense },

    // keep the <threshold> most intense points, including ties
    { "1 2 3 2 1", "10 20 30 20 10", "3", "30", ThresholdingBy_CountAfterTies, 1, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_CountAfterTies, 2, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2", "30 20 20", ThresholdingBy_CountAfterTies, 3, Orientation_MostIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "3 2 2 1 1", "30 20 20 10 10", ThresholdingBy_CountAfterTies, 4, Orientation_MostIntense },

    // keep the <threshold> least intense points, including ties
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_CountAfterTies, 1, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1", "10 10", ThresholdingBy_CountAfterTies, 2, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_CountAfterTies, 3, Orientation_LeastIntense },
    { "1 2 3 2 1", "10 20 30 20 10", "1 1 2 2", "10 10 20 20", ThresholdingBy_CountAfterTies, 4, Orientation_LeastIntense }
};

const size_t testThresholdersSize = sizeof(testThresholders) / sizeof(TestThresholder);

vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space());
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}

void test()
{
    for (size_t i=0; i < testThresholdersSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        sl->spectra.push_back(s);

        TestThresholder& t = testThresholders[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray);

        SpectrumListPtr thresholder(
            new SpectrumList_Thresholder(originalList, t.byType, t.threshold, t.orientation));

        vector<double> outputMZArray = parseDoubleArray(t.outputMZArray);
        vector<double> outputIntensityArray = parseDoubleArray(t.outputIntensityArray);

        SpectrumPtr thresholdedSpectrum = thresholder->spectrum(0, true);
        //if (os_) cout << s1->defaultArrayLength << ": " << s1->getMZArray()->data << " " << s1->getIntensityArray()->data << endl;
        unit_assert(thresholdedSpectrum->defaultArrayLength == outputMZArray.size());
        for (size_t i=0; i < outputMZArray.size(); ++i)
        {
            unit_assert(thresholdedSpectrum->getMZArray()->data[i] == outputMZArray[i]);
            unit_assert(thresholdedSpectrum->getIntensityArray()->data[i] == outputIntensityArray[i]);
        }
    }
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
    }
    
    return 1;
}
