//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#ifndef _PROTEOME_DIFF_HPP_
#define _PROTEOME_DIFF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/common/diff_std.hpp"
#include "ProteomeData.hpp"
#include "TextWriter.hpp"


namespace pwiz {
namespace proteome {


/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig 
{
    /// ignore all metadata except protein ids
    bool ignoreMetadata;

    DiffConfig()
    :   ignoreMetadata(false)
    {}
};


//
// diff implementation declarations
//


namespace diff_impl {

PWIZ_API_DECL
void diff(const ProteinList& a,
          const ProteinList& b,
          ProteinListSimple& a_b,
          ProteinListSimple& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteomeData& a,
          const ProteomeData& b,
          ProteomeData& a_b,
          ProteomeData& b_a,
          const DiffConfig& config);

} // namespace diff_impl


///     
/// Calculate diffs of objects in the ProteomeData structure hierarchy.
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


template <>
struct Diff<ProteinList>
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const ProteinList& a,
         const ProteinList& b,
         const DiffConfig& config = DiffConfig())
      :   config_(config)
    {
        diff_impl::diff(a, b, a_b, b_a, config_);
    }

    ProteinListSimple a_b;
    ProteinListSimple b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const ProteinList& a,
		             const ProteinList& b)
 
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

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Diff<ProteomeData>& diff);


} // namespace proteome
} // namespace pwiz


#endif // _PROTEOME_DIFF_HPP_
