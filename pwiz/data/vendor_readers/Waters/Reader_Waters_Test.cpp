//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#include "pwiz/utility/misc/unit.hpp"
#include "Reader_Waters.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

struct IsRawData : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath) &&
               bal::iends_with(rawpath, ".raw");
    }
};

struct IsIMSData : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath) &&
               bal::iends_with(rawpath, ".raw") &&
               (bal::istarts_with(bfs::path(rawpath).filename().string(), "HD") || bal::istarts_with(bfs::path(rawpath).filename().string(), "SONAR"));
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_WATERS
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        bool requireUnicodeSupport = false;

        pwiz::util::ReaderTestConfig config;
        pwiz::util::TestResult result;
        pwiz::msdata::Reader_Waters reader;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsRawData(), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        {
            auto newConfig = config;
            newConfig.globalChromatogramsAreMs1Only = true;
            newConfig.indexRange = make_pair(0, 0);
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile({ "MSe_Short.raw", "HDMRM_Short_noLM.raw", "HDDDA_Short_noLM.raw" }), newConfig);
        }

        // test vendor centroiding
        {
            auto newConfig = config;

            // with both ion mobility and non-IMS data
            newConfig.peakPicking = true;
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile({ "ATEHLSTLSEK_profile.raw", "HDDDA_Short_noLM.raw" }), newConfig);

            // with combineIonMobility on IMS data
            newConfig.combineIonMobilitySpectra = true;
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile({ "HDDDA_Short_noLM.raw" }), newConfig);
        }

        config.combineIonMobilitySpectra = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIMSData(), config);

        config.isolationMzAndMobilityFilter.emplace_back(4, 0.2);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIMSData(), config);
        
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
