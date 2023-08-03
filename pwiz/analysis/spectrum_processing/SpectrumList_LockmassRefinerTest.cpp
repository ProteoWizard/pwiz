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
#include "SpectrumList_PeakPicker.hpp"
#include "boost/foreach_field.hpp"
#include "boost/core/null_deleter.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;
bool generateMzML = false;

void test(const string& filepath, double lockmassMz, double lockmassTolerance, bool withPeakPicking)
{
    if (os_) *os_ << filepath << " " << lockmassMz << " " << lockmassTolerance << " " << withPeakPicking << endl;

    ExtendedReaderList readerList;
    MSDataFile msd(filepath, &readerList);

    string suffix = withPeakPicking ? "-centroid" : "";
    bfs::path targetResultFilename = bfs::path(__FILE__).parent_path() / "SpectrumList_LockmassRefinerTest.data" / (msd.run.id + suffix + ".mzML");
    if (!bfs::exists(targetResultFilename))
        throw runtime_error("test result file does not exist: " + targetResultFilename.string());
    MSDataFile targetResult(targetResultFilename.string());

    SpectrumListPtr slpp;
    SpectrumListPtr lmr;
    if (withPeakPicking)
    {
        slpp.reset(new SpectrumList_PeakPicker(msd.run.spectrumListPtr, nullptr, true, IntegerSet::positive));
        lmr.reset(new SpectrumList_LockmassRefiner(slpp, lockmassMz, lockmassMz, lockmassTolerance));
    }
    else
        lmr.reset(new SpectrumList_LockmassRefiner(msd.run.spectrumListPtr, lockmassMz, lockmassMz, lockmassTolerance));

    msd.run.spectrumListPtr = lmr;

    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = targetResult.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end() - 1);

    DiffConfig config;
    config.ignoreExtraBinaryDataArrays = true;
    config.ignoreDataProcessing = true;
    if (lockmassMz == 0)
        config.ignoreMetadata = true;

    Diff<MSData, DiffConfig> diff(msd, targetResult, config);

    if (lockmassMz == 0)
    {
        unit_assert(diff);
    }
    else
    {
        if (diff)
        {
            if (os_) *os_ << diff;
            if (generateMzML)
            {
                if (os_) *os_ << "Writing new reference file: " << targetResultFilename.string() << endl;
                MSDataFile::WriteConfig writeConfig;
                writeConfig.indexed = false;
                writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;
                writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
                MSDataFile::write(msd, targetResultFilename.string(), writeConfig);
            }
        }
        unit_assert(!diff);
    }
}


void parseArgs(const vector<string>& args, vector<string>& rawpaths)
{
    for (size_t i = 1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (args[i] == "--generate-mzML") generateMzML = true;
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
                test(filepath, 684.3469, 0.1, false);
                test(filepath, 0, 0.1, false);
                test(filepath, 684.3469, 0.1, true);
                test(filepath, 0, 0.1, true);
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
