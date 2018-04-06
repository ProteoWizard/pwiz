//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "Reader_Agilent_Detail.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {
namespace detail {
namespace Agilent {


PWIZ_API_DECL
vector<InstrumentConfiguration> createInstrumentConfigurations(MassHunterDataPtr rawfile)
{
    vector<InstrumentConfiguration> configurations;

    DeviceType deviceType = rawfile->getDeviceType();
    IonizationMode ionModes = rawfile->getIonModes();

    set<IonizationMode> ionModeSet;
    if (ionModes & IonizationMode_EI)           ionModeSet.insert(IonizationMode_EI);
    if (ionModes & IonizationMode_CI)           ionModeSet.insert(IonizationMode_CI);
    if (ionModes & IonizationMode_Maldi)        ionModeSet.insert(IonizationMode_Maldi);
    if (ionModes & IonizationMode_Appi)         ionModeSet.insert(IonizationMode_Appi);
    if (ionModes & IonizationMode_Apci)         ionModeSet.insert(IonizationMode_Apci);
    if (ionModes & IonizationMode_Esi)          ionModeSet.insert(IonizationMode_Esi);
    if (ionModes & IonizationMode_NanoEsi)      ionModeSet.insert(IonizationMode_NanoEsi);
    if (ionModes & IonizationMode_MsChip)       ionModeSet.insert(IonizationMode_MsChip);
    if (ionModes & IonizationMode_ICP)          ionModeSet.insert(IonizationMode_ICP);
    if (ionModes & IonizationMode_JetStream)    ionModeSet.insert(IonizationMode_JetStream);

    for (set<IonizationMode>::iterator ionModeItr = ionModeSet.begin();
         ionModeItr != ionModeSet.end();
         ++ionModeItr)
    {
        Component commonSource(ComponentType_Source, 1);
        commonSource.set(translateAsIonizationType(*ionModeItr));
        commonSource.set(translateAsInletType(*ionModeItr));

        switch (deviceType)
        {
            case DeviceType_Unknown:
            default:
                break;

            case DeviceType_Mixed:
                throw runtime_error("[createInstrumentConfigurations] Mixed device types not supported.");

            case DeviceType_Quadrupole:
                configurations.push_back(InstrumentConfiguration());
                configurations.back().componentList.push_back(commonSource);
                configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
                configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
                break;

            case DeviceType_IonTrap:
                configurations.push_back(InstrumentConfiguration());
                configurations.back().componentList.push_back(commonSource);
                configurations.back().componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
                configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
                break;

            case DeviceType_TimeOfFlight:
                configurations.push_back(InstrumentConfiguration());
                configurations.back().componentList.push_back(commonSource);
                configurations.back().componentList.push_back(Component(MS_time_of_flight, 2));
                configurations.back().componentList.push_back(Component(MS_multichannel_plate, 3));
                configurations.back().componentList.push_back(Component(MS_photomultiplier, 4));
                break;

            case DeviceType_TandemQuadrupole:
                configurations.push_back(InstrumentConfiguration());
                configurations.back().componentList.push_back(commonSource);
                configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
                configurations.back().componentList.push_back(Component(MS_quadrupole, 3));
                configurations.back().componentList.push_back(Component(MS_quadrupole, 4));
                configurations.back().componentList.push_back(Component(MS_electron_multiplier, 5));
                break;

            case DeviceType_QuadrupoleTimeOfFlight:
                configurations.push_back(InstrumentConfiguration());
                configurations.back().componentList.push_back(commonSource);
                configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
                configurations.back().componentList.push_back(Component(MS_quadrupole, 3));
                configurations.back().componentList.push_back(Component(MS_time_of_flight, 4));
                configurations.back().componentList.push_back(Component(MS_multichannel_plate, 5));
                configurations.back().componentList.push_back(Component(MS_photomultiplier, 6));
                break;
        }
    }

    return configurations;
}


PWIZ_API_DECL CVID translateAsInstrumentModel(DeviceType deviceType)
{
    return MS_Agilent_instrument_model;
}


PWIZ_API_DECL CVID translateAsSpectrumType(MSScanType scanType)
{
    if (scanType == MSScanType_Scan)                return MS_MS1_spectrum;
    if (scanType == MSScanType_ProductIon)          return MS_MSn_spectrum;
    if (scanType == MSScanType_PrecursorIon)        return MS_precursor_ion_spectrum;
    if (scanType == MSScanType_SelectedIon)         return MS_SIM_spectrum;
    if (scanType == MSScanType_TotalIon)            return MS_SIM_spectrum;
    if (scanType == MSScanType_MultipleReaction)    return MS_SRM_spectrum;
    if (scanType == MSScanType_NeutralLoss)         return MS_constant_neutral_loss_scan_OBSOLETE;
    if (scanType == MSScanType_NeutralGain)         return MS_constant_neutral_gain_scan_OBSOLETE;
    throw runtime_error("[translateAsSpectrumType] Error parsing spectrum type.");
}


PWIZ_API_DECL int translateAsMSLevel(MSScanType scanType)
{
    if (scanType == MSScanType_Scan)                return 1;
    if (scanType == MSScanType_ProductIon)          return 2;
    if (scanType == MSScanType_PrecursorIon)        return -1;
    if (scanType == MSScanType_SelectedIon)         return 1;
    if (scanType == MSScanType_TotalIon)            return 1;
    if (scanType == MSScanType_MultipleReaction)    return 2;
    if (scanType == MSScanType_NeutralLoss)         return 2;
    if (scanType == MSScanType_NeutralGain)         return 2;
    throw runtime_error("[translateAsMSLevel] Error parsing MS level.");
}


PWIZ_API_DECL CVID translateAsActivationType(DeviceType deviceType)
{
    switch (deviceType)
    {
        case DeviceType_Mixed:
            throw runtime_error("[translateAsActivationType] Mixed device types not supported.");

        default:
        case DeviceType_Unknown:
            return MS_CID;

        case DeviceType_IonTrap:
            return MS_trap_type_collision_induced_dissociation;

        case DeviceType_TandemQuadrupole:
        case DeviceType_Quadrupole:
        case DeviceType_QuadrupoleTimeOfFlight:
            return MS_beam_type_collision_induced_dissociation;

        case DeviceType_TimeOfFlight:
            return MS_in_source_collision_induced_dissociation; // no collision cell, but this kind of activation is still possible
    }
}


PWIZ_API_DECL CVID translateAsPolarityType(IonPolarity polarity)
{
    if (polarity == IonPolarity_Positive)          return MS_positive_scan;
    if (polarity == IonPolarity_Negative)          return MS_negative_scan;
    throw runtime_error("[translateAsPolarityType] Error parsing polarity type.");
}


PWIZ_API_DECL CVID translateAsIonizationType(IonizationMode ionizationMode)
{
    switch (ionizationMode)
    {
        case IonizationMode_EI:                     return MS_electron_ionization;
        case IonizationMode_CI:                     return MS_chemical_ionization;
        case IonizationMode_Maldi:                  return MS_matrix_assisted_laser_desorption_ionization;
        case IonizationMode_Appi:                   return MS_atmospheric_pressure_photoionization;
        case IonizationMode_Apci:                   return MS_atmospheric_pressure_chemical_ionization;
        case IonizationMode_Esi:                    return MS_microelectrospray;
        case IonizationMode_NanoEsi:                return MS_nanoelectrospray;
        case IonizationMode_MsChip:                 return MS_nanoelectrospray;
        case IonizationMode_ICP:                    return MS_plasma_desorption_ionization;
        case IonizationMode_JetStream:              return MS_nanoelectrospray;

        case IonizationMode_Unspecified:
        case IonizationMode_Mixed:
        default:
            return CVID_Unknown;
    }
}

    
PWIZ_API_DECL CVID translateAsInletType(IonizationMode ionizationMode)
{
    switch (ionizationMode)
    {
        case IonizationMode_EI:                     return MS_direct_inlet;
        case IonizationMode_CI:                     return MS_direct_inlet;
        case IonizationMode_Maldi:                  return MS_particle_beam;
        case IonizationMode_Appi:                   return MS_direct_inlet;
        case IonizationMode_Apci:                   return MS_direct_inlet;
        case IonizationMode_Esi:                    return MS_electrospray_inlet;
        case IonizationMode_NanoEsi:                return MS_nanospray_inlet;
        case IonizationMode_MsChip:                 return MS_nanospray_inlet;
        case IonizationMode_ICP:                    return MS_inductively_coupled_plasma;
        case IonizationMode_JetStream:              return MS_nanospray_inlet;

        case IonizationMode_Unspecified:
        case IonizationMode_Mixed:
        default:
            return CVID_Unknown;
    }
}


} // Agilent
} // detail
} // msdata
} // pwiz
