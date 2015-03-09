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

#include <boost/cstdint.hpp>

#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/misc/unit.hpp"

namespace {
  using namespace pwiz::util;
	typedef boost::int32_t int32_t;
  // Tests that the Foo::Bar() method does Abc.
  void testseq() {
    std::vector<double> res;
    ralab::base::base::seq(1.,10.,0.5,res);
    ralab::base::base::seq(10.,2.,res);
    ralab::base::base::seq(2.,10.,res);
    ralab::base::base::seq(10., 2. , -0.5 , res);

    std::vector<int32_t> res2;
    ralab::base::base::seq(10,2,res2);
    ralab::base::base::seq(10,res);
    ralab::base::base::seq(res2,res);

    std::vector<unsigned int> resunsigned;
    ralab::base::base::seq(1u,10u,1u,resunsigned);
    ralab::base::base::seq(1u,10u,resunsigned);

    std::vector<double> resdouble;
    ralab::base::base::seq_length(100. , 1300.,18467,resdouble);
    unit_assert(resdouble.size() == 18467);
    ralab::base::base::seq_length(100. , 1300.,19467,resdouble);
    unit_assert(resdouble.size() == 19467);

    ralab::base::base::seq_length(0.,1000.,1000,resdouble);
    unit_assert(resdouble.size() == 1000);
  }

  // Tests that Foo does Xyz.
  void testmean() {
    std::vector<double> x;
    x.push_back(1.0);
    x.push_back(1.0);
    x.push_back(1.0);
    x.push_back(1.);
    x.push_back(2.);
    x.push_back(3.);
    x.push_back(5.);
    x.push_back(5.);
    x.push_back(6.);
    x.push_back(7.);
    x.push_back(8.);
    double res = ralab::base::base::mean(x);
    unit_assert_equal ( 3.636364, res, 1e-4);
    res = ralab::base::base::mean(x, 0.3);
    unit_assert_equal ( 3.2, res, 1e-4);
    res = ralab::base::base::mean(x, 0.4);
    unit_assert_equal ( 3.33333, res, 1e-4);
    res = ralab::base::base::mean(x, 0.5);
    std::cout << res << std::endl;
    unit_assert_equal ( 3., res, 1e-4);
    res = ralab::base::base::mean(x.begin(),x.end());
    unit_assert_equal ( 3.636364, res, 1e-4);

  }

  void testgeometricmean(){
    std::vector<double> x;
    x.push_back(1.0);
    x.push_back(2.0);
    x.push_back(3.0);

    double res = ralab::base::base::geometricMean(x.begin(), x.end());
    unit_assert_equal( 1.817121, res, 1e-4 );

  }
}  // namespace

int main(int argc, char **argv) {
 testseq();
testmean();
testgeometricmean();
}
