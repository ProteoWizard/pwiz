//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2012 Vanderbilt University - Nashville, TN 37232
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


#include "ProteinListFactory.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/proteome/examples.hpp"
#include <cstring>


using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void testUsage()
{
    if (os_) *os_ << "ProteinListFactory::usage():\n" <<  ProteinListFactory::usage() << endl;
}


void testWrap()
{
    ProteomeData pd;
    examples::initializeTiny(pd);

    ProteinListPtr& pl = pd.proteinListPtr;

    unit_assert(pl.get());
    unit_assert_operator_equal(3, pl->size());

    ProteinListFactory::wrap(pd, "id DEFCON42;ZEBRA");
    unit_assert_operator_equal(2, pl->size());
    unit_assert_operator_equal("ZEBRA", pl->protein(0)->id);
    unit_assert_operator_equal("DEFCON42", pl->protein(1)->id);

    ProteinListFactory::wrap(pd, "index 1");
    unit_assert_operator_equal(1, pl->size());
    unit_assert_operator_equal("DEFCON42", pl->protein(0)->id);
}


void test()
{
    testUsage(); 
    testWrap();
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
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
}

