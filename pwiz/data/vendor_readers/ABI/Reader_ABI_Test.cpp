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
#include "Reader_ABI.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

#ifdef PWIZ_READER_ABI
#include "Reader_ABI_Detail.hpp"
#endif

struct IsWiffFile : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bal::iends_with(rawpath, ".wiff")
#ifdef _WIN64
            || bal::iends_with(rawpath, ".wiff2")
#endif
            ;
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_ABI
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        #ifdef PWIZ_READER_ABI

        using namespace pwiz::msdata;
        using namespace pwiz::msdata::detail;
        using namespace pwiz::msdata::detail::ABI;
        using namespace pwiz::util;

        // test that all instrument types are handled by translation functions (skipping the 'Unknown' type)
        bool allInstrumentTestsPassed = true;
        for (int i = 1; i < (int) InstrumentModel_Count; ++i)
        {
            InstrumentModel model = (InstrumentModel) i;

            try
            {
                unit_assert(translateAsInstrumentModel(model) != CVID_Unknown);

                InstrumentConfigurationPtr configuration = translateAsInstrumentConfiguration(model, IonSourceType_Unknown);

                unit_assert(configuration->componentList.source(0).hasCVParam(MS_ionization_type));
                unit_assert(configuration->componentList.analyzer(0).hasCVParam(MS_quadrupole));
                unit_assert(configuration->componentList.detector(0).hasCVParam(MS_electron_multiplier));
            }
            catch (runtime_error& e)
            {
                cerr << "Unit test failed for instrument model " << lexical_cast<string>(model) << ":\n" << e.what() << endl;
                allInstrumentTestsPassed = false;
            }
        }

        unit_assert(allInstrumentTestsPassed);
        #endif

        bool requireUnicodeSupport = true;
        pwiz::msdata::Reader_ABI reader;
        pwiz::util::ReaderTestConfig config;

        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsWiffFile(), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile("PressureTrace1.wiff"), newConfig);
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
