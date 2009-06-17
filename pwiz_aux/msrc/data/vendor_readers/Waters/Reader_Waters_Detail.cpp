//
// Reader_Waters_Detail.cpp
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

#include "Reader_Waters_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(RawDataPtr rawdata)
{
    return vector<InstrumentConfiguration>();
}


PWIZ_API_DECL CVID translateAsInstrumentModel(RawDataPtr rawdata)
{
    return CVID_Unknown;
}


PWIZ_API_DECL
void translateFunctionType(FunctionType functionType,
                           int& msLevel,
                           CVID& spectrumType)
{
    switch (functionType)
    {
        case FunctionType_MSMSMS:
            msLevel = 3;
            spectrumType = MS_MSn_spectrum;
            break;

        case FunctionType_MSMS:
            msLevel = 2;
            spectrumType = MS_MSn_spectrum;
            break;

        case FunctionType_MRM:
        case FunctionType_Daughter:
            msLevel = 2;
            spectrumType = MS_SRM_spectrum;
            break;

        case FunctionType_MS:
        case FunctionType_Scan:
        case FunctionType_Survey:
        case FunctionType_MALDI_TOF:
            msLevel = 1;
            spectrumType = MS_MS1_spectrum;
            break;

        default:
            throw std::runtime_error("[translateFunctionType] Unknown function type.");
    }
}


} // detail
} // msdata
} // pwiz
