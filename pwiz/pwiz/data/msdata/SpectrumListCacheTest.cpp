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


#include "pwiz/utility/misc/unit.hpp"
#include "MSDataFile.hpp"
#include "MemoryMRUCache.hpp"
#include "SpectrumListCache.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Serializer_MGF.hpp"


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
        os << " " << itr->spectrum->index;
    return os;
}

} // namespace std


void testMemoryMRUCache()
{
    SpectrumListCache::CacheType cache(MemoryMRUCacheMode_Off, 2);

    unit_assert_operator_equal(2, cache.max_size());
    unit_assert(cache.empty());
    unit_assert_operator_equal(0, cache.size());

    cache.insert(SpectrumListCache::CacheEntry(0, SpectrumPtr()));

    unit_assert(!cache.empty());
    unit_assert_operator_equal(1, cache.size());

    cache.insert(SpectrumListCache::CacheEntry(1, SpectrumPtr()));

    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(1, cache.mru().index);
    unit_assert_operator_equal(0, cache.lru().index);

    cache.insert(SpectrumListCache::CacheEntry(0, SpectrumPtr()));

    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(0, cache.mru().index);
    unit_assert_operator_equal(1, cache.lru().index);

    cache.insert(SpectrumListCache::CacheEntry(2, SpectrumPtr()));

    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().index);
    unit_assert_operator_equal(0, cache.lru().index);
}


SpectrumPtr makeSpectrumPtr(size_t index, const string& id)
{
    SpectrumPtr spectrum(new Spectrum);
    spectrum->id = id;
    spectrum->index = index;
    spectrum->set(MS_MSn_spectrum);
    spectrum->set(MS_ms_level, 2);
    spectrum->precursors.push_back(Precursor(123.4));
    spectrum->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
    BinaryDataArray& mzArray = *spectrum->getMZArray();
    BinaryDataArray& intensityArray = *spectrum->getIntensityArray();
    for (size_t i=0; i < (index+1)*10; ++i)
    {
        mzArray.data.push_back(i);
        intensityArray.data.push_back(i*100);
    }
    spectrum->defaultArrayLength = mzArray.data.size();
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
    return s.hasBinaryData();
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
    MSData msd;
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));
    msd.run.spectrumListPtr = sl;

    // SpectrumListSimple returns the same shared_ptrs regardless of caching;
    // serializing to MGF and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_MGF serializer;
    serializer.write(*ss, msd, 0);
    serializer.read(ss, msd);

    // access a series of spectra and make sure the cache behaves appropriately:
    // in metadata-only mode, entries in the cache should:
    // - always have metadata
    // - never have binary data

    SpectrumPtr s;

    SpectrumListCache slc(msd.run.spectrumListPtr, MemoryMRUCacheMode_MetaDataOnly, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert_operator_equal(2, cache.max_size());

    s = slc.spectrum(0, false);

    // pointers should be equal
    unit_assert_operator_equal(slc.spectrum(0, false), s);

    if (os_) *os_ << cache << endl;
    unit_assert(!cache.empty());
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(0, cache.mru().spectrum->index);

    // with-binary-data access should return the binary data, but only cache the metadata
    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(!spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert(spectrumHasMetadata(*cache.lru().spectrum));
    unit_assert_operator_equal(0, cache.lru().spectrum->index);

    s = slc.spectrum(2, false);

    // pointers should be equal
    unit_assert_operator_equal(slc.spectrum(2, false), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(!spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(1, cache.lru().spectrum->index);

    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(3, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(!spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(2, cache.lru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.lru().spectrum));

    s = slc.spectrum(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(!spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(3, cache.lru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.lru().spectrum));
}


void testModeBinaryDataOnly()
{
    // initialize list
    MSData msd;
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));
    msd.run.spectrumListPtr = sl;

    // SpectrumListSimple returns the same shared_ptrs regardless of caching;
    // serializing to MGF and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_MGF serializer;
    serializer.write(*ss, msd, 0);
    serializer.read(ss, msd);

    // access a series of spectra and make sure the cache behaves appropriately:
    // in binary-data-only mode, entries in the cache should:
    // - never have metadata
    // - always have binary data

    SpectrumPtr s;

    SpectrumListCache slc(msd.run.spectrumListPtr, MemoryMRUCacheMode_BinaryDataOnly, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert_operator_equal(2, cache.max_size());

    // metadata-only access should not affect the cache
    s = slc.spectrum(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert_operator_equal(0, cache.size());

    // with-binary-data access should be cached without the metadata
    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));

    s = slc.spectrum(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));

    s = slc.spectrum(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(3, cache.mru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(1, cache.lru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.lru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.lru().spectrum));

    s = slc.spectrum(1, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(3, cache.lru().spectrum->index);
    unit_assert(!spectrumHasMetadata(*cache.lru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.lru().spectrum));
}


void testModeMetaDataAndBinaryData()
{
    // initialize list
    MSData msd;
    shared_ptr<SpectrumListSimple> sl(new SpectrumListSimple);
    sl->spectra.push_back(makeSpectrumPtr(0, "S1"));
    sl->spectra.push_back(makeSpectrumPtr(1, "S2"));
    sl->spectra.push_back(makeSpectrumPtr(2, "S3"));
    sl->spectra.push_back(makeSpectrumPtr(3, "S4"));
    msd.run.spectrumListPtr = sl;

    // SpectrumListSimple returns the same shared_ptrs regardless of caching;
    // serializing to MGF and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_MGF serializer;
    serializer.write(*ss, msd, 0);
    serializer.read(ss, msd);

    // access a series of spectra and make sure the cache behaves appropriately:
    // in metadata-and-binary-data mode, entries in the cache should:
    // - always have metadata
    // - always have binary data

    SpectrumPtr s;

    SpectrumListCache slc(msd.run.spectrumListPtr, MemoryMRUCacheMode_MetaDataAndBinaryData, 2);
    SpectrumListCache::CacheType& cache = slc.spectrumCache();

    unit_assert(cache.empty());
    unit_assert_operator_equal(2, cache.max_size());

    // metadata-only access should not affect the cache
    s = slc.spectrum(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert_operator_equal(0, cache.size());

    s = slc.spectrum(1, true);

    // pointers should be equal
    unit_assert_operator_equal(slc.spectrum(1, true), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));

    s = slc.spectrum(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));

    s = slc.spectrum(3, true);

    // pointers should be equal
    unit_assert_operator_equal(slc.spectrum(3, true), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(3, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(1, cache.lru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.lru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.lru().spectrum));

    s = slc.spectrum(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.mru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.mru().spectrum));
    unit_assert_operator_equal(3, cache.lru().spectrum->index);
    unit_assert(spectrumHasMetadata(*cache.lru().spectrum));
    unit_assert(spectrumHasBinaryData(*cache.lru().spectrum));
}

void testFileReads(const char *filename) {
    std::string srcparent(__FILE__); // locate test data relative to this source file
    // something like \ProteoWizard\pwiz\pwiz\data\msdata\SpectrumListCacheTest.cpp
    size_t pos = srcparent.rfind("pwiz");
    srcparent.resize(pos);
    std::string example_data_dir = srcparent + "example_data/";
    pwiz::msdata::MSDataFile msd1(example_data_dir + filename);
    SpectrumListCache cache(msd1.run.spectrumListPtr, MemoryMRUCacheMode_MetaDataOnly, 2);
    pwiz::msdata::MSDataFile msd2(example_data_dir + filename);
    // test logic for efficient delayed read of binary data - 
    // we try to avoid reparsing the header since we have that cached
    // mzML and mzXML readers can do this, others could probably be made to
    int index = 3;
    SpectrumPtr s=msd2.run.spectrumListPtr->spectrum(index, false);
    SpectrumPtr c=cache.spectrum(index, false);
    unit_assert(*s==*c);
    unit_assert(!s->hasBinaryData());
    unit_assert(!c->hasBinaryData());
    s=msd2.run.spectrumListPtr->spectrum(index, true);
    c=cache.spectrum(index, true);
    unit_assert(*s==*c);
    unit_assert(s->hasBinaryData());
    unit_assert(c->hasBinaryData());
    unit_assert(s->binaryDataArrayPtrs[0]->data[0]==
                c->binaryDataArrayPtrs[0]->data[0]);
    unit_assert(!s->binaryDataArrayPtrs[1]->data.empty());
    unit_assert(!c->binaryDataArrayPtrs[1]->data.empty());
    unit_assert(s->binaryDataArrayPtrs[1]->data[0]==
                c->binaryDataArrayPtrs[1]->data[0]);
}


void test()
{
    testMemoryMRUCache();
    testModeOff();
    testModeMetaDataOnly();
    testModeBinaryDataOnly();
    testModeMetaDataAndBinaryData();
    // check the delayed-binary-read
    // logic for mzML and mzXML readers
    testFileReads("tiny.pwiz.mzXML");
    testFileReads("tiny.pwiz.1.0.mzML");
    testFileReads("tiny.pwiz.1.1.mzML");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
