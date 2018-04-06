//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _READER_ABI_T2D_DETAIL_HPP_ 
#define _READER_ABI_T2D_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include <vector>

#ifdef PWIZ_READER_ABI_T2D
#include "pwiz_aux/msrc/utility/vendor_api/ABI/T2D/T2D_Data.hpp"

namespace pwiz {
namespace msdata {
namespace detail {

using namespace pwiz::vendor_api::ABI::T2D;

PWIZ_API_DECL
InstrumentConfigurationPtr translateAsInstrumentConfiguration(const Data& t2d_data);

PWIZ_API_DECL CVID translateAsSpectrumType(SpectrumType spectrumType);
PWIZ_API_DECL CVID translate(IonMode ionMode);

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI_T2D

#endif // _READER_ABI_T2D_DETAIL_HPP_
