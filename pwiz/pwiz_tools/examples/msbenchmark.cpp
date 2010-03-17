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
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>


using namespace std;
using namespace pwiz::data;
using namespace pwiz::msdata;
using boost::shared_ptr;


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


void benchmark(const char* filename, bool iterateSpectra, bool getBinaryData)
{
    if (iterateSpectra)
    {
        if (getBinaryData)
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateSpectra(filename, true)) << endl;
        else
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateSpectra(filename, false)) << endl;
    }
    else
    {
        if (getBinaryData)
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateChromatograms(filename, true)) << endl;
        else
            cout << "Time elapsed: " << bpt::to_simple_string(enumerateChromatograms(filename, false)) << endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 4)
        {
            cout << "Usage: msbenchmark <spectra|chromatograms> <binary|no-binary> <filename>\n"
                 << "Iterates over a file's spectra or chromatograms to test reader speed.\n\n"
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        const char* filename = argv[3];

        bool iterateSpectra;
        if (argv[1] == string("spectra"))
            iterateSpectra = true;
        else if (argv[1] == string("chromatograms"))
            iterateSpectra = false;
        else
            throw runtime_error("[msbenchmark] First argument must be \"spectra\" or \"chromatograms\"");

        bool getBinaryData;
        if (argv[2] == string("binary"))
            getBinaryData = true;
        else if (argv[2] == string("no-binary"))
            getBinaryData = false;
        else
            throw runtime_error("[msbenchmark] Second argument must be \"binary\" or \"no-binary\"");

        benchmark(filename, iterateSpectra, getBinaryData);

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
