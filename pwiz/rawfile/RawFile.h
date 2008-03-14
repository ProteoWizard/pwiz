//
// RawFile.h
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _RAWFILE_H_
#define _RAWFILE_H_


/*
#ifdef RAWFILE_EXPORTS
#define RAWFILE_API __declspec(dllexport)
#else
#define RAWFILE_API __declspec(dllimport)
#endif
*/


#define RAWFILE_API


#include <string>
#include <memory>
#include <stdexcept>


namespace pwiz {
namespace raw {


class RAWFILE_API RawFileLibrary
{
    public:
    RawFileLibrary();
    ~RawFileLibrary();
};


class RAWFILE_API RawEgg : public std::runtime_error // Eggception class
{
    public:

    RawEgg(const std::string& error)
    :   error_(error),
        std::runtime_error(("[RawEgg] " + error).c_str())
    {}

    virtual const std::string& error() const {return error_;}
    virtual ~RawEgg() throw() {}

    private:
    std::string error_;
};


enum ControllerType
{
    Controller_None = -1,
    Controller_MS = 0,
    Controller_Analog,
    Controller_ADCard,
    Controller_PDA,
    Controller_UV
};


struct ControllerInfo
{
    ControllerType type;
    long controllerNumber;
};


enum ValueID_String
{
    FileName,
    CreatorID,
    ErrorMessage,
    WarningMessage,
    SeqRowDataPath,
    SeqRowRawFileName,
    SeqRowSampleName,
    SeqRowSampleID,
    SeqRowComment,
    SeqRowLevelName,
    SeqRowInstrumentMethod,
    SeqRowProcessingMethod,
    SeqRowCalibrationFile,
    SeqRowVial,
    Flags,
    AcquisitionFileName,
    InstrumentDescription,
    AcquisitionDate,
    Operator,
    Comment1,
    Comment2,
    SampleAmountUnits,
    InjectionAmountUnits,
    SampleVolumeUnits,
    InstName,
    InstModel,
    InstSerialNumber,
    InstSoftwareVersion,
    InstHardwareVersion,
    InstFlags,
    ValueID_String_Count
};


enum ValueID_Long
{
    VersionNumber,
    IsError,
    IsNewFile,
    ErrorCode,
    SeqRowNumber,
    SeqRowSampleType,
    InAcquisition,
    NumberOfControllers,
    NumSpectra,
    NumStatusLog,
    NumErrorLog,
    NumTuneData,
    NumTrailerExtra,
    MaxIntensity,
    FirstSpectrumNumber,
    LastSpectrumNumber,
    InstrumentID,
    InletID,
    ErrorFlag,
    VialNumber,
    NumInstMethods,
    InstNumChannelLabels,
    IsThereMSData,
    HasExpMethod,
    FilterMassPrecision,
    ValueID_Long_Count
};


enum ValueID_Double
{
    SeqRowInjectionVolume,
    SeqRowSampleWeight,
    SeqRowSampleVolume,
    SeqRowISTDAmount,
    SeqRowDilutionFactor,
    MassResolution,
    ExpectedRunTime,
    LowMass,
    HighMass,
    StartTime,
    EndTime,
    MaxIntegratedIntensity,
    SampleVolume,
    SampleWeight,
    InjectionVolume,
    ValueID_Double_Count
};


class StringArray
{
    public:
    virtual int size() const = 0;
    virtual std::string item(int index) const = 0;
    virtual ~StringArray(){}
};


class LabelValueArray
{
    public:
    virtual int size() const = 0;
    virtual std::string label(int index) const = 0;
    virtual std::string value(int index) const = 0;
    virtual ~LabelValueArray(){}
};

// Note on LabelValueArray:
//
// If we want to do anything serious with these values,
// we should return the actual type, rather than string.
// e.g. overload value():
//    virtual string value(int index, long& result) const = 0;
// or define a Type enum.


enum CutoffType
{
    Cutoff_None = 0,
    Cutoff_Absolute,
    Cutoff_Relative
};


enum WhichMassList
{
    MassList_Current,
    MassList_Previous,
    MassList_Next
};


struct MassIntensityPair
{
    double mass;
    double intensity;
};


class MassList
{
    public:
    virtual long scanNumber() const = 0;
    virtual long size() const = 0;
    virtual MassIntensityPair* data() const = 0;
    virtual double centroidPeakWidth() const = 0;

    virtual long firstAvgScanNumber() const = 0;
    virtual long lastAvgScanNumber() const = 0;
    virtual long firstBkg1ScanNumber() const = 0;
    virtual long lastBkg1ScanNumber() const = 0;
    virtual long firstBkg2ScanNumber() const = 0;
    virtual long lastBkg2ScanNumber() const = 0;

    virtual ~MassList(){}
};


enum MassAnalyzerType
{
    MassAnalyzerType_Unknown = -1,
    MassAnalyzerType_ITMS = 0,
    MassAnalyzerType_FTMS,
    MassAnalyzerType_Count
};


inline std::string toString(MassAnalyzerType type)
{
    switch (type)
    {
        case MassAnalyzerType_ITMS: return "ITMS";
        case MassAnalyzerType_FTMS: return "FTMS";
        case MassAnalyzerType_Unknown: default: return "Unknown";
    }
}


enum ScanType
{
    ScanType_Unknown = -1,
    ScanType_Full = 0,
    ScanType_Zoom
};


inline std::string toString(ScanType type)
{
    switch (type)
    {
        case ScanType_Full: return "Full";
        case ScanType_Zoom: return "Zoom";
        case ScanType_Unknown: default: return "Unknown";
    }
}


enum PolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Positive = 0,
    PolarityType_Negative
};


inline std::string toString(PolarityType type)
{
    switch (type)
    {
        case PolarityType_Positive: return "+";
        case PolarityType_Negative: return "-";
        case PolarityType_Unknown: default: return "Unknown";
    }
}


class ScanInfo
{
    public:

    virtual long scanNumber() const = 0;

    // info contained in filter string
    virtual std::string filter() const = 0;
    virtual MassAnalyzerType massAnalyzerType() const = 0;
    virtual long msLevel() const = 0;
    virtual ScanType scanType() const = 0;
    virtual PolarityType polarityType() const = 0;
    virtual long parentCount() const = 0;
    virtual double parentMass(long index) const = 0;
    virtual double parentEnergy(long index) const = 0;

    // other scan info 
    virtual bool isProfileScan() const = 0;
    virtual bool isCentroidScan() const = 0;
    virtual long packetCount() const = 0;
    virtual double startTime() const = 0;
    virtual double lowMass() const = 0;
    virtual double highMass() const = 0;
    virtual double totalIonCurrent() const = 0;
    virtual double basePeakMass() const = 0;
    virtual double basePeakIntensity() const = 0;
    virtual long channelCount() const = 0;
    virtual bool isUniformTime() const = 0;
    virtual double frequency() const = 0;

    virtual long statusLogSize() const = 0;
    virtual double statusLogRT() const = 0;
    virtual std::string statusLogLabel(long index) const = 0;
    virtual std::string statusLogValue(long index) const = 0;

    virtual long trailerExtraSize() const = 0;
    virtual std::string trailerExtraLabel(long index) const = 0;
    virtual std::string trailerExtraValue(long index) const = 0;
    virtual std::string trailerExtraValue(const std::string& name) const = 0;
    virtual double trailerExtraValueDouble(const std::string& name) const = 0;
    virtual long trailerExtraValueLong(const std::string& name) const = 0;

    virtual ~ScanInfo(){}
};


struct ErrorLogItem
{
    double rt;
    std::string errorMessage;
};


enum ChromatogramOperatorType
{
    Operator_None,
    Operator_Minus,
    Operator_Plus
};


enum ChromatogramSmoothingType
{
    Smoothing_None,
    Smoothing_Boxcar,
    Smoothing_Gaussian
};


struct TimeIntensityPair
{
    double time;
    double intensity;
};


class ChromatogramData
{
    public:
    virtual double startTime() const = 0;
    virtual double endTime() const = 0;
    virtual long size() const = 0;
    virtual TimeIntensityPair* data() const = 0;
    virtual ~ChromatogramData(){}
};


class RAWFILE_API RawFile
{
    public:

    static std::auto_ptr<RawFile> create(const std::string& filename);

    virtual std::string name(ValueID_Long id) = 0;
    virtual std::string name(ValueID_Double id) = 0;
    virtual std::string name(ValueID_String id) = 0;
    virtual long value(ValueID_Long id) = 0;
    virtual double value(ValueID_Double id) = 0;
    virtual std::string value(ValueID_String id) = 0;

    virtual std::string getCreationDate() = 0;
    virtual std::auto_ptr<LabelValueArray> getSequenceRowUserInfo() = 0;

    virtual ControllerInfo getCurrentController() = 0;
    virtual void setCurrentController(ControllerType type, long controllerNumber) = 0;
    virtual long getNumberOfControllersOfType(ControllerType type) = 0;
    virtual ControllerType getControllerType(long index) = 0;

    virtual long scanNumber(double rt) = 0;
    virtual double rt(long scanNumber) = 0;

    virtual std::auto_ptr<MassList>
    getMassList(long scanNumber,
                const std::string& filter,
                CutoffType cutoffType,
                long cutoffValue,
                long maxPeakCount,
                bool centroidResult,
                WhichMassList which = MassList_Current) = 0;

    virtual std::auto_ptr<MassList>
    getAverageMassList(long firstAvgScanNumber, long lastAvgScanNumber,
                       long firstBkg1ScanNumber, long lastBkg1ScanNumber,
                       long firstBkg2ScanNumber, long lastBkg2ScanNumber,
                       const std::string& filter,
                       CutoffType cutoffType,
                       long cutoffValue,
                       long maxPeakCount,
                       bool centroidResult) = 0;

    virtual std::auto_ptr<StringArray> getFilters() = 0;
    virtual std::auto_ptr<ScanInfo> getScanInfo(long scanNumber) = 0;
    virtual long getMSLevel(long scanNumber) = 0;
    virtual ErrorLogItem getErrorLogItem(long itemNumber) = 0;
    virtual std::auto_ptr<LabelValueArray> getTuneData(long segmentNumber) = 0;
    virtual std::auto_ptr<LabelValueArray> getInstrumentMethods() = 0;
    virtual std::auto_ptr<StringArray> getInstrumentChannelLabels() = 0;

    virtual std::auto_ptr<ChromatogramData>
    getChromatogramData(long type1,
                        ChromatogramOperatorType op,
                        long type2,
                        const std::string& filter,
                        const std::string& massRanges1,
                        const std::string& massRanges2,
                        double delay,
                        double startTime,
                        double endTime,
                        ChromatogramSmoothingType smoothingType,
                        long smoothingValue) = 0;

    virtual ~RawFile(){}
};


template<typename T>
class auto_handle
{
    public:

    template <typename Parameter>
    auto_handle(Parameter parameter)
    :   t_(T::create(parameter).release())
    {}

    ~auto_handle() {delete t_;}

    T* operator->() {return t_;}

    private:
    T* t_;
    auto_handle(auto_handle&);
    auto_handle& operator=(auto_handle&);
};


typedef auto_handle<RawFile> RawFilePtr;


} // namespace raw
} // namespace pwiz


#endif // _RAWFILE_H_
