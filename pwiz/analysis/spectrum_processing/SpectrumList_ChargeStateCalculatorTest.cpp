//
// $Id$
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


#include "SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <vector>
#include <iostream>
#include <iterator>
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "boost/foreach.hpp"


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
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

struct TestChargeStateCalculator
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* inputChargeStateArray;
    double inputPrecursorMZ;

    bool overrideExistingChargeState;
    int minMultipleCharge;
    int maxMultipleCharge;
    double singlyChargedFraction;
    const char* chargeStateArray;

};

TestChargeStateCalculator testChargeStateCalculators[] =
{
    { "1 2 3 4 5", "10 20 30 40 50", "1", 5,
      true, 2, 3, 0.9, "1" },

    { "1 2 3 4 5", "10 20 30 40 50", "1 2 3", 5,
      true, 2, 3, 0.9, "1" },

    { "1 2 3 4 5", "10 20 30 40 50", "", 2.5,
      true, 2, 3, 0.9, "2 3" },

    { "1 2 3 4 5", "10 20 30 40 50", "2", 2.5,
      true, 3, 4, 0.9, "3 4 5" },

    { "1 2 3 4 5", "10 20 30 40 50", "3 4 5", 2.5,
      true, 3, 4, 0.9, "3 4 5" },

    { "1 2 3 4 5", "10 20 30 40 50", "3", 2.5,
      true, 2, 2, 0.9, "2" },

    { "1 2 3 4 5", "10 20 30 40 50", "", 5,
      false, 2, 3, 0.9, "1" },

    { "1 2 3 4 5", "10 20 30 40 50", "", 2.5,
      false, 2, 3, 0.9, "2 3" },

    { "1 2 3 4 5", "10 20 30 40 50", "1", 2.5,
      false, 2, 3, 0.9, "1" },

    { "1 2 3 4 5", "10 20 30 40 50", "2 3", 2.5,
      false, 2, 4, 0.9, "2 3 4" }
};

const size_t testChargeStateCalculatorsSize = sizeof(testChargeStateCalculators) / sizeof(TestChargeStateCalculator);

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
    for (size_t i=0; i < testChargeStateCalculatorsSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, 2);
        sl->spectra.push_back(s);

        TestChargeStateCalculator& t = testChargeStateCalculators[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_counts);

        s->precursors.push_back(Precursor(t.inputPrecursorMZ));
        vector<double> inputChargeStateArray = parseDoubleArray(t.inputChargeStateArray);
        CVID inputChargeStateTerm = inputChargeStateArray.size() > 1 ? MS_possible_charge_state : MS_charge_state;
        BOOST_FOREACH(int z, inputChargeStateArray)
        {
            s->precursors[0].selectedIons[0].cvParams.push_back(CVParam(inputChargeStateTerm, z));
        }

        SpectrumListPtr calculator(new SpectrumList_ChargeStateCalculator(
            originalList,
            t.overrideExistingChargeState,
            t.maxMultipleCharge,
            t.minMultipleCharge,
            t.singlyChargedFraction));

        vector<double> outputChargeStateArray = parseDoubleArray(t.chargeStateArray);
        CVID outputChargeStateTerm = outputChargeStateArray.size() > 1 ? MS_possible_charge_state : MS_charge_state;

        SpectrumPtr calculatedSpectrum = calculator->spectrum(0, true);
        BOOST_FOREACH(CVParam cvParam, s->precursors[0].selectedIons[0].cvParams)
        {
            if (cvParam.cvid != MS_charge_state && cvParam.cvid != MS_possible_charge_state)
                continue;
            unit_assert(outputChargeStateTerm == cvParam.cvid);
            unit_assert(find(outputChargeStateArray.begin(), outputChargeStateArray.end(), cvParam.valueAs<int>()) != outputChargeStateArray.end());
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
