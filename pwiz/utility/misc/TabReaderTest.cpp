//
// TabReaderTest.cpp
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

#include "TabReader.hpp"
#include "unit.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>

using namespace pwiz::utility;
using namespace std;
namespace bfs = boost::filesystem;

ostream *os_ = NULL;

void testDefaultTabHandler(const bfs::path& datafile)
{
    TabReader tr;
    boost::shared_ptr<TabHandler> dth(new DefaultTabHandler());

    tr.setHandler(dth);
    tr.process(datafile.string().c_str());

    DefaultTabHandler* dth_ptr = (DefaultTabHandler*)dth.get();
}

int main(int argc, char** argv)
{
    cout << "Look look! See me Run!\n";
    try
    {
        bfs::path datafile = "./test.tab";

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datafile = bfs::path(argv[i]); //.branch_path(); 
        }   
        if (os_) *os_ << "TabReaderTest\n";
        testDefaultTabHandler(datafile);
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
