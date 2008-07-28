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

#ifdef RAWFILE_DYN_LINK
#ifdef RAWFILE_SOURCE
#define RAWFILE_API __declspec(dllexport)
#else
#define RAWFILE_API __declspec(dllimport)
#endif  // RAWFILE_SOURCE
#endif  // RAWFILE_DYN_LINK

// if RAWFILE_API isn't defined yet define it now:
#ifndef RAWFILE_API
#define RAWFILE_API
#endif

#include "ScanFilter.h"
#include <string>
#include <memory>
#include <stdexcept>
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace raw {


class RAWFILE_API RawFileLibrary
{
    public:
    RawFileLibrary();
    ~RawFileLibrary();
};


class RawEgg : public std::runtime_error // Eggception class
{
    public:

    RawEgg(const std::string& error)
    :   std::runtime_error(("[RawEgg] " + error).c_str()),
		error_(error)        
    {}

    virtual const std::string& error() const {return error_;}
    virtual ~RawEgg() throw() {}

    private:
    std::string error_;
};


enum RAWFILE_API ControllerType
{
    Controller_None = -1,
    Controller_MS = 0,
    Controller_Analog,
    Controller_ADCard,
    Controller_PDA,
    Controller_UV
};


struct RAWFILE_API ControllerInfo
{
    ControllerType type;
    long controllerNumber;
};


enum RAWFILE_API ValueID_String
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


enum RAWFILE_API ValueID_Long
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


enum RAWFILE_API ValueID_Double
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


class RAWFILE_API StringArray
{
    public:
    virtual int size() const = 0;
    virtual std::string item(int index) const = 0;
    virtual ~StringArray(){}
};


class RAWFILE_API LabelValueArray
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


enum RAWFILE_API CutoffType
{
    Cutoff_None = 0,
    Cutoff_Absolute,
    Cutoff_Relative
};


enum RAWFILE_API WhichMassList
{
    MassList_Current,
    MassList_Previous,
    MassList_Next
};


struct RAWFILE_API MassIntensityPair
{
    double mass;
    double intensity;
};


class RAWFILE_API MassList
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


struct RAWFILE_API MassRange
{
    double low;
    double high;
};


class RAWFILE_API ScanInfo
{
    public:

    virtual long scanNumber() const = 0;

    // info contained in filter string
    virtual std::string filter() const = 0;
    virtual MassAnalyzerType massAnalyzerType() const = 0;
    virtual IonizationType ionizationType() const = 0;
    virtual ActivationType activationType() const = 0;
    virtual long msLevel() const = 0;
    virtual ScanType scanType() const = 0;
    virtual PolarityType polarityType() const = 0;

    virtual long precursorCount() const = 0;
    virtual long precursorCharge() const = 0;
    virtual double precursorMZ(long index, bool preferMonoisotope = true) const = 0;
    virtual double precursorActivationEnergy(long index) const = 0;

    // "parent" synonym is deprecated
    virtual long parentCount() const = 0;
    virtual long parentCharge() const = 0;
    virtual double parentMass(long index, bool preferMonoisotope = true) const = 0;
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


class RAWFILE_API ScanEvent
{
    public:

    //virtual MassAnalyzerType massAnalyzerType() const = 0;
    virtual IonizationType ionizationType() const = 0;
    //virtual ActivationType activationType() const = 0;
    virtual ScanType scanType() const = 0;
    virtual PolarityType polarityType() const = 0;

    virtual const std::vector<MassRange>& massRanges() const = 0;

    virtual ~ScanEvent(){};

    /*long bIsValid;
    enum MS_ScanData eScanData;
    enum MS_Polarity ePolarity;
    enum MS_MSOrder eMSOrder;
    enum MS_Dep eDependent;
    enum MS_Wideband eWideband;
    long bCustom;
    enum MS_SourceCID eSourceCID;
    enum MS_ScanType eScanType;
    enum MS_TurboScan eTurboScan;
    enum MS_IonizationMode eIonizationMode;
    enum MS_Corona eCorona;
    enum MS_Detector eDetector;
    double dDetectorValue;
    enum MS_SourceCIDType eSourceCIDType;
    long nlScanTypeIndex;
    long nNumMassRanges;
    struct MS_MassRange arrMassRanges[50];
    long nNumPrecursorMasses;
    double arrPrecursorMasses[10];
    double arrPrecursorEnergies[10];
    long arrPrecursorEnergiesValid[10];
    long nNumSourceFragmentationEnergies;
    double arrSourceFragmentationEnergies[50];
    long arrSourceFragmentationEnergiesValid[50];*/
};

typedef boost::shared_ptr<ScanEvent> ScanEventPtr;


struct RAWFILE_API ErrorLogItem
{
    double rt;
    std::string errorMessage;
};


enum RAWFILE_API ChromatogramType
{
    Type_MassRange,
    Type_TIC,
    Type_BasePeak,
    Type_NeutralFragment
};


enum RAWFILE_API ChromatogramOperatorType
{
    Operator_None,
    Operator_Minus,
    Operator_Plus
};


enum RAWFILE_API ChromatogramSmoothingType
{
    Smoothing_None,
    Smoothing_Boxcar,
    Smoothing_Gaussian
};


struct RAWFILE_API TimeIntensityPair
{
    double time;
    double intensity;
};


class RAWFILE_API ChromatogramData
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

    virtual std::string getFilename() = 0;
    virtual std::string getCreationDate() = 0;
    virtual std::auto_ptr<LabelValueArray> getSequenceRowUserInfo() = 0;

    virtual ControllerInfo getCurrentController() = 0;
    virtual void setCurrentController(ControllerType type, long controllerNumber) = 0;
    virtual long getNumberOfControllersOfType(ControllerType type) = 0;
    virtual ControllerType getControllerType(long index) = 0;

    virtual ScanEventPtr getScanEvent(long index) = 0;

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

    virtual InstrumentModelType getInstrumentModel() = 0;
    virtual const std::vector<IonizationType>& getIonSources() = 0;
    virtual const std::vector<MassAnalyzerType>& getMassAnalyzers() = 0;
    virtual const std::vector<DetectorType>& getDetectors() = 0;

    virtual std::auto_ptr<ChromatogramData>
    getChromatogramData(ChromatogramType type1,
                        ChromatogramOperatorType op,
                        ChromatogramType type2,
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
