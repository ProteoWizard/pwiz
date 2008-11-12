#define PWIZ_SOURCE

#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "Reader_Waters_Detail.hpp"
#include "SpectrumList_Waters.hpp"
#include <iostream>
#include <stdexcept>

using boost::format;

namespace
{

string convertBstrToString(const BSTR& bstring)
{
	_bstr_t bTmp(bstring);
	return string((const char *)bTmp);
}

}
namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Waters::SpectrumList_Waters(const MSData& msd, const string& rawpath)
:   msd_(msd), rawpath_(rawpath),
    size_(0), functionCount_(0),
    pFunctionInfo_(IDACFunctionInfoPtr(CLSID_DACFunctionInfo)),
    pScanStats_(IDACScanStatsPtr(CLSID_DACScanStats)),
    pExScanStats_(IDACExScanStatsPtr(CLSID_DACExScanStats)),
    pSpectrum_(IDACSpectrumPtr(CLSID_DACSpectrum))
{
    // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
    while (bfs::exists(bfs::path(rawpath_) / (format("_FUNC%03d.DAT") % (functionCount_+1)).str()))
        ++functionCount_;

    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Waters::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Waters::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Waters::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index].first;
}


PWIZ_API_DECL size_t SpectrumList_Waters::find(const string& id) const
{
    vector<string> tokens;
    bal::split(tokens, id, bal::is_any_of(","));
    if (tokens.size() != 2)
        return size();

    short function;
    long scan;
    try
    {
        function = lexical_cast<short>(bal::trim_left_copy_if(tokens[0], bal::is_any_of("S")));
        scan = lexical_cast<long>(tokens[1]);
    }
    catch (bad_lexical_cast&)
    {
        return size();
    }

    map<short, map<long, size_t> >::const_iterator funcItr = nativeIdToIndexMap_.find(function);
    if (funcItr == nativeIdToIndexMap_.end())
        return size();
    map<long, size_t>::const_iterator scanItr = funcItr->second.find(scan);
    if (scanItr == funcItr->second.end())
        return size();
    return scanItr->second;
}


PWIZ_API_DECL size_t SpectrumList_Waters::findNative(const string& nativeID) const
{
    vector<string> tokens;
    bal::split(tokens, nativeID, bal::is_any_of(","));
    if (tokens.size() != 2)
        return size();

    short function;
    long scan;
    try
    {
        function = lexical_cast<short>(tokens[0]);
        scan = lexical_cast<long>(tokens[1]);
    }
    catch (bad_lexical_cast&)
    {
        return size();
    }

    map<short, map<long, size_t> >::const_iterator funcItr = nativeIdToIndexMap_.find(function);
    if (funcItr == nativeIdToIndexMap_.end())
        return size();
    map<long, size_t>::const_iterator scanItr = funcItr->second.find(scan);
    if (scanItr == funcItr->second.end())
        return size();
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData) const
{
    if (index > size_)
        throw runtime_error(("[SpectrumList_Waters::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // returned cached Spectrum if possible
    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Waters::spectrum()] Allocation error.");


    const char* szRawpath = rawpath_.c_str();
    pair<SpectrumIdentity, pair<short, long> > indexPair = index_[index];
    SpectrumIdentity& si = indexPair.first;
    short functionNumber = indexPair.second.first;
    short processNumber = 0;
    long scanNumber = indexPair.second.second;
    const FunctionMetaData& fmd = functionToMetaDataMap_.find(functionNumber)->second;

    pFunctionInfo_->GetFunctionInfo(szRawpath, functionNumber);
    pScanStats_->GetScanStats(szRawpath, functionNumber, processNumber, scanNumber);
    pExScanStats_->GetExScanStats(szRawpath, functionNumber, processNumber, scanNumber);
    pSpectrum_->GetSpectrum(szRawpath, functionNumber, processNumber, scanNumber);

    result->index = si.index;
    result->id = si.id;
    result->nativeID = si.nativeID;

    float laserAimX = pExScanStats_->LaserAimXPos;
    float laserAimY = pExScanStats_->LaserAimYPos;
    //if (scanInfo->ionizationType() == IonizationType_MALDI)
    {
        result->spotID = (format("%dx%d") % laserAimX % laserAimY).str();
    }

    SpectrumDescription& sd = result->spectrumDescription;
    Scan& scan = sd.scan;

    //scan.instrumentConfigurationPtr = 
        //findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

    result->set(fmd.spectrumType);
    result->set(MS_ms_level, fmd.msLevel);
    scan.set(fmd.scanningMethod);
    scan.set(MS_preset_scan_configuration, functionNumber);
    scan.set(MS_scan_time, pScanStats_->RetnTime, UO_minute);

    //PolarityType polarityType = scanInfo->polarityType();
    //if (polarityType!=PolarityType_Unknown) scan.cvParams.push_back(translate(polarityType));

    //bool doCentroid = msLevelsToCentroid.contains(scanInfo->msLevel());

    //if (scanInfo->isProfileScan() && !doCentroid) sd.cvParams.push_back(MS_profile_mass_spectrum);
    //else sd.cvParams.push_back(MS_centroid_mass_spectrum); 

    if (pScanStats_->Continuum > 0)
        sd.set(MS_profile_mass_spectrum);
    else
        sd.set(MS_centroid_mass_spectrum);

    sd.set(MS_base_peak_m_z, pScanStats_->BPM);
    sd.set(MS_base_peak_intensity, pScanStats_->BPI);
    sd.set(MS_total_ion_current, pScanStats_->TIC);

    // TODO: get correct values
    scan.scanWindows.push_back(ScanWindow(pScanStats_->LoMass, pScanStats_->HiMass));

    //sd.set(MS_lowest_m_z_value, minObservedMz);
    //sd.set(MS_highest_m_z_value, maxObservedMz);

    float precursorMz = pExScanStats_->SetMass;

    if (precursorMz > 0)
    {
        Precursor precursor;
        SelectedIon selectedIon;

        selectedIon.set(MS_m_z, precursorMz);

        /*long parentCharge = scanInfo->parentCharge();
        if (parentCharge > 0)
            selectedIon.cvParams.push_back(CVParam(MS_charge_state, parentCharge));*/

        // TODO: get correct activation type
        precursor.activation.set(MS_CID);
        precursor.activation.set(MS_collision_energy, pExScanStats_->CollisionEnergy);

        precursor.selectedIons.push_back(selectedIon);
        sd.precursors.push_back(precursor);
    }

    long numPeaks = pScanStats_->PeaksInScan;
    //pScanStats_->get_PeaksInScan(&numPeaks);
    result->defaultArrayLength = (size_t) numPeaks;

    // TODO: which of these to use?
    //pSpectrum_->get_NumPeaks(&result->defaultArrayLength);

    if (getBinaryData)
    {
        VARIANT pfIntensities;
	    VARIANT pfMasses;
	    pSpectrum_->get_Intensities(&pfIntensities);
	    pSpectrum_->get_Masses(&pfMasses);

	    float HUGEP *intensityArrayPtr;
	    float HUGEP *massArrayPtr;

	    // lock safe arrays for access
	    HRESULT hr;
	    // TODO: check hr return value?
	    hr = SafeArrayAccessData(pfIntensities.parray, (void HUGEP**)&intensityArrayPtr);
	    hr = SafeArrayAccessData(pfMasses.parray, (void HUGEP**)&massArrayPtr);

	    vector<double> mzArray;
        mzArray.insert(mzArray.end(), massArrayPtr, massArrayPtr+numPeaks);

        vector<double> intensityArray;
        intensityArray.insert(intensityArray.end(), intensityArrayPtr, intensityArrayPtr+numPeaks);

	    result->setMZIntensityArrays(mzArray, intensityArray, MS_number_of_counts);

	    // clean up
	    hr = SafeArrayUnaccessData(pfIntensities.parray);
	    hr = SafeArrayDestroyData(pfIntensities.parray);
	    hr = SafeArrayUnaccessData(pfMasses.parray);
	    hr = SafeArrayDestroyData(pfMasses.parray);
    }

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


PWIZ_API_DECL void SpectrumList_Waters::createIndex()
{
    // fill file content metadata while creating index
    set<CVID> spectrumTypes;

    // Determine number of scans in each function
    for (short curFunction = 1; curFunction <= functionCount_; ++curFunction)
    {
        pFunctionInfo_->GetFunctionInfo(rawpath_.c_str(), curFunction);

        // determine function type and corresponding MS level
        BSTR bstrFuncType = NULL;
        pFunctionInfo_->get_FunctionType(&bstrFuncType);
        string funcType = convertBstrToString(bstrFuncType);
        SysFreeString(bstrFuncType);

        // TODO: figure out a better way. At least complete the list of other possible funcTypes

        FunctionMetaData& fmd = functionToMetaDataMap_[curFunction];
        fmd.type = funcType;
        translateFunctionType(funcType, fmd.msLevel, fmd.scanningMethod, fmd.spectrumType);
        spectrumTypes.insert(fmd.spectrumType);

        long numScans = pFunctionInfo_->NumScans;
        //pFunctionInfo_->get_NumScans(&numScans);
        size_ += numScans;

        for (long i=0; i < numScans; ++i)
        {
            index_.push_back(make_pair(SpectrumIdentity(), make_pair(0,0)));
            pair<SpectrumIdentity, pair<short, long> >& indexPair = index_.back();
            SpectrumIdentity& si = indexPair.first;
            si.index = index_.size()-1;
            si.nativeID = (format("%d,%d") % curFunction % (i+1)).str();
            si.id = "S" + si.nativeID;
            nativeIdToIndexMap_[curFunction][i+1] = si.index;
            indexPair.second.first = curFunction;
            indexPair.second.second = i+1;
        }
    }

    spectrumCache_.resize(size_);

    BOOST_FOREACH(CVID spectrumType, spectrumTypes)
    {
        const_cast<MSData&>(msd_).fileDescription.fileContent.set(spectrumType);
    }
}


/*PWIZ_API_DECL string SpectrumList_Waters::findPrecursorID(int precursorMsLevel, size_t index) const
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
}*/

} // detail
} // msdata
} // pwiz
