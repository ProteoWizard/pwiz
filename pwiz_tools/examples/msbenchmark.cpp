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

using namespace pwiz::data;
using namespace pwiz::msdata;


/*

This example program iterates through the spectra in a data file
several different ways, recording the time taken to iterate each way.

*/


void enumerateSpectra(const string& filename, bool getBinaryData, const vector<string>& filters, const Reader::Config& config)
{
    bpt::ptime start = bpt::microsec_clock::local_time();

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

    bpt::ptime stop = bpt::microsec_clock::local_time();
    cout << "Time to open file: " << bpt::to_simple_string(stop - start) << endl;

    start = bpt::microsec_clock::local_time();

    SpectrumWorkerThreads multithreadedSpectrumList(sl);

    size_t totalArrayLength = 0;
    for (size_t i=0, size=sl.size(); i != size; ++i)
    {
        SpectrumPtr s = multithreadedSpectrumList.processBatch(i, getBinaryData);

        if (i == 0)
        {
            stop = bpt::microsec_clock::local_time();
            cout << "Time to get first spectrum: " << bpt::to_simple_string(stop - start) << endl;
        }

        totalArrayLength += s->defaultArrayLength;
        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    stop = bpt::microsec_clock::local_time();
    cout << "Time to enumerate: " << bpt::to_simple_string(stop - start) << endl;
}


void enumerateRAMPAdapterSpectra(const string& filename, bool getBinaryData)
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


void enumerateRAMPSpectra(const string& filename, bool getBinaryData)
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


void enumerateChromatograms(const string& filename, bool getBinaryData)
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


void benchmark(const char* filename, BenchmarkMode benchmarkMode, bool getBinaryData, const vector<string>& filters, const Reader::Config& config)
{
    switch (benchmarkMode)
    {
        default:
        case BenchmarkMode_Spectra:
            enumerateSpectra(filename, getBinaryData, filters, config);
            break;
        case BenchmarkMode_Chromatograms:
            enumerateChromatograms(filename, getBinaryData);
            break;
        case BenchmarkMode_RAMPAdapter:
            enumerateRAMPAdapterSpectra(filename, getBinaryData);
            break;
        case BenchmarkMode_RAMP:
            enumerateRAMPSpectra(filename, getBinaryData);
            break;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 4 || argv[1] == string("--help"))
        {
            cout << "Usage: msbenchmark <spectra|chromatograms|rampadapter|ramp> <binary|no-binary> <filename> [--filter <filter name> <options>] [another filter] \n"
                 << "Iterates over a file's spectra or chromatograms to test reader speed.\n\n"
                 << "http://proteowizard.sourceforge.net\n"
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

        bool getBinaryData;
        if (argv[2] == string("binary"))
            getBinaryData = true;
        else if (argv[2] == string("no-binary"))
            getBinaryData = false;
        else
            throw runtime_error("[msbenchmark] Second argument must be \"binary\" or \"no-binary\"");

        Reader::Config readerConfig;

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
            else
                throw runtime_error("[msbenchmark] unknown option \"" + string(argv[i]) + "\"");

        benchmark(filename, benchmarkMode, getBinaryData, filters, readerConfig);

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
