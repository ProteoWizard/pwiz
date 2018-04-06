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


#ifndef _READER_WATERS_DETAIL_HPP_ 
#define _READER_WATERS_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include <vector>

#ifdef PWIZ_READER_WATERS
#include "pwiz_aux/msrc/utility/vendor_api/Waters/WatersRawFile.hpp"
using namespace pwiz::vendor_api::Waters;

namespace pwiz {
namespace msdata {
namespace detail {
namespace Waters {

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(RawDataPtr rawdata);

PWIZ_API_DECL CVID translateAsInstrumentModel(RawDataPtr rawdata);
PWIZ_API_DECL void translateFunctionType(PwizFunctionType functionType, int& msLevel, CVID& spectrumType);
PWIZ_API_DECL CVID translateAsIonizationType(PwizIonizationType ionizationType);
PWIZ_API_DECL CVID translate(PwizPolarityType polarityType);

/*PWIZ_API_DECL CVID translate(MassAnalyzerType type);
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType);
PWIZ_API_DECL CVID translate(ActivationType activationType);*/

} // Waters
} // detail
} // msdata
} // pwiz

#endif

#endif // _READER_WATERS_DETAIL_HPP_
