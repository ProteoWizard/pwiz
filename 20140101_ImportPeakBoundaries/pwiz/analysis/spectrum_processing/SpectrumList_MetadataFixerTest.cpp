//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_MetadataFixer.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;


struct TestMetadataFixer
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;

    double tic;
    double bpi;
    double bpmz;
};

TestMetadataFixer testMetadataFixers[] =
{
    { "1 2 3 4 5", "10 20 30 40 50", 150, 50, 5 },
    { "1 2 3 4 5", "50 40 30 20 10", 150, 50, 1 },
    { "1 2 3 4 5", "10 20 30 20 10", 90, 30, 3 },
    { "1", "10", 10, 10, 1 }
};

const size_t testMetadataFixersSize = sizeof(testMetadataFixers) / sizeof(TestMetadataFixer);

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
    for (size_t i=0; i < testMetadataFixersSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);

        // test once with no metadata
        SpectrumPtr s0(new Spectrum);
        sl->spectra.push_back(s0);

        TestMetadataFixer& t = testMetadataFixers[i];

        // test again with existing metadata to be overwritten
        SpectrumPtr s1(new Spectrum);
        s1->set(MS_TIC, t.tic + 1);
        s1->set(MS_base_peak_intensity, t.bpi + 1);
        s1->set(MS_base_peak_m_z, t.bpmz + 1);
        sl->spectra.push_back(s1);

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s0->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s1->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);

        SpectrumListPtr fixer(new SpectrumList_MetadataFixer(originalList));

        SpectrumPtr fixedSpectrum = fixer->spectrum(0, true);
        unit_assert(fixedSpectrum->cvParam(MS_TIC).valueAs<double>() == t.tic);
        unit_assert(fixedSpectrum->cvParam(MS_base_peak_intensity).valueAs<double>() == t.bpi);
        unit_assert(fixedSpectrum->cvParam(MS_base_peak_m_z).valueAs<double>() == t.bpmz);

        fixedSpectrum = fixer->spectrum(1, true);
        unit_assert(fixedSpectrum->cvParam(MS_TIC).valueAs<double>() == t.tic);
        unit_assert(fixedSpectrum->cvParam(MS_base_peak_intensity).valueAs<double>() == t.bpi);
        unit_assert(fixedSpectrum->cvParam(MS_base_peak_m_z).valueAs<double>() == t.bpmz);
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
