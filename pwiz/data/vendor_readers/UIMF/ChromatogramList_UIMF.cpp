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


#include "ChromatogramList_UIMF.hpp"


#ifdef PWIZ_READER_UIMF
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

ChromatogramList_UIMF::ChromatogramList_UIMF(UIMFReaderPtr rawfile)
:   rawfile_(rawfile), indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_UIMF::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UIMF::createIndex, this));
    return index_.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_UIMF::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UIMF::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_UIMF::chromatogramIdentity()] Bad index: "
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


PWIZ_API_DECL size_t ChromatogramList_UIMF::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UIMF::createIndex, this));
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_UIMF::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_UIMF::chromatogram(size_t index, DetailLevel detailLevel) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UIMF::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_UIMF::chromatogram()] Bad index: "
                            + lexical_cast<string>(index)).c_str());

    const IndexEntry& ci = index_[index];
    ChromatogramPtr result(new Chromatogram);
    result->index = ci.index;
    result->id = ci.id;

    result->set(ci.chromatogramType);

    bool getBinaryData = detailLevel == DetailLevel_FullData;

    switch (ci.chromatogramType)
    {
        default:
            break;

        case MS_TIC_chromatogram:
        {
            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            if (getBinaryData)
            {
                std::vector<double> timeArray, intensityArray;
                rawfile_->getTic(timeArray, intensityArray);
                result->setTimeIntensityArrays(timeArray, intensityArray, UO_minute, MS_number_of_detector_counts);

                result->defaultArrayLength = result->getTimeArray()->data.size();
            }
            else
                result->defaultArrayLength = rawfile_->getFrameCount();
        }
        break;

        case MS_SRM_chromatogram:
        // TODO: Report an exception?
        break;

        case MS_SIM_chromatogram:
        // TODO: Report an exception?
        break;
    }

    return result;
}

PWIZ_API_DECL void ChromatogramList_UIMF::createIndex() const
{
    // support file-level TIC for all file types
    index_.push_back(IndexEntry());
    IndexEntry& ci = index_.back();
    ci.index = index_.size()-1;
    ci.chromatogramType = MS_TIC_chromatogram;
    ci.id = "TIC";
    idMap_[ci.id] = ci.index;
}

} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_UIMF

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_UIMF::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_UIMF::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_UIMF::find(const string& id) const {return 0;}
ChromatogramPtr ChromatogramList_UIMF::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_UIMF
