//
// Reader_ABI_Detail.hpp
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
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"
#include <vector>

namespace pwiz {
namespace msdata {
namespace detail {

PWIZ_API_DECL
InstrumentConfiguration translateAsInstrumentConfiguration(pwiz::wiff::WiffFilePtr wifffile);

PWIZ_API_DECL CVID translateAsInstrumentModel(pwiz::wiff::InstrumentModel instrumentModel);
PWIZ_API_DECL CVID translateAsIonSource(pwiz::wiff::IonSourceType ionSourceType);
PWIZ_API_DECL CVID translateAsSpectrumType(pwiz::wiff::ScanType scanType);
PWIZ_API_DECL int translateAsMSLevel(pwiz::wiff::ScanType scanType);
PWIZ_API_DECL CVID translate(pwiz::wiff::Polarity polarity);

} // detail
} // msdata
} // pwiz

#endif // _READER_ABI_DETAIL_HPP_
