//
// $Id$
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


#include "SpectrumList_Bruker.hpp"


#ifdef PWIZ_READER_BRUKER
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::vendor_api::Bruker;


namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL
SpectrumList_Bruker::SpectrumList_Bruker(MSData& msd,
                                         const string& rootpath,
                                         Reader_Bruker_Format format,
                                         CompassDataPtr compassDataPtr)
:   msd_(msd), rootpath_(rootpath), format_(format),
    compassDataPtr_(compassDataPtr),
    size_(0)
{
    fillSourceList();

    switch (format_)
    {
        case Reader_Bruker_Format_YEP:
        case Reader_Bruker_Format_BAF:
            size_ = compassDataPtr_->getMSSpectrumCount();
            break;

        case Reader_Bruker_Format_FID:
            size_ = sourcePaths_.size();
            break;

        case Reader_Bruker_Format_U2:
            //size_ = compassDataPtr_->getLCSpectrumCount();
            break;

        case Reader_Bruker_Format_BAF_and_U2:
            size_ = compassDataPtr_->getMSSpectrumCount();
            //size_ += compassDataPtr_->getLCSpectrumCount();
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


MSSpectrumPtr SpectrumList_Bruker::getMSSpectrumPtr(size_t index) const
{
    if (format_ == Reader_Bruker_Format_FID)
    {
        compassDataPtr_ = CompassData::create(sourcePaths_[index].string());
        return compassDataPtr_->getMSSpectrum(1);
    }

    return compassDataPtr_->getMSSpectrum(index+1);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    if (index >= size_)
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
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    //try
    //{
        // is this spectrum from the LC interface?
        if (si.collection > -1)
        {
            // fill the spectrum from the LC interface
            /*CompassXtractWrapper::LC_AnalysisPtr& analysis = compassXtractWrapperPtr_->lcAnalysis_;
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

            result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);

            automation_vector<double> lcX(*ssd->GetXAxis(), automation_vector<double>::MOVE);
            result->getMZArray()->data.assign(lcX.begin(), lcX.end());

            automation_vector<double> lcY(*spectrum->GetIntensity(), automation_vector<double>::MOVE);
            result->getIntensityArray()->data.assign(lcY.begin(), lcY.end());*/

            return result;
        }

        // get the spectrum from MS interface
        MSSpectrumPtr spectrum = getMSSpectrumPtr(index);

        //scan.instrumentConfigurationPtr = 
            //findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

        int msLevel = spectrum->getMSMSStage();
        result->set(MS_ms_level, msLevel);

        if (msLevel == 1)
            result->set(MS_MS1_spectrum);
        else
            result->set(MS_MSn_spectrum);

        double scanTime = spectrum->getRetentionTime();
        if (scanTime > 0)
            scan.set(MS_scan_start_time, scanTime, UO_second);

        IonPolarity polarity = spectrum->getPolarity();
        switch (polarity)
        {
            case IonPolarity_Negative:
                result->set(MS_negative_scan);
                break;
            case IonPolarity_Positive:
                result->set(MS_positive_scan);
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

        /*EDAL::IMSSpectrumParameterCollectionPtr spectrumParameters = spectrum->MSSpectrumParameterCollection;
        long numParameters = spectrumParameters->Count;
        for (long i=1; i < numParameters; ++i)
        {
            EDAL::IMSSpectrumParameterPtr pParameter = spectrumParameters->GetItem(i);
            string group = (const char*) pParameter->GroupName;
            string name = (const char*) pParameter->ParameterName;
            string value = (const char*) pParameter->ParameterValue.bstrVal;
            scan.userParams.push_back(UserParam(name, value, group));
        }*/

        if (msLevel > 1)
        {
            Precursor precursor;

            vector<double> fragMZs, isolMZs;
            vector<FragmentationMode> fragModes;
            vector<IsolationMode> isolModes;

            spectrum->getFragmentationData(fragMZs, fragModes);

            if (!fragMZs.empty())
            {
                spectrum->getIsolationData(isolMZs, isolModes);

                for (size_t i=0; i < fragMZs.size(); ++i)
                {
                    if (fragMZs[i] > 0)
                    {
                        SelectedIon selectedIon(fragMZs[i]);

                        //long parentCharge = scanInfo->parentCharge();
                        //if (parentCharge > 0)
                        //    selectedIon.cvParams.push_back(CVParam(MS_charge_state, parentCharge));

                        switch (fragModes[i])
                        {
                            case FragmentationMode_CID:
                                precursor.activation.set(MS_CID);
                                break;
                            case FragmentationMode_ETD:
                                precursor.activation.set(MS_ETD);
                                break;
                            case FragmentationMode_CIDETD_CID:
                                precursor.activation.set(MS_CID);
                                precursor.activation.set(MS_ETD);
                                break;
                            case FragmentationMode_CIDETD_ETD:
                                precursor.activation.set(MS_CID);
                                precursor.activation.set(MS_ETD);
                                break;
                            case FragmentationMode_ISCID:
                                precursor.activation.set(MS_CID);
                                break;
                            case FragmentationMode_ECD:
                                precursor.activation.set(MS_ECD);
                                break;
                            case FragmentationMode_IRMPD:
                                precursor.activation.set(MS_IRMPD);
                                break;
                            case FragmentationMode_PTR:
                                break;
                        }
                        //precursor.activation.set(MS_collision_energy, pExScanStats_->CollisionEnergy);

                        precursor.selectedIons.push_back(selectedIon);
                    }

                    if (isolMZs[i] > 0)
                    {
                        precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolMZs[i], MS_m_z);
                    }
                }
            }

            if (precursor.selectedIons.size() > 0 || !precursor.isolationWindow.empty())
                result->precursors.push_back(precursor);
        }

        bool getLineData = msLevelsToCentroid.contains(msLevel);

        if ((getLineData && spectrum->hasLineData()) || !spectrum->hasProfileData())
        {
            getLineData = true;
            result->set(MS_centroid_spectrum);
            result->defaultArrayLength = spectrum->getLineDataSize();
        }
        else
        {
            getLineData = false;
            result->set(MS_profile_spectrum);
            result->defaultArrayLength = spectrum->getProfileDataSize();
        }


        if (getBinaryData)
        {
            result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
	        automation_vector<double> mzArray, intensityArray;
            if (getLineData)
                spectrum->getLineData(mzArray, intensityArray);
            else
                spectrum->getProfileData(mzArray, intensityArray);
            result->getMZArray()->data.assign(mzArray.begin(), mzArray.end());
            result->getIntensityArray()->data.assign(intensityArray.begin(), intensityArray.end());
            result->defaultArrayLength = mzArray.size();
        }
    /*}
    catch (_com_error& e) // not caught by either std::exception or '...'
    {
        throw runtime_error(string("[SpectrumList_Bruker::spectrum()] COM error: ") +
                            (const char*)e.Description());
    }*/

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

void addSource(MSData& msd, const bfs::path& sourcePath, const bfs::path& rootPath)
{
    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = sourcePath.string();
    sourceFile->name = sourcePath.leaf();

    // sourcePath: <source>\Analysis.yep|<source>\Analysis.baf|<source>\fid
    // rootPath: c:\path\to\<source>[\Analysis.yep|Analysis.baf|fid]
    bfs::path location = rootPath.has_branch_path() ?
                         bfs::complete(rootPath.branch_path() / sourcePath) :
                         bfs::complete(sourcePath); // uses initial path
    sourceFile->location = "file://" + location.branch_path().string();

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
                // in "/foo/bar/1/1SRef/fid", replace "/foo/bar/" with "" so relativePath is "1/1SRef/fid"
                bfs::path relativePath = sourcePaths_[i] / "fid";
                if (rootpath_.has_branch_path())
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + "/", "");
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_FID_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_FID_file);
            }
            break;

        // a YEP's source path is the same as the source file
        case Reader_Bruker_Format_YEP:
            {
                sourcePaths_.push_back(rootpath_ / "Analysis.yep");
                // strip parent path to get "bar.d/Analysis.yep"
                bfs::path relativePath = bfs::path(rootpath_.filename()) / "Analysis.yep";
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_file);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }
            break;

        // a BAF's source path is the same as the source file
        case Reader_Bruker_Format_BAF:
            {
                sourcePaths_.push_back(rootpath_ / "Analysis.baf");
                // strip parent path to get "bar.d/Analysis.baf"
                bfs::path relativePath = bfs::path(rootpath_.filename()) / "Analysis.baf";
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_file);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }
            break;

        // a BAF/U2 combo has two sources, with different nativeID formats
        case Reader_Bruker_Format_BAF_and_U2:
            {
                sourcePaths_.push_back(rootpath_ / "Analysis.baf");
                // strip parent path to get "bar.d/Analysis.baf"
                bfs::path relativePath = bfs::path(rootpath_.filename()) / "Analysis.baf";
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_file);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }

            {
                sourcePaths_.push_back(bfs::change_extension(rootpath_, ".u2"));
                // in "/foo/bar.d/bar.u2", replace "/foo/" with "" so relativePath is "bar.d/bar.u2"
                bfs::path relativePath = sourcePaths_.back();
                if (rootpath_.has_branch_path())
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + "/", "");
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_file);
            }
            break;

        case Reader_Bruker_Format_U2:
            {
                sourcePaths_.push_back(bfs::change_extension(rootpath_, ".u2"));
                // in "/foo/bar.d/bar.u2", replace "/foo/" with "" so relativePath is "bar.d/bar.u2"
                bfs::path relativePath = sourcePaths_.back();
                if (rootpath_.has_branch_path())
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + "/", "");
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_file);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }
            break;
    }
}

PWIZ_API_DECL void SpectrumList_Bruker::createIndex()
{
    /*if (format_ == Reader_Bruker_Format_U2 ||
        format_ == Reader_Bruker_Format_BAF_and_U2)
    {
        msd_.fileDescription.fileContent.set(MS_EMR_spectrum);

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
    }*/

    if (format_ != Reader_Bruker_Format_U2)
    {
        size_t remainder = size_ - index_.size();
        for (size_t i=0; i < remainder; ++i)
        {
            index_.push_back(IndexEntry());
            IndexEntry& si = index_.back();
            si.declaration = 0;
            si.collection = -1;
            si.scan = 0;
            si.index = index_.size()-1;
            switch (format_)
            {
                case Reader_Bruker_Format_FID:
                    si.id = "file=" + encode_xml_id_copy(msd_.fileDescription.sourceFilePtrs[i]->id);
                    break;
                default:
                    si.id = "scan=" + lexical_cast<string>(i+1);
                    break;
            }
            idToIndexMap_[si.id] = si.index;
        }
    }
}

} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_BRUKER

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_Bruker::size() const {return 0;}
const SpectrumIdentity& SpectrumList_Bruker::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_Bruker::find(const std::string& id) const {return 0;}
SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_BRUKER
