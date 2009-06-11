#define PWIZ_SOURCE

#include "Reader_Waters_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(const string& rawpath)
{
    return vector<InstrumentConfiguration>();
}


PWIZ_API_DECL CVID translateAsInstrumentModel(const string& rawpath)
{
    return CVID_Unknown;
}


PWIZ_API_DECL
void translateFunctionType(const string& funcType,
                           int& msLevel,
                           CVID& scanningMethod,
                           CVID& spectrumType)
{
    if (funcType.find("MSMSMS") != string::npos)
    {
        msLevel = 3;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("MSMS") != string::npos)
    {
        msLevel = 2;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("Daughter") != string::npos)
    {
        msLevel = 2;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("MS")!= string::npos)
    {
        msLevel = 1;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("Scan") != string::npos)
    {
        msLevel = 1;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("Survey") != string::npos)
    {
        msLevel = 1;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else if (funcType.find("Maldi TOF") != string::npos)
    {
        msLevel = 1;
        scanningMethod = MS_full_scan;
        spectrumType = MS_MSn_spectrum;
    }
    else
    {
        msLevel = 0;
        scanningMethod = CVID_Unknown;
        spectrumType = CVID_Unknown;
    }
}


} // detail
} // msdata
} // pwiz
