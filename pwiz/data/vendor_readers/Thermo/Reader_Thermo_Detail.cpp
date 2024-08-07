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


#define PWIZ_SOURCE

#include "Reader_Thermo_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <boost/range/algorithm/find_if.hpp>

namespace pwiz {
namespace msdata {
namespace detail {
namespace Thermo {


PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModelType instrumentModelType)
{
    switch (instrumentModelType)
    {
        // Finnigan MAT
        case InstrumentModelType_MAT253:                    return MS_MAT253;
        case InstrumentModelType_MAT900XP:                  return MS_MAT900XP;
        case InstrumentModelType_MAT900XP_Trap:             return MS_MAT900XP_Trap;
        case InstrumentModelType_MAT95XP:                   return MS_MAT95XP;
        case InstrumentModelType_MAT95XP_Trap:              return MS_MAT95XP_Trap;
        case InstrumentModelType_SSQ_7000:                  return MS_SSQ_7000;
        case InstrumentModelType_TSQ_7000:                  return MS_TSQ_7000;
        case InstrumentModelType_TSQ:                       return MS_TSQ;

        // Thermo Electron
        case InstrumentModelType_Element_2:                 return MS_Element_2;

        // Thermo Finnigan
        case InstrumentModelType_Delta_Plus_XP:             return MS_DELTAplusXP;
        case InstrumentModelType_Delta_Plus_Advantage:      return MS_DELTA_plusAdvantage;
        case InstrumentModelType_LCQ_Advantage:             return MS_LCQ_Advantage;
        case InstrumentModelType_LCQ_Classic:               return MS_LCQ_Classic;
        case InstrumentModelType_LCQ_Deca:                  return MS_LCQ_Deca;
        case InstrumentModelType_LCQ_Deca_XP_Plus:          return MS_LCQ_Deca_XP_Plus;
        case InstrumentModelType_Neptune:                   return MS_neptune;
        case InstrumentModelType_DSQ:                       return MS_DSQ;
        case InstrumentModelType_PolarisQ:                  return MS_PolarisQ;
        case InstrumentModelType_Surveyor_MSQ:              return MS_Surveyor_MSQ;
        case InstrumentModelType_Tempus_TOF:                return MS_TEMPUS_TOF;
        case InstrumentModelType_Trace_DSQ:                 return MS_TRACE_DSQ;
        case InstrumentModelType_Triton:                    return MS_TRITON;

        // Thermo Scientific
        case InstrumentModelType_LCQ_Fleet:                 return MS_LCQ_Fleet;
        case InstrumentModelType_LTQ:                       return MS_LTQ;
        case InstrumentModelType_LTQ_FT:                    return MS_LTQ_FT;
        case InstrumentModelType_LTQ_FT_Ultra:              return MS_LTQ_FT_Ultra;
        case InstrumentModelType_LTQ_Orbitrap:              return MS_LTQ_Orbitrap;
        case InstrumentModelType_LTQ_Orbitrap_Classic:      return MS_LTQ_Orbitrap_Classic;
        case InstrumentModelType_LTQ_Orbitrap_Discovery:    return MS_LTQ_Orbitrap_Discovery;
        case InstrumentModelType_LTQ_Orbitrap_XL:           return MS_LTQ_Orbitrap_XL;
        case InstrumentModelType_LTQ_Velos:                 return MS_LTQ_Velos;
        case InstrumentModelType_LTQ_Velos_ETD:             return MS_LTQ_Velos_ETD;
        case InstrumentModelType_LTQ_Velos_Plus:            return MS_Velos_Plus;
        case InstrumentModelType_LTQ_Orbitrap_Velos:        return MS_LTQ_Orbitrap_Velos;
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:    return MS_LTQ_Orbitrap_Velos_Pro;
        case InstrumentModelType_LTQ_Orbitrap_Elite:        return MS_LTQ_Orbitrap_Elite;
        case InstrumentModelType_LXQ:                       return MS_LXQ;
        case InstrumentModelType_ITQ_700:                   return MS_ITQ_700;
        case InstrumentModelType_ITQ_900:                   return MS_ITQ_900;
        case InstrumentModelType_ITQ_1100:                  return MS_ITQ_1100;
        case InstrumentModelType_GC_Quantum:                return MS_GC_Quantum;
        case InstrumentModelType_LTQ_XL:                    return MS_LTQ_XL;
        case InstrumentModelType_LTQ_XL_ETD:                return MS_LTQ_XL_ETD;
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:       return MS_LTQ_Orbitrap_XL_ETD;
        case InstrumentModelType_DFS:                       return MS_DFS;
        case InstrumentModelType_DSQ_II:                    return MS_DSQ_II;
        case InstrumentModelType_ISQ:                       return MS_ISQ;
        case InstrumentModelType_MALDI_LTQ_XL:              return MS_MALDI_LTQ_XL;
        case InstrumentModelType_MALDI_LTQ_Orbitrap:        return MS_MALDI_LTQ_Orbitrap;
        case InstrumentModelType_TSQ_Quantum:               return MS_TSQ_Quantum;
        case InstrumentModelType_TSQ_Quantum_Access:        return MS_TSQ_Quantum_Access;
        case InstrumentModelType_TSQ_Quantum_Ultra:         return MS_TSQ_Quantum_Ultra;
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:      return MS_TSQ_Quantum_Ultra_AM;
        case InstrumentModelType_TSQ_Vantage_Standard:      return MS_TSQ_Vantage;
        case InstrumentModelType_TSQ_Vantage_EMR:           return MS_TSQ_Vantage;
        case InstrumentModelType_TSQ_Vantage_AM:            return MS_TSQ_Vantage;
        case InstrumentModelType_Element_XR:                return MS_Element_XR;
        case InstrumentModelType_Element_GD:                return MS_Element_GD;
        case InstrumentModelType_GC_IsoLink:                return MS_GC_IsoLink;
        case InstrumentModelType_Exactive:                  return MS_Exactive;
        case InstrumentModelType_Exactive_Plus:             return MS_Exactive_Plus;
        case InstrumentModelType_Q_Exactive:                return MS_Q_Exactive;
        case InstrumentModelType_Q_Exactive_Plus:           return MS_Q_Exactive_Plus;
        case InstrumentModelType_Q_Exactive_HF:             return MS_Q_Exactive_HF;
        case InstrumentModelType_Q_Exactive_HF_X:           return MS_Q_Exactive_HF_X;
        case InstrumentModelType_Q_Exactive_UHMR:           return MS_Q_Exactive_UHMR;
        case InstrumentModelType_Surveyor_PDA:              return MS_Surveyor_PDA;
        case InstrumentModelType_Accela_PDA:                return MS_Accela_PDA;
        case InstrumentModelType_Orbitrap_Fusion:           return MS_Orbitrap_Fusion;
        case InstrumentModelType_Orbitrap_Fusion_Lumos:     return MS_Orbitrap_Fusion_Lumos;
        case InstrumentModelType_Orbitrap_Fusion_ETD:       return MS_Orbitrap_Fusion_ETD;
        case InstrumentModelType_Orbitrap_Ascend:           return MS_Orbitrap_Ascend;
        case InstrumentModelType_Orbitrap_ID_X:             return MS_Orbitrap_ID_X;
        case InstrumentModelType_TSQ_Quantiva:              return MS_TSQ_Quantiva;
        case InstrumentModelType_TSQ_Endura:                return MS_TSQ_Endura;
        case InstrumentModelType_TSQ_Altis:                 return MS_TSQ_Altis;
        case InstrumentModelType_TSQ_Altis_Plus:            return MS_TSQ_Altis_Plus;
        case InstrumentModelType_TSQ_Quantis:               return MS_TSQ_Quantis;
        case InstrumentModelType_TSQ_8000_Evo:              return MS_TSQ_8000_Evo;
        case InstrumentModelType_TSQ_9000:                  return MS_TSQ_9000;
        case InstrumentModelType_Orbitrap_Exploris_120:     return MS_Orbitrap_Exploris_120;
        case InstrumentModelType_Orbitrap_Exploris_240:     return MS_Orbitrap_Exploris_240;
        case InstrumentModelType_Orbitrap_Exploris_480:     return MS_Orbitrap_Exploris_480;
        case InstrumentModelType_Orbitrap_Eclipse:          return MS_Orbitrap_Eclipse;
        case InstrumentModelType_Orbitrap_GC:               return MS_Orbitrap_Exploris_480;
        case InstrumentModelType_Orbitrap_Astral:           return MS_Orbitrap_Astral;
        case InstrumentModelType_Stellar:                   return MS_Stellar;

        default:
            throw std::runtime_error("[Reader_Thermo::translateAsInstrumentModel] Enumerated instrument model " + lexical_cast<string>(instrumentModelType) + " has no CV term mapping!");

        case InstrumentModelType_Unknown:
            // TODO: is it possible to distiguish between Finnigan MAT and Thermo Electron?
            return MS_Thermo_Electron_instrument_model;
    }
}


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(RawFile& rawfile)
{
    InstrumentModelType model = rawfile.getInstrumentModel();

    // source common to all configurations (TODO: handle multiple sources in a single run?)
    auto raw = rawfile.getRawByThread(0);

    // handle files with no MS controllers
    if (raw->getNumberOfControllersOfType(Controller_MS) == 0 && rawfile.getNumberOfControllersOfType(Controller_PDA) > 0)
    {
        vector<InstrumentConfiguration> configurations(1);
        configurations.back().id = "PDA";
        configurations.back().componentList.push_back(Component(MS_PDA, 1));
        return configurations;
    }

    raw->setCurrentController(Controller_MS, 1);
    ScanInfoPtr firstScanInfo = raw->getScanInfo(1);
    CVID firstIonizationType = translateAsIonizationType(firstScanInfo->ionizationType());
    CVID firstInletType = translateAsInletType(firstScanInfo->ionizationType());

    Component commonSource(ComponentType_Source, 1);
    if (firstIonizationType == CVID_Unknown)
        firstIonizationType = MS_electrospray_ionization;
    commonSource.set(firstIonizationType);
    if (firstInletType != CVID_Unknown)
        commonSource.set(firstInletType);

    auto configurations = createInstrumentConfigurations(commonSource, model);

    if (rawfile.getNumberOfControllersOfType(Controller_PDA) > 0 &&
        boost::range::find_if(configurations, [](const InstrumentConfiguration& ic) { return ic.componentList[0].hasCVParam(MS_PDA); }) == configurations.end())
    {
        configurations.push_back(InstrumentConfiguration("PDA"));
        configurations.back().componentList.push_back(Component(MS_PDA, 1));
    }

    if (configurations.empty())
    {
        configurations.push_back(InstrumentConfiguration());
        configurations.back().componentList.push_back(commonSource);
        CVID analyzerType = CVID_Unknown;
        CVID detectorType = CVID_Unknown;
        switch (firstScanInfo->massAnalyzerType())
        {
            case MassAnalyzerType_FTICR:
                analyzerType = MS_FT_ICR;
                detectorType = MS_inductive_detector;
                break;
            case MassAnalyzerType_Orbitrap:
                analyzerType = MS_orbitrap;
                detectorType = MS_inductive_detector;
                break;
            case MassAnalyzerType_Linear_Ion_Trap:
                analyzerType = MS_radial_ejection_linear_ion_trap;
                detectorType = MS_electron_multiplier;
                break;
            case MassAnalyzerType_Quadrupole_Ion_Trap:
                analyzerType = MS_quadrupole_ion_trap;
                detectorType = MS_electron_multiplier;
                break;
            case MassAnalyzerType_Magnetic_Sector:
                analyzerType = MS_magnetic_sector;
                detectorType = MS_electron_multiplier;
                break;
            case MassAnalyzerType_Astral:
                analyzerType = MS_asymmetric_track_lossless_time_of_flight_analyzer;
                detectorType = MS_electron_multiplier;
            break;
        }

        if (analyzerType != CVID_Unknown)
        {
            configurations.back().componentList.push_back(Component(analyzerType, 2));
            configurations.back().componentList.push_back(Component(detectorType, 3));
        }
    }

    return configurations;
}


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(const Component& commonSource, InstrumentModelType model)
{
    vector<InstrumentConfiguration> configurations;
    
    switch (model)
    {
        case InstrumentModelType_Q_Exactive:
        case InstrumentModelType_Q_Exactive_Plus:
        case InstrumentModelType_Q_Exactive_HF:
        case InstrumentModelType_Q_Exactive_HF_X:
        case InstrumentModelType_Q_Exactive_UHMR:
        case InstrumentModelType_Orbitrap_Exploris_120:
        case InstrumentModelType_Orbitrap_Exploris_240:
        case InstrumentModelType_Orbitrap_Exploris_480:
        case InstrumentModelType_Orbitrap_GC:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_orbitrap, 3));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 4));
            break;

        case InstrumentModelType_Exactive:
        case InstrumentModelType_Exactive_Plus:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_orbitrap, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));
            break;

        case InstrumentModelType_Orbitrap_Astral:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_orbitrap, 3));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 4));

            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_asymmetric_track_lossless_time_of_flight_analyzer, 3));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 4));
            break;

        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_FT_ICR, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));

            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_Orbitrap_Fusion:
        case InstrumentModelType_Orbitrap_Fusion_Lumos:
        case InstrumentModelType_Orbitrap_Fusion_ETD:
        case InstrumentModelType_Orbitrap_Ascend:
        case InstrumentModelType_Orbitrap_ID_X:
        case InstrumentModelType_Orbitrap_Eclipse:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_orbitrap, 3));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 4));

            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 3));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 4));
            break;

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Classic:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Velos:
        case InstrumentModelType_LTQ_Orbitrap_Velos_Pro:
        case InstrumentModelType_LTQ_Orbitrap_Elite:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_orbitrap, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));

            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_ITQ_700:
        case InstrumentModelType_ITQ_900:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

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
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_ISQ:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
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
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_quadrupole, 3));
            configurations.back().componentList.push_back(Component(MS_quadrupole, 4));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        case InstrumentModelType_DFS:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_magnetic_sector, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_Tempus_TOF:
        case InstrumentModelType_Element_2:
        case InstrumentModelType_Element_XR:
        case InstrumentModelType_Element_GD:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_Triton:
            // TODO: figure out these configurations
            break;

        case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Accela_PDA:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_PDA, 1));
            break;

        default:
            throw std::runtime_error("[Reader_Thermo::createInstrumentConfigurations] Enumerated instrument model " + lexical_cast<string>(model) + " has no instrument configuration!");

        case InstrumentModelType_Unknown:
            break; // unknown configuration
    }

    return configurations;
}


PWIZ_API_DECL CVID translateAsScanningMethod(ScanType scanType)
{
    switch (scanType)
    {
        case ScanType_Zoom:
            return MS_zoom_scan;
        case ScanType_SIM:
            return MS_SIM;
        case ScanType_SRM:
            return MS_SRM;
        case ScanType_CRM:
            return MS_CRM_OBSOLETE;
        case ScanType_Full:
        case ScanType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translateAsSpectrumType(ScanType scanType)
{
    switch (scanType)
    {
        case ScanType_Full:
        case ScanType_Zoom:
            return MS_MSn_spectrum;
        case ScanType_SIM:
            return MS_SIM_spectrum;
        case ScanType_SRM:
            return MS_SRM_spectrum;
        case ScanType_CRM:
            return MS_CRM_spectrum;
        case ScanType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translate(MassAnalyzerType type)
{
    switch (type)
    {
        case MassAnalyzerType_Linear_Ion_Trap:      return MS_radial_ejection_linear_ion_trap;
        case MassAnalyzerType_Quadrupole_Ion_Trap:  return MS_quadrupole_ion_trap;
        case MassAnalyzerType_Orbitrap:             return MS_orbitrap;
        case MassAnalyzerType_FTICR:                return MS_FT_ICR;
        case MassAnalyzerType_TOF:                  return MS_time_of_flight;
        case MassAnalyzerType_Triple_Quadrupole:    return MS_quadrupole;
        case MassAnalyzerType_Single_Quadrupole:    return MS_quadrupole;
        case MassAnalyzerType_Magnetic_Sector:      return MS_magnetic_sector;
        case MassAnalyzerType_Astral:               return MS_asymmetric_track_lossless_time_of_flight_analyzer;
        case MassAnalyzerType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translateAsIonizationType(IonizationType ionizationType)
{
    switch (ionizationType)
    {
        case IonizationType_EI:                     return MS_electron_ionization;
        case IonizationType_CI:                     return MS_chemical_ionization;
        case IonizationType_FAB:                    return MS_fast_atom_bombardment_ionization;
        case IonizationType_ESI:                    return MS_electrospray_ionization;
        case IonizationType_NSI:                    return MS_nanoelectrospray;
        case IonizationType_APCI:                   return MS_atmospheric_pressure_chemical_ionization;
        //case IonizationType_TSP:                  return MS_thermospray_ionization;
        case IonizationType_FD:                     return MS_field_desorption;
        case IonizationType_MALDI:                  return MS_matrix_assisted_laser_desorption_ionization;
        case IonizationType_GD:                     return MS_glow_discharge_ionization;
        case IonizationType_Unknown:
        default:
            return CVID_Unknown;
    }
}

    
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType)
{
    switch (ionizationType)
    {
        //case IonizationType_EI:                   return MS_electron_ionization;
        //case IonizationType_CI:                   return MS_chemical_ionization;
        case IonizationType_FAB:                    return MS_continuous_flow_fast_atom_bombardment;
        case IonizationType_ESI:                    return MS_electrospray_inlet;
        case IonizationType_NSI:                    return MS_nanospray_inlet;
        //case IonizationType_APCI:                 return MS_atmospheric_pressure_chemical_ionization;
        case IonizationType_TSP:                    return MS_thermospray_inlet;
        //case IonizationType_FD:                   return MS_field_desorption;
        //case IonizationType_MALDI:                return MS_matrix_assisted_laser_desorption_ionization;
        //case IonizationType_GD:                   return MS_glow_discharge_ionization;
        case IonizationType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translate(PolarityType polarityType)
{
    switch (polarityType)
    {
        case PolarityType_Positive:
            return MS_positive_scan;
        case PolarityType_Negative:
            return MS_negative_scan;
        case PolarityType_Unknown:
        default:
            return CVID_Unknown;
    }
}

PWIZ_API_DECL void setActivationType(ActivationType activationType, ActivationType supplementalActivationType, Activation& activation)
{
    if (activationType & ActivationType_CID)
        activation.set(MS_collision_induced_dissociation);
    if (activationType & ActivationType_ETD)
        activation.set(MS_electron_transfer_dissociation);
    if (activationType & ActivationType_ECD)
        activation.set(MS_electron_capture_dissociation);
    if (activationType & ActivationType_PQD)
        activation.set(MS_pulsed_q_dissociation);
    if (activationType & ActivationType_HCD)
        activation.set(MS_HCD);

    if (supplementalActivationType != ActivationType_Unknown)
    {
        if (supplementalActivationType & ActivationType_CID)
            activation.set(MS_supplemental_collision_induced_dissociation);
        else if (supplementalActivationType & ActivationType_HCD)
            activation.set(MS_supplemental_beam_type_collision_induced_dissociation);
    }
    
    // ActivationType_PTR: // what does this map to?
    if (activationType & ActivationType_MPD)
        activation.set(MS_multiphoton_dissociation);
    // ActivationType_Unknown:
}

} // Thermo
} // detail
} // msdata
} // pwiz
