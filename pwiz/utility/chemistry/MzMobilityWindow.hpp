//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2019 Matt Chambers
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

#ifndef _MZMOBILITYWINDOW_HPP_
#define _MZMOBILITYWINDOW_HPP_

#include <boost/optional.hpp>
#include <utility>

namespace pwiz {
namespace chemistry {

struct MzMobilityWindow
{
    MzMobilityWindow(double mz) : mz(mz) {}
    MzMobilityWindow(double mz, const std::pair<double, double>& mobilityBounds) : mz(mz), mobilityBounds(mobilityBounds) {}
    MzMobilityWindow(double mz, double mobility, double mobilityTolerance) : MzMobilityWindow(mz, std::make_pair(mobility - mobilityTolerance, mobility + mobilityTolerance)) {}

    double mz;
    boost::optional<std::pair<double, double>> mobilityBounds;
};

} // chemistry
} // pwiz

#endif // _MZMOBILITYWINDOW_HPP_