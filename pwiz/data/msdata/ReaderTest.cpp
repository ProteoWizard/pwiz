//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;


class Reader1 : public Reader
{
    public:

    struct Config
    {
        string name;
        mutable bool done;
        Config() : name("default"), done(false) {}
    };

    Config config;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "1"); 
        if (os_) *os_ << "Reader1::identify(): " << boolalpha << result << endl;
        return std::string (result?filename:std::string("")); 
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0) const 
    {
        if (os_) *os_ << "Reader1::read()\n";
        config.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "Reader1";} // satisfy inheritance
};


class Reader2 : public Reader
{
    public:

    struct Config
    {
        string color;
        mutable bool done;
        Config() : color("orange"), done(false) {}
    };

    Config config;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "2"); 
        if (os_) *os_ << "Reader2::identify(): " << boolalpha << result << endl;
        return std::string (result?filename:std::string("")); 
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0) const
    {
        if (os_) *os_ << "Reader2::read()\n";
        config.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    const char *getType() const {return "Reader2";} // satisfy inheritance
};


void testGet()
{
    if (os_) *os_ << "testGet()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    unit_assert(readers.size() == 2);

    Reader1* reader1 = readers.get<Reader1>();
    unit_assert(reader1);
    if (os_) *os_ << "reader1 config: " << reader1->config.name << endl; 
    unit_assert(reader1->config.name == "default");
    reader1->config.name = "raw";
    if (os_) *os_ << "reader1 config: " << reader1->config.name << endl; 
    unit_assert(reader1->config.name == "raw");

    Reader2* reader2 = readers.get<Reader2>();
    unit_assert(reader2);
    if (os_) *os_ << "reader2 config: " << reader2->config.color << endl; 
    unit_assert(reader2->config.color == "orange");
    reader2->config.color = "purple";
    if (os_) *os_ << "reader2 config: " << reader2->config.color << endl; 
    unit_assert(reader2->config.color == "purple");

    const ReaderList& const_readers = readers;
    const Reader2* constReader2 = const_readers.get<Reader2>();
    unit_assert(constReader2);
    if (os_) *os_ << "constReader2 config: " << constReader2->config.color << endl; 

    if (os_) *os_ << endl;
}


void testAccept()
{
    if (os_) *os_ << "testAccept()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    if (os_) *os_ << "accept 1:\n";
    unit_assert(readers.accept("1", "head"));
    if (os_) *os_ << "accept 2:\n";
    unit_assert(readers.accept("2", "head"));
    if (os_) *os_ << "accept 3:\n";
    unit_assert(!readers.accept("3", "head"));

    if (os_) *os_ << endl;
}


void testRead()
{
    if (os_) *os_ << "testRead()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    MSData msd;

    // note: composite pattern with accept/read will cause two calls
    // to accept(); the alternative is to maintain state between accept()
    // and read(), which opens possibility for misuse. 

    unit_assert(readers.get<Reader1>()->config.done == false);
    if (readers.accept("1", "head"))
        readers.read("1", "head", msd);
    unit_assert(readers.get<Reader1>()->config.done == true);

    readers.get<Reader1>()->config.done = false;
    unit_assert(readers.get<Reader2>()->config.done == false);
    if (readers.accept("2", "head"))
        readers.read("2", "head", msd);
    unit_assert(readers.get<Reader1>()->config.done == false);
    unit_assert(readers.get<Reader2>()->config.done == true);

    if (os_) *os_ << endl;
}


void testIdentifyFileFormat()
{
    ReaderPtr readers(new ExtendedReaderList);

    {ofstream fs("testSpectraDataFile.mzedML"); fs << "<?xml?><mzML>";}
    unit_assert_operator_equal(MS_mzML_file, identifyFileFormat(readers, "testSpectraDataFile.mzedML"));
    bfs::remove("testSpectraDataFile.mzedML");

    {ofstream fs("testSpectraDataFile.mzedXML"); fs << "<?xml?><mzXML>";}
    unit_assert_operator_equal(MS_ISB_mzXML_file, identifyFileFormat(readers, "testSpectraDataFile.mzedXML"));
    bfs::remove("testSpectraDataFile.mzedXML");

    {ofstream fs("testSpectraDataFile.mGF"); fs << "MGF";}
    unit_assert_operator_equal(MS_Mascot_MGF_file, identifyFileFormat(readers, "testSpectraDataFile.mGF"));
    bfs::remove("testSpectraDataFile.mGF");
    
    {ofstream fs("testSpectraDataFile.Ms2"); fs << "MS2";}
    unit_assert_operator_equal(MS_MS2_file, identifyFileFormat(readers, "testSpectraDataFile.Ms2"));
    bfs::remove("testSpectraDataFile.Ms2");
    
    {ofstream fs("testSpectraDataFile.wiFF"); fs << "WIFF";}
    unit_assert_operator_equal(MS_ABI_WIFF_file, identifyFileFormat(readers, "testSpectraDataFile.wiFF"));
    bfs::remove("testSpectraDataFile.wiFF");

    {ofstream fs("_FUNC42.DAT"); fs << "Life, the Universe, and Everything";}
    unit_assert_operator_equal(MS_Waters_raw_file, identifyFileFormat(readers, "."));
    bfs::remove("_FUNC42.DAT");
}


void test()
{
    testGet();
    testAccept();
    testRead();
    testIdentifyFileFormat();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc==2 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

