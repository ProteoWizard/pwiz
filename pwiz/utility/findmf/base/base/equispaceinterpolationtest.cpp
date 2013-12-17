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
#include "pwiz/utility/findmf/base/base/interpolate.hpp"

#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/findmf/base/base/cumsum.hpp"
#include "pwiz/utility/misc/unit.hpp"


namespace{
using namespace pwiz::util;

  void testApproxLinearSequence()
  {
    std::vector<double> x, y, ys, xout, yout;
    ralab::base::base::seq(-20.,20.,1., x);
    y.assign(x.begin(),x.end());
    //double (*p)(double, double) = pow;

    //ralab::base::stats::runif(x.size(), y ,-2.,2. );
    ralab::base::cumsum(y.begin(),y.end(),ys);

    ralab::base::base::seq(-30.,30.,.1, xout);

    yout.resize(xout.size());
    ralab::base::base::interpolate_linear(
          ys.begin(),
          ys.end(),
          xout.begin(),
          xout.end(),
          yout.begin()
          ,-20
          );
    yout.resize(xout.size());
    ralab::base::base::interpolate_cosine(ys.begin(),ys.end(),
                                    xout.begin(),xout.end(),yout.begin(),-20);

    yout.resize(xout.size());
    ralab::base::base::interpolate_cubic(ys.begin(),ys.end(),
                                   xout.begin(),xout.end(),yout.begin(),-20);


    ////// constant approximation //////
    yout.resize(xout.size());
    ralab::base::base::interpolate_Hermite(ys.begin(),ys.end() ,
                                     xout.begin(),xout.end(),yout.begin(),1.,0.,-20);
  }

}//end namespace

int main(int argc, char **argv) {
testApproxLinearSequence();
}
