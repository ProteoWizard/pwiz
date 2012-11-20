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

#include "BinaryIndexStream.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"


using namespace pwiz::util;
using namespace pwiz::data;

ostream* os_ = 0;


void test()
{
    if (os_) cout << "Testing BinaryIndexStream (single thread)" << endl;

    shared_ptr<stringstream> indexStreamPtr(new stringstream);

    // test initial creation and usage of the index stream
    {
        vector<Index::Entry> entries;
        for (size_t i=0; i < 10; ++i)
        {
            Index::Entry entry;
            entry.id = lexical_cast<string>(i);
            entry.index = i;
            entry.offset = i*100;
            entries.push_back(entry);
        }

        BinaryIndexStream index(indexStreamPtr);
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

    // test re-use of an existing index stream
    {
        BinaryIndexStream index(indexStreamPtr);
        unit_assert(index.size() == 10);
        unit_assert(!index.find("42").get());
        unit_assert(!index.find(42).get());

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

    // test creating a new, smaller index in an existing index stream
    {
        vector<Index::Entry> entries;
        for (size_t i=0; i < 5; ++i)
        {
            Index::Entry entry;
            entry.id = lexical_cast<string>(i);
            entry.index = i;
            entry.offset = i*100;
            entries.push_back(entry);
        }

        BinaryIndexStream index(indexStreamPtr);

        unit_assert(index.size() == 10);
        index.create(entries);
        unit_assert(index.size() == 5);

        for (size_t i=0; i < 5; ++i)
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

        unit_assert(!index.find("5").get());
        unit_assert(!index.find(5).get());
    }
}


void testThreadSafetyWorker(boost::barrier* testBarrier, BinaryIndexStream* testIndex)
{
    testBarrier->wait(); // wait until all threads have started
    BinaryIndexStream& index = *testIndex;

    try
    {
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
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
    }
}

void testThreadSafety()
{
    if (os_) cout << "Testing BinaryIndexStream (multithreaded)" << endl;

    shared_ptr<stringstream> indexStreamPtr(new stringstream);

    // create a shared index stream
    vector<Index::Entry> entries;
    for (size_t i=0; i < 10; ++i)
    {
        Index::Entry entry;
        entry.id = lexical_cast<string>(i);
        entry.index = i;
        entry.offset = i*100;
        entries.push_back(entry);
    }

    BinaryIndexStream index(indexStreamPtr);
    index.create(entries);
    unit_assert(index.size() == 10);

    // create workers to test using the stream
    const int testThreadCount = 100;
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, &testBarrier, &index));
    testThreadGroup.join_all();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testThreadSafety();
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
