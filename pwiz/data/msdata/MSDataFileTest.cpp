//
// MSDataFileTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "MSDataFile.hpp"
#include "Diff.hpp"
#include "IO.hpp"
#include "examples.hpp"
#include "utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


string filenameBase_ = "temp.MSDataFileTest";


void validateWriteRead(const MSDataFile::WriteConfig& writeConfig,
                       const DiffConfig diffConfig)
{
    if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + ".1";
    string filename2 = filenameBase_ + ".2";

    {
        // create MSData object in memory
        MSData tiny;
        examples::initializeTiny(tiny);

        // write to file #1 (static)
        MSDataFile::write(tiny, filename1, writeConfig);

        // read back into an MSDataFile object
        MSDataFile msd1(filename1);

        // hack -- remove last sourceFilePtr
        vector<SourceFilePtr>& sfs = msd1.fileDescription.sourceFilePtrs;
        if (!sfs.empty()) sfs.erase(sfs.end()-1);

        // compare
        Diff<MSData> diff(tiny, msd1, diffConfig);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // write to file #2 (member)
        msd1.write(filename2, writeConfig);

        // read back into another MSDataFile object
        MSDataFile msd2(filename2);

        // hack -- remove last sourceFilePtr
        vector<SourceFilePtr>& sfs2 = msd2.fileDescription.sourceFilePtrs;
        if (!sfs2.empty()) sfs2.erase(sfs2.end()-1);

        // compare
        diff(tiny, msd2);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

	    // now give the gzip read a workout
	    std::string cmd("gzip ");
	    cmd += filename1;
	    if (!system(cmd.c_str())) // don't fret if gzip command isn't present
        {
		    filename1+=".gz";
		    MSDataFile msd1(filename1);

            // hack -- remove last sourceFilePtr
            vector<SourceFilePtr>& sfs3 = msd1.fileDescription.sourceFilePtrs;
            if (!sfs3.empty()) sfs3.erase(sfs3.end()-1);

            // compare
            Diff<MSData> diff(tiny, msd1, diffConfig);
            if (diff && os_) *os_ << diff << endl;
            unit_assert(!diff);
        }
	}

    // remove temp files
    boost::filesystem::remove(filename1);
    boost::filesystem::remove(filename2);
}

void test()
{
    MSDataFile::WriteConfig writeConfig;
    DiffConfig diffConfig;

    // mzML 64-bit, full diff
    validateWriteRead(writeConfig, diffConfig);

    writeConfig.indexed = false;
    validateWriteRead(writeConfig, diffConfig); // no index
    writeConfig.indexed = true;

    // mzML 32-bit, full diff
    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    validateWriteRead(writeConfig, diffConfig);

    // mzXML 32-bit, diff ignoring metadata and chromatograms
    writeConfig.format = MSDataFile::Format_mzXML;
    diffConfig.ignoreMetadata = true;
    diffConfig.ignoreChromatograms = true;
    validateWriteRead(writeConfig, diffConfig);

    // mzXML 64-bit, diff ignoring metadata and chromatograms
    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    validateWriteRead(writeConfig, diffConfig);

    writeConfig.indexed = false;
    validateWriteRead(writeConfig, diffConfig); // no index
    writeConfig.indexed = true;
}


void demo()
{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSDataFile::WriteConfig config;
    MSDataFile::write(tiny, filenameBase_ + ".64.mzML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    MSDataFile::write(tiny, filenameBase_ + ".32.mzML", config);

    config.format = MSDataFile::Format_Text;
    MSDataFile::write(tiny, filenameBase_ + ".txt", config);

    config.format = MSDataFile::Format_mzXML;
    MSDataFile::write(tiny, filenameBase_ + ".32.mzXML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    MSDataFile::write(tiny, filenameBase_ + ".64.mzXML", config);
}


const char rawHeader_[] = {'\x01', '\xA1', 
    'F', '\0', 'i', '\0', 'n', '\0', 'n', '\0', 
    'i', '\0', 'g', '\0', 'a', '\0', 'n', '\0'};


class TestReader : public Reader
{
    public:

    TestReader() : count(0) {}

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        if (filename.size()<=4 || filename.substr(filename.size()-4)!=".RAW")
            return std::string("");

        for (size_t i=0; i<sizeof(rawHeader_); i++)
            if (head[i] != rawHeader_[i]) 
            return std::string("");

        count++;
        return filename;
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        count++;
    }

    const char *getType() const {return "testReader";} // satisfy inheritance

    mutable int count;
};


void testReader()
{
    // create a file
    string filename = filenameBase_ + ".RAW";
    ofstream os(filename.c_str());
    os.write(rawHeader_, 18);
    os.close();

    // open the file with our Reader
    TestReader reader;
    MSDataFile msd(filename, &reader);

    // verify that our reader got called properly
    unit_assert(reader.count == 2);

    // remove temp file
    boost::filesystem::remove(filename);

    if (os_) *os_ << endl;
}


void testSHA1()
{
    if (os_) *os_ << "testSHA1()\n";

    // write out a test file

    string filename = filenameBase_ + ".SHA1Test";
    MSData tiny;
    examples::initializeTiny(tiny);
    MSDataFile::write(tiny, filename);

    // read in without SHA-1 calculation

    MSDataFile msd(filename);

    if (os_)
    {
        *os_ << "no SHA-1:\n";
        pwiz::minimxml::XMLWriter writer(*os_);
        IO::write(writer, *msd.fileDescription.sourceFilePtrs.back());
    }

    unit_assert(!msd.fileDescription.sourceFilePtrs.empty());
    unit_assert(!msd.fileDescription.sourceFilePtrs.back()->hasCVParam(MS_SHA_1));

    // read in with SHA-1 calculation

    MSDataFile msd_sha1(filename, 0, true);

    if (os_)
    {
        *os_ << "with SHA-1:\n";
        pwiz::minimxml::XMLWriter writer(*os_);
        IO::write(writer, *msd_sha1.fileDescription.sourceFilePtrs.back());
    }

    unit_assert(!msd_sha1.fileDescription.sourceFilePtrs.empty());
    unit_assert(msd_sha1.fileDescription.sourceFilePtrs.back()->hasCVParam(MS_SHA_1));

    // clean up

    boost::filesystem::remove(filename);
    if (os_) *os_ << endl;
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        //demo();
        testReader();
        testSHA1();
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

