//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "PeakInfo.hpp"
#include "data/FrequencyData.hpp"


using namespace std;


namespace pwiz {
namespace peaks {


PeakInfo::PeakInfo(double f, complex<double> i, int c) 
:   frequency(f), intensity(i), charge(c) 
{}


PeakInfo::PeakInfo(const data::FrequencyDatum& datum, int c)
:   frequency(datum.x), intensity(datum.y), charge(c)
{}


bool operator==(const PeakInfo& a, const PeakInfo& b) 
{
    return a.frequency == b.frequency && 
        a.intensity == b.intensity &&
        a.charge == b.charge;
}


bool operator!=(const PeakInfo& a, const PeakInfo& b) 
{
    return !(a==b); 
}


std::ostream& operator<<(std::ostream& os, const PeakInfo& pi)
{
    os << pi.frequency << " " 
        << pi.intensity << " " 
        << pi.charge;

    return os;
}


std::istream& operator>>(std::istream& is, PeakInfo& pi)
{
    is >> pi.frequency >> pi.intensity >> pi.charge;
    return is;
}


} // namespace peaks
} // namespace pwiz


