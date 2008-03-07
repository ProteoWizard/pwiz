//
// IntegerSetTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "IntegerSet.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <vector>
#include <iterator>


using namespace std;
using namespace pwiz::util;


ostream* os_ = 0;


void test()
{
    // instantiate IntegerSet

    IntegerSet a;
    unit_assert(a.empty());

    a.insert(1);
    unit_assert(!a.empty());

    a.insert(2);
    a.insert(IntegerSet::Interval(0,2));
    a.insert(0,2);
    a.insert(4);

    // verify virtual container contents: 0, 1, 2, 4

    if (os_)
    {            
        copy(a.begin(), a.end(), ostream_iterator<int>(*os_," ")); 
        *os_ << endl;
    }

    vector<int> b; 
    copy(a.begin(), a.end(), back_inserter(b));

    unit_assert(b.size() == 4);
    unit_assert(b[0] == 0);
    unit_assert(b[1] == 1);
    unit_assert(b[2] == 2);
    unit_assert(b[3] == 4);

    // insert [2,4], and verify contents: 0, 1, 2, 3, 4

    a.insert(2,4);

    if (os_)
    {            
        copy(a.begin(), a.end(), ostream_iterator<int>(*os_," ")); 
        *os_ << endl;
    }

    b.clear();
    copy(a.begin(), a.end(), back_inserter(b));

    unit_assert(b.size() == 5);
    for (int i=0; i<5; i++)
        unit_assert(i == b[i]);
}


void testInstantiation()
{
    IntegerSet a(666);
    vector<int> b;
    copy(a.begin(), a.end(), back_inserter(b));
    unit_assert(b.size() == 1);
    unit_assert(b[0] == 666);

    IntegerSet c(666,668);
    vector<int> d;
    copy(c.begin(), c.end(), back_inserter(d));
    unit_assert(d.size() == 3);
    unit_assert(d[0] == 666);
    unit_assert(d[1] == 667);
    unit_assert(d[2] == 668);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testInstantiation();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


