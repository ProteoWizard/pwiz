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


#ifndef _PARABOLA_HPP_
#define _PARABOLA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace math {


class PWIZ_API_DECL Parabola
{
    public:

    // construct by giving 3 coefficients
    Parabola(double a=0, double b=0, double c=0);
    Parabola(std::vector<double> a);

    // construct by giving 3 or more sample points
    Parabola(const std::vector< std::pair<double,double> >& samples);

    // construct by weighted least squares
    Parabola(const std::vector< std::pair<double,double> >& samples,
             const std::vector<double>& weights);

    std::vector<double>& coefficients() {return a_;}
    const std::vector<double>& coefficients() const {return a_;}

    double operator()(double x) const {return a_[0]*x*x + a_[1]*x + a_[2];}
    double center() const {return -a_[1]/(2*a_[0]);}

    private:
    std::vector<double> a_;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Parabola& p);


} // namespace math 
} // namespace pwiz


#endif // _PARABOLA_HPP_

