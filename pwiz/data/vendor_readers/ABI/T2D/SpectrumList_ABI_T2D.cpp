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


#define PWIZ_SOURCE


#include "SpectrumList_ABI_T2D.hpp"


#ifdef PWIZ_READER_ABI_T2D
#include "Reader_ABI_T2D_Detail.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include <boost/bind.hpp>

using namespace pwiz::minimxml;

namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL SpectrumList_ABI_T2D::SpectrumList_ABI_T2D(const MSData& msd, DataPtr t2d_data)
:   msd_(msd),
    t2d_data_(t2d_data)
{
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_ABI_T2D::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_ABI_T2D::spectrumIdentity(size_t index) const
{
    if (index >= size_)
        throw runtime_error(("[SpectrumList_ABI_T2D::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_ABI_T2D::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_ABI_T2D::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_ABI_T2D::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{
    if (index >= size_)
        throw runtime_error(("[SpectrumList_ABI_T2D::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // allocate a new Spectrum
    const SpectrumIdentity& ie = index_[index];
    SpectrumPtr result = SpectrumPtr(new Spectrum);
    if (!result.get())
        throw std::runtime_error("[SpectrumList_ABI_T2D::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;
    result->sourceFilePtr = msd_.fileDescription.sourceFilePtrs[index];

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    pwiz::vendor_api::ABI::T2D::SpectrumPtr spectrum = t2d_data_->getSpectrum(index);

    // TODO: use file's last modified time to get scan start time?
    /*double scanTime = msExperiment->getCycleStartTime(ie.cycle);
    if (scanTime > 0)
        scan.set(MS_scan_start_time, scanTime, UO_minute);*/

    int msLevel = spectrum->getMsLevel();
    result->set(MS_ms_level, msLevel);
    result->set(translateAsSpectrumType(spectrum->getType()));

    CVID polarity = translate(spectrum->getPolarity());
    if (polarity != CVID_Unknown)
        result->set(polarity);

    /*double startMz, stopMz;
    msExperiment->getAcquisitionMassRange(startMz, stopMz);
    scan.scanWindows.push_back(ScanWindow(startMz, stopMz, MS_m_z));*/

    // decide whether to use RawData or PeakData to populate data arrays
    bool doCentroid = msLevelsToCentroid.contains(msLevel);

    if (doCentroid)
        result->set(MS_centroid_spectrum);
    else
        result->set(MS_profile_spectrum);

    double precursorMz = spectrum->getInstrumentSetting(InstrumentSetting_PreCursorIon);
    if (precursorMz > 0)
    {
        Precursor precursor;
        SelectedIon selectedIon;

        selectedIon.set(MS_selected_ion_m_z, precursorMz, MS_m_z);

        precursor.activation.set(MS_higher_energy_beam_type_collision_induced_dissociation); // assume higher energy beam-type collision for TOF-TOF

        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);
    }

    /*result->set(MS_lowest_observed_m_z, spectrum->getMinMz(), MS_m_z);
    result->set(MS_highest_observed_m_z, spectrum->getMaxMz(), MS_m_z);*/

    double bpmz, bpi;
    spectrum->getBasePeak(bpmz, bpi);
    result->set(MS_base_peak_m_z, bpmz, MS_m_z);
    result->set(MS_base_peak_intensity, bpi, MS_number_of_detector_counts);

    result->set(MS_total_ion_current, spectrum->getTIC(), MS_number_of_detector_counts);

    if (getBinaryData)
    {
        result->setMZIntensityArrays(std::vector<double>(), std::vector<double>(), MS_number_of_detector_counts);
        BinaryDataArrayPtr mzArray = result->getMZArray();
        BinaryDataArrayPtr intensityArray = result->getIntensityArray();

        if (doCentroid)
        {
            spectrum->getPeakData(mzArray->data, intensityArray->data);
            result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum
        }
        else
            spectrum->getRawData(mzArray->data, intensityArray->data);
    }

    if (doCentroid)
        result->defaultArrayLength = spectrum->getPeakDataSize();
    else
        result->defaultArrayLength = spectrum->getRawDataSize();

    return result;
}


PWIZ_API_DECL void SpectrumList_ABI_T2D::createIndex()
{
    const vector<bfs::path>& spectrumFilenames = t2d_data_->getSpectrumFilenames();
    for (size_t i=0; i < spectrumFilenames.size(); ++i)
    {
        index_.push_back(SpectrumIdentity());
        SpectrumIdentity& ie = index_.back();
        ie.index = index_.size()-1;
        ie.id = "file=" + encode_xml_id_copy(msd_.fileDescription.sourceFilePtrs[i]->id);
    }
    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_ABI_T2D

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_ABI_T2D::size() const {return 0;}
const SpectrumIdentity& SpectrumList_ABI_T2D::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_ABI_T2D::find(const std::string& id) const {return 0;}
SpectrumPtr SpectrumList_ABI_T2D::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_ABI_T2D::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI_T2D
