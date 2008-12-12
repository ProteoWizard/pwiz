#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "dacserver_4-1.h"
#include <map>

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;

namespace pwiz {
namespace msdata {
namespace detail {


struct PWIZ_API_DECL FunctionMetaData
{
    string type;
    int msLevel;
    CVID scanningMethod;
    CVID spectrumType;
};


//
// SpectrumList_Waters
//
class PWIZ_API_DECL SpectrumList_Waters : public SpectrumList
{
    public:

    SpectrumList_Waters(const MSData& msd, const string& rawpath);
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    //virtual ChromatogramListPtr Chromatograms() const;


    private:

    const MSData& msd_;
    string rawpath_;
    size_t size_;
    short functionCount_;
    vector<pair<SpectrumIdentity, pair<short, long> > > index_;

    // nativeIdToIndexMap_[<function #>][<scan #>] == index
    map<short, map<long, size_t> > nativeIdToIndexMap_;

    map<short, FunctionMetaData> functionToMetaDataMap_;

    void createIndex();
    //string findPrecursorID(int precursorMsLevel, size_t index) const;

    // DAC COM objects
    IDACFunctionInfoPtr pFunctionInfo_;
    IDACScanStatsPtr pScanStats_;
    IDACExScanStatsPtr pExScanStats_;
    IDACSpectrumPtr pSpectrum_;

    private:
    //void addSpectrumToChromatogramList(ScanInfo& scanInfo) const;
};

} // detail
} // msdata
} // pwiz
