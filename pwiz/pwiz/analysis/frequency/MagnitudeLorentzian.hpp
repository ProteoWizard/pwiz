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


#ifndef _MAGNITUDELORENTZIAN_HPP_
#define _MAGNITUDELORENTZIAN_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <vector>


namespace pwiz {
namespace frequency {


class PWIZ_API_DECL MagnitudeLorentzian
{
    public:

    // m(x) == 1/sqrt(ax^2 + bx + c)
    //      == alpha/sqrt(1/tau^2 + [2pi(x-center)]^2)

    MagnitudeLorentzian(double a, double b, double c);
    MagnitudeLorentzian(std::vector<double> a);
    MagnitudeLorentzian(const std::vector< std::pair<double,double> >& samples);

    double leastSquaresError() const;

    std::vector<double>& coefficients();
    const std::vector<double>& coefficients() const;

    double operator()(double x) const;
    double center() const;
    double tau() const;
    double alpha() const;

    private:
    std::vector<double> a_;
    double leastSquaresError_;
};


} // namespace frequency
} // namespace pwiz


#endif // _MAGNITUDELORENTZIAN_HPP_
