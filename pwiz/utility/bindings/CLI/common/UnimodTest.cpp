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


#include "unit.hpp"
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using System::String;
using System::ArgumentException;
using pwiz::chemistry::MZTolerance;

typedef unimod::Site Site;
typedef unimod::Position Position;
typedef unimod::Classification Classification;
typedef unimod::Modification Modification;
#define indeterminate System::Nullable<bool>()

unimod::Filter^ filter(double mass,
                       double tolerance,
                       System::Nullable<bool> monoisotopic,
                       System::Nullable<bool> approved,
                       unimod::Site site,
                       unimod::Position position,
                       unimod::Classification classification,
                       System::Nullable<bool> hidden)
{
    unimod::Filter^ filter = gcnew unimod::Filter(mass, tolerance);
    filter->monoisotopic = monoisotopic;
    filter->approved = approved;
    filter->site = site;
    filter->position = position;
    filter->classification = classification;
    filter->hidden = hidden;
    return filter;
}

unimod::Filter^ filter(double mass, double tolerance)
{
    return filter(mass, tolerance,
                  true, true,
                  unimod::Site::Any, unimod::Position::Anywhere,
                  unimod::Classification::Any, indeterminate);
}

unimod::Filter^ filter(double mass, double tolerance,
                       System::Nullable<bool> monoisotopic,
                       System::Nullable<bool> approved)
{
    return filter(mass, tolerance,
                  monoisotopic, approved,
                  unimod::Site::Any, unimod::Position::Anywhere,
                  unimod::Classification::Any, indeterminate);
}

unimod::Filter^ filter(double mass, double tolerance,
                       System::Nullable<bool> monoisotopic,
                       System::Nullable<bool> approved,
                       unimod::Site site,
                       unimod::Position position)
{
    return filter(mass, tolerance,
                  monoisotopic, approved,
                  site, position,
                  unimod::Classification::Any, indeterminate);
}


void test()
{
    unit_assert_operator_equal(Site::Any, unimod::site('x'));
    unit_assert_operator_equal(Site::Any, unimod::site('X'));
    unit_assert_operator_equal(Site::NTerminus, unimod::site('n'));
    unit_assert_operator_equal(Site::CTerminus, unimod::site('c'));
    unit_assert_operator_equal(Site::Alanine, unimod::site('A'));
    unit_assert_operator_equal(Site::Tyrosine, unimod::site('Y'));
    unit_assert_operator_equal((Site::Asparagine | Site::AsparticAcid), unimod::site('B'));
    unit_assert_operator_equal((Site::Glutamine | Site::GlutamicAcid), unimod::site('Z'));
    unit_assert_throws_what(unimod::site('1'), ArgumentException, "[unimod::site] invalid symbol \"1\"");
    unit_assert_throws_what(unimod::site('z'), ArgumentException, "[unimod::site] invalid symbol \"z\"");


    //unit_assert_operator_equal(Position::Anywhere, unimod::position());
    unit_assert_operator_equal(Position::AnyNTerminus, unimod::position(CVID::MS_modification_specificity_peptide_N_term));
    unit_assert_operator_equal(Position::AnyCTerminus, unimod::position(CVID::MS_modification_specificity_peptide_C_term));
    unit_assert_throws_what(unimod::position(CVID::MS_matrix_assisted_laser_desorption_ionization), ArgumentException, "[unimod::position] invalid cvid \"MALDI\" (1000075)");


    //if (os_) *os_ << "Unimod entries: " << modifications()->Count << endl;
    unit_assert(unimod::modifications()->Count > 500);


    Modification^ acetyl = unimod::modification("Acetyl");
    unit_assert_operator_equal(CVID::UNIMOD_Acetyl, acetyl->cvid);
    //unit_assert_operator_equal(&acetyl, &unimod::modification(CVID::UNIMOD_Acetyl));
    unit_assert_string_operator_equal(gcnew String("Acetyl"), acetyl->name);
    unit_assert(acetyl->approved);
    unit_assert_string_operator_equal(gcnew String("C2H2O1"), acetyl->deltaComposition->formula());
    unit_assert_operator_equal(9, acetyl->specificities->Count);

    unit_assert_operator_equal(Site::Lysine, acetyl->specificities->default[0]->site);
    unit_assert_operator_equal(Position::Anywhere, acetyl->specificities->default[0]->position);
    unit_assert_operator_equal(false, acetyl->specificities->default[0]->hidden);
    unit_assert_operator_equal(Classification::Multiple, acetyl->specificities->default[0]->classification);

    unit_assert_operator_equal(Site::NTerminus, acetyl->specificities->default[1]->site);
    unit_assert_operator_equal(Position::AnyNTerminus, acetyl->specificities->default[1]->position);
    unit_assert_operator_equal(false, acetyl->specificities->default[1]->hidden);
    unit_assert_operator_equal(Classification::Multiple, acetyl->specificities->default[1]->classification);

    unit_assert_operator_equal(Site::Cysteine, acetyl->specificities->default[2]->site);
    unit_assert_operator_equal(Position::Anywhere, acetyl->specificities->default[2]->position);
    unit_assert_operator_equal(true, acetyl->specificities->default[2]->hidden);
    unit_assert_operator_equal(Classification::PostTranslational, acetyl->specificities->default[2]->classification);

    unit_assert_operator_equal(Site::NTerminus, acetyl->specificities->default[4]->site);
    unit_assert_operator_equal(Position::ProteinNTerminus, acetyl->specificities->default[4]->position);
    unit_assert_operator_equal(false, acetyl->specificities->default[4]->hidden);
    unit_assert_operator_equal(Classification::PostTranslational, acetyl->specificities->default[4]->classification);

    unit_assert_operator_equal(CVID::UNIMOD_Acetyl, unimod::modifications(filter(acetyl->deltaMonoisotopicMass, 0))->default[0]->cvid);
    unit_assert_operator_equal(CVID::UNIMOD_Acetyl, unimod::modifications(filter(acetyl->deltaAverageMass, 0, false, indeterminate))->default[0]->cvid);

    // test a position-only filter
    unit_assert_operator_equal(1, unimod::modifications(filter(acetyl->deltaMonoisotopicMass, 0.0001,
                                                               indeterminate, indeterminate,
                                                               Site::Any, Position::AnyNTerminus))->Count);
    unit_assert_operator_equal(CVID::UNIMOD_Acetyl, unimod::modifications(filter(acetyl->deltaMonoisotopicMass, 0.5,
                                                                                 indeterminate, indeterminate,
                                                                                 Site::Any, Position::AnyNTerminus))->default[0]->cvid);


    Modification^ hse = unimod::modification("Met->Hse");
    unit_assert_operator_equal(CVID::UNIMOD_Met__Hse, hse->cvid);
    unit_assert_string_operator_equal(gcnew String("Met->Hse"), hse->name);
    unit_assert(hse->approved);
    unit_assert_string_operator_equal(gcnew String("C-1H-2O1S-1"), hse->deltaComposition->formula());
    unit_assert_operator_equal(Site::Methionine, hse->specificities->default[0]->site);
    unit_assert_operator_equal(Position::AnyCTerminus, hse->specificities->default[0]->position);
    unit_assert_operator_equal(false, hse->specificities->default[0]->hidden);
    unit_assert_operator_equal(Classification::ChemicalDerivative, hse->specificities->default[0]->classification);


    Modification^ oxidation = unimod::modification(CVID::UNIMOD_Oxidation);

    // 3 mods have the same mass as oxidation
    unit_assert_operator_equal(3, unimod::modifications(filter(oxidation->deltaMonoisotopicMass, 0,
                                                               true, indeterminate))->Count);
    unit_assert_operator_equal(3, unimod::modifications(filter(oxidation->deltaAverageMass, 0,
                                                               false, indeterminate))->Count);

    // only one of those mods happen on Methionine
    unit_assert_operator_equal(1, unimod::modifications(filter(oxidation->deltaMonoisotopicMass, 0, true, indeterminate, Site::Methionine, Position::Anywhere))->Count);
    unit_assert_operator_equal(1, unimod::modifications(filter(oxidation->deltaAverageMass, 0, false, indeterminate, Site::Methionine, Position::Anywhere))->Count);

    // oxidation also happens on Proline (test multi-bit Site mask)
    unit_assert_operator_equal(1, unimod::modifications(filter(oxidation->deltaAverageMass, 0, false, indeterminate, Site::Methionine | Site::Proline, Position::Anywhere))->Count);

    // add Alanine as a site and it could be a substitution
    unit_assert_operator_equal(2, unimod::modifications(filter(oxidation->deltaAverageMass, 0, false, indeterminate, Site::Methionine | Site::Alanine, Position::Anywhere))->Count);


    // 19 mods are 28 +/- 1
    unit_assert_operator_equal(19, unimod::modifications(filter(28, 1, true, indeterminate))->Count);

    // only two of those mods happen post-translationally on protein N-termini
    unit_assert_operator_equal(2, unimod::modifications(filter(28, 1, true, indeterminate, Site::Any,
                                                               Position::ProteinNTerminus,
                                                               Classification::PostTranslational,
                                                               indeterminate))->Count);


    Modification^ phospho = unimod::modification(CVID::UNIMOD_Phospho);

    // phospho on S and T are grouped (property names are duplicated)
    unit_assert_operator_equal(CVID::UNIMOD_Phospho, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0, true, true, Site::Serine, Position::Anywhere))->default[0]->cvid);
    unit_assert_operator_equal(CVID::UNIMOD_Phospho, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0, true, true, Site::Threonine, Position::Anywhere))->default[0]->cvid);
    unit_assert_operator_equal(CVID::UNIMOD_Phospho, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0, true, true, Site::Tyrosine, Position::Anywhere))->default[0]->cvid);

    // test multi-bit Site mask
    unit_assert_operator_equal(CVID::UNIMOD_Phospho, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0, true, true, Site::Serine | Site::Tyrosine, Position::Anywhere))->default[0]->cvid);

    // there are no unapproved mods at phospho's mass
    unit_assert_operator_equal(0, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0, true, false))->Count);

    // phospho and sulfo are only distinguishable with PPM mass accuracy
    double mass_2000Da_1ppm = 2000 - (2000 - MZTolerance(1, MZTolerance::PPM));
    unit_assert_operator_equal(2, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0.5, true, true, Site::Serine, Position::Anywhere))->Count);
    unit_assert_operator_equal(1, unimod::modifications(filter(phospho->deltaMonoisotopicMass, mass_2000Da_1ppm, true, true, Site::Serine, Position::Anywhere))->Count);

    // test indeterminate and average mass
    unit_assert_operator_equal(2, unimod::modifications(filter(phospho->deltaMonoisotopicMass, 0.1, indeterminate, true, Site::Serine, Position::Anywhere))->Count);
    unit_assert_operator_equal(2, unimod::modifications(filter(phospho->deltaAverageMass, 0.1, indeterminate, true, Site::Serine, Position::Anywhere))->Count);
    unit_assert_operator_equal(2, unimod::modifications(filter(phospho->deltaAverageMass, 0.1, false, true, Site::Serine, Position::Anywhere))->Count);

    // test negative mass
    unit_assert_operator_equal(CVID::UNIMOD_Gln__pyro_Glu, unimod::modifications(filter(-17.0265,
                                                                                        mass_2000Da_1ppm,
                                                                                        true, true,
                                                                                        Site::Glutamine,
                                                                                        Position::AnyNTerminus))->default[0]->cvid);


    // at 14.5 +/- 0.5 there are 3 approved mods and 9 unapproved
    unit_assert_operator_equal(3, unimod::modifications(filter(14.5, 0.5, true, true))->Count);
    unit_assert_operator_equal(9, unimod::modifications(filter(14.5, 0.5, true, false))->Count);

    // all 9 unapproved mods are hidden
    unit_assert_operator_equal(0, unimod::modifications(filter(14.5, 0.5, true, false, Site::Any, Position::Anywhere, Classification::Any, false))->Count);

    // test ambiguous residue; this mod could be a Q->P substitution
    unit_assert_operator_equal(1, unimod::modifications(filter(-31, 0.01, true, indeterminate, unimod::site('Z'), Position::Anywhere))->Count);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
