#include "utility/misc/Export.hpp"
#include "data/msdata/MSData.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "utility/misc/IntegerSet.hpp"

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
class PWIZ_API_DECL SpectrumList_Thermo : public SpectrumList
{
    public:

    SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile);
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;


    private:

    const MSData& msd_;
    shared_ptr<RawFile> rawfile_;
    size_t size_;
    mutable vector<SpectrumPtr> spectrumCache_;
    vector<SpectrumIdentity> index_;

    void createIndex();
    string findPrecursorID(int precursorMsLevel, size_t index) const;
};

} // detail
} // msdata
} // pwiz
