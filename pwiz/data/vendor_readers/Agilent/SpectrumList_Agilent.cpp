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


#include "SpectrumList_Agilent.hpp"


#ifdef PWIZ_READER_AGILENT
#include "Reader_Agilent_Detail.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Agilent;
namespace AgilentAPI = pwiz::vendor_api::Agilent;

SpectrumList_Agilent::SpectrumList_Agilent(const MSData& msd, MassHunterDataPtr rawfile, const Reader::Config& config)
:   msd_(msd),
    rawfile_(rawfile),
    config_(config),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy),
    lastFrame_(NULL),
    lastFrameIndex_(-1),
    lastRowNumber_(-1),
    lastScanRecord_(NULL)
{
}


PWIZ_API_DECL size_t SpectrumList_Agilent::size() const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Agilent::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Agilent::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Agilent::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_Agilent::spectrumIdentity] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Agilent::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Agilent::createIndex, this));
    try
    {
        size_t scanNumber = lexical_cast<size_t>(id);
        if (scanNumber>=1 && scanNumber<=size())
            return scanNumber-1;
    }
    catch (bad_lexical_cast&)
    {
        map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
        if (scanItr == idToIndexMap_.end())
            return checkNativeIdFindResult(size_, id);
        return scanItr->second;
    }

    return checkNativeIdFindResult(size_, id);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{ 
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Agilent::createIndex, this));
    if (index >= size_)
        throw runtime_error(("[SpectrumList_Agilent::spectrum] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    IndexEntry& ie = index_[index];

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Agilent::spectrum] Allocation error.");

    result->index = index;
    result->id = ie.id;

    if (lastRowNumber_ != ie.rowNumber)
        lastScanRecord_ = rawfile_->getScanRecord(lastRowNumber_ = ie.rowNumber); // equivalent to frameIndex
    ScanRecordPtr scanRecordPtr = lastScanRecord_;
    MSScanType scanType = scanRecordPtr->getMSScanType();
    DeviceType deviceType = rawfile_->getDeviceType();
    int msLevel = scanRecordPtr->getMSLevel();
    bool isIonMobilityScan = scanRecordPtr->getIsIonMobilityScan();
    CVID spectrumType = translateAsSpectrumType(scanType);

    result->set(translateAsPolarityType(scanRecordPtr->getIonPolarity()));

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    if (isIonMobilityScan)
    {
        //result->set(MS_base_peak_intensity, scanRecordPtr->getBasePeakIntensity(), MS_number_of_detector_counts);
        //result->set(MS_total_ion_current, scanRecordPtr->getTic(), MS_number_of_detector_counts);
        scan.set(MS_scan_start_time, scanRecordPtr->getRetentionTime(), UO_minute);
    }
    else
    {
        result->set(MS_base_peak_m_z, scanRecordPtr->getBasePeakMZ(), MS_m_z);
        result->set(MS_base_peak_intensity, rawfile_->getBpcIntensities()[ie.rowNumber], MS_number_of_detector_counts);
        result->set(MS_total_ion_current, rawfile_->getTicIntensities()[ie.rowNumber], MS_number_of_detector_counts);
        scan.set(MS_scan_start_time, rawfile_->getTicTimes()[ie.rowNumber], UO_minute);
    }

    double mzOfInterest = scanRecordPtr->getMZOfInterest();
    if (msLevel > 1 && mzOfInterest > 0)
    {
        Precursor precursor;
        Product product;
        SelectedIon selectedIon;

        // isolationWindow

        if (scanType == MSScanType_PrecursorIon)
        {
            product.isolationWindow.set(MS_isolation_window_target_m_z, mzOfInterest, MS_m_z);
            //product.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
            //product.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);
        }
        else
        {
            precursor.isolationWindow.set(MS_isolation_window_target_m_z, mzOfInterest, MS_m_z);
            //precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
            //precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);

            selectedIon.set(MS_selected_ion_m_z, mzOfInterest, MS_m_z);

            precursor.selectedIons.push_back(selectedIon);
        }

        CVID activationType = translateAsActivationType(deviceType);
        if (activationType == CVID_Unknown) // assume CID
            precursor.activation.set(MS_CID);
        else
            precursor.activation.set(activationType);
        precursor.activation.set(MS_collision_energy, scanRecordPtr->getCollisionEnergy(), UO_electronvolt);

        result->precursors.push_back(precursor);
        if (scanType == MSScanType_PrecursorIon)
            result->products.push_back(product);
    }

    bool reportMS2ForAllIonsScan = false; // watching out for MS1 with nonzero collision energy - return as an MS2 with a huge window
    if ((1==msLevel) || ((2==msLevel) && isIonMobilityScan)) // These are already declared MS2 in ion mobility data sets
    { 
        double collisionEnergy = scanRecordPtr->getCollisionEnergy();
        if (collisionEnergy > 0)
        {
            // all-ions scan - report it as MS2 with a single precursor and a huge selection window
            msLevel = 2;
            spectrumType = MS_MSn_spectrum;
            reportMS2ForAllIonsScan = true;
            Precursor precursor;

            CVID activationType = translateAsActivationType(deviceType);
            if (activationType == CVID_Unknown) // assume CID
                precursor.activation.set(MS_CID);
            else
                precursor.activation.set(activationType);
            precursor.activation.set(MS_collision_energy, collisionEnergy, UO_electronvolt);
            // note: can't give isolationWindow or precursor.selectedIons at (detailLevel <  DetailLevel_FullMetadata)
            result->precursors.push_back(precursor);
        }
    }
    result->set(MS_ms_level, msLevel);
    result->set(spectrumType);

    // past this point the full spectrum is required
    if ((int) detailLevel < (int) DetailLevel_FullMetadata)
        return result;

    AgilentAPI::DriftScanPtr driftScan;
    if (isIonMobilityScan)
    {
        if (ie.frameIndex != lastFrameIndex_)
            lastFrame_ = rawfile_->getIonMobilityFrame(lastFrameIndex_ = ie.frameIndex);
        if (config_.combineIonMobilitySpectra)
            driftScan = lastFrame_->getTotalScan();
        else
        {
            driftScan = lastFrame_->getScan(ie.driftBinIndex);
            scan.set(MS_ion_mobility_drift_time, driftScan->getDriftTime(), UO_millisecond);
        }
    }

    // MHDAC doesn't support centroiding of non-TOF spectra
    bool canCentroid = deviceType != DeviceType_Quadrupole &&
                       deviceType != DeviceType_TandemQuadrupole;

    bool doCentroid = canCentroid && msLevelsToCentroid.contains(msLevel);

    AgilentAPI::SpectrumPtr spectrumPtr;
    MassRange minMaxMz;
    if (!isIonMobilityScan)
    {
        if (doCentroid)
            spectrumPtr = rawfile_->getPeakSpectrumByRow(ie.rowNumber);
        else
            spectrumPtr = rawfile_->getProfileSpectrumByRow(ie.rowNumber);

        minMaxMz = spectrumPtr->getMeasuredMassRange();
        scan.scanWindows.push_back(ScanWindow(minMaxMz.start, minMaxMz.end, MS_m_z));
    }


    pwiz::util::BinaryData<double> xArray;
    pwiz::util::BinaryData<float> yArray;

    MSStorageMode storageMode;
    bool hasProfile;

    if (!isIonMobilityScan)
    {
        storageMode = spectrumPtr->getMSStorageMode();
        hasProfile = storageMode == MSStorageMode_ProfileSpectrum;

        spectrumPtr->getXArray(xArray);
        spectrumPtr->getYArray(yArray);
    }
    else
    {
        storageMode = driftScan->getMSStorageMode();
        hasProfile = storageMode == MSStorageMode_ProfileSpectrum;
        canCentroid = false;

        xArray = driftScan->getXArray();
        yArray = driftScan->getYArray();

        if (xArray.empty() || yArray.empty())
            return result;

        minMaxMz.start = xArray.front();
        minMaxMz.end = xArray.back();
        scan.scanWindows.push_back(ScanWindow(minMaxMz.start, minMaxMz.end, MS_m_z));
    }


    if (reportMS2ForAllIonsScan)
    {
        // claim a target window that encompasses all ions
        Precursor& precursor = result->precursors.back();
        double width = (minMaxMz.end-minMaxMz.start)*0.5;
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, minMaxMz.start+width, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_lower_offset, width, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_upper_offset, width, MS_m_z);
        // to avoid an empty list, claim a selected ion right in the middle of the window
        SelectedIon selectedIon;
        selectedIon.set(MS_selected_ion_m_z, minMaxMz.start+width, MS_m_z); // arbitrary choice, but known to be in range
        precursor.selectedIons.push_back(selectedIon);
    }
    else if (msLevel > 1 && mzOfInterest > 0)
    {
        Precursor& precursor = result->precursors.back();
        SelectedIon& selectedIon = precursor.selectedIons.back();

        int parentScanId = spectrumPtr->getParentScanId();
        if (parentScanId > 0)
            precursor.spectrumID = "scanId=" + lexical_cast<string>(parentScanId);

        int precursorCharge;
        if (spectrumPtr->getPrecursorCharge(precursorCharge) && precursorCharge > 0)
            selectedIon.set(MS_charge_state, precursorCharge);

        double precursorIntensity;
        if (spectrumPtr->getPrecursorIntensity(precursorIntensity) && precursorIntensity > 0)
            selectedIon.set(MS_peak_intensity, precursorIntensity, MS_number_of_detector_counts);
    }

    if (hasProfile && (!canCentroid || !doCentroid))
    {
        result->set(MS_profile_spectrum);
    }
    else
    {
        result->set(MS_centroid_spectrum);
        doCentroid = hasProfile && canCentroid;
    }

    if (detailLevel == DetailLevel_FullData)
    {
        result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);

        pwiz::util::BinaryData<double>& mzArray = result->getMZArray()->data;
        pwiz::util::BinaryData<double>& intensityArray = result->getIntensityArray()->data;

        if (doCentroid)
            result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum

        if (doCentroid || xArray.size() < 3)
        {
            mzArray = xArray;
            intensityArray.assign(yArray.begin(), yArray.end());
        }
        else
        {
            // Agilent profile mode data returns all zero-intensity samples, so we filter out
            // samples that aren't adjacent to a non-zero-intensity sample value.

            mzArray.resize(xArray.size());
            intensityArray.resize(xArray.size());
            double *mzPtr = &mzArray[0];
            double *intPtr = &intensityArray[0];
            double *xPtr = &xArray[0];
            float *yPtr = &yArray[0];

            size_t index=0;
            size_t lastIndex = yArray.size();
            while ((index < lastIndex) && (0==yPtr[index])) index++; // look for first nonzero value

            if (index < lastIndex) // we have at least one nonzero value
            {
                if (index>0)
                {
                    *mzPtr = xArray[index - 1]; ++mzPtr;
                    *intPtr = 0; ++intPtr;
                }
                *mzPtr = xPtr[index]; ++mzPtr;
                *intPtr = yPtr[index]; ++intPtr;
                index++;

                while ( index < lastIndex )
                {
                    if (0 != yPtr[index])
                    {
                        *mzPtr = xPtr[index]; ++mzPtr;
                        *intPtr = yPtr[index++]; ++intPtr;
                    }
                    else // skip over a run of zeros if possible, preserving those adjacent to nonzeros
                    {
                        *mzPtr = xPtr[index]; ++mzPtr; // we're adjacent to a nonzero so save this one at least
                        *intPtr = 0; ++intPtr;
                        // now look for next nonzero value if any
                        size_t z = index+1;
                        float *y = &yPtr[index];
                        while (z<lastIndex && (0==*++y)) z++;
                        if (z < lastIndex)
                        {
                            if (z != index+1) // did we cover a run of zeros?
                            {
                                *mzPtr = xPtr[z - 1]; ++mzPtr; // write a single adjacent zero
                                *intPtr = 0; ++intPtr;
                            }
                            *mzPtr = xPtr[z]; ++mzPtr;
                            *intPtr = yPtr[z]; ++intPtr;
                        }
                        index = z+1;
                    }
                }
            }
            size_t newcount = mzPtr - &mzArray[0];
            mzArray.resize(newcount);
            intensityArray.resize(newcount);
        }

        if (!mzArray.empty())
        {
            result->set(MS_lowest_observed_m_z, mzArray.front(), MS_m_z);
            result->set(MS_highest_observed_m_z, mzArray.back(), MS_m_z);
        }
        result->defaultArrayLength = mzArray.size();
    }
    else
    {
        if (doCentroid || xArray.size() < 3)
        {
            result->defaultArrayLength = (size_t) yArray.size();

            if (!xArray.empty())
            {
                result->set(MS_lowest_observed_m_z, xArray.front(), MS_m_z);
                result->set(MS_highest_observed_m_z, xArray.back(), MS_m_z);
            }
        }
        else
        {
            // Agilent profile mode data returns all zero-intensity samples, so we filter out
            // samples that aren't adjacent to a non-zero-intensity sample value.

            double lowestObservedMz = numeric_limits<double>::max(), highestObservedMz = 0;
            result->defaultArrayLength = 0;

            // special case for the first sample
            if (yArray[0] > 0 || yArray[1] > 0)
            {
                ++result->defaultArrayLength;
                lowestObservedMz = xArray[0];
            }

            size_t lastIndex = yArray.size() - 1;

            for (size_t i=1; i < lastIndex; ++i)
                if (yArray[i-1] > 0 || yArray[i] > 0 || yArray[i+1] > 0)
                {
                    ++result->defaultArrayLength;
                    lowestObservedMz = min(xArray[i], lowestObservedMz);
                    highestObservedMz = max(xArray[i], highestObservedMz);
                }

            // special case for the last sample
            if (yArray[lastIndex-1] > 0 || yArray[lastIndex] > 0)
            {
                ++result->defaultArrayLength;
                highestObservedMz = xArray[lastIndex];
            }

            if (!xArray.empty())
            {
                result->set(MS_lowest_observed_m_z, lowestObservedMz, MS_m_z);
                result->set(MS_highest_observed_m_z, highestObservedMz, MS_m_z);
            }
        }
    }

    return result;
}


PWIZ_API_DECL pwiz::analysis::Spectrum3DPtr SpectrumList_Agilent::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const
{
    pwiz::analysis::Spectrum3DPtr result(new pwiz::analysis::Spectrum3D);

    if (!rawfile_->hasIonMobilityData())
        return result;

    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_Agilent::createIndex, this));

    boost::container::flat_map<double, size_t>::const_iterator findItr = scanTimeToFrameMap_.lower_bound(floor(scanStartTime * 1e8)/1e8);
    if (findItr == scanTimeToFrameMap_.end() || findItr->first - 1e-8 > scanStartTime)
        return result;

    FramePtr frame = rawfile_->getIonMobilityFrame(findItr->second);
    int driftBinsPerFrame = frame->getDriftBinsPresent();
    (*result).reserve(driftBinsPerFrame);
    for (int driftBinIndex = 0; driftBinIndex < driftBinsPerFrame; ++driftBinIndex)
    {
        DriftScanPtr driftScan = frame->getScan(driftBinIndex);
        if (driftTimeRanges.find(driftScan->getDriftTime()) == driftTimeRanges.end())
            continue;

        boost::container::flat_map<double, float>& driftSpectrum = (*result)[driftScan->getDriftTime()];
        size_t numDataPoints = (size_t) driftScan->getTotalDataPoints();
        const pwiz::util::BinaryData<double>& mzArray = driftScan->getXArray();
        const pwiz::util::BinaryData<float>& intensityArray = driftScan->getYArray();
        driftSpectrum.reserve(numDataPoints);
        for (size_t i = 0; i < numDataPoints; ++i)
            driftSpectrum[mzArray[i]] = intensityArray[i];
    }
    return result;
}


PWIZ_API_DECL bool SpectrumList_Agilent::hasIonMobility() const { return rawfile_->hasIonMobilityData(); }
PWIZ_API_DECL bool SpectrumList_Agilent::canConvertIonMobilityAndCCS() const { return rawfile_->canConvertDriftTimeAndCCS(); };
PWIZ_API_DECL double SpectrumList_Agilent::ionMobilityToCCS(double driftTime, double mz, int charge) const { return rawfile_->driftTimeToCCS(driftTime, mz, charge); }
PWIZ_API_DECL double SpectrumList_Agilent::ccsToIonMobility(double ccs, double mz, int charge) const { return rawfile_->ccsToDriftTime(ccs, mz, charge); }


PWIZ_API_DECL void SpectrumList_Agilent::createIndex() const
{
    using namespace boost::spirit::karma;

	bool hasIMS = rawfile_->hasIonMobilityData(); // enumerate all drift scans
	if (hasIMS)
    {
        int frames = rawfile_->getTotalIonMobilityFramesPresent();
        int driftBinsPerFrame = rawfile_->getIonMobilityFrame(0)->getDriftBinsPresent();
        size_t size = config_.combineIonMobilitySpectra ? frames : frames * driftBinsPerFrame;
		index_.reserve(size);
        scanTimeToFrameMap_.reserve(frames);

        for (int i = 0; i < frames; ++i)
		{
            FramePtr frame = rawfile_->getIonMobilityFrame(i);
            scanTimeToFrameMap_[frame->getRetentionTime() * 60.0] = i; // frames report RT in minutes as of MIDAC 8

            if (config_.combineIonMobilitySpectra)
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
            else
            {
                if (config_.acceptZeroLengthSpectra)
                {
                    for (int driftBinIndex = 0; driftBinIndex < driftBinsPerFrame; ++driftBinIndex)
                    {
                        /*if (scan->getScanId() == 0)
                        continue;*/ // BUG or empty bin?

                        index_.push_back(IndexEntry());
                        IndexEntry& ie = index_.back();
                        ie.rowNumber = ie.frameIndex = (int)i;
                        ie.driftBinIndex = driftBinIndex;
                        ie.scanId = (i * driftBinsPerFrame) + driftBinIndex; // scan->getScanId();
                        ie.index = index_.size() - 1;

                        std::back_insert_iterator<std::string> sink(ie.id);
                        generate(sink, "scanId=" << int_, ie.scanId);
                        idToIndexMap_[ie.id] = ie.index;
                    }
                }
                else
                {
                    const vector<short>& nonEmptyDriftBins = frame->getNonEmptyDriftBins();
                    for (size_t j = 0, end = nonEmptyDriftBins.size(); j < end; ++j)
			        {
                        int driftBinIndex = nonEmptyDriftBins[j];

                        // HACK: frame 25, bin 99 of Test_ShewFromUimf returns a null spectrum but still comes back as non-empty;
                        // if this happens more often we will need a fix from Agilent
                        if (j + 1 == end)
                            try { frame->getScan(driftBinIndex); } catch (runtime_error&) { continue; }

                        /*if (scan->getScanId() == 0)
                            continue;*/ // BUG or empty bin?

				        index_.push_back(IndexEntry());
				        IndexEntry& ie = index_.back();
				        ie.rowNumber = ie.frameIndex = (int)i;
                        ie.driftBinIndex = driftBinIndex;
                        ie.scanId = (i * driftBinsPerFrame) + driftBinIndex; // scan->getScanId();
                        ie.index = index_.size() - 1;

                        std::back_insert_iterator<std::string> sink(ie.id);
                        generate(sink, "scanId=" << int_, ie.scanId);
                        idToIndexMap_[ie.id] = ie.index;
			        }
                }
            }
		}
	}
	else
	{
		MSScanType scanTypes = rawfile_->getScanTypes();

		// if any of these types are present, we enumerate each spectrum
		if (scanTypes & MSScanType_Scan ||
			scanTypes & MSScanType_ProductIon ||
			scanTypes & MSScanType_PrecursorIon ||
            scanTypes & MSScanType_SelectedIon ||
            scanTypes & MSScanType_MultipleReaction)
		{
			int size = rawfile_->getTotalScansPresent();
			index_.reserve(size);

			for (size_t i = 0, end = (size_t)size; i < end; ++i)
			{
				ScanRecordPtr scanRecordPtr = rawfile_->getScanRecord(i);
				MSScanType scanType = scanRecordPtr->getMSScanType();

				// these spectra are chromatogram-centric
				if ((!config_.simAsSpectra && scanType == MSScanType_SelectedIon) ||
					(!config_.srmAsSpectra && scanType == MSScanType_MultipleReaction) ||
                    scanType == MSScanType_TotalIon)
					continue;

				index_.push_back(IndexEntry());
				IndexEntry& ie = index_.back();
				ie.rowNumber = (int)i;
				ie.scanId = scanRecordPtr->getScanId();
                ie.frameIndex = ie.driftBinIndex = 0;
                ie.index = index_.size() - 1;

                std::back_insert_iterator<std::string> sink(ie.id);
                generate(sink, "scanId=" << int_, ie.scanId);
                idToIndexMap_[ie.id] = ie.index;
			}
		}
	}

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_AGILENT

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

PWIZ_API_DECL size_t SpectrumList_Agilent::size() const {return 0;}
PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Agilent::spectrumIdentity(size_t index) const {return emptyIdentity;}
PWIZ_API_DECL size_t SpectrumList_Agilent::find(const std::string& id) const {return 0;}
PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
PWIZ_API_DECL pwiz::analysis::Spectrum3DPtr SpectrumList_Agilent::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const {return pwiz::analysis::Spectrum3DPtr();}
PWIZ_API_DECL bool SpectrumList_Agilent::hasIonMobility() const { return false; }
PWIZ_API_DECL bool SpectrumList_Agilent::canConvertIonMobilityAndCCS() const { return false; }
PWIZ_API_DECL double SpectrumList_Agilent::ionMobilityToCCS(double driftTime, double mz, int charge) const {return 0;}
PWIZ_API_DECL double SpectrumList_Agilent::ccsToIonMobility(double ccs, double mz, int charge) const {return 0;}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_AGILENT
