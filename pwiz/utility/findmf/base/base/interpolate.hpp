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


#ifndef EQUISPACEINTERPOLATION_H
#define EQUISPACEINTERPOLATION_H

#include <utility>
#include <limits>
#include <vector>
#include <assert.h>

#include "pwiz/utility/findmf/base/base/utilities/interpolation.hpp"


/// EQUISPACEINTERPOL Interpolation on a equidistantly spaced grid.
/// Interpolation on a equidistantly spaced grid. The Values y are located at a grid [0,1,..., len(y)].
///
/// For equidistantly spaced y at a grid [0,1,..., len(y)] the \f$ x_{i}\f$ and \f$x_{i+1}\f$ enclosing \f$x_{out}\f$
/// can be found efficiently by i = floor(xout). Furthermore for \f$ x_{i+1} - x_{i} = 1 \f$  the interpolation formulas are simple.
/// Therefore, w provided this specialized implementation of interpolation functions.
///  Each segment (bounded by two data points) can be interpolated independently.
/// The parameter mu defines where to estimate the value on the interpolated line, it is 0 at the first point and 1 and the second point.
/// For interpolated values between the two points mu ranges between 0 and 1. Values of mu outside this range result in extrapolation.
/// This convention is followed for all the subsequent methods below.


namespace ralab
{
  namespace base
  {
    namespace base
    {
     /// affine interpolation on equidistantly spaced y. The y's are located at 0,1,2....,len(y).
     ///  For x's < 0 or x's > len(y) y[0] or y[len(y) -1 ] is used.

      template <typename YInputIterator,
                typename XInputIterator,
                typename OutputIterator>
      void interpolate_linear(
          YInputIterator begY, //!< y values equidistantly spaced. spacing is [0,1,2, .... ,len(y)]
          YInputIterator endY,
          XInputIterator begX, //!< points to interpolate at
          XInputIterator endX,
          OutputIterator out, //!< interpolated values, same length as x.
          int start_index = 0, //!< if y values are placed on a grid with start_index != 0
          typename std::iterator_traits<OutputIterator>::value_type epsilon =
          std::numeric_limits<typename std::iterator_traits<OutputIterator>::value_type>::epsilon()
          )
      {
        typedef typename std::iterator_traits<OutputIterator>::value_type TReal;
        utilities::LinearInterpolate<TReal> functor(epsilon);
        utilities::interpolateLinearCosine(begY , endY , begX , endX , out , functor , start_index);
      }// end interpolate cubic



      /// cosine interpolation on equidistantly spaced y. The y's are located at 0,1,2....,len(y).
      /// For x's < 0 or x's > len(y) y[0] or y[len(y) -1 ] is used.

      template <typename YInputIterator,
                typename XInputIterator,
                typename OutputIterator
                >
      void interpolate_cosine(
          YInputIterator begY, //!< y values equidistantly spaced. spacing is [0,1,2, .... ,len(y)]
          YInputIterator endY,
          XInputIterator begX, //!< points to interpolate at
          XInputIterator endX,
          OutputIterator out, //!< interpolated values, same length as x.
          int start_index = 0 //!< if y values are placed on a grid with start_index != 0
          )
      {
        typedef typename std::iterator_traits<OutputIterator>::value_type TReal;
        utilities::CosineInterpolate<TReal> functor;
        utilities::interpolateLinearCosine(begY,endY ,begX,endX, out,functor, start_index);
      }// end interpolate cubic



      /// cubic interpolation on equidistantly spaced y's. The y's are located at 0,1,2....,len(y).
      /// For x's < 0 or x's > len(y) y[0] or y[len(y) -1 ] is used.

      template <typename YInputIterator,
                typename XInputIterator,
                typename OutputIterator>
      void interpolate_cubic(
          YInputIterator begY, //!< y values equidistantly spaced. spacing is [0,1,2, .... ,len(y)]
          YInputIterator endY,
          XInputIterator begX, //!< points to interpolate at
          XInputIterator endX,
          OutputIterator out, //!< interpolated values, same length as x.
          int start_index = 0, //!< if y values are placed on a grid with start_index != 0
          typename std::iterator_traits<OutputIterator>::value_type epsilon =
          std::numeric_limits<typename std::iterator_traits<OutputIterator>::value_type>::epsilon()
          )
      {
        typedef typename std::iterator_traits<OutputIterator>::value_type TReal;
        utilities::CubicInterpolate<TReal> functor(epsilon);
        utilities::interpolateCubicHermite(begY,endY ,begX,endX, out,functor, start_index);
      }// end interpolate cubic


      /// Hermite interpolation on equidistantly spaced y's. The y's are located at 0,1,2....,len(y).
      ///  For x's < 0 or x's > len(y) y[0] or y[len(y) -1 ] is used.

      template <
          typename YInputIterator,
          typename XInputIterator,
          typename OutputIterator
          >
      void interpolate_Hermite(
          YInputIterator begY, //!< y values equidistantly spaced. spacing is [0,1,2, .... ,len(y)]
          YInputIterator endY,
          XInputIterator begX, //!< points to interpolate at
          XInputIterator endX,
          OutputIterator out, //!< interpolated values, same length as x.
          double tension = 0, //!< 1 is high, 0 normal, -1 is low
          double bias = 0, //!< 0 is even, positive is towards first segment, negative towards the other
          int start_index = 0, //!< if y values are placed on a grid with start_index != 0
          typename std::iterator_traits<OutputIterator>::value_type epsilon =
          std::numeric_limits<typename std::iterator_traits<OutputIterator>::value_type>::epsilon()
          )
      {
        typedef typename std::iterator_traits<OutputIterator>::value_type TReal;
        utilities::HermiteInterpolate<TReal> functor(tension, bias, epsilon);
        utilities::interpolateCubicHermite( begY , endY , begX , endX , out , functor , start_index );
      }// end interpolate cubic
    }//base
  }//namespace base
}//namespace ralab


#endif // EQUISPACEINTERPOLATION_H
