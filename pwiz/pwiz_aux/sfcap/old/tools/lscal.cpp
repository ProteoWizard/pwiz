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


#include "calibration/LeastSquaresCalibrator.hpp"
#include "proteome/Ion.hpp"
#include <iostream>
#include <fstream>
#include <iomanip>
#include <string>
#include <vector>
#include <iterator>
#include <stdexcept>


using namespace std;
using namespace pwiz::calibration;
using namespace pwiz::data;
using namespace pwiz::proteome;


int calibrate(const char* filename)
{
    vector<double> masses;
    vector<double> frequencies;
    vector<int> charges;

    ifstream is(filename);
    while (is)
    {
        double mz = 0;
        double frequency = 0;
        int charge = 0;
        is >> mz >> frequency >> charge;
        if (!is) break;

        masses.push_back(mz);
        frequencies.push_back(frequency);
        charges.push_back(charge);
    }

    cout << setprecision(6) << fixed;

    cout << "masses:\n";
    copy(masses.begin(), masses.end(), ostream_iterator<double>(cout, "\n"));

    cout << "frequencies:\n";
    copy(frequencies.begin(), frequencies.end(), ostream_iterator<double>(cout, "\n"));

    auto_ptr<LeastSquaresCalibrator> calibrator = LeastSquaresCalibrator::create(masses, frequencies);
    calibrator->calibrate();
    cout << "error: " << calibrator->error() << endl;
    CalibrationParameters cp = calibrator->parameters(); 
    cout << "A: " << cp.A << endl; 
    cout << "B: " << cp.B << endl; 

    vector<double> massesCalibrated;
    for (vector<double>::const_iterator it=frequencies.begin(); it!=frequencies.end(); ++it)
        massesCalibrated.push_back(cp.mz(*it));

    cout << "massesCalibrated:\n";
    copy(massesCalibrated.begin(), massesCalibrated.end(), ostream_iterator<double>(cout, "\n"));

    cout << "summary:\n";
    for (unsigned int i=0; i<masses.size(); i++)
        cout << setw(12) << masses[i]
             << setw(12) << frequencies[i] 
             << setw(4) << charges[i]
             << setw(12) << massesCalibrated[i]
             << setw(12) << Ion::neutralMass(massesCalibrated[i], charges[i])
             << endl;

    return 0;
}


int main(int argc, char* argv[])
{
    try 
    {
        if (argc < 2)
            throw runtime_error("Usage: lscal filename");

        const char* filename = argv[1];
        return calibrate(filename);
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
    }
    catch (...)
    {
        cout << "Unknown exception.\n";
    }

    return 1;
}


