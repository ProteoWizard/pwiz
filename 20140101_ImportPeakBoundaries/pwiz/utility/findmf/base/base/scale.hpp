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

#ifndef SCALE_H
#define SCALE_H

#include <math.h>
#include <algorithm>
#include <vector>
#include <functional>
#include <numeric>
#include <iterator>
#include "pwiz/utility/findmf/base/base/base.hpp"

namespace ralab
{
  namespace base
  {
    namespace stats
    {


      template<int N, typename TReal>
      struct NthPower : std::unary_function<TReal,TReal>
      {
        TReal operator()(const TReal & x)
        {
          TReal ret = x;
          for (int i=1; i < N; ++i) {
              ret *= x;
            }
          return ret;
        }
      };


      /*!\brief The root-mean-square for a column is obtained by computing the square-root of the sum-of-squares of the non-missing values in the column divided by the number of non-missing values minus one.  */
      template<typename InputIterator>
      typename std::iterator_traits<InputIterator>::value_type
      rootMeanSquare(
          const InputIterator begin, //!< [in] start iterator
          const InputIterator end //!< [in] end iterator
          )
      {
        typedef typename std::iterator_traits<InputIterator>::value_type TReal;
        std::vector<TReal> x(begin,end);

        std::transform( x.begin(), x.end(), x.begin(), NthPower<2,TReal>() ); //first sqaure all elements
        TReal sum = std::accumulate(x.begin(), x.end() , TReal(0.));
        sum = sum/static_cast<TReal>(x.size() - size_t(1));
        return(sqrt(sum));
      }


      /**
        scale centers and/or scales all values from begin in to end.
      */
      template<typename InputIterator>
      void scale(
          InputIterator begin,
          InputIterator end,
          std::pair<typename std::iterator_traits<InputIterator>::value_type,typename std::iterator_traits<InputIterator>::value_type> & scaled, //!<[out] scaled.first = center, scaled.second = scale
          bool center = true,//!<[in] either a logical value or a numeric vector of length equal to the number of columns of x.
          bool scale = true //!<[in] 	either a logical value or a numeric vector of length equal to the number of columns of x.
          )
      {
        typedef typename std::iterator_traits<InputIterator>::value_type TReal;
        std::vector<TReal> tmp;

        if(center)
          {
            scaled.first = ralab::base::base::mean( begin , end);
            std::transform(begin, end, begin, std::bind2nd( std::minus<TReal>(), scaled.first));
          }
        else
          {
            scaled.first = std::numeric_limits<TReal>::quiet_NaN();
          }
        if(scale)
          {
            scaled.second = rootMeanSquare( begin , end );
            std::transform(begin, end, begin , std::bind2nd(std::divides<TReal>(), scaled.second) );
          }
        else
          {
            scaled.second = std::numeric_limits<TReal>::quiet_NaN();
          }
      }

    }
  }
}

#endif


