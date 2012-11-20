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


#include "ProteinListCache.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Serializer_FASTA.hpp"


using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


namespace std {

ostream& operator<< (ostream& os, const ProteinListCache::CacheType& cache)
{
    os << "Protein cache indices (from MRU to LRU):";
    for (ProteinListCache::CacheType::const_iterator itr = cache.begin(); itr != cache.end(); ++itr)
        os << " " << itr->second->index;
    return os;
}

} // namespace std


void testModeOff()
{
    // initialize list
    ProteomeData pd;
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));
    pd.proteinListPtr = sl;

    // ProteinListSimple returns the same shared_ptrs regardless of caching;
    // serializing to FASTA and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_FASTA serializer;
    serializer.write(*ss, pd, 0);
    serializer.read(ss, pd);

    // access a series of proteins and make sure the cache behaves appropriately:
    // in off mode, the cache should always be empty

    ProteinPtr s;

    ProteinListCache slc(pd.proteinListPtr, ProteinListCacheMode_Off, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());

    s = slc.protein(0, false);
    s = slc.protein(1, true);
    unit_assert_operator_equal("1", s->description);
    unit_assert_operator_equal("DEF", s->sequence());
    s = slc.protein(2, false);
    s = slc.protein(3, true);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
}


void testModeMetaDataOnly()
{
    // initialize list
    ProteomeData pd;
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));
    pd.proteinListPtr = sl;

    // ProteinListSimple returns the same shared_ptrs regardless of caching;
    // serializing to FASTA and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_FASTA serializer;
    serializer.write(*ss, pd, 0);
    serializer.read(ss, pd);

    // access a series of proteins and make sure the cache behaves appropriately:
    // in metadata-only mode, entries in the cache should:
    // - always have metadata
    // - never have sequences

    ProteinPtr s;

    ProteinListCache slc(pd.proteinListPtr, ProteinListCacheMode_MetaDataOnly, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());
    unit_assert_operator_equal(2, cache.max_size());

    s = slc.protein(0, false);

    // pointers should be equal
    unit_assert_operator_equal(slc.protein(0, false), s);

    if (os_) *os_ << cache << endl;
    unit_assert(!cache.empty());
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(0, cache.mru().second->index);
    unit_assert_operator_equal("0", cache.mru().second->description);
    unit_assert_operator_equal("", cache.mru().second->sequence());

    // with-sequence access should return the sequence, but only cache the metadata
    s = slc.protein(1, true);
    unit_assert_operator_equal("DEF", s->sequence());

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(1, cache.mru().second->index);
    unit_assert_operator_equal("", cache.mru().second->sequence());
    unit_assert_operator_equal(0, cache.lru().second->index);

    s = slc.protein(2, false);

    // pointers should be equal
    unit_assert_operator_equal(slc.protein(2, false), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().second->index);
    unit_assert_operator_equal("", cache.mru().second->sequence());
    unit_assert_operator_equal(1, cache.lru().second->index);

    s = slc.protein(3, true);
    unit_assert_operator_equal("JKL", s->sequence());

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(3, cache.mru().second->index);
    unit_assert_operator_equal("", cache.mru().second->sequence());
    unit_assert_operator_equal(2, cache.lru().second->index);

    s = slc.protein(2, true);
    unit_assert_operator_equal("GHI", s->sequence());

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().second->index);
    unit_assert_operator_equal("", cache.mru().second->sequence());
    unit_assert_operator_equal(3, cache.lru().second->index);
}


void testModeMetaDataAndSequence()
{
    // initialize list
    ProteomeData pd;
    shared_ptr<ProteinListSimple> sl(new ProteinListSimple);
    sl->proteins.push_back(ProteinPtr(new Protein("P1", 0, "0", "ABC")));
    sl->proteins.push_back(ProteinPtr(new Protein("P2", 1, "1", "DEF")));
    sl->proteins.push_back(ProteinPtr(new Protein("P3", 2, "2", "GHI")));
    sl->proteins.push_back(ProteinPtr(new Protein("P4", 3, "3", "JKL")));
    pd.proteinListPtr = sl;

    // ProteinListSimple returns the same shared_ptrs regardless of caching;
    // serializing to FASTA and back will produce different shared_ptrs
    boost::shared_ptr<stringstream> ss(new stringstream);
    Serializer_FASTA serializer;
    serializer.write(*ss, pd, 0);
    serializer.read(ss, pd);

    // access a series of proteins and make sure the cache behaves appropriately:
    // in metadata-and-sequence mode, entries in the cache should:
    // - always have metadata
    // - always have sequences

    ProteinPtr s;

    ProteinListCache slc(pd.proteinListPtr, ProteinListCacheMode_MetaDataAndSequence, 2);
    const ProteinListCache::CacheType& cache = slc.cache();

    unit_assert(cache.empty());
    unit_assert_operator_equal(2, cache.max_size());

    // metadata-only access should not affect the cache
    s = slc.protein(0, false);

    if (os_) *os_ << cache << endl;
    unit_assert(cache.empty());
    unit_assert_operator_equal(0, cache.size());

    s = slc.protein(1, true);

    // pointers should be equal
    unit_assert_operator_equal(slc.protein(1, true), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().second->index);
    unit_assert_operator_equal("1", cache.mru().second->description);
    unit_assert_operator_equal("DEF", cache.mru().second->sequence());

    // metadata-only access should not affect the cache
    s = slc.protein(2, false);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(1, cache.size());
    unit_assert_operator_equal(1, cache.mru().second->index);
    unit_assert_operator_equal("DEF", cache.mru().second->sequence());

    s = slc.protein(3, true);

    // pointers should be equal
    unit_assert_operator_equal(slc.protein(3, true), s);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(3, cache.mru().second->index);
    unit_assert_operator_equal("JKL", cache.mru().second->sequence());
    unit_assert_operator_equal(1, cache.lru().second->index);

    s = slc.protein(2, true);

    if (os_) *os_ << cache << endl;
    unit_assert_operator_equal(2, cache.size());
    unit_assert_operator_equal(2, cache.mru().second->index);
    unit_assert_operator_equal("GHI", cache.mru().second->sequence());
    unit_assert_operator_equal(3, cache.lru().second->index);
}


void test()
{
    testModeOff();
    testModeMetaDataOnly();
    testModeMetaDataAndSequence();
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
