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


#ifndef _DIFF_HPP_CLI_
#define _DIFF_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "MSData.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {


/// <summary>
/// configuration struct for diffs
/// </summary>
public ref class DiffConfig 
{
    public:

    /// <summary>
    /// precision with which two doubles are compared
    /// </summary>
    double precision;

    /// <summary>
    /// ignore all file level metadata, and most scan level metadata,
    /// i.e. verify scan binary data, plus important scan metadata:
    /// <para>- msLevel</para>
    /// <para>- scanNumber</para>
    /// <para>- precursor.ionSelection</para>
    /// </summary>
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


/// <summary>
/// Calculate diffs of objects in the MSData structure hierarchy.
/// <para/>
/// <para>A diff between two objects a and b calculates the set differences
/// a\b and b\a.</para>
/// <para/>
/// <para>The Diff struct acts as a functor, but also stores the 
/// results of the diff calculation.</para>
/// <para/>
/// <para>The bool conversion operator is provided to indicate whether 
/// the two objects are different (either a\b or b\a is non-empty).</para>
/// </summary>
public ref class Diff
{
    typedef pwiz::data::Diff<pwiz::msdata::MSData, pwiz::msdata::DiffConfig> MSDataDiff;
    DEFINE_INTERNAL_BASE_CODE(Diff, MSDataDiff);

    public:

    Diff();
    Diff(DiffConfig^ config);
    Diff(MSData% a, MSData% b);
    Diff(MSData^ a, MSData^ b);
    Diff(MSData% a, MSData% b, DiffConfig^ config);
    Diff(MSData^ a, MSData^ b, DiffConfig^ config);

    property MSData^ a_b {MSData^ get();}
    property MSData^ b_a {MSData^ get();}

    /// <summary>
    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    /// </summary>
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
