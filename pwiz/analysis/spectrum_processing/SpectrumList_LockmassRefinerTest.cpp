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
#include "pwiz/data/msdata/Version.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;
bool generateMzML = false;


void mangleSourceFileLocations(const string& sourceName, vector<SourceFilePtr>& sourceFiles, const string& newSourceName = "")
{
    // mangling the absolute paths is necessary for the test to work from any path
    BOOST_FOREACH(SourceFilePtr & sourceFilePtr, sourceFiles)
    {
        // if the sourceName or newSourceName is in the location, preserve it (erase everything preceding it)
        if (!isHTTP(sourceFilePtr->location))
        {
            size_t sourceNameInLocation = newSourceName.empty() ? sourceFilePtr->location.find(sourceName) : min(sourceFilePtr->location.find(sourceName), sourceFilePtr->location.find(newSourceName));
            if (sourceNameInLocation != string::npos)
            {
                sourceFilePtr->location.erase(0, sourceNameInLocation);
                sourceFilePtr->location = "file:///" + newSourceName.empty() ? sourceName : newSourceName;
            }
            else
                sourceFilePtr->location = "file:///";
        }

        if (!newSourceName.empty())
        {
            if (!bal::contains(sourceFilePtr->id, newSourceName))
                bal::replace_all(sourceFilePtr->id, sourceName, newSourceName);
            if (!bal::contains(sourceFilePtr->name, newSourceName))
                bal::replace_all(sourceFilePtr->name, sourceName, newSourceName);
        }
    }
}


void manglePwizSoftware(MSData& msd)
{
    // a pwiz version change isn't worth regenerating the test data
    vector<size_t> oldPwizSoftwarePtrs;
    SoftwarePtr pwizSoftware;
    for (size_t i = 0; i < msd.softwarePtrs.size(); ++i)
        if (msd.softwarePtrs[i]->hasCVParam(MS_pwiz))
        {
            if (msd.softwarePtrs[i]->version != pwiz::msdata::Version::str())
                oldPwizSoftwarePtrs.push_back(i);
            else
                pwizSoftware = msd.softwarePtrs[i];
        }

    pwizSoftware->id = "current pwiz";

    msd.dataProcessingPtrs = msd.allDataProcessingPtrs();
    msd.dataProcessingPtrs.resize(1);

    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    if (sl && !msd.dataProcessingPtrs.empty()) sl->setDataProcessingPtr(msd.dataProcessingPtrs[0]);

    for (DataProcessingPtr& dp : msd.dataProcessingPtrs)
        for (ProcessingMethod& pm : dp->processingMethods)
            pm.softwarePtr = pwizSoftware;

    for (vector<size_t>::reverse_iterator itr = oldPwizSoftwarePtrs.rbegin();
        itr != oldPwizSoftwarePtrs.rend();
        ++itr)
        msd.softwarePtrs.erase(msd.softwarePtrs.begin() + (*itr));
}

enum class PeakPicking
{
    None,
    Vendor,
    CWT
};

bool test(const string& filepath, double lockmassMz, double lockmassTolerance, PeakPicking peakPickingMode, bool ddaProcessing)
{
    if (os_) *os_ << filepath << " " << lockmassMz << " " << lockmassTolerance << " " << static_cast<int>(peakPickingMode) << endl;

    ExtendedReaderList readerList;
    Reader::Config readerConfig;
    readerConfig.ddaProcessing = ddaProcessing;
    MSData msd;
    readerList.read(filepath, msd, 0, readerConfig);

    string suffix;
    switch (peakPickingMode)
    {
        case PeakPicking::Vendor: suffix = "-centroid"; break;
        case PeakPicking::CWT: suffix = "-centroid-cwt"; break;
        default: break;
    }

    if (ddaProcessing)
        suffix += "-ddaProcessing";

    bfs::path targetResultFilename = bfs::path(__FILE__).parent_path() / "SpectrumList_LockmassRefinerTest.data" / (msd.run.id + suffix + ".mzML");

    if (!bfs::exists(targetResultFilename) && generateMzML)
    {
        if (os_) *os_ << "Writing new reference file: " << targetResultFilename.string() << endl;
        MSDataFile::WriteConfig writeConfig;
        writeConfig.indexed = false;
        writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;
        writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
        MSDataFile::write(msd, targetResultFilename.string(), writeConfig);
    }

    if (!bfs::exists(targetResultFilename))
        throw runtime_error("test result file does not exist: " + targetResultFilename.string());
    MSDataFile targetResult(targetResultFilename.string());

    SpectrumListPtr slpp;
    SpectrumListPtr lmr;
    switch (peakPickingMode)
    {
        case PeakPicking::Vendor:
            slpp.reset(new SpectrumList_PeakPicker(msd.run.spectrumListPtr, nullptr, true, IntegerSet::positive));
            lmr.reset(new SpectrumList_LockmassRefiner(slpp, lockmassMz, lockmassMz, lockmassTolerance));
            msd.run.spectrumListPtr = lmr;
            break;
        case PeakPicking::CWT:
            lmr.reset(new SpectrumList_LockmassRefiner(msd.run.spectrumListPtr, lockmassMz, lockmassMz, lockmassTolerance));
            slpp.reset(new SpectrumList_PeakPicker(lmr, boost::make_shared<CwtPeakDetector>(1, 0, 0.1), false, IntegerSet::positive));
            msd.run.spectrumListPtr = slpp;
            break;
        case PeakPicking::None:
            lmr.reset(new SpectrumList_LockmassRefiner(msd.run.spectrumListPtr, lockmassMz, lockmassMz, lockmassTolerance));
            msd.run.spectrumListPtr = lmr;
            break;
    }

    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = targetResult.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end() - 1);

    string sourceName = bfs::path(filepath).filename().string();
    mangleSourceFileLocations(sourceName, sfs);
    mangleSourceFileLocations(sourceName, msd.fileDescription.sourceFilePtrs);
    manglePwizSoftware(msd);
    manglePwizSoftware(targetResult);

    DiffConfig config;
    config.ignoreExtraBinaryDataArrays = true;
    config.ignoreDataProcessing = true;
    if (lockmassMz == 0)
        config.ignoreMetadata = true;

    Diff<MSData, DiffConfig> diff(msd, targetResult, config);

    if (lockmassMz == 0)
    {
        unit_assert(diff);
        return diff; // lockmass refinement SHOULD change the results
    }

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
        else
            unit_assert(!diff);
    }
    return !diff;
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

        int tests = 0, testsFailed = 0;
        for(const string& filepath : rawpaths)
        {
            if (bal::ends_with(filepath, "ATEHLSTLSEK_profile.raw"))
            {
                ++tests;
                if (!test(filepath, 684.3469, 0.1, PeakPicking::None, false)) ++testsFailed;
                if (!test(filepath, 0, 0.1, PeakPicking::None, false)) ++testsFailed;
                if (!test(filepath, 684.3469, 0.1, PeakPicking::Vendor, false)) ++testsFailed;
                if (!test(filepath, 0, 0.1, PeakPicking::Vendor, false)) ++testsFailed;
                if (!test(filepath, 684.3469, 0.1, PeakPicking::CWT, false)) ++testsFailed;
                if (!test(filepath, 0, 0.1, PeakPicking::CWT, false)) ++testsFailed;

                if (!test(filepath, 684.3469, 0.1, PeakPicking::Vendor, true)) ++testsFailed;
                if (!test(filepath, 0, 0.1, PeakPicking::Vendor, true)) ++testsFailed;
                if (!test(filepath, 684.3469, 0.1, PeakPicking::CWT, true)) ++testsFailed;
                if (!test(filepath, 0, 0.1, PeakPicking::CWT, true)) ++testsFailed;
            }
        }

        unit_assert_operator_equal(0, testsFailed);
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
