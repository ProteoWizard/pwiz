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


#include "SpectrumList_Mobilion.hpp"

#ifdef PWIZ_READER_MOBILION
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Reader_Mobilion_Detail.hpp"
#include <boost/spirit/include/karma.hpp>

using namespace pwiz::util;

namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Mobilion::SpectrumList_Mobilion(MSData& msd, const MBIFilePtr& rawdata, const Reader::Config& config)
    : msd_(msd), mbiFile_(rawdata), rawdata_(&mbiFile_->file), config_(config), lastFrame_(-1)
{
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Mobilion::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Mobilion::spectrumIdentity(size_t index) const
{
    if (index >= size_)
        throw runtime_error("[SpectrumList_Mobilion::spectrumIdentity()] Bad index: "
                            + lexical_cast<string>(index));
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Mobilion::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Mobilion::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Mobilion::spectrum(size_t index, DetailLevel detailLevel) const
{
    if (index >= size_)
        throw runtime_error("[SpectrumList_Mobilion::spectrum()] Bad index: "
                            + lexical_cast<string>(index));

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Mobilion::spectrum()] Allocation error.");

    IndexEntry& ie = index_[index];

    result->index = ie.index;
    result->id = ie.id;

    //if (lastFrame_ > -1 && !config_.combineIonMobilitySpectra && ie.frame != lastFrame_)
        //rawdata_->GetFrame(lastFrame_)->Unload();

    auto frame = rawdata_->GetFrame(ie.frame);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    int msLevel = frame->GetCE(0) > 0 ? 2 : 1;
    CVID spectrumType = msLevel == 2 ? MS_MSn_spectrum : MS_MS1_spectrum;

    result->set(spectrumType);
    result->set(MS_ms_level, msLevel);

    double scanStartTimeInSeconds = frame->Time();
    scan.set(MS_scan_start_time, scanStartTimeInSeconds, UO_second);

    if (detailLevel == DetailLevel_InstantMetadata)
        return result;

    auto fileMetadata = rawdata_->GetGlobalMetaData();
    auto frameMetadata = frame->GetFrameMetaData();

    result->set(translatePolarity(frameMetadata->ReadString(MBISDK::MBIAttr::FrameKey::FRM_POLARITY)));
    result->set(MS_profile_spectrum);
    
    /*result->set(MS_base_peak_m_z, binaryDataSource.lock()->GetScanStat<double>(ie.function, scan, MassLynxScanItem::BASE_PEAK_MASS));
    result->set(MS_base_peak_intensity, binaryDataSource.lock()->GetScanStat<double>(ie.function, scan, MassLynxScanItem::BASE_PEAK_INTENSITY));
    result->defaultArrayLength = binaryDataSource.lock()->GetScanStat<int>(ie.function, scan, MassLynxScanItem::PEAKS_IN_SCAN);*/

    result->set(MS_total_ion_current, frame->GetFrameTotalIntensity());

    // CONSIDER: is there a better way to get minMZ?
    double minMZ = 0, maxMZ = fileMetadata->ReadDouble(MBISDK::MBIAttr::GlobalKey::ADC_MASS_SPEC_RANGE);
    scan.scanWindows.push_back(ScanWindow(minMZ, maxMZ, MS_m_z));

    if (detailLevel < DetailLevel_FastMetadata)
        return result;

    if (!config_.combineIonMobilitySpectra)
    {
        double driftTime = frame->GetArrivalBinTimeOffset(ie.scan);
        scan.set(MS_ion_mobility_drift_time, driftTime, UO_millisecond);
    }

    if (msLevel > 1)
    {
        Precursor precursor;
        double midMZ = (maxMZ + minMZ) / 2;
        precursor.isolationWindow.set(MS_isolation_window_upper_offset, maxMZ - midMZ, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_lower_offset, maxMZ - midMZ, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, midMZ, MS_m_z);
        
        precursor.activation.set(MS_beam_type_collision_induced_dissociation); // AFAIK there is no Agilent QTOF instrument with a trap collision cell

        double collisionEnergy = frame->GetCollisionEnergy();
        if (collisionEnergy > 0)
            precursor.activation.set(MS_collision_energy, collisionEnergy, UO_electronvolt);

        SelectedIon selectedIon(midMZ);
        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);
    }

    if (detailLevel < DetailLevel_FullMetadata)
        return result;

    BinaryData<double> mzArray, intensityArray;

    if (config_.combineIonMobilitySpectra)
    {
        auto mobilityArray = boost::make_shared<BinaryDataArray>();
        getCombinedSpectrumData(*frame, mzArray, intensityArray, mobilityArray->data);

        result->defaultArrayLength = mzArray.size();
        if (detailLevel == DetailLevel_FullMetadata)
            return result;

        result->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts); // Donate mass and intensity buffers to result vectors

        mobilityArray->set(MS_raw_ion_mobility_array, "", UO_millisecond);
        result->binaryDataArrayPtrs.push_back(mobilityArray);
    }
    else
    {
        std::vector<size_t> intensities;

        if (config_.ignoreZeroIntensityPoints)
        {
            std::vector<double> mz;
            if (!frame->GetScanDataMzIndexedSparse(ie.scan, &mz, &intensities))
                throw runtime_error("[SpectrumList_MBI::spectrum] error getting scan data");
            swap(mz, mzArray);

            result->defaultArrayLength = mzArray.size();
            if (detailLevel == DetailLevel_FullMetadata)
                return result;

            intensityArray.resize(mzArray.size());
            auto intensityArrayItr = intensityArray.begin();
            for (const auto& intensity : intensities)
            {
                *intensityArrayItr++ = intensity;
            }
        }
        else
        {
            std::vector<int64_t> tofSampleIndices;
            auto tofCalibration = frame->GetCalibration();
            frame->GetScanDataToFIndexedSparse(ie.scan, &tofSampleIndices, &intensities);

            const size_t totalPoints = intensities.size() * 3;
            if (totalPoints == 0)
                return result;

            mzArray.resize(totalPoints);
            intensityArray.resize(totalPoints);
            auto mzItr = mzArray.begin();
            auto intensityItr = intensityArray.begin();

            // add first bounding zero and first data point
            *mzItr = tofCalibration.IndexToMz(tofSampleIndices[0] - 1);
            *intensityItr = 0;
            ++mzItr, ++intensityItr;
            *mzItr = tofCalibration.IndexToMz(tofSampleIndices[0]);
            *intensityItr = intensities[0];
            ++mzItr, ++intensityItr;

            size_t actualPoints = 3;
            for (size_t i = 1; i < intensities.size(); ++i)
            {
                // add bounding zeros if current TOF sample is more than one away from the last TOF sample
                if (tofSampleIndices[i] - tofSampleIndices[i - 1] > 1)
                {
                    *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i - 1] + 1);
                    *intensityItr = 0;
                    ++mzItr, ++intensityItr, ++actualPoints;

                    *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i] - 1);
                    *intensityItr = 0;
                    ++mzItr, ++intensityItr, ++actualPoints;
                }

                *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i]);
                *intensityItr = intensities[i];
                ++mzItr, ++intensityItr, ++actualPoints;
            }

            // add final bounding zero
            *mzItr = tofCalibration.IndexToMz(tofSampleIndices.back() + 1);
            *intensityItr = 0;

            mzArray.resize(actualPoints);
            intensityArray.resize(actualPoints);

            result->defaultArrayLength = actualPoints;
            if (detailLevel == DetailLevel_FullMetadata)
                return result;
        }
        result->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts); // Donate mass and intensity buffers to result vectors
    }
    return result;
}


PWIZ_API_DECL bool SpectrumList_Mobilion::hasIonMobility() const
{
    return true;
}

PWIZ_API_DECL bool SpectrumList_Mobilion::hasCombinedIonMobility() const
{
    return config_.combineIonMobilitySpectra;
}

PWIZ_API_DECL bool SpectrumList_Mobilion::canConvertIonMobilityAndCCS() const
{
    try
    {
        rawdata_->GetEyeOnCCSCalibration().GetAtSurf();
        return true;
    }
    catch (exception& e)
    {
        warn_once(("[SpectrumList_Mobilion::canConvertIonMobilityAndCCS] unable to convert between DT and CCS: " + string(e.what())).c_str());
        return false;
    }
}

PWIZ_API_DECL double SpectrumList_Mobilion::ionMobilityToCCS(double ionMobility, double mz, int charge) const
{
    return rawdata_->GetEyeOnCCSCalibration().ArrivalTimeToCCS(ionMobility, fabs(mz*(double)charge));
}

PWIZ_API_DECL double SpectrumList_Mobilion::ccsToIonMobility(double ccs, double mz, int charge) const
{
    auto calibration = rawdata_->GetEyeOnCCSCalibration();
    if (ccs < calibration.GetCCSMinimum() || ccs > calibration.GetCCSMaximum())
        return numeric_limits<double>::quiet_NaN();
    return  calibration.CCSToArrivalTime(ccs, fabs(mz * (double)charge));
}

PWIZ_API_DECL void SpectrumList_Mobilion::getCombinedSpectrumData(Frame& frame, BinaryData<double>& mz, BinaryData<double>& intensity, BinaryData<double>& driftTime) const
{
    auto frameData = frame.GetFrameDataAsCOOArray();
    const auto& intensities = frameData.data;
    const auto& tofSampleIndices = frameData.columnIndices;
    const auto& scanIndices = frameData.rowIndices;
    auto tofCalibration = frame.GetCalibration();

    if (config_.ignoreZeroIntensityPoints)
    {
        const size_t totalPoints = intensities.size();
        mz.resize(totalPoints);
        intensity.resize(totalPoints);
        driftTime.resize(totalPoints);
        for (size_t i = 0; i < totalPoints; ++i)
        {
            mz[i] = tofCalibration.IndexToMz(tofSampleIndices[i]);
            intensity[i] = intensities[i];
            driftTime[i] = frame.GetArrivalBinTimeOffset(scanIndices[i]);
        }
    }
    else
    {
        const size_t totalPoints = intensities.size() * 3;
        if (totalPoints == 0)
            return;

        mz.resize(totalPoints);
        intensity.resize(totalPoints);
        driftTime.resize(totalPoints);
        auto mzItr = mz.begin();
        auto intensityItr = intensity.begin();
        auto driftTimeItr = driftTime.begin();

        // add first bounding zero and first data point
        *mzItr = tofCalibration.IndexToMz(tofSampleIndices[0] - 1);
        *intensityItr = 0;
        *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices[0]);
        ++mzItr, ++intensityItr, ++driftTimeItr;
        *mzItr = tofCalibration.IndexToMz(tofSampleIndices[0]);
        *intensityItr = intensities[0];
        *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices[0]);
        ++mzItr, ++intensityItr, ++driftTimeItr;

        size_t actualPoints = 3;
        for (size_t i = 1; i < intensities.size(); ++i)
        {
            // add bounding zeros if current TOF sample is more than one away from the last TOF sample
            if (tofSampleIndices[i] - tofSampleIndices[i - 1] > 1)
            {
                *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i - 1] + 1);
                *intensityItr = 0;
                *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices[i - 1]);
                ++mzItr, ++intensityItr, ++driftTimeItr, ++actualPoints;

                *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i] - 1);
                *intensityItr = 0;
                *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices[i]);
                ++mzItr, ++intensityItr, ++driftTimeItr, ++actualPoints;
            }

            *mzItr = tofCalibration.IndexToMz(tofSampleIndices[i]);
            *intensityItr = intensities[i];
            *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices[i]);
            ++mzItr, ++intensityItr, ++driftTimeItr, ++actualPoints;
        }

        // add final bounding zero
        *mzItr = tofCalibration.IndexToMz(tofSampleIndices.back() + 1);
        *intensityItr = 0;
        *driftTimeItr = frame.GetArrivalBinTimeOffset(scanIndices.back());

        mz.resize(actualPoints);
        intensity.resize(actualPoints);
        driftTime.resize(actualPoints);
    }
}


PWIZ_API_DECL void SpectrumList_Mobilion::createIndex()
{
    using namespace boost::spirit::karma;

    for (size_t i = 1; i <= rawdata_->NumFrames(); ++i)
    {
        if (config_.combineIonMobilitySpectra)
        {
            index_.push_back(IndexEntry());
            IndexEntry& ie = index_.back();
            ie.frame = i;
            ie.index = index_.size() - 1;

            std::back_insert_iterator<std::string> sink(ie.id);
            generate(sink, "merged=" << int_ << " frame=" << int_,
                (ie.index + 1), ie.frame);
            idToIndexMap_[ie.id] = ie.index;
        }
        else
        {
            auto frame = rawdata_->GetFrame(i);
            for (int j : frame->GetNonZeroScanIndices())
            {
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.frame = i;
                ie.scan = j;
                ie.index = index_.size() - 1;

                std::back_insert_iterator<std::string> sink(ie.id);
                generate(sink, "frame=" << int_ << " scan=" << int_,
                    ie.frame, (ie.scan + 1));
                idToIndexMap_[ie.id] = ie.index;
            }
        }
    }

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

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_Mobilion::size() const {return 0;}
const SpectrumIdentity& SpectrumList_Mobilion::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_Mobilion::find(const std::string& id) const {return 0;}
bool SpectrumList_Mobilion::hasIonMobility() const {return false;}
bool SpectrumList_Mobilion::hasCombinedIonMobility() const {return false;}
bool SpectrumList_Mobilion::canConvertIonMobilityAndCCS() const {return false;}
double SpectrumList_Mobilion::ionMobilityToCCS(double ionMobility, double mz, int charge) const {return 0;}
double SpectrumList_Mobilion::ccsToIonMobility(double ccs, double mz, int charge) const {return 0;}
SpectrumPtr SpectrumList_Mobilion::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Mobilion::spectrum(size_t index, DetailLevel detailLevel) const { return SpectrumPtr(); }

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_MOBILION
