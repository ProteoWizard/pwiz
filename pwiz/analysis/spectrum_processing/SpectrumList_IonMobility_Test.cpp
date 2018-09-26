//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2016 Vanderbilt University - Nashville, TN 37232
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

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/almost_equal.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "SpectrumList_IonMobility.hpp"
#include "SpectrumList_Filter.hpp"
#include "boost/foreach_field.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;

const int EXPECTED_TEST_COUNT = 4;

void test(const string& filepath, const ReaderList& readerList, int& testCount)
{
    MSDataFile msd(filepath, &readerList);
    const double EPSILON = 1e-4;
    ostringstream failedTests;
    SpectrumList_IonMobility slim(msd.run.spectrumListPtr);

    SpectrumListPtr slf(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_MSLevelSet(IntegerSet(1, 10))));
    SpectrumList_IonMobility slim2(slf);

    unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::none), failedTests);

    if (bal::ends_with(filepath, "ImsSynth_Chrom.d"))
    {
        unit_assert_to_stream(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec == slim.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
        unit_assert_equal_to_stream(242.55569, slim.ionMobilityToCCS(32.62, 922.01, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(195.69509, slim.ionMobilityToCCS(25.78, 400.1755, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(243.57694, slim.ionMobilityToCCS(31.55, 254.0593, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(202.32441, slim.ionMobilityToCCS(26.98, 622.0291, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(254.05743, slim.ionMobilityToCCS(33.92, 609.2808, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(172.09947, slim.ionMobilityToCCS(22.38, 294.1601, 1), EPSILON, failedTests);

        unit_assert_equal_to_stream(32.62, slim.ccsToIonMobility(242.55569, 922.01, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(25.78, slim.ccsToIonMobility(195.69509, 400.1755, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(31.55, slim.ccsToIonMobility(243.57694, 254.0593, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(26.98, slim.ccsToIonMobility(202.32441, 622.0291, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(33.92, slim.ccsToIonMobility(254.05743, 609.2808, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(22.38, slim.ccsToIonMobility(172.09947, 294.1601, 1), EPSILON, failedTests);

        unit_assert_to_stream(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec == slim2.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
    }
    else if (bal::ends_with(filepath, "HDMSe_Short_noLM.raw"))
    {
        unit_assert_to_stream(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec == slim.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
        unit_assert_equal_to_stream(177.4365, slim.ionMobilityToCCS(3.1645, 336.18, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(3.1645, slim.ccsToIonMobility(177.4365, 336.18, 1), EPSILON, failedTests);

        /*unit_assert_equal_to_stream(179.48, slim.ionMobilityToCCS(3.2, 309.11, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(158.09, slim.ionMobilityToCCS(2.71, 257.16, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(202.56, slim.ionMobilityToCCS(3.77, 458.16, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(173.46, slim.ionMobilityToCCS(3.11, 334.16, -1), EPSILON, failedTests);*/

        unit_assert_to_stream(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec == slim2.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
    }
    else if (bal::ends_with(filepath, "MSe_Short.raw"))
    {
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
    }
    else if (bal::ends_with(filepath, "HDMSe_Short_noLM.mzML"))
    {
        unit_assert_operator_equal_to_stream((int) SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec, (int) slim.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
        
        unit_assert_operator_equal_to_stream((int) SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec, (int) slim2.getIonMobilityUnits(), failedTests);
        unit_assert_to_stream(!slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec), failedTests);
        unit_assert_to_stream(!slim2.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2), failedTests);
    }
    else
        throw runtime_error("Unhandled test file: " + filepath);

    if (!failedTests.str().empty())
        throw runtime_error(failedTests.str());

    ++testCount;
}


void parseArgs(const vector<string>& args, vector<string>& rawpaths)
{
    for (size_t i = 1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (bal::starts_with(args[i], "--")) continue;
        else rawpaths.push_back(args[i]);
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        vector<string> args(argv, argv+argc);
        vector<string> rawpaths;
        parseArgs(args, rawpaths);

        ExtendedReaderList readerList;
        int testCount = 0;

        for (const string& filepath : rawpaths)
        {
            test(filepath, readerList, testCount);
        }

        unit_assert_operator_equal(EXPECTED_TEST_COUNT, testCount);
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
