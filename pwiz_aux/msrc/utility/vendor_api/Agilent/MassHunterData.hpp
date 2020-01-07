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


#ifndef _MASSHUNTERDATA_HPP_
#define _MASSHUNTERDATA_HPP_


#ifndef BOOST_DATE_TIME_NO_LIB
#define BOOST_DATE_TIME_NO_LIB // prevent MSVC auto-link
#endif


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/utility/misc/BinaryData.hpp"
#include "pwiz/utility/chemistry/MzMobilityWindow.hpp"
#include <string>
#include <vector>
#include <set>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace Agilent {


PWIZ_API_DECL enum DeviceType
{
    DeviceType_Unknown = 0,
    DeviceType_Mixed = 1,
    DeviceType_Quadrupole = 2,
    DeviceType_IonTrap = 3,
    DeviceType_TimeOfFlight = 4,
    DeviceType_TandemQuadrupole = 5,
    DeviceType_QuadrupoleTimeOfFlight = 6,
    DeviceType_FlameIonizationDetector = 10,
    DeviceType_ThermalConductivityDetector = 11,
    DeviceType_RefractiveIndexDetector = 12,
    DeviceType_MultiWavelengthDetector = 13,
    DeviceType_DiodeArrayDetector = 14,
    DeviceType_VariableWavelengthDetector = 15,
    DeviceType_AnalogDigitalConverter = 16,
    DeviceType_ElectronCaptureDetector = 17,
    DeviceType_FluorescenceDetector = 18,
    DeviceType_EvaporativeLightScatteringDetector = 19,
    DeviceType_ALS = 20,
    DeviceType_WellPlateSampler = 21,
    DeviceType_MicroWellPlateSampler = 22,
    DeviceType_CTC = 23,
    DeviceType_IsocraticPump = 30,
    DeviceType_BinaryPump = 31,
    DeviceType_QuaternaryPump = 32,
    DeviceType_CapillaryPump = 33,
    DeviceType_NanoPump = 34,
    DeviceType_ThermostattedColumnCompartment = 40,
    DeviceType_ChipCube = 41,
    DeviceType_CANValves = 42
};

PWIZ_API_DECL enum IonizationMode
{
    IonizationMode_Unspecified = 0,
    IonizationMode_Mixed = 1,
    IonizationMode_EI = 2,
    IonizationMode_CI = 4,
    IonizationMode_Maldi = 8,
    IonizationMode_Appi = 16,
    IonizationMode_Apci = 32,
    IonizationMode_Esi = 64,
    IonizationMode_NanoEsi = 128,
    IonizationMode_MsChip = 512,
    IonizationMode_ICP = 1024,
    IonizationMode_JetStream = 2048
};

PWIZ_API_DECL enum MSScanType
{
    MSScanType_Unspecified = 0,
    MSScanType_All = 7951,
    MSScanType_AllMS = 15,
    MSScanType_AllMSN = 7936,
    MSScanType_Scan = 1,
    MSScanType_SelectedIon = 2,
    MSScanType_HighResolutionScan = 4,
    MSScanType_TotalIon = 8,
    MSScanType_MultipleReaction = 256,
    MSScanType_ProductIon = 512,
    MSScanType_PrecursorIon = 1024,
    MSScanType_NeutralLoss = 2048,
    MSScanType_NeutralGain = 4096
};

PWIZ_API_DECL enum MSStorageMode
{
    MSStorageMode_Unspecified = 0,
    MSStorageMode_Mixed = 1,
    MSStorageMode_ProfileSpectrum = 2,
    MSStorageMode_PeakDetectedSpectrum = 3
};

PWIZ_API_DECL enum IonPolarity
{
    IonPolarity_Positive = 0,
    IonPolarity_Negative = 1,
    IonPolarity_Unassigned = 2,
    IonPolarity_Mixed = 3
};

PWIZ_API_DECL enum ChromatogramType
{
    ChromatogramType_Unspecified = 0,
    ChromatogramType_Signal = 1,
    ChromatogramType_InstrumentParameter = 2,
    ChromatogramType_TotalWavelength = 3,
    ChromatogramType_ExtractedWavelength = 4,
    ChromatogramType_TotalIon = 5,
    ChromatogramType_BasePeak = 6,
    ChromatogramType_ExtractedIon = 7,
    ChromatogramType_ExtractedCompound = 8,
    ChromatogramType_TotalCompound = 12,
    ChromatogramType_NeutralLoss = 9,
    ChromatogramType_MultipleReactionMode = 10,
    ChromatogramType_SelectedIonMonitoring = 11
};


struct PWIZ_API_DECL MassRange { double start, end; };
struct PWIZ_API_DECL TimeRange { double start, end; };


struct PWIZ_API_DECL Transition
{
    enum Type { MRM, SIM };

    Type type;
    double Q1;
    double Q3;
    TimeRange acquiredTimeRange;
    IonPolarity ionPolarity;

    bool operator< (const Transition& rhs) const;
};


struct PWIZ_API_DECL PeakFilter
{
    int maxNumPeaks;
    double absoluteThreshold;
    double relativeThreshold;
};

typedef boost::shared_ptr<PeakFilter> PeakFilterPtr;


struct PWIZ_API_DECL Chromatogram
{
    virtual double getCollisionEnergy() const = 0;
    virtual int getTotalDataPoints() const = 0;
    virtual void getXArray(automation_vector<double>& x) const = 0;
    virtual void getYArray(automation_vector<float>& y) const = 0;
    virtual IonPolarity getIonPolarity() const = 0;

    virtual ~Chromatogram() {}
};

typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;


struct PWIZ_API_DECL Spectrum
{
    virtual MSScanType getMSScanType() const = 0;
    virtual MSStorageMode getMSStorageMode() const = 0;
    virtual IonPolarity getIonPolarity() const = 0;
    virtual DeviceType getDeviceType() const = 0;
    virtual MassRange getMeasuredMassRange() const = 0;
    virtual int getParentScanId() const = 0;
    virtual void getPrecursorIons(std::vector<double>& precursorIons) const = 0;
    virtual bool getPrecursorCharge(int& charge) const = 0;
    virtual bool getPrecursorIntensity(double& precursorIntensity) const = 0;
    virtual double getCollisionEnergy() const = 0;
    virtual int getScanId() const = 0;

    virtual int getTotalDataPoints() const = 0;
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const = 0;
    virtual void getYArray(pwiz::util::BinaryData<float>& y) const = 0;

    virtual ~Spectrum() {}
};

typedef boost::shared_ptr<Spectrum> SpectrumPtr;


struct PWIZ_API_DECL ScanRecord
{
    virtual int getScanId() const = 0;
    virtual double getRetentionTime() const = 0;
    virtual int getMSLevel() const = 0;
    virtual MSScanType getMSScanType() const = 0;
    virtual double getTic() const = 0;
    virtual double getBasePeakMZ() const = 0;
    virtual double getBasePeakIntensity() const = 0;
    virtual IonizationMode getIonizationMode() const = 0;
    virtual IonPolarity getIonPolarity() const = 0;
    virtual double getMZOfInterest() const = 0;
    virtual int getTimeSegment() const = 0;
    virtual double getFragmentorVoltage() const = 0;
    virtual double getCollisionEnergy() const = 0;
    virtual bool getIsFragmentorVoltageDynamic() const = 0;
    virtual bool getIsCollisionEnergyDynamic() const = 0;
    virtual bool getIsIonMobilityScan() const = 0;

    virtual ~ScanRecord() {}
};

typedef boost::shared_ptr<ScanRecord> ScanRecordPtr;


struct PWIZ_API_DECL DriftScan
{
    virtual MSStorageMode getMSStorageMode() const = 0;
    virtual DeviceType getDeviceType() const = 0;
    virtual void getPrecursorIons(std::vector<double>& precursorIons) const = 0;
    virtual double getCollisionEnergy() const = 0;
    virtual double getDriftTime() const = 0;
    virtual int getScanId() const = 0;

    virtual int getTotalDataPoints() const = 0;
    virtual const pwiz::util::BinaryData<double>& getXArray() const = 0;
    virtual const pwiz::util::BinaryData<float>& getYArray() const = 0;

    virtual ~DriftScan() {}
};

typedef boost::shared_ptr<DriftScan> DriftScanPtr;


struct PWIZ_API_DECL Frame
{
    virtual int getFrameIndex() const = 0;
    virtual TimeRange getDriftTimeRange() const = 0;
    virtual MassRange getMzRange() const = 0;
    virtual double getRetentionTime() const = 0;
    virtual double getTic() const = 0;
    virtual int getDriftBinsPresent() const = 0;
    virtual const std::vector<short>& getNonEmptyDriftBins() const = 0;
    virtual DriftScanPtr getScan(int driftBinIndex) const = 0;
    virtual DriftScanPtr getTotalScan() const = 0;

    virtual void getCombinedSpectrumData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, pwiz::util::BinaryData<double>& mobilities,
                                         bool ignoreZeroIntensityPoints, const std::vector<pwiz::chemistry::MzMobilityWindow>& mzMobilityFilter) const = 0;
    virtual size_t getCombinedSpectrumDataSize(bool ignoreZeroIntensityPoints, const std::vector<pwiz::chemistry::MzMobilityWindow>& mzMobilityFilter) const = 0;

    virtual ~Frame() {}
};

typedef boost::shared_ptr<Frame> FramePtr;


class PWIZ_API_DECL MassHunterData
{
    protected:
    std::string massHunterRootPath_; // path to a .d directory with AcqData in it

    public:
    typedef boost::shared_ptr<MassHunterData> Ptr;
    static Ptr create(const std::string& path);
    static bool hasIonMobilityData(const std::string& path);

    virtual std::string getVersion() const = 0;
    virtual DeviceType getDeviceType() const = 0;
    virtual std::string getDeviceName(DeviceType deviceType) const = 0;
    virtual std::string getDeviceSerialNumber(DeviceType deviceType) const;
    virtual boost::local_time::local_date_time getAcquisitionTime(bool adjustToHostTime) const = 0;
    virtual IonizationMode getIonModes() const = 0;
    virtual MSScanType getScanTypes() const = 0;
    virtual MSStorageMode getSpectraFormat() const = 0;
    virtual int getTotalScansPresent() const = 0;
    virtual bool hasProfileData() const = 0;

    virtual bool hasIonMobilityData() const = 0;
    virtual int getTotalIonMobilityFramesPresent() const = 0;
    virtual FramePtr getIonMobilityFrame(int frameIndex) const = 0;

    virtual bool canConvertDriftTimeAndCCS() const = 0;
    virtual double driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const = 0;
    virtual double ccsToDriftTime(double ccs, double mz, int charge) const = 0;

    virtual const std::set<Transition>& getTransitions() const = 0;
    virtual ChromatogramPtr getChromatogram(const Transition& transition) const = 0;

    virtual const automation_vector<double>& getTicTimes(bool ms1Only = false) const = 0;
    virtual const automation_vector<double>& getBpcTimes(bool ms1Only = false) const = 0;
    virtual const automation_vector<float>& getTicIntensities(bool ms1Only = false) const = 0;
    virtual const automation_vector<float>& getBpcIntensities(bool ms1Only = false) const = 0;

    /// rowNumber is a 0-based index
    virtual ScanRecordPtr getScanRecord(int rowNumber) const = 0;
    virtual SpectrumPtr getProfileSpectrumByRow(int rowNumber) const = 0;
    virtual SpectrumPtr getPeakSpectrumByRow(int rowNumber, PeakFilterPtr peakFilter = PeakFilterPtr()) const = 0;

    /// scanId is an identifier used for spectrum cross references (i.e. "parent scan id")
    virtual SpectrumPtr getProfileSpectrumById(int scanId) const = 0;
    virtual SpectrumPtr getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter = PeakFilterPtr()) const = 0;

    virtual ~MassHunterData() noexcept(false) {}
};

typedef MassHunterData::Ptr MassHunterDataPtr;


} // Agilent
} // vendor_api
} // pwiz


#endif // _MASSHUNTERDATA_HPP_
