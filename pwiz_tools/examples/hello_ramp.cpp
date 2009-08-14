//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "pwiz/data/msdata/RAMPAdapter.hpp"
#include <iostream>
#include <stdexcept>
#include <algorithm>


using namespace std;
using namespace pwiz::msdata;


void test(const char* filename)
{
    RAMPAdapter adapter(filename);

    InstrumentStruct instrument;
    adapter.getInstrument(instrument);
    cout << "manufacturer: " << instrument.manufacturer << endl;
    cout << "model: " << instrument.model << endl;
    cout << "ionisation: " << instrument.ionisation << endl;
    cout << "analyzer : " << instrument.analyzer << endl;
    cout << "detector: " << instrument.detector << endl;

    size_t scanCount = adapter.scanCount();
    cout << "scanCount: " << scanCount << "\n\n";

    for (size_t i=0; i<scanCount; i++)
    {
        ScanHeaderStruct header;
        adapter.getScanHeader(i, header);

        cout << "index: " << i << endl;
        cout << "seqNum: " << header.seqNum << endl;
        cout << "acquisitionNum: " << header.acquisitionNum << endl;
        cout << "msLevel: " << header.msLevel << endl;
        cout << "peaksCount: " << header.peaksCount << endl;
        cout << "precursorScanNum: " << header.precursorScanNum << endl;
        cout << "filePosition: " << header.filePosition << endl;

        vector<double> peaks;
        adapter.getScanPeaks(i, peaks);

        for (unsigned int j=0; j<min(peaks.size(),(size_t)20); j+=2)
            cout << "  (" << peaks[j] << ", " << peaks[j+1] << ")\n";

        cout << endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc<2) throw runtime_error("Usage: hello_ramp filename");
        test(argv[1]);
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

