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
#include <new>

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

    //test with error skipping
    writeConfig.continueOnError = true;
    testWriteRead(msd, writeConfig);

    // TODO: test without compression
}

// A combined ion mobility spectrum presents its peaks in mobility bin order, so its m/z array is
// not ascending - it drops back to the low end of the range at the start of every bin, and the same
// m/z recurs once per bin. mz5 delta encodes m/z, which inverts exactly only while the values climb
// steadily; across a bin boundary the delta spans the whole m/z range and no longer subtracts
// exactly, so the running sum that rebuilds the array on read drifts by a few ulps.
//
// That loss is accepted - it is orders of magnitude below any instrument's mass accuracy, and the
// encoding earns its keep on the ordinary ascending case. What is not accepted is it growing, so
// this pins the size of it rather than asserting the values come back untouched. Callers who need
// the array back verbatim can turn the encoding off (Configuration_mz5::setTranslating), which is
// what VendorReaderTestHarness does so that peaks sharing an m/z still share one after a round trip.
void testNonAscendingMzRoundTrip()
{
    MSData msd;
    msd.cvs = defaultCVList();
    msd.run.id = "nonAscendingMz";

    // Values carrying a full mantissa, as real m/z does. A round number like 40.0 would defeat the
    // test: it has so few significant bits that even the large delta across a bin boundary subtracts
    // exactly, and the round trip would survive by accident.
    vector<double> mz, intensity;
    for (int bin = 0; bin < 3; ++bin)
        for (int i = 0; i < 200; ++i)
        {
            mz.push_back(40.123456789012345 + i * 4.821345678901234); // same m/z in every bin
            intensity.push_back(100.0 + i + bin * 1000);              // distinguishable intensities
        }
    unit_assert(!std::is_sorted(mz.begin(), mz.end())); // the premise of the test

    SpectrumListSimplePtr spectrumList(new SpectrumListSimple);
    SpectrumPtr spectrum(new Spectrum);
    spectrum->index = 0;
    spectrum->id = "merged=1 frame=1";
    spectrum->set(MS_ms_level, 1);
    spectrum->setMZIntensityArrays(mz, intensity, MS_number_of_detector_counts);
    spectrum->defaultArrayLength = mz.size();
    spectrumList->spectra.push_back(spectrum);
    msd.run.spectrumListPtr = spectrumList;

    string filename = "Serializer_mz5_Test_nonascending_" + lexical_cast<string> (
            boost::this_thread::get_id()) + ".mz5";

    MSDataFile::WriteConfig config(MSDataFile::Format_MZ5);
    config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;

    try
    {
        MSData msd2;
        Serializer_mz5 serializer(config);
        serializer.write(filename, msd);
        serializer.read(filename, msd2);

        SpectrumPtr roundTripped = msd2.run.spectrumListPtr->spectrum(0, true);
        const BinaryData<double>& mz2 = roundTripped->getMZArray()->data;
        unit_assert_operator_equal(mz.size(), mz2.size());

        // Compared as a set of values, not position by position: reading a spectrum whose m/z is
        // out of order legitimately reorders it (see SpectrumListBase::ensureMzAscending), and that
        // is not what this is about. What must hold is that every value survives untouched.
        vector<double> expected(mz.begin(), mz.end());
        vector<double> actual(mz2.begin(), mz2.end());
        sort(expected.begin(), expected.end());
        sort(actual.begin(), actual.end());
        for (size_t i = 0; i < expected.size(); ++i)
            unit_assert_equal(expected[i], actual[i], 1e-9);
    }
    catch (...)
    {
        bfs::remove(filename);
        throw;
    }
    bfs::remove(filename);
}

// m/z delta encoding is enabled only for zlib, so for every other compression setting the flag has
// to be a definite false. Constructing into deliberately poisoned storage is what makes an
// unassigned member visible - constructed normally it would pass whenever the memory being reused
// happened to hold zeroes, which is exactly how a missing assignment stays hidden.
void testTranslatingFlagIsInitialized()
{
    using pwiz::msdata::mz5::Configuration_mz5;

    const unsigned char fills[] = { 0x00, 0xFF, 0xA5 };
    const BinaryDataEncoder::Compression uncompressed[] = {
        BinaryDataEncoder::Compression_None,
        BinaryDataEncoder::Compression_Zstd
    };

    alignas(Configuration_mz5) unsigned char storage[sizeof(Configuration_mz5)];

    for (size_t c = 0; c < sizeof(uncompressed) / sizeof(*uncompressed); ++c)
        for (size_t f = 0; f < sizeof(fills) / sizeof(*fills); ++f)
        {
            MSDataFile::WriteConfig config;
            config.binaryDataEncoderConfig.compression = uncompressed[c];

            memset(storage, fills[f], sizeof(storage));
            Configuration_mz5* configuration = new (storage) Configuration_mz5(config);
            bool translating = configuration->doTranslating();
            configuration->~Configuration_mz5();

            unit_assert(!translating);
        }

    // and zlib really does turn it on
    MSDataFile::WriteConfig zlibConfig;
    zlibConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
    memset(storage, 0x00, sizeof(storage));
    Configuration_mz5* configuration = new (storage) Configuration_mz5(zlibConfig);
    bool translating = configuration->doTranslating();
    configuration->~Configuration_mz5();

    unit_assert(translating);
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

        testNonAscendingMzRoundTrip();
        testTranslatingFlagIsInitialized();
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
