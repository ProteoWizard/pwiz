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

#ifndef SIMPLEPICKER_H
#define SIMPLEPICKER_H

#include <iterator>
#include <vector>
#include <stdexcept>
#include <boost/lexical_cast.hpp>

namespace ralab
{
  namespace base
  {
    namespace ms
    {
      /*! computes first derivative of a sequence, looks for zero crossings
       */
      template<class TReal>
      struct SimplePicker{
        std::vector<TReal> worker_;
        double epsilon_;
        bool problem_; //indicates if not the whole signal was picked.

        SimplePicker(TReal epsilon = 1e-3):epsilon_(epsilon),problem_(false){}
        /*!
         *returns number of zero crossings found
         */
        template<typename Tit, typename Outit>
        size_t operator()(Tit beg, Tit end ,
                          Outit zerocrossings, //! picked peaks
                          size_t nzercross,
                          std::ptrdiff_t lag = 2 //must be even (leave odd out)
            )
        {
          if((lag % 2 ) == 1){
              return -1;
            }
          worker_.resize(std::distance(beg,end) - lag);
          TReal * pworkerBeg = &worker_[0];
          TReal * pworkerEnd = &worker_[0] + worker_.size();

          Tit tbegin = beg;
          Tit tbeginm1 = tbegin + ( lag);
          for(;tbeginm1 != end ; ++tbeginm1, ++tbegin, ++pworkerBeg  )
            {
              *pworkerBeg = (*tbeginm1 - *tbegin);
            }

          //reset worker
          pworkerBeg = &worker_[0];
          std::size_t crosscount = 0;
          for( int i = 0 ; (pworkerBeg != pworkerEnd-1) ; ++pworkerBeg , ++i )
            {
              if(crosscount >= nzercross){
                  problem_ = true;
                  return crosscount; // protect against memmory violations
                  std::string x = "nzerocross:";
                  x+=boost::lexical_cast<std::string>(nzercross);
                  x+=" crosscount:";
                  x+=boost::lexical_cast<std::string>(crosscount);
                  x+=" i: ";
                  x+= boost::lexical_cast<std::string>(i);
                  x+=" worker size ";
                  x+= boost::lexical_cast<std::string>( worker_.size() );
                  x+=" : ";
                  x+=boost::lexical_cast<std::string>(__LINE__);
                  x+=" : ";
                  x+= __FILE__;
                  throw std::length_error(x.c_str());
                }
              TReal v1 = (*pworkerBeg);
              TReal v2 = *(pworkerBeg + 1);
              //peak detected ... detect a zero crossing
              if((v1 > 0 && v2 < 0) && ((v1 - v2) > epsilon_))
                {
                  //determine zero crossing....
                  double frac = v1 / ( v1 - v2 );
                  double idx = static_cast<float>(i + lag/2) + frac;
                  *zerocrossings = ( idx );
                  ++zerocrossings;
                  ++crosscount;
                }else if( v1 > 0 && v2  == 0 ){
                  TReal v3 = *(pworkerBeg + 2);
                  if((v3 < 0) && ((v1 - v3) > epsilon_)){
                      *zerocrossings = (i + lag/2 + 1.);
                    }
                }else{
                  //just continue, nothing to handle...
                }
            }
          return crosscount;
        }

        bool getProblem() const{
          return problem_;
        }

      };
    }//ms
  }//base
}//ralab

#endif // SIMPLEPICKER_H
