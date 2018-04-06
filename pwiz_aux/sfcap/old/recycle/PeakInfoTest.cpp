//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "PeakInfo.hpp"
#include "extstd/unit.hpp"
#include <iostream>
#include <vector>
#include <iterator>
#include <fstream>


using namespace pwiz::extstd;
using namespace pwiz::peaks;
using namespace std;


void testEquality()
{
    PeakInfo a(2, 10, 0);
    const double epsilon = numeric_limits<double>::epsilon();
    PeakInfo b(2 + epsilon, 10, 0);
    unit_assert(a == b);
    PeakInfo c(2 + 2*epsilon, 10, 0);
    unit_assert(a != c);
}


void testIO()
{
    // create an array of PeakInfo structs
    vector<PeakInfo> v;
    for (int i=0; i<10; i++)
        v.push_back(PeakInfo(i, i%3, i%4));

    // write it out to file (tests <<)
    string filename = "PeakInfoTest_temp.txt";
    ofstream os(filename.c_str());
    copy(v.begin(), v.end(), ostream_iterator<PeakInfo>(os, "\n"));
    os.close();

    // read it back in and verify (tests >>)
    vector<PeakInfo> w;
    ifstream is(filename.c_str());
    copy(istream_iterator<PeakInfo>(is), istream_iterator<PeakInfo>(), back_inserter(w));
    unit_assert(v==w);
    system(("rm " + filename).c_str());
}


void test()
{
    testEquality();
    testIO();
}


int main()
{
    try
    {
        cerr << "PeakInfoTest\n";
        test();
        cout << "success\n";
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

