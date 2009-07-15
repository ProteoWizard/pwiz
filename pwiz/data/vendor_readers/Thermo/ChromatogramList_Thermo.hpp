#ifndef _CHROMATOGRAMLIST_THERMO_
#define _CHROMATOGRAMLIST_THERMO_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include <map>
#include <vector>
#include <boost/thread/once.hpp>

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::vendor_api::Thermo;

namespace pwiz {
namespace msdata {
namespace detail {

class PWIZ_API_DECL ChromatogramList_Thermo : public ChromatogramListBase
{
public:

    ChromatogramList_Thermo(const MSData& msd, RawFilePtr rawfile);
    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    
    private:

    const MSData& msd_;
    shared_ptr<RawFile> rawfile_;

    mutable boost::once_flag indexInitialized_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        ControllerType controllerType;
        long controllerNumber;
        string filter;
        double q1, q3;
        double q3Offset;
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idMap_;

    void createIndex() const;
};

} // detail
} // msdata
} // pwiz

#endif // _CHROMATOGRAMLIST_THERMO_
