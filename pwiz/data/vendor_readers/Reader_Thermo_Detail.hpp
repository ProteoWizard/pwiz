#ifndef _READER_THERMO_DETAIL_HPP_ 
#define _READER_THERMO_DETAIL_HPP_ 

#include "utility/misc/Export.hpp"
#include "data/msdata/MSData.hpp"
#include "utility/vendor_api/thermo/ScanFilter.h"

using namespace pwiz::raw;

namespace pwiz {
namespace msdata {
namespace detail {

PWIZ_API_DECL CVParam translateAsScanningMethod(ScanType scanType);
PWIZ_API_DECL CVParam translateAsSpectrumType(ScanType scanType);
PWIZ_API_DECL CVParam translate(MassAnalyzerType type);
PWIZ_API_DECL CVParam translateAsIonizationType(IonizationType ionizationType);
PWIZ_API_DECL CVParam translateAsInletType(IonizationType ionizationType);
PWIZ_API_DECL CVParam translate(PolarityType polarityType);
PWIZ_API_DECL CVParam translate(ActivationType activationType);

} // detail
} // msdata
} // pwiz

#endif // _READER_THERMO_DETAIL_HPP_
