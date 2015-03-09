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


#ifndef _CALIBRATOR_HPP_
#define _CALIBRATOR_HPP_


#include "pwiz/data/misc/CalibrationParameters.hpp"
#include <memory>
#include <vector>
#include <cmath>


namespace pwiz {
namespace calibration {


class MassDatabase;
class MassSpread;


/// Calibrates using EM algorithm using peptide mass database.
class Calibrator
{
    public:

    /// Structure for holding frequency-charge pairs.
    struct Measurement
    {
        double frequency;
        int charge;
        Measurement(double f=0, int c=1) : frequency(f), charge(c) {}
        bool operator==(const Measurement& that) const {return frequency==that.frequency && charge==that.charge;}
    };

    /// Create an instance;
    /// Log output can be suppressed by setting outputDirectory=="" 
    static std::auto_ptr<Calibrator> create(const MassDatabase& massDatabase,
                                            const std::vector<Measurement>& measurements,
                                            const data::CalibrationParameters& initialParameters,
                                            double initialErrorEstimate,
                                            int errorEstimatorIterationCount,
                                            const std::string& outputDirectory);
    /// Perform a single iteration.
    virtual void iterate() = 0;

    /// Return total number of iterations that have been performed.
    virtual int iterationCount() const = 0;

    /// Return current estimate of calibration parameters.
    virtual const data::CalibrationParameters& parameters() const = 0;

    /// Return number of measurements.
    virtual int measurementCount() const = 0;

    /// Return requested measurement.
    virtual const Measurement* measurement(int index) const = 0;

    /// Return mass spread associated with the measurement.
    virtual const MassSpread* massSpread(int index) const = 0;

    /// Return current error measurement.
    virtual double error() const = 0;

    virtual ~Calibrator(){}
};
        

} // namespace calibration
} // namespace pwiz


#endif // _CALIBRATOR_HPP_

