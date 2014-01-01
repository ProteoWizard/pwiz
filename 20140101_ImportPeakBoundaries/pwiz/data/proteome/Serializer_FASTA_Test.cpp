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


#include "Serializer_FASTA.hpp"
#include "Diff.hpp"
#include "examples.hpp"
#include "pwiz/data/common/BinaryIndexStream.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"


using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::data;


ostream* os_ = 0;


void testWriteRead(const Serializer_FASTA::Config& config)
{
    ProteomeData pd;
    examples::initializeTiny(pd);

    Serializer_FASTA serializer(config);

    ostringstream oss;
    serializer.write(oss, pd, NULL);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    ProteomeData pd2;
    serializer.read(iss, pd2);

    Diff<ProteomeData, DiffConfig> diff(pd, pd2);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}


void testWriteRead()
{
    if (os_) *os_ << "testWriteRead() MemoryIndex" << endl;
    Serializer_FASTA::Config config;
    testWriteRead(config);

    if (os_) *os_ << "testWriteRead() BinaryIndexStream" << endl;
    shared_ptr<stringstream> indexStringStream(new stringstream);
    config.indexPtr.reset(new BinaryIndexStream(indexStringStream));
    testWriteRead(config);
}


void testThreadSafetyWorker(pair<boost::barrier*, ProteomeData*>* args)
{
    args->first->wait(); // wait until all threads have started

    try
    {
        testWriteRead();

        for (int i=0; i < 3; ++i)
        {
            for (size_t j=0; j < args->second->proteinListPtr->size(); ++j)
                unit_assert(args->second->proteinListPtr->protein(j)->index == j);   
        }
    }
    catch (exception& e)
    {
        cerr << "Exception in worker thread: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unhandled exception in worker thread." << endl;
    }
}

void testThreadSafety(const int& testThreadCount)
{
    ProteomeData pd;
    examples::initializeTiny(pd);

    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, new pair<boost::barrier*, ProteomeData*>(&testBarrier, &pd)));
    testThreadGroup.join_all();
}

void testDuplicateId()
{
    ProteomeData pd;
    pd.id = "tiny";

    shared_ptr<ProteinListSimple> proteinListPtr(new ProteinListSimple);
    pd.proteinListPtr = proteinListPtr;

    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("ABC123", 0, "One two three.", "ELVISLIVES")));
    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("ZEBRA", 1, "Has stripes:", "BLACKANDWHITE")));
    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("DEFCON42", 2, "", "DNTPANIC")));
    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("ZEBRA", 1, "Black and white", "STRIPES")));

    Serializer_FASTA serializer;

    ostringstream oss;
    serializer.write(oss, pd, NULL);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    ProteomeData pd2;

    unit_assert_throws_what(serializer.read(iss, pd2), runtime_error,
                            "[ProteinList_FASTA::createIndex] duplicate protein id \"ZEBRA\"");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testWriteRead();
        testThreadSafety(2);
        testThreadSafety(4);
        testThreadSafety(8);
        testDuplicateId();
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
