//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#ifndef _SPECTRUMLISTCACHE_HPP_
#define _SPECTRUMLISTCACHE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "MemoryMRUCache.hpp"
#include "SpectrumListWrapper.hpp"


namespace pwiz {
namespace msdata {


/// adds a level of flexible MRU caching to a SpectrumList processor chain
class PWIZ_API_DECL SpectrumListCache : public SpectrumListWrapper
{
    public:

    /// a cache mapping spectrum indices to SpectrumPtrs
    struct CacheEntry { CacheEntry(size_t i, SpectrumPtr s) : index(i), spectrum(s) {}; size_t index; SpectrumPtr spectrum; };
    typedef MemoryMRUCache<CacheEntry, BOOST_MULTI_INDEX_MEMBER(CacheEntry, size_t, index) > CacheType;

    SpectrumListCache(const SpectrumListPtr& inner,
                      MemoryMRUCacheMode cacheMode,
                      size_t cacheSize);

    /// returns the requested spectrum which may or may not be cached depending on
    /// the current cache mode
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    /// returns a reference to the cache, to enable clearing it or changing the mode
    CacheType& spectrumCache();

    /// returns a const-reference to the cache
    const CacheType& spectrumCache() const;

    protected:
    mutable CacheType spectrumCache_;

    private:
    SpectrumListCache(SpectrumListCache&);
    SpectrumListCache& operator=(SpectrumListCache&);
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLISTCACHE_HPP_
