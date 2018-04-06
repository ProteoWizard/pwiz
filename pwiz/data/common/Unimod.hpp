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


#ifndef _UNIMOD_HPP_
#define _UNIMOD_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "cv.hpp"
#include <boost/enum.hpp>
#include <boost/logic/tribool.hpp>


namespace pwiz {
namespace data {
namespace unimod {

using namespace cv;
using namespace chemistry;
using namespace boost::logic;

BOOST_BITFIELD_EX(Site, PWIZ_API_DECL,
    (Any)(1<<0)
    (NTerminus)(1<<1)
    (CTerminus)(1<<2)
    (Alanine)(1<<3)
    (Cysteine)(1<<4)
    (AsparticAcid)(1<<5)
    (GlutamicAcid)(1<<6)
    (Phenylalanine)(1<<7)
    (Glycine)(1<<8)
    (Histidine)(1<<9)
    (Isoleucine)(1<<10)
    (Lysine)(1<<11)
    (Leucine)(1<<12)
    (Methionine)(1<<13)
    (Asparagine)(1<<14)
    (Proline)(1<<15)
    (Glutamine)(1<<16)
    (Arginine)(1<<17)
    (Serine)(1<<18)
    (Threonine)(1<<19)
    (Selenocysteine)(1<<20)
    (Valine)(1<<21)
    (Tryptophan)(1<<22)
    (Tyrosine)(1<<23)
)
BOOST_BITFIELD_DOMAIN_OPERATORS(Site)

BOOST_ENUM_EX(Position, PWIZ_API_DECL,
    (Anywhere)
    (AnyNTerminus)
    (AnyCTerminus)
    (ProteinNTerminus)
    (ProteinCTerminus)
)
BOOST_ENUM_DOMAIN_OPERATORS(Position)

BOOST_BITFIELD_EX(Classification, PWIZ_API_DECL,
    (Any)(1<<0)
    (Artifact)(1<<1)
    (ChemicalDerivative)(1<<2)
    (CoTranslational)(1<<3)
    (IsotopicLabel)(1<<4)
    (Multiple)(1<<5)
    (NLinkedGlycosylation)(1<<6)
    (NonStandardResidue)(1<<7)
    (OLinkedGlycosylation)(1<<8)
    (OtherGlycosylation)(1<<9)
    (Other)(1<<10)
    (PostTranslational)(1<<11)
    (PreTranslational)(1<<12)
    (Substitution)(1<<13)
    (SynthPepProtectGP)(1<<14)
)
BOOST_BITFIELD_DOMAIN_OPERATORS(Classification)


/// a modification from Unimod
struct PWIZ_API_DECL Modification
{
    struct PWIZ_API_DECL Specificity
    {
        Site site;
        Position position;
        bool hidden;
        Classification classification;
    };

    CVID cvid;
    std::string name;
    Formula deltaComposition;
    double deltaMonoisotopicMass() const;
    double deltaAverageMass() const;
    bool approved;
    std::vector<Specificity> specificities;
};


/// returns the Site given a one-letter residue code, or:
/// 'x' for Site::Any, 'n' for Site::NTerminus, 'c' for Site::CTerminus
PWIZ_API_DECL Site site(char symbol);


/// returns a Position corresponding to one of the following CVIDs:
/// CVID_Unknown: Position::Anywhere
/// MS_modification_specificity_peptide_N_term: Position::AnyNTerminus
/// MS_modification_specificity_peptide_C_term: Position::AnyCTerminus
/// Else: invalid_argument exception
PWIZ_API_DECL Position position(CVID cvid = CVID_Unknown);


/// the entire list of Unimod modifications
PWIZ_API_DECL const std::vector<Modification>& modifications();

/// get a list of modifications by mass and tolerance;
/// - mass is treated as monoisotopic if monoisotopic=true; if indeterminate, both average and monoisotopic lookups are done
/// - if approved is not indeterminate, only approved/unapproved modifications are considered
/// - the site, position, and classification parameters filter the resulting modifications such that
///   every modification must have at least one specificity matching all three criteria;
/// - if hidden is not indeterminate, matching site/position/classification specificities must be hidden (or not)
PWIZ_API_DECL std::vector<Modification> modifications(double mass,
                                                      double tolerance,
                                                      tribool monoisotopic = true,
                                                      tribool approved = true,
                                                      Site site = Site::Any,
                                                      Position position = Position::Anywhere,
                                                      Classification classification = Classification::Any,
                                                      tribool hidden = indeterminate);

/// find a modification by CVID
PWIZ_API_DECL const Modification& modification(CVID cvid);

/// find a modification by title, e.g. "Phospho" not "Phosphorylation"
PWIZ_API_DECL const Modification& modification(const std::string& title);


} // namespace unimod
} // namespace data
} // namespace pwiz


#endif // _UNIMOD_HPP_
