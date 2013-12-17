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


#ifndef BIN1D_H
#define BIN1D_H

#include <vector>
#include <algorithm>
#include <functional>
#include <vector>
#include <utility>
#include <stdexcept>
#include <boost/cstdint.hpp>

namespace ralab
{
  namespace base
  {
    namespace resample
    {
      typedef boost::int32_t int32_t;
      typedef boost::int64_t int64_t;

      //Class returns the index a value belongs too....
      struct Bin1D
      {
        std::vector<double> breaks_; // boundaries
        double * begbreaks_;
        double * endbreaks_;
      public:
        Bin1D():breaks_(),begbreaks_(0),endbreaks_(0){}
        
        Bin1D(
            std::vector<double> & breaks // breaks
            ):breaks_(), begbreaks_(0), endbreaks_(0)
        {
          this->setBreaks(breaks);
        }

        
        Bin1D(
            const Bin1D & rhs // breaks
            ):breaks_(), begbreaks_(0), endbreaks_(0)
        {
          this->setBreaks(rhs.breaks_);
        }


        void setBreaks(const std::vector<double> & breaks){
          breaks_.assign( breaks.begin(), breaks.end() );
          std::sort(breaks_.begin(),breaks_.end());
          reset();
        }

        void reset(){
          begbreaks_ = &breaks_[0];
          endbreaks_ = begbreaks_ + breaks_.size();
        }

        void getBreaks(std::vector<double> & breaks) const{
          breaks.assign(begbreaks_,endbreaks_);
        }

        const std::vector<double> & getBreaks() const{
          return breaks_;
        }

        
        bool inRange(double dat) const{
          return (dat > breaks_.front() && dat < breaks_.back());
        }

        std::size_t operator()(double dat) const
        {
          double * it2 = std::lower_bound(begbreaks_,endbreaks_,dat);
          size_t ub = std::distance(begbreaks_,it2);
          return ub;
        }

        void operator()(double dat1,
                        double dat2,
                        std::vector<int32_t> & idx,
                        std::vector<double> & dist
                        ) const
        {
          double * it1 = std::lower_bound(begbreaks_,endbreaks_,dat1);
          double * it2 = std::lower_bound(begbreaks_,endbreaks_,dat2);

          size_t ub1 = std::distance(begbreaks_,it1);
          size_t ub2 = std::distance(begbreaks_,it2);
          int64_t n = static_cast<int64_t>(ub2-ub1)+1;
          idx.resize(n);
          dist.resize(n);

          if(ub1 == ub2 ){
              idx[0] = ub1-1;
              dist[0] = dat2 - dat1;
              return;
            }
          else{
              for(int64_t i = 0; it1 != (it2+1) ; i++, it1++){
                  idx[i] = ub1 + i - 1;
                  if(i == 0)
                    {
                      dist[i] = *(it1) - dat1;
                    }
                  else if( i < n - 1 )
                    {
                      dist[i] = *(it1) - *(it1-1);
                    }
                  else
                    {
                      dist[i] = dat2 - *(it1-1);
                    }
                }
            }

        }
      };

    }//resample
  }//base
}//ralab



#endif // BUCKET1D_H
