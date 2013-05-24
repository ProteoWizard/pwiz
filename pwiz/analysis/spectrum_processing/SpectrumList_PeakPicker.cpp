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


#include "SpectrumList_PeakPicker.hpp"
#include "pwiz/utility/misc/Container.hpp"

#include "pwiz/data/vendor_readers/ABI/SpectrumList_ABI.hpp"
#include "pwiz/data/vendor_readers/ABI/T2D/SpectrumList_ABI_T2D.hpp"
#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/data/vendor_readers/Bruker/SpectrumList_Bruker.hpp"
#include "pwiz/data/vendor_readers/Thermo/SpectrumList_Thermo.hpp"


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
        detail::SpectrumList_Thermo* thermo = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner);
        if (thermo)
        {
            mode_ = 1;
        }

        detail::SpectrumList_Bruker* bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner);
        if (bruker)
        {
            mode_ = 2;
        }

        detail::SpectrumList_ABI* abi = dynamic_cast<detail::SpectrumList_ABI*>(&*inner);
        if (abi)
        {
            mode_ = 3;
        }

        detail::SpectrumList_Agilent* agilent = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner);
        if (agilent)
        {
            mode_ = 4;
        }

        detail::SpectrumList_ABI_T2D* abi_t2d = dynamic_cast<detail::SpectrumList_ABI_T2D*>(&*inner);
        if (abi_t2d)
        {
            mode_ = 5;
        }
    }

    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_peak_picking);
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    if (mode_ == 1)
        method.userParams.push_back(UserParam("Thermo/Xcalibur peak picking"));
    else if (mode_ == 2)
        method.userParams.push_back(UserParam("Bruker/Agilent/CompassXtract peak picking"));
    else if (mode_ == 3)
        method.userParams.push_back(UserParam("ABI/Analyst peak picking"));
    else if (mode_ == 4)
        method.userParams.push_back(UserParam("Agilent/MassHunter peak picking"));
    else if (mode_ == 5)
        method.userParams.push_back(UserParam("ABI/DataExplorer peak picking"));
    //else
    //    method.userParams.push_back(algorithm->name());
    if (preferVendorPeakPicking && !mode_)
    {
        cerr << "Warning: vendor peakPicking was requested, but is unavailable";
#ifdef WIN32
        cerr << " for this input data. ";
#else
        cerr << " as it depends on Windows DLLs.  ";
#endif
        cerr << "Using ProteoWizard centroiding algorithm instead." << endl;
    }
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
        case 1:
            s = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;

        case 2:
            s = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;

        case 3:
            s = dynamic_cast<detail::SpectrumList_ABI*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;

        case 4:
            s = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;

        case 5:
            s = dynamic_cast<detail::SpectrumList_ABI_T2D*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToPeakPick_);
            break;

        case 0:
        default:
            s = inner_->spectrum(index, true);
            break;
    }

    if (!msLevelsToPeakPick_.contains(s->cvParam(MS_ms_level).valueAs<int>()))
        return s;

    vector<CVParam>& cvParams = s->cvParams;
    vector<CVParam>::iterator itr = std::find(cvParams.begin(), cvParams.end(), MS_centroid_spectrum);

    // return non-profile spectra as-is
    // (could have been acquired as centroid, or vendor may have done the centroiding)
    if (itr != cvParams.end())
    {
        this->warn_once("[SpectrumList_PeakPicker]: one or more spectra are already centroided, no processing needed");
        return s;
    }

    // is this declared as profile?
    itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);
    if (cvParams.end() == itr)
    {
        this->warn_once("[SpectrumList_PeakPicker]: one or more spectra have undeclared profile/centroid status, assuming profile data and that peakpicking is needed");
        itr = std::find(cvParams.begin(), cvParams.end(), MS_spectrum_representation); // this should be there if nothing else
    }

    // make sure the spectrum has binary data
    if (!s->getMZArray().get() || !s->getIntensityArray().get())
        s = inner_->spectrum(index, true);

    // replace profile or nonspecific term with centroid term
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
