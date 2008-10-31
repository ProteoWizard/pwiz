//
// SpectrumList_NativeCentroider.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "SpectrumList_NativeCentroider.hpp"

#ifndef PWIZ_NO_READER_THERMO
#include "data/vendor_readers/SpectrumList_Thermo.hpp"
#endif

#ifndef PWIZ_NO_READER_BRUKER
#include "pwiz_aux/msrc/data/vendor_readers/SpectrumList_Bruker.hpp"
#endif


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_NativeCentroider::SpectrumList_NativeCentroider(
    const msdata::SpectrumListPtr& inner,
    const IntegerSet& msLevelsToCentroid)
:   SpectrumListWrapper(inner),
    msLevelsToCentroid_(msLevelsToCentroid),
    mode_(0)
{
    // check to see if we're able to do native centroiding, based on the SpectrumList type

    #ifndef PWIZ_NO_READER_THERMO
    detail::SpectrumList_Thermo* thermo = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner);
    if (thermo)
    {
        mode_ = 1;
    }
    #endif

    #ifndef PWIZ_NO_READER_BRUKER
    detail::SpectrumList_Bruker* bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner);
    if (bruker)
    {
        mode_ = 2;
    }
    #endif

    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.set(MS_peak_picking);
    if (mode_ == 1)
        method.userParams.push_back(UserParam("Thermo/Xcalibur peak picking"));
    else if (mode_ == 2)
        method.userParams.push_back(UserParam("Bruker/Agilent/CompassXtract peak picking"));
    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL bool SpectrumList_NativeCentroider::accept(const msdata::SpectrumListPtr& inner)
{
    #ifndef PWIZ_NO_READER_THERMO
    detail::SpectrumList_Thermo* thermo = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner);
    if (thermo)
        return true;
    #endif

    #ifndef PWIZ_NO_READER_BRUKER
    detail::SpectrumList_Bruker* bruker = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner);
    if (bruker)
        return true;
    #endif

    return false;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_NativeCentroider::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr centroidSpectrum;

    switch (mode_)
    {
        #ifndef PWIZ_NO_READER_THERMO
        case 1:
            centroidSpectrum = dynamic_cast<detail::SpectrumList_Thermo*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToCentroid_);
            break;
        #endif

        #ifndef PWIZ_NO_READER_BRUKER
        case 2:
            centroidSpectrum = dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->spectrum(index, getBinaryData, msLevelsToCentroid_);
            break;
        #endif

        default:
            return inner_->spectrum(index, getBinaryData);
    }

    centroidSpectrum->dataProcessingPtr = dp_;
    return centroidSpectrum;
}


} // namespace analysis 
} // namespace pwiz

