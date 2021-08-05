//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2015 Vanderbilt University - Nashville, TN 37232
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
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "SpectrumList_LockmassRefiner.hpp"
#include "boost/foreach_field.hpp"
#include "boost/core/null_deleter.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;


void test(const string& filepath, double lockmassMz, double lockmassTolerance)
{
    ExtendedReaderList readerList;
    MSDataFile msd(filepath, &readerList);

    bfs::path targetResultFilename = bfs::path(__FILE__).parent_path() / "SpectrumList_LockmassRefinerTest.data" / (msd.run.id + ".mzML");
    if (!bfs::exists(targetResultFilename))
        throw runtime_error("test result file does not exist: " + targetResultFilename.string());
    MSDataFile targetResult(targetResultFilename.string());

    SpectrumList_LockmassRefiner lmr(msd.run.spectrumListPtr, lockmassMz, lockmassMz, lockmassTolerance);
    SpectrumListPtr sl(&lmr, boost::null_deleter());
    msd.run.spectrumListPtr = sl;

    DiffConfig config;
    config.ignoreExtraBinaryDataArrays = true;
    config.ignoreMetadata = true;

    Diff<MSData, DiffConfig> diff(msd, targetResult, config);

    if (lockmassMz == 0)
    {
        unit_assert(diff);
    }
    else
    {
        if (os_ && diff) *os_ << diff;
        unit_assert(!diff);
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

        int tests = 0;
        for(const string& filepath : rawpaths)
        {
            if (bal::ends_with(filepath, "ATEHLSTLSEK_profile.raw"))
            {
                ++tests;
                test(filepath, 684.3469, 0.1);
                test(filepath, 0, 0.1);
            }
        }

        if (tests == 0)
            throw runtime_error("did not run any tests");
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
