//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::tradata;


ostream* os_ = 0;


class Reader1 : public Reader
{
    public:

    struct Config
    {
        mutable bool done;
        Config() : done(false) {}
    };

    Config config;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "1"); 
        if (os_) *os_ << "Reader1::identify(): " << boolalpha << result << endl;
        return result ? filename : std::string("");
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      TraData& result,
                      int runIndex = 0) const 
    {
        if (os_) *os_ << "Reader1::read()\n";
        config.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<TraDataPtr>& results) const
    {
        results.push_back(TraDataPtr(new TraData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "Reader1";} // satisfy inheritance
};


class Reader2 : public Reader
{
    public:

    struct Config
    {
        mutable bool done;
        Config() : done(false) {}
    };

    Config config;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "2"); 
        if (os_) *os_ << "Reader2::identify(): " << boolalpha << result << endl;
        return result ? filename : std::string("");
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      TraData& result,
                      int runIndex = 0) const
    {
        if (os_) *os_ << "Reader2::read()\n";
        config.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<TraDataPtr>& results) const
    {
        results.push_back(TraDataPtr(new TraData));
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

    Reader2* reader2 = readers.get<Reader2>();
    unit_assert(reader2);

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

    TraData td;

    // note: composite pattern with accept/read will cause two calls
    // to accept(); the alternative is to maintain state between accept()
    // and read(), which opens possibility for misuse. 

    unit_assert(readers.get<Reader1>()->config.done == false);
    if (readers.accept("1", "head"))
        readers.read("1", "head", td);
    unit_assert(readers.get<Reader1>()->config.done == true);

    readers.get<Reader1>()->config.done = false;
    unit_assert(readers.get<Reader2>()->config.done == false);
    if (readers.accept("2", "head"))
        readers.read("2", "head", td);
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
    TEST_PROLOG_EX(argc, argv, "_IdentData")

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

