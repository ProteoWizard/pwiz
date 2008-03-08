//
// CalibrationParameters.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _CALIBRATIONPARAMETERS_HPP_
#define _CALIBRATIONPARAMETERS_HPP_


#include <cmath>
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace data {


const double thermoA_ = 1.075e8;
const double thermoB_ = -3.455e8; 


#pragma pack(1)
struct CalibrationParameters
{
    double A;
    double B;

    CalibrationParameters(double a=0, double b=0);
    double mz(double frequency) const;
    double frequency(double mz) const;
    bool operator==(const CalibrationParameters& that) const;
    bool operator!=(const CalibrationParameters& that) const;

    static CalibrationParameters thermo();
};
#pragma pack()


inline std::ostream& operator<<(std::ostream& os, const CalibrationParameters& p)
{
    os << "(" << p.A << "," << p.B << ")";
    return os;
}


inline CalibrationParameters::CalibrationParameters(double a, double b)
: A(a), B(b)
{}


inline double CalibrationParameters::mz(double frequency) const
{
    if (frequency == 0) throw std::runtime_error("[CalibrationParameters::mz()] Division by zero.\n");
    return A/frequency + B/(frequency*frequency);
}


inline double CalibrationParameters::frequency(double mz) const
{
    if (mz == 0) throw std::runtime_error("[CalibrationParameters::frequency()] Division by zero.\n");
    return (A+sqrt(A*A + 4*B*mz))/(2*mz);
}


inline bool CalibrationParameters::operator==(const CalibrationParameters& that) const 
{
    return A==that.A && B==that.B;
} 


inline bool CalibrationParameters::operator!=(const CalibrationParameters& that) const 
{
    return !operator==(that); 
} 
    

inline CalibrationParameters CalibrationParameters::thermo()
{
    return CalibrationParameters(thermoA_, thermoB_);
}


} // namespace data 
} // namespace pwiz


#endif // _CALIBRATIONPARAMETERS_HPP_

