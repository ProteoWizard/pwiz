//
// $Id: SpectrumList_PeakFilter.hpp 1191 2009-08-14 19:33:05Z chambm $
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


#include <boost/scoped_ptr.hpp>
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/peakdetect/MZTolerance.hpp"


namespace pwiz {
namespace analysis {

struct ThresholdingParams
{
    /// determines the method of thresholding and the meaning of the threshold value
    enum ThresholdingBy_Type
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
    enum ThresholdingOrientation
    {
        Orientation_MostIntense, /// thresholder removes the least intense data points
        Orientation_LeastIntense /// thresholder removes the most intense data points
    };

    ThresholdingParams
        (const ThresholdingParams::ThresholdingBy_Type& byType_ = ThresholdingParams::ThresholdingBy_Count, 
        const double threshold_ = 1.0, 
        const ThresholdingParams::ThresholdingOrientation orientation_ = ThresholdingParams::Orientation_MostIntense);

    const ThresholdingBy_Type        byType; 
    double                            threshold;
    const ThresholdingOrientation    orientation;

    static const char* byTypeMostIntenseName[];

    static const char* byTypeLeastIntenseName[];

};


struct ThresholdingFilter : public ISpectrumDataFilter
{
    ThresholdingFilter(const ThresholdingParams& params_) : params(params_) {}

    virtual void operator () (const pwiz::msdata::SpectrumPtr) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const ThresholdingParams params;
};

} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_PEAKFILTER_HPP_ 
