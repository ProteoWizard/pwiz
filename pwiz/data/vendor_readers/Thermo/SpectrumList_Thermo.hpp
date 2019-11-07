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


#ifndef _SPECTRUMLIST_THERMO_
#define _SPECTRUMLIST_THERMO_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/container/flat_map.hpp>


#ifdef PWIZ_READER_THERMO
#include "pwiz_aux/msrc/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/Once.hpp"
#include "pwiz/data/msdata/MemoryMRUCache.hpp"
#include <boost/thread.hpp>
using namespace pwiz::vendor_api::Thermo;
#endif // PWIZ_READER_THERMO


namespace pwiz {
namespace msdata {
namespace detail {


class PWIZ_API_DECL SpectrumList_Thermo : public SpectrumListIonMobilityBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual bool hasIonMobility() const;
    virtual bool canConvertIonMobilityAndCCS() const;
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

#ifdef PWIZ_READER_THERMO
    SpectrumList_Thermo(const MSData& msd, pwiz::vendor_api::Thermo::RawFilePtr rawfile, const Reader::Config& config);

    int numSpectraOfScanType(pwiz::vendor_api::Thermo::ScanType scanType) const;
    int numSpectraOfMSOrder(pwiz::vendor_api::Thermo::MSOrder msOrder) const;

    private:

    const MSData& msd_;
    pwiz::vendor_api::Thermo::RawFilePtr rawfile_;
    const Reader::Config config_;
    size_t size_;
    vector<int> spectraByScanType;
    vector<int> spectraByMSOrder;
    mutable boost::mutex readMutex;
    map<long, vector<double> > fillIndex;

    struct IndexEntry : public SpectrumIdentity
    {
        ControllerType controllerType;
        long controllerNumber;
        long scan;

        pwiz::vendor_api::Thermo::ScanType scanType;
        pwiz::vendor_api::Thermo::MSOrder msOrder;
        double isolationMz;
    };

    vector<IndexEntry> index_;
    map<string, size_t> idToIndexMap_;
    int maxMsLevel_; // determines which ms levels to keep in precursorCache

    void createIndex();

    /// a cache mapping spectrum indices to copies of the binary data (we have to keep copies because downstream filters could modify the data)
    typedef pair<std::vector<double>, std::vector<double>> PrecursorBinaryData;
    struct CacheEntry
    {
        CacheEntry(size_t i, const PrecursorBinaryData& b) : index(i), binaryData(b) {};
        size_t index;
        PrecursorBinaryData binaryData;
    };
    typedef MemoryMRUCache<CacheEntry, BOOST_MULTI_INDEX_MEMBER(CacheEntry, size_t, index) > CacheType;
    mutable CacheType precursorCache_;

    size_t findPrecursorSpectrumIndex(RawFile* raw, int precursorMsLevel, double precursorIsolationMz, size_t index) const;
    double getPrecursorIntensity(int precursorSpectrumIndex, double isolationMz, double isolationHalfWidth, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    pwiz::vendor_api::Thermo::ScanInfoPtr findPrecursorZoomScan(RawFile* raw, int precursorMsLevel, double precursorIsolationMz, size_t index) const;

#endif // PWIZ_READER_THERMO
};


} // detail
} // msdata
} // pwiz

#endif // _SPECTRUMLIST_THERMO_
