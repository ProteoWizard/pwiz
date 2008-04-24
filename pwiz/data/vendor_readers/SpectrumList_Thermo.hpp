#include "data/msdata/MSData.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "ChromatogramList_Thermo.hpp"

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::raw;

namespace pwiz {
namespace msdata {
namespace detail {

//
// SpectrumList_Thermo
//
class SpectrumList_Thermo : public SpectrumList
{
    public:

    SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile);
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual ChromatogramListPtr Chromatograms() const;

    private:

    const MSData& msd_;
    shared_ptr<RawFile> rawfile_;
    size_t size_;
    mutable vector<SpectrumPtr> spectrumCache_;
    mutable ChromatogramListPtr chromatograms_;
    vector<SpectrumIdentity> index_;

    void createIndex();
    string findPrecursorID(int precursorMsLevel, size_t index) const;

    private:
    void addSpectrumToChromatogramList(ScanInfo& scanInfo) const;
};

} // detail
} // msdata
} // pwiz
