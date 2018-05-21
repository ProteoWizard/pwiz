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
#include "boost/foreach_field.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;

void test(const string& filepath, const ReaderList& readerList)
{
    MSDataFile msd(filepath, &readerList);
    const double EPSILON = 1e-4;

    if (bal::ends_with(filepath, "ImsSynth_Chrom.d"))
    {
        SpectrumList_IonMobility slim(msd.run.spectrumListPtr);
        ostringstream failedTests;
        unit_assert_to_stream(slim.getIonMobilityUnits() == SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec, failedTests);
        unit_assert_equal_to_stream(242.55569, slim.ionMobilityToCCS(32.62, 922.01, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(195.69509, slim.ionMobilityToCCS(25.78, 400.1755, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(243.57694, slim.ionMobilityToCCS(31.55, 254.0593, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(202.32441, slim.ionMobilityToCCS(26.98, 622.0291, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(254.05743, slim.ionMobilityToCCS(33.92, 609.2808, 1), EPSILON, failedTests);
        unit_assert_equal_to_stream(172.09947, slim.ionMobilityToCCS(22.38, 294.1601, 1), EPSILON, failedTests);
        if (!failedTests.str().empty())
            throw runtime_error(failedTests.str());
    }
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

        BOOST_FOREACH(const string& filepath, rawpaths)
        {
            test(filepath, readerList);
        }
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
