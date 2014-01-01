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
    if (!getBinaryData)
        return inner_->spectrum(index, false);

    const SpectrumPtr currentSpectrum = inner_->spectrum(index, true);
    (*filterFunctor_)(currentSpectrum);
    currentSpectrum->dataProcessingPtr = dp_;
    return currentSpectrum;
}


} // namespace analysis 
} // namespace pwiz
