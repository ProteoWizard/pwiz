//
// FilesystemTest.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "Filesystem.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>

using namespace pwiz::util;
using std::string;
using std::vector;
using std::exception;
using std::endl;
using std::cerr;

void test()
{
    // TODO: how to do a globbing unit test without knowing the contents of the current directory?
}

int main()
{
    try
    {
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << "Caught exception: " << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception" << endl;
        return 1;
    }
}