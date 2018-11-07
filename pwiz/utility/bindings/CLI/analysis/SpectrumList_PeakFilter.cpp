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


#ifndef _SPECTRUMLIST_PEAKFILTER_HPP_CLI_
#define _SPECTRUMLIST_PEAKFILTER_HPP_CLI_ 


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "spectrum_processing.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/PrecursorMassFilter.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace analysis {


using namespace pwiz::CLI::msdata;


public ref class SpectrumDataFilter abstract
{
    public:
    virtual void operator () (Spectrum^ spectrum) {(**base_)(*spectrum->base_);}
    virtual void describe(ProcessingMethod^ method) {(*base_)->describe(*method->base_);}

    internal:
    pwiz::analysis::SpectrumDataFilterPtr* base_;
};


/// <summary>
/// SpectrumList implementation to filter data points by a user-specified functor
/// </summary>
public ref class SpectrumList_PeakFilter : public SpectrumList
{
    internal: virtual ~SpectrumList_PeakFilter()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_PeakFilter* base_;

    public:

    SpectrumList_PeakFilter(SpectrumList^ inner, SpectrumDataFilter^ filterFunctor)
    : SpectrumList(0)
    {
        base_ = new pwiz::analysis::SpectrumList_PeakFilter(*inner->base_, *filterFunctor->base_);
        SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_PeakFilter::accept(*inner->base_);}
};


public ref class ThresholdFilter : public SpectrumDataFilter
{
    public:

    /// <summary>
    /// determines the method of thresholding and the meaning of the threshold value
    /// </summary>
    enum class ThresholdingBy_Type
    {
        /// <summary>
        /// keep the {threshold} [most|least] intense data points
        /// - {threshold} is rounded to the nearest integer
        /// - if the {threshold} falls within equally intense data points, all data points with that intensity are removed
        /// </summary>
        ThresholdingBy_Count,

        /// <summary>
        /// keep the {threshold} [most|least] intense data points
        /// - {threshold} is rounded to the nearest integer
        /// - if the {threshold} falls within equally intense data points, all data points with that intensity are kept
        /// </summary>
        ThresholdingBy_CountAfterTies,

        /// keep data points ranked [better|worse] than {threshold}
        /// - {threshold} is rounded to the nearest integer
        /// - rank 1 is the most intense
        // TODO: By_CompetitionRank,

        /// keep data points ranked [better|worse] than {threshold}
        /// - rank 1 is the most intense
        // TODO: By_FractionalRank,

        /// <summary>
        /// keep data points [more|less] absolutely intense than {threshold}
        /// </summary>
        ThresholdingBy_AbsoluteIntensity,

        /// <summary>
        /// keep data points [more|less] relatively intense than {threshold}
        /// - {threshold} is each data point's fraction of the base peak intensity (in the range [0,1])
        /// </summary>
        ThresholdingBy_FractionOfBasePeakIntensity,

        /// <summary>
        /// keep data points [more|less] relatively intense than {threshold}
        /// - {threshold} is each data point's fraction of the total intensity, aka total ion current (in the range [0,1])
        /// </summary>
        ThresholdingBy_FractionOfTotalIntensity,

        /// <summary>
        /// keep data points that are part of the {threshold} [most|least] intense fraction
        /// - {threshold} is the fraction of TIC to keep, i.e. the TIC of the kept data points is {threshold} * original TIC
        /// </summary>
        ThresholdingBy_FractionOfTotalIntensityCutoff
    };


    /// <summary>
    /// determines the orientation of the thresholding
    /// </summary>
    enum class ThresholdingOrientation
    {
        Orientation_MostIntense, /// <summary>thresholder removes the least intense data points</summary>
        Orientation_LeastIntense /// <summary>thresholder removes the most intense data points</summary>
    };


    ThresholdFilter(ThresholdingBy_Type byType_, 
                    double threshold_, 
                    ThresholdingOrientation orientation_)
    {
        SpectrumDataFilter::base_ =
            new pwiz::analysis::SpectrumDataFilterPtr(
                new pwiz::analysis::ThresholdFilter
                    ((pwiz::analysis::ThresholdFilter::ThresholdingBy_Type) byType_,
                     threshold_,
                     (pwiz::analysis::ThresholdFilter::ThresholdingOrientation) orientation_));
    }
};


public ref class IsolationWindowFilter : public SpectrumDataFilter
{
public:

    IsolationWindowFilter(double defaultWindowWidth_, SpectrumList^ spectrumList_)
    {
        SpectrumDataFilter::base_ =
            new pwiz::analysis::SpectrumDataFilterPtr(
                new pwiz::analysis::IsolationWindowFilter(defaultWindowWidth_, *spectrumList_->base_));
    }

    IsolationWindowFilter(double defaultWindowWidth_, IsolationWindow^ window)
    {
        SpectrumDataFilter::base_ =
            new pwiz::analysis::SpectrumDataFilterPtr(
                new pwiz::analysis::IsolationWindowFilter(defaultWindowWidth_, *window->base_));
    }
};


} // namespace analysis 
} // namespace CLI
} // namespace pwiz


#endif // _SPECTRUMLIST_PEAKFILTER_HPP_CLI_ 
