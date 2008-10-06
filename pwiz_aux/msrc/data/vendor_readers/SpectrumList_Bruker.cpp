//
// SpectrumList_Bruker.cpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#ifndef PWIZ_NO_READER_BRUKER
#include "utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/foreach.hpp"
#include "utility/misc/Filesystem.hpp"
#include "utility/misc/String.hpp"
#include "utility/misc/IntegerSet.hpp"
//#include "Reader_Bruker_Detail.hpp"
#include "SpectrumList_Bruker.hpp"
#include <iostream>
#include <stdexcept>

using boost::format;
using namespace pwiz::util;

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


PWIZ_API_DECL
SpectrumList_Bruker::SpectrumList_Bruker(const MSData& msd,
                                         const string& rootpath,
                                         SpectrumList_Bruker_Format format,
                                         EDAL::IMSAnalysisPtr& pAnalysis)
:   msd_(msd), rootpath_(rootpath), format_(format), pAnalysis_(pAnalysis), size_(0)
{
    pAnalysis->Open(rootpath.c_str());
    pSpectra_ = pAnalysis->MSSpectrumCollection;
    size_ = (size_t) pSpectra_->Count;

    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Bruker::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Bruker::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Bruker::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Bruker::find(const string& id) const
{
    string nativeID = bal::trim_left_copy_if(id, bal::is_any_of("S"));
    return findNative(nativeID);
}


PWIZ_API_DECL size_t SpectrumList_Bruker::findNative(const string& nativeID) const
{
    map<string, size_t>::const_iterator scanItr = nativeIdToIndexMap_.find(nativeID);
    if (scanItr == nativeIdToIndexMap_.end())
        return size_;
    return scanItr->second;
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    if (index > size_)
        throw runtime_error(("[SpectrumList_Bruker::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // returned cached Spectrum if possible
    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Bruker::spectrum()] Allocation error.");


    const SpectrumIdentity& si = index_[index];

    EDAL::IMSSpectrumPtr pSpectrum = pSpectra_->GetItem((long) index+1);

    result->index = si.index;
    result->id = si.id;
    result->nativeID = si.nativeID;

    SpectrumDescription& sd = result->spectrumDescription;
    Scan& scan = sd.scan;

    //scan.instrumentConfigurationPtr = 
        //findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

    long msLevel = pSpectrum->MSMSStage;
    result->set(MS_ms_level, msLevel);
    result->set(MS_MSn_spectrum);
    scan.set(MS_full_scan);

    double scanTime = pSpectrum->RetentionTime;
    if (scanTime > 0)
        scan.set(MS_scan_time, pSpectrum->RetentionTime, UO_minute);

    EDAL::SpectrumPolarity polarity = pSpectrum->Polarity;
    switch (polarity)
    {
        case EDAL::IonPolarity_Negative:
            scan.set(MS_negative_scan);
            break;
        case EDAL::IonPolarity_Positive:
            scan.set(MS_positive_scan);
            break;
        default:
            break;
    }

    /*sd.set(MS_base_peak_m_z, pScanStats_->BPM);
    sd.set(MS_base_peak_intensity, pScanStats_->BPI);
    sd.set(MS_total_ion_current, pScanStats_->TIC);*/

    // TODO: get correct values
    //scan.scanWindows.push_back(ScanWindow(pScanStats_->LoMass, pScanStats_->HiMass));

    //sd.set(MS_lowest_m_z_value, minObservedMz);
    //sd.set(MS_highest_m_z_value, maxObservedMz);

    EDAL::IMSSpectrumParameterCollectionPtr pSpectrumParameters = pSpectrum->MSSpectrumParameterCollection;
    long numParameters = pSpectrumParameters->Count;
    for (long i=1; i < numParameters; ++i)
    {
        EDAL::IMSSpectrumParameterPtr pParameter = pSpectrumParameters->GetItem(i);
        string group = convertBstrToString(pParameter->GroupName.GetBSTR());
        string name = convertBstrToString(pParameter->ParameterName.GetBSTR());
        string value = convertBstrToString(pParameter->ParameterValue.bstrVal);
        scan.userParams.push_back(UserParam(name, value, group));
    }

    if (msLevel > 1)
    {
        HRESULT hr;
        _variant_t fragmentationMassesVariant;
        SAFEARRAY* fragmentationModesArray;
        _variant_t isolationMassesVariant;
        SAFEARRAY* isolationModesArray;

        long numEntries = pSpectrum->GetFragmentationData(&fragmentationMassesVariant, &fragmentationModesArray);
        long numEntries2 = pSpectrum->GetIsolationData(&isolationMassesVariant, &isolationModesArray);
        if (numEntries != numEntries2)
            throw runtime_error("[SpectrumList_Bruker::spectrum()] Fragmentation data failed to match isolation data");

        Precursor precursor;

        if (numEntries > 0)
        {
            double HUGEP *fragmentationMassesPtr;
            EDAL::FragmentationModes HUGEP *fragmentationModesPtr;
	        hr = SafeArrayAccessData(fragmentationMassesVariant.parray, (void HUGEP**)&fragmentationMassesPtr);
            hr = SafeArrayAccessData(fragmentationModesArray, (void HUGEP**)&fragmentationModesPtr);

	        vector<double> fragmentationMasses;
            fragmentationMasses.insert(fragmentationMasses.end(), fragmentationMassesPtr, fragmentationMassesPtr+numEntries);

            vector<EDAL::FragmentationModes> fragmentationModes;
            fragmentationModes.insert(fragmentationModes.end(), fragmentationModesPtr, fragmentationModesPtr+numEntries);

            double HUGEP *isolationMassesPtr;
            EDAL::IsolationModes HUGEP *isolationModesPtr;
	        hr = SafeArrayAccessData(isolationMassesVariant.parray, (void HUGEP**)&isolationMassesPtr);
            hr = SafeArrayAccessData(isolationModesArray, (void HUGEP**)&isolationModesPtr);

	        vector<double> isolationMasses;
            isolationMasses.insert(isolationMasses.end(), isolationMassesPtr, isolationMassesPtr+numEntries);

            vector<EDAL::IsolationModes> isolationModes;
            isolationModes.insert(isolationModes.end(), isolationModesPtr, isolationModesPtr+numEntries);

            for (long i=0; i < numEntries; ++i)
            {
                if (fragmentationMasses[i] > 0)
                {
                    SelectedIon selectedIon;
                    selectedIon.set(MS_m_z, fragmentationMasses[i]);

                    //long parentCharge = scanInfo->parentCharge();
                    //if (parentCharge > 0)
                    //    selectedIon.cvParams.push_back(CVParam(MS_charge_state, parentCharge));

                    switch (fragmentationModes[i])
                    {
                        case EDAL::FragMode_CID:
                            precursor.activation.set(MS_CID);
                            break;
                        case EDAL::FragMode_ETD:
                            precursor.activation.set(MS_ETD);
                            break;
                        case EDAL::FragMode_CIDETD_CID:
                            precursor.activation.set(MS_CID);
                            precursor.activation.set(MS_ETD);
                            break;
                        case EDAL::FragMode_CIDETD_ETD:
                            precursor.activation.set(MS_CID);
                            precursor.activation.set(MS_ETD);
                            break;
                        case EDAL::FragMode_ISCID:
                            precursor.activation.set(MS_CID);
                            break;
                        case EDAL::FragMode_ECD:
                            precursor.activation.set(MS_ECD);
                            break;
                        case EDAL::FragMode_IRMPD:
                            precursor.activation.set(MS_IRMPD);
                            break;
                        case EDAL::FragMode_PTR:
                            break;
                    }
                    //precursor.activation.set(MS_collision_energy, pExScanStats_->CollisionEnergy);

                    precursor.selectedIons.push_back(selectedIon);
                }

                if (isolationMasses[i] > 0)
                    precursor.isolationWindow.set(MS_m_z, isolationMasses[i]);
            }
           
	        // clean up
            hr = SafeArrayUnaccessData(fragmentationMassesVariant.parray);
            hr = SafeArrayUnaccessData(fragmentationModesArray);
	        hr = SafeArrayUnaccessData(isolationMassesVariant.parray);
            hr = SafeArrayUnaccessData(isolationModesArray);
        }

        SafeArrayDestroyData(fragmentationMassesVariant.parray);
        SafeArrayDestroyData(fragmentationModesArray);
        SafeArrayDestroyData(isolationMassesVariant.parray);
        SafeArrayDestroyData(isolationModesArray);

        if (precursor.selectedIons.size() > 0 || !precursor.isolationWindow.empty())
            sd.precursors.push_back(precursor);
    }

    VARIANT pfIntensities;
    VARIANT pfMasses;
    long numDataPoints = pSpectrum->GetMassIntensityValues(EDAL::SpectrumType_Line, &pfMasses, &pfIntensities);

    bool getCentroid = msLevelsToCentroid.contains(msLevel);

    if (getCentroid && numDataPoints > 0)
    {
        sd.set(MS_centroid_mass_spectrum);
    }
    else
    {
        sd.set(MS_profile_mass_spectrum);
        HRESULT hr = SafeArrayDestroyData(pfIntensities.parray);
        hr = SafeArrayDestroyData(pfMasses.parray);
        numDataPoints = pSpectrum->GetMassIntensityValues(EDAL::SpectrumType_Profile, &pfMasses, &pfIntensities);
    }

    result->defaultArrayLength = numDataPoints;

    if (getBinaryData)
    {
	    double HUGEP *intensityArrayPtr;
	    double HUGEP *massArrayPtr;

	    // lock safe arrays for access
	    HRESULT hr;
	    // TODO: check hr return value?
	    hr = SafeArrayAccessData(pfIntensities.parray, (void HUGEP**)&intensityArrayPtr);
	    hr = SafeArrayAccessData(pfMasses.parray, (void HUGEP**)&massArrayPtr);

	    vector<double> mzArray;
        mzArray.insert(mzArray.end(), massArrayPtr, massArrayPtr+numDataPoints);

        vector<double> intensityArray;
        intensityArray.insert(intensityArray.end(), intensityArrayPtr, intensityArrayPtr+numDataPoints);

	    result->setMZIntensityArrays(mzArray, intensityArray, MS_number_of_counts);

	    // clean up
	    hr = SafeArrayUnaccessData(pfIntensities.parray);
	    hr = SafeArrayUnaccessData(pfMasses.parray);
    }

    HRESULT hr = SafeArrayDestroyData(pfIntensities.parray);
    hr = SafeArrayDestroyData(pfMasses.parray);

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


PWIZ_API_DECL void SpectrumList_Bruker::createIndex()
{
    // fill file content metadata while creating index
    set<CVID> spectrumTypes;

    for (size_t i=0; i < size_; ++i)
    {
        index_.push_back(SpectrumIdentity());
        SpectrumIdentity& si = index_.back();
        si.index = i;
        si.nativeID = lexical_cast<string>(i);
        si.id = "S" + si.nativeID;
        nativeIdToIndexMap_[si.nativeID] = si.index;
    }

    spectrumCache_.resize(size_);

    BOOST_FOREACH(CVID spectrumType, spectrumTypes)
    {
        const_cast<MSData&>(msd_).fileDescription.fileContent.set(spectrumType);
    }
}


/*PWIZ_API_DECL string SpectrumList_Bruker::findPrecursorID(int precursorMsLevel, size_t index) const
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

#endif // PWIZ_NO_READER_BRUKER
