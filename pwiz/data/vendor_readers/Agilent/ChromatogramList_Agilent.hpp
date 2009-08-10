//
// ChromatogramList_Agilent.hpp
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


#ifndef _CHROMATOGRAMLIST_AGILENT_
#define _CHROMATOGRAMLIST_AGILENT_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/utility/vendor_api/Agilent/MassHunterData.hpp"
#include <map>
#include <vector>
#include <boost/thread/once.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace std;
using boost::shared_ptr;
using namespace pwiz::vendor_api::Agilent;

class PWIZ_API_DECL ChromatogramList_Agilent : public ChromatogramListBase
{
public:

    ChromatogramList_Agilent(MassHunterDataPtr reader);
    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    
    private:

    MassHunterDataPtr rawfile_;

    mutable boost::once_flag indexInitialized_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        Transition transition;
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idMap_;

    void createIndex() const;
};

} // detail
} // msdata
} // pwiz

#endif // _CHROMATOGRAMLIST_AGILENT_
