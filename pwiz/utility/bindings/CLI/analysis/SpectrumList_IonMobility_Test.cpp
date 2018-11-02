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

#include "../common/unit.hpp"


#pragma unmanaged
#include <stdexcept>
#include "boost/foreach_field.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_IonMobility.hpp"
#pragma managed

ostream* os_;

using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::analysis;
using namespace System;
using namespace System::Collections::Generic;


void test(const string& filepath)
{
    MSDataFile^ msd = gcnew MSDataFile(ToSystemString(filepath));
    const double EPSILON = 1e-4;

    if (bal::ends_with(filepath, "ImsSynth_Chrom.d"))
    {
        SpectrumList_IonMobility slim(msd->run->spectrumList);
        unit_assert(slim.canConvertIonMobilityAndCCS(SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec));
        unit_assert(slim.getIonMobilityUnits() == SpectrumList_IonMobility::IonMobilityUnits::drift_time_msec);
        unit_assert_equal(242.55569, slim.ionMobilityToCCS(32.62, 922.01, 1), EPSILON);
        unit_assert_equal(195.69509, slim.ionMobilityToCCS(25.78, 400.1755, 1), EPSILON);
        unit_assert_equal(243.57694, slim.ionMobilityToCCS(31.55, 254.0593, 1), EPSILON);
        unit_assert_equal(202.32441, slim.ionMobilityToCCS(26.98, 622.0291, 1), EPSILON);
        unit_assert_equal(254.05743, slim.ionMobilityToCCS(33.92, 609.2808, 1), EPSILON);
        unit_assert_equal(172.09947, slim.ionMobilityToCCS(22.38, 294.1601, 1), EPSILON);
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
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        vector<string> args(argv, argv+argc);
        vector<string> rawpaths;
        parseArgs(args, rawpaths);

        unit_assert(!rawpaths.empty());;

        BOOST_FOREACH(const string& filepath, rawpaths)
        {
            test(filepath);
        }
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
