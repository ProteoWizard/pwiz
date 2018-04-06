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
            return FragmentationMode_CID;

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


TimsDataImpl::TimsDataImpl(const string& rawpath, bool combineIonMobilitySpectra)
    : tdfFilepath_((bfs::path(rawpath) / "analysis.tdf").string()),
      tdfStorage_(new TimsBinaryData(rawpath)),
      combineSpectra_(combineIonMobilitySpectra)
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
        else if (key == "InstrumentSourceType")
            instrumentSource_ = translateInstrumentSource(lexical_cast<int>(value));
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
    size_t count = sqlite::query(db, "SELECT COUNT(*) FROM Frames").begin()->get<sqlite3_int64>(0);
    frames_.reserve(count);

    sqlite::query q(db, "SELECT f.Id, Time, Polarity, ScanMode, MsMsType, MaxIntensity, SummedIntensities, NumScans, NumPeaks, "
                        "Parent, TriggerMass, IsolationWidth, PrecursorCharge, CollisionEnergy "
                        "FROM Frames f "
                        "LEFT JOIN FrameMsMsInfo info ON f.Id=info.Frame "
                        "ORDER BY Time");

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
            case 0: msLevel = 1; break;
            case 2: msLevel = 2; break;
            case 8: msLevel = 2; break;
            default: throw runtime_error("Unhandled msmsType: " + lexical_cast<string>(msmsType));
        }

        double bpi = row.get<double>(++idx);
        double tic = row.get<double>(++idx);
        int numScans = row.get<int>(++idx);
        int numPeaks = row.get<int>(++idx);
        if (numPeaks == 0)
            continue;

        optional<uint64_t> parentId(row.get<optional<sqlite3_int64> >(++idx));
        optional<double> precursorMz(row.get<optional<double> >(++idx));
        optional<double> isolationWidth(row.get<optional<double> >(++idx));
        optional<int> precursorCharge(row.get<optional<int> >(++idx));
        optional<double> collisionEnergy(row.get<optional<double> >(++idx));

        frames_.emplace_back(new TimsFrame(tdfStorage_, frameId,
                                           msLevel, rt,
                                           mzAcqRangeLower, mzAcqRangeUpper,
                                           tic, bpi,
                                           polarity, scanMode, numScans,
                                           parentId, precursorMz,
                                           isolationWidth, precursorCharge));
        for (int i=0; i < numScans; ++i)
            spectra_.emplace_back(new TimsSpectrum(frames_.back(), i));
    }
}


bool TimsDataImpl::hasMSData() const { return true; }
bool TimsDataImpl::hasLCData() const { return false; }
size_t TimsDataImpl::getMSSpectrumCount() const { return spectra_.size(); }
MSSpectrumPtr TimsDataImpl::getMSSpectrum(int scan, DetailLevel detailLevel) const { return spectra_[scan - 1]; }

pair<size_t, size_t> TimsDataImpl::getFrameScanPair(int scan) const
{
    const auto& s = spectra_[scan - 1];
    return make_pair(s->framePtr()->frameId(), s->scanIndex()+1);
}

size_t TimsDataImpl::getLCSourceCount() const { return 0; }
size_t TimsDataImpl::getLCSpectrumCount(int source) const { return 0; }
LCSpectrumSourcePtr TimsDataImpl::getLCSource(int source) const { return 0; }
LCSpectrumPtr TimsDataImpl::getLCSpectrum(int source, int scan) const { return LCSpectrumPtr(); }

std::string TimsDataImpl::getOperatorName() const { return operatorName_; }
std::string TimsDataImpl::getAnalysisName() const { return ""; }
boost::local_time::local_date_time TimsDataImpl::getAnalysisDateTime() const { return parse_date_time("%Y-%m-%dT%H:%M:%S%Q", acquisitionDateTime_); }
std::string TimsDataImpl::getSampleName() const { return ""; }
std::string TimsDataImpl::getMethodName() const { return ""; }
InstrumentFamily TimsDataImpl::getInstrumentFamily() const { return instrumentFamily_; }
std::string TimsDataImpl::getInstrumentDescription() const { return ""; }
InstrumentSource TimsDataImpl::getInstrumentSource() const { return instrumentSource_; }
std::string TimsDataImpl::getAcquisitionSoftware() const { return acquisitionSoftware_; }
std::string TimsDataImpl::getAcquisitionSoftwareVersion() const { return acquisitionSoftwareVersion_; }

TimsFrame::TimsFrame(TimsBinaryDataPtr storage, int64_t frameId,
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
      storage_(storage), oneOverK0_(numScans)
{
    vector<double> scanNumbers(numScans);
    for(int i=1; i <= numScans; ++i)
        scanNumbers[i-1] = i;

    storage_->scanNumToOneOverK0(frameId, scanNumbers, oneOverK0_);
}

TimsSpectrum::TimsSpectrum(const TimsFramePtr& framePtr, int scanIndex)
    : framePtr_(framePtr), scanIndex_(scanIndex)
{
}

bool TimsSpectrum::hasLineData() const { return getLineDataSize() > 0; }
bool TimsSpectrum::hasProfileData() const { return false; }
size_t TimsSpectrum::getLineDataSize() const { return framePtr_->storage_->readScans(framePtr_->frameId_, scanIndex_, scanIndex_ +1).getNbrPeaks(0); }
size_t TimsSpectrum::getProfileDataSize() const { return 0; }

void TimsSpectrum::getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    const auto& frame = *framePtr_;
    auto& storage = *frame.storage_;
    const auto& frameProxy = storage.readScans(frame.frameId_, scanIndex_, scanIndex_ + 1);
    auto mzIndices = frameProxy.getScanX(0);
    vector<double> mzIndicesAsDoubles(mzIndices.size());
    for (size_t i = 0; i < mzIndicesAsDoubles.size(); ++i)
        mzIndicesAsDoubles[i] = mzIndices[i];

    storage.indexToMz(frame.frameId_, mzIndicesAsDoubles, mz);

    auto intensityCounts = frameProxy.getScanY(0);
    intensities.resize_no_initialize(intensityCounts.size());
    for (size_t i=0; i < intensities.size(); ++i)
        intensities[i] = intensityCounts[i];
}

void TimsSpectrum::getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    // TDF does not support profile data
    mz.clear();
    intensities.clear();
    return;
}

void TimsSpectrum::getCombinedSpectumData(util::BinaryData<double>& mz, util::BinaryData<double>& intensities, util::BinaryData<double>& mobilities) const
{

}

int TimsSpectrum::getMSMSStage() const { return framePtr_->msLevel_; }
double TimsSpectrum::getRetentionTime() const { return framePtr_->rt_; }

void TimsSpectrum::getIsolationData(std::vector<double>& isolatedMZs, std::vector<IsolationMode>& isolationModes) const
{
    isolatedMZs.clear();
    isolationModes.clear();
    if (framePtr_->precursorMz_.is_initialized())
    {
        isolatedMZs.resize(1, framePtr_->precursorMz_.get());
        isolationModes.resize(1, IsolationMode_On);
    }
}

void TimsSpectrum::getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const
{
    fragmentedMZs.clear();
    fragmentationModes.clear();
    if (framePtr_->precursorMz_.is_initialized())
    {
        fragmentedMZs.resize(1, framePtr_->precursorMz_.get());
        fragmentationModes.resize(1, translateScanMode(framePtr_->scanMode_));
    }
}

IonPolarity TimsSpectrum::getPolarity() const { return framePtr_->polarity_; }

std::pair<double, double> TimsSpectrum::getScanRange() const
{
    return framePtr_->scanRange_;
}

int TimsSpectrum::getChargeState() const
{
    return framePtr_->chargeState_.get_value_or(0);
}

double TimsSpectrum::getIsolationWidth() const
{
    return framePtr_->isolationWidth_.get_value_or(0);
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
