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


#ifndef BUCKET1D_H
#define BUCKET1D_H

#include <vector>
#include <stdexcept>
#include <boost/cstdint.hpp>

#include "pwiz/utility/findmf/base/resample/bin1d.hpp"


namespace ralab{
  namespace base{
    namespace resample{
    /*!\given breaks and data compute frequencies in bins*/
typedef boost::uint32_t uint32_t;
struct Bucket1D
    {
    private:
      std::vector<double> breaks_; // boundaries
      std::vector<uint32_t> indicator_;//Has length length(breaks_) - 1, and indicates if bin is of interest.
      //this code is required to avoid bound checking in release.
      //Some stupid bruker compiler settings.
      double * begbreaks_;
      double * endbreaks_;
      uint32_t * indicatorptr_;
    public:
      /*!\brief CTor*/
      Bucket1D(
          std::vector<double> & breaks, // breaks
          std::vector<uint32_t> & indic // indicator
          ):breaks_(), indicator_(), begbreaks_(0), endbreaks_(0), indicatorptr_(0)
      {
        set( breaks , indic );
      }

      /*!\brief CCTor*/
      Bucket1D(
          const Bucket1D & rhs
          ):breaks_(), indicator_(), begbreaks_(0), endbreaks_(0), indicatorptr_(0)
      {
        this->set(rhs.breaks_,rhs.indicator_);
      }

    private:
      /*!\brief  set the data*/
      void set( const std::vector<double> & breaks,
                const std::vector<uint32_t> & indic )
      {
        if(( breaks.size() - 1 ) != indic.size()){
            throw std::out_of_range( "breaks.size == inic + 1 , failed!" );
          }
        breaks_ = breaks;
        begbreaks_ = &breaks_[0];
        endbreaks_ = begbreaks_ + breaks_.size();
        indicator_ = indic;
        indicatorptr_ = &indicator_[0];
      }

    public:

      /*! \Assignment */
      Bucket1D& operator=(const Bucket1D &rhs)
      {
        if (this == &rhs) // protect against invalid self-assignment
          return *this; // See "4:"
        this->set( rhs.breaks_, rhs.indicator_);
        return *this;
      }

      /*!\brief
                    The result tells you in which bucket which input should end up.
                    */
      template<typename InputIterator>
      void operator()(
          InputIterator beg, //!< Check wich of these masses should be bucketed
          InputIterator end, //!<
          std::vector<std::pair<std::size_t, std::size_t> > & bucketPairs //indicates an successful assignment, first:  index in bucket second: index in input
          )
      {
        std::size_t index(0);
        std::pair<std::size_t, bool> res;
        for(;beg !=end; ++beg, ++index)
          {
            res = this->operator()(*beg);
            if(res.second)
              {
                bucketPairs.push_back(std::make_pair(res.first , index ));
              }
          }
      }

      /*!\brief

                    the std::size_t indicates to which bucket dat belongs too.
                    The bool indicates if a new bucket is of interest
                    */
      std::pair<std::size_t, bool> operator()(double dat)
      {
        double * it2 = std::lower_bound(begbreaks_,endbreaks_,dat);
        std::size_t ub = std::distance(begbreaks_,it2);

        if(ub > 0 && ub <= indicator_.size())
          {
            ub = ub - 1;
            if(*(indicatorptr_ + ub) > 0)
              return std::make_pair(ub, true);
            else
              return std::make_pair(ub, false);
          }
        else
          return std::make_pair(0, false);
      }

    }; //Bucket1D
    }//namespace resample
  }//namespace base
}//namespace ralab

#endif // BUCKET1D_H
