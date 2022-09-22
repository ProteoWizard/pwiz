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
#ifdef _WIN64

#pragma unmanaged
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/sort_together.hpp"
#include "TsfData.hpp"
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

FragmentationMode translateScanMode(ScanMode scanMode)
{
    switch (scanMode)
    {
        default:
        case ScanMode::MS1:
            return FragmentationMode_CID;
            //return FragmentationMode_Unknown;

        case ScanMode::AutoMSMS:
        case ScanMode::MRM:
            return FragmentationMode_CID;

        case ScanMode::IS_CID:
        case ScanMode::BB_CID:
            return FragmentationMode_ISCID; // in-source or broadband CID
    }
}

InstrumentSource translateInstrumentSource(int instrumentSourceId)
{
    return (InstrumentSource) instrumentSourceId;
}

InstrumentSource translateScanModeToInstrumentSource(ScanMode scanMode, int instrumentSourceId)
{
    switch (scanMode)
    {
        case ScanMode::MS1:
        case ScanMode::AutoMSMS:
        case ScanMode::MRM:
        case ScanMode::IS_CID:
        case ScanMode::BB_CID:
        case ScanMode::DDA_PASEF:
        case ScanMode::DIA_PASEF:
        case ScanMode::PRM_PASEF:
        default:
            return translateInstrumentSource(instrumentSourceId);

        case ScanMode::MALDI:
            return InstrumentSource_MALDI;
    }
}

} // namespace


namespace pwiz {
namespace vendor_api {
namespace Bruker {


TsfDataImpl::TsfDataImpl(const string& rawpath, int preferOnlyMsLevel)
    : tsfFilepath_((bfs::path(rawpath) / "analysis.tsf").string()),
      preferOnlyMsLevel_(preferOnlyMsLevel),
      tsfStoragePtr_(new TsfBinaryData(rawpath)),
      tsfStorage_(*tsfStoragePtr_)
{
    tsf_set_num_threads(4);
    sqlite::database db(tsfFilepath_);

    double mzAcqRangeLower = 0, mzAcqRangeUpper = 0;

    int instrumentSource = 0;

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
        else if (key == "InstrumentSourceType") // CONSIDER: not accurate for MALDI?
            instrumentSource = lexical_cast<int>(value);
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
        else if (key == "HasLineSpectra")
            hasLineSpectra_ = lexical_cast<bool>(value);
        else if (key == "HasProfileSpectra")
            hasProfileSpectra_ = lexical_cast<bool>(value);
    }

    sqlite::query firstScanMode(db, "SELECT ScanMode FROM Frames LIMIT 1");
    for (sqlite::query::iterator itr = firstScanMode.begin(); itr != firstScanMode.end(); ++itr)
    {
        instrumentSource_ = translateScanModeToInstrumentSource((ScanMode) itr->get<int>(0), instrumentSource);
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

    // get anticipated scan count
    string queryNonEmpty = queryFrameCount + queryMsFilter + " AND NumPeaks > 0";
    size_t countNonEmpty = sqlite::query(db, queryNonEmpty.c_str()).begin()->get<sqlite3_int64>(0);
    spectra_.reserve(countNonEmpty);

    string maldiJoin, maldiColumns;
    if (db.has_table("MaldiFrameInfo"))
    {
        maldiJoin = "LEFT JOIN MaldiFrameInfo maldi ON f.Id=maldi.Frame";
        maldiColumns = ", Chip, SpotName ";
    }

    string querySelect =
        "SELECT f.Id, Time, Polarity, ScanMode, MsMsType, MaxIntensity, SummedIntensities, NumPeaks, "
        "Parent, TriggerMass, IsolationWidth, PrecursorCharge, CollisionEnergy " +
        maldiColumns +
        "FROM Frames f "
        "LEFT JOIN FrameMsMsInfo info ON f.Id=info.Frame " +
        maldiJoin +
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
        ScanMode scanMode = (ScanMode) row.get<int>(++idx);
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

        int numPeaks = row.get<int>(++idx);
        if (numPeaks == 0)
            continue;

        optional<uint64_t> parentId(row.get<optional<sqlite3_int64> >(++idx));
        optional<double> precursorMz(row.get<optional<double> >(++idx));
        optional<double> isolationWidth(row.get<optional<double> >(++idx));
        optional<int> precursorCharge(row.get<optional<int> >(++idx));
        optional<double> collisionEnergy(row.get<optional<double> >(++idx));

        optional<int> maldiChip(row.get<optional<int> >(++idx));
        optional<string> maldiSpotName(row.get<optional<string> >(++idx));

        TsfFramePtr frame = boost::make_shared<TsfFrame>(*this, frameId,
                                         msmsType, rt,
                                         mzAcqRangeLower, mzAcqRangeUpper,
                                         tic, bpi,
                                         polarity, scanMode,
                                         parentId, precursorMz,
                                         isolationWidth, precursorCharge, collisionEnergy,
                                         maldiChip, maldiSpotName);
        frames_[frameId] = frame;
    }

    if (frames_.empty())
        return;

    for (const auto& kvp : frames_)
    {
        const auto& frame = kvp.second;

        spectra_.emplace_back(boost::make_shared<TsfSpectrum>(frame));
    }
}


bool TsfDataImpl::hasMSData() const { return true; }
bool TsfDataImpl::hasLCData() const { return false; }
size_t TsfDataImpl::getMSSpectrumCount() const { return spectra_.size(); }
MSSpectrumPtr TsfDataImpl::getMSSpectrum(int scan, DetailLevel detailLevel) const { return spectra_[scan - 1]; }

FrameScanRange TsfDataImpl::getFrameScanPair(int scan) const
{
    const auto& s = spectra_[scan - 1];
    return FrameScanRange { (int) s->frame_.frameId(), 0, 0 };
}

size_t TsfDataImpl::getSpectrumIndex(int frame, int scan) const
{
    auto findItr = frames_.find(frame);
    if (findItr == frames_.end())
        throw out_of_range("[TsfData::getSpectrumIndex] invalid frame index");
    return findItr->second->frameId() - 1;
}


size_t TsfDataImpl::getLCSourceCount() const { return 0; }
size_t TsfDataImpl::getLCSpectrumCount(int source) const { return 0; }
LCSpectrumSourcePtr TsfDataImpl::getLCSource(int source) const { return 0; }
LCSpectrumPtr TsfDataImpl::getLCSpectrum(int source, int scan) const { return LCSpectrumPtr(); }

ChromatogramPtr TsfDataImpl::getTIC(bool ms1Only) const { return ms1Only ? ticMs1_ : tic_; }
ChromatogramPtr TsfDataImpl::getBPC(bool ms1Only) const { return ms1Only ? bpcMs1_ : bpc_; }

std::string TsfDataImpl::getOperatorName() const { return operatorName_; }
std::string TsfDataImpl::getAnalysisName() const { return ""; }
boost::local_time::local_date_time TsfDataImpl::getAnalysisDateTime() const { return parse_date_time("%Y-%m-%dT%H:%M:%S%Q", acquisitionDateTime_); }
std::string TsfDataImpl::getSampleName() const { return ""; }
std::string TsfDataImpl::getMethodName() const { return ""; }
InstrumentFamily TsfDataImpl::getInstrumentFamily() const { return instrumentFamily_; }
int TsfDataImpl::getInstrumentRevision() const { return instrumentRevision_; }
std::string TsfDataImpl::getInstrumentDescription() const { return ""; }
std::string TsfDataImpl::getInstrumentSerialNumber() const { return serialNumber_; }
InstrumentSource TsfDataImpl::getInstrumentSource() const { return instrumentSource_; }
std::string TsfDataImpl::getAcquisitionSoftware() const { return acquisitionSoftware_; }
std::string TsfDataImpl::getAcquisitionSoftwareVersion() const { return acquisitionSoftwareVersion_; }


TsfFrame::TsfFrame(TsfDataImpl& TsfDataImpl, int64_t frameId,
                     MsMsType msmsType, double rt,
                     double startMz, double endMz,
                     double tic, double bpi,
                     IonPolarity polarity, ScanMode scanMode,
                     const optional<uint64_t>& parentId,
                     const optional<double>& precursorMz,
                     const optional<double>& isolationWidth,
                     const optional<int>& precursorCharge,
                     const optional<double>& collisionEnergy,
                     const optional<int>& maldiChip,
                     const optional<string>& maldiSpotName)
    : frameId_(frameId), msmsType_(msmsType), rt_(rt), parentId_(parentId), tic_(tic), bpi_(bpi),
      polarity_(polarity), scanRange_(startMz, endMz),
      precursorMz_(precursorMz), scanMode_(scanMode),
      isolationWidth_(isolationWidth), collisionEnergy_(collisionEnergy), chargeState_(precursorCharge),
      maldiChip_(maldiChip), maldiSpotName_(maldiSpotName),
      tsfDataImpl_(TsfDataImpl)
{
    switch (msmsType_)
    {
        case MsMsType::MS1: msLevel_ = 1; break; // MS1
        case MsMsType::MRM: msLevel_ = 2; break; // MRM
        case MsMsType::DDA_PASEF: msLevel_ = 2; break; // PASEF
        case MsMsType::DIA_PASEF: msLevel_ = 2; break; // DIA
        case MsMsType::PRM_PASEF: msLevel_ = 2; break; // PRM
        default: throw runtime_error("Unhandled msmsType: " + lexical_cast<string>((int) msmsType_));
    }
}

bool TsfSpectrum::hasLineData() const { return frame_.tsfDataImpl_.hasLineSpectra_; }
bool TsfSpectrum::hasProfileData() const { return frame_.tsfDataImpl_.hasProfileSpectra_; }
size_t TsfSpectrum::getLineDataSize() const { return frame_.tsfDataImpl_.tsfStorage_.readLineSpectrum(frame_.frameId_, false).first.get().size(); }
size_t TsfSpectrum::getProfileDataSize() const { return frame_.tsfDataImpl_.tsfStorage_.readProfileSpectrum(frame_.frameId_, false).first.get().size(); }

void TsfSpectrum::getLineData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const
{
    auto& storage = frame_.tsfDataImpl_.tsfStorage_;
    const auto& mzIntensityArrays = storage.readLineSpectrum(frame_.frameId(), true);

    // Empty scans are not uncommon, save some heap thrashing
    if (mzIntensityArrays.first.get().empty())
    {
        mz.resize(0);
        intensities.resize(0);
        return;
    }

    mz.assign(mzIntensityArrays.first.get().begin(), mzIntensityArrays.first.get().end());
    intensities.assign(mzIntensityArrays.second.get().begin(), mzIntensityArrays.second.get().end());
}

void TsfSpectrum::getProfileData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const
{
    auto& storage = frame_.tsfDataImpl_.tsfStorage_;
    const auto& mzIntensityArrays = storage.readProfileSpectrum(frame_.frameId(), true);

    // Empty scans are not uncommon, save some heap thrashing
    if (mzIntensityArrays.first.get().empty())
    {
        mz.resize(0);
        intensities.resize(0);
        return;
    }

    mz.assign(mzIntensityArrays.first.get().begin(), mzIntensityArrays.first.get().end());
    intensities.assign(mzIntensityArrays.second.get().begin(), mzIntensityArrays.second.get().end());
}

int TsfSpectrum::getMSMSStage() const { return frame_.msLevel_; }
double TsfSpectrum::getRetentionTime() const { return frame_.rt_; }

void TsfSpectrum::getIsolationData(std::vector<IsolationInfo>& isolationInfo) const
{
    isolationInfo.clear();

    if (frame_.precursorMz_.is_initialized())
    {
        isolationInfo.resize(1, IsolationInfo{ frame_.precursorMz_.get(), IsolationMode_On, 0 });
    }
}

void TsfSpectrum::getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const
{
    fragmentedMZs.clear();
    fragmentationModes.clear();

    if (frame_.precursorMz_.is_initialized())
    {
        fragmentedMZs.resize(1, frame_.precursorMz_.get());
        fragmentationModes.resize(1, translateScanMode(frame_.scanMode_));
    }
}

IonPolarity TsfSpectrum::getPolarity() const { return frame_.polarity_; }

std::pair<double, double> TsfSpectrum::getScanRange() const
{
    return frame_.scanRange_;
}

int TsfSpectrum::getChargeState() const
{
    return frame_.chargeState_.get_value_or(0);
}

double TsfSpectrum::getIsolationWidth() const
{
    return frame_.isolationWidth_.get_value_or(0);
}

boost::optional<int> TsfSpectrum::getMaldiChip() const { return frame_.maldiChip_; }
boost::optional<std::string> TsfSpectrum::getMaldiSpotName() const { return frame_.maldiSpotName_; }


struct PWIZ_API_DECL Baf2SqlSpectrumParameterList : public MSSpectrumParameterList
{    
    virtual size_t size() const { return 0; }
    virtual value_type operator[] (size_t index) const { throw range_error("[Baf2SqlSpectrumParameterList] parameter index out of range"); }
    virtual const_iterator begin() const { return const_iterator(); }
    virtual const_iterator end() const { return const_iterator(); }
};


MSSpectrumParameterListPtr TsfSpectrum::parameters() const
{
    return MSSpectrumParameterListPtr(new Baf2SqlSpectrumParameterList());
}


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz

#endif // _WIN64
