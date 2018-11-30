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

#include "pwiz/data/msdata/MSData.hpp"
#include "SpectrumList_PeakFilter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>

namespace pwiz {
namespace analysis {


using namespace msdata;


PWIZ_API_DECL
SpectrumList_PeakFilter::SpectrumList_PeakFilter(const SpectrumListPtr& inner,
                                                 SpectrumDataFilterPtr filterFunctor)
    :   SpectrumListWrapper(inner),
        filterFunctor_(filterFunctor)
{
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    filterFunctor_->describe(method);
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_PeakFilter::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_PeakFilter::spectrum(size_t index, DetailLevel detailLevel) const
{
    // the effects of running the peak filter on the defaultArrayLength is unknown; 0 denotes that to other filters;
    // for full metadata detail, the defaultArrayLength must be known, so go ahead and get binary data anyway
    if (detailLevel < DetailLevel_FullMetadata)
    {
        SpectrumPtr innerSpectrum = inner_->spectrum(index, detailLevel);
        innerSpectrum->defaultArrayLength = 0;
        return innerSpectrum;
    }

    const SpectrumPtr currentSpectrum = inner_->spectrum(index, true);
    (*filterFunctor_)(currentSpectrum);
    currentSpectrum->dataProcessingPtr = dp_;
    return currentSpectrum;
}


PWIZ_API_DECL IsolationWindowFilter::IsolationWindowFilter(double defaultWindowWidth, const msdata::SpectrumListPtr& sl) : defaultWindowWidth(defaultWindowWidth), spectrumList(sl) {}
PWIZ_API_DECL IsolationWindowFilter::IsolationWindowFilter(double defaultWindowWidth, const msdata::IsolationWindow& window) : defaultWindowWidth(defaultWindowWidth), window(window) {}

PWIZ_API_DECL void IsolationWindowFilter::operator () (const pwiz::msdata::SpectrumPtr& s) const
{
    using namespace boost::icl;

    int precursorMsLevel = s->cvParamValueOrDefault<int>(MS_ms_level, 0);
    if (precursorMsLevel == 0)
        return;

    interval_set<double> isolationWindows;
    if (spectrumList)
    {
        const auto& sl = *spectrumList;

        // iterate forward through the list to get the isolation windows of spectra where ms level == precursorMsLevel+1 and spectrumID matches this spectrum
        for (size_t i = s->index + 1; i < sl.size(); ++i)
        {
            msdata::SpectrumPtr productSpectrum = sl.spectrum(i, DetailLevel_FullMetadata);

            int productMsLevel = productSpectrum->cvParamValueOrDefault<int>(MS_ms_level, 0);
            if (productMsLevel == 0)
                continue;
            if (productMsLevel == precursorMsLevel)
                break;

            if (productSpectrum->precursors.empty() || productSpectrum->precursors[0].isolationWindow.empty())
                continue;

            const IsolationWindow& window = productSpectrum->precursors[0].isolationWindow;
            double targetMz = window.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            double windowStart = targetMz - window.cvParamValueOrDefault(MS_isolation_window_lower_offset, defaultWindowWidth);
            double windowStop = targetMz + window.cvParamValueOrDefault(MS_isolation_window_upper_offset, defaultWindowWidth);

            isolationWindows.add(continuous_interval<double>::closed(windowStart, windowStop));
        }
    }
    else
    {
        double targetMz = window.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
        double windowStart = targetMz - window.cvParamValueOrDefault(MS_isolation_window_lower_offset, defaultWindowWidth);
        double windowStop = targetMz + window.cvParamValueOrDefault(MS_isolation_window_upper_offset, defaultWindowWidth);
        isolationWindows.add(continuous_interval<double>::closed(windowStart, windowStop));
    }

    if (isolationWindows.empty())
        return;

    auto& mzArray = s->getMZArray()->data;
    auto& intensityArray = s->getIntensityArray()->data;
    pwiz::util::BinaryData<double> filteredMzArray, filteredIntensityArray;

    for (const auto& interval : isolationWindows)
    {
        auto firstPointItr = lower_bound(mzArray.begin(), mzArray.end(), interval.lower());
        auto lastPointItr = upper_bound(firstPointItr, mzArray.end(), interval.upper());
        size_t firstPointIndex = firstPointItr - mzArray.begin();
        size_t lastPointIndex = lastPointItr - mzArray.begin();
        filteredMzArray.insert(filteredMzArray.end(), firstPointItr, lastPointItr);
        filteredIntensityArray.insert(filteredIntensityArray.end(), intensityArray.begin() + firstPointIndex, intensityArray.begin() + lastPointIndex);
    }

    s->swapMZIntensityArrays(filteredMzArray, filteredIntensityArray, s->getIntensityArray()->cvParamChild(MS_intensity_unit).cvid);
}

} // namespace analysis 
} // namespace pwiz
