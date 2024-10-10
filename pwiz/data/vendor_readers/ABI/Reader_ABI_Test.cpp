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
        return bal::iends_with(rawpath, ".wiff");
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
        using namespace pwiz::msdata;
        using namespace pwiz::util;

        #ifdef PWIZ_READER_ABI

        using namespace pwiz::msdata::detail;
        using namespace pwiz::msdata::detail::ABI;

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
        ReaderList reader;
        reader.emplace_back(boost::make_shared<Reader_ABI>());
        reader.emplace_back(boost::make_shared<Reader_ABI_WIFF2>());
        pwiz::util::ReaderTestConfig config;
        pwiz::util::TestResult result;

        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsWiffFile(), config);

        {
            auto simAsSpectraConfig = config;
            simAsSpectraConfig.simAsSpectra = true;
            simAsSpectraConfig.indexRange = make_pair(0, 100);
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile("50uMpyrone-8uL-01.wiff"), simAsSpectraConfig);
        }

        {
            auto srmAsSpectraConfig = config;
            srmAsSpectraConfig.srmAsSpectra = true;
            srmAsSpectraConfig.runIndex = 3;
            srmAsSpectraConfig.indexRange = make_pair(0, 100);
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile("Enolase_repeats_AQv1.4.2.wiff"), srmAsSpectraConfig);
        }

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile("PressureTrace1.wiff"), newConfig);

        {
            auto subsetConfig = config;
            subsetConfig.peakPicking = true;
            subsetConfig.indexRange = make_pair(0, 200);
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile("swath.api.wiff2"), subsetConfig);
        }

        {
            auto subsetConfig = config;
            subsetConfig.indexRange = make_pair(0, 20);
            result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile("7600ZenoTOFMSMS_EAD_TestData.wiff2"), subsetConfig);
        }

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
