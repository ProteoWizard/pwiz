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


#include "SampleDatum.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <complex>
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_ = 0;


template <typename abscissa_type, typename ordinate_type>
void test()
{
    typedef SampleDatum<abscissa_type,ordinate_type> sd_type;

    vector<sd_type> v;
    v.push_back(sd_type(1,2));
    v.push_back(sd_type(3,4));
    v.push_back(sd_type(5,6));

    // write the pairs out to a stream
    ostringstream oss;
    copy(v.begin(), v.end(), ostream_iterator<sd_type>(oss, "\n"));
    if (os_) *os_ << oss.str();

    // read them back in 
    vector<sd_type> w;
    istringstream iss(oss.str());
    copy(istream_iterator<sd_type>(iss), istream_iterator<sd_type>(), back_inserter(w)); 

    // compare the two vectors
    unit_assert(v == w);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "SampleDatumTest\n";

        test<int,int>();
        test<double,double>();
        test< double,complex<double> >();
        test< complex<double>,complex<double> >();

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception.\n";
    }

    return 1; 
}


