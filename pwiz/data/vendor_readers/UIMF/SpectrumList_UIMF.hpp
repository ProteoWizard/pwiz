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


#ifndef _SPECTRUMLIST_UIMF_
#define _SPECTRUMLIST_UIMF_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/container/flat_map.hpp>
#include "pwiz/analysis/spectrum_processing/SpectrumList_3D.hpp"


#ifdef PWIZ_READER_UIMF
#include "pwiz_aux/msrc/utility/vendor_api/UIMF/UIMFReader.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/thread.hpp>
using namespace pwiz::vendor_api::UIMF;
#endif // PWIZ_READER_UIMF


namespace pwiz {
namespace msdata {
namespace detail {

using boost::shared_ptr;

class PWIZ_API_DECL SpectrumList_UIMF : public SpectrumListIonMobilityBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    virtual pwiz::analysis::Spectrum3DPtr spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const;

    virtual bool hasIonMobility() const;
    virtual bool canConvertIonMobilityAndCCS() const;
    virtual double ionMobilityToCCS(double driftTime, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

#ifdef PWIZ_READER_UIMF
    SpectrumList_UIMF(const MSData& msd, UIMFReaderPtr rawfile, const Reader::Config& config);

    private:

    const MSData& msd_;
    UIMFReaderPtr rawfile_;
    Reader::Config config_;
    mutable size_t size_;
    mutable boost::mutex readMutex;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idToIndexMap_;
    mutable boost::container::flat_map<double, size_t> scanTimeToFrameMap_;

    void createIndex() const;
#endif // PWIZ_READER_UIMF
};


} // detail
} // msdata
} // pwiz

#endif // _SPECTRUMLIST_UIMF_
