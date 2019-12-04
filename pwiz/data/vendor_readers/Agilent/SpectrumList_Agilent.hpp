//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_AGILENT_
#define _SPECTRUMLIST_AGILENT_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_3D.hpp"


#ifdef PWIZ_READER_AGILENT
#include "pwiz_aux/msrc/utility/vendor_api/Agilent/MassHunterData.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/thread.hpp>
using namespace pwiz::vendor_api::Agilent;
#endif // PWIZ_READER_AGILENT


namespace pwiz {
namespace msdata {
namespace detail {

using boost::shared_ptr;

class PWIZ_API_DECL SpectrumList_Agilent : public SpectrumListIonMobilityBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;

    virtual pwiz::analysis::Spectrum3DPtr spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const;

    virtual bool hasIonMobility() const;
    virtual bool canConvertIonMobilityAndCCS() const;
    virtual double ionMobilityToCCS(double driftTime, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;
    virtual bool hasCombinedIonMobility() const;

#ifdef PWIZ_READER_AGILENT
    SpectrumList_Agilent(const MSData& msd, MassHunterDataPtr rawfile, const Reader::Config& config);

    private:

    const MSData& msd_;
    MassHunterDataPtr rawfile_;
    Reader::Config config_;
    mutable size_t size_;
    mutable boost::mutex readMutex;
    mutable int lastFrameIndex_;
    mutable pwiz::vendor_api::Agilent::FramePtr lastFrame_;
    mutable int lastRowNumber_;
    mutable ScanRecordPtr lastScanRecord_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
        int rowNumber; // continguous 0-based index (not equal to SpectrumIdentity::index since some scan types are skipped)
        int scanId; // unique but not contiguous
        int frameIndex; // 0-based in pwiz but 1-based in MIDAC
        int driftBinIndex; // 0-based
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idToIndexMap_;
    mutable boost::container::flat_map<double, size_t> scanTimeToFrameMap_;

    void createIndex() const;
#endif // PWIZ_READER_AGILENT
};


} // detail
} // msdata
} // pwiz

#endif // _SPECTRUMLIST_AGILENT_
