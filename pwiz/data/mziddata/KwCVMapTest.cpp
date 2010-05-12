//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
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

#include "KwCVMap.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::mziddata;
using boost::shared_ptr;

ostream* os_ = 0;
const double epsilon = numeric_limits<double>::epsilon();

void testCVMap()
{
    CVMap map("sample name", MS_sample_name);

    unit_assert(map.keyword == "sample name");
    unit_assert(map.cvid == MS_sample_name);
    unit_assert(map("sample name"));
}

void test()
{
    testCVMap();
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

