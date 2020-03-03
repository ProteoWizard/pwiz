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


#include "ChromatogramList_Shimadzu.hpp"


#ifdef PWIZ_READER_SHIMADZU
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

ChromatogramList_Shimadzu::ChromatogramList_Shimadzu(ShimadzuReaderPtr rawfile, const Reader::Config& config)
:   rawfile_(rawfile), config_(config), indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_Shimadzu::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Shimadzu::createIndex, this));
    return index_.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Shimadzu::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Shimadzu::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Shimadzu::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return reinterpret_cast<const ChromatogramIdentity&>(index_[index]);
}


PWIZ_API_DECL size_t ChromatogramList_Shimadzu::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Shimadzu::createIndex, this));
    map<string, size_t>::const_iterator itr = idMap_.find(id);
    if (itr != idMap_.end())
        return itr->second;

    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Shimadzu::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Shimadzu::chromatogram(size_t index, DetailLevel detailLevel) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_Shimadzu::createIndex, this));
    if (index>size())
        throw runtime_error(("[ChromatogramList_Shimadzu::chromatogram()] Bad index: " 
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

            auto ticPtr = rawfile_->getTIC(config_.globalChromatogramsAreMs1Only);
            if (getBinaryData)
            {
                result->setTimeIntensityArrays(vector<double>(), vector<double>(), UO_minute, MS_number_of_detector_counts);
                ticPtr->getXArray(result->getTimeArray()->data);
                ticPtr->getYArray(result->getIntensityArray()->data);
            }
            result->defaultArrayLength = ticPtr->getTotalDataPoints();
        }
        break;

        case MS_SRM_chromatogram:
        {
            pwiz::vendor_api::Shimadzu::ChromatogramPtr chromatogramPtr(rawfile_->getSRM(ci.transition));

            result->precursor.isolationWindow.set(MS_isolation_window_target_m_z, ci.transition.Q1, MS_m_z);
            result->precursor.activation.set(MS_CID);
            result->precursor.activation.set(MS_collision_energy, ci.transition.collisionEnergy, UO_electronvolt);
            result->set(ci.transition.polarity != 1 ? MS_positive_scan : MS_negative_scan);

            result->product.isolationWindow.set(MS_isolation_window_target_m_z, ci.transition.Q3, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_lower_offset, ci.q3Offset, MS_m_z);
            //result->product.isolationWindow.set(MS_isolation_window_upper_offset, ci.q3Offset, MS_m_z);

            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            if (getBinaryData)
            {
                result->setTimeIntensityArrays(vector<double>(), vector<double>(), UO_minute, MS_number_of_detector_counts);
                chromatogramPtr->getXArray(result->getTimeArray()->data);
                chromatogramPtr->getYArray(result->getIntensityArray()->data);
                result->defaultArrayLength = result->getTimeArray()->data.size();
            }
            else
                result->defaultArrayLength = chromatogramPtr->getTotalDataPoints();
        }
        break;
    }

    return result;
}


PWIZ_API_DECL void ChromatogramList_Shimadzu::createIndex() const
{
    const set<SRMTransition>& transitions = rawfile_->getTransitions();

    if (transitions.empty()) // MRM file reading interface doesn't provide TIC
    {
        // support file-level TIC for all file types
        index_.push_back(IndexEntry());
        IndexEntry& ci = index_.back();
        ci.index = index_.size() - 1;
        ci.chromatogramType = MS_TIC_chromatogram;
        ci.id = "TIC";
        idMap_[ci.id] = ci.index;
    }

    for (const SRMTransition& transition : transitions)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ci = index_.back();
        ci.index = index_.size()-1;
        ci.transition = transition;
        ci.chromatogramType = MS_SRM_chromatogram;
        ci.id = (format("%sSRM SIC Q1=%.10g Q3=%.10g Channel=%d Event=%d Segment=%d CE=%.10g"/* start=%.10g end=%.10g"*/)
                    % polarityStringForFilter((transition.polarity == 1) ? MS_negative_scan : MS_positive_scan)
                    % transition.Q1
                    % transition.Q3
                    % transition.channel
                    % transition.event
                    % transition.segment
                    % transition.collisionEnergy
                    /*% transition.acquiredTimeRange.start
                    % transition.acquiredTimeRange.end*/
                ).str();
        idMap_[ci.id] = ci.index;
    }
}

} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_SHIMADZU

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_Shimadzu::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_Shimadzu::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_Shimadzu::find(const string& id) const {return 0;}
ChromatogramPtr ChromatogramList_Shimadzu::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_SHIMADZU
