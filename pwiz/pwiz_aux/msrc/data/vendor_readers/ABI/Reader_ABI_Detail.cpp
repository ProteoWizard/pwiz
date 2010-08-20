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

#include "Reader_ABI_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"

#ifdef PWIZ_READER_ABI
using namespace pwiz::vendor_api::ABI;


namespace pwiz {
namespace msdata {
namespace detail {


PWIZ_API_DECL
InstrumentConfigurationPtr translateAsInstrumentConfiguration(WiffFilePtr wifffile)
{
    InstrumentConfigurationPtr icPtr(new InstrumentConfiguration);
    InstrumentConfiguration& ic = *icPtr;

    ic.id = "IC1";
    ic.set(translateAsInstrumentModel(wifffile->getInstrumentModel()));

    Component source(ComponentType_Source, 1);
    source.set(translateAsIonSource(wifffile->getIonSourceType()));

    switch (wifffile->getInstrumentModel())
    {
        // QqQ
        case API150MCA:
        case API150EX:
        case API2000:
        case API3000:
        case API3200:
        case API4000:
        case API5000:
        case API100:
        case API100LC:
        case API165:
        case API300:
        case API350:
        case API365:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_quadrupole, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        // QqLIT
        case API2000QTrap:
        case API3200QTrap:
        case API4000QTrap:
        case API5500QTrap:
        case CaribouQTrap:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_axial_ejection_linear_ion_trap, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        // QqTOF
        case QStar:
        case QStarPulsarI:
        case QStarXL:
        case QStarElite:
        case API5600TripleTOF:
        case NlxTof:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_quadrupole, 3));
            ic.componentList.push_back(Component(MS_time_of_flight, 4));
            ic.componentList.push_back(Component(MS_electron_multiplier, 5));
            break;

        case GenericSingleQuad:
            ic.componentList.push_back(source);
            ic.componentList.push_back(Component(MS_quadrupole, 2));
            ic.componentList.push_back(Component(MS_electron_multiplier, 3));
            break;
    }

    return icPtr;
}


PWIZ_API_DECL CVID translateAsInstrumentModel(InstrumentModel instrumentModel)
{
    switch (instrumentModel)
    {
        case API150MCA:         return MS_API_150EX;
        case API150EX:          return MS_API_150EX;
        case API2000:           return MS_API_2000;
        case API3000:           return MS_API_3000;
        case API3200:           return MS_API_3200;
        case API3200QTrap:      return MS_3200_QTRAP;
        case API4000:           return MS_API_4000;
        case API4000QTrap:      return MS_4000_Q_TRAP;
        case API5000:           return MS_API_5000;
        case API5600TripleTOF:  return MS_AB_SCIEX_TripleTOF_5600;
        case API5500QTrap:      return MS_AB_SCIEX_QTRAP_5500;
        case QStar:             return MS_QSTAR;
        case QStarPulsarI:      return MS_QSTAR_Pulsar;
        case QStarXL:           return MS_QSTAR_XL;
        case QStarElite:        return MS_QSTAR_Elite;

        case CaribouQTrap:
        case NlxTof:
        case API100:
        case API100LC:
        case API165:
        case API300:
        case API350:
        case API365:
        case API2000QTrap:
        case GenericSingleQuad:
        default:
            return MS_Applied_Biosystems_instrument_model;
    }
}

PWIZ_API_DECL CVID translateAsIonSource(IonSourceType ionSourceType)
{
    switch (ionSourceType)
    {
        case FlowNanoSpray:     return MS_nanoelectrospray;
        case HeatedNebulizer:   return MS_atmospheric_pressure_chemical_ionization;
        case TurboSpray:        return MS_electrospray_ionization;
        case IonSpray:          return MS_electrospray_ionization;
        case Maldi:             return MS_matrix_assisted_laser_desorption_ionization;
        case PhotoSpray:        return MS_atmospheric_pressure_photoionization;

        case Medusa:
        case Duo:
        case None:
        default:
            return CVID_Unknown;
    }
}


PWIZ_API_DECL CVID translateAsSpectrumType(ScanType scanType)
{
    switch (scanType)
    {
        default:
        case UNDEFINED_SCAN:                return CVID_Unknown;

        case Q1_SCAN:                       return MS_MS1_spectrum;
        case SIM_Q1:                        return MS_SIM_spectrum;
        case Q3_SCAN:                       return MS_MS1_spectrum;
        case SIM_Q3:                        return MS_SIM_spectrum;
        case MRM:                           return MS_MRM_spectrum;
        case PRECURSOR_ION_SCAN:            return MS_precursor_ion_spectrum;
        case PRODUCT_ION_SCAN:              return MS_MSn_spectrum;
        case NEUTRAL_LOSS_SCAN:             return MS_constant_neutral_loss_spectrum;
        case TOF_MS_SCAN:                   return MS_MS1_spectrum;
        case TOF_PRODUCT_ION_SCAN:          return MS_MSn_spectrum;
        case TOF_PRECURSOR_ION_SCAN:        return MS_precursor_ion_spectrum;
        case ENHANCED_PRODUCT_ION_SCAN:     return MS_MSn_spectrum;
        case ENHANCED_RESOLUTION_SCAN:      return MS_MS1_spectrum;
        case MSMSMS_SCAN:                   return MS_MSn_spectrum;
        case TIMEDELAY_FRAGMENTATION_SCAN:  return MS_time_delayed_fragmentation_spectrum;
        case ENHANCED_MS_SCAN:              return MS_MS1_spectrum;
        case ENHANCED_MULTCHARGE_SCAN:      return MS_enhanced_multiply_charged_spectrum;
    }
}


PWIZ_API_DECL int translateAsMSLevel(ScanType scanType)
{
    switch (scanType)
    {
        default:
        case UNDEFINED_SCAN:                return 0;

        case Q1_SCAN:                       return 1;
        case SIM_Q1:                        return 1;
        case Q3_SCAN:                       return 2;
        case SIM_Q3:                        return 2;
        case MRM:                           return 2;
        case PRECURSOR_ION_SCAN:            return 2;
        case PRODUCT_ION_SCAN:              return 2;
        case NEUTRAL_LOSS_SCAN:             return 2;
        case TOF_MS_SCAN:                   return 1;
        case TOF_PRODUCT_ION_SCAN:          return 2;
        case TOF_PRECURSOR_ION_SCAN:        return 2;
        case ENHANCED_PRODUCT_ION_SCAN:     return 2;
        case ENHANCED_RESOLUTION_SCAN:      return 1;
        case MSMSMS_SCAN:                   return 3;
        case TIMEDELAY_FRAGMENTATION_SCAN:  return 2;
        case ENHANCED_MS_SCAN:              return 1;
        case ENHANCED_MULTCHARGE_SCAN:      return 1;
    }
}


PWIZ_API_DECL CVID translate(Polarity polarity)
{
    switch (polarity)
    {
        case Positive:
            return MS_positive_scan;
        case Negative:
            return MS_negative_scan;
        case NotDefined:
        default:
            return CVID_Unknown;
    }
}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_ABI
