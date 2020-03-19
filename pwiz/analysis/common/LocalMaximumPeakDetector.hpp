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


#ifndef _SIMPLEPEAKDETECTOR_HPP_ 
#define _SIMPLEPEAKDETECTOR_HPP_


#include "PeakDetector.hpp"
namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL LocalMaximumPeakDetector : public PeakDetector
{
    LocalMaximumPeakDetector(size_t windowSize);

    /// finds all local maxima, i.e. any point that has a greater y value than both
    /// of its neighboring points;
    /// note: the peaks array, if non-NULL, only provides x and y values
    virtual void detect(const std::vector<double>& x, const std::vector<double>& y,
                        std::vector<double>& xPeakValues, std::vector<double>& yPeakValues,
                        std::vector<Peak>* peaks = NULL);
    virtual const char* name() const { return "local maximum peak picker"; }
    private:
    size_t window_;
};


} // namespace analysis
} // namespace pwiz


#endif // _SIMPLEPEAKDETECTOR_HPP_
