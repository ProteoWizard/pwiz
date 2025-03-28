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
#include "SpectrumList_PeakPicker.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"
#include <boost/range/algorithm/remove_if.hpp>


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_LockmassRefiner::SpectrumList_LockmassRefiner(const msdata::SpectrumListPtr& inner, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance)
: SpectrumListWrapper(inner), mzPositiveScans_(lockmassMzPosScans), mzNegativeScans_(lockmassMzNegScans), tolerance_(lockmassTolerance)
{
    SpectrumList_PeakPicker* peakPicker = dynamic_cast<SpectrumList_PeakPicker*>(&*inner); // If there's a peak picker, it will be outermost
    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(peakPicker ? &*peakPicker->inner() : &*inner);
    if (waters)
    {
        // add processing methods to the copy of the inner SpectrumList's data processing
        ProcessingMethod method;
        method.order = dp_->processingMethods.size();
        method.set(MS_m_z_calibration);

        if (!dp_->processingMethods.empty())
            method.softwarePtr = dp_->processingMethods[0].softwarePtr;
        method.userParams.push_back(UserParam("Waters lockmass correction"));
        dp_->processingMethods.push_back(method);
    }
    else
    {
        cerr << "Warning: lockmass refinement for spectrum data was requested, but is unavailable";
#ifdef WIN32
        cerr << " for non-Waters input data. ";
#else
        cerr << " as it depends on Windows DLLs.  ";
#endif
        cerr << endl;
    }
}


PWIZ_API_DECL bool SpectrumList_LockmassRefiner::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}

PWIZ_API_DECL SpectrumPtr SpectrumList_LockmassRefiner::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_LockmassRefiner::spectrum(size_t index, DetailLevel detailLevel) const
{
    SpectrumPtr s;

    SpectrumList_PeakPicker* peakPicker = dynamic_cast<SpectrumList_PeakPicker*>(&*inner_);
    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(peakPicker ? &*peakPicker->inner() : &*inner_);
    if (waters)
    {
        s = waters->spectrum(index, detailLevel, mzPositiveScans_, mzNegativeScans_, tolerance_, peakPicker ? peakPicker->msLevels() : pwiz::util::IntegerSet());

        // the vendor spectrum lists must put "profile spectrum" if they actually performed centroiding
        if (peakPicker && s->hasCVParam(MS_centroid_spectrum))
        {
            auto itr = boost::range::remove_if(s->cvParams, CVParamIs(MS_profile_spectrum));
            if (itr != s->cvParams.end())
                s->cvParams.erase(itr);
        }
    }
    else
        s = inner_->spectrum(index, true);

    s->dataProcessingPtr = dp_;
    return s;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_LockmassRefiner::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    SpectrumPtr s;

    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner_);
    if (waters)
    {
        s = waters->spectrum(index, detailLevel, mzPositiveScans_, mzNegativeScans_, tolerance_, msLevelsToCentroid);

        // the vendor spectrum lists must put "profile spectrum" if they actually performed centroiding
        if (s->hasCVParam(MS_centroid_spectrum))
        {
            auto itr = boost::range::remove_if(s->cvParams, CVParamIs(MS_profile_spectrum));
            if (itr != s->cvParams.end())
                s->cvParams.erase(itr);
        }
    }
    else
        s = inner_->spectrum(index, true);

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
