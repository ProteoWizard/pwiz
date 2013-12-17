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

#ifndef GAUSS_H
#define GAUSS_H

#include "pwiz/utility/findmf/base/base/constants.hpp"
#include "pwiz/utility/findmf/base/base/base.hpp"
#include "pwiz/utility/findmf/base/base/scale.hpp"

namespace ralab{
  namespace base{
    namespace filter{
      namespace utilities{

        /*! \brief Gauss function

                  \f[
                  f(x) = \frac{1}{\sigma  \sqrt{2  \pi}} \cdot e^{-0.5 \cdot \frac{x - \mu }{\sigma}^2 }
                  \f]
      \ingroup FILTER
      */
        template<typename TReal>
        struct Gauss : std::unary_function <TReal, TReal> {

          Gauss(TReal mu, TReal sigma)
            :mu_(mu),
              sigma_(sigma)
          {}

          TReal operator()(TReal x)
          {
            return( 1/(sigma_ * sqrt(2. * ralab::constants::PI) ) * exp(-0.5 * ( pow( (x - mu_ )/sigma_, TReal(2.) ) ) ));
          }
        public:
          TReal mu_;
          TReal sigma_;
        };

        /*! \brief First derivative of Gaussian

                  \f$
                  T_1 = -\frac{(x-\mu)}{ \sqrt{2 \pi}* \sigma^2 |\sigma| },\\

                  T_2 = e^{-0.5 \frac{x-\mu}{\sigma}^2 },\\

                  f'(x) = T_1 \cdot T_2
                  \f$

                  \ingroup FILTER
      */
        template<typename TReal>
        struct Gauss_1deriv : std::unary_function< TReal ,TReal > {

          Gauss_1deriv(
              TReal mu//!< mean
              , TReal sigma //!< sigma
              )
            :mu_(mu),
              sigma_(sigma)
          {}
          /*!\brief returns f'(x), with f - Gaussian.*/
          TReal operator()( TReal x )
          {
            TReal T1 = - (x - mu_) / ( sqrt(TReal(2.) * ralab::constants::PI) * pow(sigma_ , TReal(2.)) * abs(sigma_) );
            TReal T2 = exp( -0.5 * pow( ( ( x-mu_ ) / sigma_ ) , TReal(2.) ) );
            return( T1 * T2 );
          }
        protected:
          TReal mu_;
          TReal sigma_;
        };


        template<typename TReal>
        TReal getGaussWorker( TReal sigma,
                              std::vector<TReal> &gauss,
                              std::vector<TReal> &x )
        {
          //generate response
          Gauss<TReal> g(0.,sigma);
          gauss.resize(x.size());
          std::transform(x.begin(),x.end(),gauss.begin(),g);

          //ensure that are of gaussian is one...
          TReal sum = std::accumulate(gauss.begin() , gauss.end() , 0.);
          std::transform(gauss.begin(),gauss.end(),gauss.begin(),std::bind2nd(std::divides<TReal>(),sum )) ;
          TReal sumfilter = std::accumulate(gauss.begin(),gauss.end(),0.);
          return sumfilter;
        }


        template<typename TReal>
        void scaleDerivative(
            std::vector<TReal> &mh //the wavelet to scale
            )
        {
          //do this so that the sum of wavelet equals zero
          TReal sum = std::accumulate(mh.begin() , mh.end() , 0.);
          sum /= mh.size();
          std::transform(mh.begin(),mh.end(),mh.begin(),std::bind2nd(std::minus<TReal>(), sum )) ;
          //compute sum of square...
          TReal sumAbs = 0;
          for(typename std::vector<TReal>::iterator it = mh.begin() ;it != mh.end(); ++it)
            {
              sumAbs += fabs(*it);
            }
          std::transform(mh.begin(),mh.end(),mh.begin(),std::bind2nd(std::divides<TReal>(), sumAbs )) ;
        }

        template<typename TReal>
        TReal getGaussian1DerWorker( TReal sigma, std::vector<TReal> &gauss1d, std::vector<TReal> &x )
        {
          Gauss_1deriv<TReal> g(0.,sigma);
          gauss1d.resize(x.size());
          std::transform(x.begin(),x.end(),gauss1d.begin(),g);
          scaleDerivative(gauss1d);
          TReal sum = std::accumulate(gauss1d.begin(),gauss1d.end(),0.);
          return sum;
        }

      }//utilities
    }//base
  }//filter
}//ralab
#endif
