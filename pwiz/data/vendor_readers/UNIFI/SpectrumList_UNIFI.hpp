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

#ifdef PWIZ_READER_UNIFI
#include "pwiz_aux/msrc/utility/vendor_api/UNIFI/UnifiData.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/thread.hpp>
using namespace pwiz::vendor_api::UNIFI;
#endif // PWIZ_READER_UNIFI


namespace pwiz {
namespace msdata {
namespace detail {

class PWIZ_API_DECL SpectrumList_UNIFI : public SpectrumListBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    
#ifdef PWIZ_READER_UNIFI
    SpectrumList_UNIFI(const MSData& msd, UnifiDataPtr unifiData,
                       const Reader::Config& config);

    private:

    const MSData& msd_;
    UnifiDataPtr unifiData_;
    const Reader::Config config_;
    mutable boost::mutex readMutex;

    mutable size_t size_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
    };

    mutable std::vector<IndexEntry> index_;
    mutable std::map<std::string, size_t> idToIndexMap_;

    void createIndex() const;
#endif // PWIZ_READER_UNIFI
};


} // detail
} // msdata
} // pwiz
