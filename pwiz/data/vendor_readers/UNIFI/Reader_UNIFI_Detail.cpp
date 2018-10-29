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

#include "Reader_UNIFI_Detail.hpp"
#include "pwiz/utility/misc/Std.hpp"

#ifdef PWIZ_READER_UNIFI
using namespace pwiz::vendor_api::UNIFI;


namespace pwiz {
namespace msdata {
namespace detail {
namespace UNIFI {


/*PWIZ_API_DECL
InstrumentConfigurationPtr translateAsInstrumentConfiguration(InstrumentModel instrumentModel, IonSourceType ionSource)
{
    InstrumentConfigurationPtr icPtr(new InstrumentConfiguration);
    InstrumentConfiguration& ic = *icPtr;

    ic.id = "IC1";
    ic.set(translateAsInstrumentModel(instrumentModel));

    Component source(ComponentType_Source, 1);
    source.set(translateAsIonSource(ionSource));

    switch (instrumentModel)
    {
        // QqQ
        case API150MCA:
        case API365:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_quadrupole, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        // QqLIT
        case API2000QTrap:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_axial_ejection_linear_ion_trap, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        // QqTOF
        case QStar:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_time_of_flight, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        case InstrumentModel_Unknown:
            break;

        default:
            throw runtime_error("[translateAsInstrumentConfiguration] unhandled instrument model: " + lexical_cast<string>(instrumentModel));
    }

    return icPtr;
}


PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModel instrumentModel)
{
    switch (instrumentModel)
    {
        case InstrumentModel_Unknown:
            return MS_Applied_Biosystems_instrument_model;

        default:
            throw runtime_error("[translateAsInstrumentModel] unhandled instrument model: " + lexical_cast<string>(instrumentModel));
    }
}

PWIZ_API_DECL CVID translateAsIonSource(IonSourceType ionSourceType)
{
    switch (ionSourceType)
    {
        case IonSourceType_Unknown: return MS_ionization_type;
        case FlowNanoSpray:         return MS_nanoelectrospray;
        case HeatedNebulizer:       return MS_atmospheric_pressure_chemical_ionization;
        case TurboSpray:            return MS_electrospray_ionization;
        case IonSpray:              return MS_electrospray_ionization;
        case Maldi:                 return MS_matrix_assisted_laser_desorption_ionization;
        case PhotoSpray:            return MS_atmospheric_pressure_photoionization;

        case Medusa:
        case Duo:
        case None:
            return CVID_Unknown;

        default:
            throw runtime_error("[translateAsIonSource] unhandled ion source: " + lexical_cast<string>(ionSourceType));
    }
}


PWIZ_API_DECL CVID translateAsSpectrumType(ExperimentType experimentType)
{
    switch (experimentType)
    {
        case MS:                            return MS_MS1_spectrum;
        case vendor_api::ABI::Product:      return MS_MSn_spectrum;
        case vendor_api::ABI::Precursor:    return MS_precursor_ion_spectrum;
        case NeutralGainOrLoss:             return MS_constant_neutral_loss_spectrum;
        case SIM:                           return MS_SIM_spectrum;
        case MRM:                           return MS_SRM_spectrum;

        default:                            return CVID_Unknown;
    }
}*/



PWIZ_API_DECL CVID translate(Polarity polarity)
{
    switch (polarity)
    {
        case Polarity::Positive:
            return MS_positive_scan;
        case Polarity::Negative:
            return MS_negative_scan;
        case Polarity::Unknown:
        default:
            return CVID_Unknown;
    }
}

} // UNIFI
} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_UNIFI
