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
#include "MassHunterData.hpp"
#include "MidacData.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"

#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;

#using <System.dll>
#using <System.Data.dll>
using System::String;
using System::Math;
using System::Object;
namespace MHDAC = Agilent::MassSpectrometry::DataAnalysis;
namespace MIDAC = Agilent::MassSpectrometry::MIDAC;


namespace pwiz {
namespace vendor_api {
namespace Agilent {


namespace {

using namespace pwiz::minimxml;
using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;

struct Device
{
    int DeviceID;
    string Name;
    string DriverVersion;
    string FirmwareVersion;
    string ModelNumber;
    string OrdinalNumber;
    string SerialNumber;
    string Type;
    string StoredDataType;
    string Delay;
    string Vendor;
};

#pragma unmanaged
struct HandlerDevices : public SAXParser::Handler
{
    vector<Device> devices;
    string* currentProperty;

    HandlerDevices() : currentProperty(nullptr)
    {
        parseCharacters = true;
    }

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "Device")
        {
            devices.push_back(Device());
            getAttribute(attributes, "DeviceID", devices.back().DeviceID);
        }
        else if (name == "Devices" || name == "Version") return Status::Ok;
        else if (name == "Name") currentProperty = &devices.back().Name;
        else if (name == "DriverVersion") currentProperty = &devices.back().DriverVersion;
        else if (name == "FirmwareVersion") currentProperty = &devices.back().FirmwareVersion;
        else if (name == "ModelNumber") currentProperty = &devices.back().ModelNumber;
        else if (name == "OrdinalNumber") currentProperty = &devices.back().OrdinalNumber;
        else if (name == "SerialNumber") currentProperty = &devices.back().SerialNumber;
        else if (name == "Type") currentProperty = &devices.back().Type;
        else if (name == "StoredDataType") currentProperty = &devices.back().StoredDataType;
        else if (name == "Delay") currentProperty = &devices.back().Delay;
        else if (name == "Vendor") currentProperty = &devices.back().Vendor;
        else
            throw runtime_error(("[HandlerDevices] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        if (currentProperty)
        {
            currentProperty->assign(text.c_str());
            currentProperty = nullptr;
        }

        return Status::Ok;
    }
};
#pragma managed

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

template<typename N, typename M>
N managedRangeToNative(M^ input_range)
{
    N output_range;
    output_range.start = input_range->Min;
    output_range.end = input_range->Max;
    return output_range;
}

boost::mutex massHunterInitMutex;

} // namespace


class MassHunterDataImpl : public MassHunterData
{
    public:
    MassHunterDataImpl(const std::string& path);
    ~MassHunterDataImpl() noexcept(false);

    virtual std::string getVersion() const;
    virtual DeviceType getDeviceType() const;
    virtual std::string getDeviceName(DeviceType deviceType) const;
    virtual blt::local_date_time getAcquisitionTime(bool adjustToHostTime) const;
    virtual IonizationMode getIonModes() const;
    virtual MSScanType getScanTypes() const;
    virtual MSStorageMode getSpectraFormat() const;
    virtual int getTotalScansPresent() const;
    virtual bool hasProfileData() const;

    virtual bool hasIonMobilityData() const;
    virtual int getTotalIonMobilityFramesPresent() const;
    virtual FramePtr getIonMobilityFrame(int frameIndex) const;
    virtual bool canConvertDriftTimeAndCCS() const { return false; }
    virtual double driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const { throw runtime_error("[MassHunterDataImpl::driftTimeToCCS] not available on non-IMS data"); }
    virtual double ccsToDriftTime(double ccs, double mz, int charge) const { throw runtime_error("[MassHunterDataImpl::ccsToDriftTime] not available on non-IMS data"); }

    virtual const set<Transition>& getTransitions() const;
    virtual MassChromatogramPtr getChromatogram(const Transition& transition) const;

    virtual const vector<Signal>& getSignals() const;
    virtual SignalChromatogramPtr getSignal(const Signal& signal) const;

    virtual const BinaryData<double>& getTicTimes(bool ms1Only) const;
    virtual const BinaryData<double>& getBpcTimes(bool ms1Only) const;
    virtual const BinaryData<float>& getTicIntensities(bool ms1Only) const;
    virtual const BinaryData<float>& getBpcIntensities(bool ms1Only) const;

    virtual ScanRecordPtr getScanRecord(int row) const;
    virtual SpectrumPtr getProfileSpectrumByRow(int row) const;
    virtual SpectrumPtr getPeakSpectrumByRow(int row, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    virtual SpectrumPtr getProfileSpectrumById(int scanId) const;
    virtual SpectrumPtr getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    private:
    gcroot<MHDAC::IMsdrDataReader^> reader_;
    gcroot<MIDAC::IMidacImsReader^> imsReader_;
    gcroot<MHDAC::IBDAMSScanFileInformation^> scanFileInfo_;
    BinaryData<double> ticTimes_, ticTimesMs1_, bpcTimes_, bpcTimesMs1_;
    BinaryData<float> ticIntensities_, ticIntensitiesMs1_, bpcIntensities_, bpcIntensitiesMs1_;
    set<Transition> transitions_;
    map<Transition, int> transitionToChromatogramIndexMap_;

    // cached because MassHunter can only load all chromatograms
    // at the same time. Not caching caused serious performance problems.
    gcroot<array<MHDAC::IBDAChromData^>^> chromMrm_;
    // unable to achieve identical results with cached SIM chromatograms
    // gcroot<array<MHDAC::IBDAChromData^>^> chromSim_;

    mutable vector<Signal> signals_;
    mutable gcroot<System::Collections::Generic::Dictionary<String^, MHDAC::ISignalInfo^>^> signalInfoMap_;

    bool hasProfileData_;
};

typedef boost::shared_ptr<MassHunterDataImpl> MassHunterDataImplPtr;


// HACK: normally I would have a second Impl class for MIDAC, but I think future DLLs will merge the functionality together more smoothly,
// or MHDAC will disappear entirely
struct ScanRecordImpl : public ScanRecord
{
    ScanRecordImpl(MHDAC::IMSScanRecord^ scanRecord) : scanRecord_(scanRecord) {}

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
    gcroot<MHDAC::IMSScanRecord^> scanRecord_;
};


struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(MHDAC::IBDASpecData^ specData) : specData_(specData) {}

    virtual int getScanId() const {return specData_->ScanId;}
    virtual int getMSLevel() const {return specData_->MSLevelInfo == MHDAC::MSLevel::MSMS ? 2 : 1;}
    virtual MSScanType getMSScanType() const {return (MSScanType) specData_->MSScanType;}
    virtual MSStorageMode getMSStorageMode() const {return (MSStorageMode) specData_->MSStorageMode;}
    virtual IonPolarity getIonPolarity() const {return (IonPolarity) specData_->IonPolarity;}
    virtual DeviceType getDeviceType() const {return (DeviceType) specData_->DeviceType;}
    virtual double getCollisionEnergy() const {return specData_->CollisionEnergy;}
    virtual int getTotalDataPoints() const {return specData_->TotalDataPoints;}
    virtual int getParentScanId() const {return (int) specData_->ParentScanId;}

    virtual MassRange getMeasuredMassRange() const;
    virtual void getPrecursorIons(vector<double>& precursorIons) const;
    virtual bool getPrecursorCharge(int& charge) const;
    virtual bool getPrecursorIntensity(double& precursorIntensity) const;
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const;
    virtual void getYArray(pwiz::util::BinaryData<float>& y) const;

    private:
    gcroot<MHDAC::IBDASpecData^> specData_;
};


struct ChromatogramImpl : public virtual Chromatogram
{
    ChromatogramImpl(MHDAC::IBDAChromData^ chromData) : chromData_(chromData) {}

    virtual int getTotalDataPoints() const;
    virtual void getXArray(BinaryData<double>& x) const;
    virtual void getYArray(BinaryData<float>& y) const;

    virtual ~ChromatogramImpl() {}

protected:
    gcroot<MHDAC::IBDAChromData^> chromData_;
};


struct MassChromatogramImpl : public virtual ChromatogramImpl, public virtual MassChromatogram
{
    MassChromatogramImpl(MHDAC::IBDAChromData^ chromData) : ChromatogramImpl(chromData), MassChromatogram() {}

    virtual double getCollisionEnergy() const;
    virtual IonPolarity getIonPolarity() const;

    virtual int getTotalDataPoints() const { return static_cast<ChromatogramImpl>(*this).getTotalDataPoints(); }
    virtual void getXArray(BinaryData<double>& x) const { static_cast<ChromatogramImpl>(*this).getXArray(x); }
    virtual void getYArray(BinaryData<float>& y) const { static_cast<ChromatogramImpl>(*this).getYArray(y); }
};


struct SignalChromatogramImpl : public virtual ChromatogramImpl, public virtual SignalChromatogram
{
    SignalChromatogramImpl(MHDAC::IBDAChromData^ chromData) : ChromatogramImpl(chromData), SignalChromatogram() {}

    virtual int getTotalDataPoints() const { return static_cast<ChromatogramImpl>(*this).getTotalDataPoints(); }
    virtual void getXArray(BinaryData<double>& x) const { static_cast<ChromatogramImpl>(*this).getXArray(x); }
    virtual void getYArray(BinaryData<float>& y) const { static_cast<ChromatogramImpl>(*this).getYArray(y); }
};


#pragma unmanaged
PWIZ_API_DECL
bool Transition::operator< (const Transition& rhs) const
{
    if (type == rhs.type)
        if (ionPolarity == rhs.ionPolarity)
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
            return (ionPolarity < rhs.ionPolarity);
    else
        return type < rhs.type;
}

PWIZ_API_DECL
bool Signal::operator< (const Signal& rhs) const
{
    if (deviceName == rhs.deviceName)
        return signalName < rhs.signalName;
    else
        return deviceName < rhs.deviceName;
}


PWIZ_API_DECL
MassHunterDataPtr MassHunterData::create(const string& path)
{
    MassHunterDataPtr dataReader;
    if (MassHunterData::hasIonMobilityData(path))
        dataReader.reset(new MidacDataImpl(path));
    else
        dataReader.reset(new MassHunterDataImpl(path));
    return boost::static_pointer_cast<MassHunterData>(dataReader);
}


#pragma managed
bool MassHunterData::hasIonMobilityData(const string& path)
{
    try {return MIDAC::MidacFileAccess::FileHasImsData(ToSystemString(path));} CATCH_AND_FORWARD
}


MassHunterDataImpl::MassHunterDataImpl(const std::string& path)
{
    massHunterRootPath_ = path;

    try
    {
        String^ filepath = ToSystemString(path);

        {
            boost::mutex::scoped_lock lock(massHunterInitMutex);

            reader_ = gcnew MHDAC::MassSpecDataReader();
            if (!reader_->OpenDataFile(filepath))
            {
            }    // TODO: log warning about incomplete acquisition, possibly indicating corrupt data
        }

        hasProfileData_ = bfs::exists(bfs::path(path) / "AcqData/MSProfile.bin");

        scanFileInfo_ = reader_->MSScanFileInformation;

        // cycle summing can make the full file chromatograms have the wrong number of points
        MHDAC::IBDAChromFilter^ filter = gcnew MHDAC::BDAChromFilter();
        filter->DoCycleSum = false;

        // set filter for TIC
        filter->ChromatogramType = MHDAC::ChromType::TotalIon;
        MHDAC::IBDAChromData^ tic = reader_->GetChromatogram(filter)[0];
        ToBinaryData(tic->XArray, ticTimes_);
        ToBinaryData(tic->YArray, ticIntensities_);

        // set filter for BPC
        filter->ChromatogramType = MHDAC::ChromType::BasePeak;
        MHDAC::IBDAChromData^ bpc = reader_->GetChromatogram(filter)[0];
        ToBinaryData(bpc->XArray, bpcTimes_);
        ToBinaryData(bpc->YArray, bpcIntensities_);

        filter->MSLevelFilter = MHDAC::MSLevel::MS;
        filter->ChromatogramType = MHDAC::ChromType::TotalIon;
        tic = reader_->GetChromatogram(filter)[0];
        ToBinaryData(tic->XArray, ticTimesMs1_);
        ToBinaryData(tic->YArray, ticIntensitiesMs1_);

        // set filter for BPC
        filter->ChromatogramType = MHDAC::ChromType::BasePeak;
        bpc = reader_->GetChromatogram(filter)[0];
        ToBinaryData(bpc->XArray, bpcTimesMs1_);
        ToBinaryData(bpc->YArray, bpcIntensitiesMs1_);

        // chromatograms are always read completely into memory, and failing
        // to store them on this object after reading cost a 50x performance
        // hit on large MRM files.
        filter = gcnew MHDAC::BDAChromFilter();
        filter->DoCycleSum = false;
        filter->ExtractOneChromatogramPerScanSegment = true;
        filter->ChromatogramType = MHDAC::ChromType::MultipleReactionMode;
        array<MHDAC::IBDAChromData^>^ chromatograms = reader_->GetChromatogram(filter);
        for each (MHDAC::IBDAChromData^ chromatogram in chromatograms)
        {
            if (chromatogram->MZOfInterest->Length == 0 ||
                chromatogram->MeasuredMassRange->Length == 0)
                // TODO: log this anomaly
                continue;

            Transition t;
            t.type = Transition::MRM;
            t.Q1 = chromatogram->MZOfInterest[0]->Start;
            t.Q3 = chromatogram->MeasuredMassRange[0]->Start;
            switch (chromatogram->IonPolarity)
            {
            case MHDAC::IonPolarity::Positive:
                t.ionPolarity = IonPolarity::IonPolarity_Positive;
                break;
            case MHDAC::IonPolarity::Negative:
                t.ionPolarity = IonPolarity::IonPolarity_Negative;
                break;
            default:
                t.ionPolarity = IonPolarity::IonPolarity_Unassigned;
                break;
            }

            if (chromatogram->AcquiredTimeRange->Length > 0)
            {
                t.acquiredTimeRange.start = chromatogram->AcquiredTimeRange[0]->Start;
                t.acquiredTimeRange.end = chromatogram->AcquiredTimeRange[0]->End;
            }
            else
                t.acquiredTimeRange.start = t.acquiredTimeRange.end;

            transitionToChromatogramIndexMap_[t] = transitions_.size();
            transitions_.insert(t);
        }
        chromMrm_ = chromatograms;

        int mrmCount = transitions_.size();

        filter->ChromatogramType = MHDAC::ChromType::SelectedIonMonitoring;
        chromatograms = reader_->GetChromatogram(filter);
        for each (MHDAC::IBDAChromData^ chromatogram in chromatograms)
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
        // unfortunately, storing the chromatograms read here for SelectedIonMonitoring
        // even with the new filter did not produce results identical to the original
        // code, causing tests to fail.
        // someone with more knowledge of the tests and SIM would have to fix this.
        // chromSim_ = chromatograms;
    }
    CATCH_AND_FORWARD
}

MassHunterDataImpl::~MassHunterDataImpl() noexcept(false)
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

std::string MassHunterData::getDeviceSerialNumber(DeviceType deviceType) const
{
    bfs::path massHunterDevicesPath(massHunterRootPath_);
    massHunterDevicesPath /= "AcqData/Devices.xml";
    if (!bfs::exists(massHunterDevicesPath))
        return "";

    ifstream devicesXml(massHunterDevicesPath.string().c_str(), ios::binary);
    HandlerDevices handler;
    SAXParser::parse(devicesXml, handler);

    if (handler.devices.empty())
        return "";

    auto findItr = std::find_if(handler.devices.begin(), handler.devices.end(), [&](const Device& device) { return lexical_cast<int>(device.Type) == (int) deviceType; });
    if (findItr == handler.devices.end())
        return "";

    return findItr->SerialNumber;
}

blt::local_date_time MassHunterDataImpl::getAcquisitionTime(bool adjustToHostTime) const
{
    try
    {
        System::DateTime acquisitionTime = reader_->FileInformation->AcquisitionTime;

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

bool MassHunterDataImpl::hasProfileData() const
{
    return hasProfileData_;
}

bool MassHunterDataImpl::hasIonMobilityData() const { return false; }

int MassHunterDataImpl::getTotalIonMobilityFramesPresent() const
{
    return 0;
}

FramePtr MassHunterDataImpl::getIonMobilityFrame(int frameIndex) const
{
    return FramePtr();
}

const set<Transition>& MassHunterDataImpl::getTransitions() const
{
    return transitions_;
}

const BinaryData<double>& MassHunterDataImpl::getTicTimes(bool ms1Only) const
{
    return ms1Only ? ticTimesMs1_ : ticTimes_;
}

const BinaryData<double>& MassHunterDataImpl::getBpcTimes(bool ms1Only) const
{
    return ms1Only ? bpcTimesMs1_ : bpcTimes_;
}

const BinaryData<float>& MassHunterDataImpl::getTicIntensities(bool ms1Only) const
{
    return ms1Only ? ticIntensitiesMs1_ : ticIntensities_;
}

const BinaryData<float>& MassHunterDataImpl::getBpcIntensities(bool ms1Only) const
{
    return ms1Only ? bpcIntensitiesMs1_ : bpcIntensities_;
}

MassChromatogramPtr MassHunterDataImpl::getChromatogram(const Transition& transition) const
{
    try
    {
        if (!transitionToChromatogramIndexMap_.count(transition))
            throw gcnew System::Exception("[MassHunterData::getChromatogram()] No chromatogram corresponds to the transition.");

        int index = transitionToChromatogramIndexMap_.find(transition)->second;
        // until someone can figure out why storing SIM chromatograms in the constructor
        // causes the unit test to fail, only MRM uses faster way of retrieving chromatograms
        // while SIM continues to use the original, slower method.
        array<MHDAC::IBDAChromData^>^ chromatograms = chromMrm_; // transition.type == Transition::MRM ? chromMrm_ : chromSim_;
        if (transition.type != Transition::MRM)
        {
            MHDAC::IBDAChromFilter^ filter = gcnew MHDAC::BDAChromFilter();
            filter->ChromatogramType = MHDAC::ChromType::SelectedIonMonitoring;
            filter->ExtractOneChromatogramPerScanSegment = true;
            filter->DoCycleSum = false;
            chromatograms = reader_->GetChromatogram(filter);
        }

        return MassChromatogramPtr(new MassChromatogramImpl(chromatograms[index]));
    }
    CATCH_AND_FORWARD
}

const std::vector<Signal>& MassHunterDataImpl::getSignals() const
{
    try
    {
        if (!signals_.empty() || !reader_->FileInformation->IsNonMSDataPresent())
            return signals_;

        auto nonMsDataReader = (MHDAC::INonmsDataReader^) (MHDAC::IMsdrDataReader^) reader_;
        if (nonMsDataReader == nullptr)
            return signals_;
        auto devices = nonMsDataReader->GetNonmsDevices();
        if (devices == nullptr)
            return signals_;

        signalInfoMap_ = gcnew System::Collections::Generic::Dictionary<String^, MHDAC::ISignalInfo^>();
        for each (auto device in devices)
        {
            auto deviceNameAndOrdinal = device->DeviceName + device->OrdinalNumber.ToString();
            auto chromatogramSignalTable = reader_->FileInformation->GetSignalTable(deviceNameAndOrdinal, MHDAC::StoredDataType::Chromatograms);
            auto instrumentCurveSignalTable = reader_->FileInformation->GetSignalTable(deviceNameAndOrdinal, MHDAC::StoredDataType::InstrumentCurves);

            for each (System::Data::DataRow^ chromatogram in chromatogramSignalTable->Rows)
            {
                auto signalName = chromatogram["SignalName"]->ToString();
                auto signalDescription = ToStdString(chromatogram["SignalDescription"]->ToString());
                signals_.emplace_back(Signal{ ToStdString(deviceNameAndOrdinal), ToStdString(signalName), signalDescription, false, (DeviceType) device->DeviceType });
            }
            for each (auto signal in nonMsDataReader->GetSignalInfo(device, MHDAC::StoredDataType::Chromatograms))
                signalInfoMap_->default[deviceNameAndOrdinal + signal->SignalName] = signal;

            for each (System::Data::DataRow^ curve in instrumentCurveSignalTable->Rows)
            {
                auto signalName = curve["SignalName"]->ToString();
                auto signalDescription = ToStdString(curve["SignalDescription"]->ToString());
                signals_.emplace_back(Signal{ ToStdString(deviceNameAndOrdinal), ToStdString(signalName), signalDescription, true, (DeviceType) device->DeviceType });
            }
            for each (auto signal in nonMsDataReader->GetSignalInfo(device, MHDAC::StoredDataType::InstrumentCurves))
                signalInfoMap_->default[deviceNameAndOrdinal + signal->SignalName] = signal;
        }
        return signals_;
    }
    CATCH_AND_FORWARD
}

SignalChromatogramPtr MassHunterDataImpl::getSignal(const Signal& signal) const
{
    try
    {
        auto signalKey = ToSystemString(signal.deviceName + signal.signalName);
        if (!signalInfoMap_->ContainsKey(signalKey))
            throw gcnew System::Exception("[MassHunterData::getSignal()] Unknown signal.");

        auto nonMsDataReader = (MHDAC::INonmsDataReader^) (MHDAC::IMsdrDataReader^) reader_;
        return SignalChromatogramPtr(new SignalChromatogramImpl(nonMsDataReader->GetSignal(signalInfoMap_->default[signalKey])));
    }
    CATCH_AND_FORWARD
}

ScanRecordPtr MassHunterDataImpl::getScanRecord(int rowNumber) const
{
    try {return ScanRecordPtr(new ScanRecordImpl(reader_->GetScanRecord(rowNumber)));} CATCH_AND_FORWARD
}

SpectrumPtr MassHunterDataImpl::getProfileSpectrumByRow(int rowNumber) const
{
    if (!hasProfileData()) return getPeakSpectrumByRow(rowNumber);
    try {return SpectrumPtr(new SpectrumImpl(reader_->GetSpectrum(rowNumber, nullptr, nullptr, MHDAC::DesiredMSStorageType::ProfileElsePeak)));} CATCH_AND_FORWARD
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
    if (!hasProfileData()) return getPeakSpectrumById(scanId);
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


int ScanRecordImpl::getScanId() const
{
    try {return scanRecord_->ScanID;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getRetentionTime() const
{
    try {return scanRecord_->RetentionTime;} CATCH_AND_FORWARD
}

int ScanRecordImpl::getMSLevel() const
{
    try {return scanRecord_->MSLevel == MHDAC::MSLevel::MSMS ? 2 : 1;} CATCH_AND_FORWARD
}

MSScanType ScanRecordImpl::getMSScanType() const
{
    try {return (MSScanType)scanRecord_->MSScanType;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getTic() const
{
    try {return scanRecord_->Tic;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getBasePeakMZ() const
{
    try {return scanRecord_->BasePeakMZ;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getBasePeakIntensity() const
{
    try {return scanRecord_->BasePeakIntensity;} CATCH_AND_FORWARD
}

IonizationMode ScanRecordImpl::getIonizationMode() const
{
    try {return (IonizationMode)scanRecord_->IonizationMode;} CATCH_AND_FORWARD
}

IonPolarity ScanRecordImpl::getIonPolarity() const
{
    try {return (IonPolarity)scanRecord_->IonPolarity;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getMZOfInterest() const
{
    try {return scanRecord_->MZOfInterest;} CATCH_AND_FORWARD
}

int ScanRecordImpl::getTimeSegment() const
{
    try {return scanRecord_->TimeSegment;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getFragmentorVoltage() const
{
    try {return scanRecord_->FragmentorVoltage;} CATCH_AND_FORWARD
}

double ScanRecordImpl::getCollisionEnergy() const
{
    try {return scanRecord_->CollisionEnergy;} CATCH_AND_FORWARD
}

bool ScanRecordImpl::getIsFragmentorVoltageDynamic() const
{
    try {return scanRecord_->IsFragmentorVoltageDynamic;} CATCH_AND_FORWARD
}

bool ScanRecordImpl::getIsCollisionEnergyDynamic() const
{
    try {return scanRecord_->IsCollisionEnergyDynamic;} CATCH_AND_FORWARD
}

bool ScanRecordImpl::getIsIonMobilityScan() const {return false;}


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

void SpectrumImpl::getXArray(pwiz::util::BinaryData<double>& x) const
{
    try {return ToBinaryData(specData_->XArray, x);} CATCH_AND_FORWARD
}

void SpectrumImpl::getYArray(pwiz::util::BinaryData<float>& y) const
{
    try {return ToBinaryData(specData_->YArray, y);} CATCH_AND_FORWARD
}


int ChromatogramImpl::getTotalDataPoints() const
{
    try { return chromData_->TotalDataPoints; } CATCH_AND_FORWARD
}

void ChromatogramImpl::getXArray(BinaryData<double>& x) const
{
    try { return ToBinaryData(chromData_->XArray, x); } CATCH_AND_FORWARD
}

void ChromatogramImpl::getYArray(BinaryData<float>& y) const
{
    try { return ToBinaryData(chromData_->YArray, y); } CATCH_AND_FORWARD
}


double MassChromatogramImpl::getCollisionEnergy() const
{
    try {return chromData_->CollisionEnergy;} CATCH_AND_FORWARD
}

IonPolarity MassChromatogramImpl::getIonPolarity() const
{
    try { return (IonPolarity)chromData_->IonPolarity; } CATCH_AND_FORWARD
}


} // Agilent
} // vendor_api
} // pwiz
