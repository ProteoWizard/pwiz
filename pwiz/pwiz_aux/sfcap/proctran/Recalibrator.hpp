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


#ifndef _RECALIBRATOR_HPP_
#define _RECALIBRATOR_HPP_


#include "pwiz/data/misc/PeakData.hpp"


namespace pwiz {
namespace pdanalysis {


class Recalibrator
{
    public:

    typedef pwiz::data::peakdata::Scan Scan;
    typedef pwiz::data::CalibrationParameters CalibrationParameters;

    /// recalulate each Peak::mz and PeakFamily::mzMonoisotopic in the Scan
    void recalibrate(Scan& scan) const;

    virtual ~Recalibrator(){}

    protected:

    /// implementation-specific calculation of calibration parameters,
    /// to be used during the recalibration 
    virtual CalibrationParameters calculateCalibrationParameters(const Scan& scan) const = 0;
};


} // namespace pdanalysis 
} // namespace pwiz


#endif // _RECALIBRATOR_HPP_ 

