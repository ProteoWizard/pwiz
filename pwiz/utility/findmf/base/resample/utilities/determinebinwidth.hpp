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
#ifndef DETERMINEBINWIDTHUTILITIES_H
#define DETERMINEBINWIDTHUTILITIES_H

#include <functional>
#include <boost/cstdint.hpp>
#include <algorithm>

namespace ralab
{
  namespace base
  {
    namespace resample
    {

      typedef boost::int32_t int32_t;
      namespace utilities{

        template<class T>
        struct meanfunctor : std::binary_function<T,T,T>{
          T operator()(const T & x, const T& y){
            return (x+y)/2.;
          }
        };


        template <
            typename InputIterator,
            typename OutputIterator,
            typename TN //= int32_t
            >
        OutputIterator summ
        (
            InputIterator begin, //!< [in] begin
            InputIterator end, //!< [in] end
            OutputIterator destBegin, //!< [out] dest begin
            TN lag = 1//!< [in] an integer indicating which lag to use.
            )
        {
          return( std::transform(begin + lag
                                 , end
                                 , begin
                                 , destBegin
                                 , meanfunctor<typename InputIterator::value_type>())
                  );
        }

        template<typename TRealI>
        double determine(TRealI begin, TRealI end,double maxj=5.){
          //BOOST_ASSERT(!boost::range::is_sorted(begin,end));
          double j = 1.;
          double average = *begin;
          double sum = average;
          int32_t i = 1;
          for(; begin != end ; ++begin, ++i){
              while(*begin > (j+0.5) *average){
                  ++j;
                }
              if(j > maxj){
                  break;
                }
              sum += *begin/j;
              average = sum/static_cast<double>(i);
            }
          return average;

        }
      }


    }
  }
}
#endif
