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

#ifndef DIFF_H
#define DIFF_H

#include <math.h>
#include <algorithm>
#include <vector>
#include <functional>
#include <numeric>
#include <assert.h>

namespace ralab
{
  namespace base
  {
    namespace base
    {
      /*!  DIFF Lagged and iterated differences.

                        for more detials see R::base::diff <br>
                        diff(x, ...) <br>
                        ## Default S3 method: <br>
                        diff(x, lag = 1, differences = 1, ...) <br>


                        */
      /*! \brief lagged differences

                        \return .end() Iterator in destination container.

                        */
      template <
          typename InputIterator,
          typename OutputIterator,
          typename TN // = int32_t
          >
      OutputIterator diff
      (
          InputIterator begin, //!< [in] begin
          InputIterator end, //!< [in] end
          OutputIterator destBegin, //!< [out] dest begin
          TN lag//!< [in] an integer indicating which lag to use.
          )
      {
	typedef typename InputIterator::value_type vtype;
        return( std::transform(begin + lag
                               , end
                               , begin
                               , destBegin
                               , std::minus< vtype >())
                );
      }

      /*! \brief lagged difference

                        The result of the computation is performed in place!
                        \return - .end() in result container.
                        */
      template <typename InputIterator,
	 typename TN// = int32_t 
	>
      InputIterator diff
      (
          InputIterator begin, //!< begin
          InputIterator end, //!< end
          TN lag, //!< An integer indicating which lag to use.
          TN differences //!< An integer indicating the order of the difference.
          )
      {
		  if(std::distance( begin,end ) <= static_cast<int>(lag * differences) ){
          return(begin);
		  }
		  else{
        TN i = TN();
        InputIterator itmp(end) ;
        while(differences > i )
          {

            itmp = std::transform(
                  begin + lag ,
                  itmp ,
                  begin ,
                  begin ,
                  std::minus<typename InputIterator::value_type >()
                  ) ;
            ++i ;
          }
        return(itmp);
		  }
      }

    }//end base
  }//end ralab
}


#endif // DIFF_H
