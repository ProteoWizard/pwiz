// Copyright : ETH Zurich
// License   : three-clause BSD license
// Authors   : Witold Wolski
// for full text refer to files: LICENSE, AUTHORS and COPYRIGHT


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


