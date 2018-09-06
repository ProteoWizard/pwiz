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

#include "RawFileValues.h"

using namespace XRawfile;
using namespace std;


namespace pwiz {
namespace vendor_api {
namespace Thermo {
namespace RawFileValues {

ValueDescriptor<ValueID_Long> ValueID_Long_descriptors[] =
{
    {VersionNumber, &IXRawfile::GetVersionNumber, "VersionNumber"},
    {IsError, &IXRawfile::IsError, "IsError"},
    {IsNewFile, &IXRawfile::IsNewFile, "IsNewFile"},
    {ErrorCode, &IXRawfile::GetErrorCode, "ErrorCode"},
    {SeqRowNumber, &IXRawfile::GetSeqRowNumber, "SeqRowNumber"},
    {SeqRowSampleType, &IXRawfile::GetSeqRowSampleType, "SeqRowSampleType"},
    {InAcquisition, &IXRawfile::InAcquisition, "InAcquisition"},
    {NumberOfControllers, &IXRawfile::GetNumberOfControllers, "NumberOfControllers"},
    {NumSpectra, &IXRawfile::GetNumSpectra, "NumSpectra"},
    {NumStatusLog, &IXRawfile::GetNumStatusLog, "NumStatusLog"},
    {NumErrorLog, &IXRawfile::GetNumErrorLog, "NumErrorLog"},
    {NumTuneData, &IXRawfile::GetNumTuneData, "NumTuneData"},
    {NumTrailerExtra, &IXRawfile::GetNumTrailerExtra, "NumTrailerExtra"},
    {MaxIntensity, &IXRawfile::GetMaxIntensity, "MaxIntensity"},
    {FirstSpectrumNumber, &IXRawfile::GetFirstSpectrumNumber, "FirstSpectrumNumber"},
    {LastSpectrumNumber, &IXRawfile::GetLastSpectrumNumber, "LastSpectrumNumber"},
    {InstrumentID, &IXRawfile::GetInstrumentID, "InstrumentID"},
    {InletID, &IXRawfile::GetInletID, "InletID"},
    {ErrorFlag, &IXRawfile::GetErrorFlag, "ErrorFlag"},
    {VialNumber, &IXRawfile::GetVialNumber, "VialNumber"},
    {NumInstMethods, &IXRawfile::GetNumInstMethods, "NumInstMethods"},
    {InstNumChannelLabels, &IXRawfile::GetInstNumChannelLabels, "InstNumChannelLabels"},
    {IsThereMSData, &IXRawfile::IsThereMSData, "IsThereMSData"},
    {HasExpMethod, &IXRawfile::HasExpMethod, "HasExpMethod"},
    {FilterMassPrecision, &IXRawfile::GetFilterMassPrecision, "FilterMassPrecision"},
    {ValueID_Long_Count, 0, 0}
};

ValueDescriptor<ValueID_Double> ValueID_Double_descriptors[] =
{
    {SeqRowInjectionVolume, &IXRawfile::GetSeqRowInjectionVolume, "SeqRowInjectionVolume"},
    {SeqRowSampleWeight, &IXRawfile::GetSeqRowSampleWeight, "SeqRowSampleWeight"},
    {SeqRowSampleVolume, &IXRawfile::GetSeqRowSampleVolume, "SeqRowSampleVolume"},
    {SeqRowISTDAmount, &IXRawfile::GetSeqRowISTDAmount, "SeqRowISTDAmount"},
    {SeqRowDilutionFactor, &IXRawfile::GetSeqRowDilutionFactor, "SeqRowDilutionFactor"},
    {MassResolution, &IXRawfile::GetMassResolution, "MassResolution"},
    {ExpectedRunTime, &IXRawfile::GetExpectedRunTime, "ExpectedRunTime"},
    {LowMass, &IXRawfile::GetLowMass, "LowMass"},
    {HighMass, &IXRawfile::GetHighMass, "HighMass"},
    {StartTime, &IXRawfile::GetStartTime, "StartTime"},
    {EndTime, &IXRawfile::GetEndTime, "EndTime"},
    {MaxIntegratedIntensity, &IXRawfile::GetMaxIntegratedIntensity, "MaxIntegratedIntensity"},
    {SampleVolume, &IXRawfile::GetSampleVolume, "SampleVolume"},
    {SampleWeight, &IXRawfile::GetSampleWeight, "SampleWeight"},
    {InjectionVolume, &IXRawfile::GetInjectionVolume, "InjectionVolume"},
    {ValueID_Double_Count, 0, 0}
};


ValueDescriptor<ValueID_String> ValueID_String_descriptors[] =
{
    {FileName, &IXRawfile::GetFileName, "FileName"},
    {CreatorID, &IXRawfile::GetCreatorID, "CreatorID"},
    {ErrorMessage, &IXRawfile::GetErrorMessage, "ErrorMessage"},
    {WarningMessage, &IXRawfile::GetWarningMessage, "WarningMessage"},
    {SeqRowDataPath, &IXRawfile::GetSeqRowDataPath, "SeqRowDataPath"},
    {SeqRowRawFileName, &IXRawfile::GetSeqRowRawFileName, "SeqRowRawFileName"},
    {SeqRowSampleName, &IXRawfile::GetSeqRowSampleName, "SeqRowSampleName"},
    {SeqRowSampleID, &IXRawfile::GetSeqRowSampleID, "SeqRowSampleID"},
    {SeqRowComment, &IXRawfile::GetSeqRowComment, "SeqRowComment"},
    {SeqRowLevelName, &IXRawfile::GetSeqRowLevelName, "SeqRowLevelName"},
    {SeqRowInstrumentMethod, &IXRawfile::GetSeqRowInstrumentMethod, "SeqRowInstrumentMethod"},
    {SeqRowProcessingMethod, &IXRawfile::GetSeqRowProcessingMethod, "SeqRowProcessingMethod"},
    {SeqRowCalibrationFile, &IXRawfile::GetSeqRowCalibrationFile, "SeqRowCalibrationFile"},
    {SeqRowVial, &IXRawfile::GetSeqRowVial, "SeqRowVial"},
    {Flags, &IXRawfile::GetFlags, "Flags"},
    {AcquisitionFileName, &IXRawfile::GetAcquisitionFileName, "AcquisitionFileName"},
    {InstrumentDescription, &IXRawfile::GetInstrumentDescription, "InstrumentDescription"},
    {AcquisitionDate, &IXRawfile::GetAcquisitionDate, "AcquisitionDate"},
    {Operator, &IXRawfile::GetOperator, "Operator"},
    {Comment1, &IXRawfile::GetComment1, "Comment1"},
    {Comment2, &IXRawfile::GetComment2, "Comment2"},
    {SampleAmountUnits, &IXRawfile::GetSampleAmountUnits, "SampleAmountUnits"},
    {InjectionAmountUnits, &IXRawfile::GetInjectionAmountUnits, "InjectionAmountUnits"},
    {SampleVolumeUnits, &IXRawfile::GetSampleVolumeUnits, "SampleVolumeUnits"},
    {InstName, &IXRawfile::GetInstName, "InstName"},
    {InstModel, &IXRawfile::GetInstModel, "InstModel"},
    {InstSerialNumber, &IXRawfile::GetInstSerialNumber, "InstSerialNumber"},
    {InstSoftwareVersion, &IXRawfile::GetInstSoftwareVersion, "InstSoftwareVersion"},
    {InstHardwareVersion, &IXRawfile::GetInstHardwareVersion, "InstHardwareVersion"},
    {InstFlags, &IXRawfile::GetInstFlags, "InstFlags"},
    {ValueID_String_Count, 0, 0}
};


ValueData<ValueID_Long>::ValueData()
{
    ValueDescriptor<ValueID_Long>* vd = ValueID_Long_descriptors;
    for (;!(vd->function == 0 && vd->name == 0); vd++)
        descriptors_.push_back(*vd);
    for (vector<ValueDescriptor<ValueID_Long> >::const_iterator itr = descriptors_.begin();
        itr != descriptors_.end();
        ++itr)
        descriptorMap_[itr->id] = &*itr;
}

ValueData<ValueID_Double>::ValueData()
{
    ValueDescriptor<ValueID_Double>* vd = ValueID_Double_descriptors;
    for (;!(vd->function == 0 && vd->name == 0); vd++)
        descriptors_.push_back(*vd);
    for (vector<ValueDescriptor<ValueID_Double> >::const_iterator itr = descriptors_.begin();
        itr != descriptors_.end();
        ++itr)
        descriptorMap_[itr->id] = &*itr;
}

ValueData<ValueID_String>::ValueData()
{
    ValueDescriptor<ValueID_String>* vd = ValueID_String_descriptors;
    for (;!(vd->function == 0 && vd->name == 0); vd++)
        descriptors_.push_back(*vd);
    for (vector<ValueDescriptor<ValueID_String> >::const_iterator itr = descriptors_.begin();
        itr != descriptors_.end();
        ++itr)
        descriptorMap_[itr->id] = &*itr;
}


} // namespace RawFileValues
} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz
