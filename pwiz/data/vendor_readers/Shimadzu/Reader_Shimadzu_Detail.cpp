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

// Names will be normalized to uppercase and have spaces and dashes removed for matching
// Order in this list mostly isn't important, the longest match wins - but in case of length tie,
// first match wins, so "LCMS" should come after more specific models like "2010" such that input
// "LCMS 2010" yields MS_LCMS_2010 instead of MS_Shimadzu_Scientific_Instruments_instrument_model
const vector<InstrumentNameToModelMapping> nameToModelMapping =
{
    {"2010E", MS_LCMS_2010EV}, // predicted
    {"2010A", MS_LCMS_2010A}, // predicted
    {"2010", MS_LCMS_2010},
    {"2020", MS_LCMS_2020},
    {"2050", MS_LCMS_2050},
    {"7090", MS_Shimadzu_MALDI_7090}, // predicted
    {"8030 PLUS", MS_LCMS_8030_Plus},
    {"8030", MS_LCMS_8030},
    {"8040", MS_LCMS_8040},
    {"8045RX", MS_LCMS_8045RX},
    {"8045", MS_LCMS_8045},
    {"8050RX", MS_LCMS_8050RX},
    {"8050", MS_LCMS_8050},
    {"8060NX", MS_LCMS_8060NX},
    {"8060RX", MS_LCMS_8060RX},
    {"8060", MS_LCMS_8060},
    {"8065XE", MS_LCMS_8065XE},
    {"9030", MS_LCMS_9030},
    {"9050", MS_LCMS_9050},
    {"AXIMA-CFR PLUS", MS_AXIMA_CFR_plus}, // predicted
    {"AXIMA CFR", MS_AXIMA_CFR_MALDI_TOF}, // predicted
    {"AXIMA-QIT", MS_AXIMA_QIT}, // predicted
    {"AXIMA-LNR", MS_AXIMA_LNR},
    {"AXIMA-TOF", MS_AXIMA_TOF__sq__},
    {"AXIMA RESONANCE", MS_AXIMA_Resonance},
    {"AXIMA PERFORMANCE", MS_AXIMA_Performance_MALDI_TOF_TOF}, // predicted
    {"AXIMA CONFIDENCE", MS_AXIMA_Confidence_MALDI_TOF}, // predicted
    {"AXIMA ASSURANCE", MS_AXIMA_Assurance_Linear_MALDI_TOF}, // predicted
    {"GCMS-QP2010 PLUS", MS_GCMS_QP2010_Plus},
    {"GCMS-QP2010 ULTRA", MS_GCMS_QP2010_Ultra},
    {"GCMS-QP2010SE", MS_GCMS_QP2010SE}, // predicted
    {"GCMS-QP2010S", MS_GCMS_QP2010S},
    {"GCMS-QP2010", MS_GCMS_QP2010},
    {"GCMS-QP2020NX", MS_GCMS_QP2020NX},
    {"GCMS-QP2020", MS_GCMS_QP2020},
    {"GCMS-QP2050", MS_GCMS_QP2050},
    {"GCMS-QP5000", MS_GCMS_QP5000},
    {"GCMS-QP5050A", MS_GCMS_QP5050A},
    {"GCMS-TQ8040NX", MS_GCMS_TQ8040NX},
    {"GCMS-TQ8040", MS_GCMS_TQ8040},
    {"GCMS-TQ8050NX", MS_GCMS_TQ8050NX},
    {"GCMS-TQ 8030", MS_GCMS_TQ_8030},
    {"GCMS-TQ 8050", MS_GCMS_TQ_8050},
    {"IT-TOF", MS_LCMS_IT_TOF}, // predicted
    {"MALDI-8020 EASYCARE", MS_MALDI_8020_EasyCare},
    {"MALDI-8020", MS_MALDI_8020},
    {"MALDI-8030 EASYCARE", MS_MALDI_8030_EasyCare},
    {"MALDI-8030", MS_MALDI_8030},
    {"LCMS", MS_Shimadzu_Scientific_Instruments_instrument_model}, // instrument model not specified, use different fallback type to avoid error

};

inline CVID parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string normalizedInstrumentModel = bal::to_upper_copy(instrumentModel);
    normalizedInstrumentModel = bal::replace_all_copy(normalizedInstrumentModel, " ", "");
    normalizedInstrumentModel = bal::replace_all_copy(normalizedInstrumentModel, "-", "");

    CVID bestMatch = MS_Shimadzu_instrument_model;
    size_t bestLength = 0;

    for (const auto& mapping : nameToModelMapping)
    {
        std::string normalizedMapping(mapping.name);
        normalizedMapping = bal::to_upper_copy(normalizedMapping);
        normalizedMapping = bal::replace_all_copy(normalizedMapping, " ", "");
        normalizedMapping = bal::replace_all_copy(normalizedMapping, "-", "");

        if (bal::contains(normalizedInstrumentModel, normalizedMapping) && normalizedMapping.length() > bestLength)
        {
            bestMatch = mapping.modelType;
            bestLength = normalizedMapping.length();
        }
    }

    return bestMatch;
}


PWIZ_API_DECL CVID translateAsInstrumentModel(const string& systemName)
{
    return parseInstrumentModelType(systemName);
}


} // Shimadzu
} // detail
} // msdata
} // pwiz
