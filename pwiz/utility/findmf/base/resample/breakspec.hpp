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


#ifndef BREAKSSPEC_H
#define BREAKSSPEC_H

#include <vector>

namespace ralab
{
  namespace base
  {
    namespace resample{

      /*! \brief Segment mass range according to Mass Compare functor
                could be used to histogram a dataset or to compute minima for each segment
                in order to perform baseline subtraction.

                */
      template<typename TMassComparator>
      void breaks
      (
          double minMass,
          double maxMass,
          TMassComparator tmassComp,
          std::vector<double> & breaks,
          bool exact = false
          )
      {
        double startMass;
        double endMass = maxMass;
        if(exact)
          {
            startMass = minMass;
          }
        else
          {
            startMass = minMass - tmassComp(minMass)/2.;
          }

        breaks.push_back(startMass);
        do
          {
            startMass = startMass + tmassComp(startMass);
            breaks.push_back(startMass );
          }while(	startMass < endMass);

        if(exact)
          {
            breaks.back() = endMass;
          }
      }

      /*\compute mids for breaks
                */
      template<
          typename TInputIterator,
          typename TOutputIterator
          >
      TOutputIterator getMids(TInputIterator breaksBeg,
                              TInputIterator breaksEnd,
                              TOutputIterator midsBeg
                              )
      {
        typedef typename std::iterator_traits<TInputIterator>::value_type TReal;
        TReal oldval = *breaksBeg;
        TReal divider = TReal(0.5);
        ++breaksBeg;
        for( ; breaksBeg != breaksEnd; ++breaksBeg, ++midsBeg )
          {
            TReal newval = *(breaksBeg);
            *midsBeg = (oldval + newval)*divider;
            oldval = newval;
          }
        return midsBeg;
      }

      /*!\brief Dummy version of getMids*/
      inline void getMids(
          const std::vector<double> &breaks,
          std::vector<double> & mids
          )
      {
        mids.resize(breaks.size()-1);
        getMids(breaks.begin(), breaks.end(),  mids.begin() );
      }
    }//end resample
  }//end base
}//end ralab


#endif // BREAKSSPEC_H
