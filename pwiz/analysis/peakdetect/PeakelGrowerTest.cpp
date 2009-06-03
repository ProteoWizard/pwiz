//
// PeakelGrowerTest.cpp
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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


#include "PeakelGrower.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


void testPredicate()
{
    if (os_) *os_ << "testPredicate()\n";

    Peakel a;
    a.mz = 1.0;
    a.retentionTime = 1.0;

    Peakel b;
    b.mz = 2.0;
    b.retentionTime = 1.0;

    Peakel c;
    c.mz = 1.0;
    c.retentionTime = 2.0;

    LessThan_MZRT lt;

    // a < c < b

    unit_assert(lt(a,b));
    unit_assert(!lt(b,a));
    unit_assert(lt(a,c));
    unit_assert(!lt(c,a));
    unit_assert(lt(c,b));
    unit_assert(!lt(b,c));

    unit_assert(!lt(a,a));
}


void testPeakelField()
{
    if (os_) *os_ << "testPeakelField()\n";

    Peakel a; //TODO: implement default constructor properly (does nothing now)
    a.mz = 1.0;
    a.retentionTime = 1.0;

    Peakel b;
    b.mz = 2.0;
    b.retentionTime = 1.0;

    Peakel c;
    c.mz = 1.0;
    c.retentionTime = 2.0;

    PeakelField pf;

    pf.insert(a);
    pf.insert(b);
    pf.insert(c);

    unit_assert(pf.size() == 3);
    PeakelField::const_iterator it = pf.begin();
    if (os_) *os_ << *it << endl;

    vector<Peak>& v = const_cast<vector<Peak>&>(it->peaks); // access via const_cast
    // maybe make PeakelField an actual class
    //  probably not -- handle the cast internally during sow()


    const double epsilon = numeric_limits<double>::epsilon();

    // TODO: fuzzy == for Peak/Peakel? 
    unit_assert_equal(it->mz, a.mz, epsilon);
    unit_assert_equal(it->retentionTime, a.retentionTime, epsilon);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert_equal(it->mz, c.mz, epsilon);
    unit_assert_equal(it->retentionTime, c.retentionTime, epsilon);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert_equal(it->mz, b.mz, epsilon);
    unit_assert_equal(it->retentionTime, b.retentionTime, epsilon);
}


void test()
{
    testPredicate();
    testPeakelField();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

