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


#include "ErrorEstimator.hpp"
#include "MassSpread.hpp"
#include "MassDatabase.hpp"
#include "PeptideDatabase.hpp"
#include "Random.hpp"// TODO: remove this
#include "Path.h" // TODO: remove this
#include <iostream>
#include <fstream>
#include <string>
#include <iomanip>
#include <iterator>
#include <stdexcept>


using namespace std;
using namespace pwiz::math;
using namespace pwiz::calibration;
using namespace pwiz::proteome;
using namespace pwiz::msaux; // TODO: remove this


class Main
{
    public:

    Main(const MassDatabase& massDatabase,
         int measurementCount,
         double errorTrue,
         double errorEstimate,
         int iterationCount,
         const string& outputDirectory);

    void run();
    void runTest();
    void runTest2();

    private:

    const MassDatabase& massDatabase_;
    int measurementCount_;
    double errorTrue_;
    double errorEstimate_;
    int iterationCount_;
    string outputDirectory_;

    ofstream osOutput_;
    ofstream osDistribution_;

    vector<double> targets_;
    vector<double> measurements_;

    void initializeTargets();
    void initializeMeasurements();
    void outputInitialState();
    void outputIterationState(const ErrorEstimator* estimator, int iteration);
};


#ifdef WIN32
const char* separator_ = "\\";
#else
const char* separator_ = "/";
#endif // WIN32


Main::Main(const MassDatabase& massDatabase,
           int measurementCount,
           double errorTrue,
           double errorEstimate,
           int iterationCount,
           const string& outputDirectory)
:   massDatabase_(massDatabase),
    measurementCount_(measurementCount),
    errorTrue_(errorTrue),
    errorEstimate_(errorEstimate),
    iterationCount_(iterationCount),
    outputDirectory_(outputDirectory)
{
    if (Path::mkdir(outputDirectory_))
        cout << "Created directory " << outputDirectory_ << endl;

    string filenameOutput_ = outputDirectory_ + "/simerr.output.txt";
    string filenameDistribution_ = outputDirectory_ + "/simerr.distribution.txt";

    osOutput_.open(filenameOutput_.c_str());
    if (!osOutput_)
        throw runtime_error("Unable to open file " + filenameOutput_);
    cout << "Creating file " << filenameOutput_ << endl;
    osOutput_.precision(10);

    osDistribution_.open(filenameDistribution_.c_str());
    cout << "Creating file " << filenameDistribution_ << endl;
    osDistribution_.precision(10);
}


void Main::initializeTargets()
{
    // pull random targets from database
    for (int i=0; i<measurementCount_; i++)
    {
        int index = Random::integer(0, massDatabase_.size());  
        MassDatabase::Entry entry = massDatabase_.entry(index);
        targets_.push_back(entry.mass);
    }
}


void Main::initializeMeasurements()
{
    // calculate random measurements based on targets and errorTrue
    for (int i=0; i<measurementCount_; i++)
    {
        double target = targets_[i];
        double measurement = target + Random::gaussian(target*errorTrue_);
        measurements_.push_back(measurement);
    }
}


namespace {

void printTriple(ostream& os, int a, double b, double c)
{
    os << left << setw(4) << a << " " << fixed << setw(12) << b << " " << c << endl;
}

void printMasses(const string& filename,
                 int iterationCount,
                 const vector<double>& masses,
                 double probability)
{
    cout << "Creating file " << filename << endl;
    ofstream os(filename.c_str());
    os.precision(10);
    for (int iteration=0; iteration<iterationCount; iteration++)
    for (unsigned int massIndex=0; massIndex<masses.size(); massIndex++)
        printTriple(os, iteration, masses[massIndex], probability);
}

} //namespace


void Main::outputInitialState()
{
    // output the input parameters, targets, and measurements
    osOutput_ << "measurementCount: " << measurementCount_ << endl;
    osOutput_ << "errorTrue: " << errorTrue_ * 1e6 << " ppm\n";
    osOutput_ << "errorEstimate: " << errorEstimate_ * 1e6 << " ppm\n";
    osOutput_ << "iterationCount: " << iterationCount_ << endl;
    osOutput_ << "targets: ";
    copy(targets_.begin(), targets_.end(), ostream_iterator<double>(osOutput_, " "));
    osOutput_ << "\nmeasurements: ";
    copy(measurements_.begin(), measurements_.end(), ostream_iterator<double>(osOutput_, " "));
    osOutput_ << endl << endl;

    // write output files with the targets and measurements
    printMasses(outputDirectory_ + "/simerr.targets.txt", iterationCount_, targets_, 1);
    printMasses(outputDirectory_ + "/simerr.measurements.txt", iterationCount_, measurements_, 1.5);
}


void Main::outputIterationState(const ErrorEstimator* estimator, int iteration)
{
    osOutput_ << "Iteration #" << iteration << endl;

    for (int i=0; i<estimator->measurementCount(); i++)
    {
        const MassSpread* massSpread = estimator->massSpread(i);
        osOutput_ << "  measurement: " << massSpread->measurement() << " ";
        osOutput_ << "error: " << massSpread->error() * 1e12 << endl;
        osOutput_ << "  distribution: ";
        for (int j=0; j<(int)massSpread->distribution().size(); j++)
        {
            const MassSpread::Pair& p = massSpread->distribution().at(j);
            osOutput_ << "(" << p.mass << ", " << p.probability << ") ";
            printTriple(osDistribution_, iteration, p.mass, p.probability);
        }
        osOutput_ << endl;
    }

    osOutput_ << "ErrorEstimator.error(): " << estimator->error() * 1e6 << endl << endl;
}


void Main::run()
{
    Random::initialize();
    initializeTargets();
    initializeMeasurements();
    outputInitialState();

    auto_ptr<ErrorEstimator> estimator(ErrorEstimator::create(massDatabase_,
                                                              measurements_,
                                                              errorEstimate_));
    for (int i=0; i<iterationCount_; i++)
    {
        estimator->iterate();
        outputIterationState(estimator.get(), i);
    }
}


void Main::runTest()
{
    int test_targets[10] = {101, 815, 153, 795, 855, 328, 214, 922, 766, 284};

    double test_measurements[10] = {100.912, 815.723, 153.091, 794.121, 855.642,
                                    328.822, 214.023, 922.789, 765.499, 284.07};


    // ignore anything that came into the constructor
    auto_ptr<MassDatabase> massDatabase(MassDatabase::createIntegerTestDatabase());
    measurementCount_ = 10;
    errorTrue_ = .001;
    errorEstimate_ = .001;
    iterationCount_ = 10;

    copy(test_targets, test_targets+measurementCount_, back_inserter(targets_));
    copy(test_measurements, test_measurements+measurementCount_, back_inserter(measurements_));

    outputInitialState();

    auto_ptr<ErrorEstimator> estimator(ErrorEstimator::create(*massDatabase,
                                                              measurements_,
                                                              errorEstimate_));
    for (int i=0; i<iterationCount_; i++)
    {
        estimator->iterate();
        outputIterationState(estimator.get(), i);
    }
}


void Main::runTest2()
{
    int test_targets[10] = {101, 815, 153, 795, 855, 328, 214, 922, 766, 284};

    double test_measurements[10] = {100.912, 815.723, 153.091, 794.121, 855.642,
                                    328.822, 214.023, 922.789, 765.499, 284.07};

    // create a PeptideDatabase with integers, to be used for our MassDatabase
    string filename = outputDirectory_ + "/temp.pdb";
    auto_ptr<PeptideDatabase> pdb = PeptideDatabase::create();
    for (int i=100; i<=2200; i++)
    {
        PeptideDatabaseRecord record;
        record.mass = i;
        pdb->append(record);
    }
    pdb->write(filename);

    auto_ptr<MassDatabase> massDatabase = MassDatabase::createFromPeptideDatabase(filename);

    // ignore anything that came into the constructor
    measurementCount_ = 10;
    errorTrue_ = .001;
    errorEstimate_ = .001;
    iterationCount_ = 10;

    copy(test_targets, test_targets+measurementCount_, back_inserter(targets_));
    copy(test_measurements, test_measurements+measurementCount_, back_inserter(measurements_));

    outputInitialState();

    auto_ptr<ErrorEstimator> estimator(ErrorEstimator::create(*massDatabase,
                                                              measurements_,
                                                              errorEstimate_));
    for (int i=0; i<iterationCount_; i++)
    {
        estimator->iterate();
        outputIterationState(estimator.get(), i);
    }

    system(("rm " + filename).c_str());
}


void test(const string& outputDirectory)
{
    if (Path::mkdir(outputDirectory))
        cout << "Created directory " << outputDirectory << endl;

    auto_ptr<MassDatabase> massDatabase(MassDatabase::createIntegerTestDatabase());

    Main main(*massDatabase, 0, 0, 0, 0, outputDirectory + separator_ + "1");
    main.runTest();

    Main main2(*massDatabase, 0, 0, 0, 0, outputDirectory + separator_ + "2");
    main2.runTest2();
}


int main(int argc, char* argv[])
{
    try
    {
        cout << "simerr (ErrorEstimator Simulation)\n";

        if (argc==3 && !strcmp(argv[1], "test"))
        {
            test(argv[2]);
            return 0;
        }

        if (argc < 4)
        {
            cout << "Usage:  simerr measurementCount errorTrue errorEstimate [option]*\n";
            cout << "        simerr test outputDirectory (regression testing)\n";
            cout << endl; 
            cout << "Parameters:\n";
            cout << "  measurementCount:     # of measurements to be taken from the mass database\n";
            cout << "  errorTrue:            true error (sd in ppm) of the measurements\n";
            cout << "  errorEstimate:        initial estimate provided to the ErrorEstimator\n";
            cout << "  db=filename:          filename of mass database (optional)\n";
            cout << "  output=directoryName: set output directory (default=='output')\n";
            cout << "If no mass database is specified, an integer test set will be used.\n";
            return 0;
        }

        int measurementCount = atoi(argv[1]);
        double errorTrue = atoi(argv[2]) * 1e-6; // convert from ppm
        double errorEstimate = atoi(argv[3]) * 1e-6; // convert from ppm
        int iterationCount = 10;
        string outputDirectory = "output";
        string databaseFilename;

        for (int i=4; i<argc; i++)
        {
            string option = argv[i];
            if (option.find("output=")==0 && option.size()>7)
            {
                outputDirectory = option.substr(7);
            }
            else if (option.find("db=")==0 && option.size()>3)
            {
                databaseFilename = option.substr(3);
            }
        }

        auto_ptr<MassDatabase> massDatabase;

        if (!databaseFilename.empty())
        {
            cout << "Reading mass database from file " << databaseFilename << endl;
            massDatabase = MassDatabase::createFromTextFile(databaseFilename);
        }
        else
        {
            massDatabase = MassDatabase::createIntegerTestDatabase();
            cout << "Using integer test database.\n";
        }

        Main main(*massDatabase,
                  measurementCount,
                  errorTrue,
                  errorEstimate,
                  iterationCount,
                  outputDirectory);

        main.run();
        return 0;
    }
    catch (std::exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}


