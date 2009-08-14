//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _PARAMETERESTIMATOR_HPP_
#define _PARAMETERESTIMATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ParametrizedFunction.hpp"
#include <memory>
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace frequency {


class ParameterEstimator
{
    public:

    typedef ParametrizedFunction< std::complex<double> > Function;
    typedef data::SampleDatum<double, std::complex<double> > Datum;
    typedef std::vector<Datum> Data;
    typedef ublas::vector<double> Parameters;

    // instantiation
    static std::auto_ptr<ParameterEstimator> create(const Function& function,
                                                        const Data& data,
                                                        const Parameters& initialEstimate);
    virtual ~ParameterEstimator(){}

    // get/set current parameter estimate
    virtual const Parameters& estimate() const = 0;
    virtual void estimate(const Parameters& p) = 0;

    // return error, based on current parameter estimate
    virtual double error() const = 0;

    // update current parameters via Newton iteration, returns change in error, 
    // with optional output to log 
    virtual double iterate(std::ostream* log = 0) = 0;
};


} // namespace frequency
} // namespace pwiz


#endif // _PARAMETERESTIMATOR_HPP_

