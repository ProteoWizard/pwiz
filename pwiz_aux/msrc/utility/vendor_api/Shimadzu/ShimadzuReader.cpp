//
// $Id$
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
#include "ShimadzuReader.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;

#ifdef __INTELLISENSE__
#using <Shimadzu.LabSolutions.IO.IoModule.dll>
#using <DataReader.dll>
#endif

using System::String;
using System::Math;
using System::Collections::Generic::IList;
namespace ShimadzuAPI = Shimadzu::LabSolutions::DataReader;
namespace ShimadzuIO = Shimadzu::LabSolutions::IO;
namespace ShimadzuGeneric = ShimadzuIO::Generic;
using ShimadzuIO::Data::DataObject;
using ShimadzuIO::Method::MethodObject;
typedef ShimadzuGeneric::Tool ShimadzuUtil;
using ShimadzuAPI::ReaderResult;

namespace pwiz {
namespace vendor_api {
namespace Shimadzu {

class ShimadzuReaderImpl;
   
class MRMChromatogramImpl : public SRMChromatogram
{
    public:
        MRMChromatogramImpl(ShimadzuAPI::MRMChromatogram^ chromatogram, const SRMTransition& transition)
        : transition_(transition),
          chromatogram_(chromatogram)
    {}

    virtual const SRMTransition& getTransition() const { return transition_; }
    virtual int getTotalDataPoints() const { try { return chromatogram_->NumDataPoints; } CATCH_AND_FORWARD }
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const { try { ToBinaryData<double>(chromatogram_->Times, x); } CATCH_AND_FORWARD }
    virtual void getYArray(pwiz::util::BinaryData<double>& y) const { try { ToBinaryData<double>(chromatogram_->Intensities, y); } CATCH_AND_FORWARD }

    private:
    SRMTransition transition_;
    gcroot<ShimadzuAPI::MRMChromatogram^> chromatogram_;
};

class TOFChromatogramImpl : public SRMChromatogram
{
    public:
    TOFChromatogramImpl(ShimadzuIO::Generic::MassChromatogramObject^ chromatogram, const SRMTransition& transition)
        : transition_(transition),
          chromatogram_(chromatogram)
    {}

    virtual const SRMTransition& getTransition() const { return transition_; }
    virtual int getTotalDataPoints() const { try { return (int) chromatogram_->TotalPoints; } CATCH_AND_FORWARD }
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const
    {
        try
        {
            auto timeArray = chromatogram_->RetTimeList;
            x.resize(timeArray->Length);
            for (size_t i = 0; i < x.size(); ++i)
                x[i] = timeArray[i] / 1000.0;
        } CATCH_AND_FORWARD
    }

    virtual void getYArray(pwiz::util::BinaryData<double>& y) const
    {
        try
        {
            auto intensityArray = chromatogram_->ChromIntList;
            y.resize(intensityArray->Length);
            for (size_t i = 0; i < y.size(); ++i)
                y[i] = intensityArray[i];
        } CATCH_AND_FORWARD
    }

    private:
    SRMTransition transition_;
    gcroot<ShimadzuIO::Generic::MassChromatogramObject^> chromatogram_;
};


class TICChromatogramImpl : public Chromatogram
{
    public:
    TICChromatogramImpl(const ShimadzuReaderImpl& reader, DataObject^ dataObject, bool ms1Only);

    virtual int getTotalDataPoints() const { try { return (int) x_.size(); } CATCH_AND_FORWARD }
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const
    {
        x = x_;
    }

    virtual void getYArray(pwiz::util::BinaryData<double>& y) const
    {
        y = y_;
    }

    private:
    pwiz::util::BinaryData<double> x_;
    pwiz::util::BinaryData<double> y_;
    bool ms1Only;
};


class SpectrumImpl : public Spectrum
{
public:
    SpectrumImpl(ShimadzuIO::Generic::MassSpectrumObject^ spectrum, ShimadzuGeneric::Param::MS::MassEventInfo^ eventInfo, const pair<double, int>* precursorInfo)
        : spectrum_(spectrum), eventInfo_(eventInfo), precursorMz_(0), precursorCharge_(0)
    {
        if (precursorInfo != nullptr)
        {
            precursorMz_ = precursorInfo->first;
            precursorCharge_ = precursorInfo->second;
        }
        else
            precursorMz_ = (double) spectrum_->AcqModeMz / ShimadzuUtil::MASSNUMBER_UNIT;
    }

    virtual double getScanTime() const { return spectrum_->RetentionTime; }
    virtual int getMSLevel() const { return spectrum_->MassStep > 1 &&  spectrum_->PrecursorMzList->Count > 0 ? 2 : 1; }
    virtual Polarity getPolarity() const { return (Polarity) (int) spectrum_->Polarity; }

    virtual double getSumY() const { return spectrum_->TotalInt; }
    virtual double getBasePeakX() const { return (double) spectrum_->BPMass / ShimadzuUtil::MASSNUMBER_UNIT; }
    virtual double getBasePeakY() const { return spectrum_->BPInt; }
    virtual double getMinX() const { return ((ShimadzuGeneric::Param::MS::MassEventInfo^) eventInfo_) == nullptr ? 0 : (double) eventInfo_->StartMz / ShimadzuUtil::MASSNUMBER_UNIT; }
    virtual double getMaxX() const { return ((ShimadzuGeneric::Param::MS::MassEventInfo^) eventInfo_) == nullptr ? 0 : (double) eventInfo_->EndMz / ShimadzuUtil::MASSNUMBER_UNIT; }

    virtual bool getHasIsolationInfo() const { return false; }
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const { }

    virtual bool getHasPrecursorInfo() const { return precursorMz_ > 0; }
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const
    {
        if (!getHasPrecursorInfo())
            return;

        selectedMz = precursorMz_;
        intensity = 0;
        charge = precursorCharge_;
    }

    virtual int getTotalDataPoints(bool doCentroid) const { try { return doCentroid ? spectrum_->CentroidList->Count : spectrum_->ProfileList->Count; } CATCH_AND_FORWARD }
    virtual void getProfileArrays(pwiz::util::BinaryData<double>& x, pwiz::util::BinaryData<double>& y) const
    {
        try
        {
            auto profileArray = spectrum_->ProfileList;
            x.resize(profileArray->Count);
            y.resize(x.size());
            for (size_t i = 0; i < x.size(); ++i)
            {
                auto point = profileArray[i];
                x[i] = (double) point->Mass / ShimadzuUtil::MASSNUMBER_UNIT;
                y[i] = point->Intensity;
            }
        } CATCH_AND_FORWARD
    }

    virtual void getCentroidArrays(pwiz::util::BinaryData<double>& x, pwiz::util::BinaryData<double>& y) const
    {
        try
        {
            auto centroidArray = spectrum_->CentroidList;
            x.resize(centroidArray->Count);
            y.resize(x.size());
            for (size_t i = 0; i < x.size(); ++i)
            {
                auto point = centroidArray[i];
                x[i] = (double) point->Mass / ShimadzuUtil::MASSNUMBER_UNIT;
                y[i] = point->Intensity;
            }
        } CATCH_AND_FORWARD
    }

    private:
    gcroot<ShimadzuIO::Generic::MassSpectrumObject^> spectrum_;
    gcroot<ShimadzuGeneric::Param::MS::MassEventInfo^> eventInfo_;
    double precursorMz_;
    int precursorCharge_;
};


class ShimadzuReaderImpl : public ShimadzuReader
{
    public:
    ShimadzuReaderImpl(const string& filepath)
    {
        try
        {
            scanCount_ = 0;

            reader_ = gcnew ShimadzuAPI::MassDataReader();
            String^ systemFilepath = ToSystemString(filepath);

            // first try to open with MRM reader
            ReaderResult result = reader_->OpenDataFile(systemFilepath);

            // if that fails or if the file has no transitions, try to load data with QTOF reader
            if (result != ReaderResult::OK || getTransitions().empty())
            {
                if (result == ReaderResult::OK)
                    reader_->CloseDataFile();

                dataObject_ = gcnew DataObject();
                auto result2 = dataObject_->IO->LoadData(systemFilepath);
                if (ShimadzuUtil::Failed(result2))
                    throw runtime_error("[ShimadzuReader::ctor] LoadData error: " + ToStdString(System::Enum::GetName(result2.GetType(), (System::Object^) result2)));

                /*methodObject_ = gcnew MethodObject();
                result = methodObject_->IO->LoadMethod(systemFilepath);
                if (ShimadzuUtil::Failed(result))
                throw runtime_error("[ShimadzuReader::ctor] LoadMethod error: " + ToStdString(System::Enum::GetName(result.GetType(), (System::Object^) result)));*/

                try
                {
                    auto eventInfo = gcnew System::Collections::Generic::Dictionary<short, ShimadzuGeneric::Param::MS::MassEventInfo^>();
                    auto eventInfoList = gcnew System::Collections::Generic::List<ShimadzuGeneric::Param::MS::MassEventInfo^>();
                    dataObject_->MS->Parameters->GetEventInfo(eventInfoList);
                    for each (auto evt in eventInfoList)
                        eventInfo[evt->Event] = evt;
                    eventInfo_ = eventInfo;
                }
                catch (System::Exception^)
                {
                    // TODO: log warning
                }

                auto chromatogramMng = dataObject_->MS->Chromatogram;

                auto dummySpectrum = gcnew ShimadzuGeneric::MassSpectrumObject();
                try { dataObject_->MS->Spectrum->GetMSSpectrumByScan(dummySpectrum, 1); }
                catch (...) {}

                //int lastScanTime = 0;
                unsigned int lastScanNumber = 0;

                int startTime, endTime;
                dataObject_->MS->Parameters->GetAnalysisTime(startTime, endTime, 0);

                segmentCount_ = chromatogramMng->SegmentCount;
                eventNumbersBySegment_.resize(segmentCount_);
                for (int i = 1; i <= segmentCount_; ++i)
                {
                    auto& eventNumbers = eventNumbersBySegment_[i - 1];
                    int eventCount = chromatogramMng->EventCount(i);
                    eventNumbers.resize(eventCount);
                    for (int j = 1; j <= eventNumbers.size(); ++j)
                    {
                        eventNumbers[j - 1] = chromatogramMng->GetEventNo(i, j);

                        unsigned int eventLastScanNumber;
                        result2 = dataObject_->MS->Spectrum->RetTimeToScan(eventLastScanNumber, endTime, eventNumbers[j - 1]);
                        if (ShimadzuUtil::Failed(result2))
                        {
                            cerr << ("[ShimadzuReader::ctor] RetTimeToScan error for time " + lexical_cast<string>(endTime) + " and event " + lexical_cast<string>(eventNumbers[j - 1]) + ": " +
                                                ToStdString(System::Enum::GetName(result2.GetType(), (System::Object^) result2))) << endl;
                            continue;
                        }

                        if (msLevels_.size() < 2)
                        {
                            auto spectrumPtr = getSpectrum(eventLastScanNumber);
                            msLevels_.insert(spectrumPtr->getMSLevel());
                        }

                        if (eventLastScanNumber > lastScanNumber)
                            lastScanNumber = eventLastScanNumber;
                    }
                }

                scanCount_ = lastScanNumber;

                ShimadzuGeneric::PrecursorResultData^ precursorResultData;
                dataObject_->MS->Spectrum->GetPrecursorList(gcnew ShimadzuGeneric::DdaPrecursorFilter(), precursorResultData);
                if (precursorResultData->SurveyList->Count > 0)
                    for each(auto dependent in precursorResultData->SurveyList[0]->DependentList)
                        for each(int scan in dependent->ScanNoList)
                        {
                            int charge = 0;
                            switch (dependent->Charge)
                            {
                                default: break;
                                case ShimadzuGeneric::Charges::Charge1: charge = 1; break;
                                case ShimadzuGeneric::Charges::Charge2: charge = 2; break;
                                case ShimadzuGeneric::Charges::Charge3: charge = 3; break;
                                case ShimadzuGeneric::Charges::Charge4: charge = 4; break;
                                case ShimadzuGeneric::Charges::Charge5: charge = 5; break;
                                case ShimadzuGeneric::Charges::Charge6: charge = 6; break;
                                case ShimadzuGeneric::Charges::Charge7: charge = 7; break;
                            }
                            precursorInfoByScan_[scan] = make_pair(dependent->PrecursorMass * 0.000000001, charge);
                        }
            }
        }
        CATCH_AND_FORWARD
    }

    virtual ~ShimadzuReaderImpl()
    {
        if ((System::Object^)dataObject_ == nullptr)
            reader_->CloseDataFile();
        else
            dataObject_->IO->Close();
    }

    virtual int getScanCount() const { return scanCount_; }

    //virtual std::string getVersion() const = 0;
    //virtual DeviceType getDeviceType() const = 0;
    //virtual std::string getDeviceName(DeviceType deviceType) const = 0;

    virtual const set<SRMTransition>& getTransitions() const
    {
        if (!transitionSet_.empty())
            return transitionSet_;

        try
        {
            for each (ShimadzuAPI::MrmTransition^ transition in reader_->GetMrmTransition())
            {
                SRMTransition t;
                t.id = transition->Number;
                t.channel = transition->Channel;
                t.event = transition->Event;
                t.segment = transition->Segment;
                t.collisionEnergy = transition->CE; // always non-negative, even if scan polarity is negative
                t.polarity = transition->Polarity;
                t.Q1 = (transition->ParentMz[0] + transition->ParentMz[1]) / 2;
                t.Q3 = (transition->ChildMz[0] + transition->ChildMz[1]) / 2;
                transitionSet_.insert(transitionSet_.end(), t);
                transitions_[t.id] = transition;
            }
            return transitionSet_;
        }
        CATCH_AND_FORWARD
    }

    virtual boost::local_time::local_date_time getAnalysisDate(bool adjustToHostTime) const
    {
        if ((System::Object^)dataObject_ == nullptr)
            return blt::local_date_time(blt::not_a_date_time);

        System::DateTime acquisitionTime = dataObject_->SampleInfo->AnalysisDate.ToUniversalTime();

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
            return blt::local_date_time(pt + tzOffset, blt::time_zone_ptr()); // treat time as if it came from host's time zone; actual time zone may not be provided by DateTime
        }
        else
            return blt::local_date_time(pt, blt::time_zone_ptr());
    }

    virtual SRMChromatogramPtr getSRM(const SRMTransition& transition) const
    {
        try { return SRMChromatogramPtr(new MRMChromatogramImpl(reader_->GetChromatogram(transitions_[transition.id]), transition)); } CATCH_AND_FORWARD
    }

    virtual ChromatogramPtr getTIC(bool ms1Only) const
    {
        try { return ChromatogramPtr(new TICChromatogramImpl(*this, dataObject_, ms1Only)); } CATCH_AND_FORWARD
    }

    virtual SpectrumPtr getSpectrum(int scanNumber) const
    {
        try
        {
            ShimadzuGeneric::MassSpectrumObject^ spectrum;
            auto result = dataObject_->MS->Spectrum->GetMSSpectrumByScan(spectrum, scanNumber);
            if (ShimadzuUtil::Failed(result))
                throw runtime_error("[ShimadzuReader::getSpectrum] GetMSSpectrumByScan: " + ToStdString(System::Enum::GetName(result.GetType(), (System::Object^) result)));

            const pair<double, int>* precursorInfo = nullptr;
            auto findItr = precursorInfoByScan_.find(scanNumber);
            if (findItr != precursorInfoByScan_.end())
                precursorInfo = &findItr->second;

            return SpectrumPtr(new SpectrumImpl(spectrum, getEventInfo(spectrum->EventNo), precursorInfo));
        }
        CATCH_AND_FORWARD
    }

    virtual const set<int>& getMSLevels() const
    {
        return msLevels_;
    }

    private:
    friend class TICChromatogramImpl;
    gcroot<ShimadzuAPI::MassDataReader^> reader_;
    gcroot<DataObject^> dataObject_;
    gcroot<System::Collections::Generic::Dictionary<short, ShimadzuGeneric::Param::MS::MassEventInfo^>^> eventInfo_;
    //gcroot<MethodObject^> methodObject_;
    int segmentCount_;
    int scanCount_;
    set<int> msLevels_;
    vector<vector<int>> eventNumbersBySegment_;
    map<int, pair<double, int>> precursorInfoByScan_;
    mutable map<short, gcroot<ShimadzuAPI::MrmTransition^> > transitions_;
    mutable set<SRMTransition> transitionSet_;

    ShimadzuGeneric::Param::MS::MassEventInfo^ getEventInfo(short eventNo) const
    {
        return ((System::Collections::Generic::Dictionary<short, ShimadzuGeneric::Param::MS::MassEventInfo^>^) eventInfo_) == nullptr ? nullptr : ((System::Collections::Generic::Dictionary<short, ShimadzuGeneric::Param::MS::MassEventInfo^>^) eventInfo_)[eventNo];
    }
};


PWIZ_API_DECL
ShimadzuReaderPtr ShimadzuReader::create(const string& filepath)
{
    try { return ShimadzuReaderPtr(new ShimadzuReaderImpl(filepath)); } CATCH_AND_FORWARD
}


TICChromatogramImpl::TICChromatogramImpl(const ShimadzuReaderImpl& reader, DataObject^ dataObject, bool ms1Only)
{
    auto chromatogramMng = dataObject->MS->Chromatogram;
    auto eventTIC = gcnew ShimadzuGeneric::MassChromatogramObject();
    map<int, int> fullFileTIC;

    for (int i = 1; i <= reader.segmentCount_; ++i)
    {
        auto& eventNumbers = reader.eventNumbersBySegment_[i - 1];
        for (short eventNumber : eventNumbers)
        {
            auto result = chromatogramMng->GetTICChromatogram(eventTIC, i, eventNumber);
            if (ShimadzuUtil::Failed(result))
                throw runtime_error("failed to get TIC chromatogram for segment " + lexical_cast<string>(i) + ", event " + lexical_cast<string>(eventNumber));
            for (int j = 0, end = eventTIC->ChromIntList->Length; j < end; ++j)
            {
                int rt = eventTIC->RetTimeList[j];
                fullFileTIC[rt] += eventTIC->ChromIntList[j];
            }

            // assume only first event of each segment is ms1
            if (ms1Only)
                break;
        }
    }

    x_.reserve(fullFileTIC.size());
    y_.reserve(fullFileTIC.size());
    for (const auto& kvp : fullFileTIC)
    {
        x_.push_back((double) kvp.first / ShimadzuUtil::MASSNUMBER_UNIT);
        y_.push_back((double) kvp.second);
    }
}


#pragma unmanaged
PWIZ_API_DECL
bool SRMTransition::operator< (const SRMTransition& rhs) const
{
    return id < rhs.id;
}


} // Shimadzu
} // vendor_api
} // pwiz

