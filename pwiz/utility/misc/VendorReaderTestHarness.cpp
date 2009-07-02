//
// VendorReaderTestHarness.cpp
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


#include "VendorReaderTestHarness.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::msdata;


ostream* os_ = 0;


namespace pwiz {
namespace util {


namespace {

void testAccept(const Reader& reader, const string& rawpath)
{
    if (os_) *os_ << "testAccept(): " << rawpath << endl;

    bool accepted = reader.accept(rawpath, "");
    if (os_) *os_ << "accepted: " << boolalpha << accepted << endl;

    unit_assert(accepted);
}


void mangleSourceFileLocations(vector<SourceFilePtr>& sourceFiles)
{
    // mangling the absolute paths is necessary for the test to work from any path
    for (size_t i=0; i < sourceFiles.size(); ++i)
        sourceFiles[i]->location = "file:///";
}


void hackInMemoryMSData(MSData& msd)
{
    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = msd.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end()-1);

    mangleSourceFileLocations(sfs);

    // remove current DataProcessing created on read
    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(DataProcessingPtr());
    if (cl) cl->setDataProcessingPtr(DataProcessingPtr());
}


void testRead(const Reader& reader, const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    // read file into MSData object
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);

    for (size_t i=0; i < msds.size(); ++i)
    {
        MSData& msd = *msds[i];
        mangleSourceFileLocations(msd.fileDescription.sourceFilePtrs);
        if (os_) TextWriter(*os_,0)(msd);

        string targetResultFilename;
        if (msds.size() == 1)
            targetResultFilename = bfs::change_extension(rawpath, ".mzML").string();
        else
            targetResultFilename = bfs::change_extension(rawpath, "-" + msd.run.id + ".mzML").string();

        MSDataFile targetResult(targetResultFilename);
        hackInMemoryMSData(targetResult);

        // test for 1:1 equality
        Diff<MSData> diff(msd, targetResult);
        if (os_ && diff) *os_ << diff << endl; 
        unit_assert(!diff);
    }
}


void test(const Reader& reader, bool testAcceptOnly, const string& rawpath)
{
    testAccept(reader, rawpath);

    if (!testAcceptOnly)
        testRead(reader, rawpath);
}


void generate(const Reader& reader, const string& rawpath)
{
    // read file into MSData object
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);
    MSDataFile::WriteConfig config;
    config.indexed = false;
    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
    if (os_) *os_ << "Writing mzML(s) for " << rawpath << endl;
    for (size_t i=0; i < msds.size(); ++i)
    {
        string outputFilename;
        if (msds.size() == 1)
            outputFilename = bfs::change_extension(rawpath, ".mzML").string();
        else
            outputFilename = bfs::change_extension(rawpath, "-" + msds[i]->run.id + ".mzML").string();
        MSDataFile::write(*msds[i], outputFilename, config);
    }
}

void parseArgs(const vector<string>& args, bool& generateMzML, vector<string>& rawpaths)
{
    generateMzML = false;

    for (size_t i=1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (args[i] == "--generate-mzML") generateMzML = true;
        else rawpaths.push_back(args[i]);
    }
}

} // namespace


int testReader(const Reader& reader, const vector<string>& args, bool testAcceptOnly, const TestPathPredicate& isPathTestable)
{
    try
    {
        bool generateMzML;
        vector<string> rawpaths;
        parseArgs(args, generateMzML, rawpaths);

        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: " + args[0] + " [-v] [--generate-mzML] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            for (bfs::directory_iterator itr(rawpaths[i]); itr != bfs::directory_iterator(); ++itr)
            {
                if (!isPathTestable(itr->path().string()))
                    continue;
                else if (generateMzML && !testAcceptOnly)
                    generate(reader, itr->path().string());
                else
                    test(reader, testAcceptOnly, itr->path().string());
            }
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}


} // namespace util
} // namespace pwiz
