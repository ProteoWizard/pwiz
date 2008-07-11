// Created by Microsoft (R) C/C++ Compiler Version 14.00.50727.762 (46ae0707).
//
// (dacserver.tli)
//
// Wrapper implementations for Win32 type library C:\MassLynx\DACServer.dll
// (From MassLynx 4.1)
// compiler-generated file created 04/16/08 at 13:23:24 - DO NOT EDIT!

#pragma once

//
// interface IDACScanStats wrapper method implementations
//

inline long IDACScanStats::GetPeaksInScan ( ) {
    long _result = 0;
    HRESULT _hr = get_PeaksInScan(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetMolecularMass ( ) {
    long _result = 0;
    HRESULT _hr = get_MolecularMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetCalibrated ( ) {
    long _result = 0;
    HRESULT _hr = get_Calibrated(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetOverload ( ) {
    long _result = 0;
    HRESULT _hr = get_Overload(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetAccurateMass ( ) {
    long _result = 0;
    HRESULT _hr = get_AccurateMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetTIC ( ) {
    float _result = 0;
    HRESULT _hr = get_TIC(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetRetnTime ( ) {
    float _result = 0;
    HRESULT _hr = get_RetnTime(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetBPM ( ) {
    float _result = 0;
    HRESULT _hr = get_BPM(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetBPI ( ) {
    float _result = 0;
    HRESULT _hr = get_BPI(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetLoMass ( ) {
    float _result = 0;
    HRESULT _hr = get_LoMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACScanStats::GetHiMass ( ) {
    float _result = 0;
    HRESULT _hr = get_HiMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetContinuum ( ) {
    long _result = 0;
    HRESULT _hr = get_Continuum(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline int IDACScanStats::GetSegment ( ) {
    int _result = 0;
    HRESULT _hr = get_Segment(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACScanStats::GetScanStats ( _bstr_t FileName, short FunctionNumber, short ProcessNumber, long ScanNumber ) {
    long _result = 0;
    HRESULT _hr = raw_GetScanStats(FileName, FunctionNumber, ProcessNumber, ScanNumber, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACExScanStats wrapper method implementations
//

inline short IDACExScanStats::GetLinearDetectorVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_LinearDetectorVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetLinearSensitivity ( ) {
    short _result = 0;
    HRESULT _hr = get_LinearSensitivity(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetReflectronLensVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_ReflectronLensVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetReflectronDetectorVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_ReflectronDetectorVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetReflectronSensitivity ( ) {
    short _result = 0;
    HRESULT _hr = get_ReflectronSensitivity(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetLaserRepetitionRate ( ) {
    short _result = 0;
    HRESULT _hr = get_LaserRepetitionRate(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetCoarseLaserControl ( ) {
    short _result = 0;
    HRESULT _hr = get_CoarseLaserControl(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetFineLaserControl ( ) {
    short _result = 0;
    HRESULT _hr = get_FineLaserControl(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetLaserAimXPos ( ) {
    float _result = 0;
    HRESULT _hr = get_LaserAimXPos(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetLaserAimYPos ( ) {
    float _result = 0;
    HRESULT _hr = get_LaserAimYPos(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetNumShotsSummed ( ) {
    short _result = 0;
    HRESULT _hr = get_NumShotsSummed(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetNumShotsPerformed ( ) {
    short _result = 0;
    HRESULT _hr = get_NumShotsPerformed(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSegmentNumber ( ) {
    short _result = 0;
    HRESULT _hr = get_SegmentNumber(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetNeedle ( ) {
    short _result = 0;
    HRESULT _hr = get_Needle(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetCounterElectrodeVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_CounterElectrodeVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSamplingConeVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_SamplingConeVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSkimmerLens ( ) {
    short _result = 0;
    HRESULT _hr = get_SkimmerLens(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSkimmer ( ) {
    short _result = 0;
    HRESULT _hr = get_Skimmer(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetProbeTemperature ( ) {
    short _result = 0;
    HRESULT _hr = get_ProbeTemperature(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSourceTemperature ( ) {
    short _result = 0;
    HRESULT _hr = get_SourceTemperature(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetRFVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_RFVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSourceAperture ( ) {
    short _result = 0;
    HRESULT _hr = get_SourceAperture(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSourceCode ( ) {
    short _result = 0;
    HRESULT _hr = get_SourceCode(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetLMResolution ( ) {
    short _result = 0;
    HRESULT _hr = get_LMResolution(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetHMResolution ( ) {
    short _result = 0;
    HRESULT _hr = get_HMResolution(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetCollisionEnergy ( ) {
    float _result = 0;
    HRESULT _hr = get_CollisionEnergy(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetIonEnergy ( ) {
    short _result = 0;
    HRESULT _hr = get_IonEnergy(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetMultiplier1 ( ) {
    short _result = 0;
    HRESULT _hr = get_Multiplier1(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetMultiplier2 ( ) {
    short _result = 0;
    HRESULT _hr = get_Multiplier2(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetTransportDC ( ) {
    short _result = 0;
    HRESULT _hr = get_TransportDC(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetTOFAperture ( ) {
    short _result = 0;
    HRESULT _hr = get_TOFAperture(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetAccVoltage ( ) {
    short _result = 0;
    HRESULT _hr = get_AccVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetSteering ( ) {
    short _result = 0;
    HRESULT _hr = get_Steering(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetFocus ( ) {
    short _result = 0;
    HRESULT _hr = get_Focus(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetEntrance ( ) {
    short _result = 0;
    HRESULT _hr = get_Entrance(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetGuard ( ) {
    short _result = 0;
    HRESULT _hr = get_Guard(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetTOF ( ) {
    short _result = 0;
    HRESULT _hr = get_TOF(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetReflectron ( ) {
    short _result = 0;
    HRESULT _hr = get_Reflectron(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetCollisionRF ( ) {
    short _result = 0;
    HRESULT _hr = get_CollisionRF(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetTransportRF ( ) {
    short _result = 0;
    HRESULT _hr = get_TransportRF(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetSetMass ( ) {
    float _result = 0;
    HRESULT _hr = get_SetMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline unsigned char IDACExScanStats::GetReferenceScan ( ) {
    unsigned char _result = 0;
    HRESULT _hr = get_ReferenceScan(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline unsigned char IDACExScanStats::GetUseLockMassCorrection ( ) {
    unsigned char _result = 0;
    HRESULT _hr = get_UseLockMassCorrection(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetLockMassCorrection ( ) {
    float _result = 0;
    HRESULT _hr = get_LockMassCorrection(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline unsigned char IDACExScanStats::GetUseTempCorrection ( ) {
    unsigned char _result = 0;
    HRESULT _hr = get_UseTempCorrection(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetTempCorrection ( ) {
    float _result = 0;
    HRESULT _hr = get_TempCorrection(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetTempCoefficient ( ) {
    float _result = 0;
    HRESULT _hr = get_TempCoefficient(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACExScanStats::GetExScanStats ( _bstr_t FileName, short FunctionNumber, short ProcessNumber, long ScanNumber ) {
    long _result = 0;
    HRESULT _hr = raw_GetExScanStats(FileName, FunctionNumber, ProcessNumber, ScanNumber, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetTFMWell ( ) {
    short _result = 0;
    HRESULT _hr = get_TFMWell(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetPSDSegmentType ( ) {
    short _result = 0;
    HRESULT _hr = get_PSDSegmentType(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetSourceRegion1 ( ) {
    float _result = 0;
    HRESULT _hr = get_SourceRegion1(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetSourceRegion2 ( ) {
    float _result = 0;
    HRESULT _hr = get_SourceRegion2(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetReflectronLength ( ) {
    float _result = 0;
    HRESULT _hr = get_ReflectronLength(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetReflectronFieldLength ( ) {
    float _result = 0;
    HRESULT _hr = get_ReflectronFieldLength(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetReflectronVoltage ( ) {
    float _result = 0;
    HRESULT _hr = get_ReflectronVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetReflectronFieldLengthAlt ( ) {
    float _result = 0;
    HRESULT _hr = get_ReflectronFieldLengthAlt(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetReflectronLengthAlt ( ) {
    float _result = 0;
    HRESULT _hr = get_ReflectronLengthAlt(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetPSDMajorStep ( ) {
    float _result = 0;
    HRESULT _hr = get_PSDMajorStep(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetPSDMinorStep ( ) {
    float _result = 0;
    HRESULT _hr = get_PSDMinorStep(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetPSDFactor1 ( ) {
    float _result = 0;
    HRESULT _hr = get_PSDFactor1(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetFaimsCV ( ) {
    float _result = 0;
    HRESULT _hr = get_FaimsCV(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetTIC_A ( ) {
    float _result = 0;
    HRESULT _hr = get_TIC_A(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetTIC_B ( ) {
    float _result = 0;
    HRESULT _hr = get_TIC_B(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACExScanStats::GetAccurateMass ( ) {
    long _result = 0;
    HRESULT _hr = get_AccurateMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline short IDACExScanStats::GetAccurateMassFlags ( ) {
    short _result = 0;
    HRESULT _hr = get_AccurateMassFlags(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACExScanStats::GetSamplePlateVoltage ( ) {
    float _result = 0;
    HRESULT _hr = get_SamplePlateVoltage(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACSpectrum wrapper method implementations
//

inline long IDACSpectrum::GetNumPeaks ( ) {
    long _result = 0;
    HRESULT _hr = get_NumPeaks(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACSpectrum::GetMasses ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_Masses(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline _variant_t IDACSpectrum::GetIntensities ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_Intensities(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACSpectrum::GetSpectrum ( _bstr_t FileName, short FunctionNumber, short ProcessNumber, long ScanNumber ) {
    long _result = 0;
    HRESULT _hr = raw_GetSpectrum(FileName, FunctionNumber, ProcessNumber, ScanNumber, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACChromatogram wrapper method implementations
//

inline _variant_t IDACChromatogram::GetTimes ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_Times(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline _variant_t IDACChromatogram::GetIntensities ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_Intensities(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACChromatogram::GetChromatogram ( _bstr_t FileName, short FunctionNumber, short ProcessNumber, float ChroStart, float ChroEnd, long Times, _bstr_t ChroType ) {
    long _result = 0;
    HRESULT _hr = raw_GetChromatogram(FileName, FunctionNumber, ProcessNumber, ChroStart, ChroEnd, Times, ChroType, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACChromatogram::GetScans ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_Scans(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACChromatogram::GetNumScans ( ) {
    long _result = 0;
    HRESULT _hr = get_NumScans(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACFunctionInfo wrapper method implementations
//

inline _bstr_t IDACFunctionInfo::GetFunctionType ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_FunctionType(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline float IDACFunctionInfo::GetEndRT ( ) {
    float _result = 0;
    HRESULT _hr = get_EndRT(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACFunctionInfo::GetNumScans ( ) {
    long _result = 0;
    HRESULT _hr = get_NumScans(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline float IDACFunctionInfo::GetStartRT ( ) {
    float _result = 0;
    HRESULT _hr = get_StartRT(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACFunctionInfo::GetFunctionInfo ( _bstr_t FileName, short FunctionNumber ) {
    long _result = 0;
    HRESULT _hr = raw_GetFunctionInfo(FileName, FunctionNumber, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline double IDACFunctionInfo::GetFunctionSetMass ( ) {
    double _result = 0;
    HRESULT _hr = get_FunctionSetMass(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACFunctionInfo::GetNumSegments ( ) {
    long _result = 0;
    HRESULT _hr = get_NumSegments(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACFunctionInfo::GetSIRChannels ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_SIRChannels(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline _variant_t IDACFunctionInfo::GetMRMParents ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_MRMParents(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline _variant_t IDACFunctionInfo::GetMRMDaughters ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_MRMDaughters(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACFunctionInfo::GetNumFunctions ( _bstr_t FileName ) {
    long _result = 0;
    HRESULT _hr = raw_GetNumFunctions(FileName, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACAnalog wrapper method implementations
//

inline long IDACAnalog::GetNumAnalogs ( ) {
    long _result = 0;
    HRESULT _hr = get_NumAnalogs(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACAnalog::GetAnalogTypes ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_AnalogTypes(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACAnalog::GetAnalogChannels ( _bstr_t FileName ) {
    long _result = 0;
    HRESULT _hr = raw_GetAnalogChannels(FileName, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACAnalog::GetNumDataPoints ( ) {
    long _result = 0;
    HRESULT _hr = get_NumDataPoints(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACProcessInfo wrapper method implementations
//

inline long IDACProcessInfo::GetNumProcesses ( ) {
    long _result = 0;
    HRESULT _hr = get_NumProcesses(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACProcessInfo::GetProcessDescs ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_ProcessDescs(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACProcessInfo::GetProcessInfo ( _bstr_t FileName, short FunctionNumber ) {
    long _result = 0;
    HRESULT _hr = raw_GetProcessInfo(FileName, FunctionNumber, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACExperimentInfo wrapper method implementations
//

inline long IDACExperimentInfo::GetExperimentInfo ( _bstr_t FileName ) {
    long _result = 0;
    HRESULT _hr = raw_GetExperimentInfo(FileName, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _bstr_t IDACExperimentInfo::GetExperimentText ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_ExperimentText(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

//
// interface IDACHeader wrapper method implementations
//

inline long IDACHeader::GetVersionMajor ( ) {
    long _result = 0;
    HRESULT _hr = get_VersionMajor(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACHeader::GetVersionMinor ( ) {
    long _result = 0;
    HRESULT _hr = get_VersionMinor(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _bstr_t IDACHeader::GetAcquName ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_AcquName(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetAcquDate ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_AcquDate(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetAcquTime ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_AcquTime(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetJobCode ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_JobCode(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetTaskCode ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_TaskCode(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetUserName ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_UserName(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetLabName ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_LabName(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetInstrument ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_Instrument(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetConditions ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_Conditions(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetSampleDesc ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_SampleDesc(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetSubmitter ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_Submitter(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetSampleID ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_SampleID(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetBottleNumber ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_BottleNumber(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline double IDACHeader::GetSolventDelay ( ) {
    double _result = 0;
    HRESULT _hr = get_SolventDelay(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACHeader::GetResolved ( ) {
    long _result = 0;
    HRESULT _hr = get_Resolved(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _bstr_t IDACHeader::GetPepFileName ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_PepFileName(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetProcess ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_Process(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline long IDACHeader::GetEncrypted ( ) {
    long _result = 0;
    HRESULT _hr = get_Encrypted(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACHeader::GetAutosamplerType ( ) {
    long _result = 0;
    HRESULT _hr = get_AutosamplerType(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _bstr_t IDACHeader::GetGasName ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_GasName(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetInstrumentType ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_InstrumentType(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACHeader::GetPlateDesc ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_PlateDesc(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _variant_t IDACHeader::GetAnalogOffset ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_AnalogOffset(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}

inline long IDACHeader::GetMuxStream ( ) {
    long _result = 0;
    HRESULT _hr = get_MuxStream(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline long IDACHeader::GetHeader ( _bstr_t FileName ) {
    long _result = 0;
    HRESULT _hr = raw_GetHeader(FileName, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

//
// interface IDACCalibrationInfo wrapper method implementations
//

inline long IDACCalibrationInfo::GetCalibration ( _bstr_t FileName ) {
    long _result = 0;
    HRESULT _hr = raw_GetCalibration(FileName, &_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _bstr_t IDACCalibrationInfo::GetMS1StaticFunction ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS1StaticFunction(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS2StaticFunction ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS2StaticFunction(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS1StaticParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS1StaticParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS1DynamicParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS1DynamicParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS1FastParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS1FastParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS2StaticParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS2StaticParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS2DynamicParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS2DynamicParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetMS2FastParams ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_MS2FastParams(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetCalTime ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_CalTime(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline _bstr_t IDACCalibrationInfo::GetCalDate ( ) {
    BSTR _result = 0;
    HRESULT _hr = get_CalDate(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _bstr_t(_result, false);
}

inline long IDACCalibrationInfo::GetNumCalFunctions ( ) {
    long _result = 0;
    HRESULT _hr = get_NumCalFunctions(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _result;
}

inline _variant_t IDACCalibrationInfo::GetCalFunctions ( ) {
    VARIANT _result;
    VariantInit(&_result);
    HRESULT _hr = get_CalFunctions(&_result);
    if (FAILED(_hr)) _com_issue_errorex(_hr, this, __uuidof(this));
    return _variant_t(_result, false);
}
