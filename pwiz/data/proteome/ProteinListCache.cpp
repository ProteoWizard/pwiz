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


#define PWIZ_SOURCE

#include "ProteinListCache.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace proteome {
    

PWIZ_API_DECL ProteinListCache::ProteinListCache(const ProteinListPtr& inner,
                                                 ProteinListCacheMode cacheMode,
                                                 size_t cacheSize)
: ProteinListWrapper(inner), mode_(cacheMode), cache_(cacheSize)
{
}


namespace {


void clearProteinMetadata(Protein& protein)
{
    Protein fresh(protein.id, protein.index, "", "");
    swap(protein, fresh);
}


struct modifyCachedProteinPtr
{
    modifyCachedProteinPtr(const ProteinPtr& newProteinPtr)
        : newProteinPtr_(newProteinPtr) {}

    void operator() (ProteinListCache::CacheType::value_type& indexProteinPtrPair)
    {
        indexProteinPtrPair.second = newProteinPtr_;
    }

    private:
    ProteinPtr newProteinPtr_;
};


} // namespace


// There are two kinds of protein requests: metadata and metadata+sequence;
// the cache's behavior changes depending on the cache mode and the request.
//
// For metadata requests:
// - If cache off: return protein directly
// - If cache metadata: if protein not cached, cache it; return cached protein
// - If cache sequence: return protein directly
// - If cache all: return protein directly
//
// For metadata+sequence requests:
// - If cache off: return protein directly
// - If cache metadata: get protein, make a copy, remove sequence, then cache the copy and return original
// - If cache sequence: if protein cached, get protein without sequence, add cached sequence to it and return it; otherwise get full protein, make a copy, remove metadata, cache it, then return original protein
// - If cache all: if protein not cached, cache it; return cached protein

PWIZ_API_DECL ProteinPtr ProteinListCache::protein(size_t index, bool getSequence) const
{
    ProteinPtr original, copy;
    if (getSequence)
    {
        switch (mode_)
        {
            default:
            case ProteinListCacheMode_Off:
                return inner_->protein(index, true);

            case ProteinListCacheMode_MetaDataAndSequence:
                // if insert returns true, protein was not in cache
                if (cache_.insert(make_pair(index, ProteinPtr())))
                    cache_.modify(cache_.begin(), modifyCachedProteinPtr(inner_->protein(index, true)));
                return cache_.mru().second;

            case ProteinListCacheMode_MetaDataOnly:
                original = inner_->protein(index, true);

                // if insert returns true, protein was not in cache
                if (cache_.insert(make_pair(index, ProteinPtr())))
                {
                    copy.reset(new Protein(original->id, original->index, original->description, ""));
                    cache_.modify(cache_.begin(), modifyCachedProteinPtr(copy));
                }
                return original;
        }
    }
    else // !getSequence
    {
        switch (mode_)
        {
            default:
            case ProteinListCacheMode_Off:
            case ProteinListCacheMode_MetaDataAndSequence:
                return inner_->protein(index, false);

            case ProteinListCacheMode_MetaDataOnly:
                // if insert returns true, protein was not in cache
                if (cache_.insert(make_pair(index, ProteinPtr())))
                    cache_.modify(cache_.begin(), modifyCachedProteinPtr(inner_->protein(index, false)));
                return cache_.mru().second;
        }
    }
}

PWIZ_API_DECL size_t ProteinListCache::find(const std::string& id) const
{
    return inner_->find(id);
}

PWIZ_API_DECL void ProteinListCache::setMode(ProteinListCacheMode mode)
{
    if (mode != mode_)
        cache_.clear();
    mode_ = mode;
}

PWIZ_API_DECL ProteinListCacheMode ProteinListCache::mode() const {return mode_;}


} // namespace proteome
} // namespace pwiz
