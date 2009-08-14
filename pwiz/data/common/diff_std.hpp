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
#include <cmath>
#include <limits>

namespace pwiz {
namespace diff_std {

/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig 
{
    /// precision with which two doubles are compared
    double precision;

    /// ignore all file level metadata, and most scan level metadata,
    /// i.e. verify scan binary data, plus important scan metadata:
    ///  - msLevel 
    ///  - scanNumber 
    ///  - precursor.ionSelection
    bool ignoreMetadata;

    bool ignoreChromatograms;

    bool ignoreDataProcessing;

    DiffConfig()
    :   precision(1e-6), 
        ignoreMetadata(false),
        ignoreChromatograms(false),
        ignoreDataProcessing(false)
    {}
};

//
// diff implementation declarations
//


namespace diff_impl {


PWIZ_API_DECL
void diff(const std::string& a,
          const std::string& b,
          std::string& a_b,
          std::string& b_a,
          const DiffConfig& config)
{
    a_b.clear();
    b_a.clear();
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}

template <typename T>
void diff_numeric(const T& a, 
                  const T& b, 
                  T& a_b, 
                  T& b_a,
                  const DiffConfig& config)
{
    a_b = 0;
    b_a = 0;
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <>
void diff_numeric(const double& a,
                  const double& b,
                  double& a_b,
                  double& b_a,
                  const DiffConfig& config)
{
    a_b = 0;
    b_a = 0;

    if (fabs(a - b) > config.precision + std::numeric_limits<double>::epsilon())
    {
        a_b = fabs(a - b);
        b_a = fabs(a - b);
    }
}


} // namespace diff_impl

///     
/// Calculate diffs of objects in the MSData structure hierarchy.
///
/// A diff between two objects a and b calculates the set differences
/// a\b and b\a.
///
/// The Diff struct acts as a functor, but also stores the 
/// results of the diff calculation.  
///
/// The bool conversion operator is provided to indicate whether 
/// the two objects are different (either a\b or b\a is non-empty).
///
/// object_type requirements:
///   object_type a;
///   a.empty();
///   diff(const object_type& a, const object_type& b, object_type& a_b, object_type& b_a);
///
template <typename object_type>
struct Diff
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const object_type& a,
               const object_type& b,
               const DiffConfig& config = DiffConfig())
    :   config_(config)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
    }

    object_type a_b;
    object_type b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const object_type& a,
                           const object_type& b)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
        return *this;
    }

    private:
    DiffConfig config_;
};


} // namespace diff_std
} // namespace pwiz

#endif // _DIFF_STD_HPP_

