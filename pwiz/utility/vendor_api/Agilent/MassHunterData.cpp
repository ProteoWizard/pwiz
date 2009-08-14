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
#include <gcroot.h>

using System::String;
using System::Math;
namespace MHDAC = Agilent::MassSpectrometry::DataAnalysis;

// forwards managed exception to unmanaged code
#define CATCH_AND_FORWARD(x) \
    try \
    { x } \
    catch (System::ApplicationException^ e) \
    { throw std::runtime_error(ToStdString(e->Message)); }

#include <vcclr.h>
namespace {

std::string ToStdString(System::String^ source)
{
	int len = (( source->Length+1) * 2);
	char *ch = new char[ len ];
	bool result ;
	{
		pin_ptr<const wchar_t> wch = PtrToStringChars( source );
		result = wcstombs( ch, wch, len ) != -1;
	}
	std::string target = ch;
	delete ch;
	if(!result)
        throw gcnew System::Exception("error converting System::String to std::string");
	return target;
}

System::String^ ToSystemString(const std::string& source)
{
    return gcnew System::String(source.c_str());
}

template<typename managed_value_type, typename native_value_type>
void ToStdVector(cli::array<managed_value_type>^ managedArray, std::vector<native_value_type>& stdVector)
{
    stdVector.resize(managedArray->Length);
    for (int i=0; i < managedArray->Length; ++i)
        stdVector[i] = static_cast<native_value_type>(managedArray[i]);
}

template<typename managed_value_type, typename native_value_type>
void ToAutomationVector(cli::array<managed_value_type>^ managedArray, automation_vector<native_value_type>& automationArray)
{
    VARIANT v;
    ::VariantInit(&v);
    System::IntPtr vPtr = (System::IntPtr) &v;
    System::Runtime::InteropServices::Marshal::GetNativeVariantForObject((System::Object^) managedArray, vPtr);
    automationArray.attach(v);
}

}


namespace pwiz {
namespace vendor_api {
namespace Agilent {


namespace {
        
MHDAC::IMsdrPeakFilter^ msdrPeakFilter(PeakFilterPtr peakFilter)
{
    MHDAC::IMsdrPeakFilter^ result = gcnew MHDAC::MsdrPeakFilter();
    if (!peakFilter.get()) return nullptr;
    result->MaxNumPeaks = peakFilter->maxNumPeaks;
    result->AbsoluteThreshold = peakFilter->absoluteThreshold;
    result->RelativeThreshold = peakFilter->relativeThreshold;
    return result;
}

MHDAC::IBDASpecFilter^ bdaSpecFilterForScanId(int scanId)
{
    MHDAC::IBDASpecFilter^ result = gcnew MHDAC::BDASpecFilter();
    result->ScanIds = gcnew cli::array<int> { scanId };
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

MassHunterDataImpl::MassHunterDataImpl(const std::string& path)
{
    CATCH_AND_FORWARD
    (
        reader_ = gcnew MHDAC::MassSpecDataReader();
        if (!reader_->OpenDataFile(gcnew String(path.c_str())))
            throw std::runtime_error("[MassHunterDataImpl::ctor()] Error opening source path.");

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
    )
}

MassHunterDataImpl::~MassHunterDataImpl()
{
    CATCH_AND_FORWARD
    (
        reader_->CloseDataFile();
    )
}

std::string MassHunterDataImpl::getVersion() const
{
    CATCH_AND_FORWARD( return ToStdString(reader_->Version); )
}

DeviceType MassHunterDataImpl::getDeviceType() const
{
    CATCH_AND_FORWARD( return (DeviceType) scanFileInfo_->DeviceType; )
}

std::string MassHunterDataImpl::getDeviceName(DeviceType deviceType) const
{
    CATCH_AND_FORWARD
    (
        return ToStdString(reader_->FileInformation->GetDeviceName((MHDAC::DeviceType) deviceType));
    )
}

blt::local_date_time MassHunterDataImpl::getAcquisitionTime() const
{
    CATCH_AND_FORWARD
    (
        bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(reader_->FileInformation->AcquisitionTime.ToUniversalTime().ToOADate()));
        return blt::local_date_time(pt, blt::time_zone_ptr()); // keep time as UTC
    )
}

IonizationMode MassHunterDataImpl::getIonModes() const
{
    CATCH_AND_FORWARD( return (IonizationMode) scanFileInfo_->IonModes; )
}

MSScanType MassHunterDataImpl::getScanTypes() const
{
    CATCH_AND_FORWARD( return (MSScanType) scanFileInfo_->ScanTypes; )
}

MSStorageMode MassHunterDataImpl::getSpectraFormat() const
{
    CATCH_AND_FORWARD( return (MSStorageMode) scanFileInfo_->SpectraFormat; )
}

int MassHunterDataImpl::getTotalScansPresent() const
{
    CATCH_AND_FORWARD( return (int) scanFileInfo_->TotalScansPresent; )
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
//    CATCH_AND_FORWARD
//    (
    MHDAC::IBDAChromFilter^ filter = gcnew MHDAC::BDAChromFilter();
    filter->ChromatogramType = transition.type == Transition::MRM ? MHDAC::ChromType::MultipleReactionMode
                                                                  : MHDAC::ChromType::SelectedIonMonitoring;
    filter->ExtractOneChromatogramPerScanSegment = true;
    filter->DoCycleSum = false;

    if (!transitionToChromatogramIndexMap_.count(transition))
        throw std::runtime_error("[MassHunterData::getChromatogram()] No chromatogram corresponds to the transition.");

    int index = transitionToChromatogramIndexMap_.find(transition)->second;
    ChromatogramImplPtr chromatogramPtr(new ChromatogramImpl(reader_->GetChromatogram(filter)[index]));
    return chromatogramPtr;
//    )
}

SpectrumPtr MassHunterDataImpl::getProfileSpectrumByRow(int rowNumber) const
{
    return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(rowNumber, nullptr, nullptr)));
}

SpectrumPtr MassHunterDataImpl::getPeakSpectrumByRow(int rowNumber, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    MHDAC::IMsdrPeakFilter^ msdrPeakFilter_ = msdrPeakFilter(peakFilter);
    return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(rowNumber, msdrPeakFilter_, msdrPeakFilter_)));
}

SpectrumPtr MassHunterDataImpl::getProfileSpectrumById(int scanId) const
{
    return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(bdaSpecFilterForScanId(scanId), nullptr)[0]));
}

SpectrumPtr MassHunterDataImpl::getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter /*= PeakFilterPtr()*/) const
{
    return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(bdaSpecFilterForScanId(scanId), msdrPeakFilter(peakFilter))[0]));
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
    CATCH_AND_FORWARD( return (MSScanType) specData_->MSScanType; )
}

MSStorageMode SpectrumImpl::getMSStorageMode() const
{
    CATCH_AND_FORWARD( return (MSStorageMode) specData_->MSStorageMode; )
}

IonPolarity SpectrumImpl::getIonPolarity() const
{
    CATCH_AND_FORWARD( return (IonPolarity) specData_->IonPolarity; )
}

DeviceType SpectrumImpl::getDeviceType() const
{
    CATCH_AND_FORWARD( return (DeviceType) specData_->DeviceType; )
}

MassRange SpectrumImpl::getMeasuredMassRange() const
{
    CATCH_AND_FORWARD
    ( 
        MHDAC::IRange^ massRange = specData_->MeasuredMassRange;
        MassRange mr;
        mr.start = massRange->Start;
        mr.end = massRange->End;
        return mr;
    )
}

int SpectrumImpl::getParentScanId() const
{
    CATCH_AND_FORWARD( return (int) specData_->ParentScanId; )
}

void SpectrumImpl::getPrecursorIons(vector<double>& precursorIons) const
{
    int count;
    CATCH_AND_FORWARD( return ToStdVector(specData_->GetPrecursorIon(count), precursorIons); )
}

bool SpectrumImpl::getPrecursorCharge(int& charge) const
{
    CATCH_AND_FORWARD( return specData_->GetPrecursorCharge(charge); )
}

bool SpectrumImpl::getPrecursorIntensity(double& precursorIntensity) const
{
    CATCH_AND_FORWARD( return specData_->GetPrecursorIntensity(precursorIntensity); )
}

double SpectrumImpl::getCollisionEnergy() const
{
    CATCH_AND_FORWARD( return specData_->CollisionEnergy; )
}

int SpectrumImpl::getScanId() const
{
    CATCH_AND_FORWARD( return specData_->ScanId; )
}

int SpectrumImpl::getTotalDataPoints() const
{
    CATCH_AND_FORWARD( return specData_->TotalDataPoints; )
}

void SpectrumImpl::getXArray(automation_vector<double>& x) const
{
    CATCH_AND_FORWARD( return ToAutomationVector(specData_->XArray, x); )
}

void SpectrumImpl::getYArray(automation_vector<float>& y) const
{
    CATCH_AND_FORWARD( return ToAutomationVector(specData_->YArray, y); )
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
    CATCH_AND_FORWARD( return chromData_->CollisionEnergy; )
}

int ChromatogramImpl::getTotalDataPoints() const
{
    CATCH_AND_FORWARD( return chromData_->TotalDataPoints; )
}

void ChromatogramImpl::getXArray(automation_vector<double>& x) const
{
    CATCH_AND_FORWARD( return ToAutomationVector(chromData_->XArray, x); )
}

void ChromatogramImpl::getYArray(automation_vector<float>& y) const
{
    CATCH_AND_FORWARD( return ToAutomationVector(chromData_->YArray, y); )
}


} // Agilent
} // vendor_api
} // pwiz
