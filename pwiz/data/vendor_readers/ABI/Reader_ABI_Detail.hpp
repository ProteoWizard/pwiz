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


#ifndef _READER_ABI_DETAIL_HPP_ 
#define _READER_ABI_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include <vector>

#ifdef PWIZ_READER_ABI
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"

namespace pwiz {
namespace msdata {
namespace detail {
namespace ABI {

using namespace pwiz::vendor_api::ABI;

PWIZ_API_DECL
InstrumentConfigurationPtr translateAsInstrumentConfiguration(InstrumentModel instrumentModel, IonSourceType ionSource);

PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModel instrumentModel);
PWIZ_API_DECL CVID translateAsIonSource(IonSourceType ionSourceType);
PWIZ_API_DECL CVID translateAsSpectrumType(ExperimentType scanType);
PWIZ_API_DECL CVID translate(Polarity polarity);

} // ABI
} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI

#endif // _READER_ABI_DETAIL_HPP_
