//
// Smoother.hpp
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


#ifndef _SMOOTHER_HPP_ 
#define _SMOOTHER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>


namespace pwiz {
namespace analysis {


/// interface for a one-dimensional smoothing algorithm
struct PWIZ_API_DECL Smoother
{
    /// smooth to a new vector
    virtual std::vector<double> smooth_copy(const std::vector<double>& data) = 0;

    /// smooth to an existing vector
    virtual std::vector<double>& smooth(const std::vector<double>& data,
                                        std::vector<double>& smoothedData) = 0;

    virtual ~Smoother() {};
};

typedef boost::shared_ptr<Smoother> SmootherPtr;


} // namespace analysis
} // namespace pwiz


#endif // _SMOOTHER_HPP_
