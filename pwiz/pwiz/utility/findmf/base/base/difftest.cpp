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

#include "gtest/gtest.hpp"
#include "base/base/diff.hpp"
#include "base/resample/masscomparefunctors.hpp"

namespace {

  
  void testdiff(){

    double epsilon = 0.00001;
    std::vector<double> tmp, res, ref; // res- result, ref-reference
    double numbers_[] = {   2,  3,  4,  5,  6,  7,  8,  9, 10, 11 };
    const int N = sizeof( numbers_ ) / sizeof( double );
    tmp.assign( numbers_ , numbers_ + N ) ;
    ref.assign( 10 , 1 );

    ralab::base::base::diff(tmp.begin() , tmp.end() , std::back_inserter(res) , 1 );
    bool bb = std::equal( res.begin(), res.end(), ref.begin(), ralab::base::resample::DaCompFunctor<double>(epsilon) ) ;
    ASSERT_TRUE( bb );

    res.clear();
    ref.assign(10,2);
    ralab::base::base::diff(tmp.begin() , tmp.end() , std::back_inserter(res) , 2 );
    bb = std::equal( res.begin(), res.end(), ref.begin(), ralab::base::resample::DaCompFunctor<double>(epsilon) ) ;
    ASSERT_TRUE( bb );

    std::vector<double> xxx(tmp);
    std::transform( xxx.begin() , xxx.end() , xxx.begin(), xxx.begin() , std::multiplies<double>() );

    std::vector<double>::iterator itmp =
        ralab::base::base::diff
        (
          xxx.begin(),
          xxx.end(),
          3,
          3
          );
    //long long xt = std::distance(xxx.begin(),itmp);
    //xt;
    unit_assert_equal( *xxx.begin(),0.,  epsilon);
    nit_assert(std::distance(xxx.begin(),itmp) ,1);
  }
}//end namespace

int main(int argc, char **argv) {
  testdiff();
}
