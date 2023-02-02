//
// $Id$
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include "RawFileTypes.h"
#include <string>
#include <memory>
#include <stdexcept>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace Thermo {


using boost::shared_ptr;


class PWIZ_API_DECL RawEgg : public std::runtime_error
{
    public:

    RawEgg(const std::string& error)
        : std::runtime_error(("[ThermoRawFile] " + error).c_str())
    {}
};


enum PWIZ_API_DECL ControllerType
{
    Controller_None = -1,
    Controller_MS = 0,
    Controller_Analog,
    Controller_ADCard,
#ifdef _WIN64
    Controller_UV,
    Controller_PDA,
    Controller_Other,
#else
    Controller_PDA,
    Controller_UV,
#endif
    Controller_Count
};


extern const char* ControllerTypeStrings[];


struct PWIZ_API_DECL ControllerInfo
{
    ControllerType type;
    long controllerNumber;
};


enum PWIZ_API_DECL ValueID_String
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


enum PWIZ_API_DECL ValueID_Long
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


enum PWIZ_API_DECL ValueID_Double
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


class PWIZ_API_DECL StringArray
{
    public:
    virtual int size() const = 0;
    virtual std::string item(int index) const = 0;
    virtual ~StringArray(){}
};


class PWIZ_API_DECL LabelValueArray
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


enum PWIZ_API_DECL CutoffType
{
    Cutoff_None = 0,
    Cutoff_Absolute,
    Cutoff_Relative
};


struct PWIZ_API_DECL MassList
{
    pwiz::util::BinaryData<double> mzArray;
    pwiz::util::BinaryData<double> intensityArray;
    size_t size() const { return mzArray.size(); }
};


typedef shared_ptr<MassList> MassListPtr;


struct PWIZ_API_DECL MassRange
{
    double low;
    double high;
};


typedef shared_ptr<MassRange> MassRangePtr;


struct PWIZ_API_DECL PrecursorInfo
{
    int msLevel;
    double monoisotopicMZ;
    double isolationMZ;
    double isolationWidth;
    double activationEnergy;
    ActivationType activationType;
    int chargeState;
    int scanNumber;
};


class PWIZ_API_DECL ScanInfo
{
    public:
    virtual void reinitialize(const std::string& filterString) = 0;

    virtual long scanNumber() const = 0;
    virtual int scanSegmentNumber() const = 0;
    virtual int scanEventNumber() const = 0;

    // info contained in filter string
    virtual std::string filter() const = 0;
    virtual MassAnalyzerType massAnalyzerType() const = 0;
    virtual IonizationType ionizationType() const = 0;
    virtual ActivationType activationType() const = 0;
    virtual long msLevel() const = 0;
    virtual ScanType scanType() const = 0;
    virtual PolarityType polarityType() const = 0;
    virtual bool isEnhanced() const = 0;
    virtual bool isDependent() const = 0;
    virtual bool hasMultiplePrecursors() const = 0;
    virtual bool isSPS() const = 0;
    virtual bool hasLockMass() const = 0;
    virtual bool isWideband() const = 0;
    virtual bool isTurboScan() const = 0;
    virtual bool isPhotoIonization() const = 0;
    virtual bool isCorona() const = 0;
    virtual bool isDetectorSet() const = 0;
    virtual bool isSourceCID() const = 0;
    virtual AccurateMassType accurateMassType() const = 0;


    virtual const std::vector<PrecursorInfo>& precursorInfo() const = 0;
    virtual long precursorCount() const = 0;
    virtual long precursorCharge() const = 0;
    virtual double precursorMZ(long index, bool preferMonoisotope = true) const = 0;
    virtual double precursorActivationEnergy(long index) const = 0;

    virtual std::vector<double> getIsolationWidths() const = 0;

    virtual ActivationType supplementalActivationType() const = 0;
    virtual double supplementalActivationEnergy() const = 0;

    // "parent" synonym is deprecated
    virtual long parentCount() const = 0;
    virtual long parentCharge() const = 0;
    virtual double parentMass(long index, bool preferMonoisotope = true) const = 0;
    virtual double parentEnergy(long index) const = 0;

    // scan ranges parsed from filter
    virtual size_t scanRangeCount() const = 0;
    virtual const std::pair<double, double>& scanRange(size_t index) const = 0;

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
    virtual double frequency() const = 0;
    virtual bool FAIMSOn() const = 0;
    virtual double compensationVoltage() const = 0;

    virtual bool isConstantNeutralLoss() const = 0;
    virtual double analyzerScanOffset() const = 0;

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

typedef shared_ptr<ScanInfo> ScanInfoPtr;


struct PWIZ_API_DECL ErrorLogItem
{
    double rt;
    std::string errorMessage;
};


enum PWIZ_API_DECL ChromatogramType
{
    Type_MassRange,
    Type_TIC,
    Type_BasePeak,
    Type_NeutralFragment,
#ifndef _WIN64
    Type_TotalScan = Type_TIC,
    Type_ECD = Type_MassRange
#else
    Type_ECD = 31, // TraceType.ChannelA
    Type_TotalScan = 22 // TraceType.TotalAbsorbance
#endif
};


class PWIZ_API_DECL ChromatogramData
{
    public:
    virtual double startTime() const = 0;
    virtual double endTime() const = 0;
    virtual long size() const = 0;
    virtual const std::vector<double>& times() const = 0;
    virtual const std::vector<double>& intensities() const = 0;
    virtual ~ChromatogramData(){}
};

typedef shared_ptr<ChromatogramData> ChromatogramDataPtr;

struct PWIZ_API_DECL InstrumentData
{
    // Gets the name of the instrument
    std::string Name;

    // Gets the model of the instrument
    std::string Model;

    // Gets the serial number of the instrument
    std::string SerialNumber;

    // Gets the software version of the instrument
    std::string SoftwareVersion;

    // Gets the hardware version of the instrument
    std::string HardwareVersion;

    // Gets the names of the channels, for UV or analog data.
    std::vector<std::string> ChannelLabels;

    // Gets the units of the Signal, for UV or analog
    std::string Units;

    // Gets the flags. The purpose of this field is to contain flags separated by ';'
    // that denote experiment information, etc. For example, if a file is acquired under
    // instrument control based on an experiment protocol like an ion mapping experiment,
    // an appropriate flag can be set here. Legacy LCQ MS flags: 1. TIM - total ion
    // map 2. NLM - neutral loss map 3. PIM - parent ion map 4. DDZMAP - data dependent
    // zoom map
    std::string Flags;

    // Device suggested label of X axis
    std::string AxisLabelX;

    // Device suggested label of Y axis (name for units of data, such as "°C")
    std::string AxisLabelY;
};

class PWIZ_API_DECL RawFile
{
    public:

    static shared_ptr<RawFile> create(const std::string& filename);

    // on 64-bit (RawFileReader), returns a thread-specific accessor to avoid the need for locking
    virtual RawFile* getRawByThread(size_t currentThreadId) const = 0;

    virtual std::string getFilename() const = 0;
    virtual boost::local_time::local_date_time getCreationDate(bool adjustToHostTime = true) const = 0;

    virtual ControllerInfo getCurrentController() const = 0;
    virtual void setCurrentController(ControllerType type, long controllerNumber) = 0;
    virtual long getNumberOfControllersOfType(ControllerType type) const = 0;
    virtual ControllerType getControllerType(long index) const = 0;

    virtual long scanNumber(double rt) const = 0;
    virtual double rt(long scanNumber) const = 0;
    
    virtual long getFirstScanNumber() const = 0;
    virtual long getLastScanNumber() const = 0;
    virtual double getFirstScanTime() const = 0;
    virtual double getLastScanTime() const = 0;

    virtual MassListPtr
    getMassList(long scanNumber,
                const std::string& filter,
                CutoffType cutoffType,
                long cutoffValue,
                long maxPeakCount,
                bool centroidResult) const = 0;

    virtual std::vector<std::string> getFilters() const = 0;
    virtual ScanInfoPtr getScanInfo(long scanNumber) const = 0;
    static ScanInfoPtr getScanInfoFromFilterString(const std::string& filterString);

    virtual MSOrder getMSOrder(long scanNumber) const = 0;
    virtual double getPrecursorMass(long scanNumber, MSOrder msOrder = MSOrder_Any) const = 0;
    virtual ScanType getScanType(long scanNumber) const = 0;
    virtual ScanFilterMassAnalyzerType getMassAnalyzerType(long scanNumber) const = 0;
    virtual ActivationType getActivationType(long scanNumber) const = 0;
    // getDetectorType is obsolete?
    virtual double getIsolationWidth(int scanSegment, int scanEvent) const = 0;
    virtual double getDefaultIsolationWidth(int scanSegment, int msLevel)const = 0;
    virtual double calculateIsolationMzWithOffset(long scanNumber, double isolationMzPossiblyWithOffset) const = 0;

    virtual ErrorLogItem getErrorLogItem(long itemNumber) const = 0;
    virtual std::vector<std::string> getInstrumentMethods() const = 0;
    virtual std::string getInstrumentChannelLabel(long channel) const = 0;

    virtual InstrumentModelType getInstrumentModel() const = 0;
    virtual InstrumentData getInstrumentData() const = 0;
    virtual const std::vector<IonizationType>& getIonSources() const = 0;
    virtual const std::vector<MassAnalyzerType>& getMassAnalyzers() const = 0;
    virtual const std::vector<DetectorType>& getDetectors() const = 0;

    virtual std::string getSampleID() const = 0;
    virtual std::string getTrailerExtraValue(long scanNumber, const std::string& name, std::string valueIfMissing = "") const = 0;
    virtual double getTrailerExtraValueDouble(long scanNumber, const std::string& name, double valueIfMissing = 0) const = 0;
    virtual long getTrailerExtraValueLong(long scanNumber, const std::string& name, long valueIfMissing = 0) const = 0;

    virtual ChromatogramDataPtr
    getChromatogramData(ChromatogramType traceType,
                        const std::string& filter,
                        double massRangeFrom, double massRangeTo,
                        double delay,
                        double startTime,
                        double endTime) const = 0;

    virtual ~RawFile(){}
};

typedef shared_ptr<RawFile> RawFilePtr;


} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz


#endif // _RAWFILE_H_
