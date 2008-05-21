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
#include "data/vendor_readers/SpectrumList_Thermo.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace msdata::detail;
using namespace pwiz::util;
using namespace std;


PWIZ_API_DECL SpectrumList_NativeCentroider::SpectrumList_NativeCentroider(
    const msdata::SpectrumListPtr& inner,
    const IntegerSet& msLevelsToCentroid)
:   SpectrumListWrapper(inner),
    msLevelsToCentroid_(msLevelsToCentroid)
{
    // check to see if we're able to do native centroiding, based on the SpectrumList type

    SpectrumList_Thermo* thermo = dynamic_cast<SpectrumList_Thermo*>(&*inner);
    if (thermo)
        return;

    throw runtime_error("[SpectrumList_NativeCentroider] No native centroiding available.");
}


PWIZ_API_DECL SpectrumPtr SpectrumList_NativeCentroider::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumList_Thermo* thermo = dynamic_cast<SpectrumList_Thermo*>(&*inner_);
    if (thermo)
    {
        SpectrumPtr spectrum = inner_->spectrum(index, false);
        int msLevel = spectrum->cvParam(MS_ms_level).valueAs<int>();
        thermo->centroidSpectra(msLevelsToCentroid_.contains(msLevel));
        return inner_->spectrum(index, getBinaryData);
    }

    throw runtime_error("[SpectrumList_NativeCentroider::spectrum()] This isn't happening.");
}


} // namespace analysis 
} // namespace pwiz

