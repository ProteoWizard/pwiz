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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/RAMPAdapter.hpp"
#include "pwiz/data/msdata/ramp/ramp.h"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/data/msdata/SpectrumWorkerThreads.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <boost/chrono.hpp>

#ifdef _WIN32
#include "windows.h"
#include "psapi.h"
#endif


using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;


/*

This example program iterates through the spectra in a data file
several different ways, recording the time taken to iterate each way.

*/

string keyValueProcessTimes(const boost::chrono::process_cpu_clock_times& times)
{
#ifdef _WIN32
    PROCESS_MEMORY_COUNTERS_EX pmc;
    GetProcessMemoryInfo(GetCurrentProcess(), (PPROCESS_MEMORY_COUNTERS)&pmc, sizeof(pmc));

    return (boost::format("(real: %.3f; user: %.3f; sys: %.3f) seconds; memory usage: cur=%d peak=%d")
            % (times.real / 1e9)
            % (times.user / 1e9)
            % (times.system / 1e9)
            % abbreviate_byte_size(pmc.PrivateUsage, ByteSizeAbbreviation_IEC)
            % abbreviate_byte_size(pmc.PeakWorkingSetSize, ByteSizeAbbreviation_IEC)).str();
#else
    return (boost::format("(real: %.3f; user: %.3f; sys: %.3f) seconds")
            % (times.real / 1e9)
            % (times.user / 1e9)
            % (times.system / 1e9)).str();
 #endif
}

class UserFeedbackIterationListener : public IterationListener
{
    std::streamoff longestMessage;
    boost::chrono::time_point<boost::chrono::process_cpu_clock> start;

    public:

    UserFeedbackIterationListener()
    {
        longestMessage = 0;
        start = boost::chrono::process_cpu_clock::now();
    }

    virtual Status update(const UpdateMessage& updateMessage)
    {
        auto stop = boost::chrono::process_cpu_clock::now();

        stringstream updateString;
        if (updateMessage.message.empty())
            updateString << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;
        else
            updateString << updateMessage.message << ": " << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;
        updateString << " " << keyValueProcessTimes((stop - start).count());

        longestMessage = max(longestMessage, (std::streamoff) updateString.tellp());
        updateString << string(longestMessage - updateString.tellp(), ' '); // add whitespace to erase all of the previous line
        cout << updateString.str() << endl;// "\r" << flush;

        // spectrum and chromatogram lists both iterate; put them on different lines
        if (updateMessage.iterationIndex + 1 == updateMessage.iterationCount)
            cout << endl;
        return Status_Ok;
    }
};

void enumerateSpectra(const string& filename, DetailLevel detailLevel, const vector<string>& filters, const Reader::Config& config, bool reverseIteration, bool useWorkerThreads)
{
    auto start = boost::chrono::process_cpu_clock::now();

    FullReaderList readers; // for vendor Reader support
    vector<MSDataPtr> results;
    readers.read(filename, results, config);
    MSData& msd = *results[0];

    if (!msd.run.spectrumListPtr.get() && !msd.run.chromatogramListPtr.get())
        throw runtime_error("[msbenchmark] No spectra or chromatograms found.");

    if (!msd.run.spectrumListPtr.get())
        return;

    pwiz::analysis::SpectrumListFactory::wrap(msd, filters);

    SpectrumList& sl = *msd.run.spectrumListPtr;
    auto stop = boost::chrono::process_cpu_clock::now();
    cout << "Time to open file: " << keyValueProcessTimes((stop - start).count()) << endl;

    start = boost::chrono::process_cpu_clock::now();

    SpectrumWorkerThreads multithreadedSpectrumList(sl, useWorkerThreads);

    IterationListenerRegistry ilr;
    IterationListenerPtr il(new UserFeedbackIterationListener);
    ilr.addListenerWithTimer(il, 1);

    size_t totalArrayLength = 0;
    string message = "Enumerating spectra";
    if (reverseIteration)
    {
        for (size_t size = sl.size(), i = size; i > 0; --i)
        {
            SpectrumPtr s = multithreadedSpectrumList.processBatch(i-1, detailLevel);

            if (i == size)
            {
                stop = boost::chrono::process_cpu_clock::now();
                cout << "Time to get first spectrum: " << keyValueProcessTimes((stop - start).count()) << endl;
            }
            totalArrayLength += s->getMZArray()->data.size();

            //if (i == 1 || (i % 100) == 0)
                ilr.broadcastUpdateMessage(IterationListener::UpdateMessage(i, size, message + " (" + lexical_cast<string>(totalArrayLength) + " data points)"));
        }
    }
    else
        for (size_t i=0, size=sl.size(); i != size; ++i)
        {
            SpectrumPtr s = multithreadedSpectrumList.processBatch(i, detailLevel);

            if (i == 0)
            {
                stop = boost::chrono::process_cpu_clock::now();
                cout << "Time to get first spectrum: " << keyValueProcessTimes((stop - start).count()) << endl;
            }

            totalArrayLength += s->getMZArray()->data.size();
            //if (i+1 == size || ((i+1) % 100) == 0)
                ilr.broadcastUpdateMessage(IterationListener::UpdateMessage(i, size, message + " (" + lexical_cast<string>(totalArrayLength) + " data points)"));
        }
    cout << endl;

    stop = boost::chrono::process_cpu_clock::now();
    cout << "Time to enumerate: " << keyValueProcessTimes((stop - start).count()) << endl;
}


void enumerateRAMPAdapterSpectra(const string& filename, bool getBinaryData, bool reverseIteration)
{
    bpt::ptime start = bpt::microsec_clock::local_time();

    RAMPAdapter ra(filename);

    if (ra.scanCount() == 0)
        throw runtime_error("[msbenchmark] No spectra found.");

    bpt::ptime stop = bpt::microsec_clock::local_time();
    cout << "Time to open file: " << bpt::to_simple_string(stop - start) << endl;

    start = bpt::microsec_clock::local_time();

    size_t totalArrayLength = 0;
    for (size_t i=0, size=ra.scanCount(); i != size; ++i)
    {
        ScanHeaderStruct scanHeader;
        ra.getScanHeader(i, scanHeader);

        if (getBinaryData)
        {
            vector<double> scanPeaks;
            ra.getScanPeaks(i, scanPeaks);
            totalArrayLength += scanPeaks.size() / 2;
        }
        else
            totalArrayLength += scanHeader.peaksCount;

        if (i == 0)
        {
            stop = bpt::microsec_clock::local_time();
            cout << "Time to get first spectrum: " << bpt::to_simple_string(stop - start) << endl;
        }

        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    stop = bpt::microsec_clock::local_time();
    cout << "Time to enumerate: " << bpt::to_simple_string(stop - start) << endl;
}


void enumerateRAMPSpectra(const string& filename, bool getBinaryData, bool reverseIteration)
{
    bpt::ptime start = bpt::microsec_clock::local_time();

    RAMPFILE* rf = rampOpenFile(filename.c_str());
    if (!rf->fileHandle)
        throw runtime_error("[msbenchmark] Error opening RAMPFILE.");

    ramp_fileoffset_t* scanIndex;
    int lastScan;
    scanIndex = readIndex(rf, getIndexOffset(rf), &lastScan);

    RunHeaderStruct runHeader;
    readMSRun(rf, &runHeader);
    //readRunHeader(rf, scanIndex, &runHeader, lastScan);

    if (runHeader.scanCount == 0)
        throw runtime_error("[msbenchmark] No spectra found.");

    bpt::ptime stop = bpt::microsec_clock::local_time();
    cout << "Time to open file: " << bpt::to_simple_string(stop - start) << endl;

    start = bpt::microsec_clock::local_time();

    size_t totalArrayLength = 0;
    for (size_t i=0, size=runHeader.scanCount; i != size; ++i)
    {
        ScanHeaderStruct scanHeader;
        readHeader(rf, scanIndex[i], &scanHeader);

        if (getBinaryData && scanHeader.peaksCount > 0)
        {
            RAMPREAL* peaks = readPeaks(rf, scanIndex[i]);
            totalArrayLength += scanHeader.peaksCount;
            free(peaks);
        }
        else
            totalArrayLength += scanHeader.peaksCount;

        if (i == 0)
        {
            stop = bpt::microsec_clock::local_time();
            cout << "Time to get first spectrum: " << bpt::to_simple_string(stop - start) << endl;
        }

        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    free(scanIndex);
    rampCloseFile(rf);

    stop = bpt::microsec_clock::local_time();
    cout << "Time to enumerate: " << bpt::to_simple_string(stop - start) << endl;
}


void enumerateChromatograms(const string& filename, bool getBinaryData, bool reverseIteration)
{
    bpt::ptime start = bpt::microsec_clock::local_time();

    FullReaderList readers; // for vendor Reader support
    MSDataFile msd(filename, &readers);

    if (!msd.run.spectrumListPtr.get() && !msd.run.chromatogramListPtr.get())
        throw runtime_error("[msbenchmark] No spectra or chromatograms found.");

    if (!msd.run.chromatogramListPtr.get())
        return;

    ChromatogramList& cl = *msd.run.chromatogramListPtr;

    bpt::ptime stop = bpt::microsec_clock::local_time();
    cout << "Time to open file: " << bpt::to_simple_string(stop - start) << endl;

    start = bpt::microsec_clock::local_time();

    size_t totalArrayLength = 0;
    for (size_t i=0, size=cl.size(); i != size; ++i)
    {
        ChromatogramPtr c = cl.chromatogram(i, getBinaryData);

        if (i == 0)
        {
            stop = bpt::microsec_clock::local_time();
            cout << "Time to get first chromatogram: " << bpt::to_simple_string(stop - start) << endl;
        }

        totalArrayLength += c->defaultArrayLength;
        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating chromatograms: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    stop = bpt::microsec_clock::local_time();
    cout << "Time to enumerate: " << bpt::to_simple_string(stop - start) << endl;
}


enum BenchmarkMode
{
    BenchmarkMode_Spectra,
    BenchmarkMode_Chromatograms,
    BenchmarkMode_RAMPAdapter,
    BenchmarkMode_RAMP
};


void benchmark(const char* filename, BenchmarkMode benchmarkMode, DetailLevel detailLevel, const vector<string>& filters, const Reader::Config& config, bool reverseIteration, bool useWorkerThreads)
{
    switch (benchmarkMode)
    {
        default:
        case BenchmarkMode_Spectra:
            enumerateSpectra(filename, detailLevel, filters, config, reverseIteration, useWorkerThreads);
            break;
        case BenchmarkMode_Chromatograms:
            enumerateChromatograms(filename, (detailLevel == DetailLevel_FullData), reverseIteration);
            break;
        case BenchmarkMode_RAMPAdapter:
            enumerateRAMPAdapterSpectra(filename, (detailLevel == DetailLevel_FullData), reverseIteration);
            break;
        case BenchmarkMode_RAMP:
            enumerateRAMPSpectra(filename, (detailLevel == DetailLevel_FullData), reverseIteration);
            break;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 4 || argv[1] == string("--help"))
        {
            cout << "Usage: msbenchmark <spectra|chromatograms|rampadapter|ramp> <full-data|full-metadata|fast-metadata|instant-metadata> <filename> [--filter <filter name> <options>] [another filter] [optional flags]\n\n"
                 << "Iterates over a file's spectra or chromatograms to test reader speed.\n\n"
                 << "See msconvert documentation for supported --filter's.\n\n"
                 << "Optional flags are:\n"
                 << "  --acceptZeroLengthSpectra (skip expensive checking for empty spectra when opening a file)\n"
                 << "  --ignoreZeroIntensityPoints (read profile data exactly as the vendor provides, even if there are no flanking zero points)\n"
                 << "  --loop (repeat the run indefinitely)\n"
                 << "  --singleThreaded (do not use multiple threads to read spectra)\n"
                 << "  --reverse (iterate backwards)\n\n"
                 << "https://github.com/ProteoWizard\n"
                 << "support@proteowizard.org\n";

            if (argv[1] == string("--help"))
                pwiz::analysis::SpectrumListFactory::usage();

            return 1;
        }

        const char* filename = argv[3];

        BenchmarkMode benchmarkMode;
        if (argv[1] == string("spectra"))
            benchmarkMode = BenchmarkMode_Spectra;
        else if (argv[1] == string("chromatograms"))
            benchmarkMode = BenchmarkMode_Chromatograms;
        else if (argv[1] == string("rampadapter"))
            benchmarkMode = BenchmarkMode_RAMPAdapter;
        else if (argv[1] == string("ramp"))
            benchmarkMode = BenchmarkMode_RAMP;
        else
            throw runtime_error("[msbenchmark] First argument must be \"spectra\", \"chromatograms\", \"rampadapter\", or \"ramp\"");

        DetailLevel detailLevel;
        if (argv[2] == string("binary") || argv[2] == string("full-data"))
            detailLevel = DetailLevel_FullData;
        else if (argv[2] == string("no-binary") || argv[2] == string("full-metadata"))
            detailLevel = DetailLevel_FullMetadata;
        else if (argv[2] == string("fast-metadata"))
            detailLevel = DetailLevel_FastMetadata;
        else if (argv[2] == string("instant-metadata"))
            detailLevel = DetailLevel_InstantMetadata;
        else
            throw runtime_error("[msbenchmark] Second argument must be one of [full-data, full-metadata, fast-metadata, instant-metadata]");

        Reader::Config readerConfig;
        bool reverseIteration = false;
        bool loop = false;
        bool useWorkerThreads = true;

        vector<string> filters;
        for (int i = 4; i < argc; i += 2)
            if (argv[i] == string("--filter"))
            {
                if (i + 1 == argc)
                    throw runtime_error("[msbenchmark] no options passed to --filter parameter");
                filters.push_back(argv[i + 1]);
            }
            else if (argv[i] == string("--ignoreZeroIntensityPoints"))
            {
                readerConfig.ignoreZeroIntensityPoints = true;
                --i;
            }
            else if (argv[i] == string("--acceptZeroLengthSpectra"))
            {
                readerConfig.acceptZeroLengthSpectra = true;
                --i;
            }
            else if (argv[i] == string("--loop"))
            {
                loop = true;
                --i;
            }
            else if (argv[i] == string("--singleThreaded"))
            {
                useWorkerThreads = false;
                --i;
            }
            else if (argv[i] == string("--reverse"))
            {
                reverseIteration = true;
                --i;
            }
            else
                throw runtime_error("[msbenchmark] unknown option \"" + string(argv[i]) + "\"");

        do
        {
            benchmark(filename, benchmarkMode, detailLevel, filters, readerConfig, reverseIteration, useWorkerThreads);

#ifdef _WIN32
            PROCESS_MEMORY_COUNTERS_EX pmc;
            GetProcessMemoryInfo(GetCurrentProcess(), (PPROCESS_MEMORY_COUNTERS) &pmc, sizeof(pmc));
            cout << "Final memory usage: cur " << abbreviate_byte_size(pmc.PrivateUsage, ByteSizeAbbreviation_IEC)
                 << ", peak " << abbreviate_byte_size(pmc.PeakWorkingSetSize, ByteSizeAbbreviation_IEC) << endl;
#endif
        } while (loop);

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
