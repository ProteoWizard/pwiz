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
#include "MassHunterData.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;


using System::String;
using System::Math;
namespace MHDAC = Agilent::MassSpectrometry::DataAnalysis;


namespace pwiz {
namespace vendor_api {
namespace Agilent {


namespace {
        
MHDAC::IMsdrPeakFilter^ msdrPeakFilter(PeakFilterPtr peakFilter)
{
    MHDAC::IMsdrPeakFilter^ result = gcnew MHDAC::MsdrPeakFilter();
    if (peakFilter.get())
    {
        result->MaxNumPeaks = peakFilter->maxNumPeaks;
        result->AbsoluteThreshold = peakFilter->absoluteThreshold;
        result->RelativeThreshold = peakFilter->relativeThreshold;
    }
    return result;
}

MHDAC::IBDASpecFilter^ bdaSpecFilterForScanId(int scanId, bool preferProfileData = false )
{
    MHDAC::IBDASpecFilter^ result = gcnew MHDAC::BDASpecFilter();
    result->ScanIds = gcnew cli::array<int> { scanId };
    result->SpectrumType = MHDAC::SpecType::MassSpectrum;

    // default is DesiredMSStorageType::PeakElseProfile
    if (preferProfileData)
        result->DesiredMSStorageType = MHDAC::DesiredMSStorageType::ProfileElsePeak;
    else
        result->DesiredMSStorageType = MHDAC::DesiredMSStorageType::PeakElseProfile;

    return result;
}

} // namespace


class MassHunterDataImpl : public MassHunterData
{
    public:
    MassHunterDataImpl(const std::string& path);
    ~MassHunterDataImpl();

    virtual std::string getVersion() const;
    virtual DeviceType getDeviceType() const;
    virtual std::string getDeviceName(DeviceType deviceType) const;
    virtual blt::local_date_time getAcquisitionTime() const;
    virtual IonizationMode getIonModes() const;
    virtual MSScanType getScanTypes() const;
    virtual MSStorageMode getSpectraFormat() const;
    virtual int getTotalScansPresent() const;

    virtual const set<Transition>& getTransitions() const;
    virtual ChromatogramPtr getChromatogram(const Transition& transition) const;

    virtual const automation_vector<double>& getTicTimes() const;
    virtual const automation_vector<double>& getBpcTimes() const;
    virtual const automation_vector<float>& getTicIntensities() const;
    virtual const automation_vector<float>& getBpcIntensities() const;

    virtual SpectrumPtr getProfileSpectrumByRow(int row) const;
    virtual SpectrumPtr getPeakSpectrumByRow(int row, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    virtual SpectrumPtr getProfileSpectrumById(int scanId) const;
    virtual SpectrumPtr getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    private:
    gcroot<MHDAC::IMsdrDataReader^> reader_;
    gcroot<MHDAC::IBDAMSScanFileInformation^> scanFileInfo_;
    automation_vector<double> ticTimes_, bpcTimes_;
    automation_vector<float> ticIntensities_, bpcIntensities_;
    set<Transition> transitions_;
    map<Transition, int> transitionToChromatogramIndexMap_;
};

typedef boost::shared_ptr<MassHunterDataImpl> MassHunterDataImplPtr;

struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(MHDAC::IBDASpecData^ specData);
    ~SpectrumImpl();

    virtual MSScanType getMSScanType() const;
    virtual MSStorageMode getMSStorageMode() const;
    virtual IonPolarity getIonPolarity() const;
    virtual DeviceType getDeviceType() const;
    virtual MassRange getMeasuredMassRange() const;
    virtual int getParentScanId() const;
    virtual void getPrecursorIons(vector<double>& precursorIons) const;
    virtual bool getPrecursorCharge(int& charge) const;
    virtual bool getPrecursorIntensity(double& precursorIntensity) const;
    virtual double getCollisionEnergy() const;
    virtual int getScanId() const;
    virtual int getTotalDataPoints() const;
    virtual void getXArray(automation_vector<double>& x) const;
    virtual void getYArray(automation_vector<float>& y) const;

    private:
    gcroot<MHDAC::IBDASpecData^> specData_;
};

typedef boost::shared_ptr<SpectrumImpl> SpectrumImplPtr;

struct ChromatogramImpl : public Chromatogram
{
    ChromatogramImpl(MHDAC::IBDAChromData^ chromData);
    ~ChromatogramImpl();

    virtual double getCollisionEnergy() const;
    virtual int getTotalDataPoints() const;
    virtual void getXArray(automation_vector<double>& x) const;
    virtual void getYArray(automation_vector<float>& y) const;

    private:
    gcroot<MHDAC::IBDAChromData^> chromData_;
};

typedef boost::shared_ptr<ChromatogramImpl> ChromatogramImplPtr;

#pragma unmanaged
PWIZ_API_DECL
bool Transition::operator< (const Transition& rhs) const
{
    if (type == rhs.type)
        if (Q1 == rhs.Q1)
            if (Q3 == rhs.Q3)
                if (acquiredTimeRange.start == rhs.acquiredTimeRange.start)
                    return acquiredTimeRange.end < rhs.acquiredTimeRange.end;
                else
                    return acquiredTimeRange.start < rhs.acquiredTimeRange.start;
            else
                return Q3 < rhs.Q3;
        else
            return Q1 < rhs.Q1;
    else
        return type < rhs.type;
}


PWIZ_API_DECL
MassHunterDataPtr MassHunterData::create(const string& path)
{
    MassHunterDataImplPtr dataReader(new MassHunterDataImpl(path));
    return boost::static_pointer_cast<MassHunterData>(dataReader);
}

#pragma managed
MassHunterDataImpl::MassHunterDataImpl(const std::string& path)
{
    try
    {
        reader_ = gcnew MHDAC::MassSpecDataReader();
        if (!reader_->OpenDataFile(ToSystemString(path)))
        {}    // TODO: log warning about incomplete acquisition, possibly indicating corrupt data

        scanFileInfo_ = reader_->MSScanFileInformation;

        // cycle summing can make the full file chromatograms have the wrong number of points
        MHDAC::IBDAChromFilter^ filter = gcnew MHDAC::BDAChromFilter();
        filter->DoCycleSum = false;

        // set filter for TIC
        filter->ChromatogramType = MHDAC::ChromType::TotalIon;
        MHDAC::IBDAChromData^ tic = reader_->GetChromatogram(filter)[0];
        ToAutomationVector(tic->XArray, ticTimes_);
        ToAutomationVector(tic->YArray, ticIntensities_);

        // set filter for BPC
        filter->ChromatogramType = MHDAC::ChromType::BasePeak;
        MHDAC::IBDAChromData^ bpc = reader_->GetChromatogram(filter)[0];
        ToAutomationVector(bpc->XArray, bpcTimes_);
        ToAutomationVector(bpc->YArray, bpcIntensities_);

        // calculate total chromatograms present
        filter->ExtractOneChromatogramPerScanSegment = true;

        // note: we use this instead of MassSpecDataReader.MRMTransitions in case of time segments
        filter->ChromatogramType = MHDAC::ChromType::MultipleReactionMode;
        for each (MHDAC::IBDAChromData^ chromatogram in reader_->GetChromatogram(filter))
        {
            if (chromatogram->MZOfInterest->Length == 0 ||
                chromatogram->MeasuredMassRange->Length == 0)
                // TODO: log this anomaly
                continue;

            Transition t;
            t.type = Transition::MRM;
            t.Q1 = chromatogram->MZOfInterest[0]->Start;
            t.Q3 = chromatogram->MeasuredMassRange[0]->Start;

            if (chromatogram->AcquiredTimeRange->Length > 0)
            {
                t.acquiredTimeRange.start = chromatogram->AcquiredTimeRange[0]->Start;
                t.acquiredTimeRange.end = chromatogram->AcquiredTimeRange[0]->End;
            }
            else
                t.acquiredTimeRange.start = t.acquiredTimeRange.end = 0;

            transitionToChromatogramIndexMap_[t] = transitions_.size();
            transitions_.insert(t);
        }

        int mrmCount = transitions_.size();

        filter->ChromatogramType = MHDAC::ChromType::SelectedIonMonitoring;
        for each (MHDAC::IBDAChromData^ chromatogram in reader_->GetChromatogram(filter))
        {
            if (chromatogram->MeasuredMassRange->Length == 0)
                // TODO: log this anomaly
                continue;

            Transition t;
            t.type = Transition::SIM;
            t.Q1 = chromatogram->MeasuredMassRange[0]->Start;
            t.Q3 = 0;

            if (chromatogram->AcquiredTimeRange->Length > 0)
            {
                t.acquiredTimeRange.start = chromatogram->AcquiredTimeRange[0]->Start;
                t.acquiredTimeRange.end = chromatogram->AcquiredTimeRange[0]->End;
            }
            else
                t.acquiredTimeRange.start = t.acquiredTimeRange.end = 0;

            transitionToChromatogramIndexMap_[t] = transitions_.size() - mrmCount;
            transitions_.insert(t);
        }
    }
    CATCH_AND_FORWARD
}

MassHunterDataImpl::~MassHunterDataImpl()
{
    try {reader_->CloseDataFile();} CATCH_AND_FORWARD
}

std::string MassHunterDataImpl::getVersion() const
{
    try {return ToStdString(reader_->Version);} CATCH_AND_FORWARD
}

DeviceType MassHunterDataImpl::getDeviceType() const
{
    try {return (DeviceType) scanFileInfo_->DeviceType;} CATCH_AND_FORWARD
}

std::string MassHunterDataImpl::getDeviceName(DeviceType deviceType) const
{
    try {return ToStdString(reader_->FileInformation->GetDeviceName((MHDAC::DeviceType) deviceType));} CATCH_AND_FORWARD
}

blt::local_date_time MassHunterDataImpl::getAcquisitionTime() const
{
    try
    {
        bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(reader_->FileInformation->AcquisitionTime.ToUniversalTime().ToOADate()));
        return blt::local_date_time(pt, blt::time_zone_ptr()); // keep time as UTC
    }
    CATCH_AND_FORWARD
}

IonizationMode MassHunterDataImpl::getIonModes() const
{
    try {return (IonizationMode) scanFileInfo_->IonModes;} CATCH_AND_FORWARD
}

MSScanType MassHunterDataImpl::getScanTypes() const
{
    try {return (MSScanType) scanFileInfo_->ScanTypes;} CATCH_AND_FORWARD
}

MSStorageMode MassHunterDataImpl::getSpectraFormat() const
{
    try {return (MSStorageMode) scanFileInfo_->SpectraFormat;} CATCH_AND_FORWARD
}

int MassHunterDataImpl::getTotalScansPresent() const
{
    try {return (int) scanFileInfo_->TotalScansPresent;} CATCH_AND_FORWARD
}

const set<Transition>& MassHunterDataImpl::getTransitions() const
{
    return transitions_;
}

const automation_vector<double>& MassHunterDataImpl::getTicTimes() const
{
    return ticTimes_;
}

const automation_vector<double>& MassHunterDataImpl::getBpcTimes() const
{
    return bpcTimes_;
}

const automation_vector<float>& MassHunterDataImpl::getTicIntensities() const
{
    return ticIntensities_;
}

const automation_vector<float>& MassHunterDataImpl::getBpcIntensities() const
{
    return bpcIntensities_;
}

ChromatogramPtr MassHunterDataImpl::getChromatogram(const Transition& transition) const
{
    try
    {
        MHDAC::IBDAChromFilter^ filter = gcnew MHDAC::BDAChromFilter();
        filter->ChromatogramType = transition.type == Transition::MRM ? MHDAC::ChromType::MultipleReactionMode
                                                                      : MHDAC::ChromType::SelectedIonMonitoring;
        filter->ExtractOneChromatogramPerScanSegment = true;
        filter->DoCycleSum = false;

        if (!transitionToChromatogramIndexMap_.count(transition))
            throw gcnew System::Exception("[MassHunterData::getChromatogram()] No chromatogram corresponds to the transition.");

        int index = transitionToChromatogramIndexMap_.find(transition)->second;
        ChromatogramImplPtr chromatogramPtr(new ChromatogramImpl(reader_->GetChromatogram(filter)[index]));
        return chromatogramPtr;
    }
    CATCH_AND_FORWARD
}

SpectrumPtr MassHunterDataImpl::getProfileSpectrumByRow(int rowNumber) const
{
    return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(rowNumber, nullptr, nullptr, MHDAC::DesiredMSStorageType::ProfileElsePeak)));
}

SpectrumPtr MassHunterDataImpl::getPeakSpectrumByRow(int rowNumber, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    try
    {
        // MHDAC doesn't support post-acquisition centroiding of non-TOF spectra
        MHDAC::IMsdrPeakFilter^ msdrPeakFilter_ = nullptr;
        if (scanFileInfo_->DeviceType != MHDAC::DeviceType::Quadrupole &&
            scanFileInfo_->DeviceType != MHDAC::DeviceType::TandemQuadrupole)
            msdrPeakFilter_ = msdrPeakFilter(peakFilter);
        return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(rowNumber, msdrPeakFilter_, msdrPeakFilter_, MHDAC::DesiredMSStorageType::PeakElseProfile)));
    }
    CATCH_AND_FORWARD
}

SpectrumPtr MassHunterDataImpl::getProfileSpectrumById(int scanId) const
{
    try {return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(bdaSpecFilterForScanId(scanId, true), nullptr)[0]));} CATCH_AND_FORWARD
}

SpectrumPtr MassHunterDataImpl::getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    try
    {
        // MHDAC doesn't support post-acquisition centroiding of non-TOF spectra
        MHDAC::IMsdrPeakFilter^ msdrPeakFilter_ = nullptr;
        if (scanFileInfo_->DeviceType != MHDAC::DeviceType::Quadrupole &&
            scanFileInfo_->DeviceType != MHDAC::DeviceType::TandemQuadrupole)
            msdrPeakFilter_ = msdrPeakFilter(peakFilter);
        return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(bdaSpecFilterForScanId(scanId), msdrPeakFilter_)[0]));
    }
    CATCH_AND_FORWARD
}


SpectrumImpl::SpectrumImpl(MHDAC::IBDASpecData^ specData)
{
    specData_ = specData;
}

SpectrumImpl::~SpectrumImpl()
{
}

MSScanType SpectrumImpl::getMSScanType() const
{
    try {return (MSScanType) specData_->MSScanType;} CATCH_AND_FORWARD
}

MSStorageMode SpectrumImpl::getMSStorageMode() const
{
    try {return (MSStorageMode) specData_->MSStorageMode;} CATCH_AND_FORWARD
}

IonPolarity SpectrumImpl::getIonPolarity() const
{
    try {return (IonPolarity) specData_->IonPolarity;} CATCH_AND_FORWARD
}

DeviceType SpectrumImpl::getDeviceType() const
{
    try {return (DeviceType) specData_->DeviceType;} CATCH_AND_FORWARD
}

MassRange SpectrumImpl::getMeasuredMassRange() const
{
    try
    {
        MHDAC::IRange^ massRange = specData_->MeasuredMassRange;
        MassRange mr;
        mr.start = massRange->Start;
        mr.end = massRange->End;
        return mr;
    }
    CATCH_AND_FORWARD
}

int SpectrumImpl::getParentScanId() const
{
    try {return (int) specData_->ParentScanId;} CATCH_AND_FORWARD
}

void SpectrumImpl::getPrecursorIons(vector<double>& precursorIons) const
{
    int count;
    try {return ToStdVector(specData_->GetPrecursorIon(count), precursorIons);} CATCH_AND_FORWARD
}

bool SpectrumImpl::getPrecursorCharge(int& charge) const
{
    try {return specData_->GetPrecursorCharge(charge);} CATCH_AND_FORWARD
}

bool SpectrumImpl::getPrecursorIntensity(double& precursorIntensity) const
{
    try {return specData_->GetPrecursorIntensity(precursorIntensity);} CATCH_AND_FORWARD
}

double SpectrumImpl::getCollisionEnergy() const
{
    try {return specData_->CollisionEnergy;} CATCH_AND_FORWARD
}

int SpectrumImpl::getScanId() const
{
    try {return specData_->ScanId;} CATCH_AND_FORWARD
}

int SpectrumImpl::getTotalDataPoints() const
{
    try {return specData_->TotalDataPoints;} CATCH_AND_FORWARD
}

void SpectrumImpl::getXArray(automation_vector<double>& x) const
{
    try {return ToAutomationVector(specData_->XArray, x);} CATCH_AND_FORWARD
}

void SpectrumImpl::getYArray(automation_vector<float>& y) const
{
    try {return ToAutomationVector(specData_->YArray, y);} CATCH_AND_FORWARD
}


ChromatogramImpl::ChromatogramImpl(MHDAC::IBDAChromData^ chromData)
{
    chromData_ = chromData;
}

ChromatogramImpl::~ChromatogramImpl()
{
}

double ChromatogramImpl::getCollisionEnergy() const
{
    try {return chromData_->CollisionEnergy;} CATCH_AND_FORWARD
}

int ChromatogramImpl::getTotalDataPoints() const
{
    try {return chromData_->TotalDataPoints;} CATCH_AND_FORWARD
}

void ChromatogramImpl::getXArray(automation_vector<double>& x) const
{
    try {return ToAutomationVector(chromData_->XArray, x);} CATCH_AND_FORWARD
}

void ChromatogramImpl::getYArray(automation_vector<float>& y) const
{
    try {return ToAutomationVector(chromData_->YArray, y);} CATCH_AND_FORWARD
}


} // Agilent
} // vendor_api
} // pwiz
