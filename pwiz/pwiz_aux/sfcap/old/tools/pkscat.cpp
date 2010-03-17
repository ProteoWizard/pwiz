//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "data/PeakData.hpp"
#include "proteome/Ion.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <stdexcept>


using namespace std;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::proteome;


int cat(const string& filename)
{
    ifstream is(filename.c_str());
    PeakData pd;
    is >> pd;
    if (!is)
        throw runtime_error("Error reading PeakData from file " + filename);

    if (pd.scans.empty())
        throw runtime_error("No scans.");


    const Scan& scan = pd.scans[0];

    cout << fixed << setprecision(6);
    cout << "# scanNumber: " << scan.scanNumber << endl;
    cout << "# retentionTime: " << scan.retentionTime << endl;
    cout << "# observationDuration: " << scan.observationDuration << endl;
    cout << "# calibration A: " << scan.calibrationParameters.A << endl;
    cout << "# calibration B: " << scan.calibrationParameters.B << endl;
    cout << "# peak count: " << scan.peakFamilies.size() << endl;

    cout << "#\n# m/z z m frequency amplitude phase decay error area\n";

    const CalibrationParameters& cp = scan.calibrationParameters;

    for (vector<PeakFamily>::const_iterator it=scan.peakFamilies.begin(); it!=scan.peakFamilies.end(); ++it)
    {
        if (it->peaks.empty())
        {
            cout << "# empty envelope\n";
            continue;
        }

        const Peak& peak = it->peaks[0];

        double mz = cp.mz(peak.frequency);
        double neutralMass = Ion::neutralMass(mz, it->charge);

        cout << setw(14) << mz 
             << setw(3) << it->charge
             << setw(14) << neutralMass 
             << setw(14) << peak.frequency
             << setw(16) << peak.intensity
             << setw(14) << peak.phase
             << setw(14) << peak.decay
             << setw(12) << peak.error
             << setw(12) << peak.area
             << endl;
    }

    return 0;
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 2)
            throw runtime_error("Usage: pkscat filename");

        const char* filename = argv[1];
        return cat(filename);
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cout << "Unknown exception.\n"; 
        return 1;
    }
}

