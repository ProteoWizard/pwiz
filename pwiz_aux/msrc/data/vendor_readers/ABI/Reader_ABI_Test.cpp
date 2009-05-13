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
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
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

void testRead(const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    //MSDataFile targetResult(rawpath + ".mzML");

    // read file into MSData object
    Reader_ABI reader;
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);
    for (size_t i=0; i < msds.size(); ++i)
    {
        cout << msds[i]->run.spectrumListPtr->size() << endl;
        cout << msds[i]->run.chromatogramListPtr->size() << endl;
        if (os_) TextWriter(*os_,0)(*msds[i]);
    }


    // test for 1:1 equality
    //Diff<MSData> diff(msd, targetResult);
    //if (os_ && diff) *os_ << diff << endl; 
    //unit_assert(!diff);
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


int main(int argc, char* argv[])
{
    try
    {
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpaths.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: Reader_ABI_Test [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            test(rawpaths[i]);
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
