//
// Reader_ABI_Test.cpp
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


#include "Reader_ABI.hpp"
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


void testAccept(const string& rawpath)
{
    if (os_) *os_ << "testAccept(): " << rawpath << endl;

    Reader_ABI reader;
    bool accepted = reader.accept(rawpath, "");
    if (os_) *os_ << "accepted: " << boolalpha << accepted << endl;

    unit_assert(accepted);
}


void hackInMemoryMSData(MSData& msd)
{
    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = msd.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end()-1);
    //vector<SoftwarePtr>& sws = msd.softwarePtrs;
    //if (!sws.empty()) sws.erase(sws.end()-1);

    // remove current DataProcessing created on read
    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(DataProcessingPtr());
    if (cl) cl->setDataProcessingPtr(DataProcessingPtr());
}


void testRead(const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    // read file into MSData object
    Reader_ABI reader;
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);

    // WIFFs can represent multiple samples (runs), so test each one
    for (size_t i=0; i < msds.size(); ++i)
    {
        MSData& msd = *msds[i];
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


void test(const string& rawpath)
{
    testAccept(rawpath);
    
    #ifdef _MSC_VER
    testRead(rawpath);
    #else
    if (os_) *os_ << "Not MSVC -- nothing to do.\n";
    #endif // _MSC_VER
}


void generate(const string& rawpath)
{
    #ifdef _MSC_VER
    // read file into MSData object
    Reader_ABI reader;
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);
    MSDataFile::WriteConfig config;
    config.indexed = false;
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
    #else
    if (os_) *os_ << "Not MSVC -- nothing to do.\n";
    #endif // _MSC_VER
}


int main(int argc, char* argv[])
{
    try
    {
        bool generateMzML = false;
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else if (!strcmp(argv[i],"--generate-mzML")) generateMzML = true;
            else rawpaths.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: Reader_ABI_Test [-v] [--generate-mzML] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            for (bfs::directory_iterator itr(rawpaths[i]); itr != bfs::directory_iterator(); ++itr)
            {
                if (itr->path().extension() != ".wiff")
                    continue;
                else if (generateMzML)
                    generate(itr->path().string());
                else
                    test(itr->path().string());
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
