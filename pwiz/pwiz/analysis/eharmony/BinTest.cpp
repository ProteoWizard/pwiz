//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
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

///
/// BinTest.cpp
///

#include <cstring>
#include "Bin.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace std;
using namespace pwiz;
using namespace pwiz::util;

ostream* os_ = 0;

struct IsInt
{
    IsInt(int n) : _n(n){}
    bool operator()( boost::shared_ptr<const int> m) { return *m == _n;}
    int _n;

};

void test()
{
    if (os_) *os_ << "\n[BinTest.cpp] test() ... \n";
    pair<double,double> a = make_pair(1.5,2);
    pair<double,double> b = make_pair(2.5,3);
    pair<double,double> c = make_pair(3,2.0);

    int a1 = 1;
    int b1 = 2;
    int c1 = 3;

    vector<pair<pair<double,double>, int> > stuf;
    stuf.push_back(make_pair(a,a1));
    stuf.push_back(make_pair(b,b1));
    stuf.push_back(make_pair(c,c1));

    Bin<int> bin(stuf, 4, 4);

    vector<boost::shared_ptr<int> > v;
    pair<double,double> p(1.6,2);
    bin.getBinContents(p, v);

    vector<boost::shared_ptr<int> >::iterator it = v.begin();
    
    if (os_)
        {
            *os_ << "\ntesting Bin::getBinContents ... found: \n";
            for(; it != v.end(); ++it)
                *os_ << **it << endl;

        }

    vector<int> truth;
    truth.push_back(1);
    truth.push_back(2);
    truth.push_back(3);

    vector<boost::shared_ptr<int> >::iterator v_it = v.begin();
    vector<int>::iterator truth_it = truth.begin();
    for(; v_it != v.end(); ++v_it, ++truth_it) unit_assert(**v_it == *truth_it);


    // test getAdjacentBinContents
    Bin<int> smallBins(stuf,0.5,0.5);
    vector<boost::shared_ptr<int> > v2;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2),v2);
    
    vector<boost::shared_ptr<int> >::iterator it2 = v2.begin();
    
    unit_assert(find_if(v2.begin(),v2.end(),IsInt(1)) != v2.end());

    if (os_)
        {
            *os_ << "\ntesting Bin::getAdjacentBinContents ... found: \n";
            for(; it2 != v2.end(); ++it2)
                *os_ << **it2 << endl;

        }

    // test update
    
    int n = 4;
    smallBins.update(n, pair<double,double>(1.5,2));

    vector<boost::shared_ptr<int> > v3;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2), v3);
    vector<boost::shared_ptr<int> >::iterator it3 = v3.begin();

    unit_assert(find_if(v3.begin(),v3.end(),IsInt(4)) != v3.end());

    if (os_)
        {
            *os_ << "\ntesting Bin::update ... found: \n";
            for(; it3 != v3.end(); ++it3)
                *os_ << **it3 << endl;

        }


    // test erase
    smallBins.erase(n, pair<double,double>(1.5,2));
    vector<boost::shared_ptr<int> > v4;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2), v4);
    vector<boost::shared_ptr<int> >::iterator it4 = v4.begin();

    unit_assert(find_if(v4.begin(), v4.end(), IsInt(4)) == v4.end());

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            test();

        }

    catch (std::exception& e)
        {
            cerr << e.what() << endl;
            return 1;

        }

    catch (...)
        {
            cerr << "Caught unknown exception.\n";
            return 1;

        }

    return 0;

}
