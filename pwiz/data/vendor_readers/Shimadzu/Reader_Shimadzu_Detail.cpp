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


#define PWIZ_SOURCE

#include "Reader_Shimadzu_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <boost/range/algorithm/find_if.hpp>

namespace pwiz {
namespace msdata {
namespace detail {
namespace Shimadzu {

// the order here matters; more specific matches should be earlier in the list; all characters uppercase
const vector<InstrumentNameToModelMapping> nameToModelMapping =
{
    {"2010E", MS_LCMS_2010EV, Contains}, // predicted
    {"2010A", MS_LCMS_2010A, Contains}, // predicted
    {"2020", MS_LCMS_2020, Contains},
    {"7090", MS_Shimadzu_MALDI_7090, Contains}, // predicted
    {"8040", MS_LCMS_8040, Contains},
    {"8045", MS_LCMS_8045, Contains},
    {"8050", MS_LCMS_8050, Contains},
    {"8060RX", MS_LCMS_8060, Contains},
    {"8060", MS_LCMS_8060, Contains},
    {"9030", MS_LCMS_9030, Contains},
    {"AXIMA CFR", MS_AXIMA_CFR_MALDI_TOF, Contains}, // predicted
    {"AXIMA-QIT", MS_AXIMA_QIT, Contains}, // predicted
    {"AXIMA-CFR PLUS", MS_AXIMA_CFR_plus, Contains}, // predicted
    {"AXIMA PERFORMANCE", MS_AXIMA_Performance_MALDI_TOF_TOF, Contains}, // predicted
    {"AXIMA CONFIDENCE", MS_AXIMA_Confidence_MALDI_TOF, Contains}, // predicted
    {"AXIMA ASSURANCE", MS_AXIMA_Assurance_Linear_MALDI_TOF, Contains}, // predicted
    {"QP2010SE", MS_GCMS_QP2010SE, Contains}, // predicted
    {"IT-TOF", MS_LCMS_IT_TOF, Contains}, // predicted
    {"LCMS", MS_Shimadzu_Scientific_Instruments_instrument_model, Exact}, // instrument model not specified, use different fallback type to avoid error
    // need CVID {"9050", , Contains},

};

inline CVID parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string type = bal::to_upper_copy(instrumentModel);
    std::string typeNoSpaces = bal::replace_all_copy(type, " ", "");
    for (const auto& mapping : nameToModelMapping)
        switch (mapping.matchType)
        {
            case Exact: if (mapping.name == type) return mapping.modelType; break;
            case ExactNoSpaces: if (mapping.name == typeNoSpaces) return mapping.modelType; break;
            case Contains: if (bal::contains(type, mapping.name)) return mapping.modelType; break;
            case ContainsNoSpaces: if (bal::contains(typeNoSpaces, mapping.name)) return mapping.modelType; break;
            case StartsWith: if (bal::starts_with(type, mapping.name)) return mapping.modelType; break;
            case EndsWith: if (bal::ends_with(type, mapping.name)) return mapping.modelType; break;
            default:
                throw std::runtime_error("unknown match type");
        }
    return MS_Shimadzu_instrument_model;
}


PWIZ_API_DECL CVID translateAsInstrumentModel(const string& systemName)
{
    return parseInstrumentModelType(systemName);
}


} // Shimadzu
} // detail
} // msdata
} // pwiz
