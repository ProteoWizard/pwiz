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

#include "pwiz/utility/findmf/base/resample/convert2dense.hpp"

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/findmf/base/base/interpolate.hpp"
#include "pwiz/utility/findmf/base/base/cumsum.hpp"

namespace{

 using namespace pwiz::util;
  void testConvert()
  {
    std::pair<double, double> tmp;
    tmp.first = 1000.;
    tmp.second = 1010;
    double ppm = 100;
    ralab::base::resample::Convert2Dense c2d;
    c2d.defBreak(tmp,ppm);
    std::vector<double> mids;
    c2d.getMids(mids);
    c2d.am_ = 0.1;

    double mz[] = {1001. , 1001.5 , 1001.8 , 1004. , 1005., 1008. , 1009. , 1009.3};
    size_t smz = sizeof(mz)/sizeof(double);
    std::vector<double> intensity( smz, 10.), gg;
    double all = std::accumulate(intensity.begin(),intensity.end(),0.);

    c2d.convert2dense(mz,mz+smz,intensity.begin(),gg);
    
    double bla = std::accumulate(gg.begin(),gg.end(),0.);
    unit_assert_equal(bla, all - 10., 1e-10);
    
  }

}


int main(int argc, char **argv) {
  testConvert();
}
