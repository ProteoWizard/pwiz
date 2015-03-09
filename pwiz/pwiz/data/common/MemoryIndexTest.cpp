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

#include "MemoryIndex.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::util;
using namespace pwiz::data;

ostream* os_ = 0;


void test()
{
    if (os_) cout << "Testing MemoryIndex" << endl;

    vector<Index::Entry> entries;
    for (size_t i=0; i < 10; ++i)
    {
        Index::Entry entry;
        entry.id = lexical_cast<string>(i);
        entry.index = i;
        entry.offset = i*100;
        entries.push_back(entry);
    }

    MemoryIndex index;
    unit_assert(index.size() == 0);
    unit_assert(!index.find("42").get());
    unit_assert(!index.find(42).get());

    index.create(entries);
    unit_assert(index.size() == 10);

    for (size_t i=0; i < 10; ++i)
    {
        Index::EntryPtr entryPtr = index.find(i);
        unit_assert(entryPtr.get());
        unit_assert(entryPtr->id == lexical_cast<string>(i));
        unit_assert(entryPtr->index == i);
        unit_assert(entryPtr->offset == Index::stream_offset(i*100));

        entryPtr = index.find(entryPtr->id);
        unit_assert(entryPtr.get());
        unit_assert(entryPtr->id == lexical_cast<string>(i));
        unit_assert(entryPtr->index == i);
        unit_assert(entryPtr->offset == Index::stream_offset(i*100));
    }

    unit_assert(!index.find("42").get());
    unit_assert(!index.find(42).get());
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
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
