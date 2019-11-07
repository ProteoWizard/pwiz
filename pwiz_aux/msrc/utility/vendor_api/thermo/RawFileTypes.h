//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _RAWFILETYPES_H_
#define _RAWFILETYPES_H_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <vector>
#include <boost/algorithm/string/case_conv.hpp>
#include <boost/algorithm/string/predicate.hpp>
namespace bal = boost::algorithm;

namespace pwiz {
namespace vendor_api {
namespace Thermo {


enum PWIZ_API_DECL InstrumentModelType
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
    InstrumentModelType_Element_2,

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
    InstrumentModelType_LTQ_Velos,
    InstrumentModelType_LTQ_Velos_Plus,
    InstrumentModelType_LTQ_FT,
    InstrumentModelType_LTQ_FT_Ultra,
    InstrumentModelType_LTQ_Orbitrap,
    InstrumentModelType_LTQ_Orbitrap_Discovery,
    InstrumentModelType_LTQ_Orbitrap_XL,
    InstrumentModelType_LTQ_Orbitrap_Velos,
    InstrumentModelType_LTQ_Orbitrap_Elite,
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
    InstrumentModelType_ISQ,
    InstrumentModelType_MALDI_LTQ_XL,
    InstrumentModelType_MALDI_LTQ_Orbitrap,
    InstrumentModelType_TSQ_Quantum,
    InstrumentModelType_TSQ_Quantum_Access,
    InstrumentModelType_TSQ_Quantum_Ultra,
    InstrumentModelType_TSQ_Quantum_Ultra_AM,
    InstrumentModelType_TSQ_Vantage_Standard,
    InstrumentModelType_TSQ_Vantage_EMR,
    InstrumentModelType_Element_XR,
    InstrumentModelType_Element_GD,
    InstrumentModelType_GC_IsoLink,
    InstrumentModelType_Exactive,
    InstrumentModelType_Q_Exactive,
    InstrumentModelType_Surveyor_PDA,
    InstrumentModelType_Accela_PDA,
    InstrumentModelType_Orbitrap_Fusion,
    InstrumentModelType_Orbitrap_Fusion_ETD,
    InstrumentModelType_TSQ_Quantiva,
    InstrumentModelType_TSQ_Endura,
    InstrumentModelType_TSQ_Altis,
    InstrumentModelType_Orbitrap_Exploris_480,
    InstrumentModelType_Orbitrap_Eclipse,

    InstrumentModelType_Count,
};


inline InstrumentModelType parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string type = bal::to_upper_copy(instrumentModel);

    if (type == "MAT253")                       return InstrumentModelType_MAT253;
    else if (type == "MAT900XP")                return InstrumentModelType_MAT900XP;
    else if (type == "MAT900XP Trap")           return InstrumentModelType_MAT900XP_Trap;
    else if (type == "MAT95XP")                 return InstrumentModelType_MAT95XP;
    else if (type == "MAT95XP Trap")            return InstrumentModelType_MAT95XP_Trap;
    else if (type == "SSQ 7000")                return InstrumentModelType_SSQ_7000;
    else if (type == "TSQ 7000")                return InstrumentModelType_TSQ_7000;
    else if (type == "TSQ")                     return InstrumentModelType_TSQ;
    else if (type == "ELEMENT2" ||
             type == "ELEMENT 2")               return InstrumentModelType_Element_2;
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
    else if (type == "MSQ PLUS")                return InstrumentModelType_Surveyor_MSQ;
    else if (type == "TEMPUS TOF")              return InstrumentModelType_Tempus_TOF;
    else if (type == "TRACE DSQ")               return InstrumentModelType_Trace_DSQ;
    else if (type == "TRITON")                  return InstrumentModelType_Triton;
    else if (type == "LTQ" || type == "LTQ XL") return InstrumentModelType_LTQ;
    else if (type == "LTQ FT" || type == "LTQ-FT") return InstrumentModelType_LTQ_FT;
    else if (type == "LTQ FT ULTRA")            return InstrumentModelType_LTQ_FT_Ultra;
    else if (type == "LTQ ORBITRAP")            return InstrumentModelType_LTQ_Orbitrap;
    else if (type == "LTQ ORBITRAP DISCOVERY")  return InstrumentModelType_LTQ_Orbitrap_Discovery;
    else if (type == "LTQ ORBITRAP XL")         return InstrumentModelType_LTQ_Orbitrap_XL;
    else if (bal::contains(type, "ORBITRAP VELOS")) return InstrumentModelType_LTQ_Orbitrap_Velos;
    else if (bal::contains(type, "ORBITRAP ELITE")) return InstrumentModelType_LTQ_Orbitrap_Elite;
    else if (bal::contains(type, "VELOS PLUS")) return InstrumentModelType_LTQ_Velos_Plus;
    else if (bal::contains(type, "VELOS PRO"))  return InstrumentModelType_LTQ_Velos_Plus;
    else if (type == "LTQ VELOS")               return InstrumentModelType_LTQ_Velos;
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
    else if (type == "ISQ SERIES")              return InstrumentModelType_ISQ;
    else if (type == "MALDI LTQ XL")            return InstrumentModelType_MALDI_LTQ_XL;
    else if (type == "MALDI LTQ ORBITRAP")      return InstrumentModelType_MALDI_LTQ_Orbitrap;
    else if (type == "TSQ QUANTUM")             return InstrumentModelType_TSQ_Quantum;
    else if (bal::contains(type, "TSQ QUANTUM ACCESS")) return InstrumentModelType_TSQ_Quantum_Access;
    else if (type == "TSQ QUANTUM ULTRA")       return InstrumentModelType_TSQ_Quantum_Ultra;
    else if (type == "TSQ QUANTUM ULTRA AM")    return InstrumentModelType_TSQ_Quantum_Ultra_AM;
    else if (type == "TSQ VANTAGE STANDARD")    return InstrumentModelType_TSQ_Vantage_Standard;
    else if (type == "TSQ VANTAGE EMR")         return InstrumentModelType_TSQ_Vantage_EMR;
    else if (type == "TSQ QUANTIVA")            return InstrumentModelType_TSQ_Quantiva;
    else if (type == "TSQ ENDURA")              return InstrumentModelType_TSQ_Endura;
    else if (type == "TSQ ALTIS")               return InstrumentModelType_TSQ_Altis;
    else if (type == "ELEMENT XR")              return InstrumentModelType_Element_XR;
    else if (type == "ELEMENT GD")              return InstrumentModelType_Element_GD;
    else if (type == "GC ISOLINK")              return InstrumentModelType_GC_IsoLink;
    else if (bal::contains(type, "Q EXACTIVE")) return InstrumentModelType_Q_Exactive;
    else if (bal::contains(type, "EXACTIVE"))   return InstrumentModelType_Exactive;
    else if (bal::contains(type, "EXPLORIS"))   return InstrumentModelType_Orbitrap_Exploris_480;
    else if (bal::contains(type, "ECLIPSE"))    return InstrumentModelType_Orbitrap_Eclipse;
    else if (bal::contains(type, "FUSION"))     return bal::contains(type, "ETD") ? InstrumentModelType_Orbitrap_Fusion_ETD : InstrumentModelType_Orbitrap_Fusion;
    else if (type == "SURVEYOR PDA")            return InstrumentModelType_Surveyor_PDA;
    else if (type == "ACCELA PDA")              return InstrumentModelType_Accela_PDA;
    else
        return InstrumentModelType_Unknown;
}


enum PWIZ_API_DECL IonizationType
{
    IonizationType_EI = 0,       // Electron Ionization
    IonizationType_CI,           // Chemical Ionization
    IonizationType_FAB,          // Fast Atom Bombardment
    IonizationType_ESI,          // Electrospray Ionization
    IonizationType_APCI,         // Atmospheric Pressure Chemical Ionization
    IonizationType_NSI,          // Nanospray Ionization
    IonizationType_TSP,          // Thermospray
    IonizationType_FD,           // Field Desorption
    IonizationType_MALDI,        // Matrix-assisted Laser Desorption Ionization
    IonizationType_GD,           // Glow Discharge
    IonizationType_Unknown,
    IonizationType_PaperSpray,
    IonizationType_CardNanoSpray,
    IonizationType_Count
};


inline std::vector<IonizationType> getIonSourcesForInstrumentModel(InstrumentModelType type)
{
    std::vector<IonizationType> ionSources;
    switch (type)
    {
        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LTQ:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Exactive:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Orbitrap_Exploris_480:
        case InstrumentModelType_Orbitrap_Eclipse:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
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
        case InstrumentModelType_ISQ:
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


enum PWIZ_API_DECL ScanFilterMassAnalyzerType
{
    ScanFilterMassAnalyzerType_Unknown = -1,
    ScanFilterMassAnalyzerType_ITMS = 0,          // Ion Trap
    ScanFilterMassAnalyzerType_TQMS = 1,          // Triple Quadrupole
    ScanFilterMassAnalyzerType_SQMS = 2,          // Single Quadrupole
    ScanFilterMassAnalyzerType_TOFMS = 3,         // Time of Flight
    ScanFilterMassAnalyzerType_FTMS = 4,          // Fourier Transform
    ScanFilterMassAnalyzerType_Sector = 5,        // Magnetic Sector
    ScanFilterMassAnalyzerType_Count = 6
};


enum PWIZ_API_DECL MassAnalyzerType
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
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Orbitrap_Exploris_480:
            return MassAnalyzerType_Orbitrap;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Eclipse:
        {
            switch (scanFilterType)
            {
                case ScanFilterMassAnalyzerType_FTMS: return MassAnalyzerType_Orbitrap;
                //case ScanFilterMassAnalyzerType_SQMS: return MassAnalyzerType_Single_Quadrupole; FIXME: is this possible on the Fusion?
                default:
                case ScanFilterMassAnalyzerType_ITMS:
                    return MassAnalyzerType_Linear_Ion_Trap;
            }
        }

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
        case InstrumentModelType_ISQ:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            return MassAnalyzerType_Single_Quadrupole;

        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
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
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_Plus:
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
            switch (scanFilterType)
            {
                case ScanFilterMassAnalyzerType_FTMS: return MassAnalyzerType_FTICR;
                case ScanFilterMassAnalyzerType_ITMS: return MassAnalyzerType_Linear_Ion_Trap;
                case ScanFilterMassAnalyzerType_Sector: return MassAnalyzerType_Magnetic_Sector;
                case ScanFilterMassAnalyzerType_SQMS: return MassAnalyzerType_Single_Quadrupole;
                case ScanFilterMassAnalyzerType_TOFMS: return MassAnalyzerType_TOF;
                case ScanFilterMassAnalyzerType_TQMS: return MassAnalyzerType_Triple_Quadrupole;
                default: return MassAnalyzerType_Unknown;
            }
    }
}


inline std::vector<MassAnalyzerType> getMassAnalyzersForInstrumentModel(InstrumentModelType type)
{
    std::vector<MassAnalyzerType> massAnalyzers;
    switch (type)
    {
        case InstrumentModelType_Exactive:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Orbitrap_Exploris_480:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            break;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion: // has a quadrupole but only for mass filtering, not analysis
        case InstrumentModelType_Orbitrap_Fusion_ETD: // has a quadrupole but only for mass filtering, not analysis
        case InstrumentModelType_Orbitrap_Eclipse:
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
        case InstrumentModelType_ISQ:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            massAnalyzers.push_back(MassAnalyzerType_Single_Quadrupole);
            break;

        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
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
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_Plus:
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


enum PWIZ_API_DECL DetectorType
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
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Orbitrap_Exploris_480:
            detectors.push_back(DetectorType_Inductive);
            break;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Eclipse:
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
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_DFS:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_ISQ:
        case InstrumentModelType_GC_IsoLink:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
            detectors.push_back(DetectorType_Electron_Multiplier);
            break;

        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
            detectors.push_back(DetectorType_Photo_Diode_Array);

        case InstrumentModelType_Element_GD:
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


enum PWIZ_API_DECL ActivationType
{
    ActivationType_Unknown = 0,
    ActivationType_CID = 1,         // Collision Induced Dissociation
    ActivationType_MPD = 2,         // TODO: what is this?
    ActivationType_ECD = 4,         // Electron Capture Dissociation
    ActivationType_PQD = 8,         // Pulsed Q Dissociation
    ActivationType_ETD = 16,         // Electron Transfer Dissociation
    ActivationType_HCD = 32,         // High Energy CID
    ActivationType_Any = 64,         // "any activation type" when used as input parameter
    ActivationType_PTR = 128,         // Proton Transfer Reaction
    ActivationType_NETD = 256,        // TODO: nano-ETD?
    ActivationType_NPTR = 512,       // TODO: nano-PTR?
    ActivationType_Count = 1024
};


enum PWIZ_API_DECL MSOrder
{
    MSOrder_NeutralGain = -3,
    MSOrder_NeutralLoss = -2,
    MSOrder_ParentScan = -1,
    MSOrder_Any = 0,
    MSOrder_MS = 1,
    MSOrder_MS2 = 2,
    MSOrder_MS3 = 3,
    MSOrder_MS4 = 4,
    MSOrder_MS5 = 5,
    MSOrder_MS6 = 6,
    MSOrder_MS7 = 7,
    MSOrder_MS8 = 8,
    MSOrder_MS9 = 9,
    MSOrder_MS10 = 10,
    MSOrder_Count = 11
};


enum PWIZ_API_DECL ScanType
{
    ScanType_Unknown = -1,
    ScanType_Full = 0,
    ScanType_Zoom = 1,
    ScanType_SIM = 2,
    ScanType_SRM = 3,
    ScanType_CRM = 4,
    ScanType_Any = 5, /// "any scan type" when used as an input parameter
    ScanType_Q1MS = 6,
    ScanType_Q3MS = 7,
    ScanType_Count = 8
};


enum PWIZ_API_DECL PolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Negative = 0,
    PolarityType_Positive,
    PolarityType_Count
};


enum PWIZ_API_DECL DataPointType
{
	DataPointType_Unknown = -1,
	DataPointType_Centroid = 0,
	DataPointType_Profile,
    DataPointType_Count
};


enum PWIZ_API_DECL AccurateMassType
{
	AccurateMass_Unknown = -1,
	AccurateMass_NotActive = 0,                 // NOTE: in filter as "!AM": accurate mass not active
	AccurateMass_Active,                        // accurate mass active 
	AccurateMass_ActiveWithInternalCalibration, // accurate mass with internal calibration
	AccurateMass_ActiveWithExternalCalibration  // accurate mass with external calibration
};


enum PWIZ_API_DECL TriBool
{
	TriBool_Unknown = -1,
	TriBool_False = 0,
	TriBool_True = 1
};

} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz

#endif // _RAWFILETYPES_H_
