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

#include "pwiz/utility/findmf/base/resample/bucket1d.hpp"
#include "pwiz/utility/findmf/base/resample/bin1d.hpp"

#include <algorithm>
#include <boost/timer.hpp>
#include <boost/cstdint.hpp>

#include "pwiz/utility/misc/unit.hpp"

#include "pwiz/utility/findmf/base/resample/breakspec.hpp"
#include "pwiz/utility/findmf/base/resample/masscomparefunctors.hpp"

namespace {

using namespace pwiz::util;
typedef boost::uint32_t uint32_t;
typedef boost::int32_t int32_t;
  void testBin1D(){
    double ref [] = {2., 2.1 , 5. , 5.1 , 7.1 , 7.15 , 10. , 10.1};
    std::vector<double> breaks;
    breaks.assign(ref , ref + sizeof(ref)/sizeof(double));

    ralab::base::resample::Bin1D bin(breaks);
    unit_assert_operator_equal(bin(2.-1e-8) , 0);
    unit_assert_operator_equal(bin(2.+1e-8) , 1);
    unit_assert_operator_equal(bin(2.1) ,1);
    unit_assert_operator_equal(bin(2.1 + 1e-9) , 2);
  }

  void testBin1D2(){
double epsilon = 1e-8;
    double ref [] = {2., 2.1 , 5. , 5.1 , 7.1 , 7.15 , 10. , 10.1};
    std::vector<double> breaks;
    breaks.assign(ref , ref + sizeof(ref)/sizeof(double));

    ralab::base::resample::Bin1D bin(breaks);
    std::vector<int32_t> idx;
    std::vector<double> dist;
    bin(2. -1e-8 , 2. + 2e-4,  idx, dist ) ;
    unit_assert_operator_equal(idx[0],-1);
    unit_assert_operator_equal(idx[1],0);
    unit_assert_equal(dist[0],1e-8,epsilon);
    unit_assert_equal(dist[1],2e-4,epsilon);

    bin(2. -1e-8 , 2.1 + 2e-4,  idx, dist ) ;
    unit_assert_operator_equal(idx[0],-1);
    unit_assert_operator_equal(idx[1],0);
    unit_assert_operator_equal(idx[2],1);
    unit_assert_equal(dist[0], 1e-8 ,epsilon);
    unit_assert_equal(dist[1], 0.1 ,epsilon);
    unit_assert_equal(dist[2], 2e-4 ,epsilon);

    bin(2. -1e-8 , 5.1 + 2e-4,  idx, dist ) ;
    double resd [] = {1e-8, 0.1, 2.9, 0.1, 2e-4};
    int  idxr[] = {-1,0,1,2,3};
    bool x = std::equal(idxr , idxr + sizeof(idxr)/sizeof(int), idx.begin());
    unit_assert(x);
    x = std::equal(resd , resd + sizeof(resd)/sizeof(double), dist.begin(), ralab::base::resample::DaCompFunctor<double>(1e-8));
    unit_assert(x);

    bin(2. - 2e-4 , 2. - 1e-4,  idx, dist ) ;
    unit_assert_operator_equal(idx[0],-1);
    unit_assert_equal(dist[0],1e-4,1e-14);
    bin(2.1 - 2e-4 , 2.1 - 1e-4,  idx, dist ) ;
    unit_assert_operator_equal(idx[0],0);
    unit_assert_equal(dist[0],1e-4,1e-14);
    bin(2.1 - 1e-4 , 7.1 + 2e-4,  idx, dist ) ;


    //testing end span
    bin(10.1 - 1e-4 , 10.1 + 2e-4,  idx, dist ) ;
    
    bin(5.1 - 1e-4 , 10.1 + 2e-4,  idx, dist ) ;
    
    bin(10.1 + 1e-4 , 10.1 + 2e-4,  idx, dist ) ;
    

  }


  void testHist()
  {
    std::vector<double> breaks;
    std::vector<uint32_t> indicator;
    //                 0     1     2    3     4      5     6
    double ref [] = {2., 2.1 , 5. , 5.1 , 7.1 , 7.15 , 10. , 10.1};
    // We cover 0.1+0.1+0.05+0.1 = 0.35 //
    breaks.assign(ref , ref + sizeof(ref)/sizeof(double));

    /*!\brief length indic is length(ref) - 1
                                */
    //                   0   1   2   3   4   5   6
    uint32_t indic[] = { 1 , 0 , 1 , 0 , 1 , 0 , 1};

    indicator.assign(
          indic
          ,indic + sizeof(indic)/sizeof(uint32_t)
          );

    ralab::base::resample::Bucket1D b1d( breaks, indicator);
    std::vector<double> sample;
    std::pair< size_t , bool > rb;
    rb = b1d.operator()( 1. );
    rb = b1d.operator()( 2. );
    rb = b1d.operator()( 2.05 );
    rb = b1d.operator()( 2.1 );
    rb = b1d.operator()( 4. );
    rb = b1d.operator()( 5. );
    rb = b1d.operator()( 5.01 );
    rb = b1d.operator()( 5.1 );
    rb = b1d.operator()(6.);
    rb = b1d.operator()(7.);
    rb = b1d.operator()(7.12); // 4,true
    rb = b1d.operator()(8.);
    rb = b1d.operator()( 10.1 );
    rb = b1d.operator()( 13. );

    sample.push_back( 1. );//false
    sample.push_back( 2. );//false
    sample.push_back( 2.05 );//#2 -> 0
    sample.push_back( 2.1 );//#3 -> 0
    sample.push_back( 4. );//false
    sample.push_back( 5. );//false
    sample.push_back( 5.01 );//#6 -> 2
    sample.push_back( 5.1 );//#7 -> 2
    sample.push_back( 10.1 );//#8 -> 6
    sample.push_back( 13. );//false

    std::vector< std::pair< size_t , size_t > > res;
    b1d( sample.begin() , sample.end() , res );
    unit_assert(res[0].first == 0 && res[0].second == 2);
    unit_assert(res[1].first == 0 && res[1].second == 3);
  }


}//end namespace

int main(int argc, char **argv) {
  testBin1D();
testBin1D2();
testHist();
}




