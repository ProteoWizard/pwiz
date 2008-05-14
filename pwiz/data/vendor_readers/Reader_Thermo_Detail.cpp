#define PWIZ_SOURCE

#include "Reader_Thermo_Detail.hpp"
#include "utility/misc/Container.hpp"

namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModelType instrumentModelType)
{
    switch (instrumentModelType)
    {
        //case InstrumentModelType_LCQ:                       return MS_LCQ;
        case InstrumentModelType_LCQ_Advantage:             return MS_LCQ_Advantage;
        case InstrumentModelType_LCQ_Classic:               return MS_LCQ_Classic;
        case InstrumentModelType_LCQ_Deca:                  return MS_LCQ_Deca;
        //case InstrumentModelType_LCQ_Deca_XP:               return MS_LCQ_Deca_XP;
        case InstrumentModelType_LCQ_Deca_XP_Plus:          return MS_LCQ_Deca_XP_Plus;
        case InstrumentModelType_LCQ_Fleet:                 return MS_LCQ_Fleet;
        case InstrumentModelType_LTQ:                       return MS_LTQ;
        //case InstrumentModelType_LTQ_XL:                    return MS_LTQ_XL;
        case InstrumentModelType_LTQ_FT:                    return MS_LTQ_FT;
        case InstrumentModelType_LTQ_FT_Ultra:              return MS_LTQ_FT_Ultra;
        case InstrumentModelType_LTQ_Orbitrap:              return MS_LTQ_Orbitrap;
        case InstrumentModelType_LTQ_Orbitrap_Discovery:    return MS_LTQ_Orbitrap_Discovery;
        case InstrumentModelType_LTQ_Orbitrap_XL:           return MS_LTQ_Orbitrap_XL;
        case InstrumentModelType_LXQ:                       return MS_LXQ;
        case InstrumentModelType_TSQ_Quantum:               return MS_TSQ_Quantum;
        //case InstrumentModelType_TSQ_Quantum_Access:        return MS_TSQ_Quantum_Access;
        case InstrumentModelType_GC_Quantum:                return MS_GC_Quantum;
        case InstrumentModelType_Delta_Plus_XP:             return MS_DELTAplusXP;
        case InstrumentModelType_Delta_Plus_Advantage:      return MS_DELTA_plusAdvantage;
        case InstrumentModelType_ELEMENT2:                  return MS_ELEMENT2;
        case InstrumentModelType_MAT253:                    return MS_MAT253;
        case InstrumentModelType_MAT900XP:                  return MS_MAT900XP;
        case InstrumentModelType_MAT900XP_Trap:             return MS_MAT900XP_Trap;
        case InstrumentModelType_MAT95XP:                   return MS_MAT95XP;
        case InstrumentModelType_MAT95XP_Trap:              return MS_MAT95XP_Trap;
        //case InstrumentModelType_Neptune:                   return MS_NEPTUNE;
        case InstrumentModelType_PolarisQ:                  return MS_PolarisQ;
        case InstrumentModelType_Surveyor_MSQ:              return MS_Surveyor_MSQ;
        //case InstrumentModelType_Surveyor_PDA:              return MS_Surveyor_PDA;
        case InstrumentModelType_Tempus_TOF:                return MS_TEMPUS_TOF;
        case InstrumentModelType_Trace_DSQ:                 return MS_TRACE_DSQ;
        case InstrumentModelType_Triton:                    return MS_TRITON;
        //case InstrumentModelType_Accela_PDA:                return MS_ACCELA_PDA;
        case InstrumentModelType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(RawFile& rawfile)
{
    vector<InstrumentConfiguration> configurations;

    InstrumentModelType model = parseInstrumentModelType(rawfile.value(InstModel));

    // source common to all configurations (TODO: handle multiple sources in a single run?)
    std::auto_ptr<ScanInfo> firstScanInfo = rawfile.getScanInfo(1);
    CVID firstIonizationType = translateAsIonizationType(firstScanInfo->ionizationType());
    CVID firstInletType = translateAsInletType(firstScanInfo->ionizationType());

    Component commonSource(ComponentType_Source, 1);
    if (firstIonizationType == CVID_Unknown)
        firstIonizationType = MS_electrospray_ionization;
    commonSource.set(firstIonizationType);
    if (firstInletType != CVID_Unknown)
        commonSource.set(firstInletType);

    switch (model)
    {
        // hybrid models with both FT/inductive and IT/multiplier
        case InstrumentModelType_LTQ_FT:
        case InstrumentModelType_LTQ_FT_Ultra:
        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_FT_ICR, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));

            // fall through to add IT/multiplier


        //case InstrumentModelType_LCQ:
        case InstrumentModelType_LCQ_Advantage:
        case InstrumentModelType_LCQ_Classic:
        case InstrumentModelType_LCQ_Deca:
        //case InstrumentModelType_LCQ_Deca_XP:
        case InstrumentModelType_LCQ_Deca_XP_Plus:
        case InstrumentModelType_LCQ_Fleet:
        case InstrumentModelType_LTQ:
        case InstrumentModelType_LTQ_XL:
        case InstrumentModelType_LXQ:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;


        case InstrumentModelType_TSQ_Quantum:
        //case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_GC_Quantum:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_quadrupole, 3));
            configurations.back().componentList.push_back(Component(MS_quadrupole, 4));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        case InstrumentModelType_Delta_Plus_XP:
        case InstrumentModelType_Delta_Plus_Advantage:
        case InstrumentModelType_ELEMENT2:
        case InstrumentModelType_MAT253:
        case InstrumentModelType_MAT900XP:
        case InstrumentModelType_MAT900XP_Trap:
        case InstrumentModelType_MAT95XP:
        case InstrumentModelType_MAT95XP_Trap:
        case InstrumentModelType_Neptune:
        case InstrumentModelType_PolarisQ:
        case InstrumentModelType_Surveyor_MSQ:
        //case InstrumentModelType_Surveyor_PDA:
        case InstrumentModelType_Tempus_TOF:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_Triton:
        //case InstrumentModelType_Accela_PDA:
        case InstrumentModelType_Unknown:
        default:
            break; // unknown configuration
    }

    return configurations;
}


PWIZ_API_DECL CVID translateAsScanningMethod(ScanType scanType)
{
    switch (scanType)
    {
        case ScanType_Full:
            return MS_full_scan;
        case ScanType_Zoom:
            return MS_zoom_scan;
        case ScanType_SIM:
            return MS_SIM;
        case ScanType_SRM:
            return MS_SRM;
        case ScanType_CRM:
            return MS_CRM;
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
        case MassAnalyzerType_ITMS: return MS_radial_ejection_linear_ion_trap;
        case MassAnalyzerType_FTMS: return MS_FT_ICR;
        case MassAnalyzerType_TOFMS: return MS_time_of_flight;
        case MassAnalyzerType_TQMS: return MS_quadrupole;
        case MassAnalyzerType_SQMS: return MS_quadrupole;
        case MassAnalyzerType_Sector: return MS_magnetic_sector;
        case MassAnalyzerType_Unknown:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translateAsIonizationType(IonizationType ionizationType)
{
    switch (ionizationType)
    {
        case IonizationType_EI: return MS_electron_ionization;
        case IonizationType_CI: return MS_chemical_ionization;
        case IonizationType_FAB: return MS_fast_atom_bombardment_ionization;
        case IonizationType_ESI: return MS_electrospray_ionization;
        case IonizationType_NSI: return MS_nanoelectrospray;
        case IonizationType_APCI: return MS_atmospheric_pressure_chemical_ionization;
        //case IonizationType_TSP: return MS_thermospray_ionization;
        case IonizationType_FD: return MS_field_desorption;
        case IonizationType_MALDI: return MS_matrix_assisted_laser_desorption_ionization;
        case IonizationType_GD: return MS_glow_discharge_ionization;
        case IonizationType_Unknown:
        default:
            return CVID_Unknown;
    }
}

    
PWIZ_API_DECL CVID translateAsInletType(IonizationType ionizationType)
{
    switch (ionizationType)
    {
        //case IonizationType_EI: return MS_electron_ionization;
        //case IonizationType_CI: return MS_chemical_ionization;
        case IonizationType_FAB: return MS_continuous_flow_fast_atom_bombardment;
        case IonizationType_ESI: return MS_electrospray_inlet;
        case IonizationType_NSI: return MS_nanospray_inlet;
        //case IonizationType_APCI: return MS_atmospheric_pressure_chemical_ionization;
        case IonizationType_TSP: return MS_thermospray_inlet;
        //case IonizationType_FD: return MS_field_desorption;
        //case IonizationType_MALDI: return MS_matrix_assisted_laser_desorption_ionization;
        //case IonizationType_GD: return MS_glow_discharge_ionization;
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


PWIZ_API_DECL CVID translate(ActivationType activationType)
{
    switch (activationType)
    {
        case ActivationType_CID:
        case ActivationType_SA: // should supplemental CID map to CID?
            return MS_collision_induced_dissociation;
        case ActivationType_ETD:
            return MS_electron_transfer_dissociation;
        case ActivationType_ECD:
            return MS_electron_capture_dissociation;
        case ActivationType_PQD:
            return MS_pulsed_q_dissociation;
        case ActivationType_HCD:
            return MS_high_energy_collision_induced_dissociation;
        default:
        case ActivationType_PTR: // what does this map to?
        case ActivationType_MPD: // what does this map to?
        case ActivationType_Unknown:
            return CVID_Unknown;
    }
}

} // detail
} // msdata
} // pwiz
