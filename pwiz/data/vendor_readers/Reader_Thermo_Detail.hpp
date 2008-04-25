#ifndef _READER_THERMO_DETAIL_HPP_ 
#define _READER_THERMO_DETAIL_HPP_ 

#include "data/msdata/MSData.hpp"
#include "utility/vendor_api/thermo/ScanFilter.h"

using namespace pwiz::raw;

namespace pwiz {
namespace msdata {
namespace detail {

CVParam translateAsScanningMethod(ScanType scanType);
CVParam translateAsSpectrumType(ScanType scanType);
CVParam translate(MassAnalyzerType type);
CVParam translateAsIonizationType(IonizationType ionizationType);
CVParam translateAsInletType(IonizationType ionizationType);
CVParam translate(PolarityType polarityType);
CVParam translate(ActivationType activationType);

} // detail
} // msdata
} // pwiz

#endif // _READER_THERMO_DETAIL_HPP_
