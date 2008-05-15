#ifndef _RAWFILETYPES_H_
#define _RAWFILETYPES_H_

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

#include <string>
#include <boost/algorithm/string/case_conv.hpp>

namespace pwiz {
namespace raw {


enum RAWFILE_API InstrumentModelType
{
    InstrumentModelType_Unknown = -1,
    InstrumentModelType_LCQ,
    InstrumentModelType_LCQ_Advantage,
    InstrumentModelType_LCQ_Classic,
    InstrumentModelType_LCQ_Deca,
    InstrumentModelType_LCQ_Deca_XP,
    InstrumentModelType_LCQ_Deca_XP_Plus,
    InstrumentModelType_LCQ_Fleet,
    InstrumentModelType_LTQ,
    InstrumentModelType_LTQ_XL,
    InstrumentModelType_LTQ_FT,
    InstrumentModelType_LTQ_FT_Ultra,
    InstrumentModelType_LTQ_Orbitrap,
    InstrumentModelType_LTQ_Orbitrap_Discovery,
    InstrumentModelType_LTQ_Orbitrap_XL,
    InstrumentModelType_LXQ,
    InstrumentModelType_TSQ_Quantum,
    InstrumentModelType_TSQ_Quantum_Access,
    InstrumentModelType_GC_Quantum,
    InstrumentModelType_Delta_Plus_XP,
    InstrumentModelType_Delta_Plus_Advantage,
    InstrumentModelType_ELEMENT2,
    InstrumentModelType_MAT253,
    InstrumentModelType_MAT900XP,
    InstrumentModelType_MAT900XP_Trap,
    InstrumentModelType_MAT95XP,
    InstrumentModelType_MAT95XP_Trap,
    InstrumentModelType_Neptune,
    InstrumentModelType_PolarisQ,
    InstrumentModelType_Surveyor_MSQ,
    InstrumentModelType_Surveyor_PDA,
    InstrumentModelType_Tempus_TOF,
    InstrumentModelType_Trace_DSQ,
    InstrumentModelType_Triton,
    InstrumentModelType_Accela_PDA
};


inline InstrumentModelType parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string type = boost::to_upper_copy(instrumentModel);

    if (type == "LCQ")                          return InstrumentModelType_LCQ;
	else if (type == "LCQ ADVANTAGE")           return InstrumentModelType_LCQ_Advantage;
	else if (type == "LCQ CLASSIC")             return InstrumentModelType_LCQ_Classic;
	else if (type == "LCQ DECA")                return InstrumentModelType_LCQ_Deca;
	else if (type == "LCQ DECA XP")             return InstrumentModelType_LCQ_Deca_XP;
	else if (type == "LCQ DECA XP PLUS")        return InstrumentModelType_LCQ_Deca_XP_Plus;
	else if (type == "LCQ FLEET")               return InstrumentModelType_LCQ_Fleet;
    else if (type == "LTQ")                     return InstrumentModelType_LTQ;
	else if (type == "LTQ XL")                  return InstrumentModelType_LTQ_XL;
	else if (type == "LTQ FT")                  return InstrumentModelType_LTQ_FT;
	else if (type == "LTQ FT ULTRA")            return InstrumentModelType_LTQ_FT_Ultra;
	else if (type == "LTQ ORBITRAP")            return InstrumentModelType_LTQ_Orbitrap;
	else if (type == "LTQ ORBITRAP DISCOVERY")  return InstrumentModelType_LTQ_Orbitrap_Discovery;
	else if (type == "LTQ ORBITRAP XL")         return InstrumentModelType_LTQ_Orbitrap_XL;
	else if (type == "LXQ")                     return InstrumentModelType_LXQ;
	else if (type == "TSQ QUANTUM")             return InstrumentModelType_TSQ_Quantum;
    else if (type == "TSQ QUANTUM ACCESS")      return InstrumentModelType_TSQ_Quantum_Access;
    else
        return InstrumentModelType_Unknown;
}


enum RAWFILE_API MassAnalyzerType
{
    MassAnalyzerType_Unknown = -1,
    MassAnalyzerType_ITMS = 0,      // Ion Trap
    MassAnalyzerType_FTMS,          // Fourier Transform
    MassAnalyzerType_TOFMS,         // Time of Flight
    MassAnalyzerType_TQMS,          // Triple Quadrupole
    MassAnalyzerType_SQMS,          // Single Quadrupole
    MassAnalyzerType_Sector,        // Magnetic Sector
    MassAnalyzerType_Count
};


inline std::string toString(MassAnalyzerType type)
{
    switch (type)
    {
        case MassAnalyzerType_ITMS: return "ITMS";
        case MassAnalyzerType_FTMS: return "FTMS";
        case MassAnalyzerType_TOFMS: return "TOFMS";
        case MassAnalyzerType_TQMS: return "TQMS";
        case MassAnalyzerType_SQMS: return "SQMS";
        case MassAnalyzerType_Sector: return "Sector";
        case MassAnalyzerType_Unknown: default: return "Unknown";
    }
}


enum RAWFILE_API IonizationType
{
    IonizationType_Unknown = -1,
    IonizationType_EI = 0,       // Electron Ionization
    IonizationType_CI,           // Chemical Ionization
    IonizationType_FAB,          // Fast Atom Bombardment
    IonizationType_ESI,          // Electrospray Ionization
    IonizationType_NSI,          // Nanospray Ionization
    IonizationType_APCI,         // Atmospheric Pressure Chemical Ionization
    IonizationType_TSP,          // Thermospray
    IonizationType_FD,           // Field Desorption
    IonizationType_MALDI,        // Matrix-assisted Laser Desorption Ionization
    IonizationType_GD,           // Glow Discharge
    IonizationType_Count
};


inline std::string toString(IonizationType type)
{
    switch (type)
    {
        case IonizationType_EI: return "Electron Impact";
        case IonizationType_CI: return "Chemical Ionization";
        case IonizationType_FAB: return "Fast Atom Bombardment";
        case IonizationType_ESI: return "Electrospray Ionization";
        case IonizationType_NSI: return "Nanospray Ionization";
        case IonizationType_APCI: return "Atmospheric Pressure Chemical Ionization";
        case IonizationType_TSP: return "Thermospray";
        case IonizationType_FD: return "Field Desorption";
        case IonizationType_MALDI: return "Matrix-assisted Laser Desorption Ionization";
        case IonizationType_GD: return "Glow Discharge";
        case IonizationType_Unknown: default: return "Unknown";
    }
}


enum RAWFILE_API ActivationType
{
    ActivationType_Unknown = -1,
    ActivationType_CID = 0,     // Collision Induced Dissociation
    ActivationType_ETD,         // Electron Transfer Dissociation
    ActivationType_ECD,         // Electron Capture Dissociation
    ActivationType_MPD,         // TODO: what is this?
    ActivationType_PQD,         // Pulsed Q Dissociation
    ActivationType_HCD,         // High Energy CID
    ActivationType_SA,          // Supplemental CID
    ActivationType_PTR,         // Proton Transfer Reaction
    ActivationType_Count
};


inline std::string toString(ActivationType type)
{
    switch (type)
    {
        case ActivationType_CID: return "Collision Induced Dissociation";
        case ActivationType_ETD: return "Electron Transfer Dissociation";
        case ActivationType_ECD: return "Electron Capture Dissociation";
        case ActivationType_MPD: return "MPD"; // TODO: what is this?
        case ActivationType_PQD: return "Pulsed Q Dissociation";
        case ActivationType_HCD: return "High Energy CID";
        case ActivationType_SA: return "Supplemental CID";
        case ActivationType_PTR: return "Proton Transfer Reaction";
        case ActivationType_Unknown: default: return "Unknown";
    }
}


enum RAWFILE_API ScanType
{
    ScanType_Unknown = -1,
    ScanType_Full = 0,
    ScanType_SIM,
    ScanType_SRM,
    ScanType_CRM,
    ScanType_Q1MS,
    ScanType_Q3MS,
    ScanType_Zoom,
    ScanType_Count
};


inline std::string toString(ScanType type)
{
    switch (type)
    {
        case ScanType_Full: return "Full";
        case ScanType_SIM: return "Single ion monitoring";
        case ScanType_SRM: return "Single reaction monitoring";
        case ScanType_CRM: return "Constant reaction monitoring";
        case ScanType_Q1MS: return "Q1MS";
        case ScanType_Q3MS: return "Q3MS";
        case ScanType_Zoom: return "Zoom";
        case ScanType_Unknown: default: return "Unknown";
    }
}


enum RAWFILE_API PolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Positive = 0,
    PolarityType_Negative,
    PolarityType_Count
};


inline std::string toString(PolarityType type)
{
    switch (type)
    {
        case PolarityType_Positive: return "+";
        case PolarityType_Negative: return "-";
        case PolarityType_Unknown: default: return "Unknown";
    }
}


enum RAWFILE_API DataPointType
{
	DataPointType_Unknown = -1,
	DataPointType_Centroid = 0,
	DataPointType_Profile,
    DataPointType_Count
};


enum RAWFILE_API AccurateMassType
{
	AccurateMass_Unknown = -1,
	AccurateMass_NotActive = 0,                 // NOTE: in filter as "!AM": accurate mass not active
	AccurateMass_Active,                        // accurate mass active 
	AccurateMass_ActiveWithInternalCalibration, // accurate mass with internal calibration
	AccurateMass_ActiveWithExternalCalibration  // accurate mass with external calibration
};


enum RAWFILE_API TriBool
{
	TriBool_Unknown = -1,
	TriBool_False = 0,
	TriBool_True = 1
};

} // raw
} // pwiz

#endif // _RAWFILETYPES_H_
