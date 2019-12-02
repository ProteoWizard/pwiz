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


#include "SpectrumList_UIMF.hpp"


#ifdef PWIZ_READER_UIMF
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>

using namespace pwiz::util;

namespace pwiz {
namespace msdata {
namespace detail {

namespace UIMF = pwiz::vendor_api::UIMF;

SpectrumList_UIMF::SpectrumList_UIMF(const MSData& msd, UIMFReaderPtr rawfile, const Reader::Config& config)
:   msd_(msd),
    rawfile_(rawfile),
    config_(config),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy)
{
}


PWIZ_API_DECL size_t SpectrumList_UIMF::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UIMF::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_UIMF::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UIMF::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_UIMF::spectrumIdentity] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_UIMF::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UIMF::createIndex, this));

    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_UIMF::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_UIMF::spectrum(size_t index, DetailLevel detailLevel) const 
{ 
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UIMF::createIndex, this));
    if (index >= size_)
        throw runtime_error(("[SpectrumList_UIMF::spectrum] Bad index: " + lexical_cast<string>(index)).c_str());

    const IndexEntry& ie = index_[index];
    const UIMFReader::IndexEntry& rawIndexEntry = rawfile_->getIndex()[ie.index];

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_UIMF::spectrum] Allocation error.");

    result->index = index;
    result->id = ie.id;

    int msLevel = UIMFReader::getMsLevel(rawIndexEntry.frameType);
    CVID spectrumType = rawIndexEntry.frameType == FrameType_Calibration ? MS_calibration_spectrum : (msLevel == 1 ? MS_MS1_spectrum : MS_MSn_spectrum);
    result->set(MS_ms_level, msLevel);
    result->set(spectrumType);
    result->set(MS_profile_spectrum);

    //result->set(translateAsPolarityType(scanRecordPtr->getIonPolarity()));

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    //result->set(MS_base_peak_intensity, scanRecordPtr->getBasePeakIntensity(), MS_number_of_detector_counts);
    //result->set(MS_total_ion_current, scanRecordPtr->getTic(), MS_number_of_detector_counts);
    scan.set(MS_scan_start_time, rawfile_->getRetentionTime(rawIndexEntry.frame), UO_minute);
    scan.set(MS_ion_mobility_drift_time, rawfile_->getDriftTime(rawIndexEntry.frame, rawIndexEntry.scan), UO_millisecond);

    pair<double, double> scanWindow = rawfile_->getScanRange();
    scan.scanWindows.push_back(ScanWindow(scanWindow.first, scanWindow.second, MS_m_z));

    // NOTE: UIMF always represents DIA data, like Agilent all-ions
    if (msLevel > 1)
    {
        Precursor precursor;
        precursor.selectedIons.push_back(SelectedIon((scanWindow.first + scanWindow.second) / 2)); // add a fake selected ion to be backwards compatible with mzXML
        precursor.activation.set(MS_CID); // assume CID
        //precursor.activation.set(MS_collision_energy, scanRecordPtr->getCollisionEnergy(), UO_electronvolt);
        result->precursors.push_back(precursor);
    }

    // past this point the full spectrum is required
    if ((int) detailLevel < (int) DetailLevel_FullMetadata)
        return result;

    pwiz::util::BinaryData<double> mzArray, intensityArray;
    rawfile_->getScan(rawIndexEntry.frame, rawIndexEntry.scan, rawIndexEntry.frameType, mzArray, intensityArray, config_.ignoreZeroIntensityPoints);
    result->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts);

    return result;
}


PWIZ_API_DECL pwiz::analysis::Spectrum3DPtr SpectrumList_UIMF::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const
{
    pwiz::analysis::Spectrum3DPtr result(new pwiz::analysis::Spectrum3D);

    if (!rawfile_->hasIonMobility())
        return result;

    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_UIMF::createIndex, this));

    boost::container::flat_map<double, size_t>::const_iterator findItr = scanTimeToFrameMap_.lower_bound(floor(scanStartTime * 1e8)/1e8);
    if (findItr == scanTimeToFrameMap_.end() || findItr->first - 1e-8 > scanStartTime)
        return result;

    const vector<DriftScanInfoPtr> scans = rawfile_->getDriftScansForFrame(findItr->second);
    int driftBinsPerFrame = rawfile_->getMaxDriftScansPerFrame();
    (*result).reserve(driftBinsPerFrame);
    for (DriftScanInfoPtr scanInfo : scans)
    {
        if (driftTimeRanges.find(scanInfo->getDriftTime()) == driftTimeRanges.end())
            continue;

        boost::container::flat_map<double, float>& driftSpectrum = (*result)[scanInfo->getDriftTime()];
        size_t numDataPoints = (size_t) scanInfo->getNonZeroCount();
        pwiz::util::BinaryData<double> mzArray, intensityArray;
        rawfile_->getScan(scanInfo->getFrameNumber(), scanInfo->getDriftScanNumber(), scanInfo->getFrameType(), mzArray, intensityArray, true);
        driftSpectrum.reserve(numDataPoints);
        for (size_t i = 0; i < numDataPoints; ++i)
            driftSpectrum[mzArray[i]] = intensityArray[i];
    }
    return result;
}


PWIZ_API_DECL bool SpectrumList_UIMF::hasIonMobility() const
{
    return rawfile_->hasIonMobility();
}


PWIZ_API_DECL bool SpectrumList_UIMF::canConvertIonMobilityAndCCS() const
{
    return rawfile_->canConvertIonMobilityAndCCS();
}

PWIZ_API_DECL bool SpectrumList_UIMF::hasCombinedIonMobility() const
{
    return false;
}


PWIZ_API_DECL double SpectrumList_UIMF::ionMobilityToCCS(double driftTime, double mz, int charge) const
{
    return rawfile_->ionMobilityToCCS(driftTime, mz, charge);
}


PWIZ_API_DECL double SpectrumList_UIMF::ccsToIonMobility(double ccs, double mz, int charge) const
{
    return rawfile_->ccsToIonMobility(ccs, mz, charge);
}


PWIZ_API_DECL void SpectrumList_UIMF::createIndex() const
{
    using namespace boost::spirit::karma;

    size_t frames = rawfile_->getFrameCount();
    size_t size = rawfile_->getIndex().size(); //config_.combineIonMobilitySpectra ? frames : frames * driftBinsPerFrame;
	index_.reserve(size);
    scanTimeToFrameMap_.reserve(frames);

    for (int i = 1; i <= frames; ++i)
        scanTimeToFrameMap_[rawfile_->getRetentionTime(i)] = i;

    /*if (config_.combineIonMobilitySpectra)
    {
        index_.push_back(IndexEntry());
        IndexEntry& ie = index_.back();
        ie.rowNumber = ie.frameIndex = (int)i;
        ie.scanId = i + 1;
        ie.index = index_.size() - 1;

        std::back_insert_iterator<std::string> sink(ie.id);
        generate(sink, "scanId=" << int_, ie.scanId);
        idToIndexMap_[ie.id] = ie.index;
    }
    else*/
    {
        //if (config_.acceptZeroLengthSpectra)
        {
            const vector<UIMFReader::IndexEntry>& rawIndex = rawfile_->getIndex();
            for (int j=0; j < rawIndex.size(); ++j)
            {
                const UIMFReader::IndexEntry& rawIndexEntry = rawIndex[j];
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.index = j;

                std::back_insert_iterator<std::string> sink(ie.id);
                generate(sink, "frame=" << int_ << " scan=" << int_ << " frameType=" << int_, rawIndexEntry.frame, rawIndexEntry.scan, (int) rawIndexEntry.frameType);
                //generate(sink, int_ << "." << int_ << "." << int_, rawIndexEntry.frame, rawIndexEntry.scan, (int)rawIndexEntry.frameType);
                idToIndexMap_[ie.id] = ie.index;
            }
        }
    }

    size_ = index_.size();
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

namespace {const SpectrumIdentity emptyIdentity;}

PWIZ_API_DECL size_t SpectrumList_UIMF::size() const {return 0;}
PWIZ_API_DECL const SpectrumIdentity& SpectrumList_UIMF::spectrumIdentity(size_t index) const {return emptyIdentity;}
PWIZ_API_DECL size_t SpectrumList_UIMF::find(const std::string& id) const {return 0;}
PWIZ_API_DECL SpectrumPtr SpectrumList_UIMF::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
PWIZ_API_DECL SpectrumPtr SpectrumList_UIMF::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
//PWIZ_API_DECL pwiz::analysis::Spectrum3DPtr SpectrumList_UIMF::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const {return pwiz::analysis::Spectrum3DPtr();}
PWIZ_API_DECL bool SpectrumList_UIMF::hasCombinedIonMobility() const { return false; }

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_UIMF
