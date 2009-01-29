//
// LocalMaximumPeakDetector.cpp
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
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"


namespace pwiz {
namespace analysis {


PWIZ_API_DECL
void LocalMaximumPeakDetector::detect(const vector<double>& x, const vector<double>& y,
                                vector<double>& xPeakValues, vector<double>& yPeakValues,
                                vector<Peak>* peaks)
{
    if (x.size() != y.size())
        throw runtime_error("[LocalMaximumPeakDetector::detect()] x and y arrays must be the same size");

    if (x.size() < 2)
    {
        xPeakValues.assign(x.begin(), x.end());
        yPeakValues.assign(y.begin(), y.end());
    }
    else
    {
        if (y[0] > y[1])
        {
            xPeakValues.push_back(x[0]);
            yPeakValues.push_back(y[0]);
        }

        for (size_t i=1, end=x.size()-1; i < end; ++i)
        {
            if (y[i] > y[i-1] &&
                y[i] > y[i+1])
            {
                xPeakValues.push_back(x[i]);
                yPeakValues.push_back(y[i]);
            }
        }

        if (y.back() > y[y.size()-2])
        {
            xPeakValues.push_back(x.back());
            yPeakValues.push_back(y.back());
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
