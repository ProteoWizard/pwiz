//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _READER_THERMO_DETAIL_HPP_ 
#define _READER_THERMO_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz_aux/msrc/utility/vendor_api/thermo/RawFile.h"
#include <vector>

using namespace pwiz::vendor_api::Thermo;

namespace pwiz {
namespace msdata {
namespace detail {
namespace Thermo {

using namespace pwiz::cv;

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(RawFile& rawfile);

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(const Component& commonSource,
                                                                    InstrumentModelType model);

PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModelType instrumentModelType);
PWIZ_API_DECL CVID translateAsScanningMethod(ScanType scanType);
PWIZ_API_DECL CVID translateAsSpectrumType(ScanType scanType);
PWIZ_API_DECL CVID translate(MassAnalyzerType type);
PWIZ_API_DECL CVID translateAsIonizationType(IonizationType ionizationType);
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType);
PWIZ_API_DECL CVID translate(PolarityType polarityType);
PWIZ_API_DECL void setActivationType(ActivationType activationType, ActivationType supplementalActivationType, Activation& activation);

constexpr double MAX_SRM_SCAN_RANGE = 1.0; // if Q3 m/z range is larger than this, don't produce SRM chromatogram


PWIZ_API_DECL struct MassListTableEntry
{
    std::string compound;
    double precursor_mz;
    double product_mz;
    double collision_energy;
    std::string polarity;
};

typedef std::map<double, std::map<double, MassListTableEntry>> MassListTable; /// keyed by precursor m/z, then product m/z

PWIZ_API_DECL MassListTable parseMassListTables(const std::string& instrumentMethod);


} // Thermo
} // detail
} // msdata
} // pwiz

#endif // _READER_THERMO_DETAIL_HPP_
