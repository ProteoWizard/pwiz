// Copyright : ETH Zurich
// License   : three-clause BSD license
// Authors   : Witold Wolski
// for full text refer to files: LICENSE, AUTHORS and COPYRIGHT

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
