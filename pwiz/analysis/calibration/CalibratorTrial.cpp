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


#include "CalibratorTrial.hpp"
#include "MassSpread.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <iostream>
#include <stdexcept>
#include <fstream>
#include <sstream>
#include <iomanip>


using namespace std;


namespace pwiz {
namespace calibration {


CalibratorTrial::Configuration::Configuration()
:   massDatabase(0),
    calibratorIterationCount(0),
    errorEstimatorIterationCount(0),
    measurementError(0),
    initialErrorEstimate(0)
{}


void CalibratorTrial::Configuration::readTrialData(const string& filename)
{
    ifstream is(filename.c_str());
    if (!is) 
        throw runtime_error("[CalibratorTrial::Configuration::readTrialData] Unable to open file " + filename);

    double measurementCount = 0;

    while (is)
    {
        string buffer;
        getline(is, buffer);
        if (!is) break;

        istringstream iss(buffer);
        string label;
        iss >> label;

        if (label == "parametersTrue")
            iss >> parametersTrue.A >> parametersTrue.B;
        else if (label == "parametersInitialEstimate")
            iss >> parametersInitialEstimate.A >> parametersInitialEstimate.B;
        else if (label == "measurementError")
            iss >> measurementError;
        else if (label == "initialErrorEstimate")
            iss >> initialErrorEstimate;
        else if (label == "measurementCount")
            iss >> measurementCount;
        else if (label == "measurement")
        {
            double mass=0, frequency=0;
            int charge=0;
            iss >> mass >> frequency >> charge;
            trueMasses.push_back(mass);
            measurements.push_back(Calibrator::Measurement(frequency, charge));
        }
    }

    if (measurementCount != measurements.size())
        throw runtime_error("[CalibratorTrial::Configuration::readTrialData()] Bad measurementCount."); 
}


void CalibratorTrial::Configuration::writeTrialData(const string& filename) const
{
    ofstream os(filename.c_str());
    if (!os) 
        throw runtime_error("[CalibratorTrial::Configuration::writeTrialData] Unable to open file " + filename);

    if (trueMasses.size() != measurements.size())
        throw runtime_error("[CalibratorTrial::Configuration::writeTrialData] Mass count != measurement count."); 

    double errorA = (parametersInitialEstimate.A - parametersTrue.A)/parametersTrue.A;
    double errorB = (parametersInitialEstimate.B - parametersTrue.B)/parametersTrue.B;

    os.precision(20);
    os << "parametersTrue " << parametersTrue.A << " " << parametersTrue.B << endl;
    os << "parametersInitialEstimate " << parametersInitialEstimate.A << " " << parametersInitialEstimate.B << endl;
    os << "parametersError " << errorA << " " << errorB << endl; 
    os << "measurementError " << measurementError << endl;
    os << "initialErrorEstimate " << initialErrorEstimate << endl;
    os << "measurementCount " << measurements.size() << endl;
    for (unsigned int i=0; i<measurements.size(); i++)
        os << "measurement " << trueMasses[i] << " " << measurements[i].frequency << " " << measurements[i].charge << endl;
}


const char* StateHeader_ =
    "      A           B          error(true/est/cal)              id"; 


ostream& operator<<(ostream& os, const CalibratorTrial::State& state)
{
    os << scientific << setprecision(4) << setfill(' ') << 
        state.parameters.A << " " << state.parameters.B <<
        "  [ " << 
        fixed << showpoint << 
        setw(8) << state.errorTrue * 1e6 << " " << 
        setw(8) << state.errorEstimate * 1e6 << " " << 
        setw(8) << state.errorCalibration * 1e6 << 
        " ]  " << 
        setprecision(2) <<
        "( " << state.correct << " / " << state.confident << " )";

    return os;
}


struct MassCalculation
{
    int z;
    double f_obs;
    double mz_obs;
    double m_obs;
    double f_true;
    double mz_true;
    double m_true;
    double error_mass;
    double error_calibration;
    bool correct;
    bool confident;

    MassCalculation(const data::CalibrationParameters& parameters,
                    const Calibrator::Measurement* measurement, 
                    const MassSpread* massSpread,
                    const data::CalibrationParameters& trueParameters,
                    double trueMass);
};


MassCalculation::MassCalculation(const data::CalibrationParameters& parameters,
                                 const Calibrator::Measurement* measurement, 
                                 const MassSpread* massSpread,
                                 const data::CalibrationParameters& trueParameters,
                                 double trueMass)
:   z(1),
    f_obs(0), mz_obs(0), m_obs(0),    
    f_true(0), mz_true(0), m_true(trueMass),
    error_mass(0),
    error_calibration(0),
    correct(false),
    confident(false)
{
    using namespace proteome; // for Ion

    if (!measurement)
        throw runtime_error("[CalibratorTrialImpl::MassCalculation::MassCalculation()] Null measurement.");

    z = measurement->charge;

    f_obs = measurement->frequency;
    mz_obs = parameters.mz(f_obs);
    m_obs = Ion::neutralMass(mz_obs, z); 

    mz_true = Ion::mz(m_true, z);
    f_true = trueParameters.frequency(mz_true);

    error_mass = (m_obs - m_true)/m_true;

    double term1 = (parameters.A - trueParameters.A)/f_true;
    double term2 = (parameters.B - trueParameters.B)/(f_true*f_true);
    error_calibration = (term1 + term2)/mz_true;
    
    if (massSpread && !massSpread->distribution().empty())
    {
        const MassSpread::Pair& id = massSpread->distribution()[0];
        const double epsilon = 1e-8;
        const double confidenceThreshold = .95;
        if (fabs(id.mass-m_true) < epsilon)
        {
            correct = true;
            if (id.probability > confidenceThreshold)
                confident = true;
        }
    }
}


const char* MassCalculationHeader_ =
    "  m_true   z       f        m_calc     error  id distribution";


ostream& operator<<(ostream& os, const MassCalculation& mc)
{
    os << fixed << setprecision(4) << setfill(' ') << 
        setw(10) << mc.m_true << " " << 
        setw(1) << mc.z << " " <<  
        setw(11) << mc.f_obs << " " <<
        setw(10) << mc.m_obs << " " <<
        setw(9) << mc.error_mass * 1e6 << " " << 
        (mc.correct ? "*" : " ") <<
        (mc.confident ? "!" : " "); 
         
    return os;
}


class CalibratorTrialImpl : public CalibratorTrial
{
    public:
    CalibratorTrialImpl(const Configuration& configuration);
    virtual void run();
    virtual const Configuration& configuration() const {return config_;}
    virtual const Results& results() const {return results_;}
    
    private:
    Configuration config_;
    Results results_;
    auto_ptr<Calibrator> calibrator_;

    vector<MassCalculation> massCalculations_;
    void updateMassCalculations();
    
    State state_;
    void updateState();

    // logging
    ofstream osLog_;
    ofstream osSummary_;
    void initializeLogs();
    void updateLogs();
    void finalizeLogs();
};


auto_ptr<CalibratorTrial> CalibratorTrial::create(const Configuration& configuration)
{
    return auto_ptr<CalibratorTrial>(new CalibratorTrialImpl(configuration));
}


CalibratorTrialImpl::CalibratorTrialImpl(const Configuration& configuration)
:   config_(configuration)
{
    if (!config_.massDatabase)
        throw runtime_error("[CalibratorTrialImpl::CalibratorTrialImpl] Null MassDatabase*");

    if (config_.measurements.size() != config_.trueMasses.size())
        throw runtime_error("[CalibratorTrialImpl::CalibratorTrialImpl()] Measurement size mismatch.");

    calibrator_ = Calibrator::create(*config_.massDatabase,
                                     config_.measurements,
                                     config_.parametersInitialEstimate,
                                     config_.initialErrorEstimate,
                                     config_.errorEstimatorIterationCount,
                                     config_.outputDirectory);

    updateState();
    results_.initial = state_;
   
    initializeLogs();
    updateLogs();
}


void CalibratorTrialImpl::run()
{
    for (int i=0; i<config_.calibratorIterationCount; i++)
    {
        calibrator_->iterate();
        updateState();
        updateLogs();
    }

    results_.final = state_;
    finalizeLogs();
}


void CalibratorTrialImpl::updateMassCalculations()
{
    massCalculations_.clear();
    
    for (int i=0; i<calibrator_->measurementCount(); i++)
    {
        massCalculations_.push_back(MassCalculation(calibrator_->parameters(),
                                                    calibrator_->measurement(i),
                                                    calibrator_->massSpread(i), 
                                                    config_.parametersTrue,
                                                    config_.trueMasses[i]));
    }
}


void CalibratorTrialImpl::updateState()
{
    updateMassCalculations();

    double totalSquaredMassError = 0;
    double totalSquaredCalibrationError = 0;
    int totalCorrect = 0;
    int totalConfident = 0;

    for (vector<MassCalculation>::iterator it=massCalculations_.begin(); it!=massCalculations_.end(); ++it)
    {
        totalSquaredMassError += it->error_mass * it->error_mass; 
        totalSquaredCalibrationError += it->error_calibration * it->error_calibration;
        totalCorrect += it->correct ? 1 : 0;
        totalConfident += it->confident ? 1 : 0;
    }  
   
    int N = massCalculations_.size();
    if (N <= 0)
        throw runtime_error("[CalibratorTrialImpl::updateState()] Zero calculations.");

    state_.parameters = calibrator_->parameters();
    state_.errorTrue = sqrt(totalSquaredMassError / N);
    state_.errorEstimate = calibrator_->error();
    state_.errorCalibration = sqrt(totalSquaredCalibrationError / N); 
    state_.correct = (double)totalCorrect / N;
    state_.confident = (double)totalConfident / N;
}


void CalibratorTrialImpl::initializeLogs()
{
    system(("mkdir " + config_.outputDirectory + " 2> /dev/null").c_str());

    string filenameLog = config_.outputDirectory + "/ct.log.txt";
    osLog_.open(filenameLog.c_str());
    if (!osLog_) throw runtime_error(("[CalibratorTrial] Unable to open file: " + filenameLog).c_str());
    osLog_ << fixed << setprecision(7);
    
    string filenameSummary = config_.outputDirectory + "/ct.summary.txt";
    osSummary_.open(filenameSummary.c_str());
    if (!osSummary_) throw runtime_error(("[CalibratorTrial] Unable to open file: " + filenameSummary).c_str());
    osSummary_ << fixed << setprecision(7);
    osSummary_ << "#:  " << StateHeader_ << endl; 
}


void CalibratorTrialImpl::updateLogs()
{
    osSummary_ << setw(2) << setfill('0') << calibrator_->iterationCount() << ": " << state_ << endl;
    
    osLog_ << "#:  " << StateHeader_ << endl;
    osLog_ << setw(2) << setfill('0') << calibrator_->iterationCount() << ": " << state_ << endl << endl;
    osLog_ << MassCalculationHeader_ << endl;

    for (unsigned int i=0; i<massCalculations_.size(); i++)
    {
        osLog_ << massCalculations_[i] << " ";
        if (calibrator_->massSpread(i))
            calibrator_->massSpread(i)->output(osLog_);
        osLog_ << endl;
    }
    osLog_ << endl;
}


void CalibratorTrialImpl::finalizeLogs()
{
    osLog_ << "       " << StateHeader_ << endl;
    osLog_ << "final: " << state_ << endl;
}


} // namespace calibration
} // namespace pwiz

