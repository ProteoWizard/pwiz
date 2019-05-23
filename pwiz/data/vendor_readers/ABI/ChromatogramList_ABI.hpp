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

#ifdef PWIZ_READER_ABI
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"
#include "pwiz/utility/misc/Once.hpp"
using namespace pwiz::vendor_api::ABI;
#endif // PWIZ_READER_ABI


namespace pwiz {
namespace msdata {
namespace detail {


class PWIZ_API_DECL ChromatogramList_ABI : public ChromatogramListBase
{
    public:

    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    virtual ChromatogramPtr chromatogram(size_t index, DetailLevel detailLevel) const;
    
#ifdef PWIZ_READER_ABI
    ChromatogramList_ABI(const MSData& msd, WiffFilePtr wifffile, const ExperimentsMap& experimentsMap, int sample);

    private:

    const MSData& msd_;
    WiffFilePtr wifffile_;
    ExperimentsMap experimentsMap_;
    int sample;

    mutable size_t size_;

    mutable util::once_flag_proxy indexInitialized_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        int sample;
        int period;
        ExperimentPtr experiment;
        int transition;
        double q1, q3;
    };

    mutable std::vector<IndexEntry> index_;
    mutable std::map<std::string, size_t> idToIndexMap_;

    void createIndex() const;
#endif // PWIZ_READER_ABI
};


} // detail
} // msdata
} // pwiz
