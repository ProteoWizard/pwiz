//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _MSDATACACHE_HPP_
#define _MSDATACACHE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;


///
/// simple memory cache for common MSData info
///
/// Memory caching is useful to minimize data retrieval expenses:
/// - disk I/O and decoding of binary data
/// - indirection and pointer validation
/// - parameter list searching
/// - lexical casting
///
/// MSDataCache is a vector of SpectrumInfo objects, but also implements the
/// MSDataAnalyzer interface.  It can be used in two ways:
/// 1) Updated from the outside via MSDataAnalyzer interface
/// 2) Automatic updating via spectrumInfo() access method
///
/// Spectrum binary data (SpectrumInfo::data) is
/// freed from the cache using a LRU (least recently used) algorithm.
/// Binary data will be freed only to make room for a new update.  The
/// default cache size is 1, i.e. only the most recently updated binary 
/// data array is stored.  Note that modifying the SpectrumInfo objects
/// directly through the vector interface circumvents the LRU mechanism. 
///
/// Usage #1:  MSDataCache should be placed first in an 
/// MSDataAnalyzerContainer, so that it receives update() first.  Other 
/// analyzers may then use it (preferably as const MSDataCache&) to access 
/// spectrum data when they receive update().  On updateRequested(),
/// MSDataCache returns UpdateRequest_Ok to indicate that it should
/// receive any updates that are requested by other analyzers.  If no 
/// other analyzer requests an update, the cache will not be updated.
///
/// Usage #2:  Instantiate independently and call open() to set reference to an
/// MSData object.  Calls to spectrumInfo() will return the requested SpectrumInfo,
/// automatically updating the cache via call to SpectrumList::spectrum() if
/// necessary.
///
class PWIZ_API_DECL MSDataCache : public std::vector<SpectrumInfo>,
                                  public MSDataAnalyzer
                    
{
    public:

    /// MSDataCache configuration
    struct PWIZ_API_DECL Config
    {
        size_t binaryDataCacheSize;
        Config(size_t cacheSize = 1) : binaryDataCacheSize(cacheSize) {}
    };

    MSDataCache(const Config& config = Config());

    /// \name MSDataAnalyzer interface 
    //@{
    virtual void open(const DataInfo& dataInfo);

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo, 
                                          const SpectrumIdentity& spectrumIdentity) const
    { 
        // receive update() only if requested by another analyzer 
        return MSDataAnalyzer::UpdateRequest_Ok;
    }

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);
    //@}

    /// access to SpectrumInfo with automatic update (open() must be called first)
    const SpectrumInfo& spectrumInfo(size_t index, bool getBinaryData = false);

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    MSDataCache(MSDataCache&);
    MSDataCache& operator=(MSDataCache&);
};


} // namespace analysis 
} // namespace pwiz


#endif // _MSDATACACHE_HPP_

