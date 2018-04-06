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

#ifndef _MEMORYMRUCACHE_HPP_
#define _MEMORYMRUCACHE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/mru_list.hpp"


namespace pwiz {
namespace msdata {


enum PWIZ_API_DECL MemoryMRUCacheMode
{
    MemoryMRUCacheMode_Off,
    MemoryMRUCacheMode_MetaDataOnly,
    MemoryMRUCacheMode_BinaryDataOnly,
    MemoryMRUCacheMode_MetaDataAndBinaryData
};


/// an MRU cache for SpectrumPtrs or ChromatogramPtrs
template <typename PtrType, typename KeyExtractor = boost::multi_index::identity<PtrType> >
class MemoryMRUCache : public pwiz::util::mru_list<PtrType, KeyExtractor>
{
    public:
    MemoryMRUCache(MemoryMRUCacheMode mode, size_t size)
    : pwiz::util::mru_list<PtrType, KeyExtractor>(size), mode_(mode)
    {}

    /// set the caching mode
    /// note: if the new mode is different than the current mode, the cache will be cleared
    void setMode(MemoryMRUCacheMode mode)
    {
        if (mode != mode_)
            pwiz::util::mru_list<PtrType, KeyExtractor>::clear();
        mode_ = mode;
    }

    /// get the current caching mode
    MemoryMRUCacheMode mode() const {return mode_;}

    private:
    MemoryMRUCacheMode mode_;
};


} // namespace msdata
} // namespace pwiz


#endif // _MEMORYMRUCACHE_HPP_
