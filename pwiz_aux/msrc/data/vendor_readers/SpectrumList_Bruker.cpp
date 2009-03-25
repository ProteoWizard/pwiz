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
                                         Reader_Bruker_Format format,
                                         CompassXtractWrapperPtr compassXtractWrapperPtr)
:   msd_(msd), rootpath_(rootpath), format_(format),
    compassXtractWrapperPtr_(compassXtractWrapperPtr),
    size_(0)
{
    fillSourceList();

    switch (format_)
    {
        case Reader_Bruker_Format_YEP:
        case Reader_Bruker_Format_BAF:
            size_ = (size_t) compassXtractWrapperPtr_->msSpectrumCollection_->Count;
            break;

        case Reader_Bruker_Format_FID:
            size_ = sourcePaths_.size();
            break;

        case Reader_Bruker_Format_U2:
            {
                CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr->lcAnalysis_;
                CompassXtractWrapper::LC_SpectrumSourceDeclarationList& ssdList = compassXtractWrapperPtr_->spectrumSourceDeclarations_;
                for (size_t i=0; i < ssdList.size(); ++i)
                    size_ += analysis->GetSpectrumCollection(ssdList[i]->GetSpectrumCollectionId())->GetNumberOfSpectra();
            }
            break;

        case Reader_Bruker_Format_BAF_and_U2:
            size_ = (size_t) compassXtractWrapperPtr_->msSpectrumCollection_->Count;
            {
                CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr->lcAnalysis_;
                CompassXtractWrapper::LC_SpectrumSourceDeclarationList& ssdList = compassXtractWrapperPtr_->spectrumSourceDeclarations_;
                for (size_t i=0; i < ssdList.size(); ++i)
                    size_ += analysis->GetSpectrumCollection(ssdList[i]->GetSpectrumCollectionId())->GetNumberOfSpectra();
            }
            break;
    }

    createIndex();
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


namespace {

template<typename T>
void convertSafeArrayToVector(SAFEARRAY* parray, vector<T>& result)
{
    T* data;
    HRESULT hr = SafeArrayAccessData(parray, (void**) &data);
    if (FAILED(hr) || !data)
        throw runtime_error("convertSafeArrayToVector(): Data access error.");
    result.assign(data, data + parray->rgsabound->cElements);
}

} // namespace


EDAL::IMSSpectrumPtr SpectrumList_Bruker::getMSSpectrumPtr(size_t index) const
{
    if (format_ == Reader_Bruker_Format_FID)
    {
        HRESULT hr = compassXtractWrapperPtr_->msAnalysis_.CreateInstance("EDAL.MSAnalysis");
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
            throw runtime_error("[SpectrumList_Bruker::getMSSpectrumPtr()] Error initializing CompassXtract MS interface: " + error);
        }
        compassXtractWrapperPtr_->msAnalysis_->Open(sourcePaths_[index].string().c_str());
        compassXtractWrapperPtr_->msSpectrumCollection_ = compassXtractWrapperPtr_->msAnalysis_->MSSpectrumCollection;
        return compassXtractWrapperPtr_->msSpectrumCollection_->GetItem(1);
    }

    return compassXtractWrapperPtr_->msSpectrumCollection_->GetItem((long) index+1);
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

    const IndexEntry& si = index_[index];
    result->index = si.index;
    result->id = si.id;

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];

    try
    {
        // is this spectrum from the LC interface?
        if (si.collection > -1)
        {
            // fill the spectrum from the LC interface
            CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr_->lcAnalysis_;
            CompassXtractWrapper::LC_SpectrumSourceDeclarationPtr& ssd = compassXtractWrapperPtr_->spectrumSourceDeclarations_[si.declaration];
            BDal_CXt_Lc_Interfaces::ISpectrumCollectionPtr spectra = analysis->GetSpectrumCollection(si.collection);
            BDal_CXt_Lc_Interfaces::ISpectrumPtr spectrum = spectra->GetItem(si.scan);

            if (ssd->GetXAxisUnit() == BDal_CXt_Lc_Interfaces::Unit_NanoMeter)
                result->set(MS_EMR_spectrum);
            else
                throw runtime_error("[SpectrumList_Bruker::spectrum()] unexpected XAxisUnit");

            double scanTime = spectrum->GetTime();
            if (scanTime > 0)
                scan.set(MS_scan_start_time, scanTime, UO_minute);

            vector<double> lcX, lcY;
            convertSafeArrayToVector(ssd->GetXAxis(), lcX);
            convertSafeArrayToVector(spectrum->GetIntensity(), lcY);
            result->setMZIntensityArrays(lcX, lcY, MS_number_of_counts);
            return result;
        }

        // get the spectrum from MS interface
        EDAL::IMSSpectrumPtr pSpectrum = getMSSpectrumPtr(index);

        //scan.instrumentConfigurationPtr = 
            //findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

        long msLevel = pSpectrum->MSMSStage;
        result->set(MS_ms_level, msLevel);

        if (msLevel == 1)
            result->set(MS_MS1_spectrum);
        else
            result->set(MS_MSn_spectrum);

        double scanTime = pSpectrum->RetentionTime;
        if (scanTime > 0)
            scan.set(MS_scan_start_time, pSpectrum->RetentionTime, UO_minute);

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

        //sd.set(MS_lowest_observed_m_z, minObservedMz);
        //sd.set(MS_highest_observed_m_z, maxObservedMz);

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
                        selectedIon.set(MS_selected_ion_m_z, fragmentationMasses[i]);

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
                    {
                        precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolationMasses[i]);
                    }
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
            result->set(MS_centroid_spectrum);
        }
        else
        {
            result->set(MS_profile_spectrum);
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

void addSource(MSData& msd, const bfs::path& sourcePath, const bfs::path& rootPath)
{
    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = stringToIDREF(sourcePath.string());
    sourceFile->name = sourcePath.leaf();
    // sourcePath: <source>\Analysis.yep|<source>\Analysis.baf|<source>\fid
    // rootPath: c:\path\to\<source>[\Analysis.yep|Analysis.baf|fid]
    sourceFile->location = string("file://") + bfs::complete(sourcePath, rootPath.branch_path()).branch_path().string();
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
}

} // namespace


PWIZ_API_DECL void SpectrumList_Bruker::fillSourceList()
{
    switch (format_)
    {
        case Reader_Bruker_Format_FID:
            recursivelyEnumerateFIDs(sourcePaths_, rootpath_);

            // each fid's source path is a directory but the source file is the fid
            for (size_t i=0; i < sourcePaths_.size(); ++i)
            {
                bfs::path relativePath = bal::replace_first_copy((sourcePaths_[i] / "fid").string(), rootpath_.branch_path().string() + "/", "");
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_FID_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_FID_file);
            }
            break;

        // a YEP's source path is the same as the source file
        case Reader_Bruker_Format_YEP:
            sourcePaths_.push_back(rootpath_ / "Analysis.yep");
            addSource(msd_, sourcePaths_.back(), rootpath_);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_nativeID_format);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_file);
            break;

        // a BAF's source path is the same as the source file
        case Reader_Bruker_Format_BAF:
            sourcePaths_.push_back(rootpath_ / "Analysis.baf");
            addSource(msd_, sourcePaths_.back(), rootpath_);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_nativeID_format);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_file);
            break;

        // a BAF/U2 combo has two sources, with different nativeID formats
        case Reader_Bruker_Format_BAF_and_U2:
            sourcePaths_.push_back(rootpath_ / "Analysis.baf");
            addSource(msd_, sourcePaths_.back(), rootpath_);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_nativeID_format);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_file);

            sourcePaths_.push_back(bfs::change_extension(rootpath_, ".u2"));
            addSource(msd_, sourcePaths_.back(), rootpath_);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_nativeID_format);
            msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_file);
            break;
    }
}

PWIZ_API_DECL void SpectrumList_Bruker::createIndex()
{
    if (format_ == Reader_Bruker_Format_U2 ||
        format_ == Reader_Bruker_Format_BAF_and_U2)
    {
        msd_.fileDescription.fileContent.set(MS_EMR_spectrum);

        CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr_->lcAnalysis_;
        CompassXtractWrapper::LC_SpectrumSourceDeclarationList& ssdList = compassXtractWrapperPtr_->spectrumSourceDeclarations_;
        for (size_t i=0; i < ssdList.size(); ++i)
        {
            long scId = ssdList[i]->GetSpectrumCollectionId();
            size_t numSpectra = (size_t) analysis->GetSpectrumCollection(scId)->GetNumberOfSpectra();

            for (size_t j=0; j < numSpectra; ++j)
            {
                index_.push_back(IndexEntry());
                IndexEntry& si = index_.back();
                si.declaration = i;
                si.collection = scId;
                si.scan = j;
                si.index = index_.size()-1;
                si.id = "declaration=" + lexical_cast<string>(si.declaration) +
                        " collection=" + lexical_cast<string>(si.collection) +
                        " scan=" + lexical_cast<string>(si.scan);
                idToIndexMap_[si.id] = si.index;
            }
        }
    }

    if (format_ != Reader_Bruker_Format_U2)
    {
        bool hasMS1 = false;
        bool hasMSn = false;
        size_t remainder = size_ - index_.size();
        for (size_t i=0; i < remainder; ++i)
        {
            EDAL::IMSSpectrumPtr pSpectrum = getMSSpectrumPtr(i);
            if (!hasMS1 && pSpectrum->MSMSStage == 1)
            {
                hasMS1 = true;
                msd_.fileDescription.fileContent.set(MS_MS1_spectrum);
            }
            else if (!hasMSn)
            {
                hasMSn = true;
                msd_.fileDescription.fileContent.set(MS_MSn_spectrum);
            }

            index_.push_back(IndexEntry());
            IndexEntry& si = index_.back();
            si.declaration = 0;
            si.collection = -1;
            si.scan = 0;
            si.index = index_.size()-1;
            switch (format_)
            {
                case Reader_Bruker_Format_FID:
                    si.id = "file=" + msd_.fileDescription.sourceFilePtrs[i]->id;
                    break;
                default:
                    si.id = "scan=" + lexical_cast<string>(i+1);
                    break;
            }
            idToIndexMap_[si.id] = si.index;
        }
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
