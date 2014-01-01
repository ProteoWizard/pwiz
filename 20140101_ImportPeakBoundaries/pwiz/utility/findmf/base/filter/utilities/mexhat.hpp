//
// $Id:  $
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

#ifndef FILTERTYPES_H
#define FILTERTYPES_H

namespace ralab{
  namespace base{
    namespace filter{
      namespace utilities{
        /*! \brief Mexican hat wavelet.

                  \f{eqnarray*}{
                   T_1 &= {1 \over {\sqrt {2\pi}\sigma^3}}\\
                   T_2 &= \left( 1 - {t^2 \over \sigma^2} \right)\\
                   T_3 &= e^{-t^2 \over 2\sigma^2} \\
                   \psi(t) &= T_1 \cdot T_2 \cdot T_3
                  \f}
      is the negative normalized second derivative of a Gaussian function. (Source Wikipedia.)
                  (LoG) Laplace of Gaussian, with Laplace's operator having a (-1,2,-1) Kernel.

      \ingroup FILTER
      */
        template<typename TReal >
        struct Mexican_Hat: std::unary_function< TReal, TReal> {

          Mexican_Hat(
              TReal mu, //!< mean
              TReal sigma //!<standard deviation
              )
            :mu_(mu),
              sigma_(sigma)
          {}
          /*! \brief operator */
          TReal operator()( TReal x )
          {
            TReal two = TReal(2);
            TReal t1( 1 / (pow(TReal(2. * ralab::constants::PI) , TReal(.5)) * pow(sigma_,TReal(3.))) );
            TReal t2(1 - pow( x-mu_ , two)/ pow(sigma_, two ) );
            TReal t3( exp(-pow((x-mu_) , two )/( 2 * pow( sigma_, two ) ) ) );
            return( t1 * t2 * t3 );
          }
        protected:
          TReal mu_;
          TReal sigma_;
        };

        /*! \brief Mexican hat wavelet Version 2.

                  For Gaussian of the same amplitude and matching withs, generates response of same amplitude.

                  \f{eqnarray*}{
                   T_1 &= {1 \over {\sigma}}\\
                   T_2 &= \left( 1 - {t^2 \over \sigma^2} \right)\\
                   T_3 &= e^{-t^2 \over 2\sigma^2}\\
                   \psi(t) &= T_1 \cdot T_2 \cdot T_3
                  \f}

                  Note, the change in the Term \f$T_1\f$ compared with the Mexican_Hat functor.
                  */

        template<typename TReal >
        struct Mexican_Hat2 : std::unary_function<TReal, TReal >{

          Mexican_Hat2(
              TReal mu, //!< mean
              TReal sigma //!<standard deviation
              )
            :mu_(mu),
              sigma_(sigma)
          {}

          TReal operator()( TReal x )
          {
            TReal two = TReal(2);
            TReal t1( 1 / sigma_ );
            TReal t2(1 - pow( x-mu_ , two)/ pow(sigma_,two ) );
            TReal t3( exp(-pow((x-mu_) , two )/( 2 * pow( sigma_, two ) ) ) );
            return( t1 * t2 * t3 );
          }
        protected:
          TReal mu_;
          TReal sigma_;
        };



        /*!\brief Scales a mother wavelet so that the conditions hold:
                  \f[ sum(mh) = 0 \f]
                  \f[ sum(mh^2) = 1 \f]
                  */
        template<typename TReal>
        TReal scaleWavelet(
            std::vector<TReal> &mh, //the wavelet to scale
            std::vector<TReal> &x //temporary vector....
            )
        {
          //do this so that the sum of wavelet equals zero
          TReal sum = std::accumulate(mh.begin() , mh.end() , 0.);
          sum /= mh.size();
          std::transform(mh.begin(),mh.end(),mh.begin(),std::bind2nd(std::minus<TReal>(), sum )) ;

          //compute sum of square...
          std::transform( mh.begin(), mh.end(), x.begin(), ralab::base::stats::NthPower<2,TReal>() ); //first sqaure all elements
          TReal sumsq = sqrt(std::accumulate(x.begin(), x.end() , TReal(0.)));
          std::transform(mh.begin() , mh.end() , mh.begin() , std::bind2nd(std::divides<TReal>(), sumsq ) ) ;

          //this is just to verify the result
          std::transform( mh.begin(), mh.end(), x.begin(), ralab::base::stats::NthPower<2,TReal>() ); //first sqaure all elements
          sumsq = std::accumulate(x.begin(), x.end() , TReal(0.));
          return sumsq;

        }

        template<typename TReal>
        TReal getMaxHatWorker( TReal sigma, std::vector<TReal> &mh, std::vector<TReal> &x )
        {
          ralab::base::filter::utilities::Mexican_Hat<TReal> mexHatGenerator(0.,sigma);
          mh.resize( x.size() );
          std::transform( x.begin()  ,x.end(),mh.begin(),mexHatGenerator);
          scaleWavelet(mh, x);
          TReal sum = std::accumulate(mh.begin(),mh.end(),0.);
          return sum;
        }
      }//utilities
    }//filter
  }//base
}//ralab

#endif
