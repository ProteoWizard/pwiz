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


#ifndef _LEASTSQUARESCALIBRATOR_HPP_
#define _LEASTSQUARESCALIBRATOR_HPP_ 


#include "pwiz/data/misc/CalibrationParameters.hpp"
#include <memory>
#include <vector>


namespace pwiz {
namespace calibration {


class LeastSquaresCalibrator
{
    public:

    static std::auto_ptr<LeastSquaresCalibrator> create(const std::vector<double>& trueMasses,
                                                        const std::vector<double>& observedFrequencies);
    virtual void calibrate() = 0;
    virtual const data::CalibrationParameters& parameters() const = 0;
    virtual double error() const = 0; // rms relative deviation 

    virtual ~LeastSquaresCalibrator(){}
};


} // namespace calibration
} // namespace pwiz


#endif // _LEASTSQUARESCALIBRATOR_HPP_ 

