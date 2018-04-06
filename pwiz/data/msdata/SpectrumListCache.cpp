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

#include "SpectrumListCache.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {
    

PWIZ_API_DECL SpectrumListCache::SpectrumListCache(const SpectrumListPtr& inner,
                                                   MemoryMRUCacheMode cacheMode,
                                                   size_t cacheSize)
: SpectrumListWrapper(inner), spectrumCache_(cacheMode, cacheSize)
{
}


namespace {


void clearSpectrumMetadata(Spectrum& spectrum)
{
    Spectrum fresh;
    fresh.id = spectrum.id;
    fresh.index = spectrum.index;
    fresh.spotID = spectrum.spotID;
    fresh.defaultArrayLength = spectrum.defaultArrayLength;
    swap(spectrum, fresh);
    swap(spectrum.binaryDataArrayPtrs, fresh.binaryDataArrayPtrs);
}


struct modifyCachedSpectrumPtr
{
    modifyCachedSpectrumPtr(const SpectrumPtr& newSpectrumPtr)
        : newSpectrumPtr_(newSpectrumPtr) {}

    void operator() (SpectrumListCache::CacheType::value_type& indexSpectrumPtrPair)
    {
        indexSpectrumPtrPair.spectrum = newSpectrumPtr_;
    }

    private:
    SpectrumPtr newSpectrumPtr_;
};


} // namespace


// There are two kinds of spectrum requests: metadata and metadata+binary;
// the cache's behavior changes depending on the cache mode and the request.
//
// For metadata requests:
// - If cache off: return spectrum directly
// - If cache metadata: if spectrum not cached, cache it; return cached spectrum
// - If cache binary data: return spectrum directly
// - If cache all: return spectrum directly
//
// For metadata+binary requests:
// - If cache off: return spectrum directly
// - If cache metadata: get spectrum, make a copy, remove binary data, then cache the copy and return original
// - If cache binary data: if spectrum cached, get spectrum without binary data, add cached binary data to it and return it; otherwise get full spectrum, make a copy, remove metadata, cache it, then return original spectrum
// - If cache all: if spectrum not cached, cache it; return cached spectrum

PWIZ_API_DECL SpectrumPtr SpectrumListCache::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr original, copy;
    if (getBinaryData)
    {
        switch (spectrumCache_.mode())
        {
            default:
            case MemoryMRUCacheMode_Off:
                return inner_->spectrum(index, true);

            case MemoryMRUCacheMode_MetaDataAndBinaryData:
                // if insert returns true, spectrum was not in cache
                if (spectrumCache_.insert(CacheEntry(index, SpectrumPtr())))
                    spectrumCache_.modify(spectrumCache_.begin(), modifyCachedSpectrumPtr(inner_->spectrum(index, true)));
                return spectrumCache_.mru().spectrum;

            case MemoryMRUCacheMode_MetaDataOnly:

                // if insert returns true, spectrum was not in cache
                if (spectrumCache_.insert(CacheEntry(index, SpectrumPtr())))
                {
                    original = inner_->spectrum(index, true);
                    copy.reset(new Spectrum(*original));
                    copy->binaryDataArrayPtrs.clear();
                    spectrumCache_.modify(spectrumCache_.begin(), modifyCachedSpectrumPtr(copy));
                }
                else {
                    // we have cached metadata, hopefully this format knows how 
                    // to jump to binary data without rescanning metadata
                    original = inner_->spectrum(spectrumCache_.mru().spectrum, true); // copy and add binary data
                }
                return original;

            case MemoryMRUCacheMode_BinaryDataOnly:
                // if insert returns true, spectrum was not in cache
                if (spectrumCache_.insert(CacheEntry(index, SpectrumPtr())))
                {
                    original = inner_->spectrum(index, true);
                    copy.reset(new Spectrum(*original));
                    clearSpectrumMetadata(*copy);
                    spectrumCache_.modify(spectrumCache_.begin(), modifyCachedSpectrumPtr(copy));
                    return original;
                }
                else
                {
                    // get spectrum metadata, add cached binary data to it
                    original = inner_->spectrum(index, false);
                    original->binaryDataArrayPtrs = spectrumCache_.mru().spectrum->binaryDataArrayPtrs;
                    return original;
                }
        }
    }
    else // !getBinaryData
    {
        switch (spectrumCache_.mode())
        {
            default:
            case MemoryMRUCacheMode_Off:
            case MemoryMRUCacheMode_BinaryDataOnly:
            case MemoryMRUCacheMode_MetaDataAndBinaryData:
                return inner_->spectrum(index, false);

            case MemoryMRUCacheMode_MetaDataOnly:
                // if insert returns true, spectrum was not in cache
                if (spectrumCache_.insert(CacheEntry(index, SpectrumPtr())))
                    spectrumCache_.modify(spectrumCache_.begin(), modifyCachedSpectrumPtr(inner_->spectrum(index, false)));
                return spectrumCache_.mru().spectrum;
        }
    }
}

PWIZ_API_DECL SpectrumListCache::CacheType& SpectrumListCache::spectrumCache()
{
    return spectrumCache_;
}

PWIZ_API_DECL const SpectrumListCache::CacheType& SpectrumListCache::spectrumCache() const
{
    return spectrumCache_;
}


} // namespace msdata
} // namespace pwiz
