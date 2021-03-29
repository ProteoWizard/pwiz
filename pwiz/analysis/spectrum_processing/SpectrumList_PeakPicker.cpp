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
#include <boost/range/algorithm/remove_if.hpp>
#include <boost/range/algorithm/remove.hpp>

#include "pwiz/data/vendor_readers/ABI/Reader_ABI.hpp"
#include "pwiz/data/vendor_readers/ABI/SpectrumList_ABI.hpp"
#include "pwiz/data/vendor_readers/ABI/T2D/SpectrumList_ABI_T2D.hpp"
#include "pwiz/data/vendor_readers/Agilent/Reader_Agilent.hpp"
#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/data/vendor_readers/Bruker/Reader_Bruker.hpp"
#include "pwiz/data/vendor_readers/Bruker/SpectrumList_Bruker.hpp"
#include "pwiz/data/vendor_readers/Shimadzu/Reader_Shimadzu.hpp"
#include "pwiz/data/vendor_readers/Shimadzu/SpectrumList_Shimadzu.hpp"
#include "pwiz/data/vendor_readers/Thermo/Reader_Thermo.hpp"
#include "pwiz/data/vendor_readers/Thermo/SpectrumList_Thermo.hpp"
#include "pwiz/data/vendor_readers/Waters/Reader_Waters.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"


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
    minDetailLevel_(DetailLevel_InstantMetadata),
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

        detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner);
        if (waters)
        {
            mode_ = 6;
        }

        detail::SpectrumList_Shimadzu* shimadzu = dynamic_cast<detail::SpectrumList_Shimadzu*>(&*inner);
        if (shimadzu)
        {
            mode_ = 7;
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
    else if (mode_ == 6)
        method.userParams.push_back(UserParam("Waters/MassLynx peak picking"));
    else if (mode_ == 7)
        method.userParams.push_back(UserParam("Shimadzu peak picking"));
    else if (algorithm != NULL)
        method.userParams.emplace_back(algorithm->name());

    if (algorithm_)
        noVendorCentroidingWarningMessage_ = string("[SpectrumList_PeakPicker]: vendor centroiding requested but not available for this data; falling back to ") + algorithm_->name();

    if (preferVendorPeakPicking && !mode_ && (algorithm_ != NULL)) // VendorOnlyPeakPicker sets algorithm null, we deal with this at get binary data time
    {
        cerr << "Warning: vendor peakPicking was requested, but is unavailable";
#ifdef WIN32
        cerr << " for this input data. ";
#else
        cerr << " as it depends on Windows DLLs.  ";
#endif
        cerr << "Using ProteoWizard centroiding algorithm instead." << endl;
		cerr << "High-quality peak-picking can be enabled using the cwt flag." << endl;
    }
    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_PeakPicker::supportsVendorPeakPicking(const std::string& rawpath)
{
    static ReaderList peakPickingVendorReaders = ReaderPtr(new Reader_ABI)
                                               + ReaderPtr(new Reader_Agilent)
                                               + ReaderPtr(new Reader_Bruker_BAF)
                                               + ReaderPtr(new Reader_Bruker_YEP)
                                               + ReaderPtr(new Reader_Bruker_TDF)
                                               + ReaderPtr(new Reader_Shimadzu)
                                               + ReaderPtr(new Reader_Thermo)
                                               + ReaderPtr(new Reader_Waters);
    return !peakPickingVendorReaders.identify(rawpath).empty();
}


PWIZ_API_DECL bool SpectrumList_PeakPicker::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}

PWIZ_API_DECL SpectrumPtr SpectrumList_PeakPicker::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_PeakPicker::spectrum(size_t index, DetailLevel detailLevel) const
{
    SpectrumPtr s;
    
    if (minDetailLevel_ > detailLevel)
        detailLevel = minDetailLevel_;

    switch (mode_)
    {
        case 1:
            s = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 2:
            s = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 3:
            s = dynamic_cast<detail::SpectrumList_ABI*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 4:
            s = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 5:
            s = dynamic_cast<detail::SpectrumList_ABI_T2D*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 6:
            s = dynamic_cast<detail::SpectrumList_Waters*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 7:
            s = dynamic_cast<detail::SpectrumList_Shimadzu*>(&*inner_)->spectrum(index, detailLevel, msLevelsToPeakPick_);
            break;

        case 0:
        default:
            s = inner_->spectrum(index, true); // TODO you'd think this would be "detailLevel" instead of "true" but that breaks SpectrumListFactoryTest
            break;
    }

    if (!msLevelsToPeakPick_.contains(s->cvParam(MS_ms_level).valueAs<int>()))
        return s;

    bool hasSpectrumRepresentation = s->hasCVParam(MS_spectrum_representation);
    if (!hasSpectrumRepresentation && detailLevel < DetailLevel_FullMetadata)
    {
        minDetailLevel_ = (DetailLevel) (detailLevel + 1);
        return spectrum(index, minDetailLevel_);
    }

    bool isCentroided = s->hasCVParam(MS_centroid_spectrum);
    vector<CVParam>& cvParams = s->cvParams;
    vector<CVParam>::iterator itr = cvParams.end();

    // return non-profile spectra as-is
    // (could have been acquired as centroid, or vendor may have done the centroiding)
    if (isCentroided)
    {
        // the vendor spectrum lists must put "profile spectrum" if they actually performed centroiding
        itr = boost::range::remove_if(cvParams, CVParamIs(MS_profile_spectrum));
        if (itr != cvParams.end())
            cvParams.erase(itr);
        // TODO: make this a log item instead
        //else
        //    this->warn_once("[SpectrumList_PeakPicker]: one or more spectra are already centroided, no processing needed");
        return s;
    }

    // is this declared as profile?
    bool isProfile = s->hasCVParam(MS_profile_spectrum);
    ParamGroupPtr specRepParamGroup;
    if (!isProfile)
    {
        this->warn_once("[SpectrumList_PeakPicker]: one or more spectra have undeclared profile/centroid status, assuming profile data and that peakpicking is needed");
        itr = std::find(cvParams.begin(), cvParams.end(), MS_spectrum_representation); // this should be there if nothing else
        if (itr == cvParams.end() && !s->hasCVParam(MS_spectrum_representation))
            this->warn_once("[SpectrumList_PeakPicker]: spectrum representation cvParam is missing completely");
    }
    else
    {
        itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);
        if (itr == cvParams.end())
        {
            // we know spectrum is profile, so find it in paramGroups; specRepParamGroup does double duty here as a "found" boolean
            for (const auto& pg : s->paramGroupPtrs)
            {
                if (!pg) continue;
                itr = std::find(pg->cvParams.begin(), pg->cvParams.end(), MS_profile_spectrum);
                if (itr != pg->cvParams.end())
                {
                    specRepParamGroup = pg;
                    break;
                }
            }
            if (!specRepParamGroup)
                throw std::runtime_error("[SpectrumList_PeakPicker]: spectrum isProfile==true but could not find profile cvParam (report this bug)");
        }
    }

    // make sure the spectrum has binary data
    if (!s->getMZArray().get() || !s->getIntensityArray().get())
        s = inner_->spectrum(index, true);

    // replace profile or nonspecific term with centroid term; if spectrum representation is in a paramGroup, we migrate those parameters to the spectrum itself
    if (specRepParamGroup)
    {
        s->cvParams.insert(s->cvParams.end(), specRepParamGroup->cvParams.begin(), specRepParamGroup->cvParams.end());
        itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);
        boost::range::remove(s->paramGroupPtrs, specRepParamGroup);
    }

    if (itr != cvParams.end())
        *itr = MS_centroid_spectrum;

    try
    {
        if (algorithm_ == NULL) // As with VendorOnlyPeakPicker
            throw NoVendorPeakPickingException();
        if (mode_)
            warn_once(noVendorCentroidingWarningMessage_.c_str());

        BinaryData<double>& mzs = s->getMZArray()->data;
        BinaryData<double>& intensities = s->getIntensityArray()->data;
        if (mzs.empty())
            return s;

        // remove extra arrays that are the same length as the m/z array because pwiz peak picking will not preserve the one-to-one correspondence
        for (size_t i = 2; i < s->binaryDataArrayPtrs.size(); ++i)
            if (s->binaryDataArrayPtrs[i]->data.size() == mzs.size())
                s->binaryDataArrayPtrs.erase(s->binaryDataArrayPtrs.begin() + (i--));

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
