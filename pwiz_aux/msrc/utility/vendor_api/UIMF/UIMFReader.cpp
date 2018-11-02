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

    virtual boost::local_time::local_date_time getAcquisitionTime() const;

    virtual void getScan(int frame, int scan, FrameType frameType, vector<double>& mzArray, vector<double>& intensityArray) const;
    virtual double getDriftTime(int frame, int scan) const;
    virtual double getRetentionTime(int frame) const;

    private:
    gcroot<UIMFLibrary::DataReader^> reader_;
    vector<IndexEntry> index_;
    set<FrameType> frameTypes_;
    size_t frameCount_;
};

typedef boost::shared_ptr<UIMFReaderImpl> UIMFReaderImplPtr;


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
            sqlite3pp::database db(path);
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

        reader_ = gcnew UIMFLibrary::DataReader(filepath);
        frameCount_ = (size_t) reader_->GetGlobalParams()->NumFrames;
    }
    CATCH_AND_FORWARD
}

UIMFReaderImpl::~UIMFReaderImpl()
{
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

blt::local_date_time UIMFReaderImpl::getAcquisitionTime() const
{
    try
    {
        System::DateTime acquisitionTime = System::DateTime::ParseExact(reader_->GetGlobalParams()->GetValue(UIMFLibrary::GlobalParamKeyType::DateStarted), "M/d/yyyy h:mm:ss tt", System::Globalization::DateTimeFormatInfo::InvariantInfo);

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

void UIMFReaderImpl::getScan(int frame, int scan, FrameType frameType, vector<double>& mzArray, vector<double>& intensityArray) const
{
    try
    {
        cli::array<double>^ managedMzArray = nullptr;
        cli::array<int>^ managedIntensityArray = nullptr;
        reader_->GetSpectrum(frame, (UIMFLibrary::DataReader::FrameType) frameType, scan, managedMzArray, managedIntensityArray);

        if (managedMzArray->Length == 0)
            return;

        mzArray.reserve(managedMzArray->Length * 3);
        intensityArray.reserve(managedMzArray->Length * 3);

        mzArray.push_back(managedMzArray[0] - reader_->GetDeltaMz(frame, managedMzArray[0]));
        intensityArray.push_back(0);

        mzArray.push_back(managedMzArray[0]);
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

            mzArray.push_back(managedMzArray[i]);
            intensityArray.push_back((double) managedIntensityArray[i]);
        }

        mzArray.push_back(managedMzArray[managedMzArray->Length - 1] + reader_->GetDeltaMz(frame, managedMzArray[managedMzArray->Length - 1]));
        intensityArray.push_back(0);

        //ToStdVector(managedMzArray, mzArray);
        //ToStdVector((System::Collections::Generic::IList<int>^) managedIntensityArray, intensityArray);
    }
    CATCH_AND_FORWARD
}

double UIMFReaderImpl::getDriftTime(int frame, int scan) const
{
    try {return reader_->GetDriftTime(frame, scan, true);} CATCH_AND_FORWARD
}

double UIMFReaderImpl::getRetentionTime(int frame) const
{
    try {return reader_->GetFrameParams(frame)->GetValueDouble(UIMFLibrary::FrameParamKeyType::StartTimeMinutes);} CATCH_AND_FORWARD
}


} // UIMF
} // vendor_api
} // pwiz
