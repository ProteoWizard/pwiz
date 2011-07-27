/*
 * Original author: Greg Finney <gfinney .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#ifndef GAUSS_SMOOTHER_H_
#define GAUSS_SMOOTHER_H_

#include "ChromSmoother.h"
#include <vector>
#include <math.h>



class GaussSmoother : public ChromSmoother {

  public :
    GaussSmoother( int filt_size ) : ChromSmoother ( filt_size ) {
  };
    GaussSmoother() : ChromSmoother() {};
  ///defines the weights based upon the SD of a gaussian, and the derivative
  void set_gauss_weights( float sd, int derivative );
  ///trim the weights at the endpoints where the values are [abs(v) <= frac * max(weights)]
  void trim_weights_by_frac_max( float frac = 0.005 );
  ///trims off the weight distirbution.
  /** \param frac 
       For those values in the gaussian weights, normalized to 1, that are less than max(weights) / frac,
       the distribution is trimmed
  */
  void limit_weights_by_frac( float frac );
};




#endif
