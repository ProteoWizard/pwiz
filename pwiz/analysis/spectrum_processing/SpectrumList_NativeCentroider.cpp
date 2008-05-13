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


namespace pwiz {
namespace analysis {


using namespace msdata::detail;


PWIZ_API_DECL SpectrumList_NativeCentroider::SpectrumList_NativeCentroider(const msdata::SpectrumListPtr& inner)
:   SpectrumListWrapper(inner)
{
    SpectrumList_Thermo* thermo = dynamic_cast<SpectrumList_Thermo*>(&*inner);
    if (thermo)
    {
        thermo->centroidSpectra(true);
        return;
    }

    throw runtime_error("[SpectrumList_NativeCentroider] No native centroiding available.");
}


} // namespace analysis 
} // namespace pwiz

