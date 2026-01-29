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
// Order in this list mostly isn't important, the longest match wins - but in case of
// length tie, first match wins
const vector<InstrumentNameToModelMapping> nameToModelMapping =
{
    {"2010E", MS_LCMS_2010EV, Contains}, // predicted
    {"2010A", MS_LCMS_2010A, Contains}, // predicted
    {"2010", MS_LCMS_2010, Contains}, // predicted
    {"2020", MS_LCMS_2020, Contains},
    {"2050", MS_LCMS_2050, Contains},
    {"7090", MS_Shimadzu_MALDI_7090, Contains}, // predicted
    {"8030 PLUS", MS_LCMS_8030_Plus, Contains}, // predicted
    {"8030", MS_LCMS_8030, Contains}, // predicted
    {"8040", MS_LCMS_8040, Contains},
    {"8045RX", MS_LCMS_8045RX, Contains}, // predicted
    {"8045", MS_LCMS_8045, Contains},
    {"8050RX", MS_LCMS_8050RX, Contains}, // predicted
    {"8050", MS_LCMS_8050, Contains},
    {"8060NX", MS_LCMS_8060NX, Contains}, // predicted
    {"8060RX", MS_LCMS_8060RX, Contains},
    {"8060", MS_LCMS_8060, Contains},
    {"8065XE", MS_LCMS_8065XE, Contains}, // predicted
    {"9030", MS_LCMS_9030, Contains},
    {"9050", MS_LCMS_9050, Contains},
    {"AXIMA-CFR PLUS", MS_AXIMA_CFR_plus, Contains}, // predicted
    {"AXIMA CFR", MS_AXIMA_CFR_MALDI_TOF, Contains}, // predicted
    {"AXIMA-QIT", MS_AXIMA_QIT, Contains}, // predicted
    {"AXIMA-LNR", MS_AXIMA_LNR, Contains}, // predicted
    {"AXIMA-TOF", MS_AXIMA_TOF__sq__, Contains}, // predicted
    {"AXIMA RESONANCE", MS_AXIMA_Resonance, Contains}, // predicted
    {"AXIMA PERFORMANCE", MS_AXIMA_Performance_MALDI_TOF_TOF, Contains}, // predicted
    {"AXIMA CONFIDENCE", MS_AXIMA_Confidence_MALDI_TOF, Contains}, // predicted
    {"AXIMA ASSURANCE", MS_AXIMA_Assurance_Linear_MALDI_TOF, Contains}, // predicted
    {"GCMS-QP2010 PLUS", MS_GCMS_QP2010_Plus, Contains}, // predicted
    {"GCMS-QP2010 ULTRA", MS_GCMS_QP2010_Ultra, Contains}, // predicted
    {"GCMS-QP2010SE", MS_GCMS_QP2010SE, Contains}, // predicted
    {"GCMS-QP2010S", MS_GCMS_QP2010S, Contains}, // predicted
    {"GCMS-QP2010", MS_GCMS_QP2010, Contains}, // predicted
    {"GCMS-QP2020NX", MS_GCMS_QP2020NX, Contains}, // predicted
    {"GCMS-QP2020", MS_GCMS_QP2020, Contains}, // predicted
    {"GCMS-QP2050", MS_GCMS_QP2050, Contains}, // predicted
    {"GCMS-QP5000", MS_GCMS_QP5000, Contains}, // predicted
    {"GCMS-QP5050A", MS_GCMS_QP5050A, Contains}, // predicted
    {"GCMS-TQ8040NX", MS_GCMS_TQ8040NX, Contains}, // predicted
    {"GCMS-TQ8040", MS_GCMS_TQ8040, Contains}, // predicted
    {"GCMS-TQ8050NX", MS_GCMS_TQ8050NX, Contains}, // predicted
    {"GCMS-TQ 8030", MS_GCMS_TQ_8030, Contains}, // predicted
    {"GCMS-TQ 8050", MS_GCMS_TQ_8050, Contains}, // predicted
    {"IT-TOF", MS_LCMS_IT_TOF, Contains}, // predicted
    {"MALDI-8020 EASYCARE", MS_MALDI_8020_EasyCare, Contains}, // predicted
    {"MALDI-8020", MS_MALDI_8020, Contains}, // predicted
    {"MALDI-8030 EASYCARE", MS_MALDI_8030_EasyCare, Contains}, // predicted
    {"MALDI-8030", MS_MALDI_8030, Contains}, // predicted
    {"LCMS", MS_Shimadzu_Scientific_Instruments_instrument_model, Exact}, // instrument model not specified, use different fallback type to avoid error

};

inline std::string normalizeInstrumentName(const std::string& name)
{
    std::string normalized = bal::to_upper_copy(name);
    normalized = bal::replace_all_copy(normalized, " ", "");
    normalized = bal::replace_all_copy(normalized, "-", "");
    return normalized;
}

inline CVID parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string normalizedInstrumentModel = normalizeInstrumentName(instrumentModel);

    CVID bestMatch = MS_Shimadzu_instrument_model;
    size_t bestLength = 0;

    for (const auto& mapping : nameToModelMapping)
    {
        std::string normalizedMapping = normalizeInstrumentName(mapping.name);
        
        if (mapping.matchType == Exact)
        {
            if (normalizedMapping == normalizedInstrumentModel)
            {
                return mapping.modelType;
            }
            continue;
        }
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
