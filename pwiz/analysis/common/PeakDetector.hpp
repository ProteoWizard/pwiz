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


#ifndef _PEAKDETECTOR_HPP_
#define _PEAKDETECTOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>


namespace pwiz {
namespace analysis {


/// represents some generic metadata about a peak detected in a signal
struct PWIZ_API_DECL Peak
{
    double x; /// x value of a signal peak (or centroid)
    double y; /// y value of a signal peak (or centroid), aka intensity/abundance/amplitude
    double start; // x value where the peak's profile starts
    double stop; // x value where the peak's profile stops
    double area; // area under the profile between start and stop
};


struct PWIZ_API_DECL PeakDetector
{    
    /// find peaks in the signal profile described by the x and y vectors
    virtual void detect(const std::vector<double>& x, const std::vector<double>& y,
                        std::vector<double>& xPeakValues, std::vector<double>& yPeakValues,
                        std::vector<Peak>* peaks = NULL) = 0;
    virtual const char* name() const = 0;
    virtual ~PeakDetector() {}
};

typedef boost::shared_ptr<PeakDetector> PeakDetectorPtr;


} // namespace analysis
} // namespace pwiz


#endif // _PEAKDETECTOR_HPP_
