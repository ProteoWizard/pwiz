//
// Diff.hpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TRADATA_DIFF_HPP_
#define _TRADATA_DIFF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"
#include "TextWriter.hpp"


namespace pwiz {
namespace tradata {


/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig 
{
    /// precision with which two doubles are compared
    double precision;

    DiffConfig()
    :   precision(1e-6)
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
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const CV& a,
          const CV& b,
          CV& a_b,
          CV& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const UserParam& a,
          const UserParam& b,
          UserParam& a_b,
          UserParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const CVParam& a,
          const CVParam& b,
          CVParam& a_b,
          CVParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ParamContainer& a,
          const ParamContainer& b,
          ParamContainer& a_b,
          ParamContainer& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Publication& a,
          const Publication& b,
          Publication& a_b,
          Publication& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const RetentionTime& a,
          const RetentionTime& b,
          RetentionTime& a_b,
          RetentionTime& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Prediction& a,
          const Prediction& b,
          Prediction& a_b,
          Prediction& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Evidence& a,
          const Evidence& b,
          Evidence& a_b,
          Evidence& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Validation& a,
          const Validation& b,
          Validation& a_b,
          Validation& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Instrument& a,
          const Instrument& b,
          Instrument& a_b,
          Instrument& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Configuration& a,
          const Configuration& b,
          Configuration& a_b,
          Configuration& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Software& a,
          const Software& b,
          Software& a_b,
          Software& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Interpretation& a,
          const Interpretation& b,
          Interpretation& a_b,
          Interpretation& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Protein& a,
          const Protein& b,
          Protein& a_b,
          Protein& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Compound& a,
          const Compound& b,
          Compound& a_b,
          Compound& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Precursor& a,
          const Precursor& b,
          Precursor& a_b,
          Precursor& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Product& a,
          const Product& b,
          Product& a_b,
          Product& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Transition& a,
          const Transition& b,
          Transition& a_b,
          Transition& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const TraData& a,
          const TraData& b,
          TraData& a_b,
          TraData& b_a,
          const DiffConfig& config);

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


///
/// stream insertion of Diff results
///

template <typename object_type>
std::ostream& operator<<(std::ostream& os, const Diff<object_type>& diff)
{
    TextWriter write(os, 1);

    if (!diff.a_b.empty())
    {            
        os << "+\n";
        write(diff.a_b);
    }

    if (!diff.b_a.empty())
    {            
        os << "-\n";
        write(diff.b_a);
    }

    return os;
}

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Diff<TraData>& diff);

} // namespace tradata
} // namespace pwiz


#endif // _TRADATA_DIFF_HPP_
