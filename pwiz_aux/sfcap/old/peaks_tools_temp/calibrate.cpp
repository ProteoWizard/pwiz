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


#include "calibration/Calibrator.hpp"
#include "calibration/MassDatabase.hpp"
#include "data/FrequencyData.hpp"
#include "data/CalibrationParameters.hpp"
#include <iostream>
#include <fstream>
#include <stdexcept>
#include <iterator>


using namespace std;
using namespace pwiz::calibration;
using namespace pwiz::data;


double initialErrorEstimate_ = 5e-6;
int errorEstimatorIterationCount_ = 20;
int calibratorIterationCount_ = 20;
string outputDirectory_ = "calibrate.temp"; 


vector<Calibrator::Measurement> readMeasurements(const string& filename_freqz)
{
    vector<Calibrator::Measurement> result;
    
    ifstream is(filename_freqz.c_str());
    if (!is) throw runtime_error("Couldn't open file " + filename_freqz);

    while (is)
    {
        Calibrator::Measurement measurement;
        is >> measurement.frequency >> measurement.charge;
        if (is) result.push_back(measurement);
    }

    return result;
}


// for testing
namespace pwiz {
namespace calibration {
ostream& operator<<(ostream& os, const Calibrator::Measurement& m)
{
    os << m.frequency << " " << m.charge;
    return os;
}
}}//pwiz::calibration


void reportMasses(const vector<Calibrator::Measurement>& measurements, 
                  const CalibrationParameters& parameters)
{
    ofstream os("masses_final");
    if (!os) throw runtime_error("Unable to open masses_final");
    cout << "Writing masses_final.\n";

    os.precision(12);

    for (vector<Calibrator::Measurement>::const_iterator it=measurements.begin();
         it!=measurements.end(); ++it)
    {
        double mz = parameters.mz(it->frequency);
        double mass = mz * it->charge;
        os << it->frequency << " " << mz << " " << it->charge << " " << mass << endl; 
    }
}


void calibrate(const string& filename_freqz, const string& filename_cfd)
{
    const string& pdbFilename = "trypsin0_pro_uniq_mass.pdb";
    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(pdbFilename); 

    vector<Calibrator::Measurement> measurements = readMeasurements(filename_freqz);

    if (measurements.empty())
        throw runtime_error("No measurements read.");

    FrequencyData fd(filename_cfd);
    CalibrationParameters parameters(fd.calibration().A, fd.calibration().B);

    system(("mkdir " + outputDirectory_).c_str());

    auto_ptr<Calibrator> calibrator = Calibrator::create(*mdb,
                                                         measurements,
                                                         parameters,
                                                         initialErrorEstimate_,
                                                         errorEstimatorIterationCount_,
                                                         outputDirectory_);

    cout << calibrator->parameters() << " " << calibrator->error() << endl;
    for (int i=0; i<calibratorIterationCount_; i++)
    {
        calibrator->iterate();
        cout << calibrator->parameters() << " " << calibrator->error() << endl;
    }

    reportMasses(measurements, calibrator->parameters());
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 3)
        {
            cout << "Usage: calibrate freqz.txt data.cfd\n";
            cout << "Parameters:\n";
            cout << "  freqz.txt: 2-column text file with freq charge\n";
            cout << "  data.cfd:  frequency data file for initial calibration parameters\n";            
            return 0;
        }
       
        const string& filename_freqz = argv[1];
        const string& filename_cfd = argv[2];

        calibrate(filename_freqz, filename_cfd);

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

