// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2021 Vanderbilt University - Nashville, TN 37232
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
#include "Reader_UNIFI.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

struct IsUrl : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return pwiz::util::isHTTP(rawpath);
    }
};

struct IsIonMobility : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return pwiz::util::isHTTP(rawpath) && (bal::icontains(rawpath, "IMS") || bal::icontains(rawpath, "HDMS"));
    }
};

struct IsNotIonMobility : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return pwiz::util::isHTTP(rawpath) && !(bal::icontains(rawpath, "IMS") || bal::icontains(rawpath, "HDMS"));
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_UNIFI
    bool testAcceptOnly = false;

    if (::getenv("UNIFI_USERNAME") == nullptr || bal::trim_copy(string(::getenv("UNIFI_USERNAME"))).empty() ||
        ::getenv("UNIFI_PASSWORD") == nullptr || bal::trim_copy(string(::getenv("UNIFI_PASSWORD"))).empty())
    {
        cerr << "UNIFI_USERNAME and UNIFI_PASSWORD are not set; Reader_UNIFI_Test is only testing that it can identify files, not read them." << endl;
        testAcceptOnly = true;
    }
    #else
    bool testAcceptOnly = true;
    #endif

    try
    {
        bool requireUnicodeSupport = true;
        pwiz::msdata::Reader_UNIFI reader;
        pwiz::util::TestResult result;
        pwiz::util::ReaderTestConfig config;

        config.indexRange = make_pair(0, 399); // 800 drift scans for HDMSe (4 blocks)
        config.peakPickingCWT = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        config.indexRange = make_pair(0, 49);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNotIonMobility(), config);

        config.combineIonMobilitySpectra = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        // test only first 2 profile spectra
        config.indexRange = make_pair(0, 1);
        config.peakPickingCWT = false;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsIonMobility(), config);

        config.combineIonMobilitySpectra = false;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsUrl(), config);

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
