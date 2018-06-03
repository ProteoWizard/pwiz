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


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Unimod.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::chemistry;
using namespace pwiz::data::unimod;
using namespace boost::logic;


ostream* os_ = 0;


void test()
{
    if (os_) *os_ << "test()\n"; 

    unit_assert_operator_equal(Site::Any, site('x'));
    unit_assert_operator_equal(Site::Any, site('X'));
    unit_assert_operator_equal(Site::NTerminus, site('n'));
    unit_assert_operator_equal(Site::CTerminus, site('c'));
    unit_assert_operator_equal(Site::Alanine, site('A'));
    unit_assert_operator_equal(Site::Tyrosine, site('Y'));
    unit_assert_operator_equal((Site::Asparagine | Site::AsparticAcid), site('B'));
    unit_assert_operator_equal((Site::Glutamine | Site::GlutamicAcid), site('Z'));
    unit_assert_throws_what(site('1'), invalid_argument, "[unimod::site] invalid symbol \"1\"");
    unit_assert_throws_what(site('z'), invalid_argument, "[unimod::site] invalid symbol \"z\"");


    unit_assert_operator_equal(Position::Anywhere, position());
    unit_assert_operator_equal(Position::AnyNTerminus, position(MS_modification_specificity_peptide_N_term));
    unit_assert_operator_equal(Position::AnyCTerminus, position(MS_modification_specificity_peptide_C_term));
    unit_assert_throws_what(position(MS_matrix_assisted_laser_desorption_ionization), invalid_argument, "[unimod::position] invalid cvid \"MALDI\" (1000075)");


    if (os_) *os_ << "Unimod entries: " << modifications().size() << endl;
    unit_assert(modifications().size() > 500);


    const Modification& acetyl = modification("Acetyl");
    unit_assert_operator_equal(UNIMOD_Acetyl, acetyl.cvid);
    unit_assert_operator_equal(&acetyl, &modification(UNIMOD_Acetyl));
    unit_assert_operator_equal("Acetyl", acetyl.name);
    unit_assert(acetyl.approved);
    unit_assert_operator_equal("C2H2O1", acetyl.deltaComposition.formula());
    unit_assert_operator_equal(9, acetyl.specificities.size());

    unit_assert_operator_equal(Site::Lysine, acetyl.specificities[0].site);
    unit_assert_operator_equal(Position::Anywhere, acetyl.specificities[0].position);
    unit_assert_operator_equal(false, acetyl.specificities[0].hidden);
    unit_assert_operator_equal(Classification::Multiple, acetyl.specificities[0].classification);

    unit_assert_operator_equal(Site::NTerminus, acetyl.specificities[1].site);
    unit_assert_operator_equal(Position::AnyNTerminus, acetyl.specificities[1].position);
    unit_assert_operator_equal(false, acetyl.specificities[1].hidden);
    unit_assert_operator_equal(Classification::Multiple, acetyl.specificities[1].classification);

    unit_assert_operator_equal(Site::Cysteine, acetyl.specificities[2].site);
    unit_assert_operator_equal(Position::Anywhere, acetyl.specificities[2].position);
    unit_assert_operator_equal(true, acetyl.specificities[2].hidden);
    unit_assert_operator_equal(Classification::PostTranslational, acetyl.specificities[2].classification);

    unit_assert_operator_equal(Site::NTerminus, acetyl.specificities[4].site);
    unit_assert_operator_equal(Position::ProteinNTerminus, acetyl.specificities[4].position);
    unit_assert_operator_equal(false, acetyl.specificities[4].hidden);
    unit_assert_operator_equal(Classification::PostTranslational, acetyl.specificities[4].classification);

    unit_assert_operator_equal(UNIMOD_Acetyl, modifications(acetyl.deltaMonoisotopicMass(), 0)[0].cvid);
    unit_assert_operator_equal(UNIMOD_Acetyl, modifications(acetyl.deltaAverageMass(), 0, false)[0].cvid);

    // test a position-only filter
    unit_assert_operator_equal(1, modifications(acetyl.deltaMonoisotopicMass(), 0.0001,
                                                indeterminate, indeterminate,
                                                Site::Any, Position::AnyNTerminus).size());
    unit_assert_operator_equal(UNIMOD_Acetyl, modifications(acetyl.deltaMonoisotopicMass(), 0.5,
                                                            indeterminate, indeterminate,
                                                            Site::Any, Position::AnyNTerminus)[0].cvid);


    const Modification& hse = modification("Met->Hse");
    unit_assert_operator_equal(UNIMOD_Met__Hse, hse.cvid);
    unit_assert_operator_equal("Met->Hse", hse.name);
    unit_assert(hse.approved);
    unit_assert_operator_equal("C-1H-2O1S-1", hse.deltaComposition.formula());
    unit_assert_operator_equal(Site::Methionine, hse.specificities[0].site);
    unit_assert_operator_equal(Position::AnyCTerminus, hse.specificities[0].position);
    unit_assert_operator_equal(false, hse.specificities[0].hidden);
    unit_assert_operator_equal(Classification::ChemicalDerivative, hse.specificities[0].classification);


    const Modification& oxidation = modification(UNIMOD_Oxidation);

    // 3 mods have the same mass as oxidation
    unit_assert_operator_equal(3, modifications(oxidation.deltaMonoisotopicMass(), 0, true, indeterminate).size());
    unit_assert_operator_equal(3, modifications(oxidation.deltaAverageMass(), 0, false, indeterminate).size());

    // only one of those mods happen on Methionine
    unit_assert_operator_equal(1, modifications(oxidation.deltaMonoisotopicMass(), 0, true, indeterminate, Site::Methionine).size());
    unit_assert_operator_equal(1, modifications(oxidation.deltaAverageMass(), 0, false, indeterminate, Site::Methionine).size());

    // oxidation also happens on Proline (test multi-bit Site mask)
    unit_assert_operator_equal(1, modifications(oxidation.deltaAverageMass(), 0, false, indeterminate, Site::Methionine | Site::Proline).size());

    // add Alanine as a site and it could be a substitution
    unit_assert_operator_equal(2, modifications(oxidation.deltaAverageMass(), 0, false, indeterminate, Site::Methionine | Site::Alanine).size());


    // 19 mods are 28 +/- 1
    unit_assert_operator_equal(19, modifications(28, 1, true, indeterminate).size());

    // only two of those mods happen post-translationally on protein N-termini
    unit_assert_operator_equal(2, modifications(28, 1, true, indeterminate, Site::Any,
                                                Position::ProteinNTerminus,
                                                Classification::PostTranslational).size());


    const Modification& phospho = modification(UNIMOD_Phospho);

    // phospho on S and T are grouped (property names are duplicated)
    unit_assert_operator_equal(UNIMOD_Phospho, modifications(phospho.deltaMonoisotopicMass(), 0, true, true, Site::Serine)[0].cvid);
    unit_assert_operator_equal(UNIMOD_Phospho, modifications(phospho.deltaMonoisotopicMass(), 0, true, true, Site::Threonine)[0].cvid);
    unit_assert_operator_equal(UNIMOD_Phospho, modifications(phospho.deltaMonoisotopicMass(), 0, true, true, Site::Tyrosine)[0].cvid);

    // test multi-bit Site mask
    unit_assert_operator_equal(UNIMOD_Phospho, modifications(phospho.deltaMonoisotopicMass(), 0, true, true, Site::Serine | Site::Tyrosine)[0].cvid);

    // there are no unapproved mods at phospho's mass
    unit_assert_operator_equal(0, modifications(phospho.deltaMonoisotopicMass(), 0, true, false).size());

    // phospho and sulfo are only distinguishable with PPM mass accuracy
    double mass_2000Da_1ppm = 2000 - (2000 - MZTolerance(1, MZTolerance::PPM));
    unit_assert_operator_equal(2, modifications(phospho.deltaMonoisotopicMass(), 0.5, true, true, Site::Serine).size());
    unit_assert_operator_equal(1, modifications(phospho.deltaMonoisotopicMass(), mass_2000Da_1ppm, true, true, Site::Serine).size());

    // test indeterminate and average mass
    unit_assert_operator_equal(2, modifications(phospho.deltaMonoisotopicMass(), 0.1, indeterminate, true, Site::Serine).size());
    unit_assert_operator_equal(2, modifications(phospho.deltaAverageMass(), 0.1, indeterminate, true, Site::Serine).size());
    unit_assert_operator_equal(2, modifications(phospho.deltaAverageMass(), 0.1, false, true, Site::Serine).size());

    // test negative mass
    unit_assert_operator_equal(UNIMOD_Gln__pyro_Glu, modifications(-17.0265,
                                                                   mass_2000Da_1ppm,
                                                                   true, true,
                                                                   Site::Glutamine,
                                                                   Position::AnyNTerminus)[0].cvid);


    // at 14.5 +/- 0.5 there are 3 approved mods and 6 unapproved
    unit_assert_operator_equal(3, modifications(14.5, 0.5, true, true).size());
    unit_assert_operator_equal(9, modifications(14.5, 0.5, true, false).size());

    // all 9 unapproved mods are hidden
    unit_assert_operator_equal(0, modifications(14.5, 0.5, true, false, Site::Any,
                                                Position::Anywhere, Classification::Any, false).size());

    // test ambiguous residue; this mod could be a Q->P substitution
    unit_assert_operator_equal(1, modifications(-31, 0.01, true, indeterminate, site('Z')).size());
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

