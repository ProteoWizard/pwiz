#define PWIZ_SOURCE

#include "Reader_Agilent_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"

namespace pwiz {
namespace msdata {
namespace detail {

using namespace BC;

PWIZ_API_DECL CVID translateAsSpectrumType(IScanInformationPtr scanInfoPtr)
{
    MSScanType scanTypes;
    scanInfoPtr->get_ScanTypes((BDA::MSScanType*)&scanTypes);
    if (scanTypes == MSScanType_Scan)               return MS_MS1_spectrum;
    if (scanTypes == MSScanType_ProductIon)         return MS_MSn_spectrum;
    if (scanTypes == MSScanType_PrecursorIon)       return MS_precursor_ion_spectrum;
    if (scanTypes == MSScanType_SelectedIon)        return MS_SIM_spectrum;
    if (scanTypes == MSScanType_TotalIon)           return MS_SIM_spectrum;
    if (scanTypes == MSScanType_MultipleReaction)   return MS_SRM_spectrum;
    if (scanTypes == MSScanType_NeutralLoss)        return MS_constant_neutral_loss_scan;
    if (scanTypes == MSScanType_NeutralGain)        return MS_constant_neutral_gain_scan;
    throw runtime_error("[translateAsSpectrumType()] Error parsing spectrum type.");
}

PWIZ_API_DECL int translateAsMSLevel(IScanInformationPtr scanInfoPtr)
{
    MSScanType scanTypes;
    scanInfoPtr->get_ScanTypes((BDA::MSScanType*)&scanTypes);
    if (scanTypes == MSScanType_Scan)               return 1;
    if (scanTypes == MSScanType_ProductIon)         return 2;
    if (scanTypes == MSScanType_PrecursorIon)       return -1;
    if (scanTypes == MSScanType_SelectedIon)        return 1;
    if (scanTypes == MSScanType_TotalIon)           return 1;
    if (scanTypes == MSScanType_MultipleReaction)   return 2;
    if (scanTypes == MSScanType_NeutralLoss)        return 2;
    if (scanTypes == MSScanType_NeutralGain)        return 2;
    throw runtime_error("[translateAsSpectrumType()] Error parsing MS level.");
}

PWIZ_API_DECL CVID translateAsActivationType(IScanInformationPtr scanInfoPtr)
{
    return MS_CID;
}

PWIZ_API_DECL CVID translateAsPolarityType(IScanInformationPtr scanInfoPtr)
{
    IonPolarity polarity;
    scanInfoPtr->get_IonPolarity((BDA::IonPolarity*)&polarity);
    if (polarity == IonPolarity_Positive)          return MS_positive_scan;
    if (polarity == IonPolarity_Negative)          return MS_negative_scan;
    throw runtime_error("[translateAsSpectrumType()] Error parsing polarity type.");
}

/*PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModelType instrumentModelType)
{
    return MS_Agilent_instrument_model;
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
        case InstrumentModelType_Exactive:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_orbitrap, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));
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

        case InstrumentModelType_LTQ_Orbitrap:
        case InstrumentModelType_LTQ_Orbitrap_Discovery:
        case InstrumentModelType_LTQ_Orbitrap_XL:
        case InstrumentModelType_LTQ_Orbitrap_XL_ETD:
        case InstrumentModelType_MALDI_LTQ_Orbitrap:
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
        case InstrumentModelType_LTQ_XL_ETD:
        case InstrumentModelType_ITQ_1100:
        case InstrumentModelType_MALDI_LTQ_XL:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_SSQ_7000:
        case InstrumentModelType_Surveyor_MSQ:
        case InstrumentModelType_DSQ:
        case InstrumentModelType_DSQ_II:
        case InstrumentModelType_Trace_DSQ:
        case InstrumentModelType_GC_IsoLink:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(commonSource);
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentModelType_TSQ_7000:
        case InstrumentModelType_TSQ:
        case InstrumentModelType_TSQ_Quantum:
        case InstrumentModelType_TSQ_Quantum_Access:
        case InstrumentModelType_TSQ_Quantum_Ultra:
        case InstrumentModelType_TSQ_Quantum_Ultra_AM:
        case InstrumentModelType_GC_Quantum:
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
        case MassAnalyzerType_Linear_Ion_Trap:      return MS_radial_ejection_linear_ion_trap;
        case MassAnalyzerType_Quadrupole_Ion_Trap:  return MS_quadrupole_ion_trap;
        case MassAnalyzerType_Orbitrap:             return MS_orbitrap;
        case MassAnalyzerType_FTICR:                return MS_FT_ICR;
        case MassAnalyzerType_TOF:                  return MS_time_of_flight;
        case MassAnalyzerType_Triple_Quadrupole:    return MS_quadrupole;
        case MassAnalyzerType_Single_Quadrupole:    return MS_quadrupole;
        case MassAnalyzerType_Magnetic_Sector:      return MS_magnetic_sector;
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
}*/

} // detail
} // msdata
} // pwiz
