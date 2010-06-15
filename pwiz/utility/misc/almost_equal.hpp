//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, California
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


#ifndef _ALMOST_EQUAL_HPP_
#define _ALMOST_EQUAL_HPP_


#include <limits>
#include <cmath>


namespace pwiz {
namespace util {


template <typename float_type>
bool almost_equal(float_type a, float_type b, int multiplier = 1)
{
    float_type scale = a==float_type(0.0) ? float_type(1.0) : a;
    return std::abs((a-b)/scale) < float_type(multiplier) * std::numeric_limits<float_type>::epsilon();
}


} // namespace util
} // namespace pwiz 


#endif // _ALMOST_EQUAL_HPP_


