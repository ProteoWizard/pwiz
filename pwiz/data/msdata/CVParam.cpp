//
// CVParam.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#define PWIZ_SOURCE

#include "CVParam.hpp"
#include <iostream>


namespace pwiz {
namespace msdata {


using namespace std;


//
// CVParam
//


PWIZ_API_DECL CVParam::~CVParam() {}

PWIZ_API_DECL string CVParam::name() const
{
    return cvTermInfo(cvid).name;
}


PWIZ_API_DECL string CVParam::unitsName() const
{
    return cvTermInfo(units).name;
}


PWIZ_API_DECL double CVParam::timeInSeconds() const
{
    if (units == UO_second) 
        return valueAs<double>();
    else if (units == UO_minute)
        return valueAs<double>() * 60;
    else if (units == UO_hour)
        return valueAs<double>() * 3600;
    else if (units == MS_second_OBSOLETE) // mzML 1.0 support
        return valueAs<double>();
    else if (units == MS_minute_OBSOLETE) // mzML 1.0 support
        return valueAs<double>() * 60;
    return 0; 
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const CVParam& param)
{
    os << cvTermInfo(param.cvid).name << ": " << param.value;

    if (param.units != CVID_Unknown)
        os << " " << cvTermInfo(param.units).name << "(s)";

    return os;
}


} // namespace msdata
} // namespace pwiz


