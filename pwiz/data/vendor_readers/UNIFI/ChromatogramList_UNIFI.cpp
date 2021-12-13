//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2021
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


#include "ChromatogramList_UNIFI.hpp"


#ifdef PWIZ_READER_UNIFI
#include "Reader_UNIFI_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/sort_together.hpp"
#include <boost/bind.hpp>
#include <boost/xpressive/xpressive_dynamic.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace UNIFI;
namespace bxp = boost::xpressive;

PWIZ_API_DECL ChromatogramList_UNIFI::ChromatogramList_UNIFI(const MSData& msd, UnifiDataPtr unifiData, const Reader::Config& config)
:   msd_(msd),
    unifiData_(unifiData),
    size_(0),
    config_(config),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t ChromatogramList_UNIFI::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UNIFI::createIndex, this));
    return size_;
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_UNIFI::chromatogramIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UNIFI::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_UNIFI::chromatogramIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t ChromatogramList_UNIFI::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UNIFI::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_UNIFI::chromatogram(size_t index, bool getBinaryData) const
{
    return chromatogram(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL void ChromatogramList_UNIFI::makeFullFileChromatogram(pwiz::msdata::ChromatogramPtr& result, const string& chromatogramTag, bool getBinaryData) const
{
    static bxp::sregex functionNumberRegex = bxp::sregex::compile("(\\d+)\\:.*");
    bxp::smatch what;

    multimap<double, pair<int, double>> fullFileTIC;

    result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);

    for (const auto& info : unifiData_->chromatogramInfo())
    {
        if (!bal::contains(info.id, chromatogramTag))
            continue;

        if (!bxp::regex_match(info.id, what, functionNumberRegex))
            msd_.run.spectrumListPtr->warn_once(("unable to parse function number from chromatogram name: " + info.id).c_str());

        int functionNumber = lexical_cast<int>(what[1].str());
        if (config_.globalChromatogramsAreMs1Only && functionNumber != 1)
            continue;

        UnifiChromatogram chromatogram;
        unifiData_->getChromatogram(info.index, chromatogram, getBinaryData);

        if (getBinaryData)
        {
            for (size_t i = 0; i < chromatogram.arrayLength; ++i)
                fullFileTIC.insert(make_pair(chromatogram.timeArray[i], make_pair(functionNumber, chromatogram.intensityArray[i])));
        }
        else
            result->defaultArrayLength += chromatogram.arrayLength;
    }

    if (getBinaryData)
    {
        BinaryDataArrayPtr timeArray = result->getTimeArray();
        BinaryDataArrayPtr intensityArray = result->getIntensityArray();

        auto functionArray = boost::make_shared<IntegerDataArray>();
        result->integerDataArrayPtrs.emplace_back(functionArray);
        functionArray->set(MS_non_standard_data_array, "function", UO_dimensionless_unit);
        functionArray->data.reserve(fullFileTIC.size());

        timeArray->data.reserve(fullFileTIC.size());
        intensityArray->data.reserve(fullFileTIC.size());
        for (auto itr = fullFileTIC.begin(); itr != fullFileTIC.end(); ++itr)
        {
            timeArray->data.push_back(itr->first);
            intensityArray->data.push_back(itr->second.second);
            functionArray->data.push_back(itr->second.first);
        }
    }

    result->defaultArrayLength = fullFileTIC.size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_UNIFI::chromatogram(size_t index, DetailLevel detailLevel) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&ChromatogramList_UNIFI::createIndex, this));
    if (index>size_)
        throw runtime_error(("[ChromatogramList_UNIFI::chromatogram()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // allocate a new Chromatogram
    IndexEntry& ie = index_[index];
    ChromatogramPtr result = ChromatogramPtr(new Chromatogram);
    if (!result.get())
        throw std::runtime_error("[ChromatogramList_UNIFI::chromatogram()] Allocation error.");

    result->index = index;
    result->id = ie.id;
    result->set(ie.chromatogramType);

    bool getBinaryData = detailLevel == DetailLevel_FullData;

    switch (ie.chromatogramType)
    {
        case MS_TIC_chromatogram:
        {
            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            makeFullFileChromatogram(result, "(TIC)", getBinaryData);
        }
        break;

        case MS_basepeak_chromatogram:
        {
            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            makeFullFileChromatogram(result, "(BPC)", getBinaryData);
        }
        break;

        case MS_emission_chromatogram:
        {
            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, MS_number_of_detector_counts);

            UnifiChromatogram chromatogram;
            unifiData_->getChromatogram(ie.chromatogramInfoIndex, chromatogram, getBinaryData);
            result->defaultArrayLength = chromatogram.arrayLength;

            if (getBinaryData)
            {
                swap(chromatogram.timeArray, result->getTimeArray()->data);
                swap(chromatogram.intensityArray, result->getIntensityArray()->data);
            }
        }
        break;

        case MS_absorption_chromatogram:
        {
            if (detailLevel < DetailLevel_FullMetadata)
                return result;

            result->setTimeIntensityArrays(std::vector<double>(), std::vector<double>(), UO_minute, UO_absorbance_unit);

            UnifiChromatogram chromatogram;
            unifiData_->getChromatogram(ie.chromatogramInfoIndex, chromatogram, getBinaryData);
            result->defaultArrayLength = chromatogram.arrayLength;

            if (getBinaryData)
            {
                swap(chromatogram.timeArray, result->getTimeArray()->data);
                swap(chromatogram.intensityArray, result->getIntensityArray()->data);
            }
        }
        break;
    }

    return result;
}

PWIZ_API_DECL void ChromatogramList_UNIFI::createIndex() const
{
    bool hasTIC = false, hasBPI = false;
    for (size_t i=0; i < unifiData_->chromatogramInfo().size(); ++i)
    {
        const auto& chromatogramInfo = unifiData_->chromatogramInfo()[i];
        switch (chromatogramInfo.detectorType)
        {
            case DetectorType::MS:
            {
                // we only make 1 TIC and BPI chromatogram, not one per function like UNIFI provides
                if (!hasTIC && bal::contains(chromatogramInfo.id, "(TIC)"))
                {
                    hasTIC = true;
                    index_.push_back(IndexEntry());
                    IndexEntry& ie = index_.back();
                    ie.id = "TIC";
                    ie.chromatogramType = MS_TIC_chromatogram;
                }
                else if (!hasBPI && bal::contains(chromatogramInfo.id, "(BPI)"))
                {
                    hasBPI = true;
                    index_.push_back(IndexEntry());
                    IndexEntry& ie = index_.back();
                    ie.id = "BPI";
                    ie.chromatogramType = MS_basepeak_chromatogram;
                }
            }
            break;

            case DetectorType::FLR:
            {
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.id = chromatogramInfo.id;
                ie.chromatogramInfoIndex = i;
                ie.chromatogramType = MS_emission_chromatogram;
            }
            break;

            case DetectorType::UV:
            {
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.id = chromatogramInfo.id;
                ie.chromatogramInfoIndex = i;
                ie.chromatogramType = MS_absorption_chromatogram;
            }
            break;

            default:
                // chromatogram detector type not supported
                break;
        }
    }

    for (size_t i = 0; i < index_.size(); ++i)
    {
        IndexEntry& ie = index_[i];
        ie.index = i;
        idToIndexMap_[ie.id] = i;
    }

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz

#else // PWIZ_READER_UNIFI

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const ChromatogramIdentity emptyIdentity;}

size_t ChromatogramList_UNIFI::size() const {return 0;}
const ChromatogramIdentity& ChromatogramList_UNIFI::chromatogramIdentity(size_t index) const {return emptyIdentity;}
size_t ChromatogramList_UNIFI::find(const std::string& id) const {return 0;}
ChromatogramPtr ChromatogramList_UNIFI::chromatogram(size_t index, bool getBinaryData) const {return ChromatogramPtr();}
ChromatogramPtr ChromatogramList_UNIFI::chromatogram(size_t index, DetailLevel detailLevel) const {return ChromatogramPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_UNIFI
