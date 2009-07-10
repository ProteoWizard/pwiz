#ifndef _READER_THERMO_DETAIL_HPP_ 
#define _READER_THERMO_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include <vector>

using namespace pwiz::vendor_api::Thermo;

namespace pwiz {
namespace msdata {
namespace detail {

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(RawFile& rawfile);

PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModelType instrumentModelType);
PWIZ_API_DECL CVID translateAsScanningMethod(ScanType scanType);
PWIZ_API_DECL CVID translateAsSpectrumType(ScanType scanType);
PWIZ_API_DECL CVID translate(MassAnalyzerType type);
PWIZ_API_DECL CVID translateAsIonizationType(IonizationType ionizationType);
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType);
PWIZ_API_DECL CVID translate(PolarityType polarityType);
PWIZ_API_DECL CVID translate(ActivationType activationType);

} // detail
} // msdata
} // pwiz

#endif // _READER_THERMO_DETAIL_HPP_
