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


#ifndef _SAMPLEDATUM_HPP_
#define _SAMPLEDATUM_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <iostream>
#include <iomanip>
#include <sstream>
#include <stdexcept>


namespace pwiz {
namespace data {


template<typename abscissa_type, typename ordinate_type>
struct SampleDatum
{
    abscissa_type x;
    ordinate_type y;

    SampleDatum(abscissa_type _x=0, ordinate_type _y=0)
    :  x(_x), y(_y)
    {}
};


template<typename abscissa_type, typename ordinate_type>
bool operator==(const SampleDatum<abscissa_type,ordinate_type>& a,
                const SampleDatum<abscissa_type,ordinate_type>& b)
{
    return (a.x==b.x && a.y==b.y); 
}


namespace SampleDatumConstant
{
    const char open_ = '<';
    const char separator_ = ';'; // MSVC feature: this cannot be ','
    const char close_ = '>';
} // namespace SampleDatumConstant


template<typename abscissa_type, typename ordinate_type>
std::ostream& operator<<(std::ostream& os, const SampleDatum<abscissa_type,ordinate_type>& datum)
{
    os << SampleDatumConstant::open_ 
       << datum.x 
       << SampleDatumConstant::separator_ 
       << datum.y 
       << SampleDatumConstant::close_;

    return os;
}


template<typename abscissa_type, typename ordinate_type>
std::istream& operator>>(std::istream& is, SampleDatum<abscissa_type,ordinate_type>& datum)
{
    std::string buffer;
    is >> buffer; 
    if (!is) return is;

    std::istringstream iss(buffer);

    char open, separator, close;
    abscissa_type x;
    ordinate_type y;
    iss >> open >> x >> separator >> y >> close;

    if (open != SampleDatumConstant::open_ || 
        separator != SampleDatumConstant::separator_ || 
        close != SampleDatumConstant::close_)
       throw std::runtime_error("[SampleDatum::operator>>] Invalid format.");

    datum.x = x; 
    datum.y = y;

    return is;
}


} // namespace data 
} // namespace pwiz


#endif // _SAMPLEDATUM_HPP_


