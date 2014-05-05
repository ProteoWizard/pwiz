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


#include "OrderedPair.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/static_assert.hpp"


using namespace pwiz::util;
using namespace pwiz::math;


ostream* os_ = 0;


BOOST_STATIC_ASSERT(sizeof(OrderedPair) == 2*sizeof(double));


void testContainer(const OrderedPairContainerRef& pairs)
{
    // verify that pairs == { (1,2), (3,4), (5,6) }

    // test size

    if (os_) 
    {
        copy(pairs.begin(), pairs.end(), ostream_iterator<OrderedPair>(*os_, " "));
        *os_ << endl;
    }

    unit_assert(pairs.size() == 3);

    // test iteration

    OrderedPairContainerRef::const_iterator it = pairs.begin();
    unit_assert(it->x == 1);
    unit_assert(it->y == 2);
    
    ++it;
    unit_assert(it->x == 3);
    unit_assert(it->y == 4);

    ++it;
    unit_assert(it->x == 5);
    unit_assert(it->y == 6);

    // test random access

    unit_assert(pairs[0].x == 1);
    unit_assert(pairs[0].y == 2);
    unit_assert(pairs[1].x == 3);
    unit_assert(pairs[1].y == 4);
    unit_assert(pairs[2].x == 5);
    unit_assert(pairs[2].y == 6);

    // test algorithms

    vector<OrderedPair> v;
    copy(pairs.begin(), pairs.end(), back_inserter(v));
    unit_assert(v.size() == 3);
    unit_assert(v[0].x == 1);
    unit_assert(v[0].y == 2);
    unit_assert(v[1].x == 3);
    unit_assert(v[1].y == 4);
    unit_assert(v[2].x == 5);
    unit_assert(v[2].y == 6);
}


void testArray()
{
    if (os_) *os_ << "testArray()\n";
    double a[] = {1, 2, 3, 4, 5, 6};    
    OrderedPairContainerRef pairs(a, a+sizeof(a)/sizeof(double));
    testContainer(pairs);
}


void testVectorDouble()
{
    if (os_) *os_ << "testVectorDouble()\n";
    vector<double> v;
    for (int i=1; i<=6; i++) v.push_back(i);
    testContainer(v); // note automatic conversion: vector<double> -> OrderedPairContainerRef
}


void testVectorOrderedPair()
{
    if (os_) *os_ << "testVectorOrderedPair()\n";
    vector<OrderedPair> v;
    v.push_back(OrderedPair(1,2));
    v.push_back(OrderedPair(3,4));
    v.push_back(OrderedPair(5,6));
    testContainer(v); // note automatic conversion: vector<OrderedPair> -> OrderedPairContainerRef
}


#pragma pack(push, 1)
struct CustomPair {double a; double b; CustomPair(double _a, double _b) : a(_a), b(_b) {} };
#pragma pack(pop)


void testVectorCustomPair()
{
    if (os_) *os_ << "testVectorCustomPair()\n";
    vector<CustomPair> v;
    v.push_back(CustomPair(1,2));
    v.push_back(CustomPair(3,4));
    v.push_back(CustomPair(5,6));
    testContainer(v); // note automatic conversion: vector<CustomPair> -> OrderedPairContainerRef
}


void testEquality()
{
    if (os_) *os_ << "testEquality()\n";
    vector<OrderedPair> v;
    v.push_back(OrderedPair(1,2));
    v.push_back(OrderedPair(3,4));
    v.push_back(OrderedPair(5,6));

    vector<OrderedPair> w = v;
   
    unit_assert(v == w);
    w.push_back(OrderedPair(7,8));
    unit_assert(v != w);
    v.push_back(OrderedPair(7,9));
    unit_assert(v != w);
    v.back().y = w.back().y;
    unit_assert(v == w);
}


void testExtraction()
{
    vector<OrderedPair> v;
    istringstream iss("(420,666)  (421,667)");
    copy(istream_iterator<OrderedPair>(iss), istream_iterator<OrderedPair>(), back_inserter(v));
    unit_assert(v.size() == 2);
    unit_assert(v[0].x == 420);
    unit_assert(v[0].y == 666);
    unit_assert(v[1].x == 421);
    unit_assert(v[1].y == 667);
}


void test()
{
    testArray();
    testVectorDouble();
    testVectorOrderedPair();
    testVectorCustomPair();
    testEquality();
    testExtraction();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

