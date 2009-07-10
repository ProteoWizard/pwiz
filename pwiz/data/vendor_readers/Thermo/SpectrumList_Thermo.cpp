//
// SpectrumList_Thermo.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#ifdef PWIZ_READER_THERMO
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include <boost/bind.hpp>


using boost::format;
using namespace pwiz::vendor_api::Thermo;


namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Thermo::SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile),
    size_(0),
    indexInitialized_(BOOST_ONCE_INIT)
{
    spectraByScanType.resize(ScanType_Count, 0);

    // calculate total spectra count from all controllers
    for (int controllerType = Controller_MS;
         controllerType <= Controller_UV;
         ++controllerType)
    {
        // some controllers don't have spectra (even if they have a NumSpectra value!)
        if (controllerType == Controller_Analog ||
            controllerType == Controller_UV)
            continue;

        long numControllers = rawfile_->getNumberOfControllersOfType((ControllerType) controllerType);
        for (long n=1; n <= numControllers; ++n)
        {
            rawfile_->setCurrentController((ControllerType) controllerType, n);
            long numSpectra = rawfile_->value(NumSpectra);
            for (long scan=1; scan <= numSpectra; ++scan)
            {
                ++spectraByScanType[rawfile_->getScanType(scan)];
            }
        }
    }

    size_ = spectraByScanType[ScanType_Full] +
            spectraByScanType[ScanType_Zoom] +
            spectraByScanType[ScanType_Q1MS] +
            spectraByScanType[ScanType_Q3MS];
}


PWIZ_API_DECL size_t SpectrumList_Thermo::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Thermo::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Thermo::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Thermo::find(const string& id) const
{
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Thermo::createIndex, this));
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


InstrumentConfigurationPtr findInstrumentConfiguration(const MSData& msd, CVID massAnalyzerType)
{
    if (msd.instrumentConfigurationPtrs.empty())
        return InstrumentConfigurationPtr();

    for (vector<InstrumentConfigurationPtr>::const_iterator it=msd.instrumentConfigurationPtrs.begin(),
         end=msd.instrumentConfigurationPtrs.end(); it!=end; ++it)
        if ((*it)->componentList.analyzer(0).hasCVParam(massAnalyzerType))
            return *it;

    return InstrumentConfigurationPtr();
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{ 
    boost::call_once(indexInitialized_, boost::bind(&SpectrumList_Thermo::createIndex, this));
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    IndexEntry& ie = index_[index];

    try
    {
        rawfile_->setCurrentController(ie.controllerType, ie.controllerNumber);
    }
    catch (RawEgg&)
    {
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error setting controller to: " +
                            lexical_cast<string>(ie.controllerType) + "," +
                            lexical_cast<string>(ie.controllerNumber));
    }

    // get rawfile::ScanInfo and translate
    ScanInfoPtr scanInfo = rawfile_->getScanInfo(ie.scan);
    if (!scanInfo.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo.");

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    try
    {
        result->scanList.set(MS_no_combination);
        result->scanList.scans.push_back(Scan());
        Scan& scan = result->scanList.scans[0];
        scan.set(MS_scan_start_time, scanInfo->startTime(), UO_minute);

        // special handling for non-MS scans
        if (ie.controllerType != Controller_MS)
        {
            result->set(MS_EMR_spectrum);
            
            result->set(MS_base_peak_m_z, scanInfo->basePeakMass(), UO_nanometer);
            result->set(MS_base_peak_intensity, scanInfo->basePeakIntensity());
            result->set(MS_total_ion_current, scanInfo->totalIonCurrent());

            scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass(), UO_nanometer));

            MassListPtr xyList = rawfile_->getMassList(ie.scan, "", Cutoff_None, 0, 0, false);
            result->defaultArrayLength = xyList->size();

            if (xyList->size() > 0)
            {
                result->set(MS_lowest_observed_wavelength, xyList->data()[0].mass, UO_nanometer);
                result->set(MS_highest_observed_wavelength, xyList->data()[xyList->size()-1].mass, UO_nanometer);
            }

            if (getBinaryData)
            {
                result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(xyList->data()), 
                                            xyList->size(), MS_number_of_counts);

                // replace "m/z array" term with "wavelength array"
                BinaryDataArray& xArray = *result->getMZArray();
                vector<CVParam>::iterator itr = std::find_if(xArray.cvParams.begin(),
                                                             xArray.cvParams.end(),
                                                             CVParamIs(MS_m_z_array));
                *itr = CVParam(MS_wavelength_array, "", UO_nanometer);
            }
            return result;
        }

        // MS scan metadata beyond this point
        if (scanInfo->ionizationType() == IonizationType_MALDI)
        {
            result->spotID += scanInfo->trailerExtraValue("Sample Position:");
            result->spotID += "," + scanInfo->trailerExtraValue("Fine Position:");
            result->spotID += "," + scanInfo->trailerExtraValue("Absolute X Position:");
            result->spotID += "x" + scanInfo->trailerExtraValue("Absolute Y Position:");
        }

        MassAnalyzerType analyzerType = scanInfo->massAnalyzerType();
        scan.instrumentConfigurationPtr = 
            findInstrumentConfiguration(msd_, translate(analyzerType));

        string filterString = scanInfo->filter();
        scan.set(MS_filter_string, filterString);

        string scanEvent = scanInfo->trailerExtraValue("Scan Event:");
        if (!scanEvent.empty())
            scan.set(MS_preset_scan_configuration, scanEvent);

        long msLevel = scanInfo->msLevel();
        ScanType scanType = scanInfo->scanType();

        scanMsLevelCache_[index] = msLevel;
        if (msLevel == -1) // precursor ion scan
            result->set(MS_precursor_ion_spectrum);
        else
        {
            result->set(MS_ms_level, msLevel);

            if (scanType!=ScanType_Unknown)
            {
                result->set(translateAsSpectrumType(scanType));
                if (scanType!=ScanType_Full)
                    result->set(translateAsScanningMethod(scanType));
            }
        }

        PolarityType polarityType = scanInfo->polarityType();
        if (polarityType!=PolarityType_Unknown) result->set(translate(polarityType));

        bool doCentroid = msLevelsToCentroid.contains(msLevel);

        if (scanInfo->isProfileScan() && !doCentroid)
        {
            result->set(MS_profile_spectrum);
        }
        else
        {
            result->set(MS_centroid_spectrum); 
            doCentroid = scanInfo->isProfileScan();
        }

        result->set(MS_base_peak_m_z, scanInfo->basePeakMass(), MS_m_z);
        result->set(MS_base_peak_intensity, scanInfo->basePeakIntensity(), MS_number_of_counts);
        result->set(MS_total_ion_current, scanInfo->totalIonCurrent());

        scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass(), MS_m_z));

        if (msLevel > 1)
        {
            try
            {
                double mzMonoisotopic = scanInfo->trailerExtraValueDouble("Monoisotopic M/Z:");
                scan.userParams.push_back(UserParam("[Thermo Trailer Extra]Monoisotopic M/Z:", 
                                                    lexical_cast<string>(mzMonoisotopic),
                                                    "xsd:float"));
            }
            catch (RawEgg&)
            {
            }
        }

        for (long i=0, precursorCount=scanInfo->precursorCount(); i<precursorCount; i++)
        {
            // Note: we report what RawFile gives us, which comes from the filter string;
            // we can look in the trailer extra values for better (but still unreliable) 
            // info.  Precursor recalculation should be done outside the Reader.

            Precursor precursor;
            Product product;
            SelectedIon selectedIon;

            // isolationWindow

            double isolationWidth = 0;

            try
            {
                string isolationWidthTag = "MS" + lexical_cast<string>(msLevel) + " Isolation Width:";
                isolationWidth = scanInfo->trailerExtraValueDouble(isolationWidthTag) / 2;
            }
            catch (RawEgg&)
            {}

            double isolationMz = scanInfo->precursorMZ(i, false);
            if (msLevel == -1) // precursor ion scan
            {
                product.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                product.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
                product.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);
            }
            else
            {
                precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth/2, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth/2, MS_m_z);
            }

            // TODO: better test here for data dependent modes
            if ((scanType==ScanType_Full || scanType==ScanType_Zoom ) && msLevel > 1)
                precursor.spectrumID = index_[findPrecursorSpectrumIndex(msLevel-1, index)].id;

            selectedIon.set(MS_selected_ion_m_z, scanInfo->precursorMZ(i), MS_m_z);
            long precursorCharge = scanInfo->precursorCharge();
            if (precursorCharge > 0)
                selectedIon.set(MS_charge_state, precursorCharge);
            // TODO: determine precursor intensity? (parentEnergy is not precursor intensity!)

            ActivationType activationType = scanInfo->activationType();
            if (activationType == ActivationType_Unknown)
                activationType = ActivationType_CID; // assume CID
            precursor.activation.set(translate(activationType));
            if (activationType == ActivationType_CID || activationType == ActivationType_HCD)
                precursor.activation.set(MS_collision_energy, scanInfo->precursorActivationEnergy(i), UO_electronvolt);

            precursor.selectedIons.push_back(selectedIon);
            result->precursors.push_back(precursor);
            if (msLevel == -1)
                result->products.push_back(product);
        }

        MassListPtr massList;

        if (doCentroid &&
            (analyzerType == MassAnalyzerType_Orbitrap ||
             analyzerType == MassAnalyzerType_FTICR))
        {
            // use label data for accurate centroids on FT profile data
            massList = rawfile_->getMassListFromLabelData(ie.scan);
        }
        else
        {
            massList = rawfile_->getMassList(ie.scan, "", Cutoff_None, 0, 0, doCentroid);
        }

        result->defaultArrayLength = massList->size();

        if (massList->size() > 0)
        {
            result->set(MS_lowest_observed_m_z, massList->data()[0].mass, MS_m_z);
            result->set(MS_highest_observed_m_z, massList->data()[massList->size()-1].mass, MS_m_z);
        }

        if (getBinaryData)
        {
            result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(massList->data()), 
                                        massList->size(), MS_number_of_counts);
        }

        return result;
    }
    catch (RawEgg&)
    {
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving spectrum \"" + result->id + "\"");
    }
}


PWIZ_API_DECL void SpectrumList_Thermo::createIndex() const
{
    scanMsLevelCache_.resize(size_, 0);
    index_.reserve(size_);

    for (int controllerType = Controller_MS;
         controllerType <= Controller_UV;
         ++controllerType)
    {
        // some controllers don't have spectra (even if they have a NumSpectra value!)
        if (controllerType == Controller_Analog ||
            controllerType == Controller_UV)
            continue;

        long numControllers = rawfile_->getNumberOfControllersOfType((ControllerType) controllerType);
        for (long n=1; n <= numControllers; ++n)
        {
            rawfile_->setCurrentController((ControllerType) controllerType, n);
            switch (controllerType)
            {
                case Controller_MS:
                {
                    for (long scan=1, last=rawfile_->value(NumSpectra); scan <= last; ++scan)
                    {
                        switch (rawfile_->getScanType(scan))
                        {
                            // skip chromatogram-centric scan types
                            case ScanType_SIM:
                            case ScanType_SRM:
                                continue;
                        }

                        index_.push_back(IndexEntry());
                        IndexEntry& ie = index_.back();
                        ie.controllerType = (ControllerType) controllerType;
                        ie.controllerNumber = n;
                        ie.scan = scan;
                        ie.index = index_.size()-1;

                        ostringstream oss;
                        oss << "controllerType=" << ie.controllerType <<
                               " controllerNumber=" << ie.controllerNumber <<
                               " scan=" << ie.scan;
                        ie.id = oss.str();
                    }
                }
                break;

                case Controller_PDA:
                {
                    for (long scan=1, last=rawfile_->value(NumSpectra); scan <= last; ++scan)
                    {
                        index_.push_back(IndexEntry());
                        IndexEntry& ie = index_.back();
                        ie.controllerType = (ControllerType) controllerType;
                        ie.controllerNumber = n;
                        ie.scan = scan;
                        ie.index = index_.size()-1;

                        ostringstream oss;
                        oss << "controllerType=" << ie.controllerType <<
                               " controllerNumber=" << ie.controllerNumber <<
                               " scan=" << ie.scan;
                        ie.id = oss.str();
                    }
                    break;
                }
            }
        }
    }
}


PWIZ_API_DECL size_t SpectrumList_Thermo::findPrecursorSpectrumIndex(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index > 0)
    {
	    --index;
        int& cachedMsLevel = scanMsLevelCache_[index];
        if (cachedMsLevel == 0)
        {
            // populate the missing MS level
            ScanInfoPtr scanInfo = rawfile_->getScanInfo(index_[index].scan);
	        cachedMsLevel = scanInfo->msLevel();
        }
        if (cachedMsLevel == precursorMsLevel)
            return index;
    }

    return size_;
}


} // detail
} // msdata
} // pwiz


#endif // PWIZ_READER_THERMO

