//
// Java author: Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
//              Nesvizhskii Lab, Department of Computational Medicine and Bioinformatics
//
// Copyright 2014 University of Michigan, Ann Arbor, MI
//
//
// C++ port: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2020 Matt Chambers
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


#include "IsotopePatternMap.hpp"
#include <algorithm>
#include <boost/range/iterator_range_core.hpp>

namespace DiaUmpire {


namespace {

#include "IsotopePatternRange.ipp"

} // namespace


IsotopePatternMap generateIsotopePatternMap(const InstrumentParameter& parameter)
{
    IsotopePatternMap result;

    int isotopeCount = std::max(2, parameter.MaxNoPeakCluster - 1);
    result.resize(isotopeCount);
    for (int i = 0; i < isotopeCount; ++i)
        for (const IsotopePattern& pattern : boost::make_iterator_range(&isotopePatternArray[0], isotopePatternArray + isotopePatternArraySize))
            result[i][pattern.mass] = pattern.isotopeRanges[i];

    return result;
}

} // namespace DiaUmpire
