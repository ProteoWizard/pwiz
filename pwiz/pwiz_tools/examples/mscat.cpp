//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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
#include <iostream>
#include <iomanip>
#include <stdexcept>


using namespace std;
using namespace pwiz;
using namespace pwiz::msdata;
using boost::shared_ptr;


/*

This example program iterates through the spectra in a data file,
writing out the m/z-intensity pairs in a text column format.

# scanNumber     msLevel         m/z   intensity
           1         ms1    204.7591        0.00
           1         ms1    204.7593        0.00
           1         ms1    204.7596     1422.17
           1         ms1    204.7598     3215.49
           1         ms1    204.7601     3887.36
           1         ms1    204.7604     2843.17
           1         ms1    204.7606      582.91
           1         ms1    204.7609        0.00
           1         ms1    204.7611        0.00

[...]

*/


void cat(const char* filename)
{
    // open the data file

    FullReaderList readers; // for vendor Reader support
    MSDataFile msd(filename, &readers);

    // verify that we have a SpectrumList

    if (!msd.run.spectrumListPtr.get())
        throw runtime_error("[mscat] No spectra found.");

    SpectrumList& spectrumList = *msd.run.spectrumListPtr;

    // write header

    const size_t columnWidth = 14;

    cout << "#"
         << setw(columnWidth) << "scanNumber"
         << setw(columnWidth) << "msLevel"
         << setw(columnWidth) << "m/z"
         << setw(columnWidth) << "intensity" << endl;

    // iterate through the spectra in the SpectrumList
    for (size_t i=0, size=spectrumList.size(); i!=size; i++)
    {
        // retrieve the spectrum, with binary data
        const bool getBinaryData = true;
        SpectrumPtr spectrum = spectrumList.spectrum(i, getBinaryData);

        // fill in MZIntensityPair vector for convenient access to binary data
        vector<MZIntensityPair> pairs;
        spectrum->getMZIntensityPairs(pairs);

        // iterate through the m/z-intensity pairs
        for (vector<MZIntensityPair>::const_iterator it=pairs.begin(), end=pairs.end(); it!=end; ++it)
        {
            cout << " "
                 << setw(columnWidth) << id::value(spectrum->id, "scan")
                 << setw(columnWidth) << "ms" + spectrum->cvParam(MS_ms_level).value
                 << setw(columnWidth) << fixed << setprecision(4) << it->mz
                 << setw(columnWidth) << fixed << setprecision(2) << it->intensity << endl;
        }
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 2)
        {
            cout << "Usage: mscat filename\n"
                 << "Write spectrum data as text to console.\n\n"
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        const char* filename = argv[1];
        cat(filename);

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

