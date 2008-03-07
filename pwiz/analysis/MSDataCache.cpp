//
// MSDataCache.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "MSDataCache.hpp"
#include "msdata/MSDataFile.hpp"
#include <stdexcept>
#include <list>
#include <iostream>


namespace pwiz {
namespace analysis {


using boost::shared_ptr;
using boost::lexical_cast;
using namespace std;


//
// MSDataCache::Impl
//


struct MSDataCache::Impl
{
    Impl(const Config& _config) : config(_config) {}

    MSDataCache::Config config;

    typedef list<SpectrumInfo*> MRU; // most recently used
    MRU mru;
};


//
// MSDataCache
//


MSDataCache::MSDataCache(const MSDataCache::Config& config)
:   impl_(new Impl(config)) 
{}


void MSDataCache::open(const DataInfo& dataInfo)
{
    clear();

    if (dataInfo.msd.run.spectrumListPtr.get())
        resize(dataInfo.msd.run.spectrumListPtr->size());
}


void MSDataCache::update(const DataInfo& dataInfo,
                         const Spectrum& spectrum)
{
    if (!dataInfo.msd.run.spectrumListPtr.get() ||
        size()!=dataInfo.msd.run.spectrumListPtr->size())
        throw runtime_error("[MSDataCache::update()] Usage error."); 

    SpectrumInfo& info = at(spectrum.index);
    info.update(spectrum);

    // MRU binary data caching
    if (impl_->config.binaryDataCacheSize>0 && !info.data.empty())
    {
        // find and erase if we're already on the list
        Impl::MRU::iterator it = find(impl_->mru.begin(), impl_->mru.end(), &info);
        if (it!=impl_->mru.end()) 
            impl_->mru.erase(it);

        // put us at the front of the list
        impl_->mru.push_front(&info);

        // free binary data from the least recently used SpectrumInfo (back of list)
        if (impl_->mru.size() > impl_->config.binaryDataCacheSize)
        {
            SpectrumInfo* lru = impl_->mru.back();
            lru->data.clear();
            impl_->mru.pop_back();
        }
    }
}


} // namespace analysis 
} // namespace pwiz

