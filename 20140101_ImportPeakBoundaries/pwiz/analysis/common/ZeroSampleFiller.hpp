//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _ZEROSAMPLEFILLER_HPP_ 
#define _ZEROSAMPLEFILLER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <vector>
#include <memory>
#include <cstddef>


namespace pwiz {
namespace analysis {


/// fills in missing zero samples around signal profiles
struct PWIZ_API_DECL ZeroSampleFiller
{
    /// fills in missing zero samples (and won't overwrite existing samples);
    /// zeroSampleCount controls how many zero samples to add to each signal profile;
    /// preconditions:
    /// - sample rate can change, but it must change gradually
    /// - at least one zero sample on each side of every signal profile
    static void fill(const std::vector<double>& x, const std::vector<double>& y,
                     std::vector<double>& xFilled, std::vector<double>& yFilled,
                     std::size_t zeroSampleCount);
};


} // namespace analysis
} // namespace pwiz


#endif // _ZEROSAMPLEFILLER_HPP_
