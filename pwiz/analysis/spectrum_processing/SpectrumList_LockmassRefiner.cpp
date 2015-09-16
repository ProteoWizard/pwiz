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


#include "SpectrumList_LockmassRefiner.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_LockmassRefiner::SpectrumList_LockmassRefiner(const msdata::SpectrumListPtr& inner, double lockmassMz, double lockmassTolerance)
: SpectrumListWrapper(inner), mz_(lockmassMz), tolerance_(lockmassTolerance)
{

    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_m_z_calibration);
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner);
    if (waters)
    {
        method.userParams.push_back(UserParam("Waters lockmass correction"));
    }
    else
    {
        cerr << "Warning: lockmass refinement was requested, but is unavailable";
#ifdef WIN32
        cerr << " for non-Waters input data. ";
#else
        cerr << " as it depends on Windows DLLs.  ";
#endif
    }
    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_LockmassRefiner::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}

PWIZ_API_DECL SpectrumPtr SpectrumList_LockmassRefiner::spectrum(size_t index, DetailLevel detailLevel) const
{
    // for full metadata, defaultArrayLength must be accurate, so go ahead and do peak picking anyway
    return (int) detailLevel >= (int) DetailLevel_FullMetadata ? spectrum(index, true) : inner_->spectrum(index, detailLevel);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_LockmassRefiner::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s;

    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner_);
    if (waters)
        s = waters->spectrum(index, getBinaryData, mz_, tolerance_);
    else
        s = inner_->spectrum(index, true);

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
