//
// ChemistryTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "Chemistry.hpp"
#include "Ion.hpp"
#include "util/unit.hpp"
#include "math/round.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void testMassAbundance()
{
    using namespace Chemistry;
    MassAbundance ma(1, 2);
    MassAbundance ma2(1, 4);
    unit_assert(ma != ma2);
    ma2.abundance = 2;
    unit_assert(ma == ma2);
}


void testFormula()
{
    using namespace Chemistry;

    Formula formula("C1H 2N3O4 S5");
    unit_assert(formula[Element::C] == 1);
    unit_assert(formula[Element::H] == 2);
    unit_assert(formula[Element::N] == 3);
    unit_assert(formula[Element::O] == 4);
    unit_assert(formula[Element::S] == 5);

    unit_assert(round(formula.monoisotopicMass()) == 280);
    unit_assert((int)formula.molecularWeight() == 280);
    if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;

    formula[Element::C] = 6;
    unit_assert(round(formula.monoisotopicMass()) == 340);
    unit_assert((int)formula.molecularWeight() == 340);
    if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;

    //const Formula& constFormula = formula;
    //constFormula[Element::C] = 1; // this won't compile, by design 

    // test copy constructor and operator!=
    Formula formula2 = formula;
    formula2[Element::C] = 1;
    unit_assert(round(formula.monoisotopicMass()) == 340);
    unit_assert(round(formula2.monoisotopicMass()) == 280);
    if (os_) *os_ << "formula: " << formula << endl;
    if (os_) *os_ << "formula2: " << formula2 << endl;

    // test operator= and operator==
    formula2 = formula;
    unit_assert(round(formula.monoisotopicMass()) == 340);
    unit_assert(round(formula2.monoisotopicMass()) == 340);
    if (os_) *os_ << "formula: " << formula << endl;
    if (os_) *os_ << "formula2: " << formula2 << endl;

    // test data()

    Formula::Map data = formula.data();
    if (os_) *os_ << "map: "; 
    for (Formula::Map::iterator it=data.begin(), end=data.end(); it!=end; ++it)
        if (os_) *os_ << it->first << it->second << " ";

    if (os_) *os_ << "\n";
}


void testFormulaOperations()
{
    using namespace Chemistry; 
    using namespace Element; 

    Formula water("H2O1");
    Formula a("C1 H2 N3 O4 S5");

    a *= 2;
    unit_assert(a[C]==2 && a[H]==4 && a[N]==6 && a[O]==8 && a[S]==10);
    a += water;
    unit_assert(a[H]==6 && a[O]==9);
    a -= water;
    unit_assert(a[C]==2 && a[H]==4 && a[N]==6 && a[O]==8 && a[S]==10);
    a += 2*water;
    unit_assert(a[H]==8 && a[O]==10);
    a = (a - water*2);
    unit_assert(a[C]==2 && a[H]==4 && a[N]==6 && a[O]==8 && a[S]==10);
    a = water + water;
    unit_assert(a[H]==4 && a[O]==2);
    if (os_) *os_ << "water: " << a-water << endl;
}


void testInfo()
{
    using namespace Chemistry;
    Element::Info info;

    if (os_)
    {
        for (Element::Type e=Element::H; e<=Element::Ca; e=(Element::Type)(e+1))
            *os_ << info[e].symbol << " " << info[e].atomicNumber << endl;
        *os_ << endl;
    }
}


void infoExample()
{
    using namespace Chemistry;
    using namespace Element;

    Info info;
   
    if (os_)
    {
        *os_ << "Sulfur isotopes: " << info[S].isotopes.size() << endl
             << info[S].isotopes;
    }
}


void testPolysiloxane()
{
    using namespace Chemistry;

    Formula formula("Si6C12H36O6");

    if (os_) 
    {
        *os_ << "polysiloxane:\n"
             << formula << " " 
             << formula.monoisotopicMass() << " " 
             << formula.molecularWeight() << endl
             << "ion: " << Ion::mz(formula.monoisotopicMass(), 1) << endl;
    }
}


int main(int argc, char* argv[])
{

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "ChemistryTest\n" << setprecision(12);
        testMassAbundance();
        testFormula();
        testFormulaOperations();
        testInfo();
        infoExample();
        testPolysiloxane();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


