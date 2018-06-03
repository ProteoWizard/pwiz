//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "../common/unit.hpp"
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::chemistry;
using namespace System::Collections::Generic;
using System::String;
using System::Console;


ostream* os_ = 0;


void testMassAbundance()
{
    MassAbundance ma(1, 2);
    MassAbundance ma2(1, 4);
    unit_assert(%ma != %ma2);
    ma2.abundance = 2;
    unit_assert(%ma == %ma2);
}


struct TestFormula
{
    const char* formula;
    int numC, numH, numN, numO, numS, num13C, num2H, num15N, num18O;
    double monoMass;
    double avgMass;
};

const TestFormula testFormulaData[] =
{
    { "C1H2N3O4S5", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "CH2N3O4S5", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "H2N3O4S5C", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "C H2N3O4S5", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "H2N3O4S5C ", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "H2N3O4S5 C ", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "C1H 2N3O4 S5", 1, 2, 3, 4, 5, 0, 0, 0, 0, 279.864884, 280.36928 },
    { "H-42", 0, -42, 0, 0, 0, 0, 0, 0, 0, -42.328651, -42.333512 },
    { "N2C-1", -1, 0, 2, 0, 0, 0, 0, 0, 0, 28.006148 - 12, 28.013486 - 12.0107 },
    { "C39H67N11O10", 39, 67, 11, 10, 0, 0, 0, 0, 0, 849.507238, 850.01698 },
    { "C3H7N1O2Se1", 3, 7, 1, 2, 0, 0, 0, 0, 0, 168.9642, 168.0532 },
    { "_13C1_2H2_15N3_18O4", 0, 0, 0, 0, 0, 1, 2, 3, 4, 134.0285, 134.0285 },
    { "_13C1D2_15N3_18O4", 0, 0, 0, 0, 0, 1, 2, 3, 4, 134.0285, 134.0285 },
    { "_13C1_3H2_15N3_18O4", 0, 0, 0, 0, 0, 1, 2, 3, 4, 136.0324, 136.0324 },
    { "_13C_3H2_15N3_18O4", 0, 0, 0, 0, 0, 1, 2, 3, 4, 136.0324, 136.0324 },
    { "_3H2_15N3_18O4_13C", 0, 0, 0, 0, 0, 1, 2, 3, 4, 136.0324, 136.0324 },
    { "_13C1T2_15N3_18O4", 0, 0, 0, 0, 0, 1, 2, 3, 4, 136.0324, 136.0324 },
    { "C-1 _13C1 _2H2 H-2 N-3 _15N3 _18O4 O-4", -1, -2, -3, -4, 0, 1, 2, 3, 4, 14.024, 13.984 },
    { "C-1_13C1_2H2H-2N-3_15N3_18O4O-4", -1, -2, -3, -4, 0, 1, 2, 3, 4, 14.024, 13.984 },
};

const int testFormulaDataSize = sizeof(testFormulaData)/sizeof(TestFormula);

void testFormula()
{
    for (int i=0; i < testFormulaDataSize; ++i)
    {
        const TestFormula& testFormula = testFormulaData[i];
        Formula^ formula = gcnew Formula(gcnew String(testFormula.formula));

        const double EPSILON = 0.001;

        unit_assert_equal(formula->monoisotopicMass(), testFormula.monoMass, EPSILON);
        unit_assert_equal(formula->molecularWeight(), testFormula.avgMass, EPSILON);
        if (os_) Console::WriteLine(String::Format("formula: {0} {1} {2}", formula, formula->monoisotopicMass(), formula->molecularWeight()));

        formula->Item[Element::C] += 2;
        unit_assert_equal(formula->monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula->molecularWeight(), testFormula.avgMass+12.0107*2, EPSILON);
        if (os_) Console::WriteLine(String::Format("formula: {0} {1} {2}", formula, formula->monoisotopicMass(), formula->molecularWeight()));

        // test copy constructor
        Formula^ formula2 = gcnew Formula(formula);
        formula2->Item[Element::C] -= 2;
        unit_assert_equal(formula->monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula2->monoisotopicMass(), testFormula.monoMass, EPSILON);
        if (os_) Console::WriteLine(String::Format("formula: {0}", formula));
        if (os_) Console::WriteLine(String::Format("formula2: {0}", formula2));

        formula = gcnew Formula(formula2);
        unit_assert_equal(formula->monoisotopicMass(), formula2->monoisotopicMass(), EPSILON);
        if (os_) Console::WriteLine(String::Format("formula: {0}", formula));
        if (os_) Console::WriteLine(String::Format("formula2: {0}", formula2));

        // test operator==
        unit_assert(formula == formula2);
        formula2->Item[Element::C] += 4; // test difference in CHONSP
        unit_assert(formula != formula2);
        formula2->Item[Element::C] -= 4;
        unit_assert(formula == formula2);
        formula2->Item[Element::U] += 2; // test difference outside CHONSP
        unit_assert(formula != formula2);
        formula2->Item[Element::U] -= 2;
        unit_assert(formula == formula2);

        // test data()
        IDictionary<Element, int>^ data = formula->data();
        if (os_)
        {
            *os_ << "map: "; 
            for each(KeyValuePair<Element, int>^ it in data)
                *os_ << ToStdString(it->Key.ToString()) << it->Value << " ";
            *os_ << "\n";
        }
    }
}


void testFormulaOperations()
{
    Formula^ water = gcnew Formula("H2O1");
    Formula^ a = gcnew Formula("C1 H2 N3 O4 S5");

    a *= 2;
    unit_assert(a->Item[Element::C]==2 && a->Item[Element::H]==4 && a->Item[Element::N]==6 && a->Item[Element::O]==8 && a->Item[Element::S]==10);
    a += water;
    unit_assert(a->Item[Element::H]==6 && a->Item[Element::O]==9);
    a -= water;
    unit_assert(a->Item[Element::C]==2 && a->Item[Element::H]==4 && a->Item[Element::N]==6 && a->Item[Element::O]==8 && a->Item[Element::S]==10);
    a += 2*water;
    unit_assert(a->Item[Element::H]==8 && a->Item[Element::O]==10);
    a = (a - water*2);
    unit_assert(a->Item[Element::C]==2 && a->Item[Element::H]==4 && a->Item[Element::N]==6 && a->Item[Element::O]==8 && a->Item[Element::S]==10);
    a = water + water;
    unit_assert(a->Item[Element::H]==4 && a->Item[Element::O]==2);
    if (os_) Console::WriteLine(String::Format("water: {0}", a-water));
}


const double epsilon_ = 1e-14;


void testMZ()
{
    double x = 1000;
    MZTolerance tolerance(.1);

    x = x + %tolerance;
    unit_assert_equal(x, 1000.1, epsilon_);

    x = x - %tolerance;
    unit_assert_equal(x, 1000, epsilon_);

    unit_assert_equal(x+%tolerance, 1000.1, epsilon_);
    unit_assert_equal(x-%tolerance, 999.9, epsilon_);
}


void testPPM()
{
    double x = 1000;
    MZTolerance tolerance(5, MZTolerance::Units::PPM);

    x = x + %tolerance;
    unit_assert_equal(x, 1000.005, epsilon_);

    x = x - %tolerance;
    const double delta = 1000.005 * 5e-6; // a little more than .005
    unit_assert_equal(x, 1000.005 - delta, epsilon_);

    unit_assert_equal(1000+%tolerance, 1000.005, epsilon_);
    unit_assert_equal(1000-%tolerance, 999.995, epsilon_);

    unit_assert_equal(-1000+%tolerance, -999.995, epsilon_);
    unit_assert_equal(-1000-%tolerance, -1000.005, epsilon_);
}


void testIO()
{
    if (os_) Console::WriteLine("testIO()");

    MZTolerance temp;
    if (os_) Console::WriteLine("temp: {0}", %temp);

    MZTolerance fiveppm(5, MZTolerance::Units::PPM);
    MZTolerance blackbirds(4.20, MZTolerance::Units::MZ);
    if (os_) Console::WriteLine("fiveppm: {0}", %fiveppm);
    if (os_) Console::WriteLine("blackbirds: {0}", %blackbirds);

    {
        MZTolerance temp(fiveppm.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp);
        unit_assert(%temp == %fiveppm);
        unit_assert(%temp != %blackbirds);
    }

    {
        MZTolerance temp(fiveppm.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp);
        unit_assert(%temp == %fiveppm);
    }

    {
        MZTolerance temp(fiveppm.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp);
        unit_assert(%temp == %fiveppm);
    }

    {
        MZTolerance temp(blackbirds.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp);
        unit_assert(%temp == %blackbirds);
        unit_assert(%temp != %fiveppm);
    }

    {
        MZTolerance temp(blackbirds.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp); 
        unit_assert(%temp == %blackbirds);
    }

    {
        MZTolerance temp(blackbirds.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp); 
        unit_assert(%temp == %blackbirds);
    }

    {
        MZTolerance temp(blackbirds.ToString());
        if (os_) Console::WriteLine("temp: {0}", %temp); 
        unit_assert(%temp == %blackbirds);
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "ChemistryTest\n";
        testMassAbundance();
        testFormula();
        testFormulaOperations();
        testMZ();
        testPPM();
        testIO();
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

