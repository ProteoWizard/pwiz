//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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


#ifndef _UNIMOD_HPP_CLI_
#define _UNIMOD_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )

#pragma unmanaged
#include "pwiz/data/common/Unimod.hpp"
#pragma managed

#include "SharedCLI.hpp"
#include "cv.hpp"
#include "../chemistry/chemistry.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace data {


using System::String;
using System::Nullable;
using System::Collections::Generic::IList;
using namespace CLI::cv;
using namespace CLI::chemistry;


// acts like a namespace
public ref struct unimod abstract sealed {


[System::FlagsAttribute]
enum class Site
{
    Any = 1<<0,
    NTerminus = 1<<1,
    CTerminus = 1<<2,
    Alanine = 1<<3,
    Cysteine = 1<<4,
    AsparticAcid = 1<<5,
    GlutamicAcid = 1<<6,
    Phenylalanine = 1<<7,
    Glycine = 1<<8,
    Histidine = 1<<9,
    Isoleucine = 1<<10,
    Lysine = 1<<11,
    Leucine = 1<<12,
    Methionine = 1<<13,
    Asparagine = 1<<14,
    Proline = 1<<15,
    Glutamine = 1<<16,
    Arginine = 1<<17,
    Serine = 1<<18,
    Threonine = 1<<19,
    Selenocysteine = 1<<20,
    Valine = 1<<21,
    Tryptophan = 1<<22,
    Tyrosine = 1<<23
};

enum class Position
{
    Anywhere = 0,
    AnyNTerminus,
    AnyCTerminus,
    ProteinNTerminus,
    ProteinCTerminus
};

[System::FlagsAttribute]
enum class Classification
{
    Any = 1<<0,
    Artifact = 1<<1,
    ChemicalDerivative = 1<<2,
    CoTranslational = 1<<3,
    IsotopicLabel = 1<<4,
    Multiple = 1<<5,
    NLinkedGlycosylation = 1<<6,
    NonStandardResidue = 1<<7,
    OLinkedGlycosylation = 1<<8,
    OtherGlycosylation = 1<<9,
    Other = 1<<10,
    PostTranslational = 1<<11,
    PreTranslational = 1<<12,
    Substitution = 1<<13
};


/// <summary>a modification from Unimod</summary>
ref class Modification
{
    DEFINE_INTERNAL_BASE_CODE(Modification, pwiz::data::unimod::Modification);

    public:

    ref class Specificity
    {
        DEFINE_INTERNAL_BASE_CODE(Specificity, pwiz::data::unimod::Modification::Specificity);

        public:

        property Site site { Site get(); }
        property Position position { Position get(); }
        property bool hidden { bool get(); }
        property Classification classification { Classification get(); }
    };

    property CVID cvid { CVID get(); }
    property String^ name { String^ get(); }
    property Formula^ deltaComposition { Formula^ get(); }
    property double deltaMonoisotopicMass { double get(); }
    property double deltaAverageMass { double get(); }
    property bool approved { bool get(); }
    property IList<Specificity^>^ specificities { IList<Specificity^>^ get(); }
};


/// <summary>
/// returns the Site given a one-letter residue code, or:
/// 'x' for Site.Any, 'n' for Site.NTerminus, 'c' for Site.CTerminus
/// </summary>
static Site site(System::Char symbol);


/// <summary>
/// returns a Position corresponding to one of the following CVIDs:
/// CVID.CVID_Unknown: Position.Anywhere
/// MS_modification_specificity_peptide_N_term: Position.AnyNTerminus
/// MS_modification_specificity_peptide_C_term: Position.AnyCTerminus
/// Else: InvalidArgumentException
/// </summary>
static Position position(CVID cvid);


/// <summary>the entire list of Unimod modifications</summary>
static IList<Modification^>^ modifications();


/// <summary>filters the list of Unimod modifications according to specified parameters</summary>
ref struct Filter
{
    /// <summary>
    /// the default filter gets a list of modifications by mass and tolerance;
    /// - mass is treated as monoisotopic
    /// - only approved modifications
    /// - any site, position, classification, and hidden state is allowed
    /// </summary>
    Filter(double mass, double tolerance);

    /// <summary>mass is treated as monoisotopic if monoisotopic=true; if null, both average and monoisotopic lookups are done</summary>
    property double mass;

    /// <summary>the filter accepts mods within the range [mass-tolerance, mass+tolerance]</summary>
    property double tolerance;

    /// <summary>mass is treated as monoisotopic if monoisotopic=true; if null, both average and monoisotopic lookups are done</summary>
    property System::Nullable<bool> monoisotopic;

    /// <summary>if approved is not null, only approved/unapproved modifications are considered</summary>
    property System::Nullable<bool> approved;

    /// <summary>
    /// - the site, position, and classification parameters filter the resulting modifications such that
    ///   every modification must have at least one specificity matching all three criteria;
    /// </summary>
    property Site site;

    /// <summary>
    /// - the site, position, and classification parameters filter the resulting modifications such that
    ///   every modification must have at least one specificity matching all three criteria;
    /// </summary>
    property Position position;

    /// <summary>
    /// - the site, position, and classification parameters filter the resulting modifications such that
    ///   every modification must have at least one specificity matching all three criteria;
    /// </summary>
    property Classification classification;

    /// <summary>if hidden is not null, matching site/position/classification specificities must be hidden (or not)</summary>
    property System::Nullable<bool> hidden;
};

/// <summary>get a list of modifications filtered by the specified filter</summary>
static IList<Modification^>^ modifications(Filter^ filter);

/// <summary>find a modification by CVID</summary>
static Modification^ modification(CVID cvid);

/// <summary>find a modification by title, e.g. "Phospho" not "Phosphorylation"</summary>
static Modification^ modification(String^ title);


}; // namespace unimod
} // namespace data
} // namespace CLI
} // namespace pwiz


#endif // _UNIMOD_HPP_CLI_
