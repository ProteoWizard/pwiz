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


#ifndef MASSCOMPAREFUNCTORS_H
#define MASSCOMPAREFUNCTORS_H

#include <stdlib.h>
#include <cstdio>
#include <complex>
/*! \file MassCompareFunctors.h
Defines function objects which are used by SortedMatcher and UnsortedMatcher.

*/
namespace ralab
{

  namespace base
  {
    namespace resample
    {


      inline double resolution2ppm(double resolution){
         return 1/resolution * 1e6;
      }

     
     ///TODO Do checking on TReal thats a real
      template<typename TReal>
      struct PPMCompFunctor
      {
        typedef TReal value_type;
        value_type window_;
        value_type ppm_;

        PPMCompFunctor(value_type window //!< in ppm
                       ):window_(window),ppm_(1e-6)
        {}

        /// returns window at mass
        inline value_type operator()(value_type val)
        {
          return((window_ * val)*ppm_);
        }

        /// if dist pval cval smaller then window returns true
        inline bool operator()(value_type pval, value_type cval)
        {
          return( std::abs(pval - cval) < operator()(pval) );
        }
      };

      /// Da Comparator - constant mass error
      template<typename TReal>
      struct DaCompFunctor
      {
        typedef TReal value_type;
        value_type window_;
        DaCompFunctor(value_type window) : window_(window)
        {
        }

        ///  window at mass
        inline value_type operator()(value_type /*val*/)
        {
          return( window_ );
        }

        /** if dist pval cval smaller then window returns true */
        inline bool operator()(value_type pval, value_type cval)
        {
          return( std::abs(pval - cval)   < operator()(pval) );
        }
      };

      /// FTMS Comparator
      template<typename TReal>
      struct FTMSCompFunctor
      {
        typedef TReal value_type;
        value_type window_;

        value_type mass_;
        value_type invR_;//FTMS resolution

        /// brief window at mass, i.e. 0.1 Da at 400Da
        FTMSCompFunctor( value_type window , value_type mass ) : window_(window) ,  mass_(mass)
        {
          invR_ = sqrt(window_)/mass_;
        }

        /// brief returns size of windows for this mass
        inline value_type operator()(value_type val)
        {
          value_type pR = (val*invR_);
          return( pR*pR );
        }
        /// brief compares two masses, returns true if they match false otherwise
        inline bool operator()(value_type pval, value_type cval)
        {
          return( std::abs( pval - cval )   <  operator()(pval) );
        }
      };

    }//end resample
  }//end MSALGO
}//end ralab


#endif // MASSCOMPAREFUNCTORS_H
