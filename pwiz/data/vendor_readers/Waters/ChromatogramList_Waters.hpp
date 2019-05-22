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
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "Reader_Waters_Detail.hpp"
#include "pwiz/utility/misc/Once.hpp"


namespace pwiz {
namespace msdata {
namespace detail {


class PWIZ_API_DECL ChromatogramList_Waters : public ChromatogramListBase
{
    public:

    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    virtual ChromatogramPtr chromatogram(size_t index, DetailLevel detailLevel) const;
    virtual ChromatogramPtr chromatogram(size_t index, DetailLevel detailLevel, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance) const;

#ifdef PWIZ_READER_WATERS
    ChromatogramList_Waters(RawDataPtr rawdata);

    private:

    RawDataPtr rawdata_;
    mutable size_t size_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        int function;
        int offset;
        float Q1, Q3;
    };

    mutable std::vector<IndexEntry> index_;
    mutable std::map<std::string, size_t> idToIndexMap_;

    void createIndex() const;
#endif // PWIZ_READER_WATERS
};


} // detail
} // msdata
} // pwiz
