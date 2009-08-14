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


#ifndef _RECALIBRATORSIMPLE_HPP_
#define _RECALIBRATORSIMPLE_HPP_


#include "Recalibrator.hpp"


namespace pwiz {
namespace pdanalysis {


/// simple Recalibrator implementation that is instantiated with the 
/// CalibrationParameters to be used during recalibration of the Scan
class RecalibratorSimple : public Recalibrator
{
    public:

    RecalibratorSimple(const CalibrationParameters& cp) : cp_(cp) {}

    protected:

    virtual CalibrationParameters calculateCalibrationParameters(const Scan& scan) const 
    {
        return cp_;
    }

    private:

    CalibrationParameters cp_;
};


} // namespace pdanalysis 
} // namespace pwiz


#endif // _RECALIBRATORSIMPLE_HPP_ 

