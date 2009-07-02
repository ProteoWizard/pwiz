//
// Diff.hpp
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


#ifndef _MSIDDATA_HPP_
#define _MSIDDATA_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "TextWriter.hpp"

#include <string>

namespace pwiz {
namespace mziddata {

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

void diff(const CVParam& a, 
          const CVParam& b, 
          CVParam& a_b, 
          CVParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const DataCollection& a,
          const DataCollection& b,
          DataCollection& a_b,
          DataCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisProtocol& a,
          const AnalysisProtocol& b,
          AnalysisProtocol& a_b,
          AnalysisProtocol& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const BibliographicReference& a,
          const BibliographicReference& b,
          BibliographicReference& a_b,
          BibliographicReference& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Analysis& a,
          const Analysis& b,
          Analysis& a_b,
          Analysis& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const DBSequence& a,
          const DBSequence& b,
          DBSequence& a_b,
          DBSequence& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisSampleCollection& a,
          const AnalysisSampleCollection& b,
          AnalysisSampleCollection& a_b,
          AnalysisSampleCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Provider& a,
          const Provider& b,
          Provider& a_b,
          Provider& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ContactRole& a,
          const ContactRole& b,
          ContactRole& a_b,
          ContactRole& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisSoftwarePtr& a,
          const AnalysisSoftwarePtr& b,
          AnalysisSoftwarePtr& a_b,
          AnalysisSoftwarePtr& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisSoftware& a,
          const AnalysisSoftware& b,
          AnalysisSoftware& a_b,
          AnalysisSoftware& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const MzIdentML& a,
          const MzIdentML& b,
          MzIdentML& a_b,
          MzIdentML& b_a,
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

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Diff<MzIdentML>& diff);

} // namespace mziddata
} // namespace pwiz

#endif // _MSIDDATA_HPP_
