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

#ifndef _PROTEINLISTCACHE_HPP_
#define _PROTEINLISTCACHE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"
#include "ProteinListWrapper.hpp"
#include "pwiz/utility/misc/mru_list.hpp"


namespace pwiz {
namespace proteome {


enum PWIZ_API_DECL ProteinListCacheMode
{
    ProteinListCacheMode_Off,
    ProteinListCacheMode_MetaDataOnly,
    ProteinListCacheMode_MetaDataAndSequence
};


/// adds a level of flexible MRU caching to a ProteinList processor chain
class PWIZ_API_DECL ProteinListCache : public ProteinListWrapper
{
    public:

    /// a cache mapping spectrum indices to ProteinPtrs
    typedef std::pair<size_t, ProteinPtr> KeyValuePair;
    BOOST_STATIC_CONSTANT(unsigned, first_offset = offsetof(KeyValuePair, first));
    typedef pwiz::util::mru_list<KeyValuePair, boost::multi_index::member_offset<KeyValuePair, size_t, first_offset> > CacheType;

    ProteinListCache(const ProteinListPtr& inner,
                     ProteinListCacheMode cacheMode,
                     size_t cacheSize);

    /// returns the requested spectrum which may or may not be cached depending on
    /// the current cache mode
    virtual ProteinPtr protein(size_t index, bool getSequence = true) const;

    virtual size_t find(const std::string& id) const;

    /// set the caching mode
    /// note: if the new mode is different than the current mode, the cache will be cleared
    void setMode(ProteinListCacheMode mode);

    /// get the current caching mode
    ProteinListCacheMode mode() const;

    /// get a const-reference to the cache
    const CacheType& cache() const {return cache_;}

    private:
    ProteinListCache(ProteinListCache&);
    ProteinListCache& operator=(ProteinListCache&);
    mutable CacheType cache_;
    ProteinListCacheMode mode_;
};


} // namespace msdata
} // namespace pwiz


#endif // _PROTEINLISTCACHE_HPP_
