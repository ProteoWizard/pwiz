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


#include "peaks/TruncatedLorentzianParameters.hpp"
#include "peaks/TruncatedLorentzianEstimator.hpp"
#include "data/CalibrationParameters.hpp"
#include "calibration/LeastSquaresCalibrator.hpp"
#include <iostream>
#include <fstream>
#include <vector>
#include <map>
#include <iomanip>


using namespace std;
using namespace pwiz::calibration;
using namespace pwiz::data;
using namespace pwiz::peaks;


double masses_[] = {

    // Angiotensin II
    1046.5418,
    523.7745,
    349.5188,
    262.3909,

    // Bombessin
    1619.8223,
    810.4148,
    540.6123,
    405.7110,

    // Substance P
    1347.7354,
    674.3713,
    449.9167,

    // Neurotensin
    1672.9170,
    836.9621,
    558.3105,
    418.9847,
    335.3892,

    // Alpha1-6
    882.4621,
    441.7347,
    294.8255
};


const int massCount_ = sizeof(masses_)/sizeof(double);


struct TLPInfo
{
    string filename;
    TruncatedLorentzianParameters tlp;
};


typedef map<double, TLPInfo> MassMap;


MassMap massMap_;
CalibrationParameters p_;


const char* basename(const string& fullpath)
{
    string::size_type lastSlash = fullpath.find_last_of("/\\");
    return lastSlash==string::npos ? fullpath.c_str() : fullpath.c_str()+lastSlash+1;
}


string windowName(const string& filename)
{
    // replace .final.tlp with .cfd
    string::size_type index = filename.find(".final.tlp");
    if (index == string::npos) throw runtime_error("windowName(): shouldn't happen");
    string result = filename.substr(0, index);
    result += ".cfd";
    return result;
}


void handleMatch(double mz, const TruncatedLorentzianParameters& tlp, const string& filename)
{
    MassMap::iterator it = massMap_.find(mz);

    if (it == massMap_.end())
    {
        // new match -- insert into map 
        TLPInfo info;
        info.filename = filename;
        info.tlp = tlp;
        massMap_[mz] = info;
    }
    else if (massMap_[mz].tlp != tlp)
    {
        // we have a multiple match, with different model parameters -- throw a warning for now 
        cerr << "Warning: multiple incompatible matches for mass " << mz << 
            " (" << massMap_[mz].filename << " / " << filename << ")\n"; 
    }
}


void processFile(const string& filename)
{
    TruncatedLorentzianParameters tlp(filename);
    double mass = p_.mz(tlp.f0);
    
    for (double* it=masses_; it!=masses_+massCount_; ++it)
    {
        if (abs(mass-*it)<.01)
        {
            handleMatch(*it, tlp, filename);
            break;
        }
    }
}


void analyze()
{
    // least squares calibration

    vector<double> trueMasses; 
    vector<double> observedFrequencies;
    for (MassMap::iterator it=massMap_.begin(); it!=massMap_.end(); ++it)
    {
        trueMasses.push_back(it->first);
        observedFrequencies.push_back(it->second.tlp.f0);
    }

    auto_ptr<LeastSquaresCalibrator> calibrator = 
        LeastSquaresCalibrator::create(trueMasses, observedFrequencies);

    calibrator->calibrate();

    // report
    
    const CalibrationParameters& p = calibrator->parameters();

    cout << "#\n"; 
    cout << "# parameters: " << p << endl;
    cout << "# error: " << calibrator->error()*1e6 << " ppm\n";
    cout << "#\n"; 
    cout << "# mass_true mass_calc ppm_error filename f0 T tau alpha amplitude phase normalizedError sumSquaresModel\n";
    cout << "#\n"; 
   
    for (MassMap::iterator it=massMap_.begin(); it!=massMap_.end(); ++it)
    {
        const double& m_true = it->first;
        const TruncatedLorentzianParameters& tlp = it->second.tlp;
        double m_calc = p.mz(tlp.f0); 
        double error = (m_calc-m_true)/m_true;

        string cfdFilename = windowName(it->second.filename); 
        const FrequencyData fd(cfdFilename);
        auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create();

        double N = fd.data().size();
        double noiseVariance = fd.noiseFloor() * fd.noiseFloor();
        double normalizedError = estimator->normalizedError(fd, tlp);
        double sumSquaresModel = estimator->sumSquaresModel(fd, tlp);

        cout << fixed << setprecision(5) << 
            setw(10) << m_true << " " << 
            setw(10) << m_calc << " " <<
            setw(8) << error*1e6 << " " <<
            it->second.filename << " " <<
            setw(12) << tlp.f0 << " " <<
            tlp.T << " " <<
            tlp.tau << " " <<
            tlp.alpha << " " <<
            abs(tlp.alpha) << " " <<
            arg(tlp.alpha) << " " << 
            normalizedError << " " <<
            sumSquaresModel << " " << 
            endl;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        cout.precision(10);

        if (argc < 3)
        {
            cout << "Usage: tlpmatch tlplist.txt data.cfd\n";
            cout << "Parameters:\n";
            cout << "  tlplist.txt:  text file with list of .tlp files to process\n";
            cout << "  data.cfd:  used to obtain calibration parameters\n";
            return 1; 
        }

        const string& tlplist = argv[1];
        const string& cfdFilename = argv[2];

        // get list of tlp files
        vector<string> filenames;
        ifstream is(tlplist.c_str());
        if (!is) throw runtime_error(("Unable to open file " + tlplist).c_str());
        copy(istream_iterator<string>(is), istream_iterator<string>(), back_inserter(filenames));

        // grab calibration parameters from datafile
        FrequencyData fd(cfdFilename);
        p_ = CalibrationParameters(fd.calibration().A, fd.calibration().B);

        // process the files
        for_each(filenames.begin(), filenames.end(), processFile);

        // analyze data
        analyze();

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

