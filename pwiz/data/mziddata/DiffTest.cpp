//
// DiffTest.cpp
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

#include "Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::mziddata;
using boost::shared_ptr;


ostream* os_ = 0;

void testMzIdentML()
{
    cerr << "begin testMzIdentML\n";
    if (os_) *os_ << "testMzIdentML()\n";

    MzIdentML a, b;

    Diff<MzIdentML> diff(a, b);
    unit_assert(!diff);

    b.version = "version";
    a.cvs.push_back(CV());

    diff(a, b);
    if (os_) *os_ << diff << endl;

    unit_assert(diff);

    unit_assert(diff.a_b.version.empty());
    unit_assert(diff.b_a.version == "version");

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());

    cerr << "end testMzIdentML\n";
}

void test()
{
    testMzIdentML();
}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
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

