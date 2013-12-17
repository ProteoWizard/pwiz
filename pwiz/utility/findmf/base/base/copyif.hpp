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

#ifndef COPY_IF_H
#define COPY_IF_H

#include <iterator>

namespace ralab
{
  namespace base
  {
    namespace utils
    {

      template <class InputIterator,class InputIterator2, class OutputIterator, class Predicate>
      OutputIterator copy_if(
          InputIterator first,
          InputIterator last,
          InputIterator2 source,
          OutputIterator result,
          Predicate pred)
      {
        while(first!=last)
          {
            if(pred(*first))
              {
                *result = *source;
                ++result;
              }
            ++first;
            ++source;
          }
        return result;
      }

      template <class InputIterator,class InputIterator2, class OutputIterator, class Predicate>
      OutputIterator copy_if_not(
          InputIterator first,
          InputIterator last,
          InputIterator2 source,
          OutputIterator result,
          Predicate pred)
      {
        while(first!=last)
          {
            if(!pred(*first))
              {
                *result = *source;
                ++result;
              }
            ++first;
            ++source;
          }
        return result;
      }

      /*! \brief copy_if

             Implementation of copy_if as suggested
             in Efficient STL (Scott Meyers) item 37.
            */
      template < typename InputIterator,
                 typename OutputIterator,
                 typename Predicate >
      OutputIterator copy_if(
          InputIterator begin,
          InputIterator end,
          OutputIterator destBegin,
          Predicate p)
      {

        while(begin != end)
          {
            typename std::iterator_traits<InputIterator>::reference r= *begin;
            if(p(r))
              {
                *destBegin = r;
                ++destBegin;
              }
            ++begin;
          }
        return destBegin;
      }

      /*! \brief copy_if_not for containers

                        Implementation of copy_if as suggested
                        in Efficient STL (Scott Meyers) item 37.
                        */
      template < typename InputIterator,
                 typename OutputIterator,
                 typename Predicate >
      OutputIterator copy_if_not(
          InputIterator begin,
          InputIterator end,
          OutputIterator destBegin,
          Predicate p
          )
      {

        while(begin != end)
          {
            typename std::iterator_traits<InputIterator>::reference r= *begin;
            if(!p(r))
              {
                *destBegin = r;
                ++destBegin;
              }
            ++begin;
          }
        return destBegin;
      }


    }//end utils
  }//end base
}//end ralab



#endif // COPY_IF_H
