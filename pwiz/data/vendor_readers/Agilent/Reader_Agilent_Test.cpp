//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#include "pwiz/utility/misc/unit.hpp"
#include "Reader_Agilent.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

struct IsDirectory : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath);
    }
};

struct IsIonMobility : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath) && bfs::exists(bfs::path(rawpath) / "AcqData" / "IMSFrame.bin");
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_AGILENT
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        bool requireUnicodeSupport = true;
        pwiz::msdata::Reader_Agilent reader;
        pwiz::util::TestResult result;

        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsDirectory());

        pwiz::util::ReaderTestConfig config;
        config.combineIonMobilitySpectra = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile({"ImsSynthAllIons.d", "GFb_4Scan_TimeSegs_1530_100ng.d"}), newConfig);

        config.ignoreZeroIntensityPoints = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        config.isolationMzAndMobilityFilter.emplace_back(40, 1);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        result.check();
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
