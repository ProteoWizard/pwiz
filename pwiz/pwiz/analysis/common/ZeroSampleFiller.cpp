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


#include "pwiz/utility/misc/Std.hpp"
#include "ZeroSampleFiller.hpp"
#include <cmath>


namespace pwiz {
namespace analysis {


void ZeroSampleFiller::fill(const vector<double>& x, const vector<double>& y,
                            vector<double>& xFilled, vector<double>& yFilled,
                            size_t zeroSampleCount)
{
    if (x.size() != y.size())
        throw runtime_error("[ZeroSampleFiller::fill()] x and y arrays must be the same size");

    // adjacent samples are expected to be within this tolerance
    const static double EPSILON = 1e-5;

    // start with the original data
    xFilled.assign(x.begin(), x.end());
    yFilled.assign(y.begin(), y.end());

    // insert flanking zeros around non-zero data points
    bool wasInData = false;
    bool nowInData = false;
    for (int i=y.size()-1; i >= 0; --i)
    {
        nowInData = yFilled[i] > 0.0;
        if (nowInData && !wasInData)
        {
            // step forward to check for missing samples
            
            // at i==0, fudge the first order delta
            double firstOrderDelta = i < 1 ? xFilled[i+1] - xFilled[i]
                                           : xFilled[i] - xFilled[i-1];

            // at i==1 or when possibly between signals, assume no second order delta
            double secondOrderDelta = i < 2 || yFilled[i-1] == 0 ? 0 :
                                      firstOrderDelta - (xFilled[i-1] - xFilled[i-2]);
            double totalDelta = 0;
            for (int j=1; j <= (int) zeroSampleCount; ++j)
            {
                totalDelta += secondOrderDelta;
                double newX = xFilled[i+j-1] + firstOrderDelta + totalDelta;
                
                bool oob = i+j >= (int) y.size();

                // sampleDelta should never be less than negative firstOrderDelta
                double sampleDelta = oob ? 0 : xFilled[i+j] - newX;
                if (sampleDelta < -firstOrderDelta)
                    break;
                    //throw std::runtime_error("[ZeroSampleFiller::fill()] miscalculated sample rate");

                // if out of bounds or newX is a valid missing sample, insert a new zero point
                if (oob || sampleDelta > firstOrderDelta)
                {
                    xFilled.insert(xFilled.begin()+(i+j), newX);
                    yFilled.insert(yFilled.begin()+(i+j), 0.0);
                }
            }
        }
        wasInData = nowInData;
    }

    wasInData = false;
    for (int i=0, end=yFilled.size(); i < end; ++i, end=yFilled.size())
    {
        nowInData = yFilled[i] > 0.0;
        if (nowInData && !wasInData)
        {
            // step backward to check for missing samples

            // at i==end-1, fudge the first order delta
            double firstOrderDelta = i == end-1 ? xFilled[i] - xFilled[i-1]
                                                : xFilled[i+1] - xFilled[i];

            // at i==end-2 or when possibly between signals, assume no second order delta
            double secondOrderDelta = i == end-2 || yFilled[i+1] == 0 ? 0 :
                                      (xFilled[i+2] - xFilled[i+1]) - firstOrderDelta;
            double totalDelta = 0;
            for (int j=1; j <= (int) zeroSampleCount; ++j)
            {
                totalDelta += secondOrderDelta;
                double newX = (xFilled[i-j+1] - firstOrderDelta) - totalDelta;

                bool oob = i-j < 0;

                // xFilled[i-j] and newX should be nearly equal if they are the same sample

                // sampleDelta should never be greater than firstOrderDelta
                double sampleDelta = oob ? 0 : xFilled[i-j] - newX;
                if (sampleDelta > firstOrderDelta)
                    break;
                    //throw std::runtime_error("[ZeroSampleFiller::fill()] miscalculated sample rate");

                // if out of bounds or newX is a valid missing sample, insert a new zero point
                if (oob || sampleDelta <= -firstOrderDelta )
                {
                    xFilled.insert(xFilled.begin()+(i-j+1), newX);
                    yFilled.insert(yFilled.begin()+(i-j+1), 0.0);
                    ++i;
                }
            }
        }
        wasInData = nowInData;
    }
}


} // namespace analysis
} // namespace pwiz
