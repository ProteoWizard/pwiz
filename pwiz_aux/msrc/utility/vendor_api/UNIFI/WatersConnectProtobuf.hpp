//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2024 University of Washington
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//


#pragma once

using namespace System;
using namespace System::Runtime::InteropServices;

// DiscriminatedUnionObject (for "one of" support)
// Ported from newer protobuf-net code since we can't easily upgrade version in ProteoWizard
namespace ProtoBuf
{

/// note that it is the caller's responsibility to only read/write the value as the same type
[StructLayout(LayoutKind::Auto)]
public value struct DiscriminatedUnionObject
{
    public:
    property Object^ AnObject;

    bool Is(int discriminator) { return Discriminator == discriminator; }

    DiscriminatedUnionObject(int discriminator, Object^ value)
    {
        Discriminator = discriminator;
        AnObject = value;
    }

    static void Reset(DiscriminatedUnionObject% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnionObject();
    }

    property int Discriminator;
};

/// note that it is the caller's responsibility to only read/write the value as the same type
[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion64
{
    private:
    [FieldOffset(0)] int _discriminator;  // note that we can't pack further because Object needs x8 alignment/padding on x64

    [FieldOffset(8)] long long Int64;
    [FieldOffset(8)] unsigned long long UInt64;
    [FieldOffset(8)] int Int32;
    [FieldOffset(8)] unsigned int UInt32;
    [FieldOffset(8)] bool Boolean;
    [FieldOffset(8)] float Single;
    [FieldOffset(8)] double Double;
    [FieldOffset(8)] DateTime ADateTime;
    [FieldOffset(8)] TimeSpan ATimeSpan;

    DiscriminatedUnion64(int discriminator)
    {
        _discriminator = discriminator;
    }

    public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion64(int discriminator, long long value) : DiscriminatedUnion64(discriminator) { Int64 = value; }
    DiscriminatedUnion64(int discriminator, int value) : DiscriminatedUnion64(discriminator) { Int32 = value; }
    DiscriminatedUnion64(int discriminator, unsigned long long value) : DiscriminatedUnion64(discriminator) { UInt64 = value; }
    DiscriminatedUnion64(int discriminator, unsigned int value) : DiscriminatedUnion64(discriminator) { UInt32 = value; }
    DiscriminatedUnion64(int discriminator, float value) : DiscriminatedUnion64(discriminator) { Single = value; }
    DiscriminatedUnion64(int discriminator, double value) : DiscriminatedUnion64(discriminator) { Double = value; }
    DiscriminatedUnion64(int discriminator, bool value) : DiscriminatedUnion64(discriminator) { Boolean = value; }
    DiscriminatedUnion64(int discriminator, Nullable<DateTime> value) : DiscriminatedUnion64(value.HasValue ? discriminator : 0) { ADateTime = value.GetValueOrDefault(); }
    DiscriminatedUnion64(int discriminator, Nullable<TimeSpan> value) : DiscriminatedUnion64(value.HasValue ? discriminator : 0) { ATimeSpan = value.GetValueOrDefault(); }

    static void Reset(DiscriminatedUnion64% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion64();
    }

    property int Discriminator { int get() { return _discriminator; } }
};

[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion128Object
{
private:
    [FieldOffset(0)] int _discriminator;  // note that we can't pack further because Object needs x8 alignment/padding on x64

public:
    [FieldOffset(8)] long long Int64;
    [FieldOffset(8)] unsigned long long UInt64;
    [FieldOffset(8)] int Int32;
    [FieldOffset(8)] unsigned int UInt32;
    [FieldOffset(8)] bool Boolean;
    [FieldOffset(8)] float Single;
    [FieldOffset(8)] double Double;
    [FieldOffset(8)] DateTime ADateTime;
    [FieldOffset(8)] TimeSpan ATimeSpan;
    [FieldOffset(8)] Guid AGuid;
    [FieldOffset(24)] Object^ AnObject;

private:
    DiscriminatedUnion128Object(int discriminator) : _discriminator(discriminator), Int64(0), UInt64(0), Int32(0), UInt32(0), Boolean(false), Single(0.0f), Double(0.0), ADateTime(DateTime::MinValue), ATimeSpan(TimeSpan::Zero), AGuid(Guid::Empty), AnObject(nullptr) {}

public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion128Object(int discriminator, long long value) : DiscriminatedUnion128Object(discriminator) { Int64 = value; }
    DiscriminatedUnion128Object(int discriminator, int value) : DiscriminatedUnion128Object(discriminator) { Int32 = value; }
    DiscriminatedUnion128Object(int discriminator, unsigned long long value) : DiscriminatedUnion128Object(discriminator) { UInt64 = value; }
    DiscriminatedUnion128Object(int discriminator, unsigned int value) : DiscriminatedUnion128Object(discriminator) { UInt32 = value; }
    DiscriminatedUnion128Object(int discriminator, float value) : DiscriminatedUnion128Object(discriminator) { Single = value; }
    DiscriminatedUnion128Object(int discriminator, double value) : DiscriminatedUnion128Object(discriminator) { Double = value; }
    DiscriminatedUnion128Object(int discriminator, bool value) : DiscriminatedUnion128Object(discriminator) { Boolean = value; }
    DiscriminatedUnion128Object(int discriminator, Object^ value) : DiscriminatedUnion128Object(value != nullptr ? discriminator : 0) { AnObject = value; }
    DiscriminatedUnion128Object(int discriminator, Nullable<DateTime> value) : DiscriminatedUnion128Object(value.HasValue ? discriminator : 0) { ADateTime = value.GetValueOrDefault(); }
    DiscriminatedUnion128Object(int discriminator, Nullable<TimeSpan> value) : DiscriminatedUnion128Object(value.HasValue ? discriminator : 0) { ATimeSpan = value.GetValueOrDefault(); }
    DiscriminatedUnion128Object(int discriminator, Nullable<Guid> value) : DiscriminatedUnion128Object(value.HasValue ? discriminator : 0) { AGuid = value.GetValueOrDefault(); }

    static void Reset(DiscriminatedUnion128Object% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion128Object();
    }

    property int Discriminator { int get() { return _discriminator; } }
};

[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion128
{
    private:
    [FieldOffset(0)] int _discriminator;  // note that we can't pack further because Object needs x8 alignment/padding on x64

    public:
    [FieldOffset(8)] long Int64;
    [FieldOffset(8)] unsigned long long UInt64;
    [FieldOffset(8)] int Int32;
    [FieldOffset(8)] unsigned int UInt32;
    [FieldOffset(8)] bool Boolean;
    [FieldOffset(8)] float Single;
    [FieldOffset(8)] double Double;
    [FieldOffset(8)] DateTime ADateTime;
    [FieldOffset(8)] TimeSpan ATimeSpan;
    [FieldOffset(8)] Guid AGuid;

private:
    DiscriminatedUnion128(int discriminator) : _discriminator(discriminator), Int64(0), UInt64(0), Int32(0), UInt32(0), Boolean(false), Single(0.0f), Double(0.0), ADateTime(DateTime::MinValue), ATimeSpan(TimeSpan::Zero), AGuid(Guid::Empty) {}

public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion128(int discriminator, long value) : DiscriminatedUnion128(discriminator) { Int64 = value; }
    DiscriminatedUnion128(int discriminator, int value) : DiscriminatedUnion128(discriminator) { Int32 = value; }
    DiscriminatedUnion128(int discriminator, unsigned long long value) : DiscriminatedUnion128(discriminator) { UInt64 = value; }
    DiscriminatedUnion128(int discriminator, unsigned int value) : DiscriminatedUnion128(discriminator) { UInt32 = value; }
    DiscriminatedUnion128(int discriminator, float value) : DiscriminatedUnion128(discriminator) { Single = value; }
    DiscriminatedUnion128(int discriminator, double value) : DiscriminatedUnion128(discriminator) { Double = value; }
    DiscriminatedUnion128(int discriminator, bool value) : DiscriminatedUnion128(discriminator) { Boolean = value; }
    DiscriminatedUnion128(int discriminator, Nullable<DateTime> value) : DiscriminatedUnion128(value.HasValue ? discriminator : 0) { ADateTime = value.GetValueOrDefault(); }
    DiscriminatedUnion128(int discriminator, Nullable<TimeSpan> value) : DiscriminatedUnion128(value.HasValue ? discriminator : 0) { ATimeSpan = value.GetValueOrDefault(); }
    DiscriminatedUnion128(int discriminator, Nullable<Guid> value) : DiscriminatedUnion128(value.HasValue ? discriminator : 0) { AGuid = value.GetValueOrDefault(); }

    static void Reset(DiscriminatedUnion128% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion128();
    }
    property int Discriminator { int get() { return _discriminator; } }
};


[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion64Object
{
    private:
    [FieldOffset(0)] int _discriminator;  // note that we can't pack further because Object needs x8 alignment/padding on x64

    public:
    [FieldOffset(8)] long Int64;
    [FieldOffset(8)] unsigned long long UInt64;
    [FieldOffset(8)] int Int32;
    [FieldOffset(8)] unsigned int UInt32;
    [FieldOffset(8)] bool Boolean;
    [FieldOffset(8)] float Single;
    [FieldOffset(8)] double Double;
    [FieldOffset(8)] DateTime ADateTime;
    [FieldOffset(8)] TimeSpan ATimeSpan;
    [FieldOffset(16)] Object^ AnObject;

    private:
    DiscriminatedUnion64Object(int discriminator) : _discriminator(discriminator) {}

    public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion64Object(int discriminator, long value) : DiscriminatedUnion64Object(discriminator) { Int64 = value; }
    DiscriminatedUnion64Object(int discriminator, int value) : DiscriminatedUnion64Object(discriminator) { Int32 = value; }
    DiscriminatedUnion64Object(int discriminator, unsigned long long value) : DiscriminatedUnion64Object(discriminator) { UInt64 = value; }
    DiscriminatedUnion64Object(int discriminator, unsigned int value) : DiscriminatedUnion64Object(discriminator) { UInt32 = value; }
    DiscriminatedUnion64Object(int discriminator, float value) : DiscriminatedUnion64Object(discriminator) { Single = value; }
    DiscriminatedUnion64Object(int discriminator, double value) : DiscriminatedUnion64Object(discriminator) { Double = value; }
    DiscriminatedUnion64Object(int discriminator, bool value) : DiscriminatedUnion64Object(discriminator) { Boolean = value; }
    DiscriminatedUnion64Object(int discriminator, Object^ value) : DiscriminatedUnion64Object(value != nullptr ? discriminator : 0) { AnObject = value; }
    DiscriminatedUnion64Object(int discriminator, Nullable<DateTime> value) : DiscriminatedUnion64Object(value.HasValue ? discriminator : 0) { ADateTime = value.GetValueOrDefault(); }
    DiscriminatedUnion64Object(int discriminator, Nullable<TimeSpan> value) : DiscriminatedUnion64Object(value.HasValue ? discriminator : 0) { ATimeSpan = value.GetValueOrDefault(); }

    static void Reset(DiscriminatedUnion64Object% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion64Object();
    }
    property int Discriminator { int get() { return _discriminator; } }
};

/// note that it is the caller's responsbility to only read/write the value as the same type
[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion32
{
    private:
    [FieldOffset(0)] int _discriminator;

    public:
    [FieldOffset(4)] int Int32;
    [FieldOffset(4)] unsigned int UInt32;
    [FieldOffset(4)] bool Boolean;
    [FieldOffset(4)] float Single;

    private:
    DiscriminatedUnion32(int discriminator) : _discriminator(discriminator) {}

    public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion32(int discriminator, int value) : DiscriminatedUnion32(discriminator) { Int32 = value; }
    DiscriminatedUnion32(int discriminator, unsigned int value) : DiscriminatedUnion32(discriminator) { UInt32 = value; }
    DiscriminatedUnion32(int discriminator, float value) : DiscriminatedUnion32(discriminator) { Single = value; }
    DiscriminatedUnion32(int discriminator, bool value) : DiscriminatedUnion32(discriminator) { Boolean = value; }

    static void Reset(DiscriminatedUnion32% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion32();
    }
    property int Discriminator { int get() { return _discriminator; } }
};

/// note that it is the caller's responsbility to only read/write the value as the same type
[StructLayout(LayoutKind::Explicit)]
public value struct DiscriminatedUnion32Object
{
    private:
    [FieldOffset(0)] int _discriminator;

    public:
    [FieldOffset(4)] int Int32;
    [FieldOffset(4)] unsigned int UInt32;
    [FieldOffset(4)] bool Boolean;
    [FieldOffset(4)] float Single;
    [FieldOffset(8)] Object^ AnObject;

    private:
    DiscriminatedUnion32Object(int discriminator) : _discriminator(discriminator) {}

    public:
    bool Is(int discriminator) { return _discriminator == discriminator; }

    DiscriminatedUnion32Object(int discriminator, int value) : DiscriminatedUnion32Object(discriminator) { Int32 = value; }
    DiscriminatedUnion32Object(int discriminator, unsigned int value) : DiscriminatedUnion32Object(discriminator) { UInt32 = value; }
    DiscriminatedUnion32Object(int discriminator, float value) : DiscriminatedUnion32Object(discriminator) { Single = value; }
    DiscriminatedUnion32Object(int discriminator, bool value) : DiscriminatedUnion32Object(discriminator) { Boolean = value; }
    DiscriminatedUnion32Object(int discriminator, Object^ value) : DiscriminatedUnion32Object(value != nullptr ? discriminator : 0) { AnObject = value; }

    static void Reset(DiscriminatedUnion32Object% value, int discriminator)
    {
        if (value.Discriminator == discriminator) value = DiscriminatedUnion32Object();
    }
    property int Discriminator { int get() { return _discriminator; } }
};

} // ProtoBuf


enum class AdcAcquisitionModeDto;
ref class MzSpectrumItemDtoV2;
ref class ScanInfoDto;
ref class ProductInfoDto;
ref class TofPropertiesDto;
ref class MsCalibrationDto;
ref class TargetPropertiesDto;
enum class EnergyLevelDto;
ref class RangeDtoDouble;
enum class IonisationModeDto;
ref class MzSpectraDtoV2;

[ProtoBuf::ProtoContract()]
public ref class MzSpectraDtoV2Collection
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property System::Collections::Generic::List<MzSpectraDtoV2^>^ Items;

};

[ProtoBuf::ProtoContract()]
public ref class AdcIonResponseDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double MassOverCharge;

    [ProtoBuf::ProtoMember(2)]
    property int Charge;

    [ProtoBuf::ProtoMember(3)]
    property double AverageSingleIonResponse;

};

[ProtoBuf::ProtoContract()]
public ref class BasicMsPropertiesDtoV3
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double PrecursorMz;

    [ProtoBuf::ProtoMember(2)]
    property double ProductMz;

    [ProtoBuf::ProtoMember(3)]
    property String^ MassAnalyser;

    [ProtoBuf::ProtoMember(4)]
    property String^ ScanningMethod;

    [ProtoBuf::ProtoMember(5)]
    property double ScanTime;

    [ProtoBuf::ProtoMember(6)]
    property double InterScanDelay;

    [ProtoBuf::ProtoMember(7)]
    property cli::array<double>^ SetMasses;

    [ProtoBuf::ProtoMember(8)]
    property RangeDtoDouble^ AcquiredMOverZRange;

    [ProtoBuf::ProtoMember(9)]
    property IonisationModeDto IonisationMode;

    [ProtoBuf::ProtoMember(10)]
    property String^ IonisationType;

    [ProtoBuf::ProtoMember(11)]
    property double ConeVoltage;

};

[ProtoBuf::ProtoContract()]
public ref class CentroidSpectrumDataDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property cli::array<double>^ MOverZs;

    [ProtoBuf::ProtoMember(2)]
    property cli::array<double>^ MOverZStdDevs;

    [ProtoBuf::ProtoMember(3)]
    property cli::array<double>^ Intensities;

    [ProtoBuf::ProtoMember(4)]
    property cli::array<double>^ IntensityStdDevs;

    [ProtoBuf::ProtoMember(5)]
    property cli::array<double>^ MassIndexes;

    [ProtoBuf::ProtoMember(6)]
    property cli::array<double>^ MassIndexStdDevs;

    [ProtoBuf::ProtoMember(7)]
    property cli::array<int>^ AccurateFlagIndexes;

    [ProtoBuf::ProtoMember(8)]
    property cli::array<int>^ SaturatedFlagIndexes;

};

[ProtoBuf::ProtoContract()]
public ref class ContinuumSpectrumDataDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property cli::array<double>^ Masses;

    [ProtoBuf::ProtoMember(2)]
    property cli::array<double>^ Intensities;

};

[ProtoBuf::ProtoContract()]
public ref class FragmentationPropertiesDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property EnergyLevelDto EnergyLevelType;

    [ProtoBuf::ProtoMember(2)]
    property double CollisionEnergy;

};

[ProtoBuf::ProtoContract()]
public ref class MSTechniqueDtoV4
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property TargetPropertiesDto^ TargetProperties;

    [ProtoBuf::ProtoMember(2)]
    property BasicMsPropertiesDtoV3^ BasicMsProperties;

    [ProtoBuf::ProtoMember(3)]
    property MsCalibrationDto^ MsCalibration;

    [ProtoBuf::ProtoMember(4)]
    property bool IsLockMassData;

    [ProtoBuf::ProtoMember(5)]
    property FragmentationPropertiesDto^ FragmentationProperties;

    [ProtoBuf::ProtoMember(6)]
    property TofPropertiesDto^ TofProperties;

    [ProtoBuf::ProtoMember(30)]
    property String^ InstrumentId;

    [ProtoBuf::ProtoMember(31)]
    property String^ InstrumentInternalName;

};

[ProtoBuf::ProtoContract()]
public ref class MsCalibrationDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double Resolution;

};

[ProtoBuf::ProtoContract()]
public ref class MzPrecursorSpectrumItemDtoV2
{
    public:

    [ProtoBuf::ProtoMember(7)]
    property System::Collections::Generic::List<ProductInfoDto^>^ ProductsInfoes;

};

[ProtoBuf::ProtoContract()]
public ref class MzProductSpectrumItemDtoV2
{
    public:

    [ProtoBuf::ProtoMember(7)]
    property double PrecursorMz;

    [ProtoBuf::ProtoMember(8)]
    property double PrecursorIntensity;

    [ProtoBuf::ProtoMember(9)]
    property ScanInfoDto^ PrecursorScanInfo;

};

[ProtoBuf::ProtoContract()]
public ref class MzSpectraDtoV2
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property String^ ParentChannelId;

    [ProtoBuf::ProtoMember(2)]
    property String^ ChannelName;

    [ProtoBuf::ProtoMember(3)]
    property String^ Title;

    [ProtoBuf::ProtoMember(4)]
    property int TotalSpectrumCount;

    [ProtoBuf::ProtoMember(5)]
    property MSTechniqueDtoV4^ MsTechnique;

    [ProtoBuf::ProtoMember(6)]
    property System::Collections::Generic::List<MzSpectrumItemDtoV2^>^ Values;

    [ProtoBuf::ProtoMember(7)]
    property String^ CorrectionStatus;

};

[ProtoBuf::ProtoContract()]
public ref class MzSpectrumItemDtoV2
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property int ScanIndex;

    [ProtoBuf::ProtoMember(2)]
    property ContinuumSpectrumDataDto^ ContinuumSpectrumData;

    [ProtoBuf::ProtoMember(3)]
    property CentroidSpectrumDataDto^ CentroidSpectrumData;

    [ProtoBuf::ProtoMember(4)]
    property double RetentionTime;

    [ProtoBuf::ProtoMember(5)]
    property double SetMassMz;

    enum class SubtypeOneofCase
    {
        None = 0,
        MzProductSpectrumItemDtoV2 = 101,
        MzPrecursorSpectrumItemDtoV2 = 102,
    };

    ProtoBuf::DiscriminatedUnionObject^ __pbn__subtype;

    [ProtoBuf::ProtoMember(101)]
    property MzProductSpectrumItemDtoV2^ MzProductSpectrumItem
    {
        MzProductSpectrumItemDtoV2^ get() { return __pbn__subtype->Is(101) ? ((MzProductSpectrumItemDtoV2^)__pbn__subtype->AnObject) : nullptr; }
        void set(MzProductSpectrumItemDtoV2^ value) { __pbn__subtype = gcnew ProtoBuf::DiscriminatedUnionObject(101, value); }
    };
    bool ShouldSerializeMzProductSpectrumItemDtoV2() { return __pbn__subtype->Is(101);}
    //void ResetMzProductSpectrumItemDtoV2() { return ProtoBuf::DiscriminatedUnionObject::Reset(%__pbn__subtype, 101); }

    [ProtoBuf::ProtoMember(102)]
    property MzPrecursorSpectrumItemDtoV2^ MzPrecursorSpectrumItem
    {
        MzPrecursorSpectrumItemDtoV2^ get() { return __pbn__subtype->Is(102) ? ((MzPrecursorSpectrumItemDtoV2^)__pbn__subtype->AnObject) : nullptr; }
        void set(MzPrecursorSpectrumItemDtoV2^ value) { __pbn__subtype = gcnew ProtoBuf::DiscriminatedUnionObject(102, value); }
    };
    bool ShouldSerializeMzPrecursorSpectrumItemDtoV2() { return __pbn__subtype->Is(102); }
    //void ResetMzPrecursorSpectrumItemDtoV2() { return ProtoBuf::DiscriminatedUnionObject::Reset(__pbn__subtype, 102); }

    property SubtypeOneofCase SubtypeCase {
        SubtypeOneofCase get()
        {
            return (SubtypeOneofCase) __pbn__subtype->Discriminator;
        }
    }

    // ProteoWizard additions
    property MSTechniqueDtoV4^ MsTechnique;
    std::vector<double>* mzArray;
    std::vector<double>* intensityArray;
    std::vector<double>* driftTimeArray;

    virtual ~MzSpectrumItemDtoV2()
    {
        if (mzArray != nullptr) delete mzArray; mzArray = nullptr;
        if (intensityArray != nullptr) delete intensityArray; intensityArray = nullptr;
        if (driftTimeArray != nullptr) delete driftTimeArray; driftTimeArray = nullptr;
    }
    !MzSpectrumItemDtoV2() { delete this; }
};

[ProtoBuf::ProtoContract()]
public ref class ProductInfoDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double SetMassMz;

    [ProtoBuf::ProtoMember(2)]
    property double PrecursorMz;

    [ProtoBuf::ProtoMember(3)]
    property double PrecursorIntensity;

    [ProtoBuf::ProtoMember(4)]
    property System::Collections::Generic::List<ScanInfoDto^>^ ProductScanInfoes;

};

[ProtoBuf::ProtoContract()]
public ref class RangeDtoDouble
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double Start;

    [ProtoBuf::ProtoMember(2)]
    property double End;

};

[ProtoBuf::ProtoContract()]
public ref class ScanInfoDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property String^ ChannelId;

    [ProtoBuf::ProtoMember(2)]
    property int ScanIndex;

    [ProtoBuf::ProtoMember(3)]
    property double ScanRT;

};

[ProtoBuf::ProtoContract()]
public ref class TargetPropertiesDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property String^ TargetId;

    [ProtoBuf::ProtoMember(2)]
    property String^ TargetName;

    [ProtoBuf::ProtoMember(3)]
    property String^ TargetGroup;

};

[ProtoBuf::ProtoContract()]
public ref class TofPropertiesDto
{
    public:

    [ProtoBuf::ProtoMember(1)]
    property double TimeZero;

    [ProtoBuf::ProtoMember(2)]
    property double PusherFrequency;

    [ProtoBuf::ProtoMember(3)]
    property double Lteff;

    [ProtoBuf::ProtoMember(4)]
    property double Veff;

    [ProtoBuf::ProtoMember(5)]
    property AdcAcquisitionModeDto AdcAcquisitionMode;

    [ProtoBuf::ProtoMember(6)]
    property AdcIonResponseDto^ AdcIonResponse;

};

[ProtoBuf::ProtoContract()]
enum class AdcAcquisitionModeDto
{
    NotSet = 0,
    AdcPeakDetecting = 1,
    AdcAveraging = 2,
    TdcEdgeDetecting = 3,
    TdcPeakTop = 4,
};

[ProtoBuf::ProtoContract()]
enum class EnergyLevelDto
{
    EnergyLevelDtoLow = 0,
    EnergyLevelDtoHigh = 1,
    EnergyLevelDtoNotSet = 2,
};

[ProtoBuf::ProtoContract()]
enum class IonisationModeDto
{
    IonisationModeDtoPositive = 0,
    IonisationModeDtoNegative = 1,
};
