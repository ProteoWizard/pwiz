//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/thread/mutex.hpp>
#include <boost/container/flat_map.hpp>

#ifdef PWIZ_READER_SHIMADZU
#include "pwiz_aux/msrc/utility/vendor_api/Shimadzu/ShimadzuReader.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/thread.hpp>
using namespace pwiz::vendor_api::Shimadzu;
#endif // PWIZ_READER_SHIMADZU


namespace pwiz {
namespace msdata {
namespace detail {

class PWIZ_API_DECL SpectrumList_Shimadzu : public SpectrumListBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    
#ifdef PWIZ_READER_SHIMADZU
    SpectrumList_Shimadzu(const MSData& msd, ShimadzuReaderPtr rawfile, const Reader::Config& config);

    private:

    const MSData& msd_;
    const Reader::Config config_;
    ShimadzuReaderPtr rawfile_;
    mutable boost::mutex readMutex;

    mutable size_t size_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
        int scanNumber;
    };

    mutable std::vector<IndexEntry> index_;
    mutable std::map<std::string, size_t> idToIndexMap_;

    // Cache last accessed spectrum for fast in order access
    mutable boost::mutex spectrum_mutex;
    mutable size_t spectrumLastIndex_;
    mutable pwiz::vendor_api::Shimadzu::SpectrumPtr spectrumLast_;

    void createIndex() const;
#endif // PWIZ_READER_SHIMADZU
};


} // detail
} // msdata
} // pwiz
