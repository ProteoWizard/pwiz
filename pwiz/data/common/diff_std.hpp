//
// $Id$
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _DIFF_STD_HPP_
#define _DIFF_STD_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <vector>
#include <cmath>
#include <limits>
#include <stdexcept>


namespace pwiz {
namespace diff_std {


template <typename string_type>
void diff_string(const string_type& a,
                 const string_type& b,
                 string_type& a_b,
                 string_type& b_a)
{
    a_b.clear();
    b_a.clear();
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}

template <typename integral_type>
void diff_integral(const integral_type& a, 
                   const integral_type& b, 
                   integral_type& a_b, 
                   integral_type& b_a)
{
    a_b = 0;
    b_a = 0;
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}

template <typename floating_type>
void diff_floating(const floating_type& a,
                   const floating_type& b,
                   floating_type& a_b,
                   floating_type& b_a,
                   const floating_type& precision)
{
    a_b = 0;
    b_a = 0;

    if (fabs(a - b) > precision + std::numeric_limits<floating_type>::epsilon())
    {
        a_b = fabs(a - b);
        b_a = fabs(a - b);
    }
}

/// measure maximum relative difference between elements in the vectors
template <typename floating_type>
floating_type maxdiff(const std::vector<floating_type>& a, const std::vector<floating_type>& b)
{
    if (a.size() != b.size()) 
        throw std::runtime_error("[Diff::maxdiff()] Sizes differ.");

    typename std::vector<floating_type>::const_iterator i = a.begin(); 
    typename std::vector<floating_type>::const_iterator j = b.begin(); 

    floating_type max = 0;

    for (; i!=a.end(); ++i, ++j)
    {
        floating_type denominator = std::min(*i, *j);
        if (denominator == 0) denominator = 1;
        floating_type current = fabs(*i - *j)/denominator;
        if (max < current) max = current;

    }

    return max;
}

} // namespace diff_std
} // namespace pwiz

#endif // _DIFF_STD_HPP_
