//
// SpectrumList_SavitzkyGolaySmoother.cpp
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


#include "SpectrumList_SavitzkyGolaySmoother.hpp"
#include "SavitzkyGolaySmoother.hpp"
#include "pwiz/utility/misc/Container.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL SpectrumList_SavitzkyGolaySmoother::SpectrumList_SavitzkyGolaySmoother(
    const msdata::SpectrumListPtr& inner,
    const IntegerSet& msLevelsToSmooth)
:   SpectrumListWrapper(inner),
    msLevelsToSmooth_(msLevelsToSmooth)
{
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_smoothing);
    method.userParams.push_back(UserParam("Savitzky-Golay smoothing (9 point window)"));
    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_SavitzkyGolaySmoother::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_SavitzkyGolaySmoother::spectrum(size_t index, bool getBinaryData) const
{
    if (!getBinaryData)
        return inner_->spectrum(index, false);

    SpectrumPtr s = inner_->spectrum(index, true);

    try
    {
        vector<double>& intensities = s->getIntensityArray()->data;
        vector<double> smoothedIntensities;
        SavitzkyGolaySmoother<double>::smooth(intensities, smoothedIntensities);
        intensities.swap(smoothedIntensities);
    }
    catch(std::exception& e)
    {
        throw std::runtime_error(std::string("[SpectrumList_SavitzskyGolaySmoother] Error smoothing intensity data: ") + e.what());
    }

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
