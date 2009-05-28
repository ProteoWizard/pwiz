//
// Reader_Agilent_Detail.hpp
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


#ifndef _READER_AGILENT_DETAIL_HPP_ 
#define _READER_AGILENT_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/data/msdata/MSData.hpp"
#include "boost/shared_ptr.hpp"

//#import "BaseCommon.tlb" no_namespace, named_guids
//#import "BaseDataAccess.tlb" rename_namespace("BDA"), named_guids
//#import "MassSpecDataReader.tlb" rename_namespace("MSDR"), named_guids
#include "BaseCommon.tlh"
#include "BaseDataAccess.tlh"
#include "MassSpecDataReader.tlh"

#include <vector>

namespace pwiz {
namespace msdata {
namespace detail {


typedef MSDR::IMsdrDataReaderPtr IDataReaderPtr;
typedef BDA::IBDAFileInformationPtr IFileInformationPtr;
typedef BDA::IBDAMSScanFileInformationPtr IScanInformationPtr;
typedef BDA::IBDASpecDataPtr ISpectrumPtr;
typedef BDA::IBDASpecFilterPtr ISpectrumFilterPtr;
typedef MSDR::IMsdrPeakFilterPtr IPeakFilterPtr;
typedef BDA::IBDAChromDataPtr IChromatogramPtr;
typedef BDA::IBDAChromFilterPtr IChromatogramFilterPtr;


struct AgilentDataReader
{
    AgilentDataReader(const std::string& path);
    ~AgilentDataReader();

    IDataReaderPtr dataReaderPtr;
    IScanInformationPtr scanFileInfoPtr;

    automation_vector<double> ticTimes, bpcTimes;
    automation_vector<float> ticIntensities, bpcIntensities;
};

typedef boost::shared_ptr<AgilentDataReader> AgilentDataReaderPtr;


PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(AgilentDataReaderPtr rawfile);

PWIZ_API_DECL CVID translateAsInstrumentModel(DeviceType deviceType);
PWIZ_API_DECL CVID translateAsSpectrumType(MSScanType scanType);
PWIZ_API_DECL int translateAsMSLevel(MSScanType scanType);
PWIZ_API_DECL CVID translateAsActivationType(MSScanType scanType);
PWIZ_API_DECL CVID translateAsPolarityType(IonPolarity polarity);
PWIZ_API_DECL CVID translateAsIonizationType(IonizationMode ionizationMode);
PWIZ_API_DECL CVID translateAsInletType(IonizationMode ionizationMode);


} // detail
} // msdata
} // pwiz

#endif // _READER_AGILENT_DETAIL_HPP_
