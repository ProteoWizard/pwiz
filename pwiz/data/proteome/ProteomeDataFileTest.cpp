//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "ProteomeDataFile.hpp"
#include "Diff.hpp"
#include "examples.hpp"
#include "Reader_FASTA.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/filter/gzip.hpp>
#include <boost/iostreams/device/file_descriptor.hpp>
#include <boost/iostreams/copy.hpp>


using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::data;
using boost::shared_ptr;


ostream* os_ = 0;


string filenameBase_ = "temp.ProteomeDataFileTest";


void validateReadIndexed(const ProteomeDataFile::WriteConfig& writeConfig,
                         const DiffConfig diffConfig)
{
    if (os_) *os_ << "validateReadIndexed()\n" << endl;

    string filename1 = filenameBase_ + "1.fasta";

    // create ProteomeData object in memory
    ProteomeData tiny;
    examples::initializeTiny(tiny);

    // write to file #1 (static)
    ProteomeDataFile::write(tiny, filename1, writeConfig);

    {
        unit_assert(!bfs::exists(filename1 + ".index"));

        Reader_FASTA::Config config;
        config.indexed = true;
        Reader_FASTA reader(config);

        // read back into an ProteomeDataFile object
        ProteomeDataFile pd1(filename1, reader);

        unit_assert(bfs::exists(filename1));
        unit_assert(bfs::exists(filename1 + ".index"));

        // compare
        Diff<ProteomeData, DiffConfig> diff(tiny, pd1, diffConfig);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // read back into an ProteomeDataFile object, this time should be indexed
        ProteomeDataFile pd2(filename1, reader);

        // compare
        diff(tiny, pd2);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // now give the gzip read a workout
        /*bio::filtering_istream tinyGZ(bio::gzip_compressor() | bio::file_descriptor_source(filename1));
        bio::copy(tinyGZ, bio::file_descriptor_sink(filename1 + ".gz", ios::out | ios::binary));

        ProteomeDataFile pd3(filename1 + ".gz", reader);

        // compare
        diff(tiny, pd3);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);*/
    }
}

void validateWriteRead(const ProteomeDataFile::WriteConfig& writeConfig,
                       const DiffConfig diffConfig)
{
    if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + "1.fasta";
    string filename2 = filenameBase_ + "2.fasta";

    {
        // create ProteomeData object in memory
        ProteomeData tiny;
        examples::initializeTiny(tiny);

        // write to file #1 (static)
        ProteomeDataFile::write(tiny, filename1, writeConfig);

        shared_ptr<Reader> reader;
        if (writeConfig.format == ProteomeDataFile::Format_FASTA)
        {
            // Reader_FASTA creates the index in the read() call
            Reader_FASTA::Config config;
            config.indexed = writeConfig.indexed;
            reader.reset(new Reader_FASTA(config));
        }

        // read back into an ProteomeDataFile object
        ProteomeDataFile pd1(filename1, *reader);

        // compare
        Diff<ProteomeData, DiffConfig> diff(tiny, pd1, diffConfig);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // write to file #2 (member)
        pd1.write(filename2, writeConfig);

        // read back into another ProteomeDataFile object
        ProteomeDataFile pd2(filename2, *reader);

        // compare
        diff(tiny, pd2);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // now give the gzip read a workout
        bio::filtering_istream tinyGZ(bio::gzip_compressor() | bio::file_descriptor_source(filename1));
        bio::copy(tinyGZ, bio::file_descriptor_sink(filename1+".gz", ios::out|ios::binary));

        ProteomeDataFile pd3(filename1+".gz", *reader);

        // compare
        diff(tiny, pd3);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);
	}

    // remove temp files
    bfs::remove(filename1);
    bfs::remove(filename2);
    bfs::remove(filename1 + ".gz");

    bool index1Exists = bfs::exists(filename1 + ".index");
    bool index2Exists = bfs::exists(filename2 + ".index");
    bool index3Exists = bfs::exists(filename1 + ".gz.index");

    bool indexShouldExist = writeConfig.indexed;
    unit_assert(!indexShouldExist || index1Exists);
    unit_assert(!indexShouldExist || index2Exists);
    unit_assert(!indexShouldExist || index3Exists);

    if (index1Exists) bfs::remove(filename1 + ".index");
    if (index2Exists) bfs::remove(filename2 + ".index");
    if (index3Exists) bfs::remove(filename1 + ".gz.index");
}

void test()
{
    ProteomeDataFile::WriteConfig writeConfig;
    DiffConfig diffConfig;

    // test FASTA with binary stream index
    validateWriteRead(writeConfig, diffConfig);

    // test FASTA with pre-existing indexes
    validateReadIndexed(writeConfig, diffConfig);

    // test FASTA with memory index
    writeConfig.indexed = false;
    validateWriteRead(writeConfig, diffConfig);
}


class TestReader : public Reader
{
    public:

    TestReader() : count(0) {}

    virtual std::string identify(const std::string& uri, shared_ptr<istream> uriStreamPtr) const
    {
        ++count;

        if (!bal::iends_with(uri, ".fasta"))
            return "";

        string buf;
        getlinePortable(*uriStreamPtr, buf);
        if (buf[0] != '>')
            return "";

        return getType();
    }

    virtual void read(const std::string& uri,
                      shared_ptr<istream> uriStreamPtr,
                      ProteomeData& pd) const
    {
        ++count;
    }

    const char *getType() const {return "testReader";} // satisfy inheritance

    mutable int count;
};


void testReader()
{
    // create a file
    string filename = filenameBase_ + ".fAsTa";
    ofstream os(filename.c_str());
    os << ">Id Description\nSEQUENCE\n";
    os.close();

    // open the file with our Reader
    TestReader reader;
    ProteomeDataFile pd(filename, reader);

    // verify that our reader got called properly
    unit_assert(reader.count == 2);

    // remove temp file
    boost::filesystem::remove(filename);

    if (os_) *os_ << endl;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testReader();
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

