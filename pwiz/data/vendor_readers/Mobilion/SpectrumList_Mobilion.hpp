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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "Reader_Mobilion_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace pwiz::util;

//
// SpectrumList_Mobilion
//
class PWIZ_API_DECL SpectrumList_Mobilion : public SpectrumListIonMobilityBase
{
    public:
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    virtual bool hasIonMobility() const;
    virtual bool hasCombinedIonMobility() const;
    virtual bool canConvertIonMobilityAndCCS() const;
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

#ifdef PWIZ_READER_MOBILION
    SpectrumList_Mobilion(MSData& msd, const MBIFilePtr& rawdata, const Reader::Config& config);

    private:

    MSData& msd_;
    MBIFilePtr rawdata_;
    size_t size_;
    Reader::Config config_;

    int lastFrame_; // last frame accessed; when this changes in non-combined mode, unload the previous frame

    struct IndexEntry : public SpectrumIdentity
    {
        int frame;
        int scan;
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idToIndexMap_;

    void getCombinedSpectrumData(MBI::Frame& frame, BinaryData<double>& mz, BinaryData<double>& intensity, BinaryData<double>& driftTime) const;

    mutable boost::mutex readMutex;

    void createIndex();
#endif // PWIZ_READER_MOBILION
};

} // detail
} // msdata
} // pwiz
