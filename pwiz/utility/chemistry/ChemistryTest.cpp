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


#include "pwiz/utility/misc/unit.hpp"
#include "Chemistry.hpp"
#include "Ion.hpp"
#include "pwiz/utility/math/round.hpp"
#include <cstring>
#include "pwiz/utility/misc/Std.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"


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
        Formula formula(testFormula.formula);

        const double EPSILON = 0.001;

        if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;
        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass, EPSILON);
        unit_assert_equal(formula.molecularWeight(), testFormula.avgMass, EPSILON);

        formula[Element::C] += 2;
        if (os_) *os_ << formula << " " << formula.monoisotopicMass() << " " << formula.molecularWeight() << endl;
        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula.molecularWeight(), testFormula.avgMass+12.0107*2, EPSILON);

        //const Formula& constFormula = formula;
        //constFormula[Element::C] = 1; // this won't compile, by design 

        // test copy constructor
        Formula formula2 = formula;
        formula2[Element::C] -= 2;
        if (os_) *os_ << "formula: " << formula << endl;
        if (os_) *os_ << "formula2: " << formula2 << endl;
        unit_assert_equal(formula.monoisotopicMass(), testFormula.monoMass+24, EPSILON);
        unit_assert_equal(formula2.monoisotopicMass(), testFormula.monoMass, EPSILON);

        // test operator=
        formula = formula2;
        if (os_) *os_ << "formula: " << formula << endl;
        if (os_) *os_ << "formula2: " << formula2 << endl;
        unit_assert_equal(formula.monoisotopicMass(), formula2.monoisotopicMass(), EPSILON);

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

    unit_assert(Element::Info::record(Element::C).atomicNumber == 6);
    unit_assert(Element::Info::record("C").atomicNumber == 6);
    unit_assert(Element::Info::record(Element::U).atomicNumber == 92);
    unit_assert(Element::Info::record("U").atomicNumber == 92);
    unit_assert(Element::Info::record(Element::Uuh).atomicNumber == 116);
    unit_assert(Element::Info::record("Uuh").atomicNumber == 116);

    unit_assert_throws_what(Element::Info::record("foo"),
                            runtime_error,
                            "[chemistry::text2enum()] Error translating symbol foo");
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


void testThreadSafetyWorker(boost::barrier* testBarrier, int& result)
{
    testBarrier->wait(); // wait until all threads have started

    try
    {
        testMassAbundance();
        testFormula();
        testFormulaOperations();
        testInfo();
        infoExample();
        testPolysiloxane();
        result = 0;
        return;
    }
    catch (exception& e)
    {
        cerr << "Exception in worker thread: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unhandled exception in worker thread." << endl;
    }
    result = 1;
}

void testThreadSafety(const int& testThreadCount)
{
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    vector<int> results(testThreadCount, 0);
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, &testBarrier, boost::ref(results[i])));
    testThreadGroup.join_all();

    int failedThreads = std::accumulate(results.begin(), results.end(), 0);
    if (failedThreads > 0)
        throw runtime_error(lexical_cast<string>(failedThreads) + " thread failed");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "ChemistryTest\n" << setprecision(12);

    try
    {
        //testThreadSafety(1); // does not test thread-safety of singleton initialization
        testThreadSafety(2);
        testThreadSafety(4);
        testThreadSafety(8);
        testThreadSafety(16);
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


