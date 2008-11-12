#ifndef _READER_WATERS_DETAIL_HPP_ 
#define _READER_WATERS_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include <vector>

namespace pwiz {
namespace msdata {
namespace detail {

PWIZ_API_DECL
std::vector<InstrumentConfiguration> createInstrumentConfigurations(const std::string& rawpath);

PWIZ_API_DECL CVID translateAsInstrumentModel(const std::string& rawpath);
PWIZ_API_DECL void translateFunctionType(const std::string& functionType, int& msLevel, CVID& scanningMethod, CVID& spectrumType);

/*PWIZ_API_DECL CVID translate(MassAnalyzerType type);
PWIZ_API_DECL CVID translateAsIonizationType(IonizationType ionizationType);
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType);
PWIZ_API_DECL CVID translate(PolarityType polarityType);
PWIZ_API_DECL CVID translate(ActivationType activationType);*/

} // detail
} // msdata
} // pwiz

#endif // _READER_WATERS_DETAIL_HPP_
