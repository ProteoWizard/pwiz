//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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
                                                                                                     
#ifndef _MZTOLERANCE_HPP_
#define _MZTOLERANCE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <iosfwd>


namespace pwiz {
namespace chemistry {


///
/// struct for expressing m/z tolerance in either amu or ppm
///
struct PWIZ_API_DECL MZTolerance
{
    enum Units {MZ, PPM};
    double value;
    Units units;

    MZTolerance(double _value = 0, Units _units = MZ)
    :   value(_value), units(_units)
    {}
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MZTolerance& mzt);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, MZTolerance& mzt);
PWIZ_API_DECL bool operator==(const MZTolerance& a, const MZTolerance& b);
PWIZ_API_DECL bool operator!=(const MZTolerance& a, const MZTolerance& b);


PWIZ_API_DECL double& operator+=(double& d, const MZTolerance& tolerance);
PWIZ_API_DECL double& operator-=(double& d, const MZTolerance& tolerance);
PWIZ_API_DECL double operator+(double d, const MZTolerance& tolerance);
PWIZ_API_DECL double operator-(double d, const MZTolerance& tolerance);


/// returns true iff a is in (b-tolerance, b+tolerance)
PWIZ_API_DECL bool isWithinTolerance(double a, double b, const MZTolerance& tolerance);
/// returns true iff b - a is greater than the value in tolerance (useful for matching sorted mass lists)
PWIZ_API_DECL bool lessThanTolerance(double a, double b, const MZTolerance& tolerance);


} // namespace chemistry
} // namespace pwiz


#endif // _MZTOLERANCE_HPP_

