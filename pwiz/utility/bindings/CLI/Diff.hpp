//
// Diff.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _DIFF_HPP_CLI_
#define _DIFF_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4635 )
#include "MSData.hpp"
#include "../../../data/msdata/Diff.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {


/// configuration struct for diffs
public ref class DiffConfig 
{
    public:

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
public ref class Diff
{
    DEFINE_INTERNAL_BASE_CODE(Diff, pwiz::msdata::Diff<pwiz::msdata::MSData>);

    public:

    Diff();
    Diff(DiffConfig^ config);
    Diff(MSData% a, MSData% b);
    Diff(MSData^ a, MSData^ b);
    Diff(MSData% a, MSData% b, DiffConfig^ config);
    Diff(MSData^ a, MSData^ b, DiffConfig^ config);

    property MSData^ a_b {MSData^ get();}
    property MSData^ b_a {MSData^ get();}

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    static operator bool (Diff^ diff) {return (bool) *diff->base_;}
    static operator bool (Diff% diff) {return (bool) *diff.base_;}

    static operator System::String^ (Diff^ diff);
    static operator System::String^ (Diff% diff);

    Diff^ apply(MSData^ a, MSData^ b);
    Diff^ apply(MSData% a, MSData% b);
};


} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _DIFF_HPP_CLI
