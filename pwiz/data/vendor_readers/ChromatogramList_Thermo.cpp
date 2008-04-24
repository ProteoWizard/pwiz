#include "data/msdata/CVTranslator.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/algorithm/string.hpp"
#include "boost/filesystem/path.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "ChromatogramList_Thermo.hpp"
#include <iostream>
#include <stdexcept>


namespace pwiz {
namespace msdata {
namespace detail {

ChromatogramList_Thermo::ChromatogramList_Thermo()
{
}


size_t ChromatogramList_Thermo::size() const
{
    return index_.size();
}


const ChromatogramIdentity& ChromatogramList_Thermo::chromatogramIdentity(size_t index) const
{
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


size_t ChromatogramList_Thermo::find(const string& id) const
{
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


size_t ChromatogramList_Thermo::findNative(const string& nativeID) const
{
    return find(nativeID);
}


ChromatogramPtr ChromatogramList_Thermo::chromatogram(size_t index, bool getBinaryData) const 
{ 
    if (index>size())
        throw runtime_error(("[ChromatogramList_Thermo::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // chromatogram is created in memory, so it always has binary data
    return index_[index];
}

} // detail
} // msdata
} // pwiz
