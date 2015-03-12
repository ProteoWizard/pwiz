//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "Std.hpp"
#include "TabReader.hpp"
#include "MSIHandler.hpp"
#include "unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <cstring>

using namespace pwiz::util;

ostream *os_ = NULL;

void testDefaultTabHandler(const bfs::path& datafile)
{
    const char* alphabet = "abcd";
    const char* numbers = "1234";

    TabReader tr;
    VectorTabHandler vth;

    tr.setHandler(&vth);
    tr.process(datafile.string().c_str());

    VectorTabHandler::const_iterator it = vth.begin();
    cout << (* (*it).begin()) << endl;

    size_t y=0;
    for (; it != vth.end(); it++)
    {
        size_t x=0;
        for (vector<string>::const_iterator it2=(*it).begin(); it2!=(*it).end();it2++)
        {
            const char* value = (*it2).c_str();
            unit_assert(value[0] == alphabet[x]);
            unit_assert(value[1] == numbers[y]);
            x++;
        }
        cerr << endl;
        y++;
    }
}

void testMSIHandler(const bfs::path& datafile)
{
    TabReader tr;
    MSIHandler mh;

    tr.setHandler(&mh);
    tr.process(datafile.string().c_str());
}

void runTests(const bfs::path& datapath)
{
    testDefaultTabHandler(datapath / "TabTest.tab");
    testMSIHandler(datapath / "MSITest.tab");
}

int main(int argc, char** argv)
{
    TEST_PROLOG(argc, argv)

    try
    {
        bfs::path datapath = ".";

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datapath = bfs::path(argv[i]).branch_path(); 
        }   
        if (os_) *os_ << "TabReaderTest\n";
        runTests(datapath);
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
