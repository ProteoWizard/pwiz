//
// SpectrumList_Agilent.cpp
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

#ifdef PWIZ_READER_AGILENT
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader_Agilent_Detail.hpp"
#include "SpectrumList_Agilent.hpp"
#include <boost/bind.hpp>


using boost::format;


namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Agilent::SpectrumList_Agilent(AgilentDataReaderPtr rawfile)
:   rawfile_(rawfile),
    size_(0),
    indexInitialized_(BOOST_ONCE_INIT)
{
}


PWIZ_API_DECL size_t SpectrumList_Agilent::size() const
{
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Agilent::createIndex, this));
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Agilent::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Agilent::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_Agilent::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Agilent::find(const string& id) const
{
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Agilent::createIndex, this));
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
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Agilent::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{ 
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Agilent::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_Agilent::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    IndexEntry& ie = index_[index];

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Agilent::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    ISpectrumPtr spectrumPtr = rawfile_->dataReaderPtr->GetSpectrum_6(index, NULL, NULL);
    MSScanType scanType = spectrumPtr->GetMSScanType();
    DeviceType deviceType = spectrumPtr->GetDeviceType();

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.set(MS_scan_start_time, rawfile_->ticTimes[index], UO_minute);
    scan.set(translateAsPolarityType(spectrumPtr->GetIonPolarity()));

    //MassAnalyzerType analyzerType = scanInfo->massAnalyzerType();
    //scan.instrumentConfigurationPtr = 
        //findInstrumentConfiguration(msd_, translate(analyzerType));

    int msLevel = translateAsMSLevel(scanType);
    if (msLevel == -1) // precursor ion scan
        result->set(MS_precursor_ion_spectrum);
    else
    {
        result->set(MS_ms_level, msLevel);
        result->set(translateAsSpectrumType(scanType));
    }

    // MHDAC doesn't support centroiding of non-TOF spectra
    bool canCentroid = deviceType != DeviceType_Quadrupole &&
                       deviceType != DeviceType_TandemQuadrupole;

    bool doCentroid = msLevelsToCentroid.contains(msLevel);
    bool hasProfile = spectrumPtr->GetMSStorageMode() == MSStorageMode_ProfileSpectrum ||
                      spectrumPtr->GetMSStorageMode() == MSStorageMode_Mixed;

    if (hasProfile && (!canCentroid || !doCentroid))
    {
        result->set(MS_profile_spectrum);
    }
    else
    {
        result->set(MS_centroid_spectrum); 
        doCentroid = hasProfile && canCentroid;
    }

    //result->set(MS_base_peak_m_z, scanInfo->basePeakMass(), MS_m_z);
    result->set(MS_base_peak_intensity, rawfile_->bpcIntensities[index], MS_number_of_counts);
    result->set(MS_total_ion_current, rawfile_->ticIntensities[index], MS_number_of_counts);

    IRangePtr minMaxMz = spectrumPtr->GetMeasuredMassRange();
    scan.scanWindows.push_back(ScanWindow(minMaxMz->Start, minMaxMz->End, MS_m_z));

    long precursorCount;
    SAFEARRAY* precursorMzSafeArray = spectrumPtr->GetPrecursorIon(&precursorCount);
    if (precursorCount > 0)
    {
        vector<double> precursorMZs;
        convertSafeArrayToVector(precursorMzSafeArray, precursorMZs);

        Precursor precursor;
        Product product;
        SelectedIon selectedIon;

        // isolationWindow

        if (msLevel == -1) // precursor ion scan
        {
            product.isolationWindow.set(MS_isolation_window_target_m_z, precursorMZs[0], MS_m_z);
            //product.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
            //product.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);
        }
        else
        {
            precursor.isolationWindow.set(MS_isolation_window_target_m_z, precursorMZs[0], MS_m_z);
            //precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
            //precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);
            
            long parentScanId;
            spectrumPtr->get_ParentScanId(&parentScanId);
            precursor.spectrumID = "scan=" + lexical_cast<string>(parentScanId);

            selectedIon.set(MS_selected_ion_m_z, precursorMZs[0], MS_m_z);
            precursor.selectedIons.push_back(selectedIon);
        }

        long precursorCharge;
        if (spectrumPtr->GetPrecursorCharge(&precursorCharge) == VARIANT_TRUE)
            selectedIon.set(MS_charge_state, precursorCharge);

        double precursorIntensity;
        if (spectrumPtr->GetPrecursorIntensity(&precursorIntensity) == VARIANT_TRUE)
            selectedIon.set(MS_intensity, precursorIntensity, MS_number_of_counts);

        precursor.activation.set(MS_CID); // MSDR provides no access to this, so assume CID
        precursor.activation.set(MS_collision_energy, spectrumPtr->CollisionEnergy, UO_electronvolt);

        result->precursors.push_back(precursor);
        if (msLevel == -1)
            result->products.push_back(product);
    }

    /*if (massList->size() > 0)
    {
        result->set(MS_lowest_observed_m_z, massList->data()[0].mass, MS_m_z);
        result->set(MS_highest_observed_m_z, massList->data()[massList->size()-1].mass, MS_m_z);
    }*/

    // if a centroided spectrum is desired, we make a new call
    if (doCentroid)
    {
        IPeakFilterPtr filterPtr(MSDR::CLSID_MsdrPeakFilter);
        filterPtr->AbsoluteThreshold = 1.0;
        spectrumPtr = rawfile_->dataReaderPtr->GetSpectrum_6(index, filterPtr, filterPtr);
    }

    if (getBinaryData)
    {
        result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
 
        vector<double>& mzArray = result->getMZArray()->data;
        convertSafeArrayToVector(spectrumPtr->xArray, mzArray);

        vector<float> intensityArray;
        convertSafeArrayToVector(spectrumPtr->yArray, intensityArray);
        result->getIntensityArray()->data.assign(intensityArray.begin(), intensityArray.end());

    }

    result->defaultArrayLength = (size_t) spectrumPtr->TotalDataPoints;

    return result;
}


PWIZ_API_DECL void SpectrumList_Agilent::createIndex() const
{
    MSScanType scanTypes = rawfile_->scanFileInfoPtr->ScanTypes;

    // if any of these types are present, we enumerate each spectrum
    if (scanTypes & MSScanType_Scan ||
        scanTypes & MSScanType_ProductIon ||
        scanTypes & MSScanType_PrecursorIon)
    {
        __int64 size = rawfile_->scanFileInfoPtr->TotalScansPresent;
        index_.reserve(size);

        for (size_t i=0, end = (size_t) size; i < end; ++i)
        {
            ISpectrumPtr spectrumPtr = rawfile_->dataReaderPtr->GetSpectrum_6(i, NULL, NULL);
            MSScanType scanType = spectrumPtr->GetMSScanType();

            // these spectra are chromatogram-centric
            if (scanType == MSScanType_SelectedIon ||
                scanType == MSScanType_TotalIon ||
                scanType == MSScanType_MultipleReaction)
                continue;

            index_.push_back(IndexEntry());
            IndexEntry& ie = index_.back();
            ie.scan = spectrumPtr->ScanId;
            ie.index = index_.size()-1;

            ostringstream oss;
            oss << "scan=" << ie.scan;
            ie.id = oss.str();
        }
    }

    size_ = index_.size();
}


} // detail
} // msdata
} // pwiz


#endif // PWIZ_READER_AGILENT

