//
// $Id$
//
//
// Original author: Witold Wolski <wewolski@gmail.com>
//
// Copyright : ETH Zurich
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

#include "pwiz/utility/findmf/base/ms/simplepicker.hpp"
#include "pwiz/utility/misc/unit.hpp"

namespace {

  using namespace pwiz::util;

  // Tests that the Foo::Bar() method does Abc.
  void testpicker() {
    ralab::base::ms::SimplePicker<double> sp;

    std::vector<double> zerocross(10,0.);
    //lag2 at 5
    //lag2 at
    double data2[] = {1.,2.,3.,4.,5.,6.,5.,3.,2.,1.,0.};
    size_t x = sp(data2,data2+sizeof(data2)/sizeof(double),zerocross.begin(),zerocross.size(),2);
    unit_assert_equal(zerocross[0],5.,1e-10);
    x = sp(data2,data2+sizeof(data2)/sizeof(double),zerocross.begin(),zerocross.size(),4);
    unit_assert_equal(zerocross[0],4.6666,1e-4);
    x = sp(data2,data2+sizeof(data2)/sizeof(double),zerocross.begin(),zerocross.size(),6);
    unit_assert_equal(zerocross[0],4.5,1e-4);
    std::cout << x << std::endl;


    double data3[] = {0.,1.,2.,3.,5.,6.,5.,4.,3.,2.,1.};
    x = sp(data3,data3+sizeof(data3)/sizeof(double),zerocross.begin(),zerocross.size(),2);
    x = sp(data3,data3+sizeof(data3)/sizeof(double),zerocross.begin(),zerocross.size(),4);
    unit_assert_equal(zerocross[0],5.3333,1e-4);
    x = sp(data3,data3+sizeof(data3)/sizeof(double),zerocross.begin(),zerocross.size(),6);
    unit_assert_equal(zerocross[0],5.5,1e-4);
    std::cout << x << std::endl;

    double data[] = {1.,2.,3.,4.,5.,6.,5.,4.,3.,2.,1.};

    // for lag 2
    // zero cross at 5

    x = sp(data,data+sizeof(data)/sizeof(double),zerocross.begin(),2);
    unit_assert_equal(zerocross[0],5.,1e-10);
    x = sp(data,data+sizeof(data)/sizeof(double),zerocross.begin(),4);
    unit_assert_equal(zerocross[0],5.,1e-10);
    x = sp(data,data+sizeof(data)/sizeof(double),zerocross.begin(),6);
    unit_assert_equal(zerocross[0],5.,1e-10);

    std::cout << x << std::endl;
  }
}//end namespace


int main(int argc, char **argv) {
 testpicker();
}
