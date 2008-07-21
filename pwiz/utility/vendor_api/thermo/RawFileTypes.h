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
#include <vector>
#include <boost/algorithm/string/case_conv.hpp>

namespace pwiz {
namespace raw {


enum RAWFILE_API InstrumentModelType
{
    InstrumentModelType_Unknown = -1,

    // Finnigan MAT
    InstrumentModelType_MAT253,
    InstrumentModelType_MAT900XP,
    InstrumentModelType_MAT900XP_Trap,
    InstrumentModelType_MAT95XP,
    InstrumentModelType_MAT95XP_Trap,
    InstrumentModelType_SSQ_7000,
    InstrumentModelType_TSQ_7000,
    InstrumentModelType_TSQ,

    // Thermo Electron
    InstrumentModelType_Element2,

    // Thermo Finnigan
    InstrumentModelType_Delta_Plus_Advantage,
    InstrumentModelType_Delta_Plus_XP,
    InstrumentModelType_LCQ_Advantage,
    InstrumentModelType_LCQ_Classic,
    InstrumentModelType_LCQ_Deca,
    InstrumentModelType_LCQ_Deca_XP_Plus,
    InstrumentModelType_Neptune,
    InstrumentModelType_DSQ,
    InstrumentModelType_PolarisQ,
    InstrumentModelType_Surveyor_MSQ,
    InstrumentModelType_Tempus_TOF,
    InstrumentModelType_Trace_DSQ,
    InstrumentModelType_Triton,

    // Thermo Scientific
    InstrumentModelType_LTQ,
    InstrumentModelType_LTQ_FT,
    InstrumentModelType_LTQ_FT_Ultra,
    InstrumentModelType_LTQ_Orbitrap,
    InstrumentModelType_LTQ_Orbitrap_Discovery,
    InstrumentModelType_LTQ_Orbitrap_XL,
    InstrumentModelType_LXQ,
    InstrumentModelType_LCQ_Fleet,
    InstrumentModelType_ITQ_700,
    InstrumentModelType_ITQ_900,
    InstrumentModelType_ITQ_1100,
    InstrumentModelType_GC_Quantum,
    InstrumentModelType_LTQ_XL_ETD,
    InstrumentModelType_LTQ_Orbitrap_XL_ETD,
    InstrumentModelType_DFS,
    InstrumentModelType_DSQ_II,
    InstrumentModelType_MALDI_LTQ_XL,
    InstrumentModelType_MALDI_LTQ_Orbitrap,
    InstrumentModelType_TSQ_Quantum,
    InstrumentModelType_TSQ_Quantum_Access,
    InstrumentModelType_Element_XR,
    InstrumentModelType_Element_2,
    InstrumentModelType_Element_GD,
    InstrumentModelType_GC_IsoLink,
    InstrumentModelType_Exactive,
    InstrumentModelType_Surveyor_PDA,
    InstrumentModelType_Accela_PDA
};


inline InstrumentModelType parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string type = boost::to_upper_copy(instrumentModel);

    if (type == "MAT253")                       return InstrumentModelType_MAT253;
    else if (type == "MAT900XP")                return InstrumentModelType_MAT900XP;
    else if (type == "MAT900XP Trap")           return InstrumentModelType_MAT900XP_Trap;
    else if (type == "MAT95XP")                 return InstrumentModelType_MAT95XP;
    else if (type == "MAT95XP Trap")            return InstrumentModelType_MAT95XP_Trap;
    else if (type == "SSQ 7000")                return InstrumentModelType_SSQ_7000;
    else if (type == "TSQ 7000")                return InstrumentModelType_TSQ_7000;
    else if (type == "TSQ")                     return InstrumentModelType_TSQ;
    else if (type == "ELEMENT2")                return InstrumentModelType_Element2;
    else if (type == "DELTA PLUSADVANTAGE")     return InstrumentModelType_Delta_Plus_Advantage;
    else if (type == "DELTAPLUSXP")             return InstrumentModelType_Delta_Plus_XP;
	else if (type == "LCQ ADVANTAGE")           return InstrumentModelType_LCQ_Advantage;
    else if (type == "LCQ CLASSIC")             return InstrumentModelType_LCQ_Classic;
    else if (type == "LCQ DECA")                return InstrumentModelType_LCQ_Deca;
    else if (type == "LCQ DECA XP" ||
             type == "LCQ DECA XP PLUS")        return InstrumentModelType_LCQ_Deca_XP_Plus;
    else if (type == "NEPTUNE")                 return InstrumentModelType_Neptune;
    else if (type == "DSQ")                     return InstrumentModelType_DSQ;
    else if (type == "POLARISQ")                return InstrumentModelType_PolarisQ;
    else if (type == "SURVEYOR MSQ")            return InstrumentModelType_Surveyor_MSQ;
    else if (type == "TEMPUS TOF")              return InstrumentModelType_Tempus_TOF;
    else if (type == "TRACE DSQ")               return InstrumentModelType_Trace_DSQ;
    else if (type == "TRITON")                  return InstrumentModelType_Triton;
    else if (type == "LTQ" || type == "LTQ XL") return InstrumentModelType_LTQ;
    else if (type == "LTQ FT")                  return InstrumentModelType_LTQ_FT;
    else if (type == "LTQ FT ULTRA")            return InstrumentModelType_LTQ_FT_Ultra;
    else if (type == "LTQ ORBITRAP")            return InstrumentModelType_LTQ_Orbitrap;
    else if (type == "LTQ ORBITRAP DISCOVERY")  return InstrumentModelType_LTQ_Orbitrap_Discovery;
    else if (type == "LTQ ORBITRAP XL")         return InstrumentModelType_LTQ_Orbitrap_XL;
    else if (type == "LXQ")                     return InstrumentModelType_LXQ;
    else if (type == "LCQ FLEET")               return InstrumentModelType_LCQ_Fleet;
    else if (type == "ITQ 700")                 return InstrumentModelType_ITQ_700;
    else if (type == "ITQ 900")                 return InstrumentModelType_ITQ_900;
    else if (type == "ITQ 1100")                return InstrumentModelType_ITQ_1100;
    else if (type == "GC QUANTUM")              return InstrumentModelType_GC_Quantum;
    else if (type == "LTQ XL ETD")              return InstrumentModelType_LTQ_XL_ETD;
    else if (type == "LTQ ORBITRAP XL ETD")     return InstrumentModelType_LTQ_Orbitrap_XL_ETD;
    else if (type == "DFS")                     return InstrumentModelType_DFS;
    else if (type == "DSQ II")                  return InstrumentModelType_DSQ_II;
    else if (type == "MALDI LTQ XL")            return InstrumentModelType_MALDI_LTQ_XL;
    else if (type == "MALDI LTQ ORBITRAP")      return InstrumentModelType_MALDI_LTQ_Orbitrap;
    else if (type == "TSQ QUANTUM")             return InstrumentModelType_TSQ_Quantum;
    else if (type == "TSQ QUANTUM ACCESS")      return InstrumentModelType_TSQ_Quantum_Access;
    else if (type == "ELEMENT XR")              return InstrumentModelType_Element_XR;
    else if (type == "ELEMENT 2")               return InstrumentModelType_Element_2;
    else if (type == "ELEMENT GD")              return InstrumentModelType_Element_GD;
    else if (type == "GC ISOLINK")              return InstrumentModelType_GC_IsoLink;
    else if (type == "EXACTIVE")                return InstrumentModelType_Exactive;
    else if (type == "SURVEYOR PDA")            return InstrumentModelType_Surveyor_PDA;
    else if (type == "ACCELA PDA")              return InstrumentModelType_Accela_PDA;
    else
        return InstrumentModelType_Unknown;
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


inline std::vector<IonizationType> getIonSourcesForInstrumentModel(InstrumentModelType type)
{
    std::vector<IonizationType> ionSources;
    switch (type)
    {
        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_LTQ:
        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_Exactive:
            ionSources.push_back(IonizationType_ESI);
            break;

        case InstrumentModelType_DSQ:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_DFS:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_GC_IsoLink:
            ionSources.push_back(IonizationType_EI);
            break;


        case InstrumentModelType_MALDI_LTQ_XL:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
            ionSources.push_back(IonizationType_MALDI);
            break;

        case InstrumentModelType_Element_GD:
            ionSources.push_back(IonizationType_GD);
            break;

        case InstrumentModelType_Element2:
        case InstrumentModelType_Element_XR:
        case InstrumentModelType_Element_2:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_Tempus_TOF:
        case InstrumentModelType_Triton:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
            // TODO: get source information for these instruments
            break;
       
        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
        case InstrumentModelType_Unknown:
        default:
            break;
    }

    return ionSources;
}


enum RAWFILE_API ScanFilterMassAnalyzerType
{
    ScanFilterMassAnalyzerType_Unknown = -1,
    ScanFilterMassAnalyzerType_ITMS = 0,      // Ion Trap
    ScanFilterMassAnalyzerType_FTMS,          // Fourier Transform
    ScanFilterMassAnalyzerType_TOFMS,         // Time of Flight
    ScanFilterMassAnalyzerType_TQMS,          // Triple Quadrupole
    ScanFilterMassAnalyzerType_SQMS,          // Single Quadrupole
    ScanFilterMassAnalyzerType_Sector,        // Magnetic Sector
    ScanFilterMassAnalyzerType_Count
};


enum RAWFILE_API MassAnalyzerType
{
    MassAnalyzerType_Unknown = -1,
    MassAnalyzerType_Linear_Ion_Trap,
    MassAnalyzerType_Quadrupole_Ion_Trap,
    MassAnalyzerType_Single_Quadrupole,
    MassAnalyzerType_Triple_Quadrupole,
    MassAnalyzerType_TOF,
    MassAnalyzerType_Orbitrap,
    MassAnalyzerType_FTICR,
    MassAnalyzerType_Magnetic_Sector,
    MassAnalyzerType_Count
};


inline MassAnalyzerType convertScanFilterMassAnalyzer(ScanFilterMassAnalyzerType scanFilterType,
                                                      InstrumentModelType instrumentModel)
{
    switch (instrumentModel)
    {
        case InstrumentModelType_Exactive:
            return MassAnalyzerType_Orbitrap;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
            if (scanFilterType == ScanFilterMassAnalyzerType_FTMS)
                return MassAnalyzerType_Orbitrap;
            else 
                return MassAnalyzerType_Linear_Ion_Trap;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
            if (scanFilterType == ScanFilterMassAnalyzerType_FTMS)
                return MassAnalyzerType_FTICR;
            else 
                return MassAnalyzerType_Linear_Ion_Trap;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            return MassAnalyzerType_Single_Quadrupole;

        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_GC_Quantum:
            return MassAnalyzerType_Triple_Quadrupole;

        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
            return MassAnalyzerType_Quadrupole_Ion_Trap;

        case InstrumentModelType_LTQ:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
            return MassAnalyzerType_Linear_Ion_Trap;

        case InstrumentModelType_DFS:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
            return MassAnalyzerType_Magnetic_Sector;

        case InstrumentModelType_Tempus_TOF:
            return MassAnalyzerType_TOF;

        case InstrumentModelType_Element2:
        case InstrumentModelType_Element_XR:
        case InstrumentModelType_Element_2:
        case InstrumentModelType_Element_GD:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_Triton:
            // TODO: get mass analyzer information for these instruments
            return MassAnalyzerType_Unknown;
       
        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
        case InstrumentModelType_Unknown:
        default:
            return MassAnalyzerType_Unknown;
    }
}


inline std::vector<MassAnalyzerType> getMassAnalyzersForInstrumentModel(InstrumentModelType type)
{
    std::vector<MassAnalyzerType> massAnalyzers;
    switch (type)
    {
        case InstrumentModelType_Exactive:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            break;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            massAnalyzers.push_back(MassAnalyzerType_Linear_Ion_Trap);
            break;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
            massAnalyzers.push_back(MassAnalyzerType_FTICR);
            massAnalyzers.push_back(MassAnalyzerType_Linear_Ion_Trap);
            break;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            massAnalyzers.push_back(MassAnalyzerType_Single_Quadrupole);
            break;

        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_GC_Quantum:
            massAnalyzers.push_back(MassAnalyzerType_Triple_Quadrupole);
            break;

        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
            massAnalyzers.push_back(MassAnalyzerType_Quadrupole_Ion_Trap);
            break;

        case InstrumentModelType_LTQ:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
            massAnalyzers.push_back(MassAnalyzerType_Linear_Ion_Trap);
            break;

        case InstrumentModelType_DFS:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
            massAnalyzers.push_back(MassAnalyzerType_Magnetic_Sector);
            break;

        case InstrumentModelType_Tempus_TOF:
            massAnalyzers.push_back(MassAnalyzerType_TOF);
            break;

        case InstrumentModelType_Element2:
        case InstrumentModelType_Element_XR:
        case InstrumentModelType_Element_2:
        case InstrumentModelType_Element_GD:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_Triton:
            // TODO: get mass analyzer information for these instruments
            break;
       
        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
        case InstrumentModelType_Unknown:
        default:
            break;
    }

    return massAnalyzers;
}


enum RAWFILE_API DetectorType
{
    DetectorType_Unknown = -1,
    DetectorType_Electron_Multiplier,
    DetectorType_Inductive,
    DetectorType_Photo_Diode_Array,
    DetectorType_Count
};


inline std::vector<DetectorType> getDetectorsForInstrumentModel(InstrumentModelType type)
{
    std::vector<DetectorType> detectors;
    switch (type)
    {
        case InstrumentModelType_Exactive:
            detectors.push_back(DetectorType_Inductive);
            break;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
            detectors.push_back(DetectorType_Inductive);
            detectors.push_back(DetectorType_Electron_Multiplier);
            break;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_LTQ:
        case InstrumentModelType_MALDI_LTQ_XL:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_DFS:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_GC_IsoLink:
            detectors.push_back(DetectorType_Electron_Multiplier);
            break;

        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
            detectors.push_back(DetectorType_Photo_Diode_Array);

        case InstrumentModelType_Element_GD:
        case InstrumentModelType_Element2:
        case InstrumentModelType_Element_XR:
        case InstrumentModelType_Element_2:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_Tempus_TOF:
        case InstrumentModelType_Triton:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
            // TODO: get detector information for these instruments
            break;

        case InstrumentModelType_Unknown:
        default:
            break;
    }

    return detectors;
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
        case ScanType_SIM: return "Selected ion monitoring";
        case ScanType_SRM: return "Selected reaction monitoring";
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
