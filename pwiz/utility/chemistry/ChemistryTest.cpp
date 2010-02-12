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


#include "Chemistry.hpp"
#include "Ion.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/math/round.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>
#include <iterator>
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"


using namespace std;
using namespace pwiz::util;
using namespace pwiz::chemistry;


ostream* os_ = 0;


void testMassAbundance()
{
    MassAbundance ma(1, 2);
    MassAbundance ma2(1, 4);
    unit_assert(ma != ma2);
    ma2.abundance = 2;
    unit_assert(ma == ma2);
}


struct TestFormula
{
    const char* formula;
    int numC, numH, numN, numO, numS;
    double monoMass;
    double avgMass;
};

const TestFormula testFormulaData[] =
{
    { "C1H2N3O4S5", 1, 2, 3, 4, 5, 279.864884, 280.36928 },
    { "C1H 2N3O4 S5", 1, 2, 3, 4, 5, 279.864884, 280.36928 },
    { "H-42", 0, -42, 0, 0, 0, -42.328651, -42.333512 },
    { "N2C-1", -1, 0, 2, 0, 0, 28.006148-12, 28.013486-12.0107 },
    { "C39H67N11O10", 39, 67, 11, 10, 0, 849.507238, 850.01698 },
    { "C3H7N1O2Se1", 3, 7, 1, 2, 0, 168.9642, 168.0532 }
};

const int testFormulaDataSize = sizeof(testFormulaData)/sizeof(TestFormula);

void testFormula()
{
    for (int i=0; i < testFormulaDataSize; ++i)
    {
        const TestFormula& testFormula = testFormulaData[i];
        Formula formula(testFormula.formula);

        const double EPSILON = 0.001;

        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass, EPSILON);
        unit_assert_equal(formula.molecularWeight(), testFormula.avgMass, EPSILON);
        if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;

        formula[Element::C] += 2;
        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula.molecularWeight(), testFormula.avgMass+12.0107*2, EPSILON);
        if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;

        //const Formula& constFormula = formula;
        //constFormula[Element::C] = 1; // this won't compile, by design 

        // test copy constructor
        Formula formula2 = formula;
        formula2[Element::C] -= 2;
        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula2.monoisotopicMass(), testFormula.monoMass, EPSILON);
        if (os_) *os_ << "formula: " << formula << endl;
        if (os_) *os_ << "formula2: " << formula2 << endl;

        // test operator=
        formula = formula2;
        unit_assert_equal(formula.monoisotopicMass(), formula2.monoisotopicMass(), EPSILON);
        if (os_) *os_ << "formula: " << formula << endl;
        if (os_) *os_ << "formula2: " << formula2 << endl;

        // test operator==
        unit_assert(formula == testFormula.formula); // implicit construction from string
        unit_assert(formula == formula2);
        formula2[Element::C] += 4; // test difference in CHONSP
        unit_assert(formula != formula2);
        formula2[Element::C] -= 4;
        unit_assert(formula == formula2);
        formula2[Element::U] += 2; // test difference outside CHONSP
        unit_assert(formula != formula2);
        formula2[Element::U] -= 2;
        unit_assert(formula == formula2);

        // test data()
        Formula::Map data = formula.data();
        if (os_)
        {
            *os_ << "map: "; 
            for (Formula::Map::iterator it=data.begin(), end=data.end(); it!=end; ++it)
                *os_ << it->first << it->second << " ";
            *os_ << "\n";
        }
    }
}


void testFormulaOperations()
{
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
    if (os_)
    {
        for (Element::Type e=Element::H; e<=Element::Ca; e=(Element::Type)(e+1))
            *os_ << Element::Info::record(e).symbol << " " << Element::Info::record(e).atomicNumber << endl;
        *os_ << endl;
    }
}


void infoExample()
{
    if (os_)
    {
        *os_ << "Sulfur isotopes: " << Element::Info::record(Element::S).isotopes.size() << endl
             << Element::Info::record(Element::S).isotopes;
    }
}


void testPolysiloxane()
{
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


void testThreadSafetyWorker(boost::barrier* testBarrier)
{
    testBarrier->wait(); // wait until all threads have started

    unit_assert_equal(Element::Info::record(Element::C).atomicNumber, 6.0, 0);

    testFormula();
    testFormulaOperations();
}

void testThreadSafety()
{
    const int testThreadCount = 100;
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, &testBarrier));
    testThreadGroup.join_all();
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
        testThreadSafety();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


