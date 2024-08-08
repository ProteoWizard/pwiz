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
#include <boost/algorithm/string/replace.hpp>
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
    InstrumentModelType_LTQ_Velos_ETD,
    InstrumentModelType_LTQ_Velos_Plus,
    InstrumentModelType_LTQ_FT,
    InstrumentModelType_LTQ_FT_Ultra,
    InstrumentModelType_LTQ_Orbitrap,
    InstrumentModelType_LTQ_Orbitrap_Classic,
    InstrumentModelType_LTQ_Orbitrap_Discovery,
    InstrumentModelType_LTQ_Orbitrap_XL,
    InstrumentModelType_LTQ_Orbitrap_Velos,
    InstrumentModelType_LTQ_Orbitrap_Velos_Pro,
    InstrumentModelType_LTQ_Orbitrap_Elite,
    InstrumentModelType_LXQ,
    InstrumentModelType_LCQ_Fleet,
    InstrumentModelType_ITQ_700,
    InstrumentModelType_ITQ_900,
    InstrumentModelType_ITQ_1100,
    InstrumentModelType_GC_Quantum,
    InstrumentModelType_LTQ_XL,
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
    InstrumentModelType_TSQ_Vantage_AM,
    InstrumentModelType_Element_XR,
    InstrumentModelType_Element_GD,
    InstrumentModelType_GC_IsoLink,
    InstrumentModelType_Exactive,
    InstrumentModelType_Exactive_Plus,
    InstrumentModelType_Q_Exactive,
    InstrumentModelType_Q_Exactive_Plus,
    InstrumentModelType_Q_Exactive_HF,
    InstrumentModelType_Q_Exactive_HF_X,
    InstrumentModelType_Q_Exactive_UHMR,
    InstrumentModelType_Surveyor_PDA,
    InstrumentModelType_Accela_PDA,
    InstrumentModelType_Orbitrap_Fusion,
    InstrumentModelType_Orbitrap_Fusion_Lumos,
    InstrumentModelType_Orbitrap_Fusion_ETD,
    InstrumentModelType_Orbitrap_Ascend,
    InstrumentModelType_Orbitrap_ID_X,
    InstrumentModelType_TSQ_Quantiva,
    InstrumentModelType_TSQ_Endura,
    InstrumentModelType_TSQ_Altis,
    InstrumentModelType_TSQ_Altis_Plus,
    InstrumentModelType_TSQ_Quantis,
    InstrumentModelType_TSQ_8000_Evo,
    InstrumentModelType_TSQ_9000,
    InstrumentModelType_Orbitrap_Exploris_120,
    InstrumentModelType_Orbitrap_Exploris_240,
    InstrumentModelType_Orbitrap_Exploris_480,
    InstrumentModelType_Orbitrap_Eclipse,
    InstrumentModelType_Orbitrap_GC,
    InstrumentModelType_Orbitrap_Astral,
    InstrumentModelType_Stellar,

    InstrumentModelType_Count,
};


namespace {

enum MatchType
{
    Exact,
    Contains,
    StartsWith,
    EndsWith,
    ExactNoSpaces
};

}

struct InstrumentNameToModelMapping
{

    const char* name;
    InstrumentModelType modelType;
    MatchType matchType;
};

const InstrumentNameToModelMapping nameToModelMapping[] =
{
    {"MAT253", InstrumentModelType_MAT253, ExactNoSpaces},
    {"MAT900XP", InstrumentModelType_MAT900XP, ExactNoSpaces},
    {"MAT900XPTRAP", InstrumentModelType_MAT900XP_Trap, ExactNoSpaces},
    {"MAT95XP", InstrumentModelType_MAT95XP, ExactNoSpaces},
    {"MAT95XPTRAP", InstrumentModelType_MAT95XP_Trap, ExactNoSpaces},
    {"SSQ7000", InstrumentModelType_SSQ_7000, ExactNoSpaces},
    {"TSQ7000", InstrumentModelType_TSQ_7000, ExactNoSpaces},
    {"TSQ8000EVO", InstrumentModelType_TSQ_8000_Evo, ExactNoSpaces},
    {"TSQ9000", InstrumentModelType_TSQ_9000, ExactNoSpaces},
    {"TSQ", InstrumentModelType_TSQ, Exact},
    {"ELEMENT2", InstrumentModelType_Element_2, ExactNoSpaces},
    {"DELTA PLUSADVANTAGE", InstrumentModelType_Delta_Plus_Advantage, Exact},
    {"DELTAPLUSXP", InstrumentModelType_Delta_Plus_XP, Exact},
    {"LCQ ADVANTAGE", InstrumentModelType_LCQ_Advantage, Exact},
    {"LCQ CLASSIC", InstrumentModelType_LCQ_Classic, Exact},
    {"LCQ DECA", InstrumentModelType_LCQ_Deca, Exact},
    {"LCQ DECA XP", InstrumentModelType_LCQ_Deca_XP_Plus, Exact},
    {"LCQ DECA XP PLUS", InstrumentModelType_LCQ_Deca_XP_Plus, Exact},
    {"NEPTUNE", InstrumentModelType_Neptune, Exact},
    {"DSQ", InstrumentModelType_DSQ, Exact},
    {"POLARISQ", InstrumentModelType_PolarisQ, Exact},
    {"SURVEYOR MSQ", InstrumentModelType_Surveyor_MSQ, Exact},
    {"MSQ PLUS", InstrumentModelType_Surveyor_MSQ, Exact},
    {"TEMPUS TOF", InstrumentModelType_Tempus_TOF, Exact},
    {"TRACE DSQ", InstrumentModelType_Trace_DSQ, Exact},
    {"TRITON", InstrumentModelType_Triton, Exact},
    {"LTQ", InstrumentModelType_LTQ, Exact},
    {"LTQ XL", InstrumentModelType_LTQ_XL, Exact},
    {"LTQ FT", InstrumentModelType_LTQ_FT, Exact},
    {"LTQ-FT", InstrumentModelType_LTQ_FT, Exact},
    {"LTQ FT ULTRA", InstrumentModelType_LTQ_FT_Ultra, Exact},
    {"LTQ ORBITRAP", InstrumentModelType_LTQ_Orbitrap, Exact},
    {"LTQ ORBITRAP CLASSIC", InstrumentModelType_LTQ_Orbitrap_Classic, Exact}, // predicted
    {"LTQ ORBITRAP DISCOVERY", InstrumentModelType_LTQ_Orbitrap_Discovery, Exact},
    {"LTQ ORBITRAP XL", InstrumentModelType_LTQ_Orbitrap_XL, Exact},
    {"ORBITRAP VELOS PRO", InstrumentModelType_LTQ_Orbitrap_Velos_Pro, Contains},
    {"ORBITRAP VELOS", InstrumentModelType_LTQ_Orbitrap_Velos, Contains},
    {"ORBITRAP ELITE", InstrumentModelType_LTQ_Orbitrap_Elite, Contains},
    {"VELOS PLUS", InstrumentModelType_LTQ_Velos_Plus, Contains},
    {"VELOS PRO", InstrumentModelType_LTQ_Velos_Plus, Contains},
    {"LTQ VELOS", InstrumentModelType_LTQ_Velos, Exact},
    {"LTQ VELOS ETD", InstrumentModelType_LTQ_Velos_ETD, Exact},
    {"LXQ", InstrumentModelType_LXQ, Exact},
    {"LCQ FLEET", InstrumentModelType_LCQ_Fleet, Exact},
    {"ITQ 700", InstrumentModelType_ITQ_700, Exact},
    {"ITQ 900", InstrumentModelType_ITQ_900, Exact},
    {"ITQ 1100", InstrumentModelType_ITQ_1100, Exact},
    {"GC QUANTUM", InstrumentModelType_GC_Quantum, Exact},
    {"LTQ XL ETD", InstrumentModelType_LTQ_XL_ETD, Exact},
    {"LTQ ORBITRAP XL ETD", InstrumentModelType_LTQ_Orbitrap_XL_ETD, Exact},
    {"DFS", InstrumentModelType_DFS, Exact},
    {"DSQ II", InstrumentModelType_DSQ_II, Exact},
    {"ISQ SERIES", InstrumentModelType_ISQ, Exact},
    {"MALDI LTQ XL", InstrumentModelType_MALDI_LTQ_XL, Exact},
    {"MALDI LTQ ORBITRAP", InstrumentModelType_MALDI_LTQ_Orbitrap, Exact},
    {"TSQ QUANTUM", InstrumentModelType_TSQ_Quantum, Exact},
    {"TSQ QUANTUM ACCESS", InstrumentModelType_TSQ_Quantum_Access, Contains},
    {"TSQ QUANTUM ULTRA", InstrumentModelType_TSQ_Quantum_Ultra, Exact},
    {"TSQ QUANTUM ULTRA AM", InstrumentModelType_TSQ_Quantum_Ultra_AM, Exact},
    {"TSQ VANTAGE STANDARD", InstrumentModelType_TSQ_Vantage_Standard, Exact},
    {"TSQ VANTAGE EMR", InstrumentModelType_TSQ_Vantage_EMR, Exact},
    {"TSQ VANTAGE AM", InstrumentModelType_TSQ_Vantage_AM, Exact},
    {"TSQ QUANTIVA", InstrumentModelType_TSQ_Quantiva, Exact},
    {"TSQ ENDURA", InstrumentModelType_TSQ_Endura, Exact},
    {"TSQ ALTIS", InstrumentModelType_TSQ_Altis, Exact},
    {"TSQ ALTIS PLUS", InstrumentModelType_TSQ_Altis_Plus, Exact},
    {"TSQ QUANTIS", InstrumentModelType_TSQ_Quantis, Exact},
    {"ELEMENT XR", InstrumentModelType_Element_XR, Exact},
    {"ELEMENT GD", InstrumentModelType_Element_GD, Exact},
    {"GC ISOLINK", InstrumentModelType_GC_IsoLink, Exact},
    {"ORBITRAP ID-X", InstrumentModelType_Orbitrap_ID_X, Exact},
    {"Q EXACTIVE PLUS", InstrumentModelType_Q_Exactive_Plus, Contains},
    {"Q EXACTIVE HF-X", InstrumentModelType_Q_Exactive_HF_X, Contains},
    {"Q EXACTIVE HF", InstrumentModelType_Q_Exactive_HF, Contains},
    {"Q EXACTIVE UHMR", InstrumentModelType_Q_Exactive_UHMR, Contains},
    {"Q EXACTIVE", InstrumentModelType_Q_Exactive, Contains},
    {"EXACTIVE PLUS", InstrumentModelType_Exactive_Plus, Contains},
    {"EXACTIVE", InstrumentModelType_Exactive, Contains},
    {"ORBITRAP EXPLORIS 120", InstrumentModelType_Orbitrap_Exploris_120, Exact},
    {"ORBITRAP EXPLORIS 240", InstrumentModelType_Orbitrap_Exploris_240, Exact},
    {"ORBITRAP EXPLORIS 480", InstrumentModelType_Orbitrap_Exploris_480, Exact},
    {"ORBITRAP GC", InstrumentModelType_Orbitrap_GC, Contains},
    {"ECLIPSE", InstrumentModelType_Orbitrap_Eclipse, Contains},
    {"ASTRAL", InstrumentModelType_Orbitrap_Astral, Contains},
    {"FUSION ETD", InstrumentModelType_Orbitrap_Fusion_ETD, Contains},
    {"FUSION LUMOS", InstrumentModelType_Orbitrap_Fusion_Lumos, Contains},
    {"FUSION", InstrumentModelType_Orbitrap_Fusion, Contains},
    {"ASCEND", InstrumentModelType_Orbitrap_Ascend, Contains},
    {"SURVEYOR PDA", InstrumentModelType_Surveyor_PDA, Exact},
    {"ACCELA PDA", InstrumentModelType_Accela_PDA, Exact},
    {"STELLAR", InstrumentModelType_Stellar, Contains},
};

inline InstrumentModelType parseInstrumentModelType(const std::string& instrumentModel)
{
    std::string type = bal::to_upper_copy(instrumentModel);
    std::string typeNoSpaces = bal::replace_all_copy(type, " ", "");
    for (const auto& mapping : nameToModelMapping)
        switch (mapping.matchType)
        {
            case Exact: if (mapping.name == type) return mapping.modelType; break;
            case ExactNoSpaces: if (mapping.name == typeNoSpaces) return mapping.modelType; break;
            case Contains: if (bal::contains(type, mapping.name)) return mapping.modelType; break;
            case StartsWith: if (bal::starts_with(type, mapping.name)) return mapping.modelType; break;
            case EndsWith: if (bal::ends_with(type, mapping.name)) return mapping.modelType; break;
            default:
                throw std::runtime_error("unknown match type");
        }
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
        case InstrumentModelType_TSQ_8000_Evo:
        case InstrumentModelType_TSQ_9000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_LXQ:
        case InstrumentModelType_LTQ:
        case InstrumentModelType_LTQ_XL:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_ETD:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Classic:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Exactive:
        case InstrumentModelType_Exactive_Plus:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Q_Exactive_Plus:
        case InstrumentModelType_Q_Exactive_HF:
        case InstrumentModelType_Q_Exactive_HF_X:
        case InstrumentModelType_Q_Exactive_UHMR:
        case InstrumentModelType_Orbitrap_Exploris_120:
        case InstrumentModelType_Orbitrap_Exploris_240:
        case InstrumentModelType_Orbitrap_Exploris_480:
        case InstrumentModelType_Orbitrap_Eclipse:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_Lumos:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Ascend:
        case InstrumentModelType_Orbitrap_ID_X:
        case InstrumentModelType_Orbitrap_Astral:
    case InstrumentModelType_Stellar:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_TSQ_Vantage_AM:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
        case InstrumentModelType_TSQ_Altis_Plus:
        case InstrumentModelType_TSQ_Quantis:
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
        case InstrumentModelType_Orbitrap_GC:
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
    ScanFilterMassAnalyzerType_Any = 6,           // returned by RawFileReader when filter type is unspecified
    ScanFilterMassAnalyzerType_ASTMS = 7,         // ASymmetric Track lossless
    ScanFilterMassAnalyzerType_Count = 8
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
    MassAnalyzerType_Astral, // ASymmetric TRack Lossless
    MassAnalyzerType_Count
};


inline MassAnalyzerType convertScanFilterMassAnalyzer(ScanFilterMassAnalyzerType scanFilterType,
                                                      InstrumentModelType instrumentModel)
{
    switch (instrumentModel)
    {
        case InstrumentModelType_Exactive:
        case InstrumentModelType_Exactive_Plus:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Q_Exactive_Plus:
        case InstrumentModelType_Q_Exactive_HF_X:
        case InstrumentModelType_Q_Exactive_HF:
        case InstrumentModelType_Q_Exactive_UHMR:
        case InstrumentModelType_Orbitrap_Exploris_120:
        case InstrumentModelType_Orbitrap_Exploris_240:
        case InstrumentModelType_Orbitrap_Exploris_480:
            return MassAnalyzerType_Orbitrap;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Classic:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_Lumos:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Ascend:
        case InstrumentModelType_Orbitrap_ID_X:
        case InstrumentModelType_Orbitrap_Eclipse:
        case InstrumentModelType_Orbitrap_GC:
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

        case InstrumentModelType_Orbitrap_Astral:
            switch (scanFilterType)
            {
                case ScanFilterMassAnalyzerType_FTMS: return MassAnalyzerType_Orbitrap;
                default:
                case ScanFilterMassAnalyzerType_ASTMS:
                    return MassAnalyzerType_Astral;
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
        case InstrumentModelType_TSQ_8000_Evo:
        case InstrumentModelType_TSQ_9000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_TSQ_Vantage_AM:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
        case InstrumentModelType_TSQ_Altis_Plus:
        case InstrumentModelType_TSQ_Quantis:
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
        case InstrumentModelType_LTQ_XL:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_ETD:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_Stellar:
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
                case ScanFilterMassAnalyzerType_ASTMS: return MassAnalyzerType_Astral;
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
        case InstrumentModelType_Exactive_Plus:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Q_Exactive_Plus:
        case InstrumentModelType_Q_Exactive_HF_X:
        case InstrumentModelType_Q_Exactive_HF:
        case InstrumentModelType_Q_Exactive_UHMR:
        case InstrumentModelType_Orbitrap_Exploris_120:
        case InstrumentModelType_Orbitrap_Exploris_240:
        case InstrumentModelType_Orbitrap_Exploris_480:
        case InstrumentModelType_Orbitrap_GC:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            break;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Classic:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion: // has a quadrupole but only for mass filtering, not analysis
        case InstrumentModelType_Orbitrap_Fusion_Lumos: // ditto
        case InstrumentModelType_Orbitrap_Fusion_ETD: // ditto
        case InstrumentModelType_Orbitrap_Ascend:
        case InstrumentModelType_Orbitrap_ID_X: // ditto
        case InstrumentModelType_Orbitrap_Eclipse:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            massAnalyzers.push_back(MassAnalyzerType_Linear_Ion_Trap);
            break;

        case InstrumentModelType_Orbitrap_Astral:
            massAnalyzers.push_back(MassAnalyzerType_Orbitrap);
            massAnalyzers.push_back(MassAnalyzerType_Astral);
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
        case InstrumentModelType_TSQ_8000_Evo:
        case InstrumentModelType_TSQ_9000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_TSQ_Vantage_AM:
        case InstrumentModelType_GC_Quantum:
        case InstrumentModelType_TSQ_Quantiva:
        case InstrumentModelType_TSQ_Endura:
        case InstrumentModelType_TSQ_Altis:
        case InstrumentModelType_TSQ_Altis_Plus:
        case InstrumentModelType_TSQ_Quantis:
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
        case InstrumentModelType_LTQ_XL:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_ETD:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_Stellar:
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
        case InstrumentModelType_Exactive_Plus:
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Q_Exactive_Plus:
        case InstrumentModelType_Q_Exactive_HF_X:
        case InstrumentModelType_Q_Exactive_HF:
        case InstrumentModelType_Q_Exactive_UHMR:
        case InstrumentModelType_Orbitrap_Exploris_120:
        case InstrumentModelType_Orbitrap_Exploris_240:
        case InstrumentModelType_Orbitrap_Exploris_480:
        case InstrumentModelType_Orbitrap_GC:
            detectors.push_back(DetectorType_Inductive);
            break;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Classic:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_Lumos:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Ascend:
        case InstrumentModelType_Orbitrap_ID_X:
        case InstrumentModelType_Orbitrap_Eclipse:
        case InstrumentModelType_Orbitrap_Astral:
            detectors.push_back(DetectorType_Inductive);
            detectors.push_back(DetectorType_Electron_Multiplier);
            break;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ_8000_Evo:
        case InstrumentModelType_TSQ_9000:
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
        case InstrumentModelType_LTQ_XL:
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_LTQ_Velos:
        case InstrumentModelType_LTQ_Velos_ETD:
        case InstrumentModelType_LTQ_Velos_Plus:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_TSQ_Vantage_Standard:
        case InstrumentModelType_TSQ_Vantage_EMR:
        case InstrumentModelType_TSQ_Vantage_AM:
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
        case InstrumentModelType_TSQ_Altis_Plus:
        case InstrumentModelType_TSQ_Quantis:
        case InstrumentModelType_Stellar:
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
