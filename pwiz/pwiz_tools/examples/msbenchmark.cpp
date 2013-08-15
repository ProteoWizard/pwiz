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

using namespace pwiz::data;
using namespace pwiz::msdata;


/*

This example program iterates through the spectra in a data file
several different ways, recording the time taken to iterate each way.

*/


bpt::time_duration enumerateSpectra(const string& filename, bool getBinaryData)
{
    FullReaderList readers; // for vendor Reader support
    MSDataFile msd(filename, &readers);

    if (!msd.run.spectrumListPtr.get() && !msd.run.chromatogramListPtr.get())
        throw runtime_error("[msbenchmark] No spectra or chromatograms found.");

    if (!msd.run.spectrumListPtr.get())
        return bpt::time_duration();

    SpectrumList& sl = *msd.run.spectrumListPtr;

    bpt::ptime start = bpt::microsec_clock::local_time();

    size_t totalArrayLength = 0;
    for (size_t i=0, size=sl.size(); i != size; ++i)
    {
        SpectrumPtr s = sl.spectrum(i, getBinaryData);

        totalArrayLength += s->defaultArrayLength;
        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    bpt::ptime stop = bpt::microsec_clock::local_time();
    return stop - start;
}


bpt::time_duration enumerateRAMPAdapterSpectra(const string& filename, bool getBinaryData)
{
    RAMPAdapter ra(filename);

    if (ra.scanCount() == 0)
        throw runtime_error("[msbenchmark] No spectra found.");

    bpt::ptime start = bpt::microsec_clock::local_time();

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

        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    bpt::ptime stop = bpt::microsec_clock::local_time();
    return stop - start;
}


bpt::time_duration enumerateRAMPSpectra(const string& filename, bool getBinaryData)
{
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

    bpt::ptime start = bpt::microsec_clock::local_time();

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

        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating spectra: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    free(scanIndex);
    rampCloseFile(rf);

    bpt::ptime stop = bpt::microsec_clock::local_time();
    return stop - start;
}


bpt::time_duration enumerateChromatograms(const string& filename, bool getBinaryData)
{
    FullReaderList readers; // for vendor Reader support
    MSDataFile msd(filename, &readers);

    if (!msd.run.spectrumListPtr.get() && !msd.run.chromatogramListPtr.get())
        throw runtime_error("[msbenchmark] No spectra or chromatograms found.");

    if (!msd.run.chromatogramListPtr.get())
        return bpt::time_duration();

    ChromatogramList& cl = *msd.run.chromatogramListPtr;

    bpt::ptime start = bpt::microsec_clock::local_time();

    size_t totalArrayLength = 0;
    for (size_t i=0, size=cl.size(); i != size; ++i)
    {
        ChromatogramPtr c = cl.chromatogram(i, getBinaryData);

        totalArrayLength += c->defaultArrayLength;
        if (i+1 == size || ((i+1) % 100) == 0)
            cout << "Enumerating chromatograms: " << (i+1) << '/' << size << " (" << totalArrayLength << " data points)\r" << flush;
    }
    cout << endl;

    bpt::ptime stop = bpt::microsec_clock::local_time();
    return stop - start;
}


enum BenchmarkMode
{
    BenchmarkMode_Spectra,
    BenchmarkMode_Chromatograms,
    BenchmarkMode_RAMPAdapter,
    BenchmarkMode_RAMP
};


void benchmark(const char* filename, BenchmarkMode benchmarkMode, bool getBinaryData)
{
    switch (benchmarkMode)
    {
        default:
        case BenchmarkMode_Spectra:
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateSpectra(filename, getBinaryData)) << endl;
            break;
        case BenchmarkMode_Chromatograms:
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateChromatograms(filename, getBinaryData)) << endl;
            break;
        case BenchmarkMode_RAMPAdapter:
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateRAMPAdapterSpectra(filename, getBinaryData)) << endl;
            break;
        case BenchmarkMode_RAMP:
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateRAMPSpectra(filename, getBinaryData)) << endl;
            break;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 4)
        {
            cout << "Usage: msbenchmark <spectra|chromatograms|rampadapter|ramp> <binary|no-binary> <filename>\n"
                 << "Iterates over a file's spectra or chromatograms to test reader speed.\n\n"
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
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

        benchmark(filename, benchmarkMode, getBinaryData);

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
