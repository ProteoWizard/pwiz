//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2025 University of Washington
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


#ifndef _READER_SHIMADZU_DETAIL_HPP_ 
#define _READER_SHIMADZU_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"

namespace pwiz {
namespace msdata {
namespace detail {
namespace Shimadzu {

using namespace pwiz::cv;


namespace {

    enum MatchType
    {
        Exact,
        Contains
    };

} // namespace

struct InstrumentNameToModelMapping
{

    const char* name;
    CVID modelType;
    MatchType matchType;
};

extern const std::vector<InstrumentNameToModelMapping> nameToModelMapping;

PWIZ_API_DECL CVID translateAsInstrumentModel(const std::string& systemName);

} // Shimadzu
} // detail
} // msdata
} // pwiz

#endif // _READER_SHIMADZU_DETAIL_HPP_
