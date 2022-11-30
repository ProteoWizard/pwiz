//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
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


#ifndef _ROUND_HPP_
#define _ROUND_HPP_


#include <cmath>
#include <stdexcept>


#ifdef _MSC_VER // msvc hack
inline double round(double d) {return floor(d + 0.5);}
#endif // _MSC_VER
namespace pwiz {
namespace math {

// From https://www.codeproject.com/Articles/114/Floating-point-utilites
// Rounds a number to a specified number of digits.
// Number is the number you want to round.
// Num_digits specifies the number of digits to which you want to round number.
// If num_digits is greater than 0, then number is rounded to the specified number of decimal places.
// If num_digits is 0, then number is rounded to the nearest integer.
// Examples
//		ROUND(2.15, 1)		equals 2.2
//		ROUND(2.149, 1)		equals 2.1
//		ROUND(-1.475, 2)	equals -1.48
template<typename floating_type>
floating_type roundto(floating_type number, int num_digits)
{
    floating_type doComplete5i, doComplete5(number * pow(10.0, (floating_type)(num_digits + 1)));

    if (number < 0.0)
        doComplete5 -= 5.0;
    else
        doComplete5 += 5.0;

    doComplete5 /= 10.0;
    modf(doComplete5, &doComplete5i);

    return doComplete5i / pow(10.0, (floating_type)num_digits);
}

// From https://www.codeproject.com/Articles/114/Floating-point-utilites
// Rounds X to SigFigs significant figures.
// Examples
//		SigFig(1.23456, 2)		equals 1.2
//		SigFig(1.23456e-10, 2)	equals 1.2e-10
//		SigFig(1.23456, 5)		equals 1.2346
//		SigFig(1.23456e-10, 5)	equals 1.2346e-10
//		SigFig(0.000123456, 2)	equals 0.00012
template<typename floating_type>
floating_type sigfig(floating_type X, int SigFigs)
{
    if (SigFigs < 1)
        throw std::out_of_range("SigFigs must be greater than 0");

    // log10f(0) returns NaN
    if (X == 0.0)
        return X;

    int Sign;
    if (X < 0.0)
        Sign = -1;
    else
        Sign = 1;

    X = fabs(X);
    float Powers = pow(10.0f, floor(log10(X)) + 1.0);

    return Sign * roundto(X / Powers, SigFigs) * Powers;
}

} // namespace math
} // namespace pwiz

#endif // _ROUND_HPP_

