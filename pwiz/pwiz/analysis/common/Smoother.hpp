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
    /// smooth y values to existing vectors;
    /// note: in the case of sparse vectors, smoothing may fill in samples not present
    ///       in the original data, so make sure to check the size of the output vectors
    virtual void smooth(const std::vector<double>& x, const std::vector<double>& y,
                        std::vector<double>& xSmoothed, std::vector<double>& ySmoothed) = 0;

    /// smooth y values and copy back to the input vectors;
    /// note: in the case of sparse vectors, smoothing may fill in samples not present
    ///       in the original data, so make sure to check the size of the output vectors
    virtual void smooth_copy(std::vector<double>& x, std::vector<double>& y) = 0;

    virtual ~Smoother() {};
};

typedef boost::shared_ptr<Smoother> SmootherPtr;


} // namespace analysis
} // namespace pwiz


#endif // _SMOOTHER_HPP_
