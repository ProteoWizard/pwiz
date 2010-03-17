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
                                                                                                     

#define PWIZ_SOURCE
#include "MZTolerance.hpp"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include <cstring>
#include <string>


namespace pwiz {
namespace analysis {


using namespace std;


PWIZ_API_DECL ostream& operator<<(ostream& os, const MZTolerance& mzt)
{
    const char* units = (mzt.units==MZTolerance::MZ) ? "mz" : "ppm";
    os << mzt.value << units;
    return os;
}


PWIZ_API_DECL istream& operator>>(istream& is, MZTolerance& mzt)
{
    string temp;
    is >> mzt.value >> temp;

    transform(temp.begin(), temp.end(), temp.begin(), ::toupper);
    if (temp == "MZ") mzt.units = MZTolerance::MZ;
    else if (temp == "PPM") mzt.units = MZTolerance::PPM;
    else throw runtime_error(("[MZTolerance::operator>>] Unable to parse units: " + temp).c_str());

    return is;
}


PWIZ_API_DECL bool operator==(const MZTolerance& a, const MZTolerance& b)
{
    return a.value==b.value && a.units==b.units;
}


PWIZ_API_DECL double& operator+=(double& d, const MZTolerance& tolerance)
{
    if (tolerance.units == MZTolerance::MZ)
        d += tolerance.value;
    else if (tolerance.units == MZTolerance::PPM)
        d += d*tolerance.value*1e-6;
    else
        throw runtime_error("[MZTolerance::operator+=] This isn't happening.");

    return d;
}


PWIZ_API_DECL double& operator-=(double& d, const MZTolerance& tolerance)
{
    if (tolerance.units == MZTolerance::MZ)
        d -= tolerance.value;
    else if (tolerance.units == MZTolerance::PPM)
        d -= d*tolerance.value*1e-6;
    else
        throw runtime_error("[MZTolerance::operator-=] This isn't happening.");

    return d;
}


PWIZ_API_DECL double operator+(double d, const MZTolerance& tolerance)
{
    d += tolerance;
    return d;
}


PWIZ_API_DECL double operator-(double d, const MZTolerance& tolerance)
{
    d -= tolerance;
    return d;
}


PWIZ_API_DECL bool isWithinTolerance(double a, double b, const MZTolerance& tolerance)
{
    return (a > b-tolerance) && (a < b+tolerance);
}

PWIZ_API_DECL bool lessThanTolerance(double a, double b, const MZTolerance& tolerance)
{
	return (a < b-tolerance);
}


} // namespace analysis
} // namespace pwiz


