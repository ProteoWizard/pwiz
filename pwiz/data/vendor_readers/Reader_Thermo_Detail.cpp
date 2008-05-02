#define PWIZ_SOURCE

#include "Reader_Thermo_Detail.hpp"

namespace pwiz {
namespace msdata {
namespace detail {

PWIZ_API_DECL CVParam translateAsScanningMethod(ScanType scanType)
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
            return CVParam();
    }
}


PWIZ_API_DECL CVParam translateAsSpectrumType(ScanType scanType)
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
            return CVParam();
    }
}


PWIZ_API_DECL CVParam translate(MassAnalyzerType type)
{
    switch (type)
    {
        case MassAnalyzerType_ITMS: return MS_ion_trap;
        case MassAnalyzerType_FTMS: return MS_FT_ICR;
        case MassAnalyzerType_TOFMS: return MS_time_of_flight;
        case MassAnalyzerType_TQMS: return MS_quadrupole;
        case MassAnalyzerType_SQMS: return MS_quadrupole;
        case MassAnalyzerType_Sector: return MS_magnetic_sector;
        case MassAnalyzerType_Unknown:
        default:
            return CVParam();
    }
}


PWIZ_API_DECL CVParam translateAsIonizationType(IonizationType ionizationType)
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
            return CVParam();
    }
}

    
PWIZ_API_DECL CVParam translateAsInletType(IonizationType ionizationType)
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
            return CVParam();
    }
}


PWIZ_API_DECL CVParam translate(PolarityType polarityType)
{
    switch (polarityType)
    {
        case PolarityType_Positive:
            return MS_positive_scan;
        case PolarityType_Negative:
            return MS_negative_scan;
        case PolarityType_Unknown:
        default:
            return CVParam();
    }
}


PWIZ_API_DECL CVParam translate(ActivationType activationType)
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
            return CVParam();
    }
}

} // detail
} // msdata
} // pwiz
