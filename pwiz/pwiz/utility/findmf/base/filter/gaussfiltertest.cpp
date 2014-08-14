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

#include "pwiz/utility/findmf/base/filter/gaussfilter.hpp"

#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/findmf/base/base/cumsum.hpp"
#include "pwiz/utility/findmf/base/resample/masscomparefunctors.hpp"
#include "pwiz/utility/misc/unit.hpp"
namespace {
   using namespace pwiz::util;
 
  void testGaussian()
  {
    std::vector<double> x;
    std::vector<double> y;
    double epsilon = 5e-5;
    //generate x values
    ralab::base::base::seq(-10.,10.,1.,x);
    std::transform(x.begin(),x.end(),std::back_inserter(y),ralab::base::filter::utilities::Gauss<double>(0.,1.));
    double result[21] ={ 7.694599e-23, 1.027977e-18, 5.052271e-15, 9.134720e-12, 6.075883e-09,
                         1.486720e-06, 1.338302e-04, 4.431848e-03, 5.399097e-02, 2.419707e-01,
                         3.989423e-01, 2.419707e-01, 5.399097e-02, 4.431848e-03, 1.338302e-04,
                         1.486720e-06, 6.075883e-09, 9.134720e-12, 5.052271e-15, 1.027977e-18,
                         7.694599e-23};

    unit_assert(std::equal(y.begin(),y.end(),result,ralab::base::resample::DaCompFunctor<double>(epsilon)));
    double sumfilter = std::accumulate(y.begin(),y.end(),0.);
    std::pair<double,double> tmp;
    ralab::base::stats::scale(y.begin(),y.end(),tmp,true);
    std::transform(y.begin(),y.end(),y.begin(),std::bind2nd(std::plus<double>(),( 1./x.size() ) ) );
    sumfilter = std::accumulate(y.begin(),y.end(),0.);
    unit_assert_equal(sumfilter,1.,epsilon);
  }

  void testGauss_1deriv()
  {
    //code to compare goes here
    std::vector<double> x;
    std::vector<double> y;
    double epsilon = 5e-5;
    //generate x values
    ralab::base::base::seq(-10.,10.,1.,x);
    std::transform(x.begin(),x.end(),std::back_inserter(y),ralab::base::filter::utilities::Gauss_1deriv<double>(.0,1.));
    double firstderiv[21] = {
      7.694599e-22,  9.251796e-18,  4.041817e-14,  6.394304e-11,  3.645530e-08,  7.433598e-06,  5.353209e-04 , 1.329555e-02,  1.079819e-01,  2.419707e-01,
      0.000000e+00, -2.419707e-01, -1.079819e-01, -1.329555e-02, -5.353209e-04, -7.433598e-06, -3.645530e-08, -6.394304e-11 -4.041817e-14 -9.251796e-18,
      -7.694599e-22
    };

    // std::vector<double> fristderivV(firstderiv,firstderiv+21);
    unit_assert(std::equal(y.begin(),y.end(),firstderiv,ralab::base::resample::DaCompFunctor<double>(epsilon)));
    std::pair<double,double> tmp;
    ralab::base::stats::scale(y.begin(),y.end(),tmp,true);
    double sumfilter = std::accumulate(y.begin(),y.end(),0.0);
    unit_assert_equal(sumfilter,0.,epsilon);
    double t = ralab::base::filter::getGaussian1DerFilter(x);
    sumfilter = std::accumulate(x.begin(),x.end(),0.0);
    t;
  }

  void testGetGaussian()
  {
    std::vector<double> xx;
    ralab::base::filter::getGaussianFilter(xx,30.);
    unit_assert(xx.size() == 121);
    unit_assert_equal(std::accumulate(xx.begin(),xx.end(),0.),1.,1e-6);
    ralab::base::filter::getGaussianFilterQuantile(xx,30.);
  }

} //end namespace


int main(int argc, char **argv) {
  testGetGaussian();
  testGauss_1deriv();
  testGetGaussian();
}
