//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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


#include "MemoryMRUCache.hpp"
#include "SpectrumListCache.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
//using namespace pwiz::analysis;


ostream* os_ = 0;


namespace std {

ostream& operator<< (ostream& os, SpectrumListCache::CacheType& cache)
{
    os << "Spectrum cache indices (from MRU to LRU):";
    for (SpectrumListCache::CacheType::iterator itr = cache.begin(); itr != cache.end(); ++itr)
        os << " " << itr->second->index;
    return os;
}

} // namespace std


void testMemoryMRUCache()
{
    MemoryMRUCache<pair<size_t, SpectrumPtr> > cache(MemoryMRUCacheMode_Off, 2);

    unit_assert(cache.max_size() == 2);
    unit_assert(cache.empty());
    unit_assert(cache.size() == 0);

    cache.insert(make_pair(0, SpectrumPtr()));

    unit_assert(!cache.empty());
    unit_assert(cache.size() == 1);

    cache.insert(make_pair(1, SpectrumPtr()));

    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().first == 1);
    unit_assert(cache.lru().first == 0);

    cache.insert(make_pair(0, SpectrumPtr()));

    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().first == 0);
    unit_assert(cache.lru().first == 1);

    cache.insert(make_pair(2, SpectrumPtr()));

    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().first == 2);
    unit_assert(cache.lru().first == 0);
}


SpectrumPtr makeSpectrumPtr(size_t index, const string& id)
{
    SpectrumPtr spectrum(new Spectrum);
    spectrum->id = id;
    spectrum->index = index;
    spectrum->set(MS_MSn_spectrum);
    spectrum->set(MS_ms_level, index+1);
    BinaryDataArrayPtr bda(new BinaryDataArray);
    for (size_t i=0; i < (index+1)*10; ++i)
        bda->data.push_back(i);
    spectrum->binaryDataArrayPtrs.push_back(bda);
    return spectrum;
}

bool spectrumHasMetadata(const Spectrum& s)
{
    return s.dataProcessingPtr.get() ||
           s.sourceFilePtr.get() ||
           !s.scanList.empty() ||
           !s.precursors.empty() ||
           !s.paramGroupPtrs.empty() ||
           !s.cvParams.empty() ||
           !s.userParams.empty();
}

bool spectrumHasBinaryData(const Spectrum& s)
{
    return !s.binaryDataArrayPtrs.empty();
}

void testModeOff()
{
    // initialize list
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));

    // access a series of spectra and make sure the cache behaves appropriately:
    // in off mode, the cache should always be empty

    SpectrumPtr s;

    SpectrumListCache slc(sl, MemoryMRUCacheMode_Off, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());

    s = slc.spectrum(0, false);
    s = slc.spectrum(1, true);
    s = slc.spectrum(2, false);
    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
}


void testModeMetaDataOnly()
{
    // initialize list
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));

    // access a series of spectra and make sure the cache behaves appropriately:
    // in metadata-only mode, entries in the cache should:
    // - always have metadata
    // - never have binary data

    SpectrumPtr s;

    SpectrumListCache slc(sl, MemoryMRUCacheMode_MetaDataOnly, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert(cache.max_size() == 2);

    s = slc.spectrum(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(!cache.empty());
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 0);

    // with-binary-data access should return the binary data, but only cache the metadata
    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(!spectrumHasBinaryData(*cache.mru().second));
    unit_assert(spectrumHasMetadata(*cache.lru().second));
    unit_assert(cache.lru().second->index == 0);

    s = slc.spectrum(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(cache.lru().second->index == 1);

    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 3);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(!spectrumHasBinaryData(*cache.mru().second));
    unit_assert(spectrumHasMetadata(*cache.lru().second));
    unit_assert(cache.lru().second->index == 2);

    s = slc.spectrum(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(!spectrumHasBinaryData(*cache.mru().second));
    unit_assert(cache.lru().second->index == 3);
    unit_assert(spectrumHasMetadata(*cache.lru().second));
}


void testModeBinaryDataOnly()
{
    // initialize list
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));

    // access a series of spectra and make sure the cache behaves appropriately:
    // in binary-data-only mode, entries in the cache should:
    // - never have metadata
    // - always have binary data

    SpectrumPtr s;

    SpectrumListCache slc(sl, MemoryMRUCacheMode_BinaryDataOnly, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert(cache.max_size() == 2);

    // metadata-only access should not affect the cache
    s = slc.spectrum(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert(cache.size() == 0);

    // with-binary-data access should be cached without the metadata
    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(!spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));

    s = slc.spectrum(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(!spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));

    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 3);
    unit_assert(!spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));
    unit_assert(cache.lru().second->index == 1);
    unit_assert(!spectrumHasMetadata(*cache.lru().second));
    unit_assert(spectrumHasBinaryData(*cache.lru().second));

    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(!spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));
    unit_assert(cache.lru().second->index == 3);
    unit_assert(!spectrumHasMetadata(*cache.lru().second));
    unit_assert(spectrumHasBinaryData(*cache.lru().second));
}


void testModeMetaDataAndBinaryData()
{
    // initialize list
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));

    // access a series of spectra and make sure the cache behaves appropriately:
    // in metadata-and-binary-data mode, entries in the cache should:
    // - always have metadata
    // - always have binary data

    SpectrumPtr s;

    SpectrumListCache slc(sl, MemoryMRUCacheMode_MetaDataAndBinaryData, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert(cache.max_size() == 2);

    // metadata-only access should not affect the cache
    s = slc.spectrum(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert(cache.size() == 0);

    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));

    s = slc.spectrum(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 1);
    unit_assert(cache.mru().second->index == 1);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));

    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 3);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));
    unit_assert(cache.lru().second->index == 1);
    unit_assert(spectrumHasMetadata(*cache.lru().second));
    unit_assert(spectrumHasBinaryData(*cache.lru().second));

    s = slc.spectrum(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.size() == 2);
    unit_assert(cache.mru().second->index == 2);
    unit_assert(spectrumHasMetadata(*cache.mru().second));
    unit_assert(spectrumHasBinaryData(*cache.mru().second));
    unit_assert(cache.lru().second->index == 3);
    unit_assert(spectrumHasMetadata(*cache.lru().second));
    unit_assert(spectrumHasBinaryData(*cache.lru().second));
}


void test()
{
    testMemoryMRUCache();
    testModeOff();
    testModeMetaDataOnly();
    testModeBinaryDataOnly();
    testModeMetaDataAndBinaryData();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
    }

    return 1;
}
