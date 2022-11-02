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

#define PWIZ_SOURCE

#include "Reader_Mobilion_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {
namespace detail {
namespace Mobilion {


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(const MBIFilePtr& rawdata)
{
    return vector<InstrumentConfiguration>();
}


PWIZ_API_DECL CVID translateAsInstrumentModel(const MBIFilePtr& rawdata)
{
    return CVID_Unknown;
}

PWIZ_API_DECL CVID translatePolarity(const string& polarity)
{
    if (polarity == "Positive")
        return MS_positive_scan;
    else if (polarity == "Negative")
        return MS_negative_scan;
    throw std::runtime_error("[Mobilion::translatePolarity] unknown polarity '" + polarity + "'");
}


} // Mobilion
} // detail
} // msdata
} // pwiz
