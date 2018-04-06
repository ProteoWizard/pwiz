//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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
#include "pwiz/utility/misc/IntegerSet.hpp"

#ifdef PWIZ_READER_ABI_T2D
#include "pwiz_aux/msrc/utility/vendor_api/ABI/T2D/T2D_Data.hpp"
#include "pwiz/utility/misc/Once.hpp"
using namespace pwiz::vendor_api::ABI::T2D;
#endif // PWIZ_READER_ABI_T2D


namespace pwiz {
namespace msdata {
namespace detail {

class PWIZ_API_DECL SpectrumList_ABI_T2D : public SpectrumListBase
{
    public:

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    
#ifdef PWIZ_READER_ABI_T2D
    SpectrumList_ABI_T2D(const MSData& msd, DataPtr t2d_data);

    private:

    const MSData& msd_;
    DataPtr t2d_data_;

    size_t size_;

    std::vector<SpectrumIdentity> index_;
    std::map<std::string, size_t> idToIndexMap_;

    void createIndex();
#endif // PWIZ_READER_ABI_T2D
};


} // detail
} // msdata
} // pwiz
