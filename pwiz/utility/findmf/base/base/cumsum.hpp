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


#ifndef CUMSUM_H
#define CUMSUM_H
#include <vector>
#include <cstddef>
/*! \file cumsum.h
\brief functions for package base: cumsum, cumprod, cummax, cummin

*/

namespace ralab
{
  namespace base
  {

      /*!  CUMSUM Returns a vector whose elements are the cumulative sums, products, minima or maxima of the elements of the argument.*/

      template<typename TIterator, typename T>
      void cumsum(
          TIterator  beg,//!< [in] vector of values of type T
          TIterator end,
          std::vector<T> & res //!<[out] cumulative sum
          )
      {
		  if(beg!=end){
          
        res.assign(beg,end);
        typename std::vector<T>::iterator begRes = res.begin();
        typename std::vector<T>::iterator begResDelayed = begRes;
        ++begRes;

        typename std::vector<T>::iterator begEnd = res.end();
        for( ;begRes != begEnd ; ++begRes, ++begResDelayed)
          {
            *begRes += *(begResDelayed) ;
          }
		  }
      }

      //in place version of cumulative sum.
      template<typename TIterator>
      TIterator cumsum(
          TIterator  beg,//!< [in] vector of values of type T
          TIterator end //<! [in] end iterator of sequence
          )
      {
        TIterator begRes = beg;
        ++begRes;
        for( ;begRes != end ; ++begRes, ++beg)
          {
            *begRes += *(beg) ;
          }
        return begRes;
      }


      /*! \brief Returns a vector whose elements are the cumulative products of the elements of the argument. */
      template<typename T>
      void cumprod(std::vector<T> & x,//!< [in]
                   std::vector<T> & res //!< cumulative product
                   )
      {
        res.resize(x.size());
        for(size_t i = 0; i < x.size() ; ++i)
          {
            res[i] = x[i];
          }
        for(size_t i = 1; i < x.size(); ++i)
          {
            res[i] *= res[i-1] ;
          }
      }

      /*! \brief Returns a vector whose elements are the cumulative maximum of the elements of the argument. */
      template<typename T>
      void cummax(
          std::vector<T> & x, //!<[in] vector of type T
          std::vector<T> & res //!<[out] cumulative maximum
          )
      {
        res.resize(x.size());
        for(size_t i = 0; i < x.size() ; ++i)
          {
            res[i] = x[i];
          }
        for(size_t i = 1; i < x.size(); ++i)
          {
            res[i] = std::max( res[i-1] , res[i]) ;
          }
      }

      /*! \brief Returns a vector whose elements are the cumulative sums, products, minima or maxima of the elements of the argument. */
      template<typename T>
      void cummin(
          std::vector<T> & x, //!<[in] vector of type T
          std::vector<T> & res //!<[in] cumulative minimum
          )
      {
        res.resize(x.size());
        for(size_t i = 0; i < x.size() ; ++i)
          {
            res[i] = x[i];
          }
        for(size_t i = 1; i < x.size(); ++i)
          {
            res[i] = std::min( res[i-1] , res[i]) ;
          }
      }

    }// end base
}//end namespace ralab

#endif // CUMSUM_H
