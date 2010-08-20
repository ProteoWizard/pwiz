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


#include "MSDataCache.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;
const double epsilon_ = 1e-6;


void testMetadata(MSDataCache& cache)
{
    if (os_) *os_ << "testMetadata()\n";

    if (os_) *os_ << "spectrumCount: " << cache.size() << endl;
    unit_assert(cache.size() == 4);

    unit_assert(cache[0].index == 0);
    unit_assert(cache[0].id == "scan=19");
    unit_assert(cache[0].scanNumber == 19); // TODO: change to nativeID 
    unit_assert(cache[0].massAnalyzerType == MS_QIT);
    unit_assert(cache[0].msLevel == 1);
    unit_assert_equal(cache[0].retentionTime, 353.43, epsilon_);
    unit_assert_equal(cache[0].mzLow, 400.39, epsilon_);
    unit_assert_equal(cache[0].mzHigh, 1795.56, epsilon_);
    unit_assert(cache[0].precursors.empty());

    unit_assert(cache[1].index == 1);
    unit_assert(cache[1].id == "scan=20");
    unit_assert(cache[1].scanNumber == 20); // TODO:  change to nativeID
    unit_assert(cache[1].massAnalyzerType == MS_QIT);
    unit_assert(cache[1].msLevel == 2);
    unit_assert_equal(cache[1].retentionTime, 359.43, epsilon_);
    unit_assert_equal(cache[1].mzLow, 320.39, epsilon_);
    unit_assert_equal(cache[1].mzHigh, 1003.56, epsilon_);
    unit_assert(cache[1].precursors.size() == 1);
    unit_assert(cache[1].precursors[0].index == 0);
    unit_assert_equal(cache[1].precursors[0].mz, 445.34, epsilon_);
    unit_assert_equal(cache[1].precursors[0].intensity, 120053, epsilon_);
    unit_assert(cache[1].precursors[0].charge == 2);

    if (os_) *os_ << endl;
}


void testDefault()
{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSDataCache cache;

    cache.open(tiny);

    cache.update(tiny, *tiny.run.spectrumListPtr->spectrum(0));
    unit_assert(!cache[0].data.empty());
    unit_assert(cache[1].data.empty());

    cache.update(tiny, *tiny.run.spectrumListPtr->spectrum(1));
    unit_assert(cache[0].data.empty());
    unit_assert(!cache[1].data.empty());

    testMetadata(cache);
}


void printCache(ostream& os, const MSDataCache& cache)
{
    os << "cached binary data:\n";
    for (vector<SpectrumInfo>::const_iterator it=cache.begin(); it!=cache.end(); ++it) 
    {
        os << it->index << " " 
           << it->data.size() << "/"
           << it->data.capacity() << endl;
    }
    os << endl;
}


void testMRU()
{
    if (os_) *os_ << "testMRU()\n";

    vector<MZIntensityPair> pairs(100);

    SpectrumListSimplePtr sl(new SpectrumListSimple);
    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->setMZIntensityPairs(pairs, MS_number_of_counts);
        spectrum->index = i;
        spectrum->id = "scan=" + lexical_cast<string>(i);
        sl->spectra.push_back(spectrum);
    }

    MSData msd;
    msd.run.spectrumListPtr = sl;

    MSDataCache::Config config;
    config.binaryDataCacheSize = 3;
    MSDataCache cache(config);

    cache.open(msd);

    if (os_) *os_ << "update: 0 1 2\n";
    cache.update(msd, *sl->spectrum(0, true));
    cache.update(msd, *sl->spectrum(1, true));
    cache.update(msd, *sl->spectrum(2, true));
    if (os_) printCache(*os_, cache); // mru: 2 1 0

    unit_assert(cache[0].data.size() == 100);
    unit_assert(cache[1].data.size() == 100);
    unit_assert(cache[2].data.size() == 100);
    unit_assert(cache[3].data.size() == 0);

    if (os_) *os_ << "update: 3\n";
    cache.update(msd, *sl->spectrum(3, true));
    if (os_) printCache(*os_, cache); // mru: 3 2 1

    unit_assert(cache[0].data.capacity() == 0);
    unit_assert(cache[1].data.size() == 100);
    unit_assert(cache[2].data.size() == 100);
    unit_assert(cache[3].data.size() == 100);

    if (os_) *os_ << "update: 1\n";
    cache.update(msd, *sl->spectrum(1, true));
    if (os_) printCache(*os_, cache); // mru: 1 3 2

    unit_assert(cache[0].data.capacity() == 0);
    unit_assert(cache[1].data.size() == 100);
    unit_assert(cache[2].data.size() == 100);
    unit_assert(cache[3].data.size() == 100);

    if (os_) *os_ << "update: 4\n";
    cache.update(msd, *sl->spectrum(4, true));
    if (os_) printCache(*os_, cache); // mru: 4 1 3

    unit_assert(cache[0].data.capacity() == 0);
    unit_assert(cache[1].data.size() == 100);
    unit_assert(cache[2].data.capacity() == 0);
    unit_assert(cache[3].data.size() == 100);
    unit_assert(cache[3].data.size() == 100);

    if (os_) *os_ << endl;
}


struct EvenRequester : public MSDataAnalyzer
{
    virtual UpdateRequest updateRequested(const DataInfo& dataInfo, 
                                          const SpectrumIdentity& spectrumIdentity) const
    {
        return (spectrumIdentity.index%2==0) ? UpdateRequest_NoBinary : UpdateRequest_None;
    }
};


void testUpdateRequest()
{
    if (os_) *os_ << "testUpdateRequest()\n";

    vector<MZIntensityPair> pairs(100);

    SpectrumListSimplePtr sl(new SpectrumListSimple);
    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->setMZIntensityPairs(pairs, MS_number_of_counts);
        spectrum->index = i;
        spectrum->id = "scan=" + lexical_cast<string>(i);
        sl->spectra.push_back(spectrum);
    }

    MSData msd;
    msd.run.spectrumListPtr = sl;

    MSDataAnalyzerContainer analyzers;
    shared_ptr<MSDataCache> cache(new MSDataCache);
    analyzers.push_back(cache);
    analyzers.push_back(MSDataAnalyzerPtr(new EvenRequester));

    MSDataAnalyzerDriver driver(analyzers);
    driver.analyze(msd);

    for (size_t i=0, end=cache->size(); i<end; i++)
    {
        const SpectrumInfo& info = cache->at(i);
        if (os_) *os_ << info.index << " " << info.id << endl;

        // cache has only been updated with the spectra requested by EvenRequester

        unit_assert(i%2==0 && info.index==i && info.id=="scan="+lexical_cast<string>(i) ||
                    i%2==1 && info.index==(size_t)-1&& info.id.empty());
    }

    if (os_) *os_ << endl;
}


void testAutomaticUpdate()
{
    if (os_) *os_ << "testAutomaticUpdate()\n";

    vector<MZIntensityPair> pairs(100);

    SpectrumListSimplePtr sl(new SpectrumListSimple);
    for (size_t i=0; i<10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->setMZIntensityPairs(pairs, MS_number_of_counts);
        spectrum->index = i;
        spectrum->id = "scan=" + lexical_cast<string>(i);
        sl->spectra.push_back(spectrum);
    }

    MSData msd;
    msd.run.spectrumListPtr = sl;

    MSDataCache cache;
    cache.open(msd);

    unit_assert(cache.size() == sl->size());
    for (size_t i=0; i<cache.size(); i++)
        unit_assert(cache[i].index == (size_t)-1);

    const SpectrumInfo& info5 = cache.spectrumInfo(5, true);
    unit_assert(cache[5].data.size() == 100);

    cache.spectrumInfo(7); // getBinaryData==false -> doesn't change cached binary data
    unit_assert(cache[5].data.size() == 100);
    unit_assert(cache[7].data.size() == 0);

    const SpectrumInfo& info7 = cache.spectrumInfo(7, true);

    if (os_)
    {
        for (size_t i=0; i<cache.size(); i++)
            *os_ << i << " " << cache[i].index << " " << cache[i].id << " "
                 << cache[i].data.size() << endl;
    }     

    unit_assert(info7.data.size() == 100);
    unit_assert(info5.data.size() == 0);

    unit_assert(info5.index==5 && info5.id=="scan=5");
    unit_assert(cache[5].index==5 && cache[5].id=="scan=5");
    unit_assert(info7.index==7 && info7.id=="scan=7");
    unit_assert(cache[7].index==7 && cache[7].id=="scan=7");

    for (size_t i=0; i<cache.size(); i++)
        if (i!=5 && i!=7)
            unit_assert(cache[i].index == (size_t)-1);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testDefault();
        testMRU();
        testUpdateRequest();
        testAutomaticUpdate();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

