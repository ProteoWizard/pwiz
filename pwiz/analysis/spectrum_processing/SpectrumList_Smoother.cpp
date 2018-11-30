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


#include "SpectrumList_Smoother.hpp"
#include "pwiz/analysis/common/SavitzkyGolaySmoother.hpp"
//#include "WhittakerSmoother.hpp"
#include "pwiz/utility/misc/Container.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_Smoother::SpectrumList_Smoother(
        const msdata::SpectrumListPtr& inner,
        SmootherPtr algorithm,
        const IntegerSet& msLevelsToSmooth)
:   SpectrumListWrapper(inner),
    algorithm_(algorithm),
    msLevelsToSmooth_(msLevelsToSmooth)
{
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_smoothing);
    method.userParams.push_back(UserParam("Savitzky-Golay smoothing (9 point window)"));
    
    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_Smoother::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Smoother::spectrum(size_t index, bool getBinaryData) const
{
    //if (!getBinaryData)
    //    return inner_->spectrum(index, false);

    SpectrumPtr s = inner_->spectrum(index, true);

    vector<CVParam>& cvParams = s->cvParams;
    vector<CVParam>::iterator itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);

    // return non-profile spectra as-is
    if (itr == cvParams.end())
        return s;

    try
    {
        BinaryData<double>& mzs = s->getMZArray()->data;
        BinaryData<double>& intensities = s->getIntensityArray()->data;
        vector<double> smoothedMZs, smoothedIntensities;
        algorithm_->smooth(mzs, intensities, smoothedMZs, smoothedIntensities);
        mzs.swap(smoothedMZs);
        intensities.swap(smoothedIntensities);
        s->defaultArrayLength = mzs.size();
    }
    catch(std::exception& e)
    {
        throw std::runtime_error(std::string("[SpectrumList_Smoother] Error smoothing intensity data: ") + e.what());
    }

    s->dataProcessingPtr = dp_;
    return s;
}


} // namespace analysis 
} // namespace pwiz
