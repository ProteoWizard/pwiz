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


#include "SpectrumList_Shimadzu.hpp"


#ifdef PWIZ_READER_SHIMADZU
//#include "Reader_Shimadzu_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

//using namespace Shimadzu;

PWIZ_API_DECL SpectrumList_Shimadzu::SpectrumList_Shimadzu(const MSData& msd, ShimadzuReaderPtr rawfile, const Reader::Config& config)
:   msd_(msd),
    rawfile_(rawfile),
    config_(config),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t SpectrumList_Shimadzu::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Shimadzu::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Shimadzu::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Shimadzu::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_Shimadzu::spectrumIdentity()] Bad index: " + lexical_cast<string>(index));
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Shimadzu::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Shimadzu::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
	return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Shimadzu::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_Shimadzu::spectrum()] Bad index: " + lexical_cast<string>(index));


    // allocate a new Spectrum
    IndexEntry& ie = index_[index];
    SpectrumPtr result = SpectrumPtr(new Spectrum);
    if (!result.get())
        throw std::runtime_error("[SpectrumList_Shimadzu::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    pwiz::vendor_api::Shimadzu::SpectrumInfo info = rawfile_->getSpectrumInfo(ie.scanNumber);
    int msLevel = info.msLevel;
    result->set(MS_ms_level, msLevel);

    // decide whether to use Points or Peaks to populate data arrays
    bool doCentroid = msLevelsToCentroid.contains(msLevel);

    pwiz::vendor_api::Shimadzu::SpectrumPtr spectrum = rawfile_->getSpectrum(ie.scanNumber, !doCentroid);

    double scanTime = spectrum->getScanTime();
    if (scanTime > 0 || ie.scanNumber == 1)
        scan.set(MS_scan_start_time, scanTime / 1000, UO_second); // Shimadzu stores time in milliseconds

    if (msLevel == 1)
        result->set(MS_MS1_spectrum);
    else
        result->set(MS_MSn_spectrum); // TODO: get more test data

    switch (spectrum->getPolarity())
    {
        case Positive: result->set(MS_positive_scan); break;
        case Negative: result->set(MS_negative_scan); break;
        default: break;
    }

    if (spectrum->getMinX() > 0)
        scan.scanWindows.push_back(ScanWindow(spectrum->getMinX(), spectrum->getMaxX(), MS_m_z));

    /*if (experimentType == MRM)
    {
        MRMTransitions^ transitions = msExperiment->MRMTransitions;
        double q1mz = transitions[ie.transition]->Q1Mass;//ie.transition->first;
        double q3mz = transitions[ie.transition]->Q3Mass;
        double intensity = points[ie.transition]->Y;
        result->defaultArrayLength = 1;//ie.transition->second.size();

        Precursor precursor;
        SelectedIon selectedIon;

        selectedIon.set(MS_selected_ion_m_z, q1mz, MS_m_z);

        precursor.activation.set(MS_CID); // assume CID

        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);

        if (getBinaryData)
        {
            mzArray.resize(result->defaultArrayLength, q3mz);
            intensityArray.resize(result->defaultArrayLength, intensity);
        }
    }
    else*/
    {
        if (spectrum->getHasPrecursorInfo())
        {
            double selectedMz, intensity;
            int charge;
            spectrum->getPrecursorInfo(selectedMz, intensity, charge);

            Precursor precursor;
            /*if (spectrum->getHasIsolationInfo())
            {
                double centerMz, lowerLimit, upperLimit;
                spectrum->getIsolationInfo(centerMz, lowerLimit, upperLimit);
                precursor.isolationWindow.set(MS_isolation_window_target_m_z, centerMz, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_lower_offset, centerMz - lowerLimit, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_upper_offset, upperLimit - centerMz, MS_m_z);
				selectedMz = centerMz;
            }*/
            precursor.isolationWindow.set(MS_isolation_window_target_m_z, selectedMz, MS_m_z);

            SelectedIon selectedIon;

            selectedIon.set(MS_selected_ion_m_z, selectedMz, MS_m_z);
            if (charge > 0)
                selectedIon.set(MS_charge_state, charge);

            precursor.activation.set(MS_beam_type_collision_induced_dissociation); // assume beam-type CID since all Shimadzu instruments that produce spectra are QqTOF

            precursor.selectedIons.push_back(selectedIon);
            result->precursors.push_back(precursor);
        }

        bool hasProfile = spectrum->getTotalDataPoints(false) > 0;
        //bool hasCentroid = spectrum->getTotalDataPoints(true) > 0;
        bool mustCentroid = false;
        if (!doCentroid)
        {
            if (!hasProfile)
                mustCentroid = true;
        }

        if (!doCentroid && !mustCentroid)
            result->set(MS_profile_spectrum);
        else
            result->set(MS_centroid_spectrum);

        result->defaultArrayLength = spectrum->getTotalDataPoints(doCentroid);

        if (detailLevel < DetailLevel_FullMetadata)
            return result;

        // Revert to previous behavior for getting binary data or not.
        bool getBinaryData = (detailLevel == DetailLevel_FullData);

        result->set(MS_base_peak_intensity, spectrum->getBasePeakY(), MS_number_of_detector_counts);
        result->set(MS_base_peak_m_z, spectrum->getBasePeakX(), MS_m_z);

        result->set(MS_total_ion_current, spectrum->getSumY(), MS_number_of_detector_counts);

        if (getBinaryData)
        {
            result->setMZIntensityArrays(std::vector<double>(), std::vector<double>(), MS_number_of_detector_counts);
            BinaryDataArrayPtr mzArray = result->getMZArray();
            BinaryDataArrayPtr intensityArray = result->getIntensityArray();

            if (doCentroid || mustCentroid)
                spectrum->getCentroidArrays(mzArray->data, intensityArray->data);
            else
                spectrum->getProfileArrays(mzArray->data, intensityArray->data);
            result->defaultArrayLength = mzArray->data.size();
            if (doCentroid && !mustCentroid)
                result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum
        }
    }

    return result;
}


PWIZ_API_DECL void SpectrumList_Shimadzu::createIndex() const
{
    using namespace boost::spirit::karma;

    for(int i=1, end=rawfile_->getScanCount(); i <= end; ++i)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.scanNumber = i;
        ie.index = index_.size()-1;

        std::back_insert_iterator<std::string> sink(ie.id);
        generate(sink, "scan=" << int_, ie.scanNumber);
        idToIndexMap_[ie.id] = ie.index;
    }

    size_ = index_.size();
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

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_Shimadzu::size() const {return 0;}
const SpectrumIdentity& SpectrumList_Shimadzu::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_Shimadzu::find(const std::string& id) const {return 0;}
SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Shimadzu::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_Shimadzu
