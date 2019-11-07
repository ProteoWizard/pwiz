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


#include "SpectrumList_UNIFI.hpp"


#ifdef PWIZ_READER_UNIFI
#include "Reader_UNIFI_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace UNIFI;

PWIZ_API_DECL SpectrumList_UNIFI::SpectrumList_UNIFI(const MSData& msd, UnifiDataPtr unifiData,
                                                 const Reader::Config& config)
:   msd_(msd),
    unifiData_(unifiData),
    config_(config),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t SpectrumList_UNIFI::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UNIFI::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_UNIFI::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UNIFI::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_UNIFI::spectrumIdentity()] Bad index: " + lexical_cast<string>(index));
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_UNIFI::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UNIFI::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
	return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UNIFI::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_UNIFI::spectrum()] Bad index: " + lexical_cast<string>(index));


    // allocate a new Spectrum
    IndexEntry& ie = index_[index];
    SpectrumPtr result = SpectrumPtr(new Spectrum);
    if (!result.get())
        throw std::runtime_error("[SpectrumList_UNIFI::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    //Console::WriteLine("spce: {0}.{1}.{2}.{3}", ie.sample, ie.period, ie.cycle, ie.experiment);
    UnifiSpectrum spectrum;
    unifiData_->getSpectrum(index, spectrum, detailLevel >= DetailLevel_FullMetadata);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    if (spectrum.retentionTime > 0)
        scan.set(MS_scan_start_time, spectrum.retentionTime, UO_minute);

    if (spectrum.driftTime > 0)
        scan.set(MS_ion_mobility_drift_time, spectrum.driftTime, UO_millisecond);

    scan.set(MS_preset_scan_configuration, spectrum.energyLevel == EnergyLevel::Low ? 1 : 2);

    int msLevel = spectrum.energyLevel == EnergyLevel::Low ? 1 : 2;//spectrum->getMSLevel();
    result->set(MS_ms_level, msLevel);
    result->set(msLevel == 1 ? MS_MS1_spectrum : MS_MSn_spectrum);
    //result->set(translateAsSpectrumType(experimentType));
    result->set(translate(spectrum.scanPolarity));

    scan.scanWindows.push_back(ScanWindow(spectrum.scanRange.first, spectrum.scanRange.second, MS_m_z));

    result->set(MS_profile_spectrum);

    // decide whether to use Points or Peaks to populate data arrays
    /*bool doCentroid = msLevelsToCentroid.contains(msLevel);

    bool continuousData = spectrum->getDataIsContinuous();
    if (continuousData && !doCentroid)
        result->set(MS_profile_spectrum);
    else
    {
        result->set(MS_centroid_spectrum);
        doCentroid = continuousData;
    }*/

    {
        if (msLevel > 1)
        {
            double centerMz = (spectrum.scanRange.second - spectrum.scanRange.first) / 2;

            Precursor precursor;
            precursor.isolationWindow.set(MS_isolation_window_target_m_z, centerMz, MS_m_z);
            precursor.isolationWindow.set(MS_isolation_window_lower_offset, centerMz - spectrum.scanRange.first, MS_m_z);
            precursor.isolationWindow.set(MS_isolation_window_upper_offset, spectrum.scanRange.second - centerMz, MS_m_z);

            precursor.activation.set(MS_beam_type_collision_induced_dissociation); // assume beam-type CID since all UNIFI instruments are TOFs (?)

            precursor.selectedIons.emplace_back(centerMz);
            result->precursors.push_back(precursor);
        }

        if (detailLevel == DetailLevel_InstantMetadata)
            return result;

        // Revert to previous behavior for getting binary data or not.
        bool getBinaryData = (detailLevel == DetailLevel_FullData);

        //result->set(MS_lowest_observed_m_z, spectrum->getMinX(), MS_m_z);
        //result->set(MS_highest_observed_m_z, spectrum->getMaxX(), MS_m_z);

        /*if (!config_.acceptZeroLengthSpectra)
        {
            result->set(MS_base_peak_intensity, spectrum->getBasePeakY(), MS_number_of_detector_counts);
            result->set(MS_base_peak_m_z, spectrum->getBasePeakX(), MS_m_z);
        }*/

        //result->set(MS_total_ion_current, spectrum->getSumY(), MS_number_of_detector_counts);

        if (getBinaryData)
        {
            result->swapMZIntensityArrays(spectrum.mzArray, spectrum.intensityArray, MS_number_of_detector_counts);

            //if (doCentroid)
            //    result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum
        }
        else
            result->defaultArrayLength = spectrum.arrayLength;
    }

    return result;
}


PWIZ_API_DECL void SpectrumList_UNIFI::createIndex() const
{
    using namespace boost::spirit::karma;

    for (size_t i=0; i < unifiData_->numberOfSpectra(); ++i)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.index = index_.size()-1;

        std::back_insert_iterator<std::string> sink(ie.id);
        generate(sink, "scan=" << int_, ie.index+1);
        idToIndexMap_[ie.id] = ie.index;
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

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_UNIFI::size() const {return 0;}
const SpectrumIdentity& SpectrumList_UNIFI::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_UNIFI::find(const std::string& id) const {return 0;}
SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_UNIFI::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_UNIFI
