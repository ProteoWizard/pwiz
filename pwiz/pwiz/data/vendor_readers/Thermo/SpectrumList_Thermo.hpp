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

#ifdef PWIZ_READER_THERMO
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include <boost/thread/once.hpp>
using namespace pwiz::vendor_api::Thermo;
#endif // PWIZ_READER_THERMO


namespace pwiz {
namespace msdata {
namespace detail {

using namespace std;

class PWIZ_API_DECL SpectrumList_Thermo : public SpectrumListBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;

#ifdef PWIZ_READER_THERMO
    SpectrumList_Thermo(const MSData& msd, RawFilePtr rawfile);

    /// an array of size ScanType_Count to count the occurrence of each type
    vector<int> spectraByScanType;
    vector<int> spectraByMSOrder;

    private:

    const MSData& msd_;
    RawFilePtr rawfile_;
    size_t size_;

    mutable vector<int> scanMsLevelCache_;
    mutable boost::once_flag indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
        ControllerType controllerType;
        long controllerNumber;
        long scan;
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idToIndexMap_;

    void createIndex() const;
    size_t findPrecursorSpectrumIndex(int precursorMsLevel, size_t index) const;
#endif // PWIZ_READER_THERMO
};


} // detail
} // msdata
} // pwiz

#endif // _SPECTRUMLIST_THERMO_
