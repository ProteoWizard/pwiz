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
#include "Reader_Mobilion.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;

struct IsMbiFile : public TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bal::iends_with(rawpath, ".mbi");
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_MOBILION
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        bool requireUnicodeSupport = false;

        ReaderTestConfig config;
        TestResult result;
        pwiz::msdata::Reader_Mobilion reader;

        {
            auto subsetConfig = config;
            subsetConfig.indexRange = make_pair(0, 100);
            result += testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsMbiFile(), subsetConfig);
        }

        // test CWT centroiding
        {
            auto newConfig = config;

            // CWT should work with ion mobility
            newConfig.peakPickingCWT = true;
            newConfig.indexRange = make_pair(0, 100);
            result += testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile({ "ExampleTuneMix_binned5.mbi" }), newConfig);

            // with or without combineIonMobility on
            newConfig.combineIonMobilitySpectra = true;
            newConfig.indexRange.reset();
            result += testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile({ "ExampleTuneMix_binned5.mbi" }), newConfig);
        }

        config.combineIonMobilitySpectra = true;
        result += testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsMbiFile(), config);

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
