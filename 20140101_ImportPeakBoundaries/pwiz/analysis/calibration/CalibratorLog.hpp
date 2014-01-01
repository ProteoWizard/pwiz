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


#ifndef _CALIBRATORLOG_H_
#define _CALIBRATORLOG_H_


#include "Calibrator.hpp"
#include <memory>
#include <string>
#include <vector>


namespace pwiz {
namespace calibration {


class CalibratorLog
{
    public:

    static std::auto_ptr<CalibratorLog> create(const Calibrator* calibrator,
                                               const std::string& outputDirectory,
                                               const std::vector<double>* trueMasses = 0,
                                               const CalibrationParameters* trueParameters = 0);
    virtual void outputState() = 0;

    virtual ~CalibratorLog(){}
};


} // namespace calibration
} // namespace pwiz


#endif // _CALIBRATORLOG_H_
