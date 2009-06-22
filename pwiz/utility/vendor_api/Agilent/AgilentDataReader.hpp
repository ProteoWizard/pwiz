//
// AgilentDataReader.hpp
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


#ifndef _AGILENTDATAREADER_HPP_
#define _AGILENTDATAREADER_HPP_

#ifdef AGILENTDATAREADER_DYN_LINK
#ifdef AGILENTDATAREADER_SOURCE
#define AGILENTDATAREADER_API __declspec(dllexport)
#else
#define AGILENTDATAREADER_API __declspec(dllimport)
#endif  // AGILENTDATAREADER_SOURCE
#endif  // AGILENTDATAREADER_DYN_LINK

// if AGILENTDATAREADER_API isn't defined yet define it now:
#ifndef AGILENTDATAREADER_API
#define AGILENTDATAREADER_API
#endif

#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace agilent {

AGILENTDATAREADER_API enum DeviceType
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

AGILENTDATAREADER_API enum IonizationMode
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

AGILENTDATAREADER_API enum MSScanType
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

AGILENTDATAREADER_API enum MSStorageMode
{
    MSStorageMode_Unspecified = 0,
    MSStorageMode_Mixed = 1,
    MSStorageMode_ProfileSpectrum = 2,
    MSStorageMode_PeakDetectedSpectrum = 3
};

AGILENTDATAREADER_API enum IonPolarity
{
    IonPolarity_Positive = 0,
    IonPolarity_Negative = 1,
    IonPolarity_Unassigned = 2,
    IonPolarity_Mixed = 3
};

AGILENTDATAREADER_API enum ChromType
{
    ChromType_Unspecified = 0,
    ChromType_Signal = 1,
    ChromType_InstrumentParameter = 2,
    ChromType_TotalWavelength = 3,
    ChromType_ExtractedWavelength = 4,
    ChromType_TotalIon = 5,
    ChromType_BasePeak = 6,
    ChromType_ExtractedIon = 7,
    ChromType_ExtractedCompound = 8,
    ChromType_TotalCompound = 12,
    ChromType_NeutralLoss = 9,
    ChromType_MultipleReactionMode = 10,
    ChromType_SelectedIonMonitoring = 11
};

struct AGILENTDATAREADER_API Transition
{
    double precursor;
    double product;
};

struct AGILENTDATAREADER_API MassRange
{
    double start;
    double end;
};

struct AGILENTDATAREADER_API Chromatogram
{
    virtual double getCollisionEnergy() const = 0;
    virtual int getTotalDataPoints() const = 0;
    virtual std::vector<double> getXArray() const = 0;
    virtual std::vector<float> getYArray() const = 0;

    virtual ~Chromatogram() {}
};

typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;

struct AGILENTDATAREADER_API Spectrum
{
    virtual MSScanType getMSScanType() const = 0;
    virtual MSStorageMode getMSStorageMode() const = 0;
    virtual IonPolarity getIonPolarity() const = 0;
    virtual DeviceType getDeviceType() const = 0;
    virtual MassRange getMeasuredMassRange() const = 0;
    virtual long getParentScanId() const = 0;
    virtual std::vector<double> getPrecursorIon(int& precursorCount) const = 0;
    virtual bool getPrecursorCharge(int& charge) const = 0;
    virtual bool getPrecursorIntensity(double& precursorIntensity) const = 0;
    virtual double getCollisionEnergy() const = 0;
    virtual int getScanId() const = 0;
    virtual int getTotalDataPoints() const = 0;
    virtual std::vector<double> getXArray() const = 0;
    virtual std::vector<float> getYArray() const = 0;

    virtual ~Spectrum() {}
};

typedef boost::shared_ptr<Spectrum> SpectrumPtr;

class AGILENTDATAREADER_API AgilentDataReader
{
    public:
    typedef boost::shared_ptr<AgilentDataReader> Ptr;
    static Ptr create(const std::string& path);

    virtual std::string getVersion() const = 0;
    virtual DeviceType getDeviceType() const = 0;
    virtual std::string getDeviceName(DeviceType deviceType) const = 0;
    virtual double getAcquisitionTime() const = 0;
    virtual IonizationMode getIonModes() const = 0;
    virtual MSScanType getScanTypes() const = 0;
    virtual MSStorageMode getSpectraFormat() const = 0;
    virtual long getTotalScansPresent() const = 0;

    virtual std::vector<Transition> getMRMTransitions() const = 0;
    virtual std::vector<double> getSIMIons() const = 0;

    virtual std::vector<double> getTicTimes() const = 0;
    virtual std::vector<double> getBpcTimes() const = 0;
    virtual std::vector<float> getTicIntensities() const = 0;
    virtual std::vector<float> getBpcIntensities() const = 0;

    virtual ChromatogramPtr getChromatogram(int index, ChromType type) const = 0;
    virtual SpectrumPtr getSpectrum(int index, bool centroid = false) const = 0;

    virtual ~AgilentDataReader() {}
};

typedef AgilentDataReader::Ptr AgilentDataReaderPtr;

} // agilent
} // pwiz


#endif // _AGILENTDATAREADER_HPP_
