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
using boost::shared_ptr;


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


void testPredicatePtr()
{
    if (os_) *os_ << "testPredicatePtr()\n";

    shared_ptr<Peakel> a(new Peakel);
    a->mz = 1.0;
    a->retentionTime = 1.0;

    shared_ptr<Peakel> b(new Peakel);
    b->mz = 2.0;
    b->retentionTime = 1.0;

    shared_ptr<Peakel> c(new Peakel);
    c->mz = 1.0;
    c->retentionTime = 2.0;

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

    shared_ptr<Peakel> a(new Peakel); // TODO: typedef PeakelPtr
    a->mz = 1.0;
    a->retentionTime = 1.0;

    shared_ptr<Peakel> b(new Peakel);
    b->mz = 2.0;
    b->retentionTime = 1.0;

    shared_ptr<Peakel> c(new Peakel);
    c->mz = 1.0;
    c->retentionTime = 2.0;

    PeakelField pf;

    pf.insert(a);
    pf.insert(b);
    pf.insert(c);

    unit_assert(pf.size() == 3);

    PeakelField::const_iterator it = pf.begin();
    (*it)->peaks.push_back(Peak()); // we can modify **it, even though *it is const
    if (os_) *os_ << **it << endl;
    unit_assert(*it == a);

    ++it;
    if (os_) *os_ << **it << endl;
    unit_assert(*it == c);

    ++it;
    if (os_) *os_ << **it << endl;
    unit_assert(*it == b);
}

void test()
{
    testPredicate();
    testPredicatePtr();
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

