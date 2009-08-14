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


#ifndef _CALIBRATORTRIAL_HPP_
#define _CALIBRATORTRIAL_HPP_


#include "Calibrator.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include <string>
#include <vector>
#include <memory>


namespace pwiz {
namespace calibration {


class CalibratorTrial
{
    public:

    struct Configuration
    {
        // Calibrator configuration
        const MassDatabase* massDatabase;
        int calibratorIterationCount;
        int errorEstimatorIterationCount;
        std::string outputDirectory; 
        
        // trial data 
        std::vector<double> trueMasses;
        std::vector<Calibrator::Measurement> measurements;
        data::CalibrationParameters parametersTrue;
        data::CalibrationParameters parametersInitialEstimate;
        double measurementError;
        double initialErrorEstimate;

        Configuration();
        void readTrialData(const std::string& filename);
        void writeTrialData(const std::string& filename) const;
    };

    struct State
    {
        data::CalibrationParameters parameters;
        double errorTrue;
        double errorEstimate;
        double errorCalibration;
        double correct;
        double confident;

        State()
        :   errorTrue(0), errorEstimate(0), errorCalibration(0), correct(0), confident(0)
        {}
    };

    struct Results
    {
        State initial;
        State final;
    };

    static std::auto_ptr<CalibratorTrial> create(const Configuration& configuration);
    virtual ~CalibratorTrial(){}

    virtual void run() = 0;
    virtual const Configuration& configuration() const = 0;
    virtual const Results& results() const = 0;
};


std::ostream& operator<<(std::ostream& os, const CalibratorTrial::State& state);


} // namespace calibration
} // namespace pwiz


#endif // _CALIBRATORTRIAL_HPP_

