//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2017 Matt Chambers - Nashville, TN 37221
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

#pragma unmanaged
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "TimsData.hpp"
#include "sqlite3pp.h"


using namespace pwiz::util;
using namespace pwiz::msdata::detail::Bruker;
using namespace pwiz::vendor_api::Bruker;
namespace sqlite = sqlite3pp;


namespace {

InstrumentFamily translateInstrumentFamily(int instrumentFamilyId)
{
    switch (instrumentFamilyId)
    {
        case 1: return InstrumentFamily_OTOF;
        case 2: return InstrumentFamily_OTOFQ;
        case 6: return InstrumentFamily_maXis;
        case 7: return InstrumentFamily_impact;
        case 8: return InstrumentFamily_compact;
        case 9: return InstrumentFamily_timsTOF;
        case 512: return InstrumentFamily_FTMS;
        case 513: return InstrumentFamily_solariX;
        default: return InstrumentFamily_Unknown;
    }
}

FragmentationMode translateScanMode(int scanMode)
{
    switch (scanMode)
    {
        default:
        case 1:
            return FragmentationMode_CID;
            //return FragmentationMode_Unknown;

        case 2:
        case 8:
        case 9:
            return FragmentationMode_CID;

        case 3:
        case 4:
        case 5:
            return FragmentationMode_ISCID; // in-source or broadband CID
    }
}

InstrumentSource translateInstrumentSource(int instrumentSourceId)
{
    return (InstrumentSource) instrumentSourceId;
}

} // namespace


/*

PerSpectrumVariables:
Summation = 2
IsolationMass = 7
IsolationWidth = 8
DeviceTemp1 = 9
DeviceTemp2 = 10

PerSegmentVariables:
CorrectorFill = 13
DeviceReferenceTemp1 = 15
DeviceReferenceTemp2 = 16

ScanMode:
0 = MS
2 = MS/MS
4 = in-source CID
5 = broadband CID
8 = PASEF?
255 = unknown

AcquisitionMode:
1 = (axial or orthogonal) TOF, linear detection mode
2 = (axial or orthogonal) TOF, reflector detection mode
255 = unknown

*/


namespace pwiz {
namespace vendor_api {
namespace Bruker {


TimsDataImpl::TimsDataImpl(const string& rawpath, bool combineIonMobilitySpectra, int preferOnlyMsLevel, bool allowMsMsWithoutPrecursor)
    : tdfFilepath_((bfs::path(rawpath) / "analysis.tdf").string()),
      tdfStoragePtr_(new TimsBinaryData(rawpath)),
      tdfStorage_(*tdfStoragePtr_),
      combineSpectra_(combineIonMobilitySpectra),
      hasPASEFData_(false),
      preferOnlyMsLevel_(preferOnlyMsLevel),
      allowMsMsWithoutPrecursor_(allowMsMsWithoutPrecursor),
      currentFrameId_(-1)
{
    sqlite::database db(tdfFilepath_);

    double mzAcqRangeLower = 0, mzAcqRangeUpper = 0;

    sqlite::query properties(db, "SELECT Key, Value FROM GlobalMetadata");
    for (sqlite::query::iterator itr = properties.begin(); itr != properties.end(); ++itr)
    {
        string key, value;
        itr->getter() >> key >> value;
        if (key == "AcquisitionSoftware")
            acquisitionSoftware_.swap(value);
        else if (key == "AcquisitionSoftwareVersion")
            acquisitionSoftwareVersion_.swap(value);
        else if (key == "InstrumentFamily")
            instrumentFamily_ = translateInstrumentFamily(lexical_cast<int>(value));
        else if (key == "InstrumentRevision")
            instrumentRevision_ = lexical_cast<int>(value);
        else if (key == "InstrumentSourceType")
            instrumentSource_ = translateInstrumentSource(lexical_cast<int>(value));
        else if (key == "InstrumentSerialNumber")
            serialNumber_.swap(value);
        else if (key == "AcquisitionDateTime")
            acquisitionDateTime_.swap(value);
        else if (key == "OperatorName")
            operatorName_.swap(value);
        else if (key == "MzAcqRangeLower")
            mzAcqRangeLower = lexical_cast<double>(value);
        else if (key == "MzAcqRangeUpper")
            mzAcqRangeUpper = lexical_cast<double>(value);
    }

    // get frame count
    string queryMsFilter;
    switch (preferOnlyMsLevel_)
    {
        case 1:
            queryMsFilter = " WHERE MsMsType = 0"; // Skip MS2
            break;
        case 2:
            queryMsFilter = " WHERE MsMsType > 0"; // Skip MS1
            break;

        case 0:
        default:
            queryMsFilter = " WHERE Id >= 0"; // Accept all frames
            break;
    }

    string queryFrameCount = "SELECT COUNT(*) FROM Frames";
    size_t count = sqlite::query(db, queryFrameCount.c_str()).begin()->get<sqlite3_int64>(0);
    frames_.reserve(count);
    tic_.reset(new Chromatogram);
    bpc_.reset(new Chromatogram);
    tic_->times.reserve(count);
    tic_->intensities.reserve(count);
    bpc_->times.reserve(count);
    bpc_->intensities.reserve(count);

    if (!combineIonMobilitySpectra)
    {
        // get anticipated scan count
        std::string queryNonEmpty = queryFrameCount + queryMsFilter + " AND NumPeaks > 0";
        size_t countNonEmpty = sqlite::query(db, queryNonEmpty.c_str()).begin()->get<sqlite3_int64>(0);
        size_t nScans = sqlite::query(db, "SELECT MAX(NumScans) FROM Frames").begin()->get<sqlite3_int64>(0);
        spectra_.reserve(countNonEmpty * nScans);
    }

    std::string querySelect =
        "SELECT f.Id, Time, Polarity, ScanMode, MsMsType, MaxIntensity, SummedIntensities, NumScans, NumPeaks, "
        "Parent, TriggerMass, IsolationWidth, PrecursorCharge, CollisionEnergy "
        "FROM Frames f "
        "LEFT JOIN FrameMsMsInfo info ON f.Id=info.Frame " +
        queryMsFilter + 
        " ORDER BY Id"; // we currently depend on indexing the frames_ vector by Id (which so far has always been sorted by time)
    sqlite::query q(db, querySelect.c_str());

    for (sqlite::query::iterator itr = q.begin(); itr != q.end(); ++itr)
    {
        sqlite::query::rows row = *itr;
        int idx = -1;
        int64_t frameId = row.get<sqlite3_int64>(++idx);
        double rt = row.get<double>(++idx);
        IonPolarity polarity = row.get<string>(++idx) == "+" ? IonPolarity_Positive : IonPolarity_Negative;
        int scanMode = row.get<int>(++idx);
        int msmsType = row.get<int>(++idx);

        int msLevel;
        switch (msmsType)
        {
            case 0: msLevel = 1; break; // MS1
            case 2: msLevel = 2; break; // MRM
            case 8: msLevel = 2; break; // PASEF
            case 9: msLevel = 2; break; // DIA
            default: throw runtime_error("Unhandled msmsType: " + lexical_cast<string>(msmsType));
        }

        double bpi = row.get<double>(++idx);
        double tic = row.get<double>(++idx);

        tic_->times.push_back(rt);
        bpc_->times.push_back(rt);
        tic_->intensities.push_back(tic);
        bpc_->intensities.push_back(bpi);

        int numScans = row.get<int>(++idx);
        int numPeaks = row.get<int>(++idx);
        if (numPeaks == 0)
            continue;

        optional<uint64_t> parentId(row.get<optional<sqlite3_int64> >(++idx));
        optional<double> precursorMz(row.get<optional<double> >(++idx));
        optional<double> isolationWidth(row.get<optional<double> >(++idx));
        optional<int> precursorCharge(row.get<optional<int> >(++idx));
        optional<double> collisionEnergy(row.get<optional<double> >(++idx));

        TimsFramePtr frame = boost::make_shared<TimsFrame>(*this, frameId,
                                         msLevel, rt,
                                         mzAcqRangeLower, mzAcqRangeUpper,
                                         tic, bpi,
                                         polarity, scanMode, numScans,
                                         parentId, precursorMz,
                                         isolationWidth, precursorCharge);
        frames_[frameId] = frame;
    }

    hasPASEFData_ = db.has_table("PasefFrameMsMsInfo");
    if (hasPASEFData_ && preferOnlyMsLevel_ != 1)
    {
        sqlite::query q(db, "SELECT Frame, ScanNumBegin, ScanNumEnd, IsolationMz, IsolationWidth, CollisionEnergy, MonoisotopicMz, Charge, ScanNumber, Intensity, Parent "
                            "FROM PasefFrameMsMsInfo f, Precursors p where p.id=f.precursor "
                            "ORDER BY Frame, ScanNumBegin");
        for (sqlite::query::iterator itr = q.begin(); itr != q.end(); ++itr)
        {
            sqlite::query::rows row = *itr;
            int idx = -1;
            int64_t frameId = row.get<sqlite3_int64>(++idx);

            auto findItr = frames_.find(frameId);
            if (findItr == frames_.end()) // numPeaks == 0, but sometimes still shows up in PasefFrameMsMsInfo!?
                continue;
            auto& frame = findItr->second;

            frame->pasef_precursor_info_.emplace_back(new PasefPrecursorInfo);
            PasefPrecursorInfo& info = *frame->pasef_precursor_info_.back();

            info.scanBegin = row.get<int>(++idx);
            info.scanEnd = row.get<int>(++idx) - 1; // scan end in TDF is exclusive, but in pwiz is inclusive

            info.isolationMz = row.get<double>(++idx);
            info.isolationWidth = row.get<double>(++idx);
            info.collisionEnergy = row.get<double>(++idx);
            info.monoisotopicMz = row.get<double>(++idx);
            info.charge = row.get<int>(++idx);
            info.avgScanNumber = row.get<double>(++idx);
            info.intensity = row.get<double>(++idx);
        }
    }

    // when combining ion mobility spectra, spectra array is filled after querying PASEF info
    if (combineIonMobilitySpectra)
    {
        for (const auto& kvp : frames_)
        {
            const auto& frame = kvp.second;

            if (!frame->pasef_precursor_info_.empty())
            {
                for (const auto& precursor : frame->pasef_precursor_info_)
                {
                    spectra_.emplace_back(boost::make_shared<TimsSpectrumCombinedPASEF>(frame, precursor->scanBegin, precursor->scanEnd, *precursor));
                }
            }
            else // MS1 or non-PASEF MS2
            {
                spectra_.emplace_back(boost::make_shared<TimsSpectrumCombinedNonPASEF>(frame, 0, frame->numScans_ - 1));
            }
        }
    }
    else
    {
        size_t scanIndex = 0;

        for (const auto& kvp : frames_)
        {
            const auto& frame = kvp.second;

            auto& scanIndexByScanNumber = frame->scanIndexByScanNumber_;
            scanIndexByScanNumber.insert(make_pair(0, scanIndex));

            if (frame->msLevel_ == 1) // MS1
            {
                int numScans = frame->numScans();
                for (int i = 0; i < numScans; ++i, ++scanIndex)
                {
                    spectra_.emplace_back(boost::make_shared<TimsSpectrumNonPASEF>(frame, i));
                }
            }
            else // MS2
            {
                // if no PASEF info, add all MS2s (I don't know if this can happen in real data, but it doesn't hurt to check)
                if (frame->pasef_precursor_info_.empty())
                {
                    for (int i = 0; i < frame->numScans(); ++i, ++scanIndex)
                        spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, TimsSpectrum::empty_));
                }
                else if (allowMsMsWithoutPrecursor_)
                {
                    for (size_t p = 0; p < frame->pasef_precursor_info_.size(); ++p)
                    {
                        const auto& precursor = frame->pasef_precursor_info_[p];

                            // add MS2s that don't have PASEF info (between last precursor's scanEnd and this precursor's scanBegin)
                            if (p > 0)
                            {
                                const auto& lastPrecursor = frame->pasef_precursor_info_[p - 1];
                                for (int i = lastPrecursor->scanEnd + 1; i < precursor->scanBegin; ++i, ++scanIndex)
                                {
                                    spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, TimsSpectrum::empty_));
                                }
                            }
                            else
                            {
                                for (int i = 0; i < precursor->scanBegin; ++i, ++scanIndex)
                                {
                                    spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, TimsSpectrum::empty_));
                                }
                            }

                        // add MS2s for this precursor
                        for (int i = precursor->scanBegin; i <= precursor->scanEnd; ++i, ++scanIndex)
                        {
                            spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, *precursor));
                        }
                    }

                    
                    // add MS2s from the last precursor's scanEnd to the frame's numScans
                    const auto& lastPrecursor = frame->pasef_precursor_info_.back();
                    for (int i = lastPrecursor->scanEnd + 1; i < frame->numScans(); ++i, ++scanIndex)
                    {
                        spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, TimsSpectrum::empty_));
                    }
                }
                else
                {
                    for (size_t p = 0; p < frame->pasef_precursor_info_.size(); ++p)
                    {
                        const auto& precursor = frame->pasef_precursor_info_[p];
                        scanIndexByScanNumber[precursor->scanBegin] = scanIndex;

                        // add MS2s for this precursor
                        for (int i = precursor->scanBegin; i <= precursor->scanEnd; ++i, ++scanIndex)
                        {
                            spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, *precursor));
                        }
                    }
                }
            }
        }
    }
}


bool TimsDataImpl::hasMSData() const { return true; }
bool TimsDataImpl::hasLCData() const { return false; }
bool TimsDataImpl::hasPASEFData() const { return hasPASEFData_; }
size_t TimsDataImpl::getMSSpectrumCount() const { return spectra_.size(); }
MSSpectrumPtr TimsDataImpl::getMSSpectrum(int scan, DetailLevel detailLevel) const { return spectra_[scan - 1]; }

pair<size_t, size_t> TimsDataImpl::getFrameScanPair(int scan) const
{
    const auto& s = spectra_[scan - 1];
    return make_pair(s->frame_.frameId(), s->scanBegin()+1);
}

size_t TimsDataImpl::getSpectrumIndex(int frame, int scan) const
{
    auto findItr = frames_.find(frame);
    if (findItr == frames_.end())
        throw out_of_range("[TimsData::getSpectrumIndex] invalid frame index");

    if (findItr->second->scanIndexByScanNumber_.empty())
        throw runtime_error("[TimsData::getSpectrumIndex] cannot get index from frame/scan in combineIonMobilitySpectra mode");

    auto scanBlockIndexPair = findItr->second->scanIndexByScanNumber_.upper_bound(scan); --scanBlockIndexPair;
    int scanBlockStartScan = scanBlockIndexPair->first;
    size_t scanBlockStartIndex = scanBlockIndexPair->second;
    return scanBlockStartIndex + (scan - scanBlockStartScan) - 1;
}


size_t TimsDataImpl::getLCSourceCount() const { return 0; }
size_t TimsDataImpl::getLCSpectrumCount(int source) const { return 0; }
LCSpectrumSourcePtr TimsDataImpl::getLCSource(int source) const { return 0; }
LCSpectrumPtr TimsDataImpl::getLCSpectrum(int source, int scan) const { return LCSpectrumPtr(); }

ChromatogramPtr TimsDataImpl::getTIC() const { return tic_; }
ChromatogramPtr TimsDataImpl::getBPC() const { return bpc_; }

std::string TimsDataImpl::getOperatorName() const { return operatorName_; }
std::string TimsDataImpl::getAnalysisName() const { return ""; }
boost::local_time::local_date_time TimsDataImpl::getAnalysisDateTime() const { return parse_date_time("%Y-%m-%dT%H:%M:%S%Q", acquisitionDateTime_); }
std::string TimsDataImpl::getSampleName() const { return ""; }
std::string TimsDataImpl::getMethodName() const { return ""; }
InstrumentFamily TimsDataImpl::getInstrumentFamily() const { return instrumentFamily_; }
int TimsDataImpl::getInstrumentRevision() const { return instrumentRevision_; }
std::string TimsDataImpl::getInstrumentDescription() const { return ""; }
std::string TimsDataImpl::getInstrumentSerialNumber() const { return serialNumber_; }
InstrumentSource TimsDataImpl::getInstrumentSource() const { return instrumentSource_; }
std::string TimsDataImpl::getAcquisitionSoftware() const { return acquisitionSoftware_; }
std::string TimsDataImpl::getAcquisitionSoftwareVersion() const { return acquisitionSoftwareVersion_; }

///
/// Reimplementations of TimsBinaryData functions to cache entire frames while dealing with single spectrum
///

/// Read all scans from a single frame. Not thread-safe.

const ::timsdata::FrameProxy& TimsDataImpl::readFrame(
    int64_t frame_id)     //< frame index
{
    if (frame_id != currentFrameId_)
    {
        const auto findItr = frames_.find(frame_id);
        if (findItr == frames_.end())
            throw out_of_range("[TimsData::readFrame] invalid frame index");
        const auto framePtr = findItr->second;
        return tdfStorage_.readScans(currentFrameId_ = frame_id, 0, framePtr->numScans_, true);
    }
    return tdfStorage_.currentFrameProxy();
}

TimsFrame::TimsFrame(TimsDataImpl& timsDataImpl, int64_t frameId,
                     int msLevel, double rt,
                     double startMz, double endMz,
                     double tic, double bpi,
                     IonPolarity polarity, int scanMode, int numScans,
                     const optional<uint64_t>& parentId,
                     const optional<double>& precursorMz,
                     const optional<double>& isolationWidth,
                     const optional<int>& precursorCharge)
    : frameId_(frameId), msLevel_(msLevel), rt_(rt), parentId_(parentId), tic_(tic), bpi_(bpi),
      numScans_(numScans),
      polarity_(polarity), scanRange_(startMz, endMz),
      precursorMz_(precursorMz), scanMode_(scanMode),
      isolationWidth_(isolationWidth), chargeState_(precursorCharge),
      timsDataImpl_(timsDataImpl), oneOverK0_(numScans)
{
    vector<double> scanNumbers(numScans);
    for(int i=1; i <= numScans; ++i)
        scanNumbers[i-1] = i;
    timsDataImpl_.tdfStorage_.scanNumToOneOverK0(frameId, scanNumbers, oneOverK0_);
}

const PasefPrecursorInfo TimsSpectrum::empty_;

bool TimsSpectrum::hasLineData() const { return getLineDataSize() > 0; }
bool TimsSpectrum::hasProfileData() const { return false; }
size_t TimsSpectrum::getLineDataSize() const { return frame_.timsDataImpl_.readFrame(frame_.frameId_).getNbrPeaks(scanBegin_); }
size_t TimsSpectrum::getProfileDataSize() const { return 0; }

void TimsSpectrum::getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    auto& storage = frame_.timsDataImpl_;
    const auto& frameProxy = storage.readFrame(frame_.frameId_);
    auto intensityCounts = frameProxy.getScanY(scanBegin_);
    auto count = intensityCounts.size();

    // Empty scans are not uncommon, save some heap thrashing
    if (count == 0)
    {
        mz.resize(0);
        intensities.resize(0);
        return;
    }

    auto scanMZs = frameProxy.getScanMZs(scanBegin_);
    intensities.resize_no_initialize(intensityCounts.size());
    mz.resize_no_initialize(intensityCounts.size());

    double *m = &mz[0];
    double *inten = &intensities[0];
    for (size_t i = 0; i < count;)
    {
       *m++ = scanMZs[i];
        *inten++ = intensityCounts[i++];
    }
}

void TimsSpectrum::getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    // TDF does not support profile data
    mz.clear();
    intensities.clear();
    return;
}

double TimsSpectrum::oneOverK0() const
{
    if (HasPasefPrecursorInfo()) // combination mode must be on
    {
        vector<double> avgScanNumber(1, GetPasefPrecursorInfo().avgScanNumber);
        vector<double> avgOneOverK0(1);
        frame_.timsDataImpl_.tdfStorage_.scanNumToOneOverK0(frame_.frameId_, avgScanNumber, avgOneOverK0);
        return avgOneOverK0[0];
    }
    else if (!isCombinedScans())
        return frame_.oneOverK0_[scanBegin_];
    else
        return 0; // no mobility value for non-PASEF merged spectra
}

namespace {
    template<typename T>
    struct SortByOther
    {
        const vector<T> & value_vector;

        SortByOther(const vector<T> & val_vec) :
            value_vector(val_vec) {}

        bool operator()(int i1, int i2) const
        {
            return value_vector[i1] < value_vector[i2];
        }
    };
}

void TimsSpectrum::getCombinedSpectrumData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, pwiz::util::BinaryData<double>& mobilities) const
{
    auto& storage = frame_.timsDataImpl_.tdfStorage_;
    
    const auto& frameProxy = storage.readScans(frame_.frameId_, scanBegin_, scanEnd() + 1, false);
    vector<double> mzIndicesAsDoubles;
    mzIndicesAsDoubles.reserve(frameProxy.getTotalNbrPeaks());
    int range = scanEnd() - scanBegin_;
    for (int i = 0; i <= range; ++i)
    {
        auto mzIndices = frameProxy.getScanX(i);
        for (size_t i = 0; i < mzIndices.size(); ++i)
            mzIndicesAsDoubles.push_back(mzIndices[i]);
    }
    storage.indexToMz(frame_.frameId_, mzIndicesAsDoubles, mz);

    intensities.reserve(frameProxy.getTotalNbrPeaks());
    for (int i = 0; i <= range; ++i)
    {
        auto intensityCounts = frameProxy.getScanY(i);
        for (size_t i = 0; i < intensityCounts.size(); ++i)
            intensities.push_back(intensityCounts[i]);
    }

    mzIndicesAsDoubles.clear();
    for (int i = 0; i < frameProxy.getNbrScans(); ++i)
        for (int j = 0; j < frameProxy.getNbrPeaks(i); ++j)
            mzIndicesAsDoubles.push_back(scanBegin_ + i);
    storage.scanNumToOneOverK0(frame_.frameId_, mzIndicesAsDoubles, mobilities);
    
    // sort an array of indices by m/z; these indices are used to reorder all 3 arrays
    vector<int> indices(mz.size());
    for (int i = 0; i < mz.size(); ++i)
        indices[i] = i;
    std::sort(indices.begin(), indices.end(), SortByOther<double>(mz));
    pwiz::util::BinaryData<double> mzTmp(mz.size()), intensityTmp(mz.size()), mobilityTmp(mz.size());
    for (int i = 0; i < mz.size(); ++i)
    {
        mzTmp[i] = mz[indices[i]];
        intensityTmp[i] = intensities[indices[i]];
        mobilityTmp[i] = mobilities[indices[i]];
    }
    swap(mzTmp, mz);
    swap(intensityTmp, intensities);
    swap(mobilityTmp, mobilities);

    // add jitter to identical m/z values (which come from different mobility bins)
    for (size_t i = 1; i < mz.size(); ++i)
        if (mz[i - 1] == mz[i])
        {
            size_t start_i = i - 1;
            for (; i < mz.size() && mz[start_i] == mz[i]; ++i)
                mz[i] += 1e-8 * (i-start_i);
        }
}

size_t TimsSpectrum::getCombinedSpectrumDataSize() const
{
    const auto& frameProxy = frame_.timsDataImpl_.tdfStorage_.readScans(frame_.frameId_, scanBegin_, scanEnd() + 1, false);
    return frameProxy.getTotalNbrPeaks();
}

IntegerSet TimsSpectrum::getMergedScanNumbers() const
{
    return IntegerSet(scanBegin_, scanEnd());
}

int TimsSpectrum::getMSMSStage() const { return frame_.msLevel_; }
double TimsSpectrum::getRetentionTime() const { return frame_.rt_; }

void TimsSpectrum::getIsolationData(std::vector<double>& isolatedMZs, std::vector<IsolationMode>& isolationModes) const
{
    isolatedMZs.clear();
    isolationModes.clear();

    if (HasPasefPrecursorInfo())
    {
        isolatedMZs.resize(1, GetPasefPrecursorInfo().isolationMz);
        isolationModes.resize(1, IsolationMode_On);
    }
    else if (frame_.precursorMz_.is_initialized())
    {
        isolatedMZs.resize(1, frame_.precursorMz_.get());
        isolationModes.resize(1, IsolationMode_On);
    }
}

void TimsSpectrum::getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const
{
    fragmentedMZs.clear();
    fragmentationModes.clear();

    if (HasPasefPrecursorInfo())
    {
        double mz = GetPasefPrecursorInfo().monoisotopicMz;
        if (mz <= 0)
            mz = GetPasefPrecursorInfo().isolationMz;
        fragmentedMZs.resize(1, mz);
        fragmentationModes.resize(1, translateScanMode(frame_.scanMode_));
    }
    else if (frame_.precursorMz_.is_initialized())
    {
        fragmentedMZs.resize(1, frame_.precursorMz_.get());
        fragmentationModes.resize(1, translateScanMode(frame_.scanMode_));
    }
}

IonPolarity TimsSpectrum::getPolarity() const { return frame_.polarity_; }

std::pair<double, double> TimsSpectrum::getScanRange() const
{
    return frame_.scanRange_;
}

int TimsSpectrum::getChargeState() const
{
    if (HasPasefPrecursorInfo())
        return GetPasefPrecursorInfo().charge;
    else
        return frame_.chargeState_.get_value_or(0);
}

double TimsSpectrum::getIsolationWidth() const
{
    if (HasPasefPrecursorInfo())
        return GetPasefPrecursorInfo().isolationWidth;
    else
        return frame_.isolationWidth_.get_value_or(0);
}


struct PWIZ_API_DECL Baf2SqlSpectrumParameterList : public MSSpectrumParameterList
{    
    virtual size_t size() const { return 0; }
    virtual value_type operator[] (size_t index) const { throw range_error("[Baf2SqlSpectrumParameterList] parameter index out of range"); }
    virtual const_iterator begin() const { return const_iterator(); }
    virtual const_iterator end() const { return const_iterator(); }
};


MSSpectrumParameterListPtr TimsSpectrum::parameters() const
{
    return MSSpectrumParameterListPtr(new Baf2SqlSpectrumParameterList());
}


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz
