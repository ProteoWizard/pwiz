//
// Original author:  Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2020 Matt Chambers
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


#include "DiaUmpire.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"

using namespace pwiz::util;
using namespace pwiz::msdata;

ostream* os_ = 0;


class UserFeedbackIterationListener : public IterationListener
{
    std::streamoff longestMessage;

    public:

    UserFeedbackIterationListener()
    {
        longestMessage = 0;
    }

    virtual Status update(const UpdateMessage& updateMessage)
    {

        stringstream updateString;
        if (updateMessage.message.empty())
            updateString << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;
        else
            updateString << updateMessage.message << ": " << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;

        longestMessage = max(longestMessage, (std::streamoff) updateString.tellp());
        updateString << string(longestMessage - updateString.tellp(), ' '); // add whitespace to erase all of the previous line
        *os_ << updateString.str() << "\r" << flush;

        // spectrum and chromatogram lists both iterate; put them on different lines
        if (updateMessage.iterationIndex+1 >= updateMessage.iterationCount)
            *os_ << endl;
        return Status_Ok;
    }
};


void test(const string& filepath, const DiaUmpire::Config& config)
{
    IterationListenerRegistry iterationListenerRegistry;
    iterationListenerRegistry.addListenerWithTimer(IterationListenerPtr(new UserFeedbackIterationListener), 0.5);
    IterationListenerRegistry* pILR = os_ != 0 ? &iterationListenerRegistry : 0;

    FullReaderList readerList;
    MSDataFile msd(filepath, &readerList);
    DiaUmpire::DiaUmpire dia(msd, msd.run.spectrumListPtr, config, pILR);

    bfs::path referenceFilepath(filepath);
    referenceFilepath = referenceFilepath.replace_extension("").string() + "-diaumpire.mzML";

    MSDataFile::WriteConfig writeConfig;
    writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;

    ostringstream outputStream;
    MSData outputMsd;
    msd.run.spectrumListPtr = dia.outputSpectrumList();
    msd.id = msd.run.id = referenceFilepath.filename().replace_extension("").string();
    MSDataFile::write(msd, outputStream, writeConfig);

    if (bfs::exists(referenceFilepath))
    {
        MSDataFile referenceMsd(referenceFilepath.string());

        vector<SourceFilePtr>& sfs = referenceMsd.fileDescription.sourceFilePtrs;
        if (!sfs.empty()) sfs.erase(sfs.end() - 1);

        referenceMsd.run.defaultInstrumentConfigurationPtr.reset();

        DiffConfig diffConfig;
        diffConfig.ignoreDataProcessing = true;

        Diff<MSData, DiffConfig> diff(msd, (MSData&) referenceMsd, diffConfig);
        if (diff) cerr << diff << endl;
        unit_assert(!diff);
    }
    else
    {
        MSDataFile::write(msd, referenceFilepath.string(), writeConfig);
        throw runtime_error("Reference file '" + referenceFilepath.string() + "' not found. File was regenerated.");
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
        if (argc > 1 && !strcmp(argv[1], "-v")) os_ = &cout;

        vector<string> args(argv, argv + argc);
        vector<string> rawpaths;
        parseArgs(args, rawpaths);

        if (rawpaths.size() != 1)
            throw runtime_error("expected path to 1 text file to read test file/param pairs from");

        bfs::path testpath = bfs::path(rawpaths[0]).parent_path();

        vector<string> tokens;
        ifstream testsTxt(rawpaths[0].c_str());
        string line;
        int totalTests = 0, failedTests = 0;
        while (getline(testsTxt, line))
        {
            ++totalTests;
            bal::split(tokens, line, bal::is_any_of("\t"));

            string rawFilepath = (testpath / tokens[0]).string();
            string paramsFilepath = (testpath / tokens[1]).string();
            DiaUmpire::Config config(paramsFilepath);

            try
            {
                test(rawFilepath, config);
            }
            catch (exception& e)
            {
                cerr << "Test on \"" << rawFilepath << " failed: " << e.what() << endl;
                ++failedTests;
            }
        }

        unit_assert_operator_equal(0, failedTests);
        if (totalTests == 0)
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
