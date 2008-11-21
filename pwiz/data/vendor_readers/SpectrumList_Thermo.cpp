#define PWIZ_SOURCE

#ifndef PWIZ_NO_READER_THERMO
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"

using boost::format;

namespace pwiz {
namespace msdata {
namespace detail {


string scanNumberToSpectrumID(long scanNumber)
{
    return "S" + lexical_cast<string>(scanNumber); 
}


SpectrumList_Thermo::SpectrumList_Thermo(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile),
    size_(rawfile->value(NumSpectra)),
    spectrumCache_(size_),
    index_(size_)
{
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Thermo::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Thermo::find(const string& id) const
{
    try
    {
        size_t scanNumber = lexical_cast<size_t>(id);
        if (scanNumber>=1 && scanNumber<=size()) 
            return scanNumber-1;
    }
    catch (bad_lexical_cast&) {}

    return size();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::findNative(const string& nativeID) const
{
    return find(nativeID);
}


PWIZ_API_DECL const shared_ptr<const DataProcessing> SpectrumList_Thermo::dataProcessingPtr() const
{
    return msd_.dataProcessingPtrs[0]; // created by Reader_Thermo::fillInMetadata()
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
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // returned cached Spectrum if possible

    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate a new Spectrum

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Allocation error.");

    // get rawfile::ScanInfo and translate

    long scanNumber = static_cast<int>(index) + 1;
    auto_ptr<ScanInfo> scanInfo = rawfile_->getScanInfo(scanNumber);
    if (!scanInfo.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo.");

    result->index = index;
    result->id = scanNumberToSpectrumID(scanNumber);
    result->nativeID = lexical_cast<string>(scanNumber);

    if (scanInfo->ionizationType() == IonizationType_MALDI)
    {
        result->spotID += scanInfo->trailerExtraValue("Sample Position:");
        result->spotID += "," + scanInfo->trailerExtraValue("Fine Position:");
        result->spotID += "," + scanInfo->trailerExtraValue("Absolute X Position:");
        result->spotID += "x" + scanInfo->trailerExtraValue("Absolute Y Position:");
    }

    SpectrumDescription& sd = result->spectrumDescription;
    Scan& scan = sd.scan;

    MassAnalyzerType analyzerType = scanInfo->massAnalyzerType();
    scan.instrumentConfigurationPtr = 
        findInstrumentConfiguration(msd_, translate(analyzerType));

    string filterString = scanInfo->filter();
    scan.set(MS_filter_string, filterString);

    string scanEvent = scanInfo->trailerExtraValue("Scan Event:");
    if (!scanEvent.empty())
        scan.set(MS_preset_scan_configuration, scanEvent);

    result->set(MS_ms_level, scanInfo->msLevel());

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
        sd.set(MS_profile_mass_spectrum);
    }
    else
    {
        sd.set(MS_centroid_mass_spectrum); 
        doCentroid = scanInfo->isProfileScan();
    }

    scan.set(MS_scan_time, scanInfo->startTime(), UO_minute);
    sd.set(MS_base_peak_m_z, scanInfo->basePeakMass());
    sd.set(MS_base_peak_intensity, scanInfo->basePeakIntensity());
    sd.set(MS_total_ion_current, scanInfo->totalIonCurrent());

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
            isolationWidth = scanInfo->trailerExtraValueDouble(isolationWidthTag);
        }
        catch (RawEgg&)
        {}

        precursor.isolationWindow.set(MS_m_z, scanInfo->precursorMZ(i, false));
        precursor.isolationWindow.set(MS_isolation_width, isolationWidth);

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
        sd.precursors.push_back(precursor);
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
        sd.set(MS_lowest_m_z_value, massList->data()[0].mass);
        sd.set(MS_highest_m_z_value, massList->data()[massList->size()-1].mass);
    }

    if (getBinaryData)
    {
        result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(massList->data()), 
                                    massList->size());
    }

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


PWIZ_API_DECL void SpectrumList_Thermo::createIndex()
{
    for (size_t i=0; i<size_; i++)
    {
        SpectrumIdentity& si = index_[i];
        si.index = i;
        long scanNumber = (long)i+1;
        si.id = scanNumberToSpectrumID(scanNumber); 
        si.nativeID = lexical_cast<string>(scanNumber);
    }
}


PWIZ_API_DECL string SpectrumList_Thermo::findPrecursorID(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index>0)
    {
	    --index;
	    SpectrumPtr candidate = spectrum(index, false);
	    if (candidate->cvParam(MS_ms_level).valueAs<int>() == precursorMsLevel)
		    return candidate->id;
    }

    return "";
}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_NO_READER_THERMO
