//
// $Id$
//
//
// Original author: Brendan MacLean <brendanx .@. u.washington.edu>
//
// Copyright 2009 University of Washington - Seattle, WA
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
#include "boost/thread/mutex.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "UIMFReader.hpp"
#include "sqlite3pp.h"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
#include <msclr/auto_gcroot.h>
#using <System.dll>
using namespace pwiz::util;


using System::String;
using System::Math;
using System::Object;


namespace pwiz {
namespace vendor_api {
namespace UIMF {


class UIMFReaderImpl : public UIMFReader
{
    public:
    UIMFReaderImpl(const std::string& path);
    ~UIMFReaderImpl();

    virtual const vector<IndexEntry>& getIndex() const { return index_; }
    virtual const set<FrameType>& getFrameTypes() const { return frameTypes_; }
    virtual size_t getFrameCount() const { return frameCount_; }
    virtual pair<double, double> getScanRange() const;

    virtual const std::vector<DriftScanInfoPtr> getDriftScansForFrame(int frame) const;
    virtual size_t getMaxDriftScansPerFrame() const { return driftScansPerFrame_; }
    
    virtual boost::local_time::local_date_time getAcquisitionTime() const;

    virtual bool hasIonMobility() const { return true; }
    virtual bool canConvertIonMobilityAndCCS() const { return false; }
    virtual double ionMobilityToCCS(double driftTimeInMilliseconds, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

    virtual void getScan(int frame, int scan, FrameType frameType, pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray, bool ignoreZeroIntensityPoints) const;
    virtual double getDriftTime(int frame, int scan) const;
    virtual double getRetentionTime(int frame) const;

    virtual const void getTic(std::vector<double>& timeArray, std::vector<double>& intensityArray) const;

    private:
    msclr::auto_gcroot<UIMFLibrary::DataReader^> reader_;
    vector<IndexEntry> index_;
    set<FrameType> frameTypes_;
    size_t frameCount_;
    size_t driftScansPerFrame_;
};

typedef boost::shared_ptr<UIMFReaderImpl> UIMFReaderImplPtr;


struct DriftScanInfoImpl : public DriftScanInfo
{
    DriftScanInfoImpl(UIMFLibrary::ScanInfo^ scanInfo, FrameType frameType, double retentionTime);

    virtual int getFrameNumber() const { return frameNumber_; }
    virtual FrameType getFrameType() const { return frameType_; };
    virtual int getDriftScanNumber() const { return driftScanNumber_; }
    virtual double getDriftTime() const { return driftTime_; }
    virtual double getRetentionTime() const { return retentionTime_; }
    virtual int getNonZeroCount() const { return nonZeroCount_; }
    virtual double getTIC() const { return tic_; }

    private:
    int frameNumber_;
    FrameType frameType_;
    int driftScanNumber_;
    double driftTime_;
    double retentionTime_;
    int nonZeroCount_;
    double tic_;
};

#pragma unmanaged
PWIZ_API_DECL
UIMFReaderPtr UIMFReader::create(const string& path)
{
    UIMFReaderPtr dataReader(new UIMFReaderImpl(path));
    return boost::static_pointer_cast<UIMFReader>(dataReader);
}

int UIMFReader::getMsLevel(FrameType frameType)
{
    switch (frameType)
    {
        case FrameType_MS1: return 1;
        case FrameType_MS2: return 2;
        case FrameType_Calibration: return 1;
        case FrameType_Prescan: return 1;
        default: throw runtime_error("[UIMFReadeR::getMsLevel] invalid frame type");
    }
}


#pragma managed
UIMFReaderImpl::UIMFReaderImpl(const std::string& path)
{
    try
    {
        String^ filepath = ToSystemString(path);

        // populate the index before intializing the reader
        {
            sqlite3pp::database db(path, sqlite3pp::full_mutex, sqlite3pp::read_only);
            sqlite3pp::query indexQuery(db, "SELECT fs.FrameNum, ScanNum, FrameType FROM Frame_Scans fs, Frame_Parameters fp WHERE fs.FrameNum=fp.FrameNum");
            for (sqlite3pp::query::iterator itr = indexQuery.begin(); itr != indexQuery.end(); ++itr)
            {
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                int frameType;
                itr->getter() >> ie.frame >> ie.scan >> frameType;
                ie.frameType = (FrameType) frameType;
                frameTypes_.insert(ie.frameType);
            }
        }

        reader_ = gcnew UIMFLibrary::DataReader(filepath, false);
        UIMFLibrary::GlobalParams^ gp = reader_->GetGlobalParams();
        frameCount_ = (size_t) gp->NumFrames;
        driftScansPerFrame_ = (size_t) gp->Bins;
    }
    CATCH_AND_FORWARD
}

UIMFReaderImpl::~UIMFReaderImpl()
{
}

DriftScanInfoImpl::DriftScanInfoImpl(UIMFLibrary::ScanInfo^ scanInfo, FrameType frameType, double retentionTime)
{
    frameNumber_ = scanInfo->Frame;
    frameType_ = frameType;
    driftScanNumber_ = scanInfo->Scan;
    driftTime_ = scanInfo->DriftTime;
    retentionTime_ = retentionTime;
    nonZeroCount_ = scanInfo->NonZeroCount;
    tic_ = scanInfo->TIC;
}

pair<double, double> UIMFReaderImpl::getScanRange() const
{
    try
    {
        UIMFLibrary::FrameParams^ fp = reader_->GetFrameParams(1);
        UIMFLibrary::GlobalParams^ gp = reader_->GetGlobalParams();
        return make_pair(UIMFLibrary::DataReader::ConvertBinToMZ(fp->CalibrationSlope, fp->CalibrationIntercept, gp->BinWidth, gp->TOFCorrectionTime, 1),
                         UIMFLibrary::DataReader::ConvertBinToMZ(fp->CalibrationSlope, fp->CalibrationIntercept, gp->BinWidth, gp->TOFCorrectionTime, gp->Bins));
    }
    CATCH_AND_FORWARD
}

const std::vector<DriftScanInfoPtr> UIMFReaderImpl::getDriftScansForFrame(int frame) const
{
    FrameType frameType = (FrameType) reader_->GetFrameTypeForFrame(frame);
    double retentionTime = getRetentionTime(frame);
    System::Collections::Generic::List<UIMFLibrary::ScanInfo^>^ scanInfos = reader_->GetFrameScans(frame);

    vector<DriftScanInfoPtr> driftScans;
    driftScans.reserve(scanInfos->Count);

    // Use C++/CLI for each, because otherwise scanInfos has to be converted to a C++/CLI container to use C++11 for-range
    for each (UIMFLibrary::ScanInfo^ scanInfo in scanInfos)
    {
        driftScans.push_back(DriftScanInfoPtr(new DriftScanInfoImpl(scanInfo, frameType, retentionTime)));
    }
    return driftScans;
}

blt::local_date_time UIMFReaderImpl::getAcquisitionTime() const
{
    try
    {
        System::String^ dateString = reader_->GetGlobalParams()->GetValue(UIMFLibrary::GlobalParamKeyType::DateStarted)->ToString();
        System::DateTime acquisitionTime;
        if (!System::String::IsNullOrWhiteSpace(dateString))
        {
            acquisitionTime = System::DateTime::ParseExact(dateString, gcnew array<System::String^> { "M/d/yyyy h:mm:ss tt", "yyyy-M-d h:mm:ss tt" }, System::Globalization::DateTimeFormatInfo::InvariantInfo, System::Globalization::DateTimeStyles::None);
        }

        // these are Boost.DateTime restrictions
        if (acquisitionTime.Year > 10000)
            acquisitionTime = acquisitionTime.AddYears(10000 - acquisitionTime.Year);
        else if (acquisitionTime.Year < 1400)
            acquisitionTime = acquisitionTime.AddYears(1400 - acquisitionTime.Year);

        bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(acquisitionTime.ToOADate())); // time zone is unknown
        return blt::local_date_time(pt, blt::time_zone_ptr()); // keep time as is
    }
    CATCH_AND_FORWARD
}


double UIMFReaderImpl::ionMobilityToCCS(double driftTime, double mz, int charge) const
{
    return 0;
}


double UIMFReaderImpl::ccsToIonMobility(double ccs, double mz, int charge) const
{
    return 0;
}

void UIMFReaderImpl::getScan(int frame, int scan, FrameType frameType, pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray, bool ignoreZeroIntensityPoints) const
{
    try
    {
        cli::array<double>^ managedMzArray = nullptr;
        cli::array<int>^ managedIntensityArray = nullptr;
        reader_->GetSpectrum(frame, (UIMFLibrary::DataReader::FrameType) frameType, scan, managedMzArray, managedIntensityArray);

        if (managedMzArray->Length == 0)
            return;

        if (!ignoreZeroIntensityPoints)
        {
            mzArray.reserve(managedMzArray->Length * 3);
            intensityArray.reserve(managedMzArray->Length * 3);

            mzArray.push_back(managedMzArray[0] - reader_->GetDeltaMz(frame, managedMzArray[0]));
            intensityArray.push_back(0);

            double mzTmp = managedMzArray[0];
            mzArray.push_back(mzTmp);
            intensityArray.push_back((double) managedIntensityArray[0]);

            for (int i = 1, end = managedMzArray->Length; i < end; ++i)
            {
                double deltaMz = reader_->GetDeltaMz(frame, managedMzArray[i]);

                if (fabs(managedMzArray[i] - mzArray.back()) - deltaMz > 1e-2)
                {
                    mzArray.push_back(managedMzArray[i - 1] + deltaMz);
                    intensityArray.push_back(0);

                    mzArray.push_back(managedMzArray[i] - deltaMz);
                    intensityArray.push_back(0);
                }

                mzTmp = managedMzArray[i];
                mzArray.push_back(mzTmp);
                intensityArray.push_back((double)managedIntensityArray[i]);
            }

            mzArray.push_back(managedMzArray[managedMzArray->Length - 1] + reader_->GetDeltaMz(frame, managedMzArray[managedMzArray->Length - 1]));
            intensityArray.push_back(0);
        }
        else
        {
            ToBinaryData(managedMzArray, mzArray);
            ToBinaryData((System::Collections::Generic::IList<int>^) managedIntensityArray, intensityArray);
        }
    }
    CATCH_AND_FORWARD
}

double UIMFReaderImpl::getDriftTime(int frame, int scan) const
{
    try {return reader_->GetDriftTime(frame, scan, true);} CATCH_AND_FORWARD
}

double UIMFReaderImpl::getRetentionTime(int frame) const
{
    try
    {
        UIMFLibrary::FrameParams^ fp = reader_->GetFrameParams(frame);
        if (fp->HasParameter(UIMFLibrary::FrameParamKeyType::StartTimeMinutes))
            return fp->GetValueDouble(UIMFLibrary::FrameParamKeyType::StartTimeMinutes);

        return reader_->GetFrameStartTimeMinutesEstimated(frame);
    } CATCH_AND_FORWARD
}

const void UIMFReaderImpl::getTic(std::vector<double>& timeArray, std::vector<double>& intensityArray) const
{
    timeArray.reserve(frameCount_);
    intensityArray.reserve(frameCount_);

    // GetTICByFrame: if (0, 0, 0, 0) is provided, values for all frames and scans are returned (one per frame)
    for each (auto frameTic in reader_->GetTICByFrame(0, 0, 0, 0))
    {
        timeArray.push_back(reader_->GetFrameStartTimeMinutesEstimated(frameTic.Key));
        intensityArray.push_back(frameTic.Value);
    }
}


} // UIMF
} // vendor_api
} // pwiz
