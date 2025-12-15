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
#include "Reader_Shimadzu.hpp"

#include "Reader_Shimadzu_Detail.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/common/diff_std.hpp"

using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::msdata::detail::Shimadzu;

struct IsShimadzuLCD : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bal::iends_with(rawpath, ".lcd");
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

#ifdef PWIZ_READER_SHIMADZU
        const bool testAcceptOnly = false;
#else
        const bool testAcceptOnly = true;
#endif

    try
    {

#ifdef PWIZ_READER_SHIMADZU
        // test that all Shimidzu instruments have a mapping
        {
            vector<CVID> allInstruments, mappedInstruments, unmappedInstruments, mappedButNotInCvHierarchy;

            set<CVID> brands{ MS_Shimadzu_instrument_model, MS_Shimadzu_MALDI_TOF_instrument_model, MS_Shimadzu_Scientific_Instruments_instrument_model };
            auto isChildOfBrand = [&](CVID cvid)
                {
                    for (CVID brand : brands)
                        if (cvIsA(cvid, brand))
                            return true;
                    return false;
                };

            for (const auto& cvid : cvids())
            {
                if (!isChildOfBrand(cvid))
                    continue;
                allInstruments.push_back(cvid);
            }

            for (const auto& mapping : nameToModelMapping)
                mappedInstruments.push_back(translateAsInstrumentModel(mapping.name));

            diff_impl::vector_diff(allInstruments, mappedInstruments, unmappedInstruments, mappedButNotInCvHierarchy);
            unmappedInstruments.erase(std::remove_if(unmappedInstruments.begin(), unmappedInstruments.end(),
                                                     [&](CVID cvid) { return brands.count(cvid) > 0; }),
                                      unmappedInstruments.end());
            auto instrumentTermNames = [](vector<CVID>& cvids) {ostringstream result; for (CVID cvid : cvids) result << cvTermInfo(cvid).name << "\n"; return result.str(); };
            unit_assert_operator_equal("", instrumentTermNames(unmappedInstruments));
            unit_assert_operator_equal("", instrumentTermNames(mappedButNotInCvHierarchy));
        }
#endif // PWIZ_READER_SHIMADZU

        bool requireUnicodeSupport = true;
        pwiz::msdata::Reader_Shimadzu reader;
        pwiz::util::ReaderTestConfig config;
        pwiz::util::TestResult result;

        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsShimadzuLCD(), config);

        config.peakPicking = true;
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsShimadzuLCD(), config);

        config.srmAsSpectra = true;
        config.indexRange = make_pair(1240, 1260);
        result += pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsNamedRawFile("20140312_六mix_column_1 (scheduled) 一个试.lcd"), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here (TODO: get a small test file with MS2 spectra)
        /*auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile("10nmol_Negative_MS_ID_ON_055.lcd"), newConfig);*/

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
