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


#ifndef _CHROMATOGRAMLIST_THERMO_
#define _CHROMATOGRAMLIST_THERMO_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"


#ifdef PWIZ_READER_THERMO
#include "pwiz_aux/msrc/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>
using namespace pwiz::vendor_api::Thermo;
#endif // PWIZ_READER_THERMO


using boost::shared_ptr;


namespace pwiz {
namespace msdata {
namespace detail {

class PWIZ_API_DECL ChromatogramList_Thermo : public ChromatogramListBase
{
public:

    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    virtual ChromatogramPtr chromatogram(size_t index, DetailLevel detailLevel) const;
    
#ifdef PWIZ_READER_THERMO
    ChromatogramList_Thermo(const MSData& msd, RawFilePtr rawfile, const Reader::Config& config);

    ChromatogramPtr xic(double startTime, double endTime, const boost::icl::interval_set<double>& massRanges, int msLevel);

    private:

    const MSData& msd_;
    shared_ptr<RawFile> rawfile_;
    const Reader::Config config_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        ControllerType controllerType;
        long controllerNumber;
        string filter;
        double q1, q3;
        double q3Offset;
        CVID polarityType;
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idMap_;

    void createIndex() const;
    IndexEntry& addChromatogram(const string& id, ControllerType controllerType, int controllerNumber, CVID chromatogramType, const string& filter) const;
#endif // PWIZ_READER_THERMO
};

} // detail
} // msdata
} // pwiz

#endif // _CHROMATOGRAMLIST_THERMO_
