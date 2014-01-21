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


namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Agilent::SpectrumList_Agilent(const MSData& msd, MassHunterDataPtr rawfile)
:   msd_(msd),
    rawfile_(rawfile),
    size_(0),
    indexInitialized_(util::init_once_flag_proxy)
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
        try
        {
            size_t scanNumber = lexical_cast<size_t>(id::value(id, "scan"));
            if (scanNumber>=1 && scanNumber<=size())
                return scanNumber-1;
        }
        catch (bad_lexical_cast&) {}
    }

    return size();
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

    ScanRecordPtr scanRecordPtr = rawfile_->getScanRecord(ie.rowNumber);
    MSScanType scanType = scanRecordPtr->getMSScanType();
    int msLevel = scanRecordPtr->getMSLevel();

    result->set(translateAsPolarityType(scanRecordPtr->getIonPolarity()));
    result->set(MS_ms_level, msLevel);
    result->set(translateAsSpectrumType(scanType));

    result->set(MS_base_peak_m_z, scanRecordPtr->getBasePeakMZ(), MS_m_z);
    result->set(MS_base_peak_intensity, rawfile_->getBpcIntensities()[ie.rowNumber], MS_number_of_detector_counts);
    result->set(MS_total_ion_current, rawfile_->getTicIntensities()[ie.rowNumber], MS_number_of_detector_counts);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;
    scan.set(MS_scan_start_time, rawfile_->getTicTimes()[ie.rowNumber], UO_minute);

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

        precursor.activation.set(MS_CID); // MSDR provides no access to this, so assume CID
        precursor.activation.set(MS_collision_energy, scanRecordPtr->getCollisionEnergy(), UO_electronvolt);

        result->precursors.push_back(precursor);
        if (scanType == MSScanType_PrecursorIon)
            result->products.push_back(product);
    }

    bool reportMS2ForAllIonsScan = false; // watching out for MS1 with nonzero collision energy - return as an MS2 with a huge window
    if (1==msLevel)
    { 
        double collisionenergy = scanRecordPtr->getCollisionEnergy();
        if (collisionenergy > 0)
        {
            // all-ions scan - report it as MS2 with a single precursor and a huge selection window
            msLevel = 2;
            result->set(MS_ms_level, msLevel);
            reportMS2ForAllIonsScan = true;
            Precursor precursor;
            precursor.activation.set(MS_CID); // MSDR provides no access to this, so assume CID
            precursor.activation.set(MS_collision_energy, collisionenergy, UO_electronvolt);
            // note: can't give isolationWindow or precursor.selectedIons at (detailLevel <  DetailLevel_FullMetadata)
            result->precursors.push_back(precursor);
        }
    }

    // past this point the full spectrum is required
    if ((int) detailLevel < (int) DetailLevel_FullMetadata)
        return result;

    // MHDAC doesn't support centroiding of non-TOF spectra
    DeviceType deviceType = rawfile_->getDeviceType();
    bool canCentroid = deviceType != DeviceType_Quadrupole &&
                       deviceType != DeviceType_TandemQuadrupole;

    bool doCentroid = canCentroid && msLevelsToCentroid.contains(msLevel);

    pwiz::vendor_api::Agilent::SpectrumPtr spectrumPtr;
    if (doCentroid)
        spectrumPtr = rawfile_->getPeakSpectrumByRow(ie.rowNumber);
    else
        spectrumPtr = rawfile_->getProfileSpectrumByRow(ie.rowNumber);

    MassRange minMaxMz = spectrumPtr->getMeasuredMassRange();
    scan.scanWindows.push_back(ScanWindow(minMaxMz.start, minMaxMz.end, MS_m_z));
    
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

    MSStorageMode storageMode = spectrumPtr->getMSStorageMode();
    bool hasProfile = storageMode == MSStorageMode_ProfileSpectrum;

    if (hasProfile && (!canCentroid || !doCentroid))
    {
        result->set(MS_profile_spectrum);
    }
    else
    {
        result->set(MS_centroid_spectrum); 
        doCentroid = hasProfile && canCentroid;
    }

    automation_vector<double> xArray;
    spectrumPtr->getXArray(xArray);

    automation_vector<float> yArray;
    spectrumPtr->getYArray(yArray);

    if (detailLevel == DetailLevel_FullData)
    {
        result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);

        vector<double>& mzArray = result->getMZArray()->data;
        vector<double>& intensityArray = result->getIntensityArray()->data;

        if (doCentroid)
            result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum

        if (doCentroid || xArray.size() < 3)
        {
            mzArray.assign(xArray.begin(), xArray.end());
            intensityArray.assign(yArray.begin(), yArray.end());
        }
        else
        {
            // Agilent profile mode data returns all zero-intensity samples, so we filter out
            // samples that aren't adjacent to a non-zero-intensity sample value.

            mzArray.reserve(xArray.size() / 2);
            intensityArray.reserve(xArray.size() / 2);

            size_t index=0;
            size_t lastIndex = yArray.size();
            while ((index < lastIndex) && (0==yArray[index])) index++; // look for first nonzero value

            if (index < lastIndex) // we have at least one nonzero value
            {
                if (index>0)
                {
                    mzArray.push_back(xArray[index-1]);
                    intensityArray.push_back(0);
                }
                mzArray.push_back(xArray[index]);
                intensityArray.push_back(yArray[index]);
                index++;

                while ( index < lastIndex )
                {
                    if (0 != yArray[index])
                    {
                        mzArray.push_back(xArray[index]);
                        intensityArray.push_back(yArray[index++]);
                    }
                    else // skip over a run of zeros if possible, preserving those adjacent to nonzeros
                    {
                        mzArray.push_back(xArray[index]);  // we're adjacent to a nonzero so save this one at least
                        intensityArray.push_back(0);
                        // now look for next nonzero value if any
                        size_t z = index+1;
                        float *y=&yArray[index];
                        while (z<lastIndex && (0==*++y)) z++;
                        if (z < lastIndex )
                        {
                            if (z != index+1) // did we cover a run of zeros?
                            {
                                mzArray.push_back(xArray[z-1]); // write a single adjacent zero
                                intensityArray.push_back(0);
                            }
                            mzArray.push_back(xArray[z]); 
                            intensityArray.push_back(yArray[z]);
                        }
                        index = z+1;
                    }
                }
            }

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
        if (doCentroid || yArray.size() < 3)
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

            double lowestObservedMz = 0, highestObservedMz = 0;
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


PWIZ_API_DECL void SpectrumList_Agilent::createIndex() const
{
    MSScanType scanTypes = rawfile_->getScanTypes();

    // if any of these types are present, we enumerate each spectrum
    if (scanTypes & MSScanType_Scan ||
        scanTypes & MSScanType_ProductIon ||
        scanTypes & MSScanType_PrecursorIon)
    {
        int size = rawfile_->getTotalScansPresent();
        index_.reserve(size);

        for (size_t i=0, end = (size_t) size; i < end; ++i)
        {
            ScanRecordPtr scanRecordPtr = rawfile_->getScanRecord(i);
            MSScanType scanType = scanRecordPtr->getMSScanType();

            // these spectra are chromatogram-centric
            if (scanType == MSScanType_SelectedIon ||
                scanType == MSScanType_TotalIon ||
                scanType == MSScanType_MultipleReaction)
                continue;

            index_.push_back(IndexEntry());
            IndexEntry& ie = index_.back();
            ie.rowNumber = (int) i;
            ie.scanId = scanRecordPtr->getScanId();
            ie.index = index_.size()-1;

            ostringstream oss;
            oss << "scanId=" << ie.scanId;
            ie.id = oss.str();
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

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_AGILENT
