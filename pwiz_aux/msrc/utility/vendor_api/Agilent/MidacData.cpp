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
#include "MidacData.hpp"


#pragma managed
namespace pwiz {
namespace vendor_api {
namespace Agilent {


namespace {

template<typename N, typename M>
N managedRangeToNative(M^ input_range)
{
    N output_range;
    output_range.start = input_range->Min;
    output_range.end = input_range->Max;
    return output_range;
}

boost::mutex massHunterInitMutex;

}


struct MidacScanRecord : public ScanRecord
{
    MidacScanRecord(MIDAC::IMidacFrameInfo^ frameInfo) : frameInfo_(frameInfo), specDetails_(frameInfo->SpectrumDetails) {}

    virtual int getScanId() const;
    virtual double getRetentionTime() const;
    virtual int getMSLevel() const;
    virtual MSScanType getMSScanType() const;
    virtual double getTic() const;
    virtual double getBasePeakMZ() const;
    virtual double getBasePeakIntensity() const;
    virtual IonizationMode getIonizationMode() const;
    virtual IonPolarity getIonPolarity() const;
    virtual double getMZOfInterest() const;
    virtual int getTimeSegment() const;
    virtual double getFragmentorVoltage() const;
    virtual double getCollisionEnergy() const;
    virtual bool getIsFragmentorVoltageDynamic() const;
    virtual bool getIsCollisionEnergyDynamic() const;
    virtual bool getIsIonMobilityScan() const;

    private:
    gcroot<MIDAC::IMidacFrameInfo^> frameInfo_;
    gcroot<MIDAC::IMidacMsDetailsSpec^> specDetails_;
};


struct FrameImpl : public Frame
{
    FrameImpl(MIDAC::IMidacImsReader^ imsReader, int frameIndex);

    virtual int getFrameIndex() const;
    virtual TimeRange getDriftTimeRange() const;
    virtual double getRetentionTime() const;
    virtual int getDriftBinsPresent() const;
    virtual const std::vector<short>& getNonEmptyDriftBins() const;
    virtual DriftScanPtr getScan(int driftBinIndex) const;
    virtual DriftScanPtr getTotalScan() const;

    private:
    int frameIndex_;
    int numDriftBins_;
    gcroot<MIDAC::IMidacImsReader^> imsReader_;
    gcroot<MIDAC::IMidacFrameInfo^> frameInfo_;
    mutable gcroot<MIDAC::IMidacSpecDataMs^> specData_;
    mutable vector<short> nonEmptyDriftBins_;
};


struct DriftScanImpl : public DriftScan
{
    DriftScanImpl(MIDAC::IMidacSpecDataMs^ specData);

    virtual MSStorageMode getMSStorageMode() const;
    virtual DeviceType getDeviceType() const;
    virtual void getPrecursorIons(std::vector<double>& precursorIons) const;
    virtual double getCollisionEnergy() const;
    virtual double getDriftTime() const;
    virtual int getScanId() const;

    virtual int getTotalDataPoints() const;
    virtual const pwiz::util::BinaryData<double>& getXArray() const;
    virtual const pwiz::util::BinaryData<float>& getYArray() const;

    private:
    MSStorageMode msStorageMode_;
    DeviceType deviceType_;
    MassRange massRange_;
    std::vector<double> precursorIons_;
    int precursorCharge_;
    double precursorIntensity_;
    double collisionEnergy_;
    double driftTime_;
    int scanId_;
    pwiz::util::BinaryData<double> xArray_;
    pwiz::util::BinaryData<float> yArray_;
};

MidacDataImpl::MidacDataImpl(const std::string& path)
{
    massHunterRootPath_ = path;

    try
    {
        String^ filepath = ToSystemString(path);

        {
            boost::mutex::scoped_lock lock(massHunterInitMutex);

            imsReader_ = MIDAC::MidacFileAccess::ImsDataReader(filepath);

            imsCcsReader_ = gcnew MIDAC::ImsCcsInfoReader();

            imsCcsReader_->Read(filepath);

            // Force read of some data before we start; gets some assertions out of the way.
            imsReader_->FrameInfo(1)->FrameUnitConverter;
        }

        ticTimes_.resize(imsReader_->FileInfo->NumFrames);
        ticIntensities_.resize(ticTimes_.size());
        for (size_t i = 0; i < ticTimes_.size(); ++i)
        {
            auto frameInfo = imsReader_->FrameInfo(i+1);
            ticTimes_[i] = frameInfo->AcqTimeRange->Min;
            ticIntensities_[i] = frameInfo->Tic;
        }

        hasProfileData_ = bfs::exists(bfs::path(path) / "AcqData/MSProfile.bin");
    }
    CATCH_AND_FORWARD
}

MidacDataImpl::~MidacDataImpl() noexcept(false)
{
    try {imsReader_->Close();} CATCH_AND_FORWARD
}

std::string MidacDataImpl::getVersion() const
{
    try {return ToStdString(imsReader_->FileInfo->AcquisitionSofwareVersion);} CATCH_AND_FORWARD
}

DeviceType MidacDataImpl::getDeviceType() const
{
    return DeviceType_Unknown;
}

std::string MidacDataImpl::getDeviceName(DeviceType deviceType) const
{
    try {return ToStdString(imsReader_->FileInfo->InstrumentName);} CATCH_AND_FORWARD
}

blt::local_date_time MidacDataImpl::getAcquisitionTime(bool adjustToHostTime) const
{
    try
    {
        System::DateTime acquisitionTime = imsReader_->FileInfo->AcquisitionDate;

        // these are Boost.DateTime restrictions enforced because one of the test files had a corrupt date
        if (acquisitionTime.Year > 10000)
            acquisitionTime = acquisitionTime.AddYears(10000 - acquisitionTime.Year);
        else if (acquisitionTime.Year < 1400)
            acquisitionTime = acquisitionTime.AddYears(1400 - acquisitionTime.Year);

        bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
                      bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));

        if (adjustToHostTime)
        {
            bpt::time_duration tzOffset = bpt::second_clock::universal_time() - bpt::second_clock::local_time();
            return blt::local_date_time(pt + tzOffset, blt::time_zone_ptr()); // treat time as if it came from host's time zone; actual time zone may not be provided by Sciex
        }
        else
            return blt::local_date_time(pt, blt::time_zone_ptr());
    }
    CATCH_AND_FORWARD
}

IonizationMode MidacDataImpl::getIonModes() const
{
    try {return (IonizationMode) imsReader_->FileInfo->TfsMsDetails->IonizationMode;} CATCH_AND_FORWARD
}

MSScanType MidacDataImpl::getScanTypes() const
{
    try {return (MSScanType) imsReader_->FileInfo->TfsMsDetails->MsScanType;} CATCH_AND_FORWARD
}

MSStorageMode MidacDataImpl::getSpectraFormat() const
{
    try {return (MSStorageMode) imsReader_->FileInfo->TfsMsDetails->MsStorageMode;} CATCH_AND_FORWARD
}

int MidacDataImpl::getTotalScansPresent() const
{
    try {return (int) imsReader_->FileInfo->NumFrames * imsReader_->FileInfo->MaxNonTfsMsPerFrame;} CATCH_AND_FORWARD
}

bool MidacDataImpl::hasProfileData() const
{
    return hasProfileData_;
}

bool MidacDataImpl::hasIonMobilityData() const {return true;}

int MidacDataImpl::getTotalIonMobilityFramesPresent() const
{
    try {return (int) imsReader_->FileInfo->NumFrames;} CATCH_AND_FORWARD
}

FramePtr MidacDataImpl::getIonMobilityFrame(int frameIndex) const
{
    try {return FramePtr(new FrameImpl(imsReader_, frameIndex));} CATCH_AND_FORWARD
}

bool MidacDataImpl::canConvertDriftTimeAndCCS() const
{
    try { return  imsCcsReader_->HasSingleFieldCcsInformation; } CATCH_AND_FORWARD
}

double MidacDataImpl::driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const
{
    try { return imsCcsReader_->CcsFromDriftTime(driftTimeInMilliseconds, mz, abs(charge)); } CATCH_AND_FORWARD
}

double MidacDataImpl::ccsToDriftTime(double ccs, double mz, int charge) const
{
    try { return imsCcsReader_->DriftTimeFromCcs(ccs, mz, abs(charge)); } CATCH_AND_FORWARD
}

ScanRecordPtr MidacDataImpl::getScanRecord(int rowNumber) const
{
    try {return ScanRecordPtr(new MidacScanRecord(imsReader_->FrameInfo(rowNumber + 1)));} CATCH_AND_FORWARD
}

const set<Transition>& MidacDataImpl::getTransitions() const
{
    return transitions_;
}

const automation_vector<double>& MidacDataImpl::getTicTimes() const
{
    return ticTimes_;
}

const automation_vector<double>& MidacDataImpl::getBpcTimes() const
{
    return bpcTimes_;
}

const automation_vector<float>& MidacDataImpl::getTicIntensities() const
{
    return ticIntensities_;
}

const automation_vector<float>& MidacDataImpl::getBpcIntensities() const
{
    return bpcIntensities_;
}

ChromatogramPtr MidacDataImpl::getChromatogram(const Transition& transition) const
{
    return ChromatogramPtr();
}

SpectrumPtr MidacDataImpl::getProfileSpectrumByRow(int rowNumber) const
{
    return SpectrumPtr();
}

SpectrumPtr MidacDataImpl::getPeakSpectrumByRow(int rowNumber, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    return SpectrumPtr();
}

SpectrumPtr MidacDataImpl::getProfileSpectrumById(int scanId) const
{
    return SpectrumPtr();
}

SpectrumPtr MidacDataImpl::getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    return SpectrumPtr();
}


int MidacScanRecord::getScanId() const
{
    try {return frameInfo_->DriftBinRange->Min;} CATCH_AND_FORWARD
}

double MidacScanRecord::getRetentionTime() const
{
    try { return specDetails_->AcqTimeRanges[0]->Min; } CATCH_AND_FORWARD
}

int MidacScanRecord::getMSLevel() const
{
    try {return specDetails_->MsLevel == MIDAC::MsLevel::MSMS ? 2 : 1;} CATCH_AND_FORWARD
}

MSScanType MidacScanRecord::getMSScanType() const
{
    try {return (MSScanType)specDetails_->MsScanType;} CATCH_AND_FORWARD
}

double MidacScanRecord::getTic() const
{
    try {return frameInfo_->Tic;} CATCH_AND_FORWARD
}

double MidacScanRecord::getBasePeakMZ() const
{
    return 0;
}

double MidacScanRecord::getBasePeakIntensity() const
{
    try {return frameInfo_->AbundRange->IsEmpty ? 0 : frameInfo_->AbundRange->Max;} CATCH_AND_FORWARD
}

IonizationMode MidacScanRecord::getIonizationMode() const
{
    try {return (IonizationMode)specDetails_->IonizationMode;} CATCH_AND_FORWARD
}

IonPolarity MidacScanRecord::getIonPolarity() const
{
    try {return (IonPolarity)specDetails_->IonPolarity;} CATCH_AND_FORWARD
}

double MidacScanRecord::getMZOfInterest() const
{
    try {return specDetails_->MzOfInterestRanges != nullptr && specDetails_->MzOfInterestRanges->Length > 0 && !specDetails_->MzOfInterestRanges[0]->IsEmpty ? specDetails_->MzOfInterestRanges[0]->Center : 0;} CATCH_AND_FORWARD
}

int MidacScanRecord::getTimeSegment() const
{
    try {return frameInfo_->TimeSegmentId;} CATCH_AND_FORWARD
}

double MidacScanRecord::getFragmentorVoltage() const
{
    try {return specDetails_->FragmentorVoltageRange != nullptr ? specDetails_->FragmentorVoltageRange->Min : 0;} CATCH_AND_FORWARD
}

double MidacScanRecord::getCollisionEnergy() const
{
    // From an email from MattC 6/3/2016
    // I've confirmed that half the scans have 0,0 and half have 0,48 for FragmentationEnergyRange. But I'm not exactly sure what you would consider a "fix" here for the ramped case 
    // since we can only have one CE value for the scan in the mzML data model.Do you want the halfway point between min / max or do you want the max ? IIRC, we've been using Min 
    // because in the non-ramped case, Min is actually set but Max is often 0.
    //
    // So now we take the max of min and max (!) - bspratt
    try { return specDetails_->FragmentationEnergyRange != nullptr ? max(specDetails_->FragmentationEnergyRange->Min, specDetails_->FragmentationEnergyRange->Max) : 0; } CATCH_AND_FORWARD
}

bool MidacScanRecord::getIsFragmentorVoltageDynamic() const
{
    return false;
}

bool MidacScanRecord::getIsCollisionEnergyDynamic() const
{
    return false;
}

bool MidacScanRecord::getIsIonMobilityScan() const {return true;}


FrameImpl::FrameImpl(MIDAC::IMidacImsReader^ imsReader, int frameIndex) : imsReader_(imsReader), frameIndex_(frameIndex)
{
    frameInfo_ = imsReader->FrameInfo(frameIndex + 1);
    numDriftBins_ = imsReader->FileInfo->MaxNonTfsMsPerFrame;
}

int FrameImpl::getFrameIndex() const { return frameIndex_; }

TimeRange FrameImpl::getDriftTimeRange() const
{
    try {return managedRangeToNative<TimeRange>(frameInfo_->DriftTimeRange);} CATCH_AND_FORWARD
}

double FrameImpl::getRetentionTime() const
{
    try {return frameInfo_->AcqTimeRange->Min;} CATCH_AND_FORWARD
}

int FrameImpl::getDriftBinsPresent() const { return numDriftBins_; }

const std::vector<short>& FrameImpl::getNonEmptyDriftBins() const
{
    if (nonEmptyDriftBins_.empty())
        ToStdVector(imsReader_->NonEmptyDriftBins(frameIndex_ + 1), nonEmptyDriftBins_);
    return nonEmptyDriftBins_;
}

DriftScanPtr FrameImpl::getScan(int driftBinIndex) const
{
    try
    {
        MIDAC::IMidacSpecDataMs^ specData = (MIDAC::IMidacSpecDataMs^) specData_;
        imsReader_->FrameMs(frameIndex_ + 1, driftBinIndex, MIDAC::MidacSpecFormat::ZeroBounded, true, (MIDAC::IMidacSpecDataMs^%) specData);
        if (Object::ReferenceEquals(specData, nullptr))
            throw gcnew System::Exception(ToSystemString("null spectrum returned for frame ") + frameIndex_ + " and drift bin " + driftBinIndex);;
        specData_ = specData;
        return DriftScanPtr(new DriftScanImpl(specData_));
    }
    CATCH_AND_FORWARD
}

DriftScanPtr FrameImpl::getTotalScan() const
{
    try
    {
        MIDAC::IMidacSpecDataMs^ specData = imsReader_->ProfileTotalFrameMs(MIDAC::MidacSpecFormat::ZeroBounded, frameIndex_+ 1);
        if (Object::ReferenceEquals(specData, nullptr))
            throw gcnew System::Exception(ToSystemString("null spectrum returned for total scan of frame ") + frameIndex_);
        specData_ = specData;
        return DriftScanPtr(new DriftScanImpl(specData_));
    }
    CATCH_AND_FORWARD
}


DriftScanImpl::DriftScanImpl(MIDAC::IMidacSpecDataMs^ specData)
{
    ToBinaryData(specData->XArray, xArray_);
    ToBinaryData(specData->YArray, yArray_);
    msStorageMode_ = (MSStorageMode)specData->MsStorageMode;
    deviceType_ = specData->DeviceInfo != nullptr ? (DeviceType)specData->DeviceInfo->DeviceType : DeviceType_Unknown;
    if (specData->MzOfInterestRanges != nullptr)
        for (int i = 0; i < specData->MzOfInterestRanges->Length; ++i)
            precursorIons_.push_back((specData->MzOfInterestRanges[i]->Max + specData->MzOfInterestRanges[i]->Min) / 2.0);
    collisionEnergy_ = specData->FragmentationEnergyRange != nullptr ? specData->FragmentationEnergyRange->Max : 0;
    driftTime_ = specData->DriftTimeRanges != nullptr && specData->DriftTimeRanges->Length > 0 ? specData->DriftTimeRanges[0]->Min : 0;
    scanId_ = specData->ScanId;
}

MSStorageMode DriftScanImpl::getMSStorageMode() const { return msStorageMode_; }
DeviceType DriftScanImpl::getDeviceType() const { return deviceType_; }
void DriftScanImpl::getPrecursorIons(std::vector<double>& precursorIons) const { boost::copy(precursorIons_, precursorIons.end()); }
double DriftScanImpl::getCollisionEnergy() const { return collisionEnergy_; }
double DriftScanImpl::getDriftTime() const { return driftTime_; }
int DriftScanImpl::getScanId() const { return scanId_; }

int DriftScanImpl::getTotalDataPoints() const { return xArray_.size(); }
const pwiz::util::BinaryData<double>& DriftScanImpl::getXArray() const { return xArray_; }
const pwiz::util::BinaryData<float>& DriftScanImpl::getYArray() const { return yArray_; }


} // Agilent
} // vendor_api
} // pwiz
