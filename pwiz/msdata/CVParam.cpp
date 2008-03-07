//
// CVParam.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
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


