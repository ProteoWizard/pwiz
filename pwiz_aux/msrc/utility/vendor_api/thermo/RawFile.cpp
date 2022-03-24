//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "RawFile.h"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/xpressive/xpressive.hpp>

using namespace pwiz::vendor_api::Thermo;
using namespace pwiz::util;

#include <gcroot.h>
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"

#ifndef _WIN64
#include "RawFileCOM.h"
#include "RawFileValues.h"
#include "ScanFilter.h"
using namespace XRawfile;

// note:
// XRawfile seems to return 0 for success, >0 for failure;
// COM standard: HRESULT is >=0 for success, <0 for failure
#define checkResultException(stmt, exc) \
{ \
    HRESULT hr = (stmt); \
    if (hr != 0) \
        throw (exc)(_com_error(hr).ErrorMessage()); \
}
#define checkResult(stmt) checkResultException(stmt, RawEgg)

#else // is WIN64
using namespace ThermoFisher::CommonCore::RawFileReader;
using namespace ThermoFisher::CommonCore::Data::Interfaces;
namespace ThermoEnum = ThermoFisher::CommonCore::Data::FilterEnums;
namespace Thermo = ThermoFisher::CommonCore::Data::Business;

#include <msclr/auto_gcroot.h>
#endif // WIN64


#ifdef _WIN64
const char* pwiz::vendor_api::Thermo::ControllerTypeStrings[] = { "MS", "Analog", "A/D Card", "UV", "PDA", "Other" };
#else
const char* pwiz::vendor_api::Thermo::ControllerTypeStrings[] = { "MS", "Analog", "A/D Card", "PDA", "UV", "Other" };
#endif


class RawFileImpl : public RawFile
{
    public:

    RawFileImpl(const string& filename);
    ~RawFileImpl();

    virtual RawFile* getRawByThread(size_t currentThreadId) const;

#ifndef _WIN64
    virtual string name(ValueID_Long id) const;
    virtual string name(ValueID_Double id) const;
    virtual string name(ValueID_String id) const;
    virtual long value(ValueID_Long id) const;
    virtual double value(ValueID_Double id) const;
    virtual string value(ValueID_String id) const;
#endif

    virtual std::string getFilename() const {return filename_;}

    virtual blt::local_date_time getCreationDate(bool adjustToHostTime = true) const;
    virtual ControllerInfo getCurrentController() const;
    virtual void setCurrentController(ControllerType type, long controllerNumber);
    virtual long getNumberOfControllersOfType(ControllerType type) const;
    virtual ControllerType getControllerType(long index) const;

    virtual long scanNumber(double rt) const;
    virtual double rt(long scanNumber) const;

    virtual long getFirstScanNumber() const;
    virtual long getLastScanNumber() const;
    virtual double getFirstScanTime() const;
    virtual double getLastScanTime() const;

    virtual MassListPtr
    getMassList(long scanNumber,
                const string& filter,
                CutoffType cutoffType,
                long cutoffValue,
                long maxPeakCount,
                bool centroidResult) const;

    virtual std::vector<std::string> getFilters() const;
    virtual ScanInfoPtr getScanInfo(long scanNumber) const;

    virtual MSOrder getMSOrder(long scanNumber) const;
    virtual double getPrecursorMass(long scanNumber, MSOrder msOrder) const;
    virtual ScanType getScanType(long scanNumber) const;
    virtual ScanFilterMassAnalyzerType getMassAnalyzerType(long scanNumber) const;
    virtual ActivationType getActivationType(long scanNumber) const;
    virtual double getIsolationWidth(int scanSegment, int scanEvent) const;
    virtual double getDefaultIsolationWidth(int scanSegment, int msLevel) const;
    virtual double calculateIsolationMzWithOffset(long scanNumber, double isolationMzPossiblyWithOffset) const;

    virtual ErrorLogItem getErrorLogItem(long itemNumber) const;
    virtual std::vector<std::string> getInstrumentMethods() const;
    virtual std::string getInstrumentChannelLabel(long channel) const;

#ifndef _WIN64
    virtual auto_ptr<LabelValueArray> getTuneData(long segmentNumber) const;
#endif

    virtual InstrumentModelType getInstrumentModel() const;
    virtual InstrumentData getInstrumentData() const;
    virtual const vector<IonizationType>& getIonSources() const;
    virtual const vector<MassAnalyzerType>& getMassAnalyzers() const;
    virtual const vector<DetectorType>& getDetectors() const;

    virtual std::string getSampleID() const;
    virtual std::string getTrailerExtraValue(long scanNumber, const std::string& name, string valueIfMissing = "") const;
    virtual double getTrailerExtraValueDouble(long scanNumber, const std::string& name, double valueIfMissing = 0) const;
    virtual long getTrailerExtraValueLong(long scanNumber, const std::string& name, long valueIfMissing = 0) const;

    virtual ChromatogramDataPtr
    getChromatogramData(ChromatogramType traceType,
                        const string& filter,
                        double massRangeFrom, double massRangeTo,
                        double delay,
                        double startTime,
                        double endTime) const;

    private:
    friend class ScanInfoImpl;
    friend class RawFileThreadImpl;

#ifdef _WIN64
    msclr::auto_gcroot<IRawDataPlus^> raw_;
    msclr::auto_gcroot<IRawFileThreadManager^> rawManager_;

    mutable map<size_t, shared_ptr<RawFileThreadImpl>> rawByThread_;
#else // is WIN32
    IXRawfile5Ptr raw_;
    int rawInterfaceVersion_; // IXRawfile=1, IXRawfile2=2, IXRawfile3=3, etc.
#endif // WIN32

    string filename_;
    bool isTemporary_;

#ifndef _WIN64
    ControllerType currentControllerType_;
    long currentControllerNumber_;
#endif

    mutable InstrumentModelType instrumentModel_;
    mutable vector<IonizationType> ionSources_;
    mutable vector<MassAnalyzerType> massAnalyzers_;
    mutable vector<DetectorType> detectors_;

    map<string, int> trailerExtraIndexByName;
    mutable map<int, map<int, double> > defaultIsolationWidthBySegmentAndMsLevel;
    mutable map<int, map<int, double> > isolationWidthBySegmentAndScanEvent;

    struct IsolationMzOffset
    {
        double offset;
        bool reportedMassIsOffset;
    };

    map<string, IsolationMzOffset> isolationMzOffsetByScanDescription;

    void parseInstrumentMethod();
};


#ifdef _WIN64
class RawFileThreadImpl : public RawFile
{
    public:

    RawFileThreadImpl(const RawFileImpl* raw);
    ~RawFileThreadImpl() {}

    virtual RawFile* getRawByThread(size_t currentThreadId) const { return const_cast<RawFile*>(reinterpret_cast<const RawFile*>(this)); }

    virtual std::string getFilename() const { return rawFile_->filename_; }

    virtual blt::local_date_time getCreationDate(bool adjustToHostTime = true) const;
    virtual ControllerInfo getCurrentController() const;
    virtual void setCurrentController(ControllerType type, long controllerNumber);
    virtual long getNumberOfControllersOfType(ControllerType type) const;
    virtual ControllerType getControllerType(long index) const;

    virtual long scanNumber(double rt) const;
    virtual double rt(long scanNumber) const;

    virtual long getFirstScanNumber() const;
    virtual long getLastScanNumber() const;
    virtual double getFirstScanTime() const;
    virtual double getLastScanTime() const;

    virtual MassListPtr
        getMassList(long scanNumber,
            const string& filter,
            CutoffType cutoffType,
            long cutoffValue,
            long maxPeakCount,
            bool centroidResult) const;

    virtual std::vector<std::string> getFilters() const;
    virtual ScanInfoPtr getScanInfo(long scanNumber) const;

    virtual MSOrder getMSOrder(long scanNumber) const;
    virtual double getPrecursorMass(long scanNumber, MSOrder msOrder) const;
    virtual ScanType getScanType(long scanNumber) const;
    virtual ScanFilterMassAnalyzerType getMassAnalyzerType(long scanNumber) const;
    virtual ActivationType getActivationType(long scanNumber) const;
    virtual double getIsolationWidth(int scanSegment, int scanEvent) const;
    virtual double getDefaultIsolationWidth(int scanSegment, int msLevel) const;
    virtual double calculateIsolationMzWithOffset(long scanNumber, double isolationMzPossiblyOffset) const;

    virtual ErrorLogItem getErrorLogItem(long itemNumber) const;
    virtual std::vector<std::string> getInstrumentMethods() const;
    virtual std::string getInstrumentChannelLabel(long channel) const;

    virtual InstrumentModelType getInstrumentModel() const;
    virtual InstrumentData getInstrumentData() const;
    virtual const vector<IonizationType>& getIonSources() const;
    virtual const vector<MassAnalyzerType>& getMassAnalyzers() const;
    virtual const vector<DetectorType>& getDetectors() const;

    virtual std::string getSampleID() const;
    virtual std::string getTrailerExtraValue(long scanNumber, const std::string& name, std::string valueIfMissing = "") const;
    virtual double getTrailerExtraValueDouble(long scanNumber, const std::string& name, double valueIfMissing = 0) const;
    virtual long getTrailerExtraValueLong(long scanNumber, const std::string& name, long valueIfMissing = 0) const;

    virtual ChromatogramDataPtr
        getChromatogramData(ChromatogramType traceType,
            const string& filter,
            double massRangeFrom, double massRangeTo,
            double delay,
            double startTime,
            double endTime) const;

    private:
    friend class ScanInfoImpl;

    const RawFileImpl* rawFile_;
    msclr::auto_gcroot<IRawDataPlus^> raw_;

    ControllerType currentControllerType_;
    long currentControllerNumber_;
};
#endif // WIN64


RawFileImpl::RawFileImpl(const string& filename)
:   filename_(filename),
    isTemporary_(false),
    instrumentModel_(InstrumentModelType_Unknown)
{
    try
    {        
        // if file is on a network drive, copy it to a temporary local file
        /*if (::PathIsNetworkPath(filename.c_str()))
        {
            char* temp = ::getenv("TEMP");
            bfs::path tempFilepath = bfs::path(temp) / bfs::path(filename).filename();
            if (bfs::exists(tempFilepath))
                bfs::remove(tempFilepath);
            bfs::copy_file(filename, tempFilepath);
            filename_ = tempFilepath.string();
            isTemporary_ = true;
        }*/

#ifndef _WIN64
        IXRawfile5Ptr raw5(NULL);

        if (FAILED(raw5.CreateInstance("MSFileReader.XRawfile.1")))
        {
            rawInterfaceVersion_ = 0;
            throw RawEgg("[RawFile::ctor] Unable to initialize XRawfile; was ProteoWizard built with MSFileReader installed? Or an older version of MSFileReader is registered instead of the bundled ProteoWizard version.");
        }
        else
        {
            raw_ = raw5;
            rawInterfaceVersion_ = 5;
        }

        if (raw_->Open(bfs::path(filename_).native().c_str()))
            throw RawEgg("[RawFile::ctor] Unable to open file " + filename);

        if (getNumberOfControllersOfType(Controller_MS) == 0)
            return; // none of the following metadata stuff works for non-MS controllers as far as I can tell

        currentControllerType_ = Controller_None;
        currentControllerNumber_ = -1;
        setCurrentController(Controller_MS, 1);

#else // is WIN64
        auto managedFilename = ToSystemString(filename);
        rawManager_ = RawFileReaderAdapter::ThreadedFileFactory(managedFilename);
        raw_ = rawManager_->CreateThreadAccessor();
        //raw_ = RawFileReaderAdapter::FileFactory(managedFilename);
        raw_->IncludeReferenceAndExceptionData = true;

        // CONSIDER: throwing C++ exceptions in managed code may cause Wine to crash?
        if (raw_->IsError || raw_->InAcquisition)
            throw gcnew System::Exception("Corrupt RAW file " + managedFilename);

        if (getNumberOfControllersOfType(Controller_MS) == 0)
            return; // none of the following metadata stuff works for non-MS controllers as far as I can tell

        setCurrentController(Controller_MS, 1);

        auto trailerExtraInfo = raw_->GetTrailerExtraHeaderInformation();
        for (int i = 0; i < trailerExtraInfo->Length; ++i)
        {
            auto label = ToStdString(trailerExtraInfo[i]->Label);
            trailerExtraIndexByName[label] = i;
        }

#endif // WIN64
        parseInstrumentMethod();

        // initialize global metadata when opening file to avoid needing to synchronize threads when this information is first accessed
        InstrumentData instData = getInstrumentData();
        string modelString = instData.Model;
        string nameString = instData.Name;
        if (modelString == "LTQ Velos" || modelString == "LTQ") // HACK: disambiguate LTQ Velos/Orbitrap Velos, and LTQ/LTQ-FT
        {
            modelString = nameString;
        }
        else if (modelString == "LTQ Orbitrap" &&
            nameString.empty()) // HACK: disambiguate LTQ Orbitrap and some broken Exactive files
        {
#ifndef _WIN64

            auto_ptr<LabelValueArray> lvArray = getTuneData(0);
            for (int i = 0; i < lvArray->size(); ++i)
                if (lvArray->label(i) == "Model")
                {
                    modelString = lvArray->value(i);
                    break;
                }
#else
            // Exactive has instrument info at the end of the tune method
            auto logEntry = raw_->GetTuneData(0);
            for (int i = 0; i < logEntry->Length; ++i)
                if (logEntry->Labels[i] == "Model")
                {
                    modelString = ToStdString(logEntry->Values[i]);
                    break;
                }
#endif
        }

        instrumentModel_ = parseInstrumentModelType(modelString);
        if (instrumentModel_ == InstrumentModelType_Unknown)
            instrumentModel_ = parseInstrumentModelType(nameString);

        detectors_ = getDetectorsForInstrumentModel(getInstrumentModel());
        massAnalyzers_ = getMassAnalyzersForInstrumentModel(getInstrumentModel());
        ionSources_ = getIonSourcesForInstrumentModel(getInstrumentModel());
    }
    CATCH_AND_FORWARD
}


RawFileImpl::~RawFileImpl()
{
#ifndef _WIN64
    raw_->Close();
    raw_ = NULL;
#else
    raw_.reset();
    rawManager_.reset();
    System::GC::Collect();
#endif
    // if applicable, delete temporary file
    if (isTemporary_)
        bfs::remove(filename_);
}

#ifndef _WIN64
string RawFileImpl::name(ValueID_Long id) const
{
    return RawFileValues::descriptor(id)->name;
}


string RawFileImpl::name(ValueID_Double id) const
{
    return RawFileValues::descriptor(id)->name;
}


string RawFileImpl::name(ValueID_String id) const
{
    return RawFileValues::descriptor(id)->name;
}


long RawFileImpl::value(ValueID_Long id) const
{
    long result = 0;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(&result);
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return result;
}


double RawFileImpl::value(ValueID_Double id) const
{
    double result = 0;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(&result);
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return result;
}


string RawFileImpl::value(ValueID_String id) const
{
    _bstr_t bstr;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(bstr.GetAddress());
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return (const char*)(bstr);
}
#endif

long RawFileImpl::getFirstScanNumber() const
{
#ifdef _WIN64
    return raw_->RunHeader->FirstSpectrum;
#else
    return value(FirstSpectrumNumber);
#endif
}

long RawFileImpl::getLastScanNumber() const
{
#ifdef _WIN64
    return raw_->RunHeader->LastSpectrum;
#else
    return value(LastSpectrumNumber);
#endif
}

double RawFileImpl::getFirstScanTime() const
{
#ifdef _WIN64
    return raw_->RunHeader->StartTime;
#else
    return rt(value(FirstSpectrumNumber));
#endif
}

double RawFileImpl::getLastScanTime() const
{
#ifdef _WIN64
    return raw_->RunHeader->EndTime;
#else
    return rt(value(LastSpectrumNumber));
#endif
}


blt::local_date_time RawFileImpl::getCreationDate(bool adjustToHostTime) const
{
    try
    {
#ifndef _WIN64
        DATE oadate;
        checkResult(raw_->GetCreationDate(&oadate));
        bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(oadate));
#else
        System::DateTime acquisitionTime = raw_->FileHeader->CreationDate;
        bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
            bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));
#endif

        if (adjustToHostTime)
        {
            bpt::time_duration tzOffset = bpt::second_clock::universal_time() - bpt::second_clock::local_time();
            return blt::local_date_time(pt + tzOffset, blt::time_zone_ptr()); // treat time as if it came from host's time zone; actual time zone is not provided by Thermo
        }
        else
            return blt::local_date_time(pt, blt::time_zone_ptr());
    }
    CATCH_AND_FORWARD
}

/*namespace {
class VectorLabelValueArray : public LabelValueArray
{
    public:

    virtual int size() const {return (int)labels_.size();}
    virtual string label(int index) const {checkIndex(index); return labels_[index];}
    virtual string value(int index) const {checkIndex(index); return values_[index];}

    void push_back(const string& label, const string& value)
    {
        labels_.push_back(label);
        values_.push_back(value);
    }

    private:
    vector<string> labels_;
    vector<string> values_;

    void checkIndex(int i) const
    {
        if (i<0 || i>=(int)labels_.size())
            throw RawEgg("VectorLabelValueArray: Array out of bounds.");
    }
};
} // namespace

auto_ptr<LabelValueArray> RawFileImpl::getSequenceRowUserInfo()
{
    auto_ptr<VectorLabelValueArray> info(new VectorLabelValueArray);

    for (int i=0; i<5; i++)
    {
        _bstr_t bstrLabel;
        _bstr_t bstrValue;
        checkResult(raw_->GetSeqRowUserLabel(i, bstrLabel.GetAddress()), "[RawFileImpl::getSequenceRowUserInfo(), GetSeqRowUserLabel()] ");
        checkResult(raw_->GetSeqRowUserText(i, bstrValue.GetAddress()), "[RawFileImpl::getSequenceRowUserInfo(), GetSeqRowUserText()] ");
        info->push_back((const char*)(bstrLabel), (const char*)(bstrValue));
    }

    return info;
}*/


ControllerInfo RawFileImpl::getCurrentController() const
{
#ifndef _WIN64
    ControllerInfo result;
    result.type = currentControllerType_;
    result.controllerNumber = currentControllerNumber_;
    return result;
#else
    return getRawByThread(0)->getCurrentController();
#endif
}


void RawFileImpl::setCurrentController(ControllerType type, long controllerNumber)
{
#ifndef _WIN64
    if (currentControllerType_ == type && currentControllerNumber_ == controllerNumber)
        return;

    try
    {
        checkResult(raw_->SetCurrentController(type, controllerNumber));
        currentControllerType_ = type;
        currentControllerNumber_ = controllerNumber;
    }
    CATCH_AND_FORWARD
#else
    raw_->SelectInstrument((Thermo::Device) type, controllerNumber);
    getRawByThread(0)->setCurrentController(type, controllerNumber);
#endif
}


long RawFileImpl::getNumberOfControllersOfType(ControllerType type) const
{
    try
    {
#ifndef _WIN64
        long result = 0;
        checkResult(raw_->GetNumberOfControllersOfType(type, &result));
        return result;
#else
        return raw_->GetInstrumentCountOfType((Thermo::Device) type);
#endif
    }
    CATCH_AND_FORWARD
}


ControllerType RawFileImpl::getControllerType(long index) const
{
    try
    {
#ifndef _WIN64
        long result = 0;
        checkResult(raw_->GetControllerType(index, &result));
        return ControllerType(result);
#else
        return (ControllerType) raw_->GetInstrumentType(index);
#endif
    }
    CATCH_AND_FORWARD
}


long RawFileImpl::scanNumber(double rt) const
{
    try
    {
#ifndef _WIN64
        long result = 0;
        checkResult(raw_->ScanNumFromRT(rt, &result));
        return result;
#else
        return raw_->ScanNumberFromRetentionTime(rt);
#endif
    }
    CATCH_AND_FORWARD
}


double RawFileImpl::rt(long scanNumber) const
{
    try
    {
#ifndef _WIN64
        double result = 0;
        checkResult(raw_->RTFromScanNum(scanNumber, &result));
        return result;
#else
        return raw_->RetentionTimeFromScanNumber(scanNumber);
#endif
    }
    CATCH_AND_FORWARD
}


InstrumentModelType RawFileImpl::getInstrumentModel() const
{
    return instrumentModel_;
}

InstrumentData RawFileImpl::getInstrumentData() const
{
    try
    {
        InstrumentData result;
#ifdef _WIN64
        auto source = raw_->GetInstrumentData();
        result.Model = ToStdString(source->Model);
        result.Name = ToStdString(source->Name);
        result.SerialNumber = ToStdString(source->SerialNumber);
        result.SoftwareVersion = ToStdString(source->SoftwareVersion);
        result.HardwareVersion = ToStdString(source->HardwareVersion);
        result.Units = ToStdString(System::Enum::GetName(Thermo::DataUnits::typeid, source->Units));
        ToStdVector(source->ChannelLabels, result.ChannelLabels);
        result.Flags = ToStdString(source->Flags);
        result.AxisLabelX = ToStdString(source->AxisLabelX);
        result.AxisLabelY = ToStdString(source->AxisLabelY);
#else
        result.Model = value(InstModel);
        result.Name = value(InstName);
        result.SerialNumber = value(InstSerialNumber);
        result.SoftwareVersion = value(InstSoftwareVersion);
        result.HardwareVersion = value(InstHardwareVersion);
        result.Flags = value(InstFlags);
#endif
        return result;
    }
    CATCH_AND_FORWARD
}


const vector<IonizationType>& RawFileImpl::getIonSources() const
{
    return ionSources_;
}


const vector<MassAnalyzerType>& RawFileImpl::getMassAnalyzers() const
{
    return massAnalyzers_;
}


const vector<DetectorType>& RawFileImpl::getDetectors() const
{
    return detectors_;
}


std::string RawFileImpl::getSampleID() const
{
#ifndef _WIN64
    return value(SeqRowSampleID);
#else
    return ToStdString(raw_->SampleInformation->SampleId);
#endif
}


std::string RawFileImpl::getTrailerExtraValue(long scanNumber, const string& name, string valueIfMissing) const
{
#ifndef _WIN64
    try
    {
        _variant_t v;

        try
        {
            checkResultException(raw_->GetTrailerExtraValueForScanNum(scanNumber, name.c_str(), &v), invalid_argument);
        }
        catch (invalid_argument&)
        {
            return valueIfMissing;
        }

        switch (v.vt)
        {
            case VT_I1: return lexical_cast<string>(v.cVal);
            case VT_UI1: return lexical_cast<string>(v.bVal);
            case VT_I2: return lexical_cast<string>(v.iVal);
            case VT_UI2: return lexical_cast<string>(v.uiVal);
            case VT_I4: return lexical_cast<string>(v.lVal);
            case VT_UI4: return lexical_cast<string>(v.ulVal);
            case VT_INT: return lexical_cast<string>(v.intVal);
            case VT_UINT: return lexical_cast<string>(v.uintVal);
            case VT_R4: return lexical_cast<string>(v.fltVal);
            case VT_R8: return lexical_cast<string>(v.dblVal);
            case VT_BSTR: return lexical_cast<string>((const char*) _bstr_t(v.bstrVal));
            default:
                throw RawEgg("[RawFileImpl::getTrailerExtraValue()] Unknown type.");
        }
    }
    CATCH_AND_FORWARD_EX(name)
#else
    return getRawByThread(0)->getTrailerExtraValue(scanNumber, name);
#endif
}

double RawFileImpl::getTrailerExtraValueDouble(long scanNumber, const string& name, double valueIfMissing) const
{
#ifndef _WIN64
    try
    {
        _variant_t v;

        try
        {
            checkResultException(raw_->GetTrailerExtraValueForScanNum(scanNumber, name.c_str(), &v), invalid_argument);
        }
        catch (invalid_argument&)
        {
            return valueIfMissing;
        }

        switch (v.vt)
        {
            case VT_R4: return v.fltVal;
            case VT_R8: return v.dblVal;
            default:
                throw RawEgg("[RawFileImpl::getTrailerExtraValueDouble()] Unknown type.");
        }
    }
    CATCH_AND_FORWARD_EX(name)
#else
    return getRawByThread(0)->getTrailerExtraValueDouble(scanNumber, name);
#endif
}


long RawFileImpl::getTrailerExtraValueLong(long scanNumber, const string& name, long valueIfMissing) const
{
#ifndef _WIN64
    try
    {
        _variant_t v;

        try
        {
            checkResultException(raw_->GetTrailerExtraValueForScanNum(scanNumber, name.c_str(), &v), invalid_argument);
        }
        catch (invalid_argument&)
        {
            return valueIfMissing;
        }

        switch (v.vt)
        {
            case VT_I1: return v.cVal;
            case VT_UI1: return v.bVal;
            case VT_I2: return v.iVal;
            case VT_UI2: return v.uiVal;
            case VT_I4: return v.lVal;
            case VT_UI4: return v.ulVal;
            case VT_INT: return v.intVal;
            case VT_UINT: return v.uintVal;
            default:
                throw RawEgg("[RawFileImpl::getTrailerExtraValueLong()] Unknown type.");
        }
    }
    CATCH_AND_FORWARD_EX(name)
#else
    return getRawByThread(0)->getTrailerExtraValueLong(scanNumber, name);
#endif
}

#ifndef _WIN64
struct PWIZ_API_DECL MassIntensityPair
{
    double mass;
    double intensity;
};

namespace{
class MassListImpl
{
    public:

    MassListImpl(VARIANT& v, long size)
    :   msa_(v, size)
    {
        if (v.vt != (VT_ARRAY | VT_R8))
            throw RawEgg("MassListImpl(): VARIANT error.");
    }

    virtual long size() const {return msa_.size();}
    virtual MassIntensityPair* data() const {return (MassIntensityPair*)msa_.data();}

    private:
    ManagedSafeArray msa_;
};

/*class MassListFromLabelDataImpl : public MassList
{
    public:
    MassListFromLabelDataImpl(long scanNumber, VARIANT& labels)
    :   scanNumber_(scanNumber)
    {
        if (labels.vt != (VT_ARRAY | VT_R8))
            throw RawEgg("MassListFromLabelDataImpl(): VARIANT error.");

        _variant_t labels2(labels, false);
        size_ = (long) labels2.parray->rgsabound[0].cElements;
        data_ = new MassIntensityPair[size_];

        double* pdval = (double*) labels2.parray->pvData;
        for(long i=0; i < size_; ++i)
        {
            data_[i].mass = (double) pdval[(i*6)+0];
            data_[i].intensity = (double) pdval[(i*6)+1];
        }
        // labels is freed when labels2 goes out of scope
    }

    ~MassListFromLabelDataImpl()
    {
        delete data_;
    }

    virtual long scanNumber() const {return scanNumber_;}
    virtual long size() const {return size_;}
    virtual MassIntensityPair* data() const {return data_;}

    virtual double centroidPeakWidth() const {return 0;}
    virtual long firstAvgScanNumber() const {return 0;}
    virtual long lastAvgScanNumber() const {return 0;}
    virtual long firstBkg1ScanNumber() const {return 0;}
    virtual long lastBkg1ScanNumber() const {return 0;}
    virtual long firstBkg2ScanNumber() const {return 0;}
    virtual long lastBkg2ScanNumber() const {return 0;}

    private:
    long scanNumber_;
    MassIntensityPair* data_;
    long size_;
};*/
} // namespace
#endif

MassListPtr RawFileImpl::getMassList(long scanNumber,
                                     const string& filter,
                                     CutoffType cutoffType,
                                     long cutoffValue,
                                     long maxPeakCount,
                                     bool centroidResult) const
{
    try
    {
        auto result = boost::make_shared<MassList>();

#ifndef _WIN64
        long size = 0;
        if (centroidResult && getMassAnalyzerType(scanNumber) == ScanFilterMassAnalyzerType_FTMS)
        {
            _variant_t varLabels;
            _variant_t varFlags;
            raw_->GetLabelData(&varLabels, &varFlags, &scanNumber);

            _variant_t labels2(varLabels, false);
            size = (long)labels2.parray->rgsabound[0].cElements;
            result->mzArray.resize(size);
            result->intensityArray.resize(size);
            double* pdval = (double*)labels2.parray->pvData;
            for (long i = 0; i < size; ++i)
            {
                result->mzArray[i] = (double)pdval[(i * 6) + 0];
                result->intensityArray[i] = (double)pdval[(i * 6) + 1];
            }
        }
        else
        {
            double centroidPeakWidth = 0;
            _variant_t variantMassList;
            _variant_t variantPeakFlags;
            checkResult(raw_->GetMassListFromScanNum(&scanNumber,
                                                     _bstr_t(filter.c_str()),
                                                     cutoffType,
                                                     cutoffValue,
                                                     maxPeakCount,
                                                     centroidResult,
                                                     &centroidPeakWidth,
                                                     &variantMassList,
                                                     &variantPeakFlags,
                                                     &size));

            MassListImpl ml(variantMassList, size);
            MassIntensityPair* mzIntPairs = ml.data();
            result->mzArray.resize(size);
            result->intensityArray.resize(size);
            for (size_t i = 0; i < size; ++i, ++mzIntPairs)
            {
                result->mzArray[i] = mzIntPairs->mass;
                result->intensityArray[i] = mzIntPairs->intensity;
            }
        }
#else
        if (centroidResult && raw_->GetFilterForScanNumber(scanNumber)->MassAnalyzer == ThermoEnum::MassAnalyzerType::MassAnalyzerFTMS)
        {
            auto centroidStream = raw_->GetCentroidStream(scanNumber, true);
            if (centroidStream != nullptr && centroidStream->Length > 0)
            {
                ToBinaryData(centroidStream->Masses, result->mzArray);
                ToBinaryData(centroidStream->Intensities, result->intensityArray);
                return result;
            }
        }

        if (centroidResult)
        {
            auto scan = Thermo::Scan::FromFile(raw_.get(), scanNumber);
            if (scan->SegmentedScanAccess->Positions->Length == 0 || scan->ScanStatistics->BasePeakIntensity == 0)
                return result;
            auto centroidScan = Thermo::Scan::ToCentroid(scan);
            if (centroidScan == nullptr || centroidScan->SegmentedScanAccess->Positions->Length == 0)
                throw gcnew System::Exception("failed to centroid scan");
            ToBinaryData(centroidScan->SegmentedScanAccess->Positions, result->mzArray);
            ToBinaryData(centroidScan->SegmentedScanAccess->Intensities, result->intensityArray);
        }
        else
        {
            auto segmentedStream = raw_->GetSegmentedScanFromScanNumber(scanNumber, nullptr);
            ToBinaryData(segmentedStream->Positions, result->mzArray);
            ToBinaryData(segmentedStream->Intensities, result->intensityArray);
        }
#endif
        return result;
    }
    CATCH_AND_FORWARD
}

#ifndef _WIN64
/*MassListPtr
RawFileImpl::getAverageMassList(long firstAvgScanNumber, long lastAvgScanNumber,
                                long firstBkg1ScanNumber, long lastBkg1ScanNumber,
                                long firstBkg2ScanNumber, long lastBkg2ScanNumber,
                                const string& filter,
                                CutoffType cutoffType,
                                long cutoffValue,
                                long maxPeakCount,
                                bool centroidResult)
{
    _bstr_t bstrFilter(filter.c_str());
    double centroidPeakWidth = 0;
    _variant_t variantMassList;
    _variant_t variantPeakFlags;
    long size = 0;

    checkResult(raw_->GetAverageMassList(&firstAvgScanNumber, &lastAvgScanNumber,
                                         &firstBkg1ScanNumber, &lastBkg1ScanNumber,
                                         &firstBkg2ScanNumber, &lastBkg2ScanNumber,
                                         bstrFilter,
                                         cutoffType,
                                         cutoffValue,
                                         maxPeakCount,
                                         centroidResult,
                                         &centroidPeakWidth,
                                         &variantMassList,
                                         &variantPeakFlags,
                                         &size),
                                         "[RawFileImpl::getMassList(), GetAverageMassList()] ");

    return MassListPtr(new MassListImpl(0,
                                        variantMassList,
                                        size,
                                        centroidPeakWidth,
                                        firstAvgScanNumber, lastAvgScanNumber,
                                        firstBkg1ScanNumber, lastBkg1ScanNumber,
                                        firstBkg2ScanNumber, lastBkg2ScanNumber));
}


MassListPtr RawFileImpl::getMassListFromLabelData(long scanNumber)
{
    if (rawInterfaceVersion_ < 2)
        throw RawEgg("[RawFileImpl::getMassListFromLabelData()] GetLabelData requires the IXRawfile2 interface.");

    IXRawfile2Ptr raw2 = (IXRawfile2Ptr) raw_;
	_variant_t varLabels;
    _variant_t varFlags;
	raw2->GetLabelData(&varLabels, &varFlags, &scanNumber);
    return MassListPtr(new MassListFromLabelDataImpl(scanNumber, varLabels));
}*/
#endif

std::vector<string> RawFileImpl::getFilters() const
{
    try
    {
        vector<string> result;
#ifndef _WIN64
        _variant_t v;
        long size = 0;
        checkResult(raw_->GetFilters(&v, &size));
        VariantStringArray vsa(v, size);
        result.reserve(size);
        for (size_t i = 0; i < size; ++i)
            result.emplace_back(vsa.item(i));
#else
        ToStdVector(raw_->GetAutoFilters(), result);
#endif
        return result;
    }
    CATCH_AND_FORWARD
}



class ScanInfoImpl : public ScanInfo
{
    public:

#ifndef _WIN64
    ScanInfoImpl(long scanNumber, const RawFileImpl* raw);
#else
    ScanInfoImpl(long scanNumber, const RawFileThreadImpl* raw);
#endif
    ScanInfoImpl(const std::string& filter);

    void reinitialize(const std::string& filter);

    virtual long scanNumber() const {return scanNumber_;}
    virtual int scanSegmentNumber() const { return scanSegment_; }
    virtual int scanEventNumber() const { return scanEvent_; }

#ifndef _WIN64
    virtual std::string filter() const {return filter_;}
#else
    virtual std::string filter() const { return ToStdString(filter_->ToString()); }
#endif

    virtual MassAnalyzerType massAnalyzerType() const {return massAnalyzerType_;}
    virtual IonizationType ionizationType() const {return ionizationType_;}
    virtual ActivationType activationType() const {return activationType_;}
    virtual long msLevel() const {return msLevel_;}
    virtual ScanType scanType() const {return scanType_;}
    virtual PolarityType polarityType() const {return polarityType_;}
    virtual bool isEnhanced() const {return isEnhanced_;}
    virtual bool isDependent() const {return isDependent_;}
    virtual bool hasMultiplePrecursors() const {return hasMultiplePrecursors_; }
    virtual bool isSPS() const { return isSPS_; }
    virtual bool hasLockMass() const { return hasLockMass_; }
    virtual bool isWideband() const { return isWideband_; }
    virtual bool isTurboScan() const { return isTurboScan_; }
    virtual bool isPhotoIonization() const { return isPhotoIonization_; }
    virtual bool isCorona() const { return isCorona_; }
    virtual bool isDetectorSet() const { return isDetectorSet_; }
    virtual bool isSourceCID() const { return isSourceCID_; }
    virtual AccurateMassType accurateMassType() const { return accurateMassType_; }

    virtual const std::vector<PrecursorInfo>& precursorInfo() const;
    virtual long precursorCount() const {return precursorMZs_.size();}
    virtual long precursorCharge() const;
    virtual double precursorMZ(long index, bool preferMonoisotope) const;
    virtual double precursorActivationEnergy(long index) const {return precursorActivationEnergies_[index];}

    virtual vector<double> getIsolationWidths() const;

    virtual ActivationType supplementalActivationType() const {return saType_;}
    virtual double supplementalActivationEnergy() const {return saEnergy_;}

    virtual long parentCount() const {return precursorCount();}
    virtual long parentCharge() const {return precursorCharge();}
    virtual double parentMass(long index, bool preferMonoisotope) const {return precursorMZ(index, preferMonoisotope);}
    virtual double parentEnergy(long index) const {return precursorActivationEnergy(index);}

    virtual size_t scanRangeCount() const {return scanRanges_.size();}
    virtual const pair<double, double>& scanRange(size_t index) const {return scanRanges_[index];}

    virtual bool isProfileScan() const {return isProfileScan_;}
    virtual bool isCentroidScan() const {return isCentroidScan_;}
    virtual long packetCount() const {return packetCount_;}
    virtual double startTime() const {return startTime_;}
    virtual double lowMass() const {return lowMass_;}
    virtual double highMass() const {return highMass_;}
    virtual double totalIonCurrent() const {return totalIonCurrent_;}
    virtual double basePeakMass() const {return basePeakMZ_;}
    virtual double basePeakMZ() const {return basePeakMZ_;}
    virtual double basePeakIntensity() const {return basePeakIntensity_;}
    virtual long channelCount() const {return channelCount_;}
    virtual double frequency() const {return frequency_;}
    virtual bool FAIMSOn() const {return faimsOn_;}
    virtual double compensationVoltage() const {return compensationVoltage_;}

    virtual bool isConstantNeutralLoss() const { return constantNeutralLoss_;};
    virtual double analyzerScanOffset() const { return analyzerScanOffset_;};


    virtual long statusLogSize() const {initStatusLog(); return statusLogSize_;}
    virtual double statusLogRT() const {initStatusLog(); return statusLogRT_;}
    virtual std::string statusLogLabel(long index) const {initStatusLog(); return statusLogLabels_.at(index);}
    virtual std::string statusLogValue(long index) const {initStatusLog(); return statusLogValues_.at(index);}

    virtual long trailerExtraSize() const {initTrailerExtra(); return trailerExtraSize_;}
    virtual std::string trailerExtraLabel(long index) const {initTrailerExtra(); return trailerExtraLabels_.at(index);}
    virtual std::string trailerExtraValue(long index) const {initTrailerExtra(); return trailerExtraValues_.at(index);}
    virtual std::string trailerExtraValue(const string& name) const {initTrailerExtra(); return trailerExtraMap_[name];}
    virtual double trailerExtraValueDouble(const string& name) const;
    virtual long trailerExtraValueLong(const string& name) const;

    private:

    long scanNumber_;
    int scanSegment_;
    int scanEvent_;
#ifndef _WIN64
    const RawFileImpl* rawfile_;
    string filter_;
#else
    const RawFileThreadImpl* rawfile_;
    // TODO: make this static without breaking MSTest
    gcroot<IFilterParser^> filterParser_;
    gcroot<IScanFilter^> filter_;
#endif
    MassAnalyzerType massAnalyzerType_;
    IonizationType ionizationType_;
    ActivationType activationType_, saType_;
    long msLevel_;
    ScanType scanType_;
    PolarityType polarityType_;
    bool isEnhanced_;
    bool isDependent_;
    bool hasMultiplePrecursors_; // true for "MSX" mode or when there are "SPS Masses"
    bool isCorona_;
    bool isPhotoIonization_;
    bool isSourceCID_;
    bool isDetectorSet_;
    bool isTurboScan_;
    bool isWideband_; // wideband activation
    bool hasLockMass_;
    bool isSPS_;
    AccurateMassType accurateMassType_;
    bool supplementalActivation_;
    vector<double> precursorMZs_;
    vector<double> precursorActivationEnergies_;
    vector<PrecursorInfo> precursorInfo_;
    vector<pair<double, double> > scanRanges_;
    bool isProfileScan_;
    bool isCentroidScan_;
    long packetCount_;
    double startTime_;
    double lowMass_;
    double highMass_;
    double totalIonCurrent_;
    double basePeakMZ_;
    double basePeakIntensity_;
    long channelCount_;
    double frequency_;
    bool faimsOn_;
    double compensationVoltage_;
    double saEnergy_;
    std::vector<double> spsMasses_;

    bool constantNeutralLoss_;
    double analyzerScanOffset_;

    mutable bool statusLogInitialized_;
    mutable long statusLogSize_;
    mutable double statusLogRT_;
    mutable std::vector<std::string> statusLogLabels_;
    mutable std::vector<std::string> statusLogValues_;

    mutable bool trailerExtraInitialized_;
    mutable long trailerExtraSize_;
    mutable std::vector<std::string> trailerExtraLabels_;
    mutable std::vector<std::string> trailerExtraValues_;
    mutable map<string,string> trailerExtraMap_;

    void initialize();
    void initStatusLog() const;
    void initStatusLogHelper() const;
    void initTrailerExtra() const;
    void initTrailerExtraHelper() const;
    void parseFilterString();
};


#ifndef _WIN64
ScanInfoImpl::ScanInfoImpl(long scanNumber, const RawFileImpl* raw)
#else
ScanInfoImpl::ScanInfoImpl(long scanNumber, const RawFileThreadImpl* raw)
#endif
:   scanNumber_(scanNumber),
    rawfile_(raw)    
{
    initialize();
}

ScanInfoImpl::ScanInfoImpl(const std::string& filterString) : scanNumber_(0), rawfile_(nullptr)
{
#ifdef _WIN64
    filterParser_ = FilterParserFactory::CreateFilterParser();
#endif
    reinitialize(filterString);
}

void ScanInfoImpl::reinitialize(const string& filter)
{
    try
    {
#ifndef _WIN64
        filter_ = filter;
#else
        filter_ = filterParser_->GetFilterFromString(ToSystemString(filter));
#endif
        initialize();
    }
    CATCH_AND_FORWARD
}

void ScanInfoImpl::initialize()
{
    try
    {
        scanSegment_ = 0;
        scanEvent_ = 0;
        massAnalyzerType_ = (MassAnalyzerType_Unknown);
        ionizationType_ = (IonizationType_Unknown);
        activationType_ = (ActivationType_Unknown);
        saType_ = (ActivationType_Unknown);
        msLevel_ = (1);
        scanType_ = (ScanType_Unknown);
        polarityType_ = (PolarityType_Unknown);
        isEnhanced_ = (false);
        isDependent_ = (false);
        isCorona_ = (false);
        isPhotoIonization_ = (false);
        isSourceCID_ = (false);
        isDetectorSet_ = (false);
        isTurboScan_ = (false);
        isWideband_ = (false);
        hasLockMass_ = (false);
        isSPS_ = (false);
        accurateMassType_ = (AccurateMass_Unknown);
        hasMultiplePrecursors_ = (false);
        supplementalActivation_ = (false);
        isProfileScan_ = (false);
        isCentroidScan_ = (false);
        packetCount_ = (0);
        startTime_ = (0);
        lowMass_ = (0);
        highMass_ = (0);
        totalIonCurrent_ = (0);
        basePeakMZ_ = (0);
        basePeakIntensity_ = (0);
        channelCount_ = (0);
        frequency_ = (0);
        faimsOn_ = (false);
        compensationVoltage_ = (0);
        saEnergy_ = (0);
        statusLogInitialized_ = (false);
        statusLogSize_ = (0);
        statusLogRT_ = (0);
        statusLogLabels_.clear();
        statusLogValues_.clear();
        trailerExtraInitialized_ = (false);
        trailerExtraSize_ = (0);
        trailerExtraLabels_.clear();
        trailerExtraValues_.clear();
        scanRanges_.clear();
        precursorMZs_.clear();
        precursorActivationEnergies_.clear();
        precursorInfo_.clear();
        trailerExtraMap_.clear();
        spsMasses_.clear();

        if (scanNumber_ > 0)
        {
            // TODO: figure out which controllers have filters, PDA/UV does not!
            if (rawfile_->currentControllerType_ == Controller_MS)
            {
#ifndef _WIN64
                _bstr_t bstrFilter;
                checkResult(rawfile_->raw_->GetFilterForScanNum(scanNumber_, bstrFilter.GetAddress()));
                filter_ = (const char*)(bstrFilter);
#else
                filter_ = rawfile_->raw_->GetFilterForScanNumber(scanNumber_);
#endif
            }

#ifndef _WIN64
            long isUniformTime = 0;
            HRESULT hr = rawfile_->raw_->GetScanHeaderInfoForScanNum(scanNumber_,
                                                                    &packetCount_,
                                                                    &startTime_,
                                                                    &lowMass_,
                                                                    &highMass_,
                                                                    &totalIonCurrent_,
                                                                    &basePeakMZ_,
                                                                    &basePeakIntensity_,
                                                                    &channelCount_,
                                                                    &isUniformTime,
                                                                    &frequency_);
            if (hr != 0) 
                checkResult(rawfile_->raw_->GetStartTime(&startTime_));
#else
            auto scanStats = rawfile_->raw_->GetScanStatsForScanNumber(scanNumber_);
            scanSegment_ = scanStats->SegmentNumber;
            scanEvent_ = scanStats->ScanEventNumber;
            packetCount_ = scanStats->PacketCount;
            startTime_ = scanStats->StartTime;
            lowMass_ = scanStats->LowMass;
            highMass_ = scanStats->HighMass;
            totalIonCurrent_ = scanStats->TIC;
            basePeakMZ_ = scanStats->BasePeakMass;
            basePeakIntensity_ = scanStats->BasePeakIntensity;
            channelCount_ = scanStats->NumberOfChannels;
#endif
        }
        parseFilterString();

        if (scanNumber_ > 0)
        {
            // append SPS masses to precursors parsed from filter string
            string spsMassesStr = rawfile_->getTrailerExtraValue(scanNumber_, "SPS Masses:") + rawfile_->getTrailerExtraValue(scanNumber_, "SPS Masses Continued:");
            bal::trim(spsMassesStr);
            if (!spsMassesStr.empty())
            {
                vector<string> tokens;
                bal::split(tokens, spsMassesStr, bal::is_any_of(","));

                double isolationWidth = precursorInfo_.back().isolationWidth;

                // skip first SPS mass which has already been added to precursorMZs_ in parseFilterString()
                for (size_t i = 1; i < tokens.size(); ++i)
                {
                    bal::trim(tokens[i]);
                    if (tokens[i].empty())
                        continue;
                    spsMasses_.push_back(lexical_cast<double>(tokens[i]));
                    precursorMZs_.push_back(spsMasses_.back());
                    precursorActivationEnergies_.push_back(precursorActivationEnergies_.back());
                    precursorInfo_.push_back(PrecursorInfo{ msLevel_ - 1, spsMasses_.back(), spsMasses_.back(), isolationWidth, precursorActivationEnergies_.back(), activationType_, 0, 0 });
                }
                hasMultiplePrecursors_ = true;
                isSPS_ = true;
            }
        }
    }
    CATCH_AND_FORWARD_EX(filter())
}

void ScanInfoImpl::initStatusLog() const
{
    if (scanNumber_ == 0)
        return;

    if (!statusLogInitialized_)
        initStatusLogHelper();
}

void ScanInfoImpl::initStatusLogHelper() const
{
    try
    {
        statusLogInitialized_ = true;
#ifndef _WIN64
        _variant_t variantStatusLogLabels;
        _variant_t variantStatusLogValues;

        checkResult(rawfile_->raw_->GetStatusLogForScanNum(scanNumber_,
                                                 &statusLogRT_,
                                                 &variantStatusLogLabels,
                                                 &variantStatusLogValues,
                                                 &statusLogSize_));
        VariantStringArray labels(variantStatusLogLabels, statusLogSize_);
        VariantStringArray values(variantStatusLogValues, statusLogSize_);
        for(size_t i=0; i < statusLogSize_; ++i)
        {
            statusLogLabels_.emplace_back(labels.item(i));
            statusLogValues_.emplace_back(values.item(i));
        }
#else
        auto logEntry = rawfile_->raw_->GetStatusLogForRetentionTime(rawfile_->rt(scanNumber_));
        statusLogRT_ = rawfile_->rt(scanNumber_);
        ToStdVector(logEntry->Labels, statusLogLabels_);
        ToStdVector(logEntry->Values, statusLogValues_);
#endif
    }
    CATCH_AND_FORWARD
}

void ScanInfoImpl::initTrailerExtra() const
{
    if (scanNumber_ == 0)
        return;

    if (!trailerExtraInitialized_)
        initTrailerExtraHelper();
}

void ScanInfoImpl::initTrailerExtraHelper() const
{
    try
    {
        trailerExtraInitialized_ = true;

#ifndef _WIN64
        _variant_t variantTrailerExtraLabels;
        _variant_t variantTrailerExtraValues;

        try
        {
            checkResult(rawfile_->raw_->GetTrailerExtraForScanNum(scanNumber_,
                                                        &variantTrailerExtraLabels,
                                                        &variantTrailerExtraValues,
                                                        &trailerExtraSize_));
        }
        catch (RawEgg& e)
        {
            if (bal::contains(e.what(), "Incorrect function"))
            {
                trailerExtraLabels_.clear();
                trailerExtraValues_.clear();
                return;
            }
            else
                throw;
        }
        VariantStringArray labels(variantTrailerExtraLabels, trailerExtraSize_);
        VariantStringArray values(variantTrailerExtraValues, trailerExtraSize_);
        for (size_t i = 0; i < trailerExtraSize_; ++i)
        {
            trailerExtraLabels_.emplace_back(labels.item(i));
            trailerExtraValues_.emplace_back(values.item(i));
        }

        if (trailerExtraLabels_.size() != trailerExtraValues_.size())
            throw RawEgg("[ScanInfoImpl::initTrailerExtra()] Trailer Extra sizes do not match."); 

        for (int i=0; i < trailerExtraLabels_.size(); i++)
            trailerExtraMap_[trailerExtraLabels_[i]] = trailerExtraValues_[i];
#else
        auto trailerExtra = rawfile_->raw_->GetTrailerExtraInformation(scanNumber_);
        trailerExtraLabels_.reserve(trailerExtra->Length);
        trailerExtraValues_.reserve(trailerExtra->Length);
        for (int i = 0; i < trailerExtra->Length; ++i)
        {
            trailerExtraLabels_.push_back(ToStdString(trailerExtra->Labels[i]));
            trailerExtraValues_.push_back(ToStdString(trailerExtra->Values[i]));
            trailerExtraMap_[trailerExtraLabels_.back()] = trailerExtraValues_.back();
        }
#endif
        trailerExtraSize_ = trailerExtraLabels_.size();
    }
    CATCH_AND_FORWARD

}


#ifdef _WIN64
namespace {
    ActivationType convertRawFileReaderActivationType(ThermoEnum::ActivationType activationType)
    {
        switch (activationType)
        {
            case ThermoEnum::ActivationType::CollisionInducedDissociation: return ActivationType_CID;
            case ThermoEnum::ActivationType::ElectronCaptureDissociation: return ActivationType_ECD;
            case ThermoEnum::ActivationType::ElectronTransferDissociation: return ActivationType_ETD;
            case ThermoEnum::ActivationType::HigherEnergyCollisionalDissociation: return ActivationType_HCD;
            case ThermoEnum::ActivationType::MultiPhotonDissociation: return ActivationType_MPD;
            case ThermoEnum::ActivationType::NegativeElectronTransferDissociation: return ActivationType_NETD;
            case ThermoEnum::ActivationType::NegativeProtonTransferReaction: return ActivationType_NPTR;
            case ThermoEnum::ActivationType::PQD: return ActivationType_PQD;
            case ThermoEnum::ActivationType::ProtonTransferReaction: return ActivationType_PTR;
            case ThermoEnum::ActivationType::SAactivation: return ActivationType_CID;
            case ThermoEnum::ActivationType::UltraVioletPhotoDissociation: return ActivationType_MPD; // FIXME
            default: return ActivationType_Unknown;
        }
    }
}
#endif


void ScanInfoImpl::parseFilterString()
{
#ifndef _WIN64
    if (filter_.empty())
        return;

    ScanFilter filterParser;
    try
    {
        filterParser.parse(filter_);
    }
    catch (exception& e)
    {
        throw RawEgg("[ScanInfoImpl::parseFilterString()] error parsing filter \"" + filter_ + "\": " + e.what());
    }

    msLevel_ = filterParser.msLevel_;
    massAnalyzerType_ = convertScanFilterMassAnalyzer(filterParser.massAnalyzerType_,
                                                      rawfile_ == nullptr ? InstrumentModelType_Unknown : rawfile_->getInstrumentModel());
    ionizationType_ = filterParser.ionizationType_;
    polarityType_ = filterParser.polarityType_;
    scanType_ = filterParser.scanType_;
    activationType_ = filterParser.activationType_;
    isEnhanced_ = filterParser.enhancedOn_ == TriBool_True;
    isDependent_ = filterParser.dependentActive_ == TriBool_True;
    hasMultiplePrecursors_ = filterParser.multiplePrecursorMode_;
    isCorona_ = filterParser.coronaOn_ == TriBool_True;
    isPhotoIonization_ = filterParser.photoIonizationOn_ == TriBool_True;
    isSourceCID_ = filterParser.sourceCIDOn_ == TriBool_True;
    isDetectorSet_ = filterParser.detectorSet_ == TriBool_True;
    isTurboScan_ = filterParser.turboScanOn_ == TriBool_True;
    isWideband_ = filterParser.widebandOn_ == TriBool_True;
    hasLockMass_ = filterParser.lockMassOn_ == TriBool_True;
    isSPS_ = filterParser.spsOn_ == TriBool_True;
    accurateMassType_ = filterParser.accurateMassType_;
    precursorMZs_.insert(precursorMZs_.end(), filterParser.precursorMZs_.begin(), filterParser.precursorMZs_.end());
    precursorActivationEnergies_.insert(precursorActivationEnergies_.end(), filterParser.precursorEnergies_.begin(), filterParser.precursorEnergies_.end());

    supplementalActivation_ = filterParser.supplementalCIDOn_ == TriBool_True && activationType_ & ActivationType_ETD && !filterParser.saTypes_.empty();
    if (supplementalActivation_)
    {
        saType_ = filterParser.saTypes_[0];
        saEnergy_ = filterParser.saEnergies_[0];
    }

    isProfileScan_ = filterParser.dataPointType_ == DataPointType_Profile;
    isCentroidScan_ = filterParser.dataPointType_ == DataPointType_Centroid;
	faimsOn_ = filterParser.faimsOn_ == TriBool_True;
	compensationVoltage_ = filterParser.compensationVoltage_;
    constantNeutralLoss_ = filterParser.constantNeutralLoss_;
    analyzerScanOffset_ = filterParser.analyzer_scan_offset_;

    // overwrite the filter line's isolation m/z with the value from GetPrecursorMassFromScanNum()
    if (precursorMZs_.size() > msLevel_-2 && isDependent_ && !hasMultiplePrecursors_ && rawfile_ != nullptr)
        for (int i = msLevel_-2; i >= 0; --i)
            precursorMZs_[i] = rawfile_->getPrecursorMass(scanNumber_, MSOrder(i+2));

    for (size_t i=0; i < filterParser.scanRangeMin_.size(); ++i)
        scanRanges_.push_back(make_pair(filterParser.scanRangeMin_[i], filterParser.scanRangeMax_[i]));
#else // is WIN64
    if ((IScanFilter^) filter_ == nullptr)
        return;

    try
    {
        auto msOrder = filter_->MSOrder;
        msLevel_ = (long) msOrder < -1 ? 2 : (long) msOrder; // parent ion scan is MSOrder -1, is used as a special value; neutral gain, neutral loss are MSOrder < -1 and are treated as MS2
        massAnalyzerType_ = convertScanFilterMassAnalyzer((ScanFilterMassAnalyzerType)filter_->MassAnalyzer,
                                                          rawfile_ == nullptr ? InstrumentModelType_Unknown : rawfile_->getInstrumentModel());
        ionizationType_ = (IonizationType)filter_->IonizationMode;
        polarityType_ = (PolarityType)filter_->Polarity;
        scanType_ = (ScanType)filter_->ScanMode;
        activationType_ = msLevel_ > 1 ? convertRawFileReaderActivationType(filter_->GetActivation(0)) : ActivationType_Unknown;
        isEnhanced_ = filter_->Enhanced == ThermoEnum::TriState::On;
        isDependent_ = filter_->Dependent == ThermoEnum::TriState::On;
        hasMultiplePrecursors_ = filter_->Multiplex == ThermoEnum::TriState::On;
        isWideband_ = filter_->Wideband == ThermoEnum::TriState::On;
        isSPS_ = filter_->MultiNotch == ThermoEnum::TriState::On;
        hasLockMass_ = filter_->Lock == ThermoEnum::TriState::On;
        isTurboScan_ = filter_->TurboScan == ThermoEnum::TriState::On || filter_->ParamR == ThermoEnum::TriState::On;
        isPhotoIonization_ = filter_->PhotoIonization == ThermoEnum::TriState::On;
        isCorona_ = filter_->Corona == ThermoEnum::TriState::On;
        isDetectorSet_ = filter_->Detector == ThermoEnum::DetectorType::Valid;
        isSourceCID_ = filter_->SourceFragmentation == ThermoEnum::TriState::On;
        accurateMassType_ = filter_->AccurateMass == FilterAccurateMass::Any ? AccurateMass_Unknown : (AccurateMassType) filter_->AccurateMass;
        constantNeutralLoss_ = msOrder == ThermoEnum::MSOrderType::Ng || msOrder == ThermoEnum::MSOrderType::Nl;
        analyzerScanOffset_ = constantNeutralLoss_ ? filter_->GetMass(0) : 0;

        if (scanType_ == ScanType_Q1MS || scanType_ == ScanType_Q3MS)
        {
            msLevel_ = 1;
            scanType_ = ScanType_Full;
        }

        // CONSIDER: does detector set always mean CID is really HCD?
        if (filter_->Detector == ThermoEnum::DetectorType::Valid &&
            massAnalyzerType_ == MassAnalyzerType_FTICR &&
            activationType_ == ActivationType_CID)
            activationType_ = ActivationType_HCD;

        if ((msLevel_ > 1 && !constantNeutralLoss_) || msLevel_ == -1) // workaround bug(?) where MS1 have MassRange and Reaction
            for (int i = 0; i < filter_->MassCount && (i < msLevel_-1 || hasMultiplePrecursors_ || msLevel_ == -1); ++i)
            {
                precursorMZs_.push_back(filter_->GetMass(i));
                precursorActivationEnergies_.push_back(filter_->GetEnergy(i));
            }

        supplementalActivation_ = filter_->SupplementalActivation == ThermoEnum::TriState::On && activationType_ & ActivationType_ETD;
        if (supplementalActivation_)
        {
            if (filter_->MassCount > 1)
            {
                saType_ = convertRawFileReaderActivationType(filter_->GetActivation(1));
                saEnergy_ = filter_->GetEnergy(1);
            }
            else // if sa flag is set on ms2 scan with no saTypes, it's still supplemental CID or HCD
            {
                // CONSIDER: does detector set always mean CID is really HCD?
                if (filter_->Detector == ThermoEnum::DetectorType::Valid &&
                    massAnalyzerType_ == MassAnalyzerType_FTICR)
                    saType_ = ActivationType_HCD;
                else
                    saType_ = ActivationType_CID;

                saEnergy_ = 0; // every precursor must have an energy and it defaults to 0 if not present
            }

            activationType_ = static_cast<ActivationType>(activationType_ | saType_);
        }

        isProfileScan_ = filter_->ScanData == ThermoEnum::ScanDataType::Profile;
        isCentroidScan_ = filter_->ScanData == ThermoEnum::ScanDataType::Centroid;
        faimsOn_ = filter_->CompensationVoltage == ThermoEnum::TriState::On;
        compensationVoltage_ = faimsOn_ ? filter_->CompensationVoltageValue(0) : 0;

        for (int i=0; i < filter_->MassRangeCount; ++i)
            scanRanges_.push_back(make_pair(filter_->GetMassRange(i)->Low, filter_->GetMassRange(i)->High));
    }
    CATCH_AND_FORWARD_EX(ToStdString(filter_->ToString()))
#endif

    if (precursorMZs_.empty() || msLevel_ < 1)
        return;

    auto isolationWidths = getIsolationWidths();
    for (size_t i = 0; i < msLevel_ - 1; ++i)
        precursorInfo_.push_back(PrecursorInfo{ int(i+1), precursorMZs_[i], precursorMZs_[i], isolationWidths[i], precursorActivationEnergies_[i], activationType_, 0, 0 });

    if (hasMultiplePrecursors_ && spsMasses_.empty()) // MSX mode means there can be more than 1 filter line m/z for the current ms level
    {
        for (size_t i = msLevel_ - 1; i < precursorMZs_.size(); ++i)
            precursorInfo_.push_back(PrecursorInfo{ msLevel_ - 1, precursorMZs_[i], precursorMZs_[i], isolationWidths.back(), precursorActivationEnergies_[i], activationType_, 0, 0 });
    }
}

const vector<PrecursorInfo>& ScanInfoImpl::precursorInfo() const
{
    return precursorInfo_;
}

long ScanInfoImpl::precursorCharge() const
{
    // "Charge State" header item for SPS spectra refers to MS2's precursor charge;
    // however, I have seen spectra with a single SPS mass that have real MS3 precursor charge states
    // CONSIDER: real charge states might be available in the instrument method
    if (spsMasses_.size() > 1)
        return 0;

    try
    {
        return trailerExtraValueLong("Charge State:");
    }
    catch (RawEgg&)
    {
        // almost certainly means that the label was not present
        return 0;
    }
}

double ScanInfoImpl::precursorMZ(long index, bool preferMonoisotope) const
{
    if (preferMonoisotope)
    {
        try
        {
            double mz = trailerExtraValueDouble("Monoisotopic M/Z:");
            if (mz > 0)
                return mz;
        }
        catch (RawEgg&)
        {
            // almost certainly means that the label was not present
        }
    }
    return precursorMZs_[index];
}

double ScanInfoImpl::trailerExtraValueDouble(const string& name) const
{
    if (scanNumber_ == 0) return 0.0;
    return rawfile_->getTrailerExtraValueDouble(scanNumber_, name);
}


long ScanInfoImpl::trailerExtraValueLong(const string& name) const
{
    if (scanNumber_ == 0) return 0;
    return rawfile_->getTrailerExtraValueLong(scanNumber_, name);
}


ScanInfoPtr RawFileImpl::getScanInfo(long scanNumber) const
{
#ifndef _WIN64
    ScanInfoPtr scanInfo(new ScanInfoImpl(scanNumber, this));
    return scanInfo;
#else
    throw runtime_error("getScanInfo must be called from RawFileThreadImpl");
#endif
}

ScanInfoPtr RawFile::getScanInfoFromFilterString(const string& filterString)
{
    ScanInfoPtr scanInfo(new ScanInfoImpl(filterString));
    return scanInfo;
}


MSOrder RawFileImpl::getMSOrder(long scanNumber) const
{
    try
    {
#ifndef _WIN64
        IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

        long result;
        checkResult(raw4->GetMSOrderForScanNum(scanNumber, &result));
        return (MSOrder) result;
#else
        return (MSOrder) raw_->GetFilterForScanNumber(scanNumber)->MSOrder;
#endif
    }
    CATCH_AND_FORWARD
}


double RawFileImpl::getPrecursorMass(long scanNumber, MSOrder msOrder) const
{
    try
    {
#ifndef _WIN64
        IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

        if (msOrder < MSOrder_MS2)
            msOrder = getMSOrder(scanNumber);

        double result;
        checkResult(raw4->GetPrecursorMassForScanNum(scanNumber, msOrder, &result));
#else
        double result = raw_->GetFilterForScanNumber(scanNumber)->GetMass((int) msOrder - 2);
        // raw_->GetFilterForScanNumber(scanNumber)->GetIsolationWidthOffset() ??
#endif
        return calculateIsolationMzWithOffset(scanNumber, result);
    }
    CATCH_AND_FORWARD
}

double RawFileImpl::calculateIsolationMzWithOffset(long scanNumber, double isolationMzPossiblyWithOffset) const
{
    try
    {
        // if scan description is empty, scan can't be mapped back to instrument method, and thus reported mass is not known (could be either offset or original)
        string scanDescription = getTrailerExtraValue(scanNumber, "Scan Description:");
        if (bal::trim_copy(scanDescription).empty())
        {
            double monoMz = getTrailerExtraValueDouble(scanNumber, "Monoisotopic M/Z:");
            if (monoMz > 0)
            {
                double offset = getTrailerExtraValueDouble(scanNumber, "MS2 Isolation Offset:");
                double iw = getTrailerExtraValueDouble(scanNumber, "MS2 Isolation Width:");
                if (iw - fabs(monoMz - isolationMzPossiblyWithOffset) < -fabs(offset)) // if true, reported mass is probably original
                    isolationMzPossiblyWithOffset += offset;
            }
        }
        else
        {
            if (isolationMzOffsetByScanDescription.empty())
                return isolationMzPossiblyWithOffset;

            auto findItr = isolationMzOffsetByScanDescription.find(scanDescription);
            if (findItr != isolationMzOffsetByScanDescription.end() && !findItr->second.reportedMassIsOffset)
                isolationMzPossiblyWithOffset += findItr->second.offset;
        }
    }
    catch (RawEgg&)
    {
    }

    return isolationMzPossiblyWithOffset;
}


ScanType RawFileImpl::getScanType(long scanNumber) const
{
    try
    {
#ifndef _WIN64
        IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

        long result;
        checkResult(raw4->GetScanTypeForScanNum(scanNumber, &result));
        return (ScanType) result;
#else
        return (ScanType) raw_->GetFilterForScanNumber(scanNumber)->ScanMode;
#endif
    }
    CATCH_AND_FORWARD
}


ScanFilterMassAnalyzerType RawFileImpl::getMassAnalyzerType(long scanNumber) const
{
    try
    {
#ifndef _WIN64
        IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

        long result;
        checkResult(raw4->GetMassAnalyzerTypeForScanNum(scanNumber, &result));
        return (ScanFilterMassAnalyzerType) result;
#else
        return (ScanFilterMassAnalyzerType) raw_->GetFilterForScanNumber(scanNumber)->MassAnalyzer;
#endif
    }
    CATCH_AND_FORWARD
}


ActivationType RawFileImpl::getActivationType(long scanNumber) const
{
    try
    {
#ifndef _WIN64
        IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

        long result;
        checkResult(raw4->GetActivationTypeForScanNum(scanNumber, MSOrder_Any, &result));
        return (ActivationType) result;
#else
        return (ActivationType)raw_->GetFilterForScanNumber(scanNumber)->GetActivation(0);
#endif
    }
    CATCH_AND_FORWARD
}


void RawFileImpl::parseInstrumentMethod()
{
    using namespace boost::xpressive;

    string instrumentMethods;

    auto methods = getInstrumentMethods();

    for (int i=0; i < methods.size(); ++i)
        instrumentMethods += methods[i] + "\n";

    int scanSegment = 1, scanEvent;
    bool scanEventDetails = false;
    bool dataDependentSettings = false;
    double lastIsolationMzOffset = 0;
    string lastReportedMassType = "Original"; // assume original for older instruments where reported mass is not given


    sregex scanSegmentRegex = sregex::compile("\\s*Segment (\\d+) Information\\s*");
    sregex scanEventRegex = sregex::compile("\\s*(\\d+):.*");
    sregex scanEventIsolationWidthRegex = sregex::compile("\\s*Isolation Width:\\s*(\\S+)\\s*");
    sregex scanEventIsoWRegex = sregex::compile("\\s*MS.*:.*\\s+IsoW\\s+(\\S+)\\s*");
    sregex repeatedEventRegex = sregex::compile("\\s*Scan Event (\\d+) repeated for top (\\d+)\\s*");
    sregex defaultIsolationWidthRegex = sregex::compile("\\s*MS(\\d+) Isolation Width:\\s*(\\S+)\\s*");
    sregex defaultIsolationWindowRegex = sregex::compile("\\s*Isolation Window \\(m/z\\) =\\s*(\\S+)\\s*");
    sregex isolationMzOffsetRegex = sregex::compile("\\s*Isolation m/z Offset =\\s*(\\S+)\\s*");
    sregex reportedMassRegex = sregex::compile("\\s*Reported Mass =\\s*(\\S+) Mass\\s*");
    sregex scanDescriptionRegex = sregex::compile("\\s*Scan Description =\\s*(\\S+)\\s*");

    smatch what;
    string line;
    istringstream instrumentMethodStream(instrumentMethods);
    while (std::getline(instrumentMethodStream, line))
    {
        string::const_iterator start = line.begin(), end = line.end();

        // Segment 1 Information
        if (regex_match(line, what, scanSegmentRegex))
        {
            scanSegment = lexical_cast<int>(what[1]);
            continue;
        }

        // Scan Event Details:
        if (bal::icontains(line, "Scan Event Details"))
        {
            scanEventDetails = true;
            continue;
        }

        if (scanEventDetails)
        {
            // 2:  ITMS + c norm Dep MS/MS Most intense ion from (1)
            //       ...
            //       Isolation Width:         2.5
            //       ...
            if (regex_match(line, what, scanEventRegex))
            {
                scanEvent = lexical_cast<int>(what[1]);
                continue;
            }

            if (regex_match(line, what, scanEventIsolationWidthRegex) || regex_match(line, what, scanEventIsoWRegex))
            {
                isolationWidthBySegmentAndScanEvent[scanSegment][scanEvent] = lexical_cast<double>(what[1]);
                continue;
            }

            // Scan Event M repeated for top N peaks
            if (regex_match(line, what, repeatedEventRegex))
            {
                int repeatedEvent = lexical_cast<int>(what[1]);
                int repeatCount = lexical_cast<int>(what[2]);
                double repeatedIsolationWidth = isolationWidthBySegmentAndScanEvent[scanSegment][repeatedEvent];
                for (int i = repeatedEvent + 1; i < repeatedEvent + repeatCount; ++i)
                    isolationWidthBySegmentAndScanEvent[scanSegment][i] = repeatedIsolationWidth;
                continue;
            }

            if (bal::all(line, bal::is_space()))
                scanEventDetails = false;
        }

        // Data Dependent Settings:
        if (bal::icontains(line, "Data Dependent Settings") || bal::icontains(line, "Scan DIAScan"))
        {
            dataDependentSettings = true;
            continue;
        }

        if (dataDependentSettings)
        {
            // MSn Isolation Width:       2.5
            if (regex_match(line, what, defaultIsolationWidthRegex))
            {
                int msLevel = lexical_cast<int>(what[1]);
                double isolationWidth = lexical_cast<double>(what[2]);
                defaultIsolationWidthBySegmentAndMsLevel[scanSegment][msLevel] = isolationWidth;
                continue;
            }
            
            if (regex_match(line, what, defaultIsolationWindowRegex))
            {
                double isolationWidth = lexical_cast<double>(what[1]);
                defaultIsolationWidthBySegmentAndMsLevel[scanSegment][2] = isolationWidth;
                continue;
            }

            if (bal::all(line, bal::is_space()))
                dataDependentSettings = false;
        }

        if (regex_match(line, what, isolationMzOffsetRegex))
        {
            lastIsolationMzOffset = lexical_cast<double>(what[1]);
            continue;
        }

        if (regex_match(line, what, reportedMassRegex))
        {
            lastReportedMassType = what[1];
            continue;
        }

        if (regex_match(line, what, scanDescriptionRegex) && lastIsolationMzOffset != 0)
        {
            string scanDescription = what[1];
            isolationMzOffsetByScanDescription[scanDescription] = IsolationMzOffset{ lastIsolationMzOffset, lastReportedMassType == "Offset" };
            lastIsolationMzOffset = 0;
            lastReportedMassType = "Original";
            continue;
        }

        if (bal::all(line, bal::is_space()))
            lastIsolationMzOffset = 0;
    }
}

vector<double> ScanInfoImpl::getIsolationWidths() const
{
    vector<double> isolationWidths(max(0l, msLevel_ - 1), 0);

    if (scanNumber_ == 0)
        return isolationWidths;

    if (!spsMasses_.empty())
    {
        isolationWidths.clear();
#ifndef _WIN64
        double isolationWidth;
        checkResult(rawfile_->raw_->GetIsolationWidthForScanNum(scanNumber_, msLevel_ - 1, &isolationWidth));
        isolationWidths.resize(precursorMZs_.size(), isolationWidth);
#else
        isolationWidths.resize(precursorMZs_.size(), filter_->GetIsolationWidth(filter_->MassCount - 1));
#endif
        return isolationWidths;
    }

#ifndef _WIN64
    long msOrder;
    checkResult(rawfile_->raw_->GetMSOrderForScanNum(scanNumber_, &msOrder));
    if (msOrder == 1)
        return isolationWidths;

    long numMSOrders;
    checkResult(rawfile_->raw_->GetNumberOfMSOrdersFromScanNum(scanNumber_, &numMSOrders));
    isolationWidths.resize(max(numMSOrders, msLevel_ - 1));
    for (long i = 0; i < isolationWidths.size(); i++)
    {
        checkResult(rawfile_->raw_->GetIsolationWidthForScanNum(scanNumber_, i, &isolationWidths[i]));
    }
#else
    MSOrder msOrder = (MSOrder) filter_->MSOrder;
    if ((int) msOrder == 1)
        return isolationWidths;

    long massCount = filter_->MassCount;
    isolationWidths.resize(max(massCount, msLevel_ - 1));
    for (long i = 0; i < isolationWidths.size(); i++)
    {
        isolationWidths[i] = filter_->GetIsolationWidth(i);
    }
#endif
    return isolationWidths;
}

double RawFileImpl::getIsolationWidth(int scanSegment, int scanEvent) const
{
    if (isolationWidthBySegmentAndScanEvent.count(scanSegment) > 0 &&
        isolationWidthBySegmentAndScanEvent[scanSegment].count(scanEvent) > 0)
        return isolationWidthBySegmentAndScanEvent[scanSegment][scanEvent];
    return 0.0;
}


double RawFileImpl::getDefaultIsolationWidth(int scanSegment, int msLevel) const
{
    if (defaultIsolationWidthBySegmentAndMsLevel.count(scanSegment) > 0 &&
        defaultIsolationWidthBySegmentAndMsLevel[scanSegment].count(msLevel) > 0)
        return defaultIsolationWidthBySegmentAndMsLevel[scanSegment][msLevel];
    return 0.0;
}


ErrorLogItem RawFileImpl::getErrorLogItem(long itemNumber) const
{
    try
    {
        ErrorLogItem result;

#ifndef _WIN64
        _bstr_t bstr;
        checkResult(raw_->GetErrorLogItem(itemNumber, &result.rt, bstr.GetAddress()));
        result.errorMessage = (const char*)(bstr);
#else
        auto logEntry = raw_->GetErrorLogItem(itemNumber);
        result.rt = logEntry->RetentionTime;
        result.errorMessage = ToStdString(logEntry->Message);
#endif
        return result;
    }
    CATCH_AND_FORWARD
}

#ifndef _WIN64
namespace {
class TuneDataLabelValueArray : public LabelValueArray
{
    public:

    TuneDataLabelValueArray(VARIANT& variantLabels, long size,
                            const IXRawfilePtr& raw, long segmentNumber)
    :   labels_(variantLabels, size),
        raw_(raw),
        segmentNumber_(segmentNumber)
    {}

    virtual int size() const {return labels_.size();}
    virtual string label(int index) const {return labels_.item(index);}

    virtual string value(int index) const
    {
        // lazy evaluation via GetTuneDataValue() because
        // GetTuneData() was returning unexpected error

        _variant_t v;
        _bstr_t bstrLabel(labels_.item(index).c_str());
        HRESULT hr = raw_->GetTuneDataValue(segmentNumber_, bstrLabel, &v);
        if (hr)
            return string(); // ignore errors

        ostringstream oss;

        switch(v.vt)
        {
            case VT_EMPTY:
                break;
            case VT_R8:
                oss << v.dblVal;
                break;
            case VT_BOOL:
                oss << v.boolVal;
                break;
            case VT_I2:
                oss << v.iVal;
                break;
            case VT_BSTR:
            {
                _bstr_t temp(v.bstrVal);
                oss << (const char*)(temp);
                break;
            }
            default:
                oss << "UNHANDLED VT: " << v.vt;
                break;
        }

        return oss.str();
    }

    private:

    VariantStringArray labels_;
    const IXRawfilePtr& raw_;
    long segmentNumber_;

    TuneDataLabelValueArray(TuneDataLabelValueArray&);
    TuneDataLabelValueArray& operator=(TuneDataLabelValueArray&);
};
} // namespace


auto_ptr<LabelValueArray> RawFileImpl::getTuneData(long segmentNumber) const
{
    _variant_t variantLabels;
    long size = 0;

    checkResult(raw_->GetTuneDataLabels(segmentNumber, &variantLabels, &size));

    auto_ptr<TuneDataLabelValueArray> a(
        new TuneDataLabelValueArray(variantLabels, size, raw_, segmentNumber));
    return a;
}

namespace {
class InstrumentMethodLabelValueArray : public LabelValueArray
{
    public:

    InstrumentMethodLabelValueArray(VARIANT& variantLabels, long size, const IXRawfilePtr& raw)
        : labels_(variantLabels, size),
        raw_(raw)
    {}

    virtual int size() const { return labels_.size(); }
    virtual string label(int index) const { return labels_.item(index); }

    virtual string value(int index) const
    {
        // lazy evaluation: non-VARIANT interface to get the values
        _bstr_t bstr;
        HRESULT hr = raw_->GetInstMethod(index, bstr.GetAddress());
        if (hr)
            return string(); // empty value
        return (const char*)(bstr);
    }

    private:

    VariantStringArray labels_;
    const IXRawfilePtr& raw_;

    InstrumentMethodLabelValueArray(InstrumentMethodLabelValueArray&);
    InstrumentMethodLabelValueArray& operator=(InstrumentMethodLabelValueArray&);
};

class InstrumentChannelStringArray : public StringArray
{
    public:

    InstrumentChannelStringArray(const RawFileImpl* rawFile, const IXRawfilePtr& raw)
        : rawFile_(rawFile),
        raw_(raw)
    {
        size_ = rawFile_->value(InstNumChannelLabels);
    }

    virtual int size() const { return size_; }

    virtual string item(int index) const
    {
        _bstr_t bstr;
        HRESULT hr = raw_->GetInstChannelLabel(index, bstr.GetAddress());
        if (hr)
            throw RawEgg("InstrumentChannelStringArray: error");
        return (const char*)(bstr);
    }

    private:
    const RawFileImpl* rawFile_;
    const IXRawfilePtr& raw_;
    int size_;
};
} // namespace

#endif // WIN32


vector<string> RawFileImpl::getInstrumentMethods() const
{
    try
    {
        vector<string> result;

#ifndef _WIN64
        _variant_t variantLabels;
        long size = 0;

        try
        {
            checkResult(raw_->GetInstMethodNames(&size, &variantLabels));
            InstrumentMethodLabelValueArray methods(variantLabels, size, raw_);
            for (int i = 0; i < size; ++i)
                result.push_back(methods.value(i));
        }
        catch (exception&)
        {
            // TODO: log warning?
        }
#else
        for (int i = 0; i < raw_->InstrumentMethodsCount; ++i)
            result.push_back(ToStdString(raw_->GetInstrumentMethod(i)));
#endif
        return result;
    }
    CATCH_AND_FORWARD
}


string RawFileImpl::getInstrumentChannelLabel(long channel) const
{
    try
    {
#ifndef _WIN64
        long size = value(InstNumChannelLabels);
        if (channel >= size)
            throw out_of_range("invalid channel number");

        _bstr_t bstr;
        checkResult(raw_->GetInstChannelLabel(channel, bstr.GetAddress()));
        return (const char*)(bstr);
#else
        return ToStdString(raw_->GetInstrumentData()->ChannelLabels[channel]);
#endif
    }
    CATCH_AND_FORWARD
}


namespace{
class ChromatogramDataImpl : public ChromatogramData
{
    public:

    ChromatogramDataImpl(vector<double>& times, vector<double>& intensities, double startTime, double endTime)
    : startTime_(startTime),
      endTime_(endTime)
    {
        times_.swap(times);
        intensities_.swap(intensities);
    }

    virtual double startTime() const {return startTime_;}
    virtual double endTime() const {return endTime_;}
    virtual long size() const {return times_.size();}
    virtual const std::vector<double>& times() const { return times_; }
    virtual const std::vector<double>& intensities() const { return intensities_; }

    private:
    vector<double> times_, intensities_;
    double startTime_;
    double endTime_;
};

struct PWIZ_API_DECL TimeIntensityPair
{
    double time;
    double intensity;
};
} // namespace


ChromatogramDataPtr
RawFileImpl::getChromatogramData(ChromatogramType traceType,
                                 const string& filter,
                                 double massRangeFrom, double massRangeTo,
                                 double delay,
                                 double startTime,
                                 double endTime) const
{
    try
    {
        vector<double> times, intensities;
#ifndef _WIN64
        _bstr_t bstrFilter(filter.c_str());
        _bstr_t bstrMassRange = massRangeFrom == 0 && massRangeTo == 0 ? "" : (boost::format("%.10g-%.10g", std::locale::classic()) % massRangeFrom % massRangeTo).str().c_str();
        _variant_t variantChromatogramData;
        _variant_t variantPeakFlags;
        long size = 0;

        checkResult(raw_->GetChroData((long) traceType, 0, (long) Type_MassRange,
                                      bstrFilter, bstrMassRange, "",
                                      delay, &startTime, &endTime,
                                      0, 0,
                                      &variantChromatogramData, &variantPeakFlags, &size));
        ManagedSafeArray msa(variantChromatogramData, size);
        TimeIntensityPair* timeIntensityPairs = (TimeIntensityPair*) msa.data();
        size = msa.size();
        times.reserve(size);
        intensities.reserve(size);
        for (size_t i = 0; i < size; ++i)
        {
            times.push_back(timeIntensityPairs[i].time);
            intensities.push_back(timeIntensityPairs[i].intensity);
        }
#else
        auto ranges = gcnew array<Thermo::Range^> { gcnew Thermo::Range(massRangeFrom, massRangeTo) };
        auto settings = gcnew array<Thermo::ChromatogramTraceSettings^> { gcnew Thermo::ChromatogramTraceSettings(ToSystemString(filter), ranges) };
        settings[0]->DelayInMin = delay;
        settings[0]->Trace = (Thermo::TraceType) traceType;

        auto chroData = raw_->GetChromatogramData(settings, raw_->ScanNumberFromRetentionTime(startTime), raw_->ScanNumberFromRetentionTime(endTime));
        ToStdVector(chroData->PositionsArray[0], times);
        ToStdVector(chroData->IntensitiesArray[0], intensities);
#endif
        return ChromatogramDataPtr(new ChromatogramDataImpl(times, intensities, startTime, endTime));
    }
    CATCH_AND_FORWARD
}





#ifdef _WIN64
RawFileThreadImpl::RawFileThreadImpl(const RawFileImpl* raw) : rawFile_(raw)
{
    currentControllerType_ = Controller_None;
    currentControllerNumber_ = 0;

    raw_ = raw->rawManager_->CreateThreadAccessor();
    raw_->IncludeReferenceAndExceptionData = true;
}


long RawFileThreadImpl::getFirstScanNumber() const
{
    return raw_->RunHeader->FirstSpectrum;
}

long RawFileThreadImpl::getLastScanNumber() const
{
    return raw_->RunHeader->LastSpectrum;
}

double RawFileThreadImpl::getFirstScanTime() const
{
    return raw_->RunHeader->StartTime;
}

double RawFileThreadImpl::getLastScanTime() const
{
    return raw_->RunHeader->EndTime;
}


blt::local_date_time RawFileThreadImpl::getCreationDate(bool adjustToHostTime) const
{
    try
    {
        System::DateTime acquisitionTime = raw_->FileHeader->CreationDate;
        bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
            bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));

        if (adjustToHostTime)
        {
            bpt::time_duration tzOffset = bpt::second_clock::universal_time() - bpt::second_clock::local_time();
            return blt::local_date_time(pt + tzOffset, blt::time_zone_ptr()); // treat time as if it came from host's time zone; actual time zone is not provided by Thermo
        }
        else
            return blt::local_date_time(pt, blt::time_zone_ptr());
    }
    CATCH_AND_FORWARD
}


ControllerInfo RawFileThreadImpl::getCurrentController() const
{
    ControllerInfo result;
    result.type = currentControllerType_;
    result.controllerNumber = currentControllerNumber_;
    return result;
}


void RawFileThreadImpl::setCurrentController(ControllerType type, long controllerNumber)
{
    if (currentControllerType_ == type && currentControllerNumber_ == controllerNumber)
        return;

    try
    {
        raw_->SelectInstrument((Thermo::Device) type, controllerNumber);
        currentControllerType_ = type;
        currentControllerNumber_ = controllerNumber;
    }
    CATCH_AND_FORWARD
}


long RawFileThreadImpl::getNumberOfControllersOfType(ControllerType type) const
{
    try
    {
        return raw_->GetInstrumentCountOfType((Thermo::Device) type);
    }
    CATCH_AND_FORWARD
}


ControllerType RawFileThreadImpl::getControllerType(long index) const
{
    try
    {
        return (ControllerType)raw_->GetInstrumentType(index);
    }
    CATCH_AND_FORWARD
}


long RawFileThreadImpl::scanNumber(double rt) const
{
    try
    {
        return raw_->ScanNumberFromRetentionTime(rt);
    }
    CATCH_AND_FORWARD
}


double RawFileThreadImpl::rt(long scanNumber) const
{
    try
    {
        return raw_->RetentionTimeFromScanNumber(scanNumber);
    }
    CATCH_AND_FORWARD
}


InstrumentModelType RawFileThreadImpl::getInstrumentModel() const
{
    return rawFile_->instrumentModel_;
}

InstrumentData RawFileThreadImpl::getInstrumentData() const
{
    try
    {
        InstrumentData result;
        auto source = raw_->GetInstrumentData();
        result.Model = ToStdString(source->Model);
        result.Name = ToStdString(source->Name);
        result.SerialNumber = ToStdString(source->SerialNumber);
        result.SoftwareVersion = ToStdString(source->SoftwareVersion);
        result.HardwareVersion = ToStdString(source->HardwareVersion);
        result.Units = ToStdString(System::Enum::GetName(Thermo::DataUnits::typeid, source->Units));
        ToStdVector(source->ChannelLabels, result.ChannelLabels);
        result.Flags = ToStdString(source->Flags);
        result.AxisLabelX = ToStdString(source->AxisLabelX);
        result.AxisLabelY = ToStdString(source->AxisLabelY);
        return result;
    }
    CATCH_AND_FORWARD
}


const vector<IonizationType>& RawFileThreadImpl::getIonSources() const
{
    return rawFile_->ionSources_;
}


const vector<MassAnalyzerType>& RawFileThreadImpl::getMassAnalyzers() const
{
    return rawFile_->massAnalyzers_;
}


const vector<DetectorType>& RawFileThreadImpl::getDetectors() const
{
    return rawFile_->detectors_;
}


std::string RawFileThreadImpl::getSampleID() const
{
    return ToStdString(raw_->SampleInformation->SampleId);
}


std::string RawFileThreadImpl::getTrailerExtraValue(long scanNumber, const string& name, string valueIfMissing) const
{
    if (currentControllerType_ != Controller_MS)
        return "";

    try
    {
        auto findItr = rawFile_->trailerExtraIndexByName.find(name);
        if (findItr == rawFile_->trailerExtraIndexByName.end())
            return "";

        auto result = raw_->GetTrailerExtraValue(scanNumber, findItr->second);
        return result == nullptr ? "" : ToStdString(result->ToString());
    }
    CATCH_AND_FORWARD_EX(name)
}

double RawFileThreadImpl::getTrailerExtraValueDouble(long scanNumber, const string& name, double valueIfMissing) const
{
    if (currentControllerType_ != Controller_MS)
        return valueIfMissing;

    try
    {
        auto findItr = rawFile_->trailerExtraIndexByName.find(name);
        if (findItr == rawFile_->trailerExtraIndexByName.end())
            return valueIfMissing;
        System::Object^ result = raw_->GetTrailerExtraValue(scanNumber, findItr->second);
        return result == nullptr ? valueIfMissing : System::Convert::ToDouble(result);
    }
    CATCH_AND_FORWARD_EX(name)
}


long RawFileThreadImpl::getTrailerExtraValueLong(long scanNumber, const string& name, long valueIfMissing) const
{
    if (currentControllerType_ != Controller_MS)
        return valueIfMissing;

    try
    {
        auto findItr = rawFile_->trailerExtraIndexByName.find(name);
        if (findItr == rawFile_->trailerExtraIndexByName.end())
            return valueIfMissing;

        System::Object^ result = raw_->GetTrailerExtraValue(scanNumber, findItr->second);
        return result == nullptr ? valueIfMissing : System::Convert::ToInt32(result);
    }
    CATCH_AND_FORWARD_EX(name)
}

MassListPtr RawFileThreadImpl::getMassList(long scanNumber,
    const string& filter,
    CutoffType cutoffType,
    long cutoffValue,
    long maxPeakCount,
    bool centroidResult) const
{
    try
    {
        auto result = boost::make_shared<MassList>();

        if (centroidResult && raw_->GetFilterForScanNumber(scanNumber)->MassAnalyzer == ThermoEnum::MassAnalyzerType::MassAnalyzerFTMS)
        {
            auto centroidStream = raw_->GetCentroidStream(scanNumber, true);
            if (centroidStream != nullptr && centroidStream->Length > 0)
            {
                ToBinaryData(centroidStream->Masses, result->mzArray);
                ToBinaryData(centroidStream->Intensities, result->intensityArray);
                return result;
            }
        }

        if (centroidResult)
        {
            auto scan = Thermo::Scan::FromFile(raw_.get(), scanNumber);
            if (scan->SegmentedScanAccess->Positions->Length == 0 || scan->ScanStatistics->BasePeakIntensity == 0)
                return result;
            auto centroidScan = Thermo::Scan::ToCentroid(scan);
            if (centroidScan == nullptr || centroidScan->SegmentedScanAccess->Positions->Length == 0)
                throw gcnew System::Exception("failed to centroid scan");
            ToBinaryData(centroidScan->SegmentedScanAccess->Positions, result->mzArray);
            ToBinaryData(centroidScan->SegmentedScanAccess->Intensities, result->intensityArray);
        }
        else
        {
            auto segmentedStream = raw_->GetSegmentedScanFromScanNumber(scanNumber, nullptr);
            ToBinaryData(segmentedStream->Positions, result->mzArray);
            ToBinaryData(segmentedStream->Intensities, result->intensityArray);
        }
        return result;
    }
    CATCH_AND_FORWARD
}


std::vector<string> RawFileThreadImpl::getFilters() const
{
    try
    {
        vector<string> result;
        ToStdVector(raw_->GetAutoFilters(), result);
        return result;
    }
    CATCH_AND_FORWARD
}


ScanInfoPtr RawFileThreadImpl::getScanInfo(long scanNumber) const
{
    ScanInfoPtr scanInfo(new ScanInfoImpl(scanNumber, this));
    return scanInfo;
}


MSOrder RawFileThreadImpl::getMSOrder(long scanNumber) const
{
    try
    {
        return (MSOrder)raw_->GetFilterForScanNumber(scanNumber)->MSOrder;
    }
    CATCH_AND_FORWARD
}


double RawFileThreadImpl::getPrecursorMass(long scanNumber, MSOrder msOrder) const
{
    try
    {
        double result = raw_->GetFilterForScanNumber(scanNumber)->GetMass((int)msOrder - 2);
        // raw_->GetFilterForScanNumber(scanNumber)->GetIsolationWidthOffset() ??

        return calculateIsolationMzWithOffset(scanNumber, result);
    }
    CATCH_AND_FORWARD
}

double RawFileThreadImpl::calculateIsolationMzWithOffset(long scanNumber, double isolationMzPossiblyWithOffset) const
{
    return rawFile_->calculateIsolationMzWithOffset(scanNumber, isolationMzPossiblyWithOffset);
}


ScanType RawFileThreadImpl::getScanType(long scanNumber) const
{
    try
    {
        return (ScanType)raw_->GetFilterForScanNumber(scanNumber)->ScanMode;
    }
    CATCH_AND_FORWARD
}


ScanFilterMassAnalyzerType RawFileThreadImpl::getMassAnalyzerType(long scanNumber) const
{
    try
    {
        return (ScanFilterMassAnalyzerType)raw_->GetFilterForScanNumber(scanNumber)->MassAnalyzer;
    }
    CATCH_AND_FORWARD
}


ActivationType RawFileThreadImpl::getActivationType(long scanNumber) const
{
    try
    {
        return (ActivationType)raw_->GetFilterForScanNumber(scanNumber)->GetActivation(0);
    }
    CATCH_AND_FORWARD
}


double RawFileThreadImpl::getIsolationWidth(int scanSegment, int scanEvent) const
{
    if (rawFile_->isolationWidthBySegmentAndScanEvent.count(scanSegment) > 0 &&
        rawFile_->isolationWidthBySegmentAndScanEvent[scanSegment].count(scanEvent) > 0)
        return rawFile_->isolationWidthBySegmentAndScanEvent[scanSegment][scanEvent];
    return 0.0;
}


double RawFileThreadImpl::getDefaultIsolationWidth(int scanSegment, int msLevel) const
{
    if (rawFile_->defaultIsolationWidthBySegmentAndMsLevel.count(scanSegment) > 0 &&
        rawFile_->defaultIsolationWidthBySegmentAndMsLevel[scanSegment].count(msLevel) > 0)
        return rawFile_->defaultIsolationWidthBySegmentAndMsLevel[scanSegment][msLevel];
    return 0.0;
}


ErrorLogItem RawFileThreadImpl::getErrorLogItem(long itemNumber) const
{
    try
    {
        ErrorLogItem result;

        auto logEntry = raw_->GetErrorLogItem(itemNumber);
        result.rt = logEntry->RetentionTime;
        result.errorMessage = ToStdString(logEntry->Message);
        return result;
    }
    CATCH_AND_FORWARD
}



vector<string> RawFileThreadImpl::getInstrumentMethods() const
{
    try
    {
        vector<string> result;

        for (int i = 0; i < raw_->InstrumentMethodsCount; ++i)
            result.push_back(ToStdString(raw_->GetInstrumentMethod(i)));
        return result;
    }
    CATCH_AND_FORWARD
}


string RawFileThreadImpl::getInstrumentChannelLabel(long channel) const
{
    try
    {
        return ToStdString(raw_->GetInstrumentData()->ChannelLabels[channel]);
    }
    CATCH_AND_FORWARD
}


ChromatogramDataPtr
RawFileThreadImpl::getChromatogramData(ChromatogramType traceType,
    const string& filter,
    double massRangeFrom, double massRangeTo,
    double delay,
    double startTime,
    double endTime) const
{
    try
    {
        vector<double> times, intensities;
        auto ranges = gcnew array<Thermo::Range^> { gcnew Thermo::Range(massRangeFrom, massRangeTo) };
        auto settings = gcnew array<Thermo::ChromatogramTraceSettings^> { gcnew Thermo::ChromatogramTraceSettings(ToSystemString(filter), ranges) };
        settings[0]->DelayInMin = delay;
        settings[0]->Trace = (Thermo::TraceType) traceType;

        auto chroData = raw_->GetChromatogramData(settings, raw_->ScanNumberFromRetentionTime(startTime), raw_->ScanNumberFromRetentionTime(endTime));
        ToStdVector(chroData->PositionsArray[0], times);
        ToStdVector(chroData->IntensitiesArray[0], intensities);
        return ChromatogramDataPtr(new ChromatogramDataImpl(times, intensities, startTime, endTime));
    }
    CATCH_AND_FORWARD
}
#endif // WIN64



RawFile* RawFileImpl::getRawByThread(size_t currentThreadId) const
{
#ifdef _WIN64
    Lock lock(rawManager_);
    auto lb = rawByThread_.lower_bound(currentThreadId);
    if (lb != rawByThread_.end() && lb->first == currentThreadId)
    {
        //cout << endl << "Using existing accessor for thread " << currentThreadId << endl;
        return reinterpret_cast<RawFile*>(lb->second.get());
    }
    else
    {
        //cout << endl << "Creating new accessor for thread " << currentThreadId << endl;
        auto insertPair = rawByThread_.insert(lb, make_pair(currentThreadId, boost::make_shared<RawFileThreadImpl>(this)));
        return reinterpret_cast<RawFile*>(insertPair->second.get());
    }
#else
    return const_cast<RawFileImpl*>(this);
#endif
}



#pragma unmanaged
PWIZ_API_DECL RawFilePtr RawFile::create(const string& filename)
{
    return RawFilePtr(new RawFileImpl(filename));
}
