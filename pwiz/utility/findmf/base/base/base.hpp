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

#ifndef BASE_H
#define BASE_H

#include <math.h>
#include <algorithm>
#include <vector>
#include <functional>
#include <numeric>
#include <stdexcept>
#include <limits>
#include <iterator>
#include <cmath>
#include <string>
#include <boost/utility/enable_if.hpp>
#include <boost/type_traits/is_integral.hpp>
#include "pwiz/utility/findmf/base/base/utilities/base.hpp"

namespace ralab
{
  namespace base
  {
    namespace base
    {

      /// generates the sequence from, from+/-1, ..., to (identical to from:to).
      template<typename TReal>
      void seq
      (
          TReal from, //!<[in] the starting  value of the sequence
          TReal to, //!<[in] the end value of the sequence
          std::vector<TReal> &result //!<[out] result sequence
          )
      {
        result.clear();
        typedef typename std::vector<TReal>::size_type size_type;
        if( from <= to )
        {
          size_type length = static_cast<size_type>(to - from) + 1;
          result.resize(length);
          std::generate(result.begin() , result.end() , utilities::SeqPlus<TReal>(from));
        }
        else
        {
          size_type length = static_cast<size_type>(from - to) + 1 ;
          result.resize(length);
          std::generate(result.begin() , result.end() , utilities::SeqMinus<TReal>(from));
        }
      }

      /// generates sequence: from, from+by, from + 2*by, ..., up to a sequence value less than, or equal than to.
      /// Specifying to < from and by of positive sign is an error.
      template<typename TReal>
      void seq(
          TReal from,//!<[in] the starting value of the sequence
          TReal to,//!<[in] the end value of the sequence
          TReal by, //!<[in] number: increment of the sequence
          std::vector<TReal> &result //!<[out] result sequence
          )
      {
        result.clear();
        typedef typename std::vector<TReal>::size_type size_type;
        size_type size = static_cast<size_type>(  (to - from)  / by ) + 1u ;
        result.reserve( size );

        if(from <= to)
        {
          if(!(by > 0)){
            throw std::logic_error(std::string( "by > 0" ));
          }
          for(; from <= to; from += by)
          {
            result.push_back(from);
          }
        }
        else
        {
          if(! (by < 0) ){
            throw std::logic_error(std::string( "by < 0" ));
          }
          for(; from >= to; from += by)
          {
            result.push_back(from);
          }
        }
      }

      /// generates sequence: from, to of length
      /// calls seq with \$[ by = ( ( to - from ) / (  length  - 1. ) ) \$]
      template<typename TReal>
      void seq_length(
          TReal from,//!<[in] the starting value of the sequence
          TReal to,//!<[in] the end value of the sequence
          unsigned int length, //!<[in] length of sequence
          std::vector<TReal> &result //!<[out] result sequence
          )
      {
        TReal by = ( ( to - from ) / ( static_cast<TReal>( length ) - 1. ) );
        seq(from, to, by, result);

        //this is required because of machine precision...
        // sometimes by does not add's up to precisely _to_ nad
        if(result.size() < length)
        {
          result.push_back(result[result.size()-1] + by );
        }
        else
        {
          result.resize(length);
        }
      }

      /// generates the sequence [1, 2, ..., length(ref)]
      /// (as if argument along.with had been specified),
      /// unless the argument is numeric of length 1 when it is interpreted as
      ///  1:from (even for seq(0) for compatibility with S).

      template< typename T1 , typename T2 >
      void seq
      (
          std::vector<T1> & ref, //!<[in] take the length from the length of this argument.
          std::vector<T2> & res //!<[out] result sequence
          )
      {
        T2 t(1);
        res.assign(ref.size() , t );
        std::partial_sum( res.begin() , res.end() , res.begin() );
      }

      /// Generates Sequence 1,2,3,....length .
      /// Generates 1, 2, ..., length unless length.out = 0, when it generates
      /// integer(0).

      template<typename TSize, typename TReal>
      typename boost::enable_if<boost::is_integral<TSize>, void>::type
      seq(
          TSize length, //!< [in] length of sequence
          std::vector<TReal> &res //!< [out] result sequence
          )
      {
        TReal t(1);
        res.assign(length , t );
        std::partial_sum(res.begin(),res.end(),res.begin());
      }


      /// MEAN Trimmed arithmetic mean.

      /// ## Default S3 method:
      /// mean(x, trim = 0, na.rm = FALSE, ...)

      /// Arguments
      /// x 	An R object. Currently there are methods for numeric data frames, numeric vectors and dates. A complex vector is allowed for trim = 0, only.
      /// trim 	the fraction (0 to 0.5) of observations to be trimmed from each end of x before the mean is computed. Values outside that range are taken as the nearest endpoint.
      /// na.rm 	a logical value indicating whether NA values should be stripped before the computation proceeds.
      /// ... 	further arguments passed to or from other methods.

      /// Value
      /// For a data frame, a named vector with the appropriate method being applied column by column.
      /// If trim is zero (the default), the arithmetic mean of the values in x is computed, as a numeric or complex vector of length one. If x is not logical (coerced to numeric), integer, numeric or complex, NA is returned, with a warning.
      /// If trim is non-zero, a symmetrically trimmed mean is computed with a fraction of trim observations deleted from each end before the mean is computed.


      template < typename InputIterator >
      inline
      typename std::iterator_traits<InputIterator>::value_type
      mean(
          InputIterator begin, //!< [in]
          InputIterator end //!< [in]
          )
      {
        typedef typename std::iterator_traits<InputIterator>::value_type TReal;
        TReal size = static_cast<TReal>(std::distance(begin,end));
        TReal sum = std::accumulate(begin , end, 0. );
        return(sum/size);
      }

      /// mean
      template <typename TReal>
      inline TReal mean(const std::vector<TReal> & x )
      {
        TReal size = static_cast<TReal>(x.size());
        TReal sum = std::accumulate(x.begin() , x.end(), 0. );
        return ( sum / size ) ;
      }

      /// mean
      template <typename TReal>
      TReal mean(
          const std::vector<TReal> & x, //!< [in]
          TReal trim  //!< [in] trim 0 - 0.5
          )
      {
        if(trim >= 0.5)
        {
          trim = 0.4999999;
        }
        TReal size = static_cast<TReal>(x.size());
        std::vector<TReal> wc(x); //working copy
        std::sort(wc.begin(),wc.end());
        size_t nrelemstrim = static_cast<size_t>(round( size * trim )) ;
        size_t nrelems = std::distance(wc.begin() + nrelemstrim, wc.end() - nrelemstrim );
        TReal sum = std::accumulate(wc.begin() + nrelemstrim , wc.end() - nrelemstrim, 0. );
        return ( sum / static_cast<TReal>( nrelems ) ); //static_cast will be required with boost::math::round
      }

      /// computes the mean
      template<class Iter_T>
      typename std::iterator_traits<Iter_T>::value_type geometricMean(Iter_T first, Iter_T last)
      {
        typedef typename std::iterator_traits<Iter_T>::value_type TReal;
        size_t cnt = distance(first, last);
        std::vector<TReal> copyOfInput(first, last);

        // ln(x)
        typename std::vector<TReal>::iterator inputIt;

        for(inputIt = copyOfInput.begin(); inputIt != copyOfInput.end() ; ++inputIt)
        {
          *inputIt = std::log(*inputIt);
        }

        // sum(ln(x))
        TReal sum( std::accumulate(copyOfInput.begin(), copyOfInput.end(), TReal() ));

        // e^(sum(ln(x))/N)
        TReal geomean( std::exp(sum / cnt) );
        return geomean;
      }


      /// Range of Values
      /// range returns a std::pair containing minimum and maximum of all the given values.
      template<typename TReal>
      void Range(
          const std::vector<TReal> & values, //!< [in] data
          std::pair<TReal,TReal> & range //!< [out] range
          )
      {
        TReal min = * std::min_element(values.begin(),values.end());
        TReal max = * std::max_element(values.begin(),values.end());
        range.first = min;
        range.second = max;
      }

      ///  maximum of 3 numbers
      template<typename T>
      inline double max3(T a, T b, T c)
      {
        T max;
        if(a>b)
          max=a;
        else
          max=b;
        if(max<c)
          max=c;
        return(max);
      }

      /// log base 2
      template<typename TReal>
      inline TReal log2(TReal test)
      {
        if(test==0)
          return(0);
        else
          return( log10( test ) / log10( static_cast<TReal>(2.) ));
      }

    }//namespace BASE ends here
  }//namespace base ends here
}//namespace ralab ends here

#endif // BASE_H
