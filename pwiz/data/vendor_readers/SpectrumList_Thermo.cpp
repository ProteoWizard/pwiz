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


namespace pwiz {
namespace msdata {
namespace detail {


string scanNumberToSpectrumID(long scanNumber)
{
    return "controllerType=0 controllerNumber=1 scan=" + lexical_cast<string>(scanNumber); 
}


SpectrumList_Thermo::SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile),
    size_(rawfile->value(NumSpectra)),
    indexInitialized_(BOOST_ONCE_INIT)
{
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

    // get rawfile::ScanInfo and translate
    long scanNumber = static_cast<int>(index) + 1;
    auto_ptr<ScanInfo> scanInfo = rawfile_->getScanInfo(scanNumber);
    if (!scanInfo.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo.");

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Allocation error.");

    result->index = index;
    result->id = scanNumberToSpectrumID(scanNumber);

    if (scanInfo->ionizationType() == IonizationType_MALDI)
    {
        result->spotID += scanInfo->trailerExtraValue("Sample Position:");
        result->spotID += "," + scanInfo->trailerExtraValue("Fine Position:");
        result->spotID += "," + scanInfo->trailerExtraValue("Absolute X Position:");
        result->spotID += "x" + scanInfo->trailerExtraValue("Absolute Y Position:");
    }

    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];

    MassAnalyzerType analyzerType = scanInfo->massAnalyzerType();
    scan.instrumentConfigurationPtr = 
        findInstrumentConfiguration(msd_, translate(analyzerType));

    string filterString = scanInfo->filter();
    scan.set(MS_filter_string, filterString);

    string scanEvent = scanInfo->trailerExtraValue("Scan Event:");
    if (!scanEvent.empty())
        scan.set(MS_preset_scan_configuration, scanEvent);

    result->set(MS_ms_level, scanInfo->msLevel());
    scanMsLevelCache_[index] = scanInfo->msLevel();

    ScanType scanType = scanInfo->scanType();
    if (scanType!=ScanType_Unknown)
    {
        result->set(translateAsSpectrumType(scanType));
        scan.set(translateAsScanningMethod(scanType));
    }

    PolarityType polarityType = scanInfo->polarityType();
    if (polarityType!=PolarityType_Unknown) scan.set(translate(polarityType));

    bool doCentroid = msLevelsToCentroid.contains(scanInfo->msLevel());

    if (scanInfo->isProfileScan() && !doCentroid)
    {
        result->set(MS_profile_mass_spectrum);
    }
    else
    {
        result->set(MS_centroid_mass_spectrum); 
        doCentroid = scanInfo->isProfileScan();
    }

    scan.set(MS_scan_time, scanInfo->startTime(), UO_minute);
    result->set(MS_base_peak_m_z, scanInfo->basePeakMass());
    result->set(MS_base_peak_intensity, scanInfo->basePeakIntensity());
    result->set(MS_total_ion_current, scanInfo->totalIonCurrent());

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

    scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass()));

    for (long i=0, precursorCount=scanInfo->precursorCount(); i<precursorCount; i++)
    {
        // Note: we report what RawFile gives us, which comes from the filter string;
        // we can look in the trailer extra values for better (but still unreliable) 
        // info.  Precursor recalculation should be done outside the Reader.

        Precursor precursor;
        SelectedIon selectedIon;

        // isolationWindow

        double isolationWidth = 0;

        try
        {
            string isolationWidthTag = "MS" + lexical_cast<string>(scanInfo->msLevel()) + " Isolation Width:";
            isolationWidth = scanInfo->trailerExtraValueDouble(isolationWidthTag) / 2;
        }
        catch (RawEgg&)
        {}

        double isolationMz = scanInfo->precursorMZ(i, false);
        precursor.isolationWindow.set(MS_isolation_m_z_lower_limit, isolationMz - isolationWidth);
        precursor.isolationWindow.set(MS_isolation_m_z_upper_limit, isolationMz + isolationWidth);

        // TODO: better test here for data dependent modes
        if ((scanType==ScanType_Full || scanType==ScanType_Zoom ) && scanInfo->msLevel() > 1)
            precursor.spectrumID = findPrecursorID(scanInfo->msLevel()-1, index);

        selectedIon.set(MS_m_z, scanInfo->precursorMZ(i));
        long precursorCharge = scanInfo->precursorCharge();
        if (precursorCharge > 0)
            selectedIon.set(MS_charge_state, precursorCharge);
        // TODO: determine precursor intensity? (parentEnergy is not precursor intensity!)

        ActivationType activationType = scanInfo->activationType();
        if (activationType == ActivationType_Unknown)
            activationType = ActivationType_CID; // assume CID
        precursor.activation.set(translate(activationType));
        if (activationType == ActivationType_CID || activationType == ActivationType_HCD)
            precursor.activation.set(MS_collision_energy, scanInfo->precursorActivationEnergy(i));

        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);
    }

    MassListPtr massList;

    if (doCentroid &&
        (analyzerType == MassAnalyzerType_Orbitrap ||
         analyzerType == MassAnalyzerType_FTICR))
    {
        // use label data for accurate centroids on FT profile data
        massList = rawfile_->getMassListFromLabelData(scanNumber);
    }
    else
    {
        massList = rawfile_->getMassList(scanNumber, "", raw::Cutoff_None, 0, 0, doCentroid);
    }

    result->defaultArrayLength = massList->size();

    if (massList->size() > 0)
    {
        result->set(MS_lowest_m_z_value, massList->data()[0].mass);
        result->set(MS_highest_m_z_value, massList->data()[massList->size()-1].mass);
    }

    if (getBinaryData)
    {
        result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(massList->data()), 
                                    massList->size());
    }

    return result;
}


PWIZ_API_DECL void SpectrumList_Thermo::createIndex() const
{
    scanMsLevelCache_.resize(size_, 0);
    index_.resize(size_);
    for (size_t i=0; i<size_; i++)
    {
        SpectrumIdentity& si = index_[i];
        si.index = i;
        long scanNumber = (long)i+1;
        si.id = scanNumberToSpectrumID(scanNumber); 
    }
}


PWIZ_API_DECL string SpectrumList_Thermo::findPrecursorID(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index > 0)
    {
	    --index;
        int& cachedMsLevel = scanMsLevelCache_[index];
        if (cachedMsLevel == 0)
        {
            // populate the missing MS level
            auto_ptr<ScanInfo> scanInfo = rawfile_->getScanInfo(index+1);
	        cachedMsLevel = scanInfo->msLevel();
        }
        if (cachedMsLevel == precursorMsLevel)
		        return scanNumberToSpectrumID(index+1);
    }

    return "";
}


} // detail
} // msdata
} // pwiz


#endif // PWIZ_READER_THERMO

