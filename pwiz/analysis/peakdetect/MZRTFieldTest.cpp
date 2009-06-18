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


#include "MZRTField.hpp"
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

    PeakelPtr a(new Peakel(Peak(1.0, 1.0)));
    PeakelPtr b(new Peakel(Peak(2.0, 1.0)));
    PeakelPtr c(new Peakel(Peak(1.0, 2.0)));

    LessThan_MZRT<Peakel> lt;

    // a < c < b

    unit_assert(lt(*a,*b));
    unit_assert(lt(a,b));
    unit_assert(!lt(b,a));
    unit_assert(lt(a,c));
    unit_assert(!lt(c,a));
    unit_assert(lt(c,b));
    unit_assert(!lt(b,c));

    unit_assert(!lt(a,a));
}


void testPredicate_Feature()
{
    Feature a,b;
    a.mz = 1;
    b.mz = 2;

    LessThan_MZRT<Feature> lt;
    unit_assert(lt(a,b));
    unit_assert(!lt(b,a));
    unit_assert(!lt(a,a));
}


struct Goober
{
    // minimum requirements to instantiate MZRTField<>
    double mz;
    double retentionTime;
    double retentionTimeMin();
    double retentionTimeMax();
};


void testConceptChecking()
{
    MZRTField<Goober> gooberField;
}


void testPeakelField()
{
    if (os_) *os_ << "testPeakelField()\n";

    PeakelPtr a(new Peakel(Peak(1.0, 1.0)));
    PeakelPtr b(new Peakel(Peak(2.0, 1.0)));
    PeakelPtr c(new Peakel(Peak(1.0, 2.0)));

    PeakelField pf;

    pf.insert(a);
    pf.insert(b);
    pf.insert(c);

    unit_assert(pf.size() == 3);

    PeakelField::const_iterator it = pf.begin();
    if (os_) *os_ << **it << endl;
    unit_assert(*it == a);

    // note that std::set allows only const access
    // however, we can modify **it
    (*it)->peaks.push_back(Peak()); 
    (*it)->peaks.clear();

    ++it;
    if (os_) *os_ << **it << endl;
    unit_assert(*it == c);

    ++it;
    if (os_) *os_ << **it << endl;
    unit_assert(*it == b);

    // find()

    vector<PeakelPtr> v = pf.find(1.5, .6, 1, .5);

    if (os_) 
    {
        *os_ << "find(): " << v.size() << endl;
        for (vector<PeakelPtr>::const_iterator it=v.begin(); it!=v.end(); ++it)
            *os_ << **it << endl;
    }

    unit_assert(v.size()==2 && v[0]==a && v[1]==b);
    v = pf.find(1.5, .4, 1, .5);
    unit_assert(v.empty());
    v = pf.find(2, .1, 1, .1);
    unit_assert(v.size()==1 && v[0]==b);

    MZTolerance fiveppm(5, MZTolerance::PPM);
    v = pf.find(1.000001, fiveppm, 1, 10);
    unit_assert(v.size()==2 && v[0]==a && v[1]==c);
    v = pf.find(1.000006, fiveppm, 1, 10);
    unit_assert(v.empty());
}


void testFeatureField()
{
    if (os_) *os_ << "testPeakelField()\n";

    //MZRTField<Feature> pf; // TODO: this will compile when retentionTimeMin/Max() added to Feature
}


void test()
{
    testPredicate();
    testPredicate_Feature();
    testConceptChecking();
    testPeakelField();
    testFeatureField();
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

