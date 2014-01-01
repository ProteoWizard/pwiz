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


#include "SpectrumList_Bruker.hpp"


#ifdef PWIZ_READER_BRUKER
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include <boost/range/algorithm/find_if.hpp>


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::vendor_api::Bruker;


namespace pwiz {
namespace msdata {
namespace detail {


const char* parameterAlternativeNames[] =
{
    "IsolationWidth:MS(n) Isol Width;Isolation Resolution FWHM",
    "ChargeState:Trigger Charge MS(2);Trigger Charge MS(3);Trigger Charge MS(4);Trigger Charge MS(5);Precursor Charge State"
};

size_t parameterAlternativeNamesSize = sizeof(parameterAlternativeNames) / sizeof(const char*);


PWIZ_API_DECL
string SpectrumList_Bruker::ParameterCache::get(const string& parameterName, MSSpectrumParameterList& parameters)
{
    map<string, size_t>::const_iterator findItr = parameterIndexByName_.find(parameterName);

    if (findItr == parameterIndexByName_.end())
    {
        update(parameters);

        // if still not found, return empty string
        findItr = parameterIndexByName_.find(parameterName);
        if (findItr == parameterIndexByName_.end())
            return string();
    }

    const MSSpectrumParameter& parameter = parameters[findItr->second];
    map<string, string>::const_iterator alternativeNameItr = parameterAlternativeNameMap_.find(parameter.name);

    if (parameter.name != parameterName && alternativeNameItr == parameterAlternativeNameMap_.end())
    {
        // if parameter name doesn't match, invalidate the cache and try again
        update(parameters);
        return get(parameterName, parameters);
    }

    return parameter.value;
}

PWIZ_API_DECL
void SpectrumList_Bruker::ParameterCache::update(MSSpectrumParameterList& parameters)
{
    parameterIndexByName_.clear();
    parameterAlternativeNameMap_.clear();

    vector<string> tokens;
    for (size_t i=0; i < parameterAlternativeNamesSize; ++i)
    {
        bal::split(tokens, parameterAlternativeNames[i], bal::is_any_of(":;"));
        for (size_t j=1; j < tokens.size(); ++j)
            parameterAlternativeNameMap_[tokens[j]] = tokens[0];
    }

    size_t i = 0;
    BOOST_FOREACH(const MSSpectrumParameter& p, parameters)
    {
        map<string, string>::const_iterator findItr = parameterAlternativeNameMap_.find(p.name);
        if (findItr != parameterAlternativeNameMap_.end())
        {   
            //cout << p.name << ": " << p.value << "\n";
            parameterIndexByName_[findItr->second] = i;
        }
        else
            parameterIndexByName_[p.name] = i;

        ++i;
    }
}


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
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Bruker::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Bruker::spectrumIdentity(size_t index) const
{
    if (index >= size_)
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


MSSpectrumPtr SpectrumList_Bruker::getMSSpectrumPtr(size_t scan, vendor_api::Bruker::DetailLevel detailLevel) const
{
    if (format_ == Reader_Bruker_Format_FID)
    {
        compassDataPtr_ = CompassData::create(sourcePaths_[scan].string());
        return compassDataPtr_->getMSSpectrum(1, detailLevel);
    }

    return compassDataPtr_->getMSSpectrum(scan, detailLevel);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, DetailLevel detailLevel) const 
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{ 
    if (index >= size_)
        throw runtime_error(("[SpectrumList_Bruker::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Bruker::spectrum()] Allocation error.");

    vendor_api::Bruker::DetailLevel brukerDetailLevel;
    switch (detailLevel)
    {
        case DetailLevel_InstantMetadata:
        case DetailLevel_FastMetadata:
            brukerDetailLevel = vendor_api::Bruker::DetailLevel_InstantMetadata;
            break;

        case DetailLevel_FullMetadata:
            brukerDetailLevel = vendor_api::Bruker::DetailLevel_FullMetadata;
            break;

        default:
        case DetailLevel_FullData:
            brukerDetailLevel = vendor_api::Bruker::DetailLevel_FullData;
            break;
    }

    const IndexEntry& si = index_[index];
    result->index = si.index;
    result->id = si.id;

    // the scan element may be empty for MALDI spectra, but it's required for a valid file
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
            LCSpectrumSourcePtr source = compassDataPtr_->getLCSource(si.source);
            LCSpectrumPtr spectrum = compassDataPtr_->getLCSpectrum(si.source, si.scan);

            if (source->getXAxisUnit() == LCUnit_NanoMeter)
                result->set(MS_EMR_spectrum);
            else
                throw runtime_error("[SpectrumList_Bruker::spectrum()] unexpected XAxisUnit");

            double scanTime = spectrum->getTime();
            if (scanTime > 0)
                scan.set(MS_scan_start_time, scanTime, UO_minute);

            result->set(MS_profile_spectrum);

            vector<double> lcX;
            source->getXAxis(lcX);

            if (detailLevel == DetailLevel_FullData)
            {
                result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
                result->defaultArrayLength = lcX.size();

                BinaryDataArrayPtr mzArray = result->getMZArray();
                vector<CVParam>::iterator itr = boost::range::find_if(mzArray->cvParams, CVParamIs(MS_m_z_array));
                *itr = CVParam(MS_wavelength_array);

                swap(mzArray->data, lcX);

                vector<double> lcY;
                spectrum->getData(lcY);
                swap(result->getIntensityArray()->data, lcY);
            }
            else
                result->defaultArrayLength = lcX.size();

            return result;
        }

        // get the spectrum from MS interface; for FID formats scan is 0-based, else it's 1-based
        MSSpectrumPtr spectrum = getMSSpectrumPtr(si.scan, brukerDetailLevel);

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

        // Enumerating the parameter list is not instant.
        if (detailLevel == DetailLevel_InstantMetadata)
            return result;

        /*sd.set(MS_base_peak_m_z, pScanStats_->BPM);
        sd.set(MS_base_peak_intensity, pScanStats_->BPI);
        sd.set(MS_total_ion_current, pScanStats_->TIC);*/

        //sd.set(MS_lowest_observed_m_z, minObservedMz);
        //sd.set(MS_highest_observed_m_z, maxObservedMz);

        MSSpectrumParameterListPtr parametersPtr = spectrum->parameters();
        MSSpectrumParameterList& parameters = *parametersPtr;

        // cache parameter indexes for this msLevel if they aren't already cached
        ParameterCache& parameterCache = parameterCacheByMsLevel_[msLevel];

        string scanBegin = parameterCache.get("Scan Begin", parameters);
        string scanEnd = parameterCache.get("Scan End", parameters);

        if (!scanBegin.empty() && !scanEnd.empty())
            scan.scanWindows.push_back(ScanWindow(lexical_cast<double>(scanBegin), lexical_cast<double>(scanEnd), MS_m_z));

        if (msLevel > 1)
        {
            string isolationWidth = parameterCache.get("IsolationWidth", parameters);
            string triggerCharge = parameterCache.get("ChargeState", parameters);

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

                        if (!triggerCharge.empty())
                        {
                            if (triggerCharge == "single") selectedIon.set(MS_charge_state, 1);
                            else if (triggerCharge == "double") selectedIon.set(MS_charge_state, 2);
                            else if (triggerCharge == "triple") selectedIon.set(MS_charge_state, 3);
                            else if (triggerCharge == "quad") selectedIon.set(MS_charge_state, 4);
                            else
                            {
                                try
                                {
                                    int charge = lexical_cast<int>(triggerCharge);
                                    if (charge > 0)
                                        selectedIon.set(MS_charge_state, charge);
                                }
                                catch (bad_lexical_cast&) {}
                            }
                        }

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

                        if (!isolationWidth.empty())
                        {
                            double value = lexical_cast<double>(isolationWidth);
                            if (value > 0)
                            {
                                precursor.isolationWindow.set(MS_isolation_window_lower_offset, value/2, MS_m_z);
                                precursor.isolationWindow.set(MS_isolation_window_upper_offset, value/2, MS_m_z);
                            }
                        }
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


        if (detailLevel == DetailLevel_FullData)
        {
            result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
	        automation_vector<double> mzArray, intensityArray;
            if (getLineData)
            {
                spectrum->getLineData(mzArray, intensityArray);
                if (spectrum->hasProfileData())
                    result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum
            }
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
    sourceFile->name = BFS_STRING(sourcePath.leaf());

    // sourcePath: <source>\Analysis.yep|<source>\Analysis.baf|<source>\fid
    // rootPath: c:\path\to\<source>[\Analysis.yep|Analysis.baf|fid]
    bfs::path location = rootPath.has_branch_path() ?
                         BFS_COMPLETE(rootPath.branch_path() / sourcePath) :
                         BFS_COMPLETE(sourcePath); // uses initial path
    sourceFile->location = "file://" + location.branch_path().string();

    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
}

} // namespace


#if BOOST_FILESYSTEM_VERSION == 2
# define NATIVE_PATH_SLASH "/"
#else
# define NATIVE_PATH_SLASH "\\"
#endif


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
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + NATIVE_PATH_SLASH, "");
                relativePath = bal::replace_all_copy(relativePath.string(), NATIVE_PATH_SLASH, "/");
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
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + NATIVE_PATH_SLASH, "");
                relativePath = bal::replace_all_copy(relativePath.string(), NATIVE_PATH_SLASH, "/");
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
                    relativePath = bal::replace_first_copy(relativePath.string(), rootpath_.branch_path().string() + NATIVE_PATH_SLASH, "");
                relativePath = bal::replace_all_copy(relativePath.string(), NATIVE_PATH_SLASH, "/");
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
    if (format_ == Reader_Bruker_Format_U2 ||
        format_ == Reader_Bruker_Format_BAF_and_U2)
    {
        msd_.fileDescription.fileContent.set(MS_EMR_spectrum);

        for (size_t i=0, end=compassDataPtr_->getLCSourceCount(); i < end; ++i)
        {
            LCSpectrumSourcePtr source = compassDataPtr_->getLCSource(i);

            for (size_t scan=0, end=compassDataPtr_->getLCSpectrumCount(i); scan < end; ++scan)
            {
                index_.push_back(IndexEntry());
                IndexEntry& si = index_.back();
                si.source = i;
                si.collection = source->getCollectionId();
                si.scan = scan;
                si.index = index_.size()-1;
                si.id = "collection=" + lexical_cast<string>(si.collection) +
                        " scan=" + lexical_cast<string>(si.scan);
                idToIndexMap_[si.id] = si.index;
            }
        }
    }

    if (format_ == Reader_Bruker_Format_FID)
    {
        int scan = -1;
        BOOST_FOREACH(const SourceFilePtr& sf, msd_.fileDescription.sourceFilePtrs)
        {
            index_.push_back(IndexEntry());
            IndexEntry& si = index_.back();
            si.source = si.collection = -1;
            si.index = index_.size()-1;
            si.scan = ++scan;
            si.id = "file=" + encode_xml_id_copy(sf->id);
            idToIndexMap_[si.id] = si.index;
        }
    }
    else if (format_ != Reader_Bruker_Format_U2)
    {
        for (size_t scan=1, end=compassDataPtr_->getMSSpectrumCount(); scan <= end; ++scan)
        {
            index_.push_back(IndexEntry());
            IndexEntry& si = index_.back();
            si.source = si.collection = -1;
            si.index = index_.size()-1;
            si.scan = scan;
            si.id = "scan=" + lexical_cast<string>(si.scan);
            idToIndexMap_[si.id] = si.index;
        }
    }

    size_ = index_.size();
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
SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_BRUKER
