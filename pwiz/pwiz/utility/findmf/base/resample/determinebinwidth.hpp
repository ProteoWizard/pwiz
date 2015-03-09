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
#ifndef DETERMINEBINWIDTH_H
#define DETERMINEBINWIDTH_H


#include <cmath>
#include <boost/assert.hpp>
#include <boost/range/algorithm_ext.hpp>
#include <boost/bind.hpp>
#include <algorithm>

#include "pwiz/utility/findmf/base/base/diff.hpp"
#include "pwiz/utility/findmf/base/resample/utilities/determinebinwidth.hpp"


namespace ralab
{
	namespace base
	{
		namespace resample
		{
			template<typename TReal>
			struct SquareRoot{
				TReal operator()(TReal x) const{
					return(sqrt(x));
				}
			};

			struct SamplingWith{
				std::vector<double> diff_;
				std::vector<double> summ_;
				std::vector<double> am_;

				//expects a sorted sequence
				template<typename TRealI>
				double operator()(TRealI begin, TRealI end)
				{
					//BOOST_ASSERT(!boost::range::is_sorted(begin,end));
					typedef typename std::iterator_traits<TRealI>::value_type TReal;
					std::size_t N = std::distance(begin,end);
					double am;
					if(N > 1){
						diff_.resize(N-1);
						summ_.resize(N-1);
						am_.resize(N-1);
						ralab::base::base::diff(begin,end,diff_.begin(),1);

						utilities::summ( begin , end, summ_.begin(),1);
						//square the sum
						//std::transform(summ_.begin(),summ_.end(),summ_.begin(),boost::bind(sqrt,_1));
						std::transform(summ_.begin(),summ_.end(),summ_.begin(),SquareRoot<TReal>());
						std::transform(diff_.begin(),diff_.end(),summ_.begin(),am_.begin(),std::divides<double>());
						std::sort(am_.begin(),am_.end());
						am = utilities::determine(am_.begin(),am_.end());
					}else{
						am = 0.;
					}
					return am;
				}
			};

		}
	}
}


#endif // DETERMINEBINWIDTH_H
