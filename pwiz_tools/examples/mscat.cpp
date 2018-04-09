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
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::msdata;


/*

This example program iterates through the spectra and chromatograms in a data file,
writing out the m/z-intensity and time-intensity pairs in a text column format.

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

#  index                          id          time     intensity
       0                         TIC       30.9310         37.21
       0                         TIC       30.9392         95.23
       0                         TIC       30.9477         37.20
       0                         TIC       30.9558         99.50
       1     SRM SIC 484.944,500.319       30.9310         34.29
       1     SRM SIC 484.944,500.319       30.9477         33.29
       1     SRM SIC 484.944,500.319       30.9643         29.88
       1     SRM SIC 484.944,500.319       30.9809         24.00

[...]

*/


void cat(const char* filename)
{
    // open the data file

    FullReaderList readers; // for vendor Reader support
    MSDataFile msd(filename, &readers);

    // verify that we have a SpectrumList

    if (!msd.run.spectrumListPtr.get() || !msd.run.spectrumListPtr->size())
    {
        if (!msd.run.chromatogramListPtr.get() || !msd.run.chromatogramListPtr->size())
            throw runtime_error("[mscat] No spectra or chromatograms found.");
    }
    else
    {

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
    if (msd.run.chromatogramListPtr.get() && msd.run.chromatogramListPtr->size())
    {
        ChromatogramList& chromatogramList = *msd.run.chromatogramListPtr;

        // write header

        const size_t columnWidth = 14;

        cout << "#"
             << setw(columnWidth/2) << "index"
             << setw(columnWidth*2) << "id"
             << setw(columnWidth) << "time"
             << setw(columnWidth) << "intensity" << endl;

        // iterate through the spectra in the chromatogramList
        for (size_t i=0, size=chromatogramList.size(); i!=size; i++)
        {
            // retrieve the chromatogram, with binary data
            const bool getBinaryData = true;
            ChromatogramPtr chromatogram = chromatogramList.chromatogram(i, getBinaryData);

            // fill in TimeIntensityPair vector for convenient access to binary data
            vector<TimeIntensityPair> pairs;
            chromatogram->getTimeIntensityPairs(pairs);

            // iterate through the m/z-intensity pairs
            for (vector<TimeIntensityPair>::const_iterator it=pairs.begin(), end=pairs.end(); it!=end; ++it)
            {
                cout << " "
                    << setw(columnWidth/2) << chromatogram->index
                     << setw(columnWidth*2) << chromatogram->id
                     << setw(columnWidth) << fixed << setprecision(4) << it->time
                     << setw(columnWidth) << fixed << setprecision(2) << it->intensity << endl;
            }
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
                 << "Write spectrum and chromatogram data as text to console.\n\n"
                 << "https://github.com/ProteoWizard\n"
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

