//
// SpectrumList_PeakPicker.cpp
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


#include "SpectrumList_PeakPicker.hpp"
#include "pwiz/utility/misc/Container.hpp"

#ifdef PWIZ_READER_THERMO
#include "pwiz/data/vendor_readers/SpectrumList_Thermo.hpp"
#endif

#ifdef PWIZ_READER_BRUKER
#include "pwiz_aux/msrc/data/vendor_readers/Reader_Bruker.hpp"
#include "pwiz_aux/msrc/data/vendor_readers/SpectrumList_Bruker.hpp"
#endif


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_PeakPicker::SpectrumList_PeakPicker(
        const msdata::SpectrumListPtr& inner,
        PeakDetectorPtr algorithm,
        bool preferVendorPeakPicking,
        const IntegerSet& msLevelsToPeakPick)
:   SpectrumListWrapper(inner),
    algorithm_(algorithm),
    msLevelsToPeakPick_(msLevelsToPeakPick),
    mode_(0)
{
    if (preferVendorPeakPicking)
    {
        #ifdef PWIZ_READER_THERMO
        detail::SpectrumList_Thermo* thermo = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner);
        if (thermo)
        {
            mode_ = 1;
        }
        #endif

        #ifdef PWIZ_READER_BRUKER
        detail::SpectrumList_Bruker* bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner);
        if (bruker)
        {
            mode_ = 2;
        }
        #endif
    }

    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_peak_picking);
    if (mode_ == 1)
        method.userParams.push_back(UserParam("Thermo/Xcalibur peak picking"));
    else if (mode_ == 2)
        method.userParams.push_back(UserParam("Bruker/Agilent/CompassXtract peak picking"));
    //else
    //    method.userParams.push_back(algorithm->name());
    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_PeakPicker::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_PeakPicker::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s;
    
    switch (mode_)
    {
        #ifdef PWIZ_READER_THERMO
        case 1:
            s = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;
        #endif

        #ifdef PWIZ_READER_BRUKER
        case 2:
            s = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;
        #endif

        case 0:
        default:
            s = inner_->spectrum(index, true);
            break;
    }

    if (!msLevelsToPeakPick_.contains(s->cvParam(MS_ms_level).valueAs<int>()))
        return s;

    vector<CVParam>& cvParams = s->cvParams;
    vector<CVParam>::iterator itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);

    // return non-profile spectra as-is
    // (could have been acquired as centroid, or vendor may have done the centroiding)
    if (itr == cvParams.end())
        return s;

    // replace profile term with centroid term
    *itr = MS_centroid_spectrum;

    try
    {
        vector<double>& mzs = s->getMZArray()->data;
        vector<double>& intensities = s->getIntensityArray()->data;
        vector<double> xPeakValues, yPeakValues;
        algorithm_->detect(mzs, intensities, xPeakValues, yPeakValues);
        mzs.swap(xPeakValues);
        intensities.swap(yPeakValues);
        s->defaultArrayLength = mzs.size();
    }
    catch(std::exception& e)
    {
        throw std::runtime_error(std::string("[SpectrumList_PeakPicker::spectrum()] Error picking peaks: ") + e.what());
    }

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
