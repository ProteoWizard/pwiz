//
// $Id$
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
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


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
    // minimum requirements to instantiate MZRTField<>;
    // removing any of these will produce compiler error via Boost concept checking
    // with the struct HasMZRT

    double mz;
    double retentionTime;
    double retentionTimeMin() const;
    double retentionTimeMax() const;
};


void testConceptChecking()
{
    MZRTField<Goober> gooberField;
}


struct Simple
{
    double mz;
    double retentionTime;
    double rtMin;
    double rtMax;
    double retentionTimeMin() const {return rtMin;}
    double retentionTimeMax() const {return rtMax;}

    Simple(double _mz = 0, double _rtMin = 0, double _rtMax = 0)
    :   mz(_mz), 
        retentionTime((_rtMin+_rtMax)/2),
        rtMin(_rtMin), rtMax(_rtMax)
    {}
};


typedef shared_ptr<Simple> SimplePtr;


void testFind()
{
    // rt\mz  400  410  420  430  440 
    //  660    a         c
    //  661    a         c
    //  662              c
    //  663
    //  664    b
    //  665    b         d
    //  666    b         d
    //  667    b         d
    //  668    b
    //  669

    SimplePtr a(new Simple(400, 660, 661));
    SimplePtr b(new Simple(400, 664, 668));
    SimplePtr c(new Simple(420, 660, 662));
    SimplePtr d(new Simple(420, 665, 667));

    MZRTField<Simple> simpleField;
    simpleField.insert(a);
    simpleField.insert(b);
    simpleField.insert(c);
    simpleField.insert(d);

    vector<SimplePtr> result = simpleField.find(420, 1, RTMatches_Any<Simple>());        
    unit_assert(result.size()==2 && result[0]==c && result[1]==d);

    result = simpleField.find(410, 11, RTMatches_Contains<Simple>(666,0));
    unit_assert(result.size()==2 && result[0]==b && result[1]==d);

    result = simpleField.find(420, 1, RTMatches_IsContainedIn<Simple>(*b));
    unit_assert(result.size()==1 && result[0]==d);

    result = simpleField.find(400, 1, RTMatches_IsContainedIn<Simple>(*d, 1.5));
    unit_assert(result.size()==1 && result[0]==b);
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

    if (os_) *os_ << pf << endl;

    unit_assert(pf.size() == 3);

    PeakelField::const_iterator it = pf.begin();
    unit_assert(*it == a);

    // note that std::set allows only const access
    // however, we can modify **it
    (*it)->peaks.push_back(Peak()); 
    (*it)->peaks.clear();

    ++it;
    unit_assert(*it == c);

    ++it;
    unit_assert(*it == b);

    // find()

    if (os_) *os_ << "testPeakelField(): find()\n";

    vector<PeakelPtr> v = pf.find(1.5, .6, RTMatches_Contains<Peakel>(1, .5));

    if (os_) 
    {
        *os_ << "find(): " << v.size() << endl;
        for (vector<PeakelPtr>::const_iterator it=v.begin(); it!=v.end(); ++it)
            *os_ << **it << endl;
    }

    unit_assert(v.size()==2 && v[0]==a && v[1]==b);
    v = pf.find(1.5, .4, RTMatches_Contains<Peakel>(1, .5));
    unit_assert(v.empty());
    v = pf.find(2, .1, RTMatches_Contains<Peakel>(1, .1));
    unit_assert(v.size()==1 && v[0]==b);

    MZTolerance fiveppm(5, MZTolerance::PPM);
    v = pf.find(1.000001, fiveppm, RTMatches_Contains<Peakel>(1, 10));
    unit_assert(v.size()==2 && v[0]==a && v[1]==c);
    v = pf.find(1.000006, fiveppm, RTMatches_Contains<Peakel>(1, 10));
    unit_assert(v.empty());

    // remove()

    if (os_) *os_ << "testPeakelField(): remove()\n";

    pf.remove(a); 
    unit_assert(pf.size() == 2);
    it = pf.begin();
    unit_assert(*it == c);
    ++it; 
    unit_assert(*it == b);

    bool caught = false;
    try { 
        pf.remove(a); 
    }
    catch (exception& e) {
        if (os_) *os_ << "Caught exception correctly: " << e.what() << endl;
        caught = true; 
    }
    unit_assert(caught);

    pf.remove(b);
    unit_assert(pf.size() == 1);
    it = pf.begin();
    unit_assert(*it == c);

    pf.remove(c);
    unit_assert(pf.empty());

    if (os_) *os_ << endl;
}


struct MyPred
{
    void operator()(double mz)
    {
        cout << "MyPred: " << mz << endl;
    }
};


void testFeatureField()
{
    if (os_) *os_ << "testFeatureField()\n";

    FeatureField ff;

    FeaturePtr a(new Feature);
    a->mz=1; a->retentionTime=1;

    FeaturePtr b(new Feature);
    b->mz=2; b->retentionTime=1;

    FeaturePtr c(new Feature);
    c->mz=1; c->retentionTime=2;

    ff.insert(a);
    ff.insert(b);
    ff.insert(c);

    if (os_) *os_ << ff << endl;

    MZTolerance fiveppm(5, MZTolerance::PPM);
    vector<FeaturePtr> v = ff.find(1.000001, fiveppm, RTMatches_Contains<Feature>(1, 10));
    unit_assert(v.size()==2 && v[0]==a && v[1]==c);
    v = ff.find(1.000006, fiveppm, RTMatches_Contains<Feature>(1, 10));
    unit_assert(v.empty());
}


void test()
{
    testPredicate();
    testPredicate_Feature();
    testConceptChecking();
    testFind();
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

