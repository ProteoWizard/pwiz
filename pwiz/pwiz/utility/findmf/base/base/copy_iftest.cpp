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


#include "pwiz/utility/findmf/base/base/copyif.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/findmf/base/resample/masscomparefunctors.hpp"
#include <boost/bind.hpp>
#include <vector>

namespace {
 using namespace pwiz::util;
  void copyif()
  {
    double x[] = {1,3,5,1,3,5,1,3,5,1,3,5};
    double y[] = {2,4,6,2,4,6,2,4,6,2,4,6};

    std::vector<double> res;

    ralab::base::utils::copy_if(x,x+sizeof(x)/sizeof(double),std::back_inserter(res),boost::bind(std::less<double>(), _1, 2 ) );
    unit_assert_operator_equal(res.size(),4);
    res.clear();
    ralab::base::utils::copy_if_not(x,x+sizeof(x)/sizeof(double),std::back_inserter(res),boost::bind(std::less<double>(), _1, 2 ) );
    unit_assert_operator_equal(res.size(),8);
    res.clear();
  }

  //tmp
  void copyif2()
  {
    double x[] = {1,3,5,1,3,5,1,3,5,1,3,5};
    double y[] = {2,4,6,2,4,6,2,4,6,2,4,6};

    std::vector<double> res;
    ralab::base::utils::copy_if(x,x+sizeof(x)/sizeof(double),y,
                                std::back_inserter(res),
                                boost::bind(std::less<double>(), _1, 2 ) );
    unit_assert_operator_equal(res.size(),4);
    unit_assert_operator_equal(res[0],2);
    res.clear();
    ralab::base::utils::copy_if_not(x,x+sizeof(x)/sizeof(double),y,
                                    std::back_inserter(res),boost::bind(std::less<double>(), _1, 2 ) );
    unit_assert_operator_equal(res.size(),8);
    unit_assert_operator_equal(res[0],4);
    res.clear();
  }
}//namespace

int main(int argc, char **argv) {
copyif();
copyif2();
 return 0;
}


