// Created by Microsoft (R) C/C++ Compiler Version 14.00.50727.762 (46ae0707).
//
// (dacserver.tlh)
//
// C++ source equivalent of Win32 type library C:\MassLynx\DACServer.dll
// (From MassLynx 4.1)
// compiler-generated file created 04/16/08 at 13:23:24 - DO NOT EDIT!

#pragma once
#pragma pack(push, 8)

#include <comdef.h>

//
// Forward references and typedefs
//

struct __declspec(uuid("0d4eb5e2-3561-11d5-803c-00508b5ffec8"))
/* LIBID */ __DACSERVERLib;
struct /* coclass */ DACScanStats;
struct __declspec(uuid("0d0678c1-3a1b-11d5-8040-00508b5ffec8"))
/* dual interface */ IDACScanStats;
struct /* coclass */ DACExScanStats;
struct __declspec(uuid("35be9403-3ae1-11d5-8041-00508b5ffec8"))
/* dual interface */ IDACExScanStats;
struct /* coclass */ DACSpectrum;
struct __declspec(uuid("42bae6e3-3d52-11d5-8043-00508b5ffec8"))
/* dual interface */ IDACSpectrum;
struct /* coclass */ DACChromatogram;
struct __declspec(uuid("42bae6e5-3d52-11d5-8043-00508b5ffec8"))
/* dual interface */ IDACChromatogram;
struct /* coclass */ DACFunctionInfo;
struct __declspec(uuid("63e4a0c1-5684-11d5-8063-00508b5ffec8"))
/* dual interface */ IDACFunctionInfo;
struct /* coclass */ DACAnalog;
struct __declspec(uuid("3d9244d0-599a-11d5-8066-00508b5ffec8"))
/* dual interface */ IDACAnalog;
struct /* coclass */ DACProcessInfo;
struct __declspec(uuid("99af80b0-7f7d-11d5-808d-00508b5ffec8"))
/* dual interface */ IDACProcessInfo;
struct /* coclass */ DACExperimentInfo;
struct __declspec(uuid("95339ef1-8650-11d5-8095-00508b5ffec8"))
/* dual interface */ IDACExperimentInfo;
struct /* coclass */ DACHeader;
struct __declspec(uuid("111a3110-8a5c-11d5-809c-00508b5ffec8"))
/* dual interface */ IDACHeader;
struct /* coclass */ DACCalibrationInfo;
struct __declspec(uuid("686ed0d1-8a7a-11d5-809c-00508b5ffec8"))
/* dual interface */ IDACCalibrationInfo;

//
// Smart pointer typedef declarations
//

_COM_SMARTPTR_TYPEDEF(IDACScanStats, __uuidof(IDACScanStats));
_COM_SMARTPTR_TYPEDEF(IDACExScanStats, __uuidof(IDACExScanStats));
_COM_SMARTPTR_TYPEDEF(IDACSpectrum, __uuidof(IDACSpectrum));
_COM_SMARTPTR_TYPEDEF(IDACChromatogram, __uuidof(IDACChromatogram));
_COM_SMARTPTR_TYPEDEF(IDACFunctionInfo, __uuidof(IDACFunctionInfo));
_COM_SMARTPTR_TYPEDEF(IDACAnalog, __uuidof(IDACAnalog));
_COM_SMARTPTR_TYPEDEF(IDACProcessInfo, __uuidof(IDACProcessInfo));
_COM_SMARTPTR_TYPEDEF(IDACExperimentInfo, __uuidof(IDACExperimentInfo));
_COM_SMARTPTR_TYPEDEF(IDACHeader, __uuidof(IDACHeader));
_COM_SMARTPTR_TYPEDEF(IDACCalibrationInfo, __uuidof(IDACCalibrationInfo));

//
// Type library items
//

struct __declspec(uuid("0d0678c2-3a1b-11d5-8040-00508b5ffec8"))
DACScanStats;
    // [ default ] interface IDACScanStats

struct __declspec(uuid("0d0678c1-3a1b-11d5-8040-00508b5ffec8"))
IDACScanStats : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetPeaksInScan))
    long PeaksInScan;
    __declspec(property(get=GetMolecularMass))
    long MolecularMass;
    __declspec(property(get=GetCalibrated))
    long Calibrated;
    __declspec(property(get=GetOverload))
    long Overload;
    __declspec(property(get=GetAccurateMass))
    long AccurateMass;
    __declspec(property(get=GetTIC))
    float TIC;
    __declspec(property(get=GetRetnTime))
    float RetnTime;
    __declspec(property(get=GetBPM))
    float BPM;
    __declspec(property(get=GetBPI))
    float BPI;
    __declspec(property(get=GetLoMass))
    float LoMass;
    __declspec(property(get=GetHiMass))
    float HiMass;
    __declspec(property(get=GetContinuum))
    long Continuum;
    __declspec(property(get=GetSegment))
    int Segment;

    //
    // Wrapper methods for error-handling
    //

    long GetPeaksInScan ( );
    long GetMolecularMass ( );
    long GetCalibrated ( );
    long GetOverload ( );
    long GetAccurateMass ( );
    float GetTIC ( );
    float GetRetnTime ( );
    float GetBPM ( );
    float GetBPI ( );
    float GetLoMass ( );
    float GetHiMass ( );
    long GetContinuum ( );
    int GetSegment ( );
    long GetScanStats (
        _bstr_t FileName,
        short FunctionNumber,
        short ProcessNumber,
        long ScanNumber );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_PeaksInScan (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_MolecularMass (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_Calibrated (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_Overload (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_AccurateMass (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_TIC (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_RetnTime (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_BPM (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_BPI (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_LoMass (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_HiMass (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_Continuum (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_Segment (
        /*[out,retval]*/ int * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetScanStats (
        /*[in]*/ BSTR FileName,
        /*[in]*/ short FunctionNumber,
        /*[in]*/ short ProcessNumber,
        /*[in]*/ long ScanNumber,
        /*[out,retval]*/ long * pnResult ) = 0;
};

struct __declspec(uuid("35be9404-3ae1-11d5-8041-00508b5ffec8"))
DACExScanStats;
    // [ default ] interface IDACExScanStats

struct __declspec(uuid("35be9403-3ae1-11d5-8041-00508b5ffec8"))
IDACExScanStats : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetLinearDetectorVoltage))
    short LinearDetectorVoltage;
    __declspec(property(get=GetLinearSensitivity))
    short LinearSensitivity;
    __declspec(property(get=GetReflectronLensVoltage))
    short ReflectronLensVoltage;
    __declspec(property(get=GetReflectronDetectorVoltage))
    short ReflectronDetectorVoltage;
    __declspec(property(get=GetReflectronSensitivity))
    short ReflectronSensitivity;
    __declspec(property(get=GetLaserRepetitionRate))
    short LaserRepetitionRate;
    __declspec(property(get=GetCoarseLaserControl))
    short CoarseLaserControl;
    __declspec(property(get=GetFineLaserControl))
    short FineLaserControl;
    __declspec(property(get=GetLaserAimXPos))
    float LaserAimXPos;
    __declspec(property(get=GetLaserAimYPos))
    float LaserAimYPos;
    __declspec(property(get=GetNumShotsSummed))
    short NumShotsSummed;
    __declspec(property(get=GetNumShotsPerformed))
    short NumShotsPerformed;
    __declspec(property(get=GetSegmentNumber))
    short SegmentNumber;
    __declspec(property(get=GetNeedle))
    short Needle;
    __declspec(property(get=GetCounterElectrodeVoltage))
    short CounterElectrodeVoltage;
    __declspec(property(get=GetSamplingConeVoltage))
    short SamplingConeVoltage;
    __declspec(property(get=GetSkimmerLens))
    short SkimmerLens;
    __declspec(property(get=GetSkimmer))
    short Skimmer;
    __declspec(property(get=GetProbeTemperature))
    short ProbeTemperature;
    __declspec(property(get=GetSourceTemperature))
    short SourceTemperature;
    __declspec(property(get=GetRFVoltage))
    short RFVoltage;
    __declspec(property(get=GetSourceAperture))
    short SourceAperture;
    __declspec(property(get=GetSourceCode))
    short SourceCode;
    __declspec(property(get=GetLMResolution))
    short LMResolution;
    __declspec(property(get=GetHMResolution))
    short HMResolution;
    __declspec(property(get=GetCollisionEnergy))
    float CollisionEnergy;
    __declspec(property(get=GetIonEnergy))
    short IonEnergy;
    __declspec(property(get=GetMultiplier1))
    short Multiplier1;
    __declspec(property(get=GetMultiplier2))
    short Multiplier2;
    __declspec(property(get=GetTransportDC))
    short TransportDC;
    __declspec(property(get=GetTOFAperture))
    short TOFAperture;
    __declspec(property(get=GetAccVoltage))
    short AccVoltage;
    __declspec(property(get=GetSteering))
    short Steering;
    __declspec(property(get=GetFocus))
    short Focus;
    __declspec(property(get=GetEntrance))
    short Entrance;
    __declspec(property(get=GetGuard))
    short Guard;
    __declspec(property(get=GetTOF))
    short TOF;
    __declspec(property(get=GetReflectron))
    short Reflectron;
    __declspec(property(get=GetCollisionRF))
    short CollisionRF;
    __declspec(property(get=GetTransportRF))
    short TransportRF;
    __declspec(property(get=GetSetMass))
    float SetMass;
    __declspec(property(get=GetReferenceScan))
    unsigned char ReferenceScan;
    __declspec(property(get=GetUseLockMassCorrection))
    unsigned char UseLockMassCorrection;
    __declspec(property(get=GetLockMassCorrection))
    float LockMassCorrection;
    __declspec(property(get=GetUseTempCorrection))
    unsigned char UseTempCorrection;
    __declspec(property(get=GetTempCorrection))
    float TempCorrection;
    __declspec(property(get=GetTempCoefficient))
    float TempCoefficient;
    __declspec(property(get=GetTFMWell))
    short TFMWell;
    __declspec(property(get=GetPSDSegmentType))
    short PSDSegmentType;
    __declspec(property(get=GetSourceRegion1))
    float SourceRegion1;
    __declspec(property(get=GetSourceRegion2))
    float SourceRegion2;
    __declspec(property(get=GetReflectronLength))
    float ReflectronLength;
    __declspec(property(get=GetReflectronFieldLength))
    float ReflectronFieldLength;
    __declspec(property(get=GetReflectronVoltage))
    float ReflectronVoltage;
    __declspec(property(get=GetReflectronFieldLengthAlt))
    float ReflectronFieldLengthAlt;
    __declspec(property(get=GetReflectronLengthAlt))
    float ReflectronLengthAlt;
    __declspec(property(get=GetPSDMajorStep))
    float PSDMajorStep;
    __declspec(property(get=GetPSDMinorStep))
    float PSDMinorStep;
    __declspec(property(get=GetPSDFactor1))
    float PSDFactor1;
    __declspec(property(get=GetFaimsCV))
    float FaimsCV;
    __declspec(property(get=GetTIC_A))
    float TIC_A;
    __declspec(property(get=GetTIC_B))
    float TIC_B;
    __declspec(property(get=GetAccurateMass))
    long AccurateMass;
    __declspec(property(get=GetAccurateMassFlags))
    short AccurateMassFlags;
    __declspec(property(get=GetSamplePlateVoltage))
    float SamplePlateVoltage;

    //
    // Wrapper methods for error-handling
    //

    short GetLinearDetectorVoltage ( );
    short GetLinearSensitivity ( );
    short GetReflectronLensVoltage ( );
    short GetReflectronDetectorVoltage ( );
    short GetReflectronSensitivity ( );
    short GetLaserRepetitionRate ( );
    short GetCoarseLaserControl ( );
    short GetFineLaserControl ( );
    float GetLaserAimXPos ( );
    float GetLaserAimYPos ( );
    short GetNumShotsSummed ( );
    short GetNumShotsPerformed ( );
    short GetSegmentNumber ( );
    short GetNeedle ( );
    short GetCounterElectrodeVoltage ( );
    short GetSamplingConeVoltage ( );
    short GetSkimmerLens ( );
    short GetSkimmer ( );
    short GetProbeTemperature ( );
    short GetSourceTemperature ( );
    short GetRFVoltage ( );
    short GetSourceAperture ( );
    short GetSourceCode ( );
    short GetLMResolution ( );
    short GetHMResolution ( );
    float GetCollisionEnergy ( );
    short GetIonEnergy ( );
    short GetMultiplier1 ( );
    short GetMultiplier2 ( );
    short GetTransportDC ( );
    short GetTOFAperture ( );
    short GetAccVoltage ( );
    short GetSteering ( );
    short GetFocus ( );
    short GetEntrance ( );
    short GetGuard ( );
    short GetTOF ( );
    short GetReflectron ( );
    short GetCollisionRF ( );
    short GetTransportRF ( );
    float GetSetMass ( );
    unsigned char GetReferenceScan ( );
    unsigned char GetUseLockMassCorrection ( );
    float GetLockMassCorrection ( );
    unsigned char GetUseTempCorrection ( );
    float GetTempCorrection ( );
    float GetTempCoefficient ( );
    long GetExScanStats (
        _bstr_t FileName,
        short FunctionNumber,
        short ProcessNumber,
        long ScanNumber );
    short GetTFMWell ( );
    short GetPSDSegmentType ( );
    float GetSourceRegion1 ( );
    float GetSourceRegion2 ( );
    float GetReflectronLength ( );
    float GetReflectronFieldLength ( );
    float GetReflectronVoltage ( );
    float GetReflectronFieldLengthAlt ( );
    float GetReflectronLengthAlt ( );
    float GetPSDMajorStep ( );
    float GetPSDMinorStep ( );
    float GetPSDFactor1 ( );
    float GetFaimsCV ( );
    float GetTIC_A ( );
    float GetTIC_B ( );
    long GetAccurateMass ( );
    short GetAccurateMassFlags ( );
    float GetSamplePlateVoltage ( );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_LinearDetectorVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_LinearSensitivity (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronLensVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronDetectorVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronSensitivity (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_LaserRepetitionRate (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_CoarseLaserControl (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_FineLaserControl (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_LaserAimXPos (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_LaserAimYPos (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_NumShotsSummed (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_NumShotsPerformed (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SegmentNumber (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Needle (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_CounterElectrodeVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SamplingConeVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SkimmerLens (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Skimmer (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_ProbeTemperature (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SourceTemperature (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_RFVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SourceAperture (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SourceCode (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_LMResolution (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_HMResolution (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_CollisionEnergy (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_IonEnergy (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Multiplier1 (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Multiplier2 (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_TransportDC (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_TOFAperture (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_AccVoltage (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Steering (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Focus (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Entrance (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Guard (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_TOF (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_Reflectron (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_CollisionRF (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_TransportRF (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SetMass (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReferenceScan (
        /*[out,retval]*/ unsigned char * pVal ) = 0;
      virtual HRESULT __stdcall get_UseLockMassCorrection (
        /*[out,retval]*/ unsigned char * pVal ) = 0;
      virtual HRESULT __stdcall get_LockMassCorrection (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_UseTempCorrection (
        /*[out,retval]*/ unsigned char * pVal ) = 0;
      virtual HRESULT __stdcall get_TempCorrection (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_TempCoefficient (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetExScanStats (
        BSTR FileName,
        short FunctionNumber,
        short ProcessNumber,
        long ScanNumber,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_TFMWell (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_PSDSegmentType (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SourceRegion1 (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_SourceRegion2 (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronLength (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronFieldLength (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronVoltage (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronFieldLengthAlt (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_ReflectronLengthAlt (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_PSDMajorStep (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_PSDMinorStep (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_PSDFactor1 (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_FaimsCV (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_TIC_A (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_TIC_B (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_AccurateMass (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_AccurateMassFlags (
        /*[out,retval]*/ short * pVal ) = 0;
      virtual HRESULT __stdcall get_SamplePlateVoltage (
        /*[out,retval]*/ float * pVal ) = 0;
};

struct __declspec(uuid("42bae6e4-3d52-11d5-8043-00508b5ffec8"))
DACSpectrum;
    // [ default ] interface IDACSpectrum

struct __declspec(uuid("42bae6e3-3d52-11d5-8043-00508b5ffec8"))
IDACSpectrum : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetNumPeaks))
    long NumPeaks;
    __declspec(property(get=GetMasses))
    _variant_t Masses;
    __declspec(property(get=GetIntensities))
    _variant_t Intensities;

    //
    // Wrapper methods for error-handling
    //

    long GetNumPeaks ( );
    _variant_t GetMasses ( );
    _variant_t GetIntensities ( );
    long GetSpectrum (
        _bstr_t FileName,
        short FunctionNumber,
        short ProcessNumber,
        long ScanNumber );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_NumPeaks (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_Masses (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_Intensities (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetSpectrum (
        /*[in]*/ BSTR FileName,
        /*[in]*/ short FunctionNumber,
        /*[in]*/ short ProcessNumber,
        /*[in]*/ long ScanNumber,
        /*[out,retval]*/ long * pnResult ) = 0;
};

struct __declspec(uuid("42bae6e6-3d52-11d5-8043-00508b5ffec8"))
DACChromatogram;
    // [ default ] interface IDACChromatogram

struct __declspec(uuid("42bae6e5-3d52-11d5-8043-00508b5ffec8"))
IDACChromatogram : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetTimes))
    _variant_t Times;
    __declspec(property(get=GetIntensities))
    _variant_t Intensities;
    __declspec(property(get=GetScans))
    _variant_t Scans;
    __declspec(property(get=GetNumScans))
    long NumScans;

    //
    // Wrapper methods for error-handling
    //

    _variant_t GetTimes ( );
    _variant_t GetIntensities ( );
    long GetChromatogram (
        _bstr_t FileName,
        short FunctionNumber,
        short ProcessNumber,
        float ChroStart,
        float ChroEnd,
        long Times,
        _bstr_t ChroType );
    _variant_t GetScans ( );
    long GetNumScans ( );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_Times (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_Intensities (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetChromatogram (
        BSTR FileName,
        short FunctionNumber,
        short ProcessNumber,
        float ChroStart,
        float ChroEnd,
        long Times,
        BSTR ChroType,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_Scans (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_NumScans (
        /*[out,retval]*/ long * pVal ) = 0;
};

struct __declspec(uuid("63e4a0c2-5684-11d5-8063-00508b5ffec8"))
DACFunctionInfo;
    // [ default ] interface IDACFunctionInfo

struct __declspec(uuid("63e4a0c1-5684-11d5-8063-00508b5ffec8"))
IDACFunctionInfo : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetFunctionType))
    _bstr_t FunctionType;
    __declspec(property(get=GetEndRT))
    float EndRT;
    __declspec(property(get=GetNumScans))
    long NumScans;
    __declspec(property(get=GetStartRT))
    float StartRT;
    __declspec(property(get=GetFunctionSetMass))
    double FunctionSetMass;
    __declspec(property(get=GetNumSegments))
    long NumSegments;
    __declspec(property(get=GetSIRChannels))
    _variant_t SIRChannels;
    __declspec(property(get=GetMRMParents))
    _variant_t MRMParents;
    __declspec(property(get=GetMRMDaughters))
    _variant_t MRMDaughters;

    //
    // Wrapper methods for error-handling
    //

    _bstr_t GetFunctionType ( );
    float GetEndRT ( );
    long GetNumScans ( );
    float GetStartRT ( );
    long GetFunctionInfo (
        _bstr_t FileName,
        short FunctionNumber );
    double GetFunctionSetMass ( );
    long GetNumSegments ( );
    _variant_t GetSIRChannels ( );
    _variant_t GetMRMParents ( );
    _variant_t GetMRMDaughters ( );
    long GetNumFunctions (
        _bstr_t FileName );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_FunctionType (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_EndRT (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall get_NumScans (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_StartRT (
        /*[out,retval]*/ float * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetFunctionInfo (
        BSTR FileName,
        short FunctionNumber,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_FunctionSetMass (
        /*[out,retval]*/ double * pVal ) = 0;
      virtual HRESULT __stdcall get_NumSegments (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_SIRChannels (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_MRMParents (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_MRMDaughters (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetNumFunctions (
        /*[in]*/ BSTR FileName,
        /*[out,retval]*/ long * NumFunctions ) = 0;
};

struct __declspec(uuid("3d9244d1-599a-11d5-8066-00508b5ffec8"))
DACAnalog;
    // [ default ] interface IDACAnalog

struct __declspec(uuid("3d9244d0-599a-11d5-8066-00508b5ffec8"))
IDACAnalog : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetNumAnalogs))
    long NumAnalogs;
    __declspec(property(get=GetAnalogTypes))
    _variant_t AnalogTypes;
    __declspec(property(get=GetNumDataPoints))
    long NumDataPoints;

    //
    // Wrapper methods for error-handling
    //

    long GetNumAnalogs ( );
    _variant_t GetAnalogTypes ( );
    long GetAnalogChannels (
        _bstr_t FileName );
    long GetNumDataPoints ( );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_NumAnalogs (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_AnalogTypes (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetAnalogChannels (
        BSTR FileName,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_NumDataPoints (
        /*[out,retval]*/ long * pVal ) = 0;
};

struct __declspec(uuid("99af80b1-7f7d-11d5-808d-00508b5ffec8"))
DACProcessInfo;
    // [ default ] interface IDACProcessInfo

struct __declspec(uuid("99af80b0-7f7d-11d5-808d-00508b5ffec8"))
IDACProcessInfo : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetNumProcesses))
    long NumProcesses;
    __declspec(property(get=GetProcessDescs))
    _variant_t ProcessDescs;

    //
    // Wrapper methods for error-handling
    //

    long GetNumProcesses ( );
    _variant_t GetProcessDescs ( );
    long GetProcessInfo (
        _bstr_t FileName,
        short FunctionNumber );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_NumProcesses (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_ProcessDescs (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetProcessInfo (
        BSTR FileName,
        short FunctionNumber,
        /*[out,retval]*/ long * pnResult ) = 0;
};

struct __declspec(uuid("95339ef2-8650-11d5-8095-00508b5ffec8"))
DACExperimentInfo;
    // [ default ] interface IDACExperimentInfo

struct __declspec(uuid("95339ef1-8650-11d5-8095-00508b5ffec8"))
IDACExperimentInfo : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetExperimentText))
    _bstr_t ExperimentText;

    //
    // Wrapper methods for error-handling
    //

    long GetExperimentInfo (
        _bstr_t FileName );
    _bstr_t GetExperimentText ( );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall raw_GetExperimentInfo (
        BSTR FileName,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_ExperimentText (
        /*[out,retval]*/ BSTR * pVal ) = 0;
};

struct __declspec(uuid("111a3111-8a5c-11d5-809c-00508b5ffec8"))
DACHeader;
    // [ default ] interface IDACHeader

struct __declspec(uuid("111a3110-8a5c-11d5-809c-00508b5ffec8"))
IDACHeader : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetVersionMajor))
    long VersionMajor;
    __declspec(property(get=GetVersionMinor))
    long VersionMinor;
    __declspec(property(get=GetAcquName))
    _bstr_t AcquName;
    __declspec(property(get=GetAcquDate))
    _bstr_t AcquDate;
    __declspec(property(get=GetAcquTime))
    _bstr_t AcquTime;
    __declspec(property(get=GetJobCode))
    _bstr_t JobCode;
    __declspec(property(get=GetTaskCode))
    _bstr_t TaskCode;
    __declspec(property(get=GetUserName))
    _bstr_t UserName;
    __declspec(property(get=GetLabName))
    _bstr_t LabName;
    __declspec(property(get=GetInstrument))
    _bstr_t Instrument;
    __declspec(property(get=GetConditions))
    _bstr_t Conditions;
    __declspec(property(get=GetSampleDesc))
    _bstr_t SampleDesc;
    __declspec(property(get=GetSubmitter))
    _bstr_t Submitter;
    __declspec(property(get=GetSampleID))
    _bstr_t SampleID;
    __declspec(property(get=GetBottleNumber))
    _bstr_t BottleNumber;
    __declspec(property(get=GetSolventDelay))
    double SolventDelay;
    __declspec(property(get=GetResolved))
    long Resolved;
    __declspec(property(get=GetPepFileName))
    _bstr_t PepFileName;
    __declspec(property(get=GetProcess))
    _bstr_t Process;
    __declspec(property(get=GetEncrypted))
    long Encrypted;
    __declspec(property(get=GetAutosamplerType))
    long AutosamplerType;
    __declspec(property(get=GetGasName))
    _bstr_t GasName;
    __declspec(property(get=GetInstrumentType))
    _bstr_t InstrumentType;
    __declspec(property(get=GetPlateDesc))
    _bstr_t PlateDesc;
    __declspec(property(get=GetAnalogOffset))
    _variant_t AnalogOffset;
    __declspec(property(get=GetMuxStream))
    long MuxStream;

    //
    // Wrapper methods for error-handling
    //

    long GetVersionMajor ( );
    long GetVersionMinor ( );
    _bstr_t GetAcquName ( );
    _bstr_t GetAcquDate ( );
    _bstr_t GetAcquTime ( );
    _bstr_t GetJobCode ( );
    _bstr_t GetTaskCode ( );
    _bstr_t GetUserName ( );
    _bstr_t GetLabName ( );
    _bstr_t GetInstrument ( );
    _bstr_t GetConditions ( );
    _bstr_t GetSampleDesc ( );
    _bstr_t GetSubmitter ( );
    _bstr_t GetSampleID ( );
    _bstr_t GetBottleNumber ( );
    double GetSolventDelay ( );
    long GetResolved ( );
    _bstr_t GetPepFileName ( );
    _bstr_t GetProcess ( );
    long GetEncrypted ( );
    long GetAutosamplerType ( );
    _bstr_t GetGasName ( );
    _bstr_t GetInstrumentType ( );
    _bstr_t GetPlateDesc ( );
    _variant_t GetAnalogOffset ( );
    long GetMuxStream ( );
    long GetHeader (
        _bstr_t FileName );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall get_VersionMajor (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_VersionMinor (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_AcquName (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_AcquDate (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_AcquTime (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_JobCode (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_TaskCode (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_UserName (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_LabName (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_Instrument (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_Conditions (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_SampleDesc (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_Submitter (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_SampleID (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_BottleNumber (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_SolventDelay (
        /*[out,retval]*/ double * pVal ) = 0;
      virtual HRESULT __stdcall get_Resolved (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_PepFileName (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_Process (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_Encrypted (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_AutosamplerType (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_GasName (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_InstrumentType (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_PlateDesc (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_AnalogOffset (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
      virtual HRESULT __stdcall get_MuxStream (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall raw_GetHeader (
        BSTR FileName,
        /*[out,retval]*/ long * pnResult ) = 0;
};

struct __declspec(uuid("686ed0d2-8a7a-11d5-809c-00508b5ffec8"))
DACCalibrationInfo;
    // [ default ] interface IDACCalibrationInfo

struct __declspec(uuid("686ed0d1-8a7a-11d5-809c-00508b5ffec8"))
IDACCalibrationInfo : IDispatch
{
    //
    // Property data
    //

    __declspec(property(get=GetMS1StaticFunction))
    _bstr_t MS1StaticFunction;
    __declspec(property(get=GetMS2StaticFunction))
    _bstr_t MS2StaticFunction;
    __declspec(property(get=GetMS1StaticParams))
    _bstr_t MS1StaticParams;
    __declspec(property(get=GetMS1DynamicParams))
    _bstr_t MS1DynamicParams;
    __declspec(property(get=GetMS1FastParams))
    _bstr_t MS1FastParams;
    __declspec(property(get=GetMS2StaticParams))
    _bstr_t MS2StaticParams;
    __declspec(property(get=GetMS2DynamicParams))
    _bstr_t MS2DynamicParams;
    __declspec(property(get=GetMS2FastParams))
    _bstr_t MS2FastParams;
    __declspec(property(get=GetCalTime))
    _bstr_t CalTime;
    __declspec(property(get=GetCalDate))
    _bstr_t CalDate;
    __declspec(property(get=GetNumCalFunctions))
    long NumCalFunctions;
    __declspec(property(get=GetCalFunctions))
    _variant_t CalFunctions;

    //
    // Wrapper methods for error-handling
    //

    long GetCalibration (
        _bstr_t FileName );
    _bstr_t GetMS1StaticFunction ( );
    _bstr_t GetMS2StaticFunction ( );
    _bstr_t GetMS1StaticParams ( );
    _bstr_t GetMS1DynamicParams ( );
    _bstr_t GetMS1FastParams ( );
    _bstr_t GetMS2StaticParams ( );
    _bstr_t GetMS2DynamicParams ( );
    _bstr_t GetMS2FastParams ( );
    _bstr_t GetCalTime ( );
    _bstr_t GetCalDate ( );
    long GetNumCalFunctions ( );
    _variant_t GetCalFunctions ( );

    //
    // Raw methods provided by interface
    //

      virtual HRESULT __stdcall raw_GetCalibration (
        BSTR FileName,
        /*[out,retval]*/ long * pnResult ) = 0;
      virtual HRESULT __stdcall get_MS1StaticFunction (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS2StaticFunction (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS1StaticParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS1DynamicParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS1FastParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS2StaticParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS2DynamicParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_MS2FastParams (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_CalTime (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_CalDate (
        /*[out,retval]*/ BSTR * pVal ) = 0;
      virtual HRESULT __stdcall get_NumCalFunctions (
        /*[out,retval]*/ long * pVal ) = 0;
      virtual HRESULT __stdcall get_CalFunctions (
        /*[out,retval]*/ VARIANT * pVal ) = 0;
};

//
// Named GUID constants initializations
//

extern "C" const GUID __declspec(selectany) LIBID_DACSERVERLib =
    {0x0d4eb5e2,0x3561,0x11d5,{0x80,0x3c,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACScanStats =
    {0x0d0678c2,0x3a1b,0x11d5,{0x80,0x40,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACScanStats =
    {0x0d0678c1,0x3a1b,0x11d5,{0x80,0x40,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACExScanStats =
    {0x35be9404,0x3ae1,0x11d5,{0x80,0x41,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACExScanStats =
    {0x35be9403,0x3ae1,0x11d5,{0x80,0x41,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACSpectrum =
    {0x42bae6e4,0x3d52,0x11d5,{0x80,0x43,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACSpectrum =
    {0x42bae6e3,0x3d52,0x11d5,{0x80,0x43,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACChromatogram =
    {0x42bae6e6,0x3d52,0x11d5,{0x80,0x43,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACChromatogram =
    {0x42bae6e5,0x3d52,0x11d5,{0x80,0x43,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACFunctionInfo =
    {0x63e4a0c2,0x5684,0x11d5,{0x80,0x63,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACFunctionInfo =
    {0x63e4a0c1,0x5684,0x11d5,{0x80,0x63,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACAnalog =
    {0x3d9244d1,0x599a,0x11d5,{0x80,0x66,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACAnalog =
    {0x3d9244d0,0x599a,0x11d5,{0x80,0x66,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACProcessInfo =
    {0x99af80b1,0x7f7d,0x11d5,{0x80,0x8d,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACProcessInfo =
    {0x99af80b0,0x7f7d,0x11d5,{0x80,0x8d,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACExperimentInfo =
    {0x95339ef2,0x8650,0x11d5,{0x80,0x95,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACExperimentInfo =
    {0x95339ef1,0x8650,0x11d5,{0x80,0x95,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACHeader =
    {0x111a3111,0x8a5c,0x11d5,{0x80,0x9c,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACHeader =
    {0x111a3110,0x8a5c,0x11d5,{0x80,0x9c,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) CLSID_DACCalibrationInfo =
    {0x686ed0d2,0x8a7a,0x11d5,{0x80,0x9c,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};
extern "C" const GUID __declspec(selectany) IID_IDACCalibrationInfo =
    {0x686ed0d1,0x8a7a,0x11d5,{0x80,0x9c,0x00,0x50,0x8b,0x5f,0xfe,0xc8}};

//
// Wrapper method implementations
//

// originally:
//#include "dacserver.tli"
#include "dacserver_4-1.cpp"

#pragma pack(pop)
