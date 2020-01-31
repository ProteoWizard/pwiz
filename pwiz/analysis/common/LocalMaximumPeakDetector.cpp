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

#define PWIZ_SOURCE


#include "LocalMaximumPeakDetector.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "ZeroSampleFiller.hpp"


namespace pwiz {
namespace analysis {


PWIZ_API_DECL
LocalMaximumPeakDetector::LocalMaximumPeakDetector(size_t windowSize)
: window_(windowSize)
{
}


PWIZ_API_DECL
void LocalMaximumPeakDetector::detect(const vector<double>& x, const vector<double>& y,
                                vector<double>& xPeakValues, vector<double>& yPeakValues,
                                vector<Peak>* peaks)
{
    if (x.size() != y.size())
        throw runtime_error("[LocalMaximumPeakDetector::detect()] x and y arrays must be the same size");

    if (x.empty())
        return;

    // the size of the window in either direction
    size_t flank = size_t(window_-1) / 2;

    // fill in missing samples based on window size
    // note: we don't need all the missing samples because a window full of zeros
    //       will always smooth to a 0, regardless of the X values involved
    vector<double> xCopy;
    vector<double> yCopy;
    ZeroSampleFiller::fill(x, y, xCopy, yCopy, flank+1);

    for (size_t i=flank, end=yCopy.size()-flank; i < end; ++i)
    {
        bool isPeak = true;
        for (size_t j=1; j <= flank; ++j)
            if (yCopy[i] < yCopy[i-j] ||
                yCopy[i] < yCopy[i+j])
            {
                isPeak = false;
                break;
            }

        if (isPeak)
        {
            xPeakValues.push_back(xCopy[i]);
            yPeakValues.push_back(yCopy[i]);
        }
    }

    if (peaks)
    {
        peaks->resize(xPeakValues.size());
        for (size_t i=0; i < xPeakValues.size(); ++i)
        {
            Peak& p = (*peaks)[i];
            p.x = xPeakValues[i];
            p.y = yPeakValues[i];
            p.start = 0;
            p.stop = 0;
            p.area = 0;
        }
    }
}


} // namespace analysis
} // namespace msdata
