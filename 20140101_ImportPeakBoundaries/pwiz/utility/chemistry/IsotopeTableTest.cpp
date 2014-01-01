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


#include "IsotopeTable.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::chemistry;


ostream* os_ = 0;


bool hasGreaterAbundance(const MassAbundance& a, const MassAbundance& b)
{
    return a.abundance > b.abundance;
}


bool hasLessMass(const MassAbundance& a, const MassAbundance& b)
{
    return a.mass < b.mass;
}


void test1()
{
    MassDistribution md;
    md.push_back(MassAbundance(10, 1));

    IsotopeTable table(md, 10, 0);

    for (int i=1; i<=10; i++)
    {
        MassDistribution temp = table.distribution(i);
        unit_assert(temp.size() == 1);
        unit_assert(temp[0] == MassAbundance(i*10, 1));
        //cout << i << " atoms:\n" << temp << endl;
    }
}


void test2()
{
    const double p0 = .9;
    const double p1 = 1 - p0;

    MassDistribution md;
    md.push_back(MassAbundance(10, p0));
    md.push_back(MassAbundance(11, p1));

    IsotopeTable table(md, 10, 0);

/*
    for (int i=0; i<=10; i++)
        cout << i << " atoms:\n" << table.distribution(i) << endl;
*/

    // test manually for 1 atom

    MassDistribution test1 = table.distribution(1);
    unit_assert(test1.size() == 2);
    unit_assert(test1[0] == md[0]); 
    unit_assert(test1[1] == md[1]); 

    // test manually for 10 atoms
    
    const int n = 10;
    MassDistribution good10; 
    double abundance = pow(p0, n);
    double mass = 100;

    for (int k=0; k<=n; k++)
    {
        good10.push_back(MassAbundance(mass, abundance));
        abundance *= p1/p0*(n-k)/(k+1);
        mass += 1;
    }

    sort(good10.begin(), good10.end(), hasGreaterAbundance);

    MassDistribution test10 = table.distribution(10);
    sort(test10.begin(), test10.end(), hasGreaterAbundance);

    unit_assert((int)test10.size() == n+1); 

    for (int k=0; k<=n; k++)
        unit_assert_equal(test10[k].abundance, good10[k].abundance, 1e-15);

    // test cutoff
    
    IsotopeTable table_cutoff(md, 10, 1e-8);
    unit_assert(table_cutoff.distribution(10).size() == 9);
}


void compare(const MassDistribution& test, const MassDistribution& good)
{
    unit_assert(test.size() == good.size()); 
    for (unsigned int i=0; i<test.size(); i++)
    {
        unit_assert_equal(test[i].mass, good[i].mass, 1e-12);
        unit_assert_equal(test[i].abundance, good[i].abundance, 1e-12);
    }
}


void test3()
{
    const double p0 = .9;
    const double p1 = .09;
    const double p2 = 1 - (p0 + p1);

    const double m0 = 10;
    const double m1 = 11;
    const double m2 = 12.33;

    MassDistribution md;
    md.push_back(MassAbundance(m0, p0));
    md.push_back(MassAbundance(m1, p1));
    md.push_back(MassAbundance(m2, p2));

//    cout << "test3 distribution:\n" << md << endl;

    IsotopeTable table(md, 10, 1e-5);

    // compare distribution for 1 atom
    compare(table.distribution(1), md);

    // compare distribution for 2 atoms
    MassDistribution good3_2;
    good3_2.push_back(MassAbundance(m0*2, p0*p0));
    good3_2.push_back(MassAbundance(m0+m1, p0*p1*2));
    good3_2.push_back(MassAbundance(m0+m2, p0*p2*2));
    good3_2.push_back(MassAbundance(m1+m2, p1*p2*2));
    good3_2.push_back(MassAbundance(m1+m1, p1*p1));
    good3_2.push_back(MassAbundance(m2+m2, p2*p2));
    sort(good3_2.begin(), good3_2.end(), hasGreaterAbundance);

    MassDistribution test3_2 = table.distribution(2);
    sort(test3_2.begin(), test3_2.end(), hasGreaterAbundance);

//    cout << "good:\n" << good3_2 << endl;
//    cout << "test:\n" << test3_2 << endl;

    compare(test3_2, good3_2);
}


void test4()
{
    const double p0 = .9;
    const double p1 = .09;
    const double p2 = .009;
    const double p3 = 1 - (p0 + p1 + p2);

    const double m0 = 10;
    const double m1 = 11;
    const double m2 = 12.33;
    const double m3 = 13.13;

    MassDistribution md;
    md.push_back(MassAbundance(m0, p0));
    md.push_back(MassAbundance(m1, p1));
    md.push_back(MassAbundance(m2, p2));
    md.push_back(MassAbundance(m3, p3));

    cout << "test4 distribution:\n" << md << endl;

    IsotopeTable table(md, 10, 1e-5);

    compare(md, table.distribution(1));

    MassDistribution test4_2 = table.distribution(2);

    cout << "2 atoms:\n" << test4_2 << endl;
}


void testSulfur()
{
    IsotopeTable table(Element::Info::record(Element::Se).isotopes, 10, 1e-10);
   
    cout << table << endl; 

    MassDistribution dist10 = table.distribution(10);
    cout << "distribution: " << dist10 << endl;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "IsotopeTableTest\n" << setprecision(12);
        test1();
        test2();
        test3();
        //test4();
        //testSulfur();
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

