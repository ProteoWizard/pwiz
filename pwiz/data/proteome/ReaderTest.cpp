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


#include "Reader.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::util;
using namespace pwiz::proteome;


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

    virtual std::string identify(const std::string& uri, boost::shared_ptr<std::istream> uriStreamPtr) const
    {
        bool result = (uri == "1"); 
        if (os_) *os_ << "Reader1::identify(): " << boolalpha << result << endl;
        return std::string (result?uri:std::string("")); 
    }

    virtual void read(const std::string& uri,
                      boost::shared_ptr<std::istream> uriStreamPtr,
                      ProteomeData& result) const
    {
        if (os_) *os_ << "Reader1::read()\n";
        config.done = true;
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

    virtual std::string identify(const std::string& uri, boost::shared_ptr<std::istream> uriStreamPtr) const
    {
        bool result = (uri == "2"); 
        if (os_) *os_ << "Reader2::identify(): " << boolalpha << result << endl;
        return std::string (result?uri:std::string("")); 
    }

    virtual void read(const std::string& uri,
                      boost::shared_ptr<std::istream> uriStreamPtr,
                      ProteomeData& result) const
    {
        if (os_) *os_ << "Reader2::read()\n";
        config.done = true;
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
    unit_assert(readers.accept("1", shared_ptr<istream>()));
    if (os_) *os_ << "accept 2:\n";
    unit_assert(readers.accept("2", shared_ptr<istream>()));
    if (os_) *os_ << "accept 3:\n";
    unit_assert(!readers.accept("3", shared_ptr<istream>()));

    if (os_) *os_ << endl;
}


void testRead()
{
    if (os_) *os_ << "testRead()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    ProteomeData pd;

    // note: composite pattern with accept/read will cause two calls
    // to accept(); the alternative is to maintain state between accept()
    // and read(), which opens possibility for misuse. 

    unit_assert(readers.get<Reader1>()->config.done == false);
    if (readers.accept("1", shared_ptr<istream>()))
        readers.read("1", shared_ptr<istream>(), pd);
    unit_assert(readers.get<Reader1>()->config.done == true);

    readers.get<Reader1>()->config.done = false;
    unit_assert(readers.get<Reader2>()->config.done == false);
    if (readers.accept("2", shared_ptr<istream>()))
        readers.read("2", shared_ptr<istream>(), pd);
    unit_assert(readers.get<Reader1>()->config.done == false);
    unit_assert(readers.get<Reader2>()->config.done == true);

    if (os_) *os_ << endl;
}


void test()
{
    testGet();
    testAccept();
    testRead();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_ProteomeData")

    try
    {
        if (argc==2 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

