//
// CompassDataEnums.hpp
//
// Extracted Bruker CompassData enums for cleaner separation.
//
// Created to hold enums originally in CompassData.hpp so they can be
// included from multiple translation units without dragging in large headers.
//
#ifndef _COMPASSDATA_ENUMS_HPP_
#define _COMPASSDATA_ENUMS_HPP_

#include "pwiz/utility/misc/Export.hpp"

namespace pwiz {
namespace vendor_api {
namespace Bruker {

PWIZ_API_DECL enum SpectrumType
{
    SpectrumType_Line = 0,
    SpectrumType_Profile = 1
};

PWIZ_API_DECL enum IonPolarity
{
    IonPolarity_Positive = 0,
    IonPolarity_Negative = 1,
    IonPolarity_Unknown = 255
};

PWIZ_API_DECL enum FragmentationMode
{
    FragmentationMode_Off = 0,
    FragmentationMode_CID = 1,
    FragmentationMode_ETD = 2,
    FragmentationMode_CIDETD_CID = 3,
    FragmentationMode_CIDETD_ETD = 4,
    FragmentationMode_ISCID = 5,
    FragmentationMode_ECD = 6,
    FragmentationMode_IRMPD = 7,
    FragmentationMode_PTR = 8,
    FragmentationMode_Unknown = 255
};

PWIZ_API_DECL enum InstrumentFamily
{
    InstrumentFamily_Trap = 0,
    InstrumentFamily_OTOF = 1,
    InstrumentFamily_OTOFQ = 2,
    InstrumentFamily_BioTOF = 3,
    InstrumentFamily_BioTOFQ = 4,
    InstrumentFamily_MaldiTOF = 5,
    InstrumentFamily_FTMS = 6,
    InstrumentFamily_maXis = 7,
    InstrumentFamily_timsTOF = 9, // not from CXT
    InstrumentFamily_impact = 90, // not from CXT
    InstrumentFamily_compact = 91, // not from CXT
    InstrumentFamily_solariX = 92, // not from CXT
    InstrumentFamily_Unknown = 255
};

PWIZ_API_DECL enum IsolationMode
{
    IsolationMode_Off = 0,
    IsolationMode_On = 1,
    IsolationMode_Unknown = 255
};

PWIZ_API_DECL enum class MsMsType // not from CXT
{
    MS1 = 0,
    MRM = 2,
    DDA_PASEF = 8,
    DIA_PASEF = 9,
    PRM_PASEF = 10
};

PWIZ_API_DECL enum InstrumentSource // not from CXT
{
    InstrumentSource_AlsoUnknown = 0,
    InstrumentSource_ESI = 1,
    InstrumentSource_APCI = 2,
    InstrumentSource_NANO_ESI_OFFLINE = 3,
    InstrumentSource_NANO_ESI_ONLINE = 4,
    InstrumentSource_APPI = 5,
    InstrumentSource_AP_MALDI = 6,
    InstrumentSource_MALDI = 7,
    InstrumentSource_MULTI_MODE = 8,
    InstrumentSource_NANO_FLOW_ESI = 9,
    InstrumentSource_Ultraspray = 10,
    InstrumentSource_CaptiveSpray = 11,
    InstrumentSource_EI = 16,
    InstrumentSource_GC_APCI = 17,
    InstrumentSource_VIP_HESI = 18,
    InstrumentSource_VIP_APCI = 19,
    InstrumentSource_Unknown = 255
};

PWIZ_API_DECL enum LCUnit
{
    LCUnit_NanoMeter = 1,
    LCUnit_MicroLiterPerMinute,
    LCUnit_Bar,
    LCUnit_Percent,
    LCUnit_Kelvin,
    LCUnit_Intensity,
    LCUnit_Unknown = 7
};

PWIZ_API_DECL enum DetailLevel
{
    DetailLevel_InstantMetadata,
    DetailLevel_FullMetadata,
    DetailLevel_FullData
};

PWIZ_API_DECL enum class TraceType
{
    NoneTrace = 0,
    ChromMS = 1,
    ChromUV = 3,
    ChromPressure = 4,
    ChromSolventMix = 5,
    ChromFlow = 6,
    ChromTemperature = 7,
    ChromUserDefined = 9999
};

PWIZ_API_DECL enum class TraceUnit
{
    NoneUnit = 0,
    Length_nm = 1,
    Flow_mul_min = 2,
    Pressure_bar = 3,
    Percent = 4,
    Temperature_C = 5,
    Intensity = 6,
    UnknownUnit = 7,
    Absorbance_AU = 8,
    Absorbance_mAU = 9,
    Counts = 10,
    Current_A = 11,
    Current_mA = 12,
    Current_muA = 13,
    Flow_ml_min = 14,
    Flow_nl_min = 15,
    Length_cm = 16,
    Length_mm = 17,
    Length_mum = 18,
    Luminescence = 20,
    Molarity_mM = 21,
    Power_W = 22,
    Power_mW = 23,
    Pressure_mbar = 24,
    Pressure_kPa = 25,
    Pressure_MPa = 26,
    Pressure_psi = 27,
    RefractiveIndex = 28,
    Temperature_F = 30,
    Time_h = 31,
    Time_min = 32,
    Time_s = 33,
    Time_ms = 34,
    Time_mus = 35,
    Viscosity_cP = 36,
    Voltage_kV = 37,
    Voltage_V = 38,
    Voltage_mV = 39,
    Volume_l = 40,
    Volume_ml = 41,
    Volume_mul = 42,
    Energy_J = 43,
    Energy_mJ = 44,
    Energy_muJ = 45,
    Length_Angstrom = 46,
};

} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz

#endif // _COMPASSDATA_ENUMS_HPP_
