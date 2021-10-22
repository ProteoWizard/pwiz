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


#define PWIZ_SOURCE


#include "ChromatogramList_Mobilion.hpp"


#ifdef PWIZ_READER_MOBILION
#include "Reader_Mobilion_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Mobilion;

PWIZ_API_DECL ChromatogramList_Mobilion::ChromatogramList_Mobilion(const MBIFilePtr& rawdata, const Reader::Config& config)
:   rawdata_(rawdata),
    size_(0),
    config_(config),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_Mobilion::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Mobilion::createIndex, this));
    return size_;
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Mobilion::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Mobilion::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_Mobilion::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t ChromatogramList_Mobilion::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Mobilion::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Mobilion::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Mobilion::chromatogram(size_t index, DetailLevel detailLevel) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Mobilion::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_Mobilion::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    
    // allocate a new Chromatogram
    IndexEntry& ie = index_[index];
    ChromatogramPtr result = ChromatogramPtr(new Chromatogram);
    if (!result.get())
        throw std::runtime_error("[ChromatogramList_Mobilion::chromatogram()] Allocation error.");

    result->index = index;
    result->id = ie.id;
    result->set(ie.chromatogramType);

    switch (ie.chromatogramType)
    {
        case MS_TIC_chromatogram:
        {
            result->defaultArrayLength = rawdata_->NumFrames();
            if (detailLevel < DetailLevel_FullData)
                return result;

            /*auto ticMap = rawdata_->GetTotalChromatogram();
            
            if (ticMap.size() != rawdata_->NumFrames())
                throw runtime_error("[ChromatogramList_Mobilion::chromatogram()] TIC map size not equal to NumFrames");*/

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);
            result->defaultArrayLength = rawdata_->NumFrames();

            BinaryDataArrayPtr timeArray = result->getTimeArray();
            BinaryDataArrayPtr intensityArray = result->getIntensityArray();

            auto msLevelArray = boost::make_shared<IntegerDataArray>();
            result->integerDataArrayPtrs.emplace_back(msLevelArray);
            msLevelArray->set(MS_non_standard_data_array, "ms level", UO_dimensionless_unit);

            auto& timeData = timeArray->data;
            auto& intensityData = intensityArray->data;
            auto& msLevelData = msLevelArray->data;

            timeData.resize(result->defaultArrayLength);
            intensityData.resize(result->defaultArrayLength);
            msLevelData.resize(result->defaultArrayLength);

            auto mzItr = &timeData[0], intItr = &intensityData[0];
            auto msLevelItr = &msLevelData[0];
            for (int i = 0; i < rawdata_->NumFrames(); ++i, ++mzItr, ++intItr, ++msLevelItr)
            {
                auto frame = rawdata_->GetFrame(i);
                *mzItr = frame->Time();
                *intItr = frame->TotalIntensity();
                *msLevelItr = frame->IsFragmentationData() ? 2 : 1;
            }
        }
        break;
    }

    return result;
}

PWIZ_API_DECL void ChromatogramList_Mobilion::createIndex() const
{
    index_.push_back(IndexEntry());
    IndexEntry& ie = index_.back();
    ie.index = index_.size()-1;
    ie.id = "TIC";
    ie.chromatogramType = MS_TIC_chromatogram;
    idToIndexMap_[ie.id] = ie.index;

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz

#else // PWIZ_READER_MOBILION

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_Mobilion::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_Mobilion::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_Mobilion::find(const std::string& id) const {return 0;}
ChromatogramPtr ChromatogramList_Mobilion::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}
ChromatogramPtr ChromatogramList_Mobilion::chromatogram(size_t index, DetailLevel detailLevel) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_MOBILION
