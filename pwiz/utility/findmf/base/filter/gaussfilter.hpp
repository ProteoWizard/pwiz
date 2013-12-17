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

#ifndef GAUSSFILTERTYPES_H
#define GAUSSFILTERTYPES_H

#include <boost/math/distributions/normal.hpp>
#include "pwiz/utility/findmf/base/filter/utilities/gauss.hpp"


namespace ralab{
  namespace base{
    namespace filter{

      /*! \brief generate the gauss filter function for filtering of peaks with fwhm (full width at half max)

                        \post accumulate(gauss) == 1.
                        \return accumulate(gauss) == 1.
                        */
      template <typename TReal>
      TReal getGaussianFilter
      (
          std::vector<TReal> & gauss, //!<[out] Gaussian for filtering
          TReal fwhm = 20 //!<[in] full width at half max in points
          )
      {
        std::vector<TReal> x;
        ralab::base::base::seq<TReal>( -ceil(TReal(2*fwhm)), ceil(TReal(2*fwhm)) , x);
        TReal sigma = fwhm/2.35;
        //generate response
        return utilities::getGaussWorker(sigma, gauss, x);
      }

      /*! \brief generate the gauss filter function for filtering of peaks with fwhm (full width at half max)

                        \post accumulate(gauss) == 1.
                        \return accumulate(gauss) == 1.
                        */
      template <typename TReal>
      TReal getGaussianFilterQuantile
      (
          std::vector<TReal> & gauss, //!<[out] Gaussian for filtering
          TReal fwhm = 20, //!<[in] full width at half max in points
          TReal quantile = 0.01 //!< would mean that the generated distribution covers at least 99.8 of mass
          )
      {
        if( quantile >= 0.5)
          {
            throw std::logic_error("quantile >= 0.5");
          }
        std::vector<TReal> x;

        TReal sigma = fwhm/2.35;
        boost::math::normal_distribution<TReal> nd_(0,sigma);
        TReal quant = floor(boost::math::quantile(nd_,quantile));
        ralab::base::base::seq( quant , -quant , x);
        return utilities::getGaussWorker(sigma, gauss, x);

      }


      /*! \brief generate first derivative Gauss

                        \post accumulate(gauss1d) == 0.
                        \post accumulate(fabs(gauss1d)) == 1.
                        */

      template <typename TReal>
      TReal getGaussian1DerFilter(
          std::vector<TReal> & gauss1d, //!<[out] Gaussian for filtering
          TReal fwhm = 20 //!<[in] full width at half max in points
          )
      {
        std::vector<TReal> x;
        ralab::base::base::seq( - ceil(TReal(2*fwhm)), ceil(TReal(2*fwhm)) , x);
        TReal sigma = fwhm/2.35;
        //generate response
        return utilities::getGaussian1DerWorker(sigma, gauss1d, x);
      }



      template <typename TReal>
      TReal getGaussian1DerFilterQuantile(
          std::vector<TReal> & gauss1d, //!<[out] Gaussian for filtering
          TReal fwhm = 20, //!<[in] full width at half max in points
          TReal quantile = 0.1
          )
      {
        if( quantile >= 0.5)
          {
            throw std::logic_error("quantile >= 0.5");
          }
        std::vector<TReal> x;
        TReal sigma = fwhm/2.35;
        boost::math::normal_distribution<TReal> nd_(0,sigma);
        TReal quant = floor(boost::math::quantile(nd_,quantile));
        ralab::base::base::seq( quant , -quant , x);
        //generate response
        return utilities::getGaussian1DerWorker(sigma, gauss1d, x);
      }
    }//filter
  }//base
}//ralab

#endif
