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


#ifndef _ERRORESTIMATOR_HPP_
#define _ERRORESTIMATOR_HPP_


#include <memory>
#include <vector>


namespace pwiz {
namespace calibration {


class MassDatabase;
class MassSpread;


class ErrorEstimator
{
    public:

    static std::auto_ptr<ErrorEstimator> create(const MassDatabase& massDatabase,
                                                const std::vector<double>& massMeasurements,
                                                double initialErrorEstimate,
                                                const char* outputFilename = 0);
    virtual void iterate() = 0;

    virtual int measurementCount() const = 0;
    virtual const MassSpread* massSpread(int index) const = 0;
    virtual double error() const = 0;

    virtual void output(std::ostream& os) const = 0;

    virtual ~ErrorEstimator(){}
};


} // namespace calibration
} // namespace pwiz


#endif //_ERRORESTIMATOR_HPP_

