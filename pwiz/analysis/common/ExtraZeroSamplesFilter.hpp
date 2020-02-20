//
// $Id$
//
//
// Original author: Brian Pratt <brian.pratt <a.t> insilicos.com>
//
// Copyright 2012  Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _EXTRAZEROSAMPLESFILTER_HPP_ 
#define _EXTRAZEROSAMPLESFILTER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <vector>


namespace pwiz {
namespace analysis {


/// removes (most) zero samples in signal profiles, except those flanking nonzero samples
/// simply looks for runs of 0 values, removes all but start and end of run
struct PWIZ_API_DECL ExtraZeroSamplesFilter 
{
    static void remove_zeros(const std::vector<double>& x, const std::vector<double>& y,
                             std::vector<double>& xProcessed, std::vector<double>& yProcessed,
                             bool preserveFlankingZeros);

    /// return the count of datapoints after removing excess zero samples
    static int count_non_zeros(const std::vector<double>& x, const std::vector<double>& y, bool preserveFlankingZeros);
};


} // namespace analysis
} // namespace pwiz


#endif // _EXTRAZEROSAMPLESFILTER_HPP_
