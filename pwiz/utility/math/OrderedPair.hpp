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


#ifndef _ORDEREDPAIR_HPP_
#define _ORDEREDPAIR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <vector>
#include <iostream>
#include <stdexcept>


namespace pwiz {
namespace math {


#pragma pack(push, 1)
struct OrderedPair
{
    double x;
    double y;

    OrderedPair(double _x = 0, double _y = 0) : x(_x), y(_y) {}
};
#pragma pack(pop)


inline std::ostream& operator<<(std::ostream& os, const OrderedPair& p)
{
    os << "(" << p.x << "," << p.y << ")";
    return os;
}


inline std::istream& operator>>(std::istream& is, OrderedPair& p)
{
    char open='\0', comma='\0', close='\0';
    is >>  open >> p.x >> comma >> p.y >> close;
    if (!is) return is;
    if (open!='(' || comma!=',' || close!=')')
        throw std::runtime_error("[OrderedPair::operator>>] Unexpected input.");
    return is;
}


inline bool operator==(const OrderedPair& a, const OrderedPair& b)
{
    return a.x==b.x && a.y==b.y;
}


inline bool operator!=(const OrderedPair& a, const OrderedPair& b)
{
    return !(a == b);
}


///
/// wrapper class for accessing contiguous data as a container of OrderedPairs;
/// note that it does not own the underlying data
///
class OrderedPairContainerRef
{
    public:
    
    /// constructor for wrapping array of contiguous data
    OrderedPairContainerRef(const void* begin, const void* end)
    :   begin_(reinterpret_cast<const OrderedPair*>(begin)),
        end_(reinterpret_cast<const OrderedPair*>(end))
    {}

    /// template constructor for automatic conversion from vector;
    /// e.g. vector<double>, vector<OrderedPair>, vector<CustomPair> 
    template<typename T>
    OrderedPairContainerRef(const std::vector<T>& v)
    :   begin_(reinterpret_cast<const OrderedPair*>(&v[0])),
        end_(reinterpret_cast<const OrderedPair*>(&v[0]+v.size()))
    {}

    typedef const OrderedPair* const_iterator;

    const_iterator begin() const {return begin_;}
    const_iterator end() const {return end_;}
    size_t size() const {return end_-begin_;}
    const OrderedPair& operator[](size_t index) const {return *(begin_+index);}

    private:

    const OrderedPair* begin_;
    const OrderedPair* end_;
};


} // namespace math 
} // namespace pwiz


#endif // _ORDEREDPAIR_HPP_

