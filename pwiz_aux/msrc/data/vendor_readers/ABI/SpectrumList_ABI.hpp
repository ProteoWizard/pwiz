//
// SpectrumList_ABI.hpp
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
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <boost/thread/once.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace pwiz::vendor_api::ABI;

class PWIZ_API_DECL SpectrumList_ABI : public SpectrumListBase
{
    public:

    SpectrumList_ABI(const MSData& msd, WiffFilePtr wifffile, int sample);
    ~SpectrumList_ABI();
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    
    private:

    const MSData& msd_;
    WiffFilePtr wifffile_;
    int sample;

    mutable size_t size_;

    mutable boost::once_flag indexInitialized_;

    struct IndexEntry : public SpectrumIdentity
    {
        int sample;
        int period;
        int cycle;
        int experiment;
        int transition;
    };

    mutable std::vector<IndexEntry> index_;
    mutable std::map<std::string, size_t> idToIndexMap_;

    void createIndex() const;
    size_t findPrecursorSpectrumIndex(int precursorMsLevel, size_t index) const;
};


} // detail
} // msdata
} // pwiz
