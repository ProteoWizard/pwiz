#ifndef _CHROMATOGRAMLIST_THERMO_
#define _CHROMATOGRAMLIST_THERMO_
#include "data/msdata/MSData.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include <map>
#include <vector>

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::raw;

namespace pwiz {
namespace msdata {
namespace detail {

class ChromatogramList_Thermo : public ChromatogramList
{
public:

    ChromatogramList_Thermo();
    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    
    map<string, size_t> idMap_;
    vector<ChromatogramPtr> index_;
};

} // detail
} // msdata
} // pwiz

#endif // _CHROMATOGRAMLIST_THERMO_
