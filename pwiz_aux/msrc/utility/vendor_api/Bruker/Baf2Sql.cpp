//
// $Id: CompassData.cpp 6478 2014-07-08 20:01:38Z chambm $
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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
#include "Baf2Sql.hpp"
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
            return FragmentationMode_Unknown;

        case 2: return FragmentationMode_CID;

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
255 = unknown

AcquisitionMode:
1 = (axial or orthogonal) TOF, linear detection mode
2 = (axial or orthogonal) TOF, reflector detection mode
255 = unknown

*/


namespace pwiz {
namespace vendor_api {
namespace Bruker {


Baf2SqlImpl::Baf2SqlImpl(const string& rawpath) : rawpath_(rawpath), bafFilepath_((bfs::path(rawpath) / "analysis.baf").string()), bafStorage_(new BinaryStorage(bafFilepath_))
{
    sqlite::database db(baf2sql::getSQLiteCacheFilename(bafFilepath_));
    sqlite::query q(db, "SELECT s.Id, ak.MsLevel+1, Rt, Segment, AcquisitionKey, "
                        "MzAcqRangeLower, MzAcqRangeUpper, SumIntensity, MaxIntensity, Polarity, "
                        "ProfileMzId, ProfileIntensityId, LineMzId, LineIntensityId, "
                        "Parent, Mass, IsolationType, ReactionType, ScanMode "
                        "FROM Spectra s, AcquisitionKeys ak "
                        "LEFT JOIN Steps step ON s.Id=TargetSpectrum "
                        "WHERE ak.Id=s.AcquisitionKey "
                        "ORDER BY Rt");

    for (sqlite::query::iterator itr = q.begin(); itr != q.end(); ++itr)
    {
        sqlite::query::rows row = *itr;
        int idx = -1;
        int id = row.get<int>(++idx);
        int msLevel = row.get<int>(++idx);
        double rt = row.get<double>(++idx);
        int segment = row.get<int>(++idx);
        int ak = row.get<int>(++idx);
        double startMz = row.get<double>(++idx);
        double endMz = row.get<double>(++idx);
        double tic = row.get<double>(++idx);
        double bpi = row.get<double>(++idx);
        IonPolarity polarity = (IonPolarity) row.get<int>(++idx);
        optional<uint64_t> profileMzId(row.get<optional<sqlite3_int64> >(++idx));
        optional<uint64_t> profileIntensityId(row.get<optional<sqlite3_int64> >(++idx)); 
        optional<uint64_t> lineMzId(row.get<optional<sqlite3_int64> >(++idx)); 
        optional<uint64_t> lineIntensityId(row.get<optional<sqlite3_int64> >(++idx));
        optional<uint64_t> parentId(row.get<optional<sqlite3_int64> >(++idx));
        optional<double> precursorMz(row.get<optional<double> >(++idx));
        optional<int> isolationMode(row.get<optional<int> >(++idx));
        optional<int> reactionMode(row.get<optional<int> >(++idx));
        int scanMode = row.get<int>(++idx);

        spectra_.push_back(MSSpectrumPtr(new Baf2SqlSpectrum(bafStorage_, id,
                                                             msLevel, rt, segment, ak, startMz, endMz,
                                                             tic, bpi, polarity, scanMode,
                                                             profileMzId, profileIntensityId, lineMzId, lineIntensityId,
                                                             parentId, precursorMz, isolationMode, reactionMode)));
    }

    sqlite::query properties(db, "SELECT Key, Value FROM Properties");
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
    }
}


bool Baf2SqlImpl::hasMSData() const { return true; }
bool Baf2SqlImpl::hasLCData() const { return false; }
size_t Baf2SqlImpl::getMSSpectrumCount() const { return spectra_.size(); }
MSSpectrumPtr Baf2SqlImpl::getMSSpectrum(int scan, DetailLevel detailLevel) const { return spectra_[scan - 1]; }

size_t Baf2SqlImpl::getLCSourceCount() const { return 0; }
size_t Baf2SqlImpl::getLCSpectrumCount(int source) const { return 0; }
LCSpectrumSourcePtr Baf2SqlImpl::getLCSource(int source) const { return 0; }
LCSpectrumPtr Baf2SqlImpl::getLCSpectrum(int source, int scan) const { return LCSpectrumPtr(); }

std::string Baf2SqlImpl::getOperatorName() const { return operatorName_; }
std::string Baf2SqlImpl::getAnalysisName() const { return ""; }
boost::local_time::local_date_time Baf2SqlImpl::getAnalysisDateTime() const { return parse_date_time("%Y-%m-%dT%H:%M:%S%Q", acquisitionDateTime_); }
std::string Baf2SqlImpl::getSampleName() const { return ""; }
std::string Baf2SqlImpl::getMethodName() const { return ""; }
InstrumentFamily Baf2SqlImpl::getInstrumentFamily() const { return instrumentFamily_; }
std::string Baf2SqlImpl::getInstrumentDescription() const { return ""; }
InstrumentSource Baf2SqlImpl::getInstrumentSource() const { return instrumentSource_; }
std::string Baf2SqlImpl::getAcquisitionSoftware() const { return acquisitionSoftware_; }
std::string Baf2SqlImpl::getAcquisitionSoftwareVersion() const { return acquisitionSoftwareVersion_; }


Baf2SqlSpectrum::Baf2SqlSpectrum(BinaryStoragePtr storage, int index,
                                 int msLevel, double rt, int segment, int acqKey,
                                 double startMz, double endMz, double tic, double bpi,
                                 IonPolarity polarity, int scanMode,
                                 const optional<uint64_t>& profileMzArrayId, const optional<uint64_t>& profileIntensityArrayId,
                                 const optional<uint64_t>& lineMzarrayId, const optional<uint64_t>& lineIntensityArrayId)
    : index_(index), msLevel_(msLevel), rt_(rt), segment_(segment), acqKey_(acqKey), tic_(tic), bpi_(bpi),
      profileMzArrayId_(profileMzArrayId), profileIntensityArrayId_(profileIntensityArrayId),
      lineMzArrayId_(lineMzarrayId), lineIntensityArrayId_(lineIntensityArrayId),
      polarity_(polarity), scanRange_(startMz, endMz), scanMode_(scanMode),
      storage_(storage)
{
}

Baf2SqlSpectrum::Baf2SqlSpectrum(BinaryStoragePtr storage, int index,
                                 int msLevel, double rt, int segment, int acqKey,
                                 double startMz, double endMz, double tic, double bpi,
                                 IonPolarity polarity, int scanMode,
                                 const optional<uint64_t>& profileMzArrayId, const optional<uint64_t>& profileIntensityArrayId,
                                 const optional<uint64_t>& lineMzarrayId, const optional<uint64_t>& lineIntensityArrayId,
                                 const optional<uint64_t>& parentId, const optional<double>& precursorMz,
                                 const optional<int>& isolationMode, const optional<int>& reactionMode)
    : index_(index), msLevel_(msLevel), rt_(rt), segment_(segment), acqKey_(acqKey), parentId_(parentId), tic_(tic), bpi_(bpi),
      profileMzArrayId_(profileMzArrayId), profileIntensityArrayId_(profileIntensityArrayId),
      lineMzArrayId_(lineMzarrayId), lineIntensityArrayId_(lineIntensityArrayId),
      polarity_(polarity), scanRange_(startMz, endMz),
      isolationMode_(isolationMode), reactionMode_(reactionMode), precursorMz_(precursorMz), scanMode_(scanMode),
      storage_(storage)
{
}

bool Baf2SqlSpectrum::hasLineData() const { return getLineDataSize() > 0; }
bool Baf2SqlSpectrum::hasProfileData() const { return getProfileDataSize() > 0; }
size_t Baf2SqlSpectrum::getLineDataSize() const { return lineIntensityArrayId_.is_initialized() ? storage_->getArrayNumElements(lineIntensityArrayId_.get()) : 0; }
size_t Baf2SqlSpectrum::getProfileDataSize() const { return profileIntensityArrayId_.is_initialized() ? storage_->getArrayNumElements(profileIntensityArrayId_.get()) : 0; }

void Baf2SqlSpectrum::readArray(uint64_t id, automation_vector<double> & result) const
{
    size_t n = static_cast<size_t>(storage_->getArrayNumElements(id));
    readArray(id, result, n);
}

void Baf2SqlSpectrum::readArray(uint64_t id, automation_vector<double> & result, size_t n) const
{

    if (n > std::numeric_limits<size_t>::max())
    {
        BOOST_THROW_EXCEPTION(std::runtime_error("Array too large."));
    }

    result.resize_no_initialize(n);
    if ((n>0) && (baf2sql_array_read_double(storage_->getHandle(), id, &result[0]) == 0))
    {
        baf2sql::throwLastBaf2SqlError();
    }
}


void Baf2SqlSpectrum::getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    if (!lineIntensityArrayId_.is_initialized())
    {
        mz.clear();
        intensities.clear();
        return;
    }
    readArray(lineIntensityArrayId_.get(), intensities); // These are quicker to inspect than mz - probably because they're stored as float instead of double
    size_t n = intensities.size();
    if (n == 0)
    {
        mz.clear();
        return;
    }
    readArray(lineMzArrayId_.get(), mz, n);  // Assume mz and intensity arrays are same length, for best read speed
}

void Baf2SqlSpectrum::getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
{
    if (!profileIntensityArrayId_.is_initialized())
    {
        mz.clear();
        intensities.clear();
        return;
    }
    readArray(profileIntensityArrayId_.get(), intensities); // These are quicker to inspect than mz - probably because they're stored as float instead of double
    size_t n = intensities.size();
    if (n == 0)
    {
        mz.clear();
        return;
    }
    readArray(profileMzArrayId_.get(), mz, n); // Assume mz and intensity arrays are same length, for best read speed
}

int Baf2SqlSpectrum::getMSMSStage() const { return msLevel_; }
double Baf2SqlSpectrum::getRetentionTime() const { return rt_; }

void Baf2SqlSpectrum::getIsolationData(std::vector<double>& isolatedMZs, std::vector<IsolationMode>& isolationModes) const
{
    isolatedMZs.clear();
    isolationModes.clear();
    if (precursorMz_.is_initialized())
    {
        isolatedMZs.resize(1, precursorMz_.get());
        isolationModes.resize(1, IsolationMode_On);
    }
}

void Baf2SqlSpectrum::getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const
{
    fragmentedMZs.clear();
    fragmentationModes.clear();
    if (precursorMz_.is_initialized())
    {
        fragmentedMZs.resize(1, precursorMz_.get());
        fragmentationModes.resize(1, translateScanMode(scanMode_));
    }
}

IonPolarity Baf2SqlSpectrum::getPolarity() const { return polarity_; }

pair<double, double> Baf2SqlSpectrum::getScanRange() const
{
    return scanRange_;
}

int Baf2SqlSpectrum::getChargeState() const
{
    return chargeState_.get_value_or(0);
}

double Baf2SqlSpectrum::getIsolationWidth() const
{
    return isolationWidth_.get_value_or(0);
}


struct PWIZ_API_DECL Baf2SqlSpectrumParameterList : public MSSpectrumParameterList
{    
    virtual size_t size() const { return 0; }
    virtual value_type operator[] (size_t index) const { throw range_error("[Baf2SqlSpectrumParameterList] parameter index out of range"); }
    virtual const_iterator begin() const { return const_iterator(); }
    virtual const_iterator end() const { return const_iterator(); }
};


MSSpectrumParameterListPtr Baf2SqlSpectrum::parameters() const
{
    return MSSpectrumParameterListPtr(new Baf2SqlSpectrumParameterList());
}


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz
