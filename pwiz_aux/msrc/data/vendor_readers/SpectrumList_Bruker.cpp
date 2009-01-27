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

#ifdef PWIZ_READER_BRUKER
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
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
SpectrumList_Bruker::SpectrumList_Bruker(MSData& msd,
                                         const string& rootpath,
                                         SpectrumList_Bruker_Format format,
                                         EDAL::IMSAnalysisPtr& pAnalysis)
:   msd_(msd), rootpath_(rootpath), format_(format), pAnalysis_(pAnalysis), size_(0)
{
    try
    {
        if (format != SpectrumList_Bruker_Format_FID)
        {
            pAnalysis->Open(rootpath.c_str());
            pSpectra_ = pAnalysis->MSSpectrumCollection;
            size_ = (size_t) pSpectra_->Count;
        }
    }
    catch (_com_error& e) // not caught by either std::exception or '...'
    {
        throw runtime_error(string("[SpectrumList_Bruker::ctor()] COM error: ") +
            (const char*)e.Description() + "(HRESULT:" + lexical_cast<string>(e.Error()));
    }

    fillSourceList();
    createIndex();
    size_ = index_.size();
}


PWIZ_API_DECL size_t SpectrumList_Bruker::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Bruker::spectrumIdentity(size_t index) const
{
    if (index > size_)
        throw runtime_error(("[SpectrumList_Bruker::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Bruker::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
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

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Bruker::spectrum()] Allocation error.");

    const SpectrumIdentity& si = index_[index];

    try
    {
        EDAL::IMSSpectrumPtr pSpectrum;
        if (format_ == SpectrumList_Bruker_Format_FID)
        {
            HRESULT hr = pAnalysis_.CreateInstance("EDAL.MSAnalysis");
            if (FAILED(hr))
            {
                // No success when creating the analysis pointer - we decrypt the error from hr.
                LPVOID lpMsgBuf;

                ::FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER |
	                           FORMAT_MESSAGE_FROM_SYSTEM,
	                           NULL,
	                           hr,
	                           MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
	                           (LPTSTR) &lpMsgBuf,
	                           0,
	                           NULL );

                string error((const char*) lpMsgBuf);
                LocalFree(lpMsgBuf);
                throw runtime_error("[SpectrumList_Bruker::spectrum()] Error initializing CompassXtract: " + error);
            }
            pAnalysis_->Open(sourcePaths_[index].string().c_str());
            pSpectra_ = pAnalysis_->MSSpectrumCollection;
            pSpectrum = pSpectra_->GetItem(1);
        } else
            pSpectrum = pSpectra_->GetItem((long) index+1);

        result->index = si.index;
        result->id = si.id;

        result->scanList.scans.push_back(Scan());
        Scan& scan = result->scanList.scans[0];

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
                result->precursors.push_back(precursor);
        }

        VARIANT pfIntensities;
        VARIANT pfMasses;
        long numDataPoints = pSpectrum->GetMassIntensityValues(EDAL::SpectrumType_Line, &pfMasses, &pfIntensities);

        bool getCentroid = msLevelsToCentroid.contains(msLevel);

        if (getCentroid && numDataPoints > 0)
        {
            result->set(MS_centroid_mass_spectrum);
        }
        else
        {
            result->set(MS_profile_mass_spectrum);
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
    }
    catch (_com_error& e) // not caught by either std::exception or '...'
    {
        throw runtime_error(string("[SpectrumList_Bruker::spectrum()] COM error: ") +
                            (const char*)e.Description());
    }

    return result;
}


namespace {

void recursivelyEnumerateFIDs(vector<bfs::path>& fidPaths, const bfs::path& rootpath)
{
    const static bfs::directory_iterator endItr = bfs::directory_iterator();

    if (rootpath.leaf() == "fid")
        fidPaths.push_back(rootpath.branch_path());
    else if (bfs::is_directory(rootpath))
    {
        for (bfs::directory_iterator itr(rootpath); itr != endItr; ++itr)
            recursivelyEnumerateFIDs(fidPaths, itr->path());
    }
}

inline char idref_allowed(char c)
{
    return isalnum(c) || c=='-' ? 
           c : 
           '_';
}


string stringToIDREF(const string& s)
{
    string result = s;
    transform(result.begin(), result.end(), result.begin(), idref_allowed);
    return result;
}

void addSource(MSData& msd, const bfs::path& sourcePath)
{
    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = stringToIDREF(sourcePath.string());
    sourceFile->name = sourcePath.leaf();
    sourceFile->location = string("file://") + bfs::complete(sourcePath.branch_path()).string();
    sourceFile->cvParams.push_back(MS_yep_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
}

} // namespace


PWIZ_API_DECL void SpectrumList_Bruker::fillSourceList()
{
    switch (format_)
    {
        case SpectrumList_Bruker_Format_FID:
            recursivelyEnumerateFIDs(sourcePaths_, rootpath_);

            // each fid's source path is a directory but the source file is the fid
            for (size_t i=0; i < sourcePaths_.size(); ++i)
                addSource(msd_, sourcePaths_[i] / "fid");
            break;

        // a YEP's source path is the same as the source file
        case SpectrumList_Bruker_Format_YEP:
            sourcePaths_.push_back(rootpath_ / "Analysis.yep");
            addSource(msd_, sourcePaths_.back());
            break;

        // a BAF's source path is the same as the source file
        case SpectrumList_Bruker_Format_BAF:
            sourcePaths_.push_back(rootpath_ / "Analysis.baf");
            addSource(msd_, sourcePaths_.back());
            break;
    }
}

PWIZ_API_DECL void SpectrumList_Bruker::createIndex()
{
    // fill file content metadata while creating index
    set<CVID> spectrumTypes;
    spectrumTypes.insert(MS_MSn_spectrum);

    index_.resize(max(size_, sourcePaths_.size()));

    for (size_t i=0; i < index_.size(); ++i)
    {
        SpectrumIdentity& si = index_[i];
        si.index = i;
        switch (format_)
        {
            case SpectrumList_Bruker_Format_FID:
                si.id = "file=" + msd_.fileDescription.sourceFilePtrs[i]->id;
                break;
            default:
                si.id = "scan=" + lexical_cast<string>(i+1);
                break;
        }
        idToIndexMap_[si.id] = si.index;
    }

    BOOST_FOREACH(CVID spectrumType, spectrumTypes)
    {
        msd_.fileDescription.fileContent.set(spectrumType);
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

#endif // PWIZ_READER_BRUKER
