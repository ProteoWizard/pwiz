//
// $Id$
//
//
// Original author: Brian Pratt <brian.pratt <a.t> insilicos.com>
//
// Copyright 2012  Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#include "SpectrumList_ZeroSamplesFilter.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/analysis/common/ExtraZeroSamplesFilter.hpp"
#include "pwiz/analysis/common/ZeroSampleFiller.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_ZeroSamplesFilter::SpectrumList_ZeroSamplesFilter(
        const msdata::SpectrumListPtr& inner,
        const IntegerSet& msLevelsToFilter,
        Mode mode, 
        size_t flankingZeroCount)
:   SpectrumListWrapper(inner), mode_(mode), 
      flankingZeroCount_(flankingZeroCount), msLevelsToFilter_(msLevelsToFilter)
{
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    if (Mode_RemoveExtraZeros == mode)
    {
        method.userParams.push_back(UserParam("removed extra zero samples"));
    } 
    else
    {
        method.userParams.push_back(UserParam("added missing zero samples"));
    }
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_ZeroSamplesFilter::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_ZeroSamplesFilter::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s = inner_->spectrum(index, true);

    if (!msLevelsToFilter_.contains(s->cvParam(MS_ms_level).valueAs<int>()))
        return s;

    try
    {
        BinaryData<double>& mzs = s->getMZArray()->data;
        BinaryData<double>& intensities = s->getIntensityArray()->data;
        vector<double> FilteredMZs, FilteredIntensities;
        if (Mode_AddMissingZeros == mode_)
            ZeroSampleFiller().fill(mzs, intensities, FilteredMZs, FilteredIntensities, flankingZeroCount_);
        else
            ExtraZeroSamplesFilter().remove_zeros(mzs, intensities, FilteredMZs, FilteredIntensities,
              !s->hasCVParam(MS_centroid_spectrum)); // preserve flanking zeros if not centroided
        mzs.swap(FilteredMZs);
        intensities.swap(FilteredIntensities);
        s->defaultArrayLength = mzs.size();
    }
    catch(std::exception& e)
    {
        throw std::runtime_error(std::string("[SpectrumList_ZeroSamplesFilter] Error filtering intensity data: ") + e.what());
    }

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
