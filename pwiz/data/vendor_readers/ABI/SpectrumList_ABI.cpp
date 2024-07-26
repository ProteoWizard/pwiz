//
// $Id: SpectrumList_ABI.cpp 11220 2017-08-15 03:24:49Z nickshulman $
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


#include "SpectrumList_ABI.hpp"


#ifdef PWIZ_READER_ABI
#include "Reader_ABI_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace ABI;

PWIZ_API_DECL SpectrumList_ABI::SpectrumList_ABI(const MSData& msd, WiffFilePtr wifffile,
                                                 const ExperimentsMap& experimentsMap, int sample,
                                                 const Reader::Config& config)
:   msd_(msd),
    wifffile_(wifffile),
    config_(config),
    experimentsMap_(experimentsMap),
    sample(sample),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy),
    spectrumLastIndex_((size_t)-1)
{
}


PWIZ_API_DECL size_t SpectrumList_ABI::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_ABI::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_ABI::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_ABI::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_ABI::spectrumIdentity()] Bad index: " + lexical_cast<string>(index));
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_ABI::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_ABI::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_ABI::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_ABI::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_ABI::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_ABI::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_ABI::createIndex, this));
    if (index >= size_)
        throw runtime_error("[SpectrumList_ABI::spectrum()] Bad index: " + lexical_cast<string>(index));


    // allocate a new Spectrum
    IndexEntry& ie = index_[index];
    SpectrumPtr result = SpectrumPtr(new Spectrum);
    if (!result.get())
        throw std::runtime_error("[SpectrumList_ABI::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    //Console::WriteLine("spce: {0}.{1}.{2}.{3}", ie.sample, ie.period, ie.cycle, ie.experiment);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];

    ExperimentPtr msExperiment = ie.experiment;

    // Synchronize access to cached spectrum for multi-threaded use
    pwiz::vendor_api::ABI::SpectrumPtr spectrum;
    {
        boost::mutex::scoped_lock spectrum_lock(spectrum_mutex);

        if (spectrumLastIndex_ != index)
        {
            spectrumLastIndex_ = index;
            spectrumLast_ = wifffile_->getSpectrum(msExperiment, ie.cycle);
        }
        spectrum = spectrumLast_;
    }

    double scanTime = spectrum->getStartTime();
    if (scanTime > 0)
        scan.set(MS_scan_start_time, scanTime, UO_minute);
    scan.set(MS_preset_scan_configuration, msExperiment->getExperimentNumber());
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    ExperimentType experimentType = msExperiment->getExperimentType();
    int msLevel = spectrum->getMSLevel();
    result->set(MS_ms_level, msLevel);
    CVID spectrumType = translateAsSpectrumType(experimentType);
    result->set(spectrumType);
    result->set(translate(msExperiment->getPolarity()));

    double startMz, stopMz;
    msExperiment->getAcquisitionMassRange(startMz, stopMz);
    scan.scanWindows.push_back(ScanWindow(startMz, stopMz, MS_m_z));

    // decide whether to use Points or Peaks to populate data arrays
    bool doCentroid = msLevelsToCentroid.contains(msLevel);

    if (!doCentroid && spectrum->getDataIsContinuous())
        result->set(MS_profile_spectrum);
    else
        result->set(MS_centroid_spectrum);

    if (spectrum->getHasPrecursorInfo())
    {
        double selectedMz = 0, intensity, collisionEnergy = 0, electronKineticEnergy = 0;
        double centerMz = 0, lowerLimit, upperLimit;
        int charge;
        FragmentationMode fragmentationMode = FragmentationMode_CID;
        spectrum->getPrecursorInfo(selectedMz, intensity, charge);

        if (spectrum->getHasIsolationInfo())
        {
            spectrum->getIsolationInfo(centerMz, lowerLimit, upperLimit, collisionEnergy, electronKineticEnergy, fragmentationMode);
            selectedMz = centerMz;
        }

        if (spectrumType == MS_precursor_ion_spectrum)
        {
            Product product;

            // CONSIDER: error/warn if no isolation info?
            if (centerMz > 0)
            {
                product.isolationWindow.set(MS_isolation_window_target_m_z, centerMz, MS_m_z);
                if (lowerLimit > 0 && upperLimit > 0)
                {
                    product.isolationWindow.set(MS_isolation_window_lower_offset, centerMz - lowerLimit, MS_m_z);
                    product.isolationWindow.set(MS_isolation_window_upper_offset, upperLimit - centerMz, MS_m_z);
                }
            }

            result->products.push_back(product);
        }
        else
        {
            Precursor precursor;
            SelectedIon selectedIon;

            if (centerMz > 0)
            {
                precursor.isolationWindow.set(MS_isolation_window_target_m_z, centerMz, MS_m_z);
                if (lowerLimit > 0 && upperLimit > 0)
                {
                    precursor.isolationWindow.set(MS_isolation_window_lower_offset, centerMz - lowerLimit, MS_m_z);
                    precursor.isolationWindow.set(MS_isolation_window_upper_offset, upperLimit - centerMz, MS_m_z);
                }
            }

            selectedIon.set(MS_selected_ion_m_z, selectedMz, MS_m_z);
            if (charge > 0)
                selectedIon.set(MS_charge_state, charge);

            if(fragmentationMode == FragmentationMode_CID)
                precursor.activation.set(MS_beam_type_collision_induced_dissociation); // assume beam-type CID since all ABI instruments that write WIFFs are either QqTOF or QqLIT
            else if(fragmentationMode == FragmentationMode_EAD)
            {
                precursor.activation.set(MS_EAD);
                if(electronKineticEnergy > 0)
                    precursor.activation.set(MS_electron_beam_energy, electronKineticEnergy, UO_electronvolt);
            }
            
            if (collisionEnergy > 0)
                precursor.activation.set(MS_collision_energy, collisionEnergy, UO_electronvolt);

            precursor.selectedIons.push_back(selectedIon);
            result->precursors.push_back(precursor);
        }
    }

    if (detailLevel == DetailLevel_InstantMetadata)
        return result;

    // Revert to previous behavior for getting binary data or not.
    bool getBinaryData = (detailLevel == DetailLevel_FullData);

    //result->set(MS_lowest_observed_m_z, spectrum->getMinX(), MS_m_z);
    //result->set(MS_highest_observed_m_z, spectrum->getMaxX(), MS_m_z);

    if (!config_.acceptZeroLengthSpectra && spectrum->getBasePeakY() > 0)
    {
        result->set(MS_base_peak_intensity, spectrum->getBasePeakY(), MS_number_of_detector_counts);
        result->set(MS_base_peak_m_z, spectrum->getBasePeakX(), MS_m_z);
    }

    result->set(MS_total_ion_current, spectrum->getSumY(), MS_number_of_detector_counts);

    if (getBinaryData)
    {
        result->setMZIntensityArrays(std::vector<double>(), std::vector<double>(), MS_number_of_detector_counts);
        BinaryDataArrayPtr mzArray = result->getMZArray();
        BinaryDataArrayPtr intensityArray = result->getIntensityArray();

        spectrum->getData(doCentroid, mzArray->data, intensityArray->data, config_.ignoreZeroIntensityPoints);
        if (doCentroid)
            result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum
    }

    // This forces the WIFF reader to get the data, making full metadata
    // nearly equivalent in performance to getting binary.
    result->defaultArrayLength = spectrum->getDataSize(doCentroid, config_.ignoreZeroIntensityPoints);

    return result;
}


PWIZ_API_DECL void SpectrumList_ABI::createIndex() const
{
    using namespace boost::spirit::karma;

    typedef multimap<double, pair<ExperimentPtr, int> > ExperimentAndCycleByTime;
    ExperimentAndCycleByTime experimentAndCycleByTime;
    vector<double> times, intensities;

    bool wiff2 = bal::iends_with(wifffile_->getWiffPath(), ".wiff2");

    int periodCount = wifffile_->getPeriodCount(sample);
    for (int period=1; period <= periodCount; ++period)
    {
        int experimentCount = wifffile_->getExperimentCount(sample, period);
        for (int experiment=1; experiment <= experimentCount; ++experiment)
        {
            ExperimentPtr msExperiment = experimentsMap_.find(pair<int, int>(period, experiment))->second;

            ExperimentType experimentType = msExperiment->getExperimentType();

            if (bal::iends_with(wifffile_->getWiffPath(), "wiff2") &&
                (experimentType == MRM || experimentType == SIM))
            {
                warn_once("WARNING: the WIFF2 reader does not support SIM/MRM chromatograms or spectra; point at the WIFF file instead");
                continue;
            }

            if ((experimentType == MRM && !config_.srmAsSpectra) ||
                (experimentType == SIM && !config_.simAsSpectra))
                continue;

            if (config_.acceptZeroLengthSpectra)
            {
                msExperiment->getTIC(times, intensities);

                for (int i = 0, end = (int) times.size(); i < end; ++i)
                    if ((!wiff2 || intensities[i] > 0) &&
                        (experimentType != ABI::Product || wifffile_->getSpectrum(msExperiment, i+1)->getHasPrecursorInfo()))
                        experimentAndCycleByTime.insert(make_pair(times[i], make_pair(msExperiment, i + 1)));
            }
            else
            {
                msExperiment->getBPC(times, intensities);
                if (times.empty())
                    msExperiment->getTIC(times, intensities);

                for (int i = 0, end = (int)times.size(); i < end; ++i)
                    if (intensities[i] > 0 && wifffile_->getSpectrum(msExperiment, i + 1)->getDataSize(false, true) > 0)
                        experimentAndCycleByTime.insert(make_pair(times[i], make_pair(msExperiment, i + 1)));
            }
        }
    }

    for(const ExperimentAndCycleByTime::value_type& itr : experimentAndCycleByTime)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.sample = sample;
        ie.period = 1; // TODO
        ie.cycle = itr.second.second;
        ie.experiment = itr.second.first;
        ie.index = index_.size()-1;


        std::back_insert_iterator<std::string> sink(ie.id);
        generate(sink,
                 "sample=" << int_ << " period=" << int_ << " cycle=" << int_ << " experiment=" << int_,
                 ie.sample, ie.period, ie.cycle, ie.experiment->getExperimentNumber());
        idToIndexMap_[ie.id] = ie.index;
    }

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_ABI

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_ABI::size() const {return 0;}
const SpectrumIdentity& SpectrumList_ABI::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_ABI::find(const std::string& id) const {return 0;}
SpectrumPtr SpectrumList_ABI::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_ABI::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_ABI::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_ABI::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI
