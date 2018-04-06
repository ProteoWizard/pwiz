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


#include "Calibrator.hpp"
#include "CalibratorTrial.hpp"
#include "MassDatabase.hpp"
#include "auto_vector.h"
#include "Random.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <algorithm>
#include <iterator>
#include <stdexcept>
#include <sstream>


using namespace std;
using namespace pwiz::math;
using namespace pwiz::calibration;
using namespace pwiz::data;


double test_true_masses_1[10] = 
{
    1393.672524,
    1923.802369,
    2078.028012,
    2271.107266,
    2358.020458,
    2443.033293,
    2524.244078,
    2920.372661,
    3031.435235,
    5426.469832
};


double test_observed_frequencies_1[10] = 
{
    77075.38157,
    55846.51464,
    103360.0612,
    94580.60383,
    91097.06527,
    87929.6722,
    85102.87241,
    73566.86176,
    70873.21484,
    59394.70403
};


double test_true_masses_2[10] = 
{
    982.4728425,
    1263.545021,
    1352.649998,
    1472.733351,
    1533.570304,
    1554.677292,
    1590.781741,
    1849.763601,
    1927.95743,
    1946.937406
};


double test_observed_frequencies_2[10] = 
{
    109302.5542,
    85007.24634,
    79411.23894,
    72940.41782,
    70048.65467,
    69098.21263,
    67530.84364,
    58080.72939,
    55726.2012,
    55183.11844
};


void runTrialTest(const string& databaseName, 
                  const string& outputDirectory, 
                  double* true_masses, 
                  double* observed_frequencies)
{
    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(databaseName);
    
    CalibratorTrial::Configuration config;
    config.massDatabase = mdb.get();
    config.calibratorIterationCount = 5;
    config.errorEstimatorIterationCount = 10;
    config.parametersInitialEstimate = CalibrationParameters::thermo_FT();
    config.initialErrorEstimate = 1e-6;
    config.outputDirectory = outputDirectory;
    config.parametersTrue = CalibrationParameters::thermo_FT();
    copy(true_masses, true_masses + 10, back_inserter(config.trueMasses)); 

    for (unsigned int i=0; i<config.trueMasses.size(); i++)
    {
        double neutralMass = config.trueMasses[i];
        int z = (int)ceil(neutralMass/2000);
        double fobs = observed_frequencies[i]; 
        config.measurements.push_back(Calibrator::Measurement(fobs,z));
    }

    auto_ptr<CalibratorTrial> trial = CalibratorTrial::create(config);
    trial->run();

    const CalibratorTrial::Results& results = trial->results();
    cout << "results: \n";
    cout << results.initial << endl; 
    cout << results.final << endl; 
}


#ifdef WIN32
const char* separator_ = "\\";
#else
const char* separator_ = "/";
#endif // WIN32


void test(const string& outputDirectory)
{
    cout.precision(10);
    const string& databaseName = "trypsin0_pro_uniq_mass.pdb";

    if (!system(("mkdir " + outputDirectory).c_str()))
        cout << "Created directory " << outputDirectory << endl;

    runTrialTest(databaseName, outputDirectory + separator_ + "1", 
                 test_true_masses_1, test_observed_frequencies_1);

    runTrialTest(databaseName, outputDirectory + separator_ + "2", 
                 test_true_masses_2, test_observed_frequencies_2);
}


vector<double> randomMasses(const MassDatabase& mdb, int count)
{
    vector<double> masses;
    for (int i=0; i<count; i++)
    {
        int index = Random::integer(0, mdb.size());
        //int index = Random::integer(0, 170000); // mass < 2000 
        MassDatabase::Entry entry = mdb.entry(index);
        masses.push_back(entry.mass);         
    }
    sort(masses.begin(), masses.end());
    return masses;
}


double perturbGaussian(double value, double relativeError)
{
    return value + Random::gaussian(relativeError*value);
}


double perturbUniform(double value, double relativeError)
{
    double delta = relativeError*value;
    return value + Random::real(-delta, delta);
}


double maxErrorA_ = 5e-6;
double maxErrorB_ = .1;
double maxErrorMeasurement_ = 10e-6;
double maxErrorEstimate_ = 10e-6;
int minMassCount_ = 10;
int maxMassCount_ = 100;


void parseConfigFile()
{
    const string filename = "simcal.cfg";
    cout << "Reading configuration file " << filename << endl;
    ifstream is(filename.c_str());
    if (!is)
        cout << filename << " not found.  Using default configuration.\n";

    while (is)
    {
        string buffer;
        getline(is, buffer);         
        if (!is) break;
        istringstream iss(buffer);
        string name;
        iss >> name;

        if (name == "maxErrorA")
            iss >> maxErrorA_;
        else if (name == "maxErrorB")
            iss >> maxErrorB_;
        else if (name == "maxErrorMeasurement")
            iss >> maxErrorMeasurement_;
        else if (name == "maxErrorEstimate")
            iss >> maxErrorEstimate_;
        else if (name == "minMassCount")
            iss >> minMassCount_;
        else if (name == "maxMassCount")
            iss >> maxMassCount_;
        else
            cout << "Ignoring unknown parameter name '" << name << "'.\n";
    }

    cout << "maxErrorA_: " << maxErrorA_ << endl; 
    cout << "maxErrorB_: " << maxErrorB_ << endl; 
    cout << "maxErrorMeasurement_: " << maxErrorMeasurement_ << endl; 
    cout << "maxErrorEstimate_: " << maxErrorEstimate_ << endl; 
    cout << "minMassCount_: " << minMassCount_ << endl; 
    cout << "maxMassCount_: " << maxMassCount_ << endl; 
}


void createTrial(const MassDatabase& mdb, const string& filename)
{
    using namespace pwiz::proteome; // for Ion

    CalibratorTrial::Configuration config;
    
    config.parametersTrue = CalibrationParameters::thermo_FT();
    
    double A = perturbUniform(thermoA_FT_, maxErrorA_); 
    double B = perturbGaussian(thermoB_FT_, maxErrorB_); 
    config.parametersInitialEstimate = CalibrationParameters(A, B); 

    config.measurementError = Random::real(0, maxErrorMeasurement_); 
    config.initialErrorEstimate = Random::real(0, maxErrorEstimate_); 

    int massCount = Random::integer(minMassCount_, maxMassCount_);
    config.trueMasses = randomMasses(mdb, massCount);

    for (unsigned int i=0; i<config.trueMasses.size(); i++)
    {
        double neutralMass = config.trueMasses[i];
        int z = (int)ceil(neutralMass/2000);
        double mz = Ion::mz(neutralMass, z);
        double f_true = config.parametersTrue.frequency(mz);
        double f = perturbGaussian(f_true, config.measurementError);
        config.measurements.push_back(Calibrator::Measurement(f,z));
    }

    config.writeTrialData(filename);
}


int digitCount(int n)
{
    int result = 0;
    while (n>0)
    {
        n/=10;
        result++;
    }
    return result;
}


const string& trialListFilename_ = "trial_list";


void createTrials(const string& databaseName, 
                  const string& outputDirectory,
                  int trialCount)
{
    cout.precision(10);
    Random::initialize();
    cout << "databaseName: " << databaseName << endl;
    cout << "outputDirectory: " << outputDirectory << endl;
    cout << "trialCount: " << trialCount << endl;

    if (trialCount < 1)
        throw runtime_error("[createTrials] Nothing to do.");

    if (!system(("mkdir " + outputDirectory).c_str()))
        cout << "Created directory " << outputDirectory << endl;

    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(databaseName);
 
    int digits = digitCount(trialCount-1);
    ofstream trialList((outputDirectory + separator_ + trialListFilename_).c_str());

    for (int i=0; i<trialCount; i++)
    {
        ostringstream filename;
        filename << "trial" << setw(digits) << setfill('0') << i;
        createTrial(*mdb, outputDirectory + separator_ + filename.str()); 
        trialList << filename.str() << endl;
    }    
}


auto_ptr<CalibratorTrial> createTrialObject(const string& filename, 
                                            const MassDatabase& mdb, 
                                            int calibratorIterationCount, 
                                            int errorEstimatorIterationCount)
{
    CalibratorTrial::Configuration config;
    config.massDatabase = &mdb;
    config.calibratorIterationCount = calibratorIterationCount;
    config.errorEstimatorIterationCount = errorEstimatorIterationCount;
    config.outputDirectory = filename + ".output";
    config.readTrialData(filename);
    return CalibratorTrial::create(config); 
}


void createTrialObjects(const string& trialDirectory,
                        const MassDatabase& mdb, 
                        int calibratorIterationCount, 
                        int errorEstimatorIterationCount,
                        auto_vector<CalibratorTrial>& result)
{
    string trialListFullPath = trialDirectory + separator_ + trialListFilename_;
    ifstream is(trialListFullPath.c_str());
    if (!is)
        throw runtime_error("[runTrials] Unable to open file " + trialListFullPath); 

    while (is)
    {
        string entry;
        is >> entry;
        if (!is) break;

        string filename = trialDirectory + separator_ + entry;
        cout << filename << endl;
        result.push_back(createTrialObject(filename, mdb, calibratorIterationCount, errorEstimatorIterationCount));
    }
}


double inverseAverageInverseFrequency(const vector<Calibrator::Measurement>& measurements)
{
    int N = measurements.size();
    if (N == 0) return 0;

    double sum = 0;
    for (int i=0; i<N; i++)
        sum += 1/measurements[i].frequency;
    return N/sum;
}


void reportResults(const auto_vector<CalibratorTrial>& trials, const string& trialDirectory)
{
    string filename = trialDirectory + "/summary.txt";
    ofstream os(filename.c_str());
    if (!os) throw runtime_error("[runTrials()] Unable to open file " + filename);
    cout << "Creating file " << filename << endl; 

    //    "0000:    91 8.902 1/<1/f> 1.0750e+008 -4.5348e+008  [  13.7745   6.5978  11.9151 ]  (0.00/0.00) 1.0750e+008 -3.6770e+008  [   8.4878   9.5236   3.2082 ]  (0.11/0.03)
    os << "                           Initial                                                               Final\n";
    os << "trial peaks error 1/<1/f>     A             B          error(true/est/cal)              id           A           B             error(true/est/cal)            id\n";
 
    for (unsigned int i=0; i<trials.size(); i++)
    {
        const CalibratorTrial& trial = *trials[i];
        const CalibratorTrial::Configuration& config = trial.configuration();
        os << setw(4) << setfill('0') << i << ": " << 
            setw(5) << setfill(' ') << config.measurements.size() << " " <<
            setprecision(3) << setw(5) << fixed << config.measurementError * 1e6 << " " <<
            inverseAverageInverseFrequency(config.measurements) << " " <<
            trial.results().initial << " " <<
            trial.results().final << endl;
    }
}


void runTrials(const string& databaseName, 
               const string& trialDirectory,
               int calibratorIterationCount,
               int errorEstimatorIterationCount)
{
    cout.precision(10);
    cout << "databaseName: " << databaseName << endl;
    cout << "trialDirectory: " << trialDirectory << endl;
    
    auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(databaseName);

    auto_vector<CalibratorTrial> trials;
    createTrialObjects(trialDirectory, 
                       *mdb, 
                       calibratorIterationCount,
                       errorEstimatorIterationCount,
                       trials);

    for (unsigned int i=0; i<trials.size(); i++)
    {
        cout << "Running trial " << i << endl;
        trials[i]->run();
    }

    reportResults(trials, trialDirectory);
}


int main(int argc, char* argv[])
{
    cout << "simcal\n";

    try
    {
        if (argc < 3)  
        {
            cout << "Usage: simcal command [options]\n";
            cout << "Commands:\n";
            cout << "    create_trials databaseName outputDirectory trialCount\n"; 
            cout << "    run_trials databaseName trialDirectory [calibratorIterations=10] [errorEstimatorIterations=10]\n"; 
            cout << "    test outputDirectory (regression tests)\n";
            return 0;
        }

        const string& command = argv[1];

        if (command=="create_trials" && argc==5)
        {
            const string& databaseName = argv[2];
            const string& outputDirectory = argv[3];
            int trialCount = atoi(argv[4]);
            parseConfigFile();
            createTrials(databaseName, outputDirectory, trialCount);
        }
        else if (command=="run_trials" && argc>=4)
        {
            const string& databaseName = argv[2];
            const string& trialDirectory = argv[3];
            int calibratorIterationCount = argc>4 ? atoi(argv[4]) : 10; 
            int errorEstimatorIterationCount = argc>5 ? atoi(argv[5]) : 10; 
            runTrials(databaseName, trialDirectory, calibratorIterationCount, errorEstimatorIterationCount); 
        }
        else if (command=="test" && argc==3)
        {
            const string& outputDirectory = argv[2];
            test(outputDirectory);
        }
        else
        {
            throw runtime_error("Unknown command.");
        }

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}


