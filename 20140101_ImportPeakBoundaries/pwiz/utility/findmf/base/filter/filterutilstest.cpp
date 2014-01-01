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
#include "pwiz/utility/findmf/base/filter/filter.hpp"


#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/findmf/base/base/cumsum.hpp"
#include "pwiz/utility/findmf/base/resample/masscomparefunctors.hpp"
#include "pwiz/utility/misc/unit.hpp"

namespace {
using namespace pwiz::util;
  /*! \brief test filter function */
  void filterTest()
  {
    double epsilon = 0.000001;
    epsilon;
    std::vector<double> data;
    ralab::base::base::seq(-500., 500., .1, data);
    std::vector<double> y;
    std::transform(data.begin(), data.end(), std::back_inserter(y), static_cast<double(*)(double)>(sin) );

    std::vector<double> filt3(21,1./21.); // mean filter
    double sumfilt =   std::accumulate(filt3.begin(), filt3.end(), 0.0 );
    unit_assert_equal(sumfilt, 1.0,epsilon );

    std::vector<double> result;
    ralab::base::filter::filter(
          y,
          filt3,
          result
          );

    ralab::base::filter::filter(
          y,
          filt3,
          result,
          true
          );

    result.resize(y.size());

    ralab::base::filter::filter_sequence(
          y.begin(),
          y.end(),
          filt3.begin(),
          filt3.size(),
          result.begin(),
          true
          );

  }

  /*! \brief Evaluate data extension */
  void testExtendData()
  {
    std::vector<int> tmp, res;
    ralab::base::base::seq(5,tmp);
    std::vector<int>::iterator it = ralab::base::filter::utilities::prepareData(tmp.begin(),tmp.end(), 5, res);
    res.resize(std::distance(res.begin(),it));
    double ref[] = { 4, 5, 1, 2, 3, 4 ,5, 1, 2};

    /*std::copy(res.begin(),res.end(),std::ostream_iterator<int>(std::cout," "));
                              std::cout << std::endl;
                              */
    bool iseq = std::equal(res.begin(),res.end(),ref);
    unit_assert(iseq);

    it = ralab::base::filter::utilities::prepareData(tmp.begin(),tmp.end(), 5, res, true);
    res.resize(std::distance(res.begin(),it));

    double ref2[] = {2, 1, 1, 2, 3, 4, 5, 5, 4};
    iseq = std::equal(res.begin(),res.end(),ref2);
    unit_assert(iseq);
  }

}//end namespace


int main(int argc, char **argv) {
	filterTest();
	testExtendData();
}





