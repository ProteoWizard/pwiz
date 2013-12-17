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


#ifndef INTERPOLATIONUTILITIES_H
#define INTERPOLATIONUTILITIES_H

#include "pwiz/utility/findmf/base/base/constants.hpp"
#include <cmath>

namespace ralab
{
  namespace base
  {
    namespace base
    {

      namespace utilities
      {
        /// LinearInterpolate Functor
        template<typename TReal>
        struct LinearInterpolate
        {
          TReal epsilon_;
          LinearInterpolate(TReal epsilon = std::numeric_limits<TReal>::epsilon() )
            :epsilon_(epsilon){}

          inline TReal operator()
          (
              TReal y1, //!< y1
              TReal y2, //!< y2
              TReal mu //!< location parameter 0,1
              )
          {
            if(mu < epsilon_)
              return y1;
            else if(-(mu - 1.) < epsilon_)
              return y2;
            else
              return ( y1 * (1-mu) + y2 * mu ) ;
          }
        };//

        /// CosineInterpolate Functor
        /// Linear interpolation results in discontinuities at each point.
        /// Often a smoother interpolating function is desirable, perhaps the simplest is cosine interpolation.
        /// A suitable orientated piece of a cosine function serves
        /// to provide a smooth transition between adjacent segments.

        template<typename TReal>
        struct CosineInterpolate
        {
          /*!\brief operator */
          inline TReal operator()(
              TReal y1,//!< y1
              TReal y2,//!< y2
              TReal mu//!< location parameter in [0.,1.]
              )
          {
            TReal mu2;
            mu2 = (1. - cos(mu* ralab::constants::PI))/2;
            return(y1*( 1. -mu2 )+y2*mu2);
          }
        };


        /// CubicInterpolate Functor

        /// Cubic interpolation is the simplest method that offers true continuity between the segments.
        /// As such it requires more than just the two endpoints of the segment but also the two points on either side of them.
        /// So the function requires 4 points in all labeled y0, y1, y2, and y3, in the code below.
        /// mu still behaves the same way for interpolating between the segment y1 to y2.
        /// This doe  s raise issues for how to interpolate between the first and last segments.
        /// In the examples here I just haven't bothered.
        /// A common solution is the dream up two extra points at the start and end of the sequence,
        /// the new points are created so that they have a slope equal to the slope of the start or end segment.

        template<typename TReal>
        struct CubicInterpolate
        {
          TReal epsilon_;
          CubicInterpolate(TReal epsilon = std::numeric_limits<TReal>::epsilon()
              ):epsilon_(epsilon)
          {}

          /*!\brief operator */
          inline TReal operator()
          (
              TReal y0,//!< y0
              TReal y1,//!< y1
              TReal y2,//!< y2
              TReal y3,//!< y3
              double mu//!< location parameter in [0.,1.]
              )
          {
            if(mu < epsilon_)
              {
                return y1;
              }
            else if(-(mu - 1.) < epsilon_)
              {
                return y2;
              }
            else
              {
                TReal a0,a1,a2,a3,mu2;
                mu2 = mu*mu;
                a0 = y3 - y2 - y0 + y1;
                a1 = y0 - y1 - a0;
                a2 = y2 - y0;
                a3 = y1;
                return(a0*mu*mu2 + a1*mu2 + a2*mu + a3);
              }
          }
        };

        /// HermiteInterpolation.
        /// Hermite interpolation like cubic requires 4 points so that it can achieve a higher degree of continuity.
        /// In addition it has nice tension and biasing controls.
        /// Tension can be used to tighten up the curvature at the known points.
        /// The bias is used to twist the curve about the known points.
        /// The examples shown here have the default tension and bias values of 0,
        /// it will be left as an exercise for the reader to explore different tension and bias values.

        template<typename TReal>
        struct HermiteInterpolate
        {
          TReal tension_;
          TReal bias_;
          TReal epsilon_;
          HermiteInterpolate(
              TReal tension,//!< 1 is high, 0 normal, -1 is low
              TReal bias,//!< 0 is even, positive is towards first segment, negative towards the other
              TReal epsilon = std::numeric_limits<TReal>::epsilon()
              ): tension_(tension), bias_(bias), epsilon_(epsilon)
          {}
          /*!\brief operator */
          inline TReal operator ()(
              TReal y0,//!< y0
              TReal y1,//!< y1
              TReal y2,//!< y2
              TReal y3,//!< y3
              TReal mu //!< location
              )
          {
            if(mu < epsilon_)
              {
                return y1;
              }
            else if(-(mu - 1.) < epsilon_)
              {
                return y2;
              }
            else
              {
                TReal m0,m1,mu2,mu3;
                TReal a0,a1,a2,a3;
                mu2 = mu * mu;
                mu3 = mu2 * mu;
                m0  = (y1-y0)*(1+bias_)*(1-tension_)/2;
                m0 += (y2-y1)*(1-bias_)*(1-tension_)/2;
                m1  = (y2-y1)*(1+bias_)*(1-tension_)/2;
                m1 += (y3-y2)*(1-bias_)*(1-tension_)/2;
                a0 =  2*mu3 - 3*mu2 + 1;
                a1 =  mu3 - 2*mu2 + mu;
                a2 =  mu3 -   mu2;
                a3 = -2*mu3 + 3*mu2;
                return( a0*y1 + a1*m0 + a2*m1 + a3*y2 );
              }
          }
        }; // HermiteInterpolation


        /// Cubic or Hermite interpolation worker
        template<
            typename YInputIterator,
            typename XInputIterator,
            typename OutputIterator,
            typename TFunctor
            >
        static void interpolateCubicHermite
        (
            YInputIterator begY,
            YInputIterator endY,
            XInputIterator begX,
            XInputIterator endX,
            OutputIterator out, //!< interpolated values, same length as x.
            TFunctor & functor, //!< either CubicInterpolate or HermiteInterpolate
            int start_index = 0
            )
        {
          typedef typename std::iterator_traits<OutputIterator>::value_type TReal;
          //size_t nrX = std::distance( begX , endX );
          size_t nrY = std::distance( begY , endY );
          OutputIterator outI = out;

          for( unsigned int i = 0 ; begX != endX ; ++i , ++begX, ++outI )
            {
              double xd = *begX - start_index;
              int index = static_cast<int>(floor(xd));
              //interpolate
              TReal mu = xd - static_cast<double>(index);
              if(index < -1)
                {
                  *outI = *begY;
                }
              else if(index == -1)
                {
                  TReal y1 = *begY;
                  TReal y2 = *begY;
                  TReal y3 = *begY;
                  TReal y4 = *(begY+1);
                  *outI = functor(y1 , y2 , y3 , y4 , mu );
                }
              //extrapolate
              else if(index == 0 )
                {
                  TReal y1 = 0;
                  TReal y2 = *begY;
                  TReal y3 = *(begY+1);
                  TReal y4 = *(begY+2);
                  *outI = functor(y1 , y2 , y3 , y4 , mu );
                }
              else if( index > 0 && index < static_cast<int>(nrY - 2) )//the normal case
                {
                  YInputIterator begTmp = (begY + index - 1);
                  TReal y1 = *begTmp;
                  TReal y2 = *(begTmp + 1);
                  TReal y3 = *(begTmp + 2);
                  TReal y4 = *(begTmp + 3);
                  *outI = functor(y1 , y2 , y3 , y4 , mu );
                }
              else if(index == static_cast<int>(nrY-2) ) //you are getting out of range
                {
                  YInputIterator begTmp = (begY + index - 1);
                  TReal y1 = *begTmp;
                  TReal y2 = *(begTmp+1);
                  TReal y3 = *(begTmp+2);
                  TReal y4 = 0 ;
                  *outI = functor(y1 , y2 , y3 , y4 ,mu);
                }
              else if(index == static_cast<int>(nrY-1) ) //you are even farther out...
                {
                  YInputIterator begTmp = (begY + index - 1);
                  TReal y1 = *begTmp;
                  TReal y2 = *(begTmp+1);
                  TReal y3 = *(begTmp+1);
                  TReal y4 = *(begTmp+1);
                  *outI = functor(y1 , y2 , y3 , y4 , mu );
                }
              else
                {
                  *outI = *(endY-1);
                }
            }//end for
        }//end interpolate_cubic

        /// Linear cubic interpolator worker
        template <
            typename YInputIterator,
            typename XInputIterator,
            typename OutputIterator,
            typename TFunctor
            >
        static void interpolateLinearCosine
        (
            YInputIterator y_p, //!< y values equidistantly spaced. spacing is [0,1,2, .... ,len(y)]
            YInputIterator endY,
            XInputIterator x_p, //!< points to interpolate at
            XInputIterator endX,
            OutputIterator out_p, //!< interpolated values, same length as x.
            TFunctor & interpolator, //!< interpolation functor, either: CosineInterpolate, LinearInterpolate.
            int start_index = 0 //!< if y values are placed on a grid with start_index != 0
            )
        {
          typedef typename std::iterator_traits<OutputIterator>::value_type TReal ;
          size_t nrX = std::distance(x_p,endX);
          size_t nrY = std::distance(y_p,endY);
          TReal xd;

          for(unsigned int i = 0 ; i < nrX; ++i, ++x_p, ++out_p)
            {
              xd = * x_p - start_index;
              double indexd = floor(xd);
              int index = static_cast<int>( indexd );
              assert(fabs (index - indexd) < 0.001);

              //interpolate
              if(index < 0 )
                {
                  *out_p = *y_p;
                }else if( index < static_cast<int>(nrY-1) )
                {
                  TReal mu = xd - indexd;
                  YInputIterator y1_p = (y_p + index);
                  TReal y1 = *y1_p;
                  TReal y2 = *(++y1_p);
                  *out_p = interpolator(y1,y2,mu);
                }
              else
                {
                  *out_p = *(y_p + (nrY-1));
                }
            }//end for
        }//end interpolate cubic
      }//end utilities
    }//end base
  }//base
}//ralab

#endif
