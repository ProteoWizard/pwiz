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


#ifndef _CALIBRATIONPARAMETERS_HPP_
#define _CALIBRATIONPARAMETERS_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <cmath>
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace data {


const double thermoA_FT_ = 1.075e8;
const double thermoB_FT_ = -3.455e8; 
const double thermoA_Orbitrap_ = 4.753e10;
const double thermoB_Orbitrap_ = 0;


#pragma pack(1)
struct CalibrationParameters
{
    double A;
    double B;

    enum InstrumentModel {FT, Orbitrap};
    InstrumentModel instrumentModel;

    CalibrationParameters(double a=0, double b=0, InstrumentModel im=FT);
    double mz(double frequency) const;
    double frequency(double mz) const;
    bool operator==(const CalibrationParameters& that) const;
    bool operator!=(const CalibrationParameters& that) const;

    static CalibrationParameters thermo_FT();
    static CalibrationParameters thermo_Orbitrap();
};
#pragma pack()


inline std::ostream& operator<<(std::ostream& os, const CalibrationParameters& p)
{
    os << "(" << p.A << "," << p.B << ")";
    return os;
}


inline CalibrationParameters::CalibrationParameters(double a, double b, InstrumentModel im)
: A(a), B(b), instrumentModel(im)
{}


inline double CalibrationParameters::mz(double frequency) const
{
    if (frequency == 0) throw std::runtime_error("[CalibrationParameters::mz()] Division by zero.\n");
    return (instrumentModel==Orbitrap) ?
        A/(frequency*frequency) : 
        A/frequency + B/(frequency*frequency);
}


inline double CalibrationParameters::frequency(double mz) const
{
    if (mz == 0) throw std::runtime_error("[CalibrationParameters::frequency()] Division by zero.\n");
    return (instrumentModel==Orbitrap) ?
        sqrt(A/mz) :
        (A+sqrt(A*A + 4*B*mz))/(2*mz);
}


inline bool CalibrationParameters::operator==(const CalibrationParameters& that) const 
{
    return A==that.A && B==that.B && instrumentModel==that.instrumentModel;
} 


inline bool CalibrationParameters::operator!=(const CalibrationParameters& that) const 
{
    return !operator==(that); 
} 
    

inline CalibrationParameters CalibrationParameters::thermo_FT()
{
    return CalibrationParameters(thermoA_FT_, thermoB_FT_, FT);
}


inline CalibrationParameters CalibrationParameters::thermo_Orbitrap()
{
    return CalibrationParameters(thermoA_Orbitrap_, thermoB_Orbitrap_, Orbitrap);
}


} // namespace data 
} // namespace pwiz


#endif // _CALIBRATIONPARAMETERS_HPP_

