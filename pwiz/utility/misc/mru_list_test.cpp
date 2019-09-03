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
#include "mru_list.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;

void test()
{
    mru_list<std::string> mru(5);

    unit_assert(mru.find("Fu") == mru.end());

    mru.insert("Fighting");

    unit_assert(mru.find("Fu") == mru.end());

    mru.insert("Fu");

    unit_assert(mru.find("Fu") == mru.begin());

    mru.insert("Kung");
    mru.insert("Was");
    mru.insert("Everybody");

    unit_assert(mru.size() == 5);
    unit_assert(*mru.begin() == "Everybody");
    unit_assert(*mru.rbegin() == "Fighting");

    // set "Everybody" as MRU item (no effect)
    mru.insert("Everybody");

    unit_assert(mru.size() == 5);
    unit_assert(*mru.begin() == "Everybody");
    unit_assert(*mru.rbegin() == "Fighting");

    // set "Fighting" as MRU item
    mru.insert("Fighting");

    unit_assert(mru.size() == 5);
    unit_assert(*mru.begin() == "Fighting");
    unit_assert(*mru.rbegin() == "Fu");

    // pop LRU item "Fu"
    mru.insert("Wax on, wax off");

    unit_assert(mru.size() == 5);
    unit_assert(*mru.begin() == "Wax on, wax off");
    unit_assert(*mru.rbegin() == "Kung");
    unit_assert(mru.find("Fu") == mru.end());
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
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
