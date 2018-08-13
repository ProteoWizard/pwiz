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
#include "pwiz/utility/chemistry/Chemistry.hpp"


#ifdef PWIZ_READER_BRUKER
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include <boost/range/algorithm/find_if.hpp>
#include <boost/spirit/include/karma.hpp>


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::vendor_api::Bruker;


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Bruker;


PWIZ_API_DECL
SpectrumList_Bruker::SpectrumList_Bruker(MSData& msd,
                                         const string& rootpath,
                                         Reader_Bruker_Format format,
                                         CompassDataPtr compassDataPtr,
                                         const Reader::Config& config)
:   msd_(msd), rootpath_(rootpath), format_(format),
    compassDataPtr_(compassDataPtr), config_(config),
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
    boost::container::flat_map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
    {
        if (format_ == Reader_Bruker_Format_TDF)
        {
            try
            {
                int frame = msdata::id::valueAs<int>(id, "frame");
                int scan = msdata::id::valueAs<int>(id, "scan");
                return compassDataPtr_->getSpectrumIndex(frame, scan);
            }
            catch (exception&)
            {
                // fall through and return size_ (id not found)
            }
        }

        return size_;
    }
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
    return spectrum(index, getBinaryData, MSLevelsNone);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Bruker::spectrum(size_t index, DetailLevel detailLevel) const 
{
    return spectrum(index, detailLevel, MSLevelsNone);
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

        pair<double, double> scanRange = spectrum->getScanRange();
        if (scanRange.first > 0 && scanRange.second > 0)
            scan.scanWindows.push_back(ScanWindow(scanRange.first, scanRange.second, MS_m_z));

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

        //sd.set(MS_base_peak_m_z, pScanStats_->BPM);
        if (spectrum->getTIC() > 0)
        {
            result->set(MS_base_peak_intensity, spectrum->getBPI());
            result->set(MS_total_ion_current, spectrum->getTIC());
        }

        if (detailLevel == DetailLevel_InstantMetadata)
            return result;

        if (spectrum->isIonMobilitySpectrum())
            scan.set(MS_inverse_reduced_ion_mobility, spectrum->oneOverK0(), MS_Vs_cm_2);

        if (detailLevel == DetailLevel_FastMetadata)
            return result;

        // Enumerating merged scan numbers is not instant.
        IntegerSet scanNumbers = spectrum->getMergedScanNumbers();
        if (config_.combineIonMobilitySpectra && scanNumbers.size() < 100)
        {
            using namespace boost::spirit::karma;
            auto frameScanPair = compassDataPtr_->getFrameScanPair(si.scan);
            generate(std::back_insert_iterator<std::string>(scan.spectrumID),
                     "frame=" << int_ << " scan=" << int_,
                     frameScanPair.first, *scanNumbers.begin());

            vector<Scan>& scans = result->scanList.scans;
            for (auto itr = ++scanNumbers.begin(); itr != scanNumbers.end(); ++itr)
            {
                scans.push_back(Scan());
                scans.back().instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;
                generate(std::back_insert_iterator<std::string>(scans.back().spectrumID),
                         "frame=" << int_ << " scan=" << int_,
                         frameScanPair.first, *itr);

                // CONSIDER: do we need this? all scan times will be the same and it's rather verbose
                //if (scanTime > 0)
                //    scans.back().set(MS_scan_start_time, scanTime, UO_second);

                // CONSIDER: do we need this? all scan ranges will be the same and it's very verbose!
                //if (scanRange.first > 0 && scanRange.second > 0)
                //    scans.back().scanWindows.push_back(ScanWindow(scanRange.first, scanRange.second, MS_m_z));
            }
            result->scanList.set(MS_sum_of_spectra);
        }
        else
            result->scanList.set(MS_no_combination);

        //sd.set(MS_lowest_observed_m_z, minObservedMz);
        //sd.set(MS_highest_observed_m_z, maxObservedMz);

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

                        int charge = spectrum->getChargeState();
                        if (charge > 0)
                            selectedIon.set(MS_charge_state, charge);

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
                                precursor.activation.set(MS_in_source_collision_induced_dissociation);
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

                        double isolationWidth = spectrum->getIsolationWidth();
                        if (isolationWidth > 0)
                        {
                            precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth / 2, MS_m_z);
                            precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth / 2, MS_m_z);
                        }
                    }
                }
            }

            if (precursor.selectedIons.size() > 0 || !precursor.isolationWindow.empty())
                result->precursors.push_back(precursor);
        }

        if (detailLevel == DetailLevel_FullData)
        {
            bool getLineData = msLevelsToCentroid.contains(msLevel);

            result->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);

            if (config_.combineIonMobilitySpectra)
            {
                auto& mz = result->getMZArray()->data;
                auto& intensity = result->getIntensityArray()->data;

                BinaryDataArrayPtr mobility(new BinaryDataArray);
                result->binaryDataArrayPtrs.push_back(mobility);
                CVParam arrayType(MS_mean_ion_mobility_array);
                arrayType.units = MS_Vs_cm_2;
                mobility->cvParams.emplace_back(arrayType);

                spectrum->getCombinedSpectrumData(mz, intensity, mobility->data);
                result->defaultArrayLength = mz.size();
            }
            else
            {
                automation_vector<double> mzArray, intensityArray;
                if (!getLineData)
                {
                    spectrum->getProfileData(mzArray, intensityArray);
                    if (mzArray.size() == 0)
                        getLineData = true;  // We preferred profile, but there isn't any - try centroided
                    else
                        result->set(MS_profile_spectrum);
                }

                if (getLineData)
                {
                    result->set(MS_centroid_spectrum); // Declare this as centroided data even if scan is empty
                    spectrum->getLineData(mzArray, intensityArray);
                    if (mzArray.size() > 0 && msLevelsToCentroid.contains(msLevel))
                    {
                        result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was probably also a profile spectrum, but doesn't need conversion (actually checking for profile data is crazy slow)
                    }
                }
                result->getMZArray()->data.assign(mzArray.begin(), mzArray.end());
                result->getIntensityArray()->data.assign(intensityArray.begin(), intensityArray.end());
                result->defaultArrayLength = mzArray.size();
            }
        }
        else if (detailLevel == DetailLevel_FullMetadata)
        {
            // N.B.: just getting the data size from the Bruker API is quite expensive.
            if (msLevelsToCentroid.contains(msLevel) || ((result->defaultArrayLength = spectrum->getProfileDataSize())==0))
            {
                result->defaultArrayLength = spectrum->getLineDataSize();
                result->set(MS_centroid_spectrum);
            }
            else
            {
                result->set(MS_profile_spectrum);
            }
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_FID_format);
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_format);
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_format);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }
            break;

        // a TDF's source path is the same as the source file
        case Reader_Bruker_Format_TDF:
            {
                sourcePaths_.push_back(rootpath_ / "Analysis.tdf");
                // strip parent path to get "bar.d/Analysis.tdf"
                bfs::path relativePath = bfs::path(rootpath_.filename()) / "Analysis.tdf";
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_TDF_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_TDF_format);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }

            {
                sourcePaths_.push_back(rootpath_ / "Analysis.tdf_bin");
                // strip parent path to get "bar.d/Analysis.tdf_bin"
                bfs::path relativePath = bfs::path(rootpath_.filename()) / "Analysis.tdf_bin";
                addSource(msd_, relativePath, rootpath_);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_TDF_nativeID_format);
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_TDF_format);
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_BAF_format);
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_format);
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
                msd_.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_U2_format);
                msd_.run.defaultSourceFilePtr = msd_.fileDescription.sourceFilePtrs.back();
            }
            break;
    }
}

PWIZ_API_DECL void SpectrumList_Bruker::createIndex()
{
    using namespace boost::spirit::karma;
    map<std::string, size_t> idToIndexTempMap;

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
                si.id = "collection=" + lexical_cast<std::string>(si.collection) +
                        " scan=" + lexical_cast<std::string>(si.scan);
                idToIndexTempMap[si.id] = si.index;
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
            idToIndexTempMap[si.id] = si.index;
        }
    }
    else if (format_ == Reader_Bruker_Format_TDF)
    {
        index_.reserve(compassDataPtr_->getMSSpectrumCount());
        for (size_t scan = 1, end = compassDataPtr_->getMSSpectrumCount(); scan <= end; ++scan)
        {
            index_.emplace_back(IndexEntry());
            IndexEntry& si = index_.back();
            si.source = si.collection = -1;
            si.index = index_.size() - 1;
            si.scan = scan;
            auto frameScanPair = compassDataPtr_->getFrameScanPair(scan);
            std::back_insert_iterator<std::string> sink(si.id);
            if (config_.combineIonMobilitySpectra)
            {
                generate(sink,
                         "merged=" << int_,
                         si.index);
                idToIndexTempMap[si.id] = si.index;
            }
            else // not inserting into idToIndexTempMap (instead uses on-the-fly logic in find())
                generate(sink,
                         "frame=" << int_ << " scan=" << int_,
                         frameScanPair.first, frameScanPair.second);
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
            std::back_insert_iterator<std::string> sink(si.id);
            generate(sink,
                     "scan=" << int_,
                     si.scan);
            idToIndexTempMap[si.id] = si.index;
        }
    }

    idToIndexMap_.reserve(idToIndexTempMap.size());
    idToIndexMap_.insert(boost::container::ordered_unique_range, idToIndexTempMap.begin(), idToIndexTempMap.end());
    size_ = index_.size();
}

PWIZ_API_DECL bool SpectrumList_Bruker::hasIonMobility() const
{
    return format_ == Reader_Bruker_Format_TDF;
}

PWIZ_API_DECL bool SpectrumList_Bruker::hasPASEF() const
{
    return compassDataPtr_->hasPASEFData();
}

PWIZ_API_DECL bool SpectrumList_Bruker::canConvertInverseK0AndCCS() const
{
    return format_ == Reader_Bruker_Format_TDF;
}

// Per email thread Aug 22 2017 bpratt, mattc, Bruker's SvenB:
// The gas is nitrogen(14.0067 AMU) and the temperature is(according to Sven) assumed to be 305K.
static const double ccs_conversion_factor = 18509.863216340458;
static const double MolWeightGas = 14.0067;
static const double Temperature = 305;

double SpectrumList_Bruker::inverseK0ToCCS(double inverseK0, double mz, int charge) const
{
    double MolWeight = mz * abs(charge) + chemistry::Electron * charge;
    double ReducedMass = MolWeight * MolWeightGas / (MolWeight + MolWeightGas);
    double K0 = (inverseK0 == 0) ? 0 : (1.0 / inverseK0);
    double ccs = ccs_conversion_factor * abs(charge) / (sqrt(ReducedMass * Temperature) * K0);
    return ccs;    // in Angstrom^2
}

double SpectrumList_Bruker::ccsToInverseK0(double ccs, double mz, int charge) const
{
    double MolWeight = mz * abs(charge) + chemistry::Electron * charge;
    double ReducedMass = MolWeight * MolWeightGas / (MolWeight + MolWeightGas);
    double K0 = ccs_conversion_factor * abs(charge) / (sqrt(ReducedMass * Temperature) * ccs);
    return K0 == 0 ? 0 : 1 / K0;    // in Vs/cm^2
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
bool SpectrumList_Bruker::hasIonMobility() const { return false; }
bool SpectrumList_Bruker::hasPASEF() const { return false; }
bool SpectrumList_Bruker::canConvertInverseK0AndCCS() const { return false; }
double SpectrumList_Bruker::inverseK0ToCCS(double inverseK0, double mz, int charge) const {return 0;}
double SpectrumList_Bruker::ccsToInverseK0(double ccs, double mz, int charge) const {return 0;}
} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_BRUKER
