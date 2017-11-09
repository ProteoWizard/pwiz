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


#ifndef _THRESHOLDFILTER_HPP_ 
#define _THRESHOLDFILTER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/common/DataFilter.hpp"
#include <climits>


namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL ThresholdFilter : public SpectrumDataFilter
{
    /// determines the method of thresholding and the meaning of the threshold value
    PWIZ_API_DECL enum ThresholdingBy_Type
    {
        /// keep the <threshold> [most|least] intense data points
        /// - <threshold> is rounded to the nearest integer
        /// - if the <threshold> falls within equally intense data points, all data points with that intensity are removed
        ThresholdingBy_Count,

        /// keep the <threshold> [most|least] intense data points
        /// - <threshold> is rounded to the nearest integer
        /// - if the <threshold> falls within equally intense data points, all data points with that intensity are kept
        ThresholdingBy_CountAfterTies,

        /// keep data points ranked [better|worse] than <threshold>
        /// - <threshold> is rounded to the nearest integer
        /// - rank 1 is the most intense
        // TODO: By_CompetitionRank,

        /// keep data points ranked [better|worse] than <threshold>
        /// - rank 1 is the most intense
        // TODO: By_FractionalRank,

        /// keep data points [more|less] absolutely intense than <threshold>
        ThresholdingBy_AbsoluteIntensity,

        /// keep data points [more|less] relatively intense than <threshold>
        /// - <threshold> is each data point's fraction of the base peak intensity (in the range [0,1])
        ThresholdingBy_FractionOfBasePeakIntensity,

        /// keep data points [more|less] relatively intense than <threshold>
        /// - <threshold> is each data point's fraction of the total intensity, aka total ion current (in the range [0,1])
        ThresholdingBy_FractionOfTotalIntensity,

        /// keep data points that are part of the <threshold> [most|least] intense fraction
        /// - <threshold> is the fraction of TIC to keep, i.e. the TIC of the kept data points is <threshold> * original TIC
        ThresholdingBy_FractionOfTotalIntensityCutoff
    };

    /// determines the orientation of the thresholding
    PWIZ_API_DECL enum ThresholdingOrientation
    {
        Orientation_MostIntense, /// thresholder removes the least intense data points
        Orientation_LeastIntense /// thresholder removes the most intense data points
    };

    ThresholdFilter(ThresholdingBy_Type byType_ = ThresholdingBy_Count, 
                    double threshold_ = 1.0, 
                    ThresholdingOrientation orientation_ = Orientation_MostIntense,
                    const pwiz::util::IntegerSet& msLevelsToThreshold = pwiz::util::IntegerSet(1, INT_MAX));

    virtual void operator () (const pwiz::msdata::SpectrumPtr&) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const ThresholdingBy_Type byType;
    const double threshold;
    const ThresholdingOrientation orientation;
    const pwiz::util::IntegerSet msLevelsToThreshold;

    private:
    static const char* byTypeMostIntenseName[];
    static const char* byTypeLeastIntenseName[];
};

} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_PEAKFILTER_HPP_ 
