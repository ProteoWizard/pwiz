//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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


#include "pwiz/utility/misc/unit.hpp"
#include "Serializer_mz5.hpp"
#include "Diff.hpp"
#include "References.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"
#include <cstring>
#include <cstdlib>

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;

ostream* os_ = 0;

void testWriteRead(const MSData& msd, const MSDataFile::WriteConfig& config)
{
    if (os_)
        *os_ << "testWriteRead() " << config << endl;

    string filename = "Serializer_mz5_Test_" + lexical_cast<string> (
            boost::this_thread::get_id()) + ".mz5";

    {
        MSData msd2;
        Serializer_mz5 mz5Serializer(config);
        IterationListenerRegistry ilr;
        mz5Serializer.write(filename, msd, &ilr);

        mz5Serializer.read(filename, msd2);

        References::resolve(msd2);

        Diff<MSData, DiffConfig> diff(msd, msd2);
        if (os_ && diff)
            *os_ << diff << endl;
        unit_assert(!diff);
    }

    bfs::remove(filename);
}

void testWriteRead()
{
    MSData msd;
    examples::initializeTiny(msd);

    // test with 64 bit precision
    MSDataFile::WriteConfig writeConfig;
    writeConfig.binaryDataEncoderConfig.precision
            = BinaryDataEncoder::Precision_64;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
            = BinaryDataEncoder::Precision_64;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array]
            = BinaryDataEncoder::Precision_64;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_time_array]
            = BinaryDataEncoder::Precision_64;
    // compression activated
    writeConfig.binaryDataEncoderConfig.compression
            = BinaryDataEncoder::Compression_Zlib;
    testWriteRead(msd, writeConfig);

    //test with 32 bit precision
    writeConfig.binaryDataEncoderConfig.precision
            = BinaryDataEncoder::Precision_32;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
            = BinaryDataEncoder::Precision_32;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array]
            = BinaryDataEncoder::Precision_32;
    writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_time_array]
            = BinaryDataEncoder::Precision_32;
    testWriteRead(msd, writeConfig);

    // TODO: test without compression
}

void testThreadSafetyWorker(boost::barrier* testBarrier)
{
    testBarrier->wait(); // wait until all threads have started

    try
    {
        testWriteRead();
    } catch (exception& e)
    {
        cerr << "Exception in worker thread: " << e.what() << endl;
    } catch (...)
    {
        cerr << "Unhandled exception in worker thread." << endl;
        exit(1); // fear the unknown!
    }
}

void testThreadSafety(const int& testThreadCount)
{
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i = 0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker,
                &testBarrier));
    testThreadGroup.join_all();
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc > 1 && !strcmp(argv[1], "-v"))
            os_ = &cout;

        testWriteRead();
        testThreadSafety(2);
        testThreadSafety(4);
        testThreadSafety(8);
        testThreadSafety(16);

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
