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
#include "ScanFilter.h"
#include <string>
#include <memory>
#include <stdexcept>
#include "boost/shared_ptr.hpp"
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
    Controller_PDA,
    Controller_UV
};


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


enum PWIZ_API_DECL WhichMassList
{
    MassList_Current,
    MassList_Previous,
    MassList_Next
};


struct PWIZ_API_DECL MassIntensityPair
{
    double mass;
    double intensity;
};


class PWIZ_API_DECL MassList
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


typedef shared_ptr<MassList> MassListPtr;


struct PWIZ_API_DECL MassRange
{
    double low;
    double high;
};


typedef shared_ptr<MassRange> MassRangePtr;


struct PWIZ_API_DECL PrecursorInfo
{
    double monoisotopicMZ;
    double isolationMZ;
    int chargeState;
    int scanNumber;
};


class PWIZ_API_DECL ScanInfo
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
    virtual bool isEnhanced() const = 0;
    virtual bool isDependent() const = 0;
    virtual bool hasMultiplePrecursors() const = 0;

    virtual std::vector<PrecursorInfo> precursorInfo() const = 0;
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
    virtual bool FAIMSOn() const = 0;
    virtual double CompensationVoltage() const = 0;

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


class PWIZ_API_DECL ScanEvent
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

typedef shared_ptr<ScanEvent> ScanEventPtr;


struct PWIZ_API_DECL ErrorLogItem
{
    double rt;
    std::string errorMessage;
};


enum PWIZ_API_DECL ChromatogramType
{
    Type_MassRange,
    Type_ECD = Type_MassRange,
    Type_TIC,
    Type_TotalScan = Type_TIC,
    Type_BasePeak,
    Type_NeutralFragment
};


enum PWIZ_API_DECL ChromatogramOperatorType
{
    Operator_None,
    Operator_Minus,
    Operator_Plus
};


enum PWIZ_API_DECL ChromatogramSmoothingType
{
    Smoothing_None,
    Smoothing_Boxcar,
    Smoothing_Gaussian
};


struct PWIZ_API_DECL TimeIntensityPair
{
    double time;
    double intensity;
};


class PWIZ_API_DECL ChromatogramData
{
    public:
    virtual double startTime() const = 0;
    virtual double endTime() const = 0;
    virtual long size() const = 0;
    virtual TimeIntensityPair* data() const = 0;
    virtual ~ChromatogramData(){}
};

typedef shared_ptr<ChromatogramData> ChromatogramDataPtr;


class PWIZ_API_DECL RawFile
{
    public:

    static shared_ptr<RawFile> create(const std::string& filename);

    virtual std::string name(ValueID_Long id) = 0;
    virtual std::string name(ValueID_Double id) = 0;
    virtual std::string name(ValueID_String id) = 0;
    virtual long value(ValueID_Long id) = 0;
    virtual double value(ValueID_Double id) = 0;
    virtual std::string value(ValueID_String id) = 0;

    virtual std::string getFilename() = 0;
    virtual boost::local_time::local_date_time getCreationDate() = 0;
    virtual std::auto_ptr<LabelValueArray> getSequenceRowUserInfo() = 0;

    virtual ControllerInfo getCurrentController() = 0;
    virtual void setCurrentController(ControllerType type, long controllerNumber) = 0;
    virtual long getNumberOfControllersOfType(ControllerType type) = 0;
    virtual ControllerType getControllerType(long index) = 0;

    virtual ScanEventPtr getScanEvent(long index) = 0;

    virtual long scanNumber(double rt) = 0;
    virtual double rt(long scanNumber) = 0;

    virtual MassListPtr
    getMassList(long scanNumber,
                const std::string& filter,
                CutoffType cutoffType,
                long cutoffValue,
                long maxPeakCount,
                bool centroidResult,
                WhichMassList which = MassList_Current,
                const MassRangePtr massRange = MassRangePtr()) = 0;

    virtual MassListPtr
    getAverageMassList(long firstAvgScanNumber, long lastAvgScanNumber,
                       long firstBkg1ScanNumber, long lastBkg1ScanNumber,
                       long firstBkg2ScanNumber, long lastBkg2ScanNumber,
                       const std::string& filter,
                       CutoffType cutoffType,
                       long cutoffValue,
                       long maxPeakCount,
                       bool centroidResult) = 0;

    /// use label data to get centroids for FTMS scans
    virtual MassListPtr getMassListFromLabelData(long scanNumber) = 0;

    virtual std::auto_ptr<StringArray> getFilters() = 0;
    virtual ScanInfoPtr getScanInfo(long scanNumber) = 0;

    virtual MSOrder getMSOrder(long scanNumber) = 0;
    virtual double getPrecursorMass(long scanNumber, MSOrder msOrder = MSOrder_Any) = 0;
    virtual ScanType getScanType(long scanNumber) = 0;
    virtual ScanFilterMassAnalyzerType getMassAnalyzerType(long scanNumber) = 0;
    virtual ActivationType getActivationType(long scanNumber) = 0;
    // getDetectorType is obsolete?
    virtual std::vector<double> getIsolationWidths(long scanNumber) = 0;
    virtual double getIsolationWidth(int scanSegment, int scanEvent) = 0;
    virtual double getDefaultIsolationWidth(int scanSegment, int msLevel) = 0;

    virtual ErrorLogItem getErrorLogItem(long itemNumber) = 0;
    virtual std::auto_ptr<LabelValueArray> getTuneData(long segmentNumber) = 0;
    virtual std::auto_ptr<LabelValueArray> getInstrumentMethods() = 0;
    virtual std::auto_ptr<StringArray> getInstrumentChannelLabels() = 0;

    virtual InstrumentModelType getInstrumentModel() = 0;
    virtual const std::vector<IonizationType>& getIonSources() = 0;
    virtual const std::vector<MassAnalyzerType>& getMassAnalyzers() = 0;
    virtual const std::vector<DetectorType>& getDetectors() = 0;

    virtual ChromatogramDataPtr
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

typedef shared_ptr<RawFile> RawFilePtr;


} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz


#endif // _RAWFILE_H_
