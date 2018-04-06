//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _READER_AGILENT_DETAIL_HPP_ 
#define _READER_AGILENT_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz_aux/msrc/utility/vendor_api/Agilent/MassHunterData.hpp"
#include <vector>

namespace pwiz {
namespace msdata {
namespace detail {
namespace Agilent {

using namespace pwiz::vendor_api::Agilent;

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(MassHunterDataPtr rawfile);

PWIZ_API_DECL CVID translateAsInstrumentModel(DeviceType deviceType);
PWIZ_API_DECL CVID translateAsSpectrumType(MSScanType scanType);
PWIZ_API_DECL int translateAsMSLevel(MSScanType scanType);
PWIZ_API_DECL CVID translateAsActivationType(DeviceType deviceType);
PWIZ_API_DECL CVID translateAsPolarityType(IonPolarity polarity);
PWIZ_API_DECL CVID translateAsIonizationType(IonizationMode ionizationMode);
PWIZ_API_DECL CVID translateAsInletType(IonizationMode ionizationMode);


} // Agilent
} // detail
} // msdata
} // pwiz

#endif // _READER_AGILENT_DETAIL_HPP_
