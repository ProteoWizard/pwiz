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


#include "CVParam.hpp"
#include <iostream>


namespace pwiz {
namespace msdata {


using namespace std;


//
// CVParam
//


string CVParam::name() const
{
    return cvinfo(cvid).name;
}


string CVParam::unitsName() const
{
    return cvinfo(units).name;
}


double CVParam::timeInSeconds() const
{
    if (units == MS_second) 
        return valueAs<double>();
    else if (units == MS_minute) 
        return valueAs<double>() * 60;
    return 0; 
}


ostream& operator<<(ostream& os, const CVParam& param)
{
    os << cvinfo(param.cvid).name << ": " << param.value;

    if (param.units != CVID_Unknown)
        os << " " << cvinfo(param.units).name << "(s)";

    return os;
}


} // namespace msdata
} // namespace pwiz


