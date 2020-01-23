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


TimsDataImpl::TimsDataImpl(const string& rawpath, bool combineIonMobilitySpectra, int preferOnlyMsLevel, bool allowMsMsWithoutPrecursor, const vector<chemistry::MzMobilityWindow>& isolationMzFilter)
    : tdfFilepath_((bfs::path(rawpath) / "analysis.tdf").string()),
      combineSpectra_(combineIonMobilitySpectra),
      hasPASEFData_(false),
      preferOnlyMsLevel_(preferOnlyMsLevel),
      allowMsMsWithoutPrecursor_(allowMsMsWithoutPrecursor),
      isolationMzFilter_(isolationMzFilter),
      currentFrameId_(-1),
      tdfStoragePtr_(new TimsBinaryData(rawpath)),
      tdfStorage_(*tdfStoragePtr_)
{
    tims_set_num_threads(4);
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

    ticMs1_.reset(new Chromatogram);
    bpcMs1_.reset(new Chromatogram);
    ticMs1_->times.reserve(count / 2);
    ticMs1_->intensities.reserve(count / 2);
    bpcMs1_->times.reserve(count / 2);
    bpcMs1_->intensities.reserve(count / 2);

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
        "Parent, TriggerMass, IsolationWidth, PrecursorCharge, CollisionEnergy, TimsCalibration-1 "
        "FROM Frames f "
        "LEFT JOIN FrameMsMsInfo info ON f.Id=info.Frame " +
        queryMsFilter + 
        " ORDER BY Id"; // we currently depend on indexing the frames_ vector by Id (which so far has always been sorted by time)
    sqlite::query q(db, querySelect.c_str());

    int maxNumScans = 0;
    vector<TimsFramePtr> representativeFrameByCalibrationIndex; // the first frame for each calibration index
    vector<map<double, int>> scanNumberByOneOverK0ByCalibrationIndex;

    for (sqlite::query::iterator itr = q.begin(); itr != q.end(); ++itr)
    {
        sqlite::query::rows row = *itr;
        int idx = -1;
        int64_t frameId = row.get<sqlite3_int64>(++idx);
        double rt = row.get<double>(++idx);
        IonPolarity polarity = row.get<string>(++idx) == "+" ? IonPolarity_Positive : IonPolarity_Negative;
        int scanMode = row.get<int>(++idx);
        MsMsType msmsType = (MsMsType) row.get<int>(++idx);

        double bpi = row.get<double>(++idx);
        double tic = row.get<double>(++idx);

        tic_->times.push_back(rt);
        bpc_->times.push_back(rt);
        tic_->intensities.push_back(tic);
        bpc_->intensities.push_back(bpi);

        if (msmsType == MsMsType::MS1)
        {
            ticMs1_->times.push_back(rt);
            bpcMs1_->times.push_back(rt);
            ticMs1_->intensities.push_back(tic);
            bpcMs1_->intensities.push_back(bpi);
        }

        int numScans = row.get<int>(++idx);
        int numPeaks = row.get<int>(++idx);
        if (numPeaks == 0)
            continue;

        maxNumScans = max(maxNumScans, numScans);

        optional<uint64_t> parentId(row.get<optional<sqlite3_int64> >(++idx));
        optional<double> precursorMz(row.get<optional<double> >(++idx));
        optional<double> isolationWidth(row.get<optional<double> >(++idx));
        optional<int> precursorCharge(row.get<optional<int> >(++idx));
        optional<double> collisionEnergy(row.get<optional<double> >(++idx));

        int calibrationIndex = row.get<int>(++idx);
        bool newCalibrationIndex = oneOverK0ByScanNumberByCalibration_.size() <= calibrationIndex;
        if (newCalibrationIndex)
        {
            oneOverK0ByScanNumberByCalibration_.resize(calibrationIndex + 1);
            representativeFrameByCalibrationIndex.resize(calibrationIndex + 1);
            scanNumberByOneOverK0ByCalibrationIndex.resize(calibrationIndex + 1);
        }

        TimsFramePtr frame = boost::make_shared<TimsFrame>(*this, frameId,
                                         msmsType, rt,
                                         mzAcqRangeLower, mzAcqRangeUpper,
                                         tic, bpi,
                                         polarity, scanMode, numScans,
                                         parentId, precursorMz,
                                         isolationWidth, precursorCharge,
                                         calibrationIndex, oneOverK0ByScanNumberByCalibration_[calibrationIndex]);
        frames_[frameId] = frame;

        if (newCalibrationIndex)
            representativeFrameByCalibrationIndex[calibrationIndex] = frame;
    }

    if (frames_.empty())
        return;

    // pre-cache scan number to 1/k0 mapping for each calibration (and also the inverse, for 1/k0 filtering, which is why it has to be done here instead of on-demand)
    vector<double> scanNumbers(maxNumScans+1);
    for (int i = 0; i <= maxNumScans; ++i)
        scanNumbers[i] = i;
    for (size_t i = 0; i < oneOverK0ByScanNumberByCalibration_.size(); ++i)
    {
        vector<double>& oneOverK0 = oneOverK0ByScanNumberByCalibration_[i];
        tdfStorage_.scanNumToOneOverK0(representativeFrameByCalibrationIndex[i]->frameId_, scanNumbers, oneOverK0);

        for (int j = 0; j < scanNumbers.size(); ++j)
            scanNumberByOneOverK0ByCalibrationIndex[i][oneOverK0[j]] = scanNumbers[j];
    }

    bool isDdaPasef = db.has_table("PasefFrameMsMsInfo") && sqlite::query(db, "SELECT COUNT(*) FROM PasefFrameMsMsInfo").begin()->get<int>(0) > 0;
    bool isDiaPasef = !isDdaPasef && db.has_table("DiaFrameMsMsInfo") && sqlite::query(db, "SELECT COUNT(*) FROM DiaFrameMsMsInfo").begin()->get<int>(0) > 0;
    hasPASEFData_ = isDdaPasef | isDiaPasef;

    string pasefIsolationMzFilter;
    if (hasPASEFData_ && !isolationMzFilter_.empty())
    {
        vector<string> isolationMzFilterStrs;
        for (int i=1; i <= isolationMzFilter_.size(); ++i)
        {
            const auto& mzMobilityWindow = isolationMzFilter_[i-1];
            if (!mzMobilityWindow.mz)
                continue;
            string mzStr = lexical_cast<string>(mzMobilityWindow.mz.get());
            isolationMzFilterStrs.push_back(mzStr + " > IsolationMz-IsolationWidth/2 AND " + mzStr + " < IsolationMz+IsolationWidth/2");
        }
        if (!isolationMzFilterStrs.empty())
            pasefIsolationMzFilter = " WHERE (" + bal::join(isolationMzFilterStrs, ") OR (") + ") ";
    }

    if (hasPASEFData_ && preferOnlyMsLevel_ != 1)
    {
        if (isDdaPasef)
        {
            string querySql = "SELECT Frame, ScanNumBegin, ScanNumEnd, IsolationMz, IsolationWidth, CollisionEnergy, MonoisotopicMz, Charge, ScanNumber, Intensity, Parent "
                              "FROM PasefFrameMsMsInfo f "
                              "JOIN Precursors p ON p.id=f.precursor " +
                              pasefIsolationMzFilter +
                              "ORDER BY Frame, ScanNumBegin";
            sqlite::query q(db, querySql.c_str());
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
        else // DiaPasef
        {
            string querySql = "SELECT Frame, MIN(ScanNumBegin), MAX(ScanNumEnd), IsolationMz, IsolationWidth, AVG(CollisionEnergy), f.WindowGroup "
                              "FROM DiaFrameMsMsInfo f "
                              "JOIN DiaFrameMsMsWindows w ON w.WindowGroup=f.WindowGroup " +
                              pasefIsolationMzFilter +
                              "GROUP BY Frame, IsolationMz, IsolationWidth "
                              "ORDER BY Frame, ScanNumBegin";
            sqlite::query q(db, querySql.c_str());
            DiaPasefIsolationInfo info;
            for (sqlite::query::iterator itr = q.begin(); itr != q.end(); ++itr)
            {
                sqlite::query::rows row = *itr;
                const int colFrameId = 0;
                const int colScanBegin = 1;
                const int colScanEnd = 2;
                const int colIsolationMz = 3;
                const int colIsolationWidth = 4;
                const int colCE = 5;
                const int colWindowGroup = 6;

                int64_t frameId = row.get<sqlite3_int64>(colFrameId);

                auto findItr = frames_.find(frameId);
                if (findItr == frames_.end()) // numPeaks == 0, but sometimes still shows up in PasefFrameMsMsInfo!?
                    continue;
                auto& frame = findItr->second;

                int scanBegin = row.get<int>(colScanBegin);
                int scanEnd = row.get<int>(colScanEnd) - 1; // scan end in TDF is exclusive, but in pwiz is inclusive

                info.isolationMz = row.get<double>(colIsolationMz);
                info.isolationWidth = row.get<double>(colIsolationWidth);
                info.collisionEnergy = row.get<double>(colCE);
                int windowGroup = row.get<int>(colWindowGroup);

                info.numScans = 1 + scanEnd - scanBegin;

                if (!isolationMzFilter_.empty())
                {
                    // swap values for min/max below
                    int filteredScanEnd = scanBegin;
                    int filteredScanBegin = scanEnd;

                    double isolationHalfWidth = info.isolationWidth / 2;
                    double isolationLowerBound = info.isolationMz - isolationHalfWidth;
                    double isolationUpperBound = info.isolationMz + isolationHalfWidth;

                    // the scan range is set to the superset of all mobility filters that match the frame
                    for (const auto& mzMobilityWindow : isolationMzFilter_)
                    {
                        if (mzMobilityWindow.mz.is_initialized() && (mzMobilityWindow.mz.get() < isolationLowerBound || mzMobilityWindow.mz.get() > isolationUpperBound))
                            continue;

                        // if any matching m/z filter has no mobility filter, then all scans must be included
                        if (!mzMobilityWindow.mobilityBounds)
                        {
                            filteredScanBegin = scanBegin;
                            filteredScanEnd = scanEnd;
                            break;
                        }

                        const map<double, int>& scanNumberByOneOverK0 = scanNumberByOneOverK0ByCalibrationIndex[frame->calibrationIndex_];

                        // 1/k0 is inverse to scan number (lowest scan number is highest 1/k0)
                        auto scanNumLowerBoundItr = scanNumberByOneOverK0.upper_bound(mzMobilityWindow.mobilityBounds.get().second); --scanNumLowerBoundItr;
                        auto scanNumUpperBoundItr = scanNumberByOneOverK0.upper_bound(mzMobilityWindow.mobilityBounds.get().first); if (scanNumUpperBoundItr != scanNumberByOneOverK0.begin()) --scanNumUpperBoundItr;
                        filteredScanBegin = min(filteredScanBegin, scanNumLowerBoundItr->second);
                        filteredScanEnd = max(filteredScanEnd, scanNumUpperBoundItr->second);
                        /*cout << "Filtering frame " << frame->frameId_ << " for m/z " << mzMobilityWindow.mz <<
                                " and 1/k0 [" << mzMobilityWindow.mobilityBounds.get().second << "," << mzMobilityWindow.mobilityBounds.get().first << "]" <<
                                " (scans [" << scanBegin << "," << scanEnd << "])\n";*/
                    }

                    // if no mobility filters matched, the frame will be filtered out (windowGroup remains unset)
                    if (filteredScanBegin > filteredScanEnd)
                        continue;

                    scanBegin = filteredScanBegin;
                    scanEnd = filteredScanEnd;
                    info.numScans = 1 + scanEnd - scanBegin;
                }

                frame->windowGroup_ = windowGroup; 
                frame->diaPasefIsolationInfoByScanNumber_[scanBegin] = info;
            }
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
            else if (isDiaPasef && frame->msLevel_ > 1)
            {
                for (const auto& info : frame->diaPasefIsolationInfoByScanNumber_)
                {
                    spectra_.emplace_back(boost::make_shared<TimsSpectrumCombinedPASEF>(frame, info.first, info.first + info.second.numScans - 1, TimsSpectrum::empty_));
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
                if (isDiaPasef)
                {
                    for (const auto& info : frame->diaPasefIsolationInfoByScanNumber_)
                    {
                        scanIndexByScanNumber[info.first] = scanIndex;
                        for (int i = info.first; i < info.first + info.second.numScans; ++i, ++scanIndex)
                            spectra_.emplace_back(boost::make_shared<TimsSpectrumPASEF>(frame, i, TimsSpectrum::empty_));
                    }
                }
                // if no PASEF info, add all MS2s (I don't know if this can happen in real data, but it doesn't hurt to check)
                else if (frame->pasef_precursor_info_.empty())
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

FrameScanRange TimsDataImpl::getFrameScanPair(int scan) const
{
    const auto& s = spectra_[scan - 1];
    return FrameScanRange { (int) s->frame_.frameId(), s->scanBegin() + 1, s->scanEnd() + 1 };
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

ChromatogramPtr TimsDataImpl::getTIC(bool ms1Only) const { return ms1Only ? ticMs1_ : tic_; }
ChromatogramPtr TimsDataImpl::getBPC(bool ms1Only) const { return ms1Only ? bpcMs1_ : bpc_; }

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
                     MsMsType msmsType, double rt,
                     double startMz, double endMz,
                     double tic, double bpi,
                     IonPolarity polarity, int scanMode, int numScans,
                     const optional<uint64_t>& parentId,
                     const optional<double>& precursorMz,
                     const optional<double>& isolationWidth,
                     const optional<int>& precursorCharge,
                     int calibrationIndex,
                     const vector<double>& oneOverK0)
    : frameId_(frameId), msmsType_(msmsType), rt_(rt), parentId_(parentId), tic_(tic), bpi_(bpi),
      numScans_(numScans),
      polarity_(polarity), scanRange_(startMz, endMz),
      precursorMz_(precursorMz), scanMode_(scanMode),
      isolationWidth_(isolationWidth), chargeState_(precursorCharge),
      calibrationIndex_(calibrationIndex),
      timsDataImpl_(timsDataImpl),
      oneOverK0_(oneOverK0)
{
    switch (msmsType_)
    {
        case MsMsType::MS1: msLevel_ = 1; break; // MS1
        case MsMsType::MRM: msLevel_ = 2; break; // MRM
        case MsMsType::DDA_PASEF: msLevel_ = 2; break; // PASEF
        case MsMsType::DIA_PASEF: msLevel_ = 2; break; // DIA
        default: throw runtime_error("Unhandled msmsType: " + lexical_cast<string>((int) msmsType_));
    }
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
    if (!isCombinedScans())
    {
        return frame_.oneOverK0_[scanBegin_];
    }
    else if (HasPasefPrecursorInfo()) // combination mode must be on
    {
        double avgScanNumber = GetPasefPrecursorInfo().avgScanNumber;
        size_t ceilIndex = min(frame_.oneOverK0_.size()-1, (size_t) ceil(avgScanNumber));
        size_t floorIndex = (size_t) floor(avgScanNumber);
        double tmp;
        double fraction = modf(avgScanNumber, &tmp);
        //cout << scanBegin_ << " " << avgScanNumber << " " << ceilIndex << " " << floorIndex << " " << fraction << " " << frame_.oneOverK0_[floorIndex] << " " << frame_.oneOverK0_[ceilIndex] << endl;
        return (1-fraction) * frame_.oneOverK0_[floorIndex] + fraction * frame_.oneOverK0_[ceilIndex];
    }
    else
    {
        return (frame_.oneOverK0_[scanBegin_] + frame_.oneOverK0_[scanEnd()]) / 2;
    }
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

void TimsSpectrum::getCombinedSpectrumData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, pwiz::util::BinaryData<double>& mobilities, bool sortAndJitter) const
{
    auto& storage = frame_.timsDataImpl_.tdfStorage_;
    
    const auto& frameProxy = storage.readScans(frame_.frameId_, scanBegin_, scanEnd() + 1, false);
    vector<double> mzIndicesAsDoubles;
    mzIndicesAsDoubles.reserve(frameProxy.getTotalNbrPeaks());
    int range = scanEnd() - scanBegin_;
    for (int i = 0; i <= range; ++i)
    {
        auto mzIndices = frameProxy.getScanX(i);
        for (size_t j = 0; j < mzIndices.size(); ++j)
            mzIndicesAsDoubles.push_back(mzIndices[j]);
    }
    storage.indexToMz(frame_.frameId_, mzIndicesAsDoubles, mz);

    intensities.resize(frameProxy.getTotalNbrPeaks());
    mobilities.resize(intensities.size());
    double* itr = &intensities[0];
    double* itr2 = &mobilities[0];
    for (int i = 0; i <= range; ++i)
    {
        auto intensityCounts = frameProxy.getScanY(i);
        for (size_t j = 0; j < intensityCounts.size(); ++j, ++itr, ++itr2)
        {
            *itr = intensityCounts[j];
            *itr2 = frame_.oneOverK0_[scanBegin_ + i];
            /*if (*itr2 < 0.0001 || *itr2 > 2)
                throw runtime_error("bad 1/k0 value at i=" + lexical_cast<string>(i) +
                                    " j=" + lexical_cast<string>(j) +
                                    " scanBegin_=" + lexical_cast<string>(scanBegin_));*/
        }
    }

    if (!sortAndJitter)
        return;

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

void TimsSpectrum::getIsolationData(std::vector<IsolationInfo>& isolationInfo) const
{
    isolationInfo.clear();

    if (HasPasefPrecursorInfo())
    {
        const auto& info = GetPasefPrecursorInfo();
        isolationInfo.resize(1, IsolationInfo{ info.isolationMz, IsolationMode_On, info.collisionEnergy });
    }
    else if (!frame_.diaPasefIsolationInfoByScanNumber_.empty())
    {
        const auto& info = getDiaPasefIsolationInfo();
        isolationInfo.resize(1, IsolationInfo{ info.isolationMz, IsolationMode_On, info.collisionEnergy });
    }
    else if (frame_.precursorMz_.is_initialized())
    {
        isolationInfo.resize(1, IsolationInfo{ frame_.precursorMz_.get(), IsolationMode_On, 0 });
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
    else if (!frame_.diaPasefIsolationInfoByScanNumber_.empty())
    {
        fragmentedMZs.resize(1, getDiaPasefIsolationInfo().isolationMz);
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
    else if (!frame_.diaPasefIsolationInfoByScanNumber_.empty())
    {
        return getDiaPasefIsolationInfo().isolationWidth;
    }
    else
        return frame_.isolationWidth_.get_value_or(0);
}

int TimsSpectrum::getWindowGroup() const
{
    return frame_.windowGroup_.get_value_or(0);
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
