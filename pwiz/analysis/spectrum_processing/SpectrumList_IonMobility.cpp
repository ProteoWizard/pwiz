//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2016 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_IonMobility.hpp"
#include "pwiz/utility/misc/Std.hpp"

#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/data/vendor_readers/Bruker/SpectrumList_Bruker.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"
#include "pwiz/data/vendor_readers/Thermo/SpectrumList_Thermo.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_IonMobility::SpectrumList_IonMobility(const msdata::SpectrumListPtr& inner)
:   SpectrumListWrapper(inner), equipment_(SpectrumList_IonMobility::eIonMobilityEquipment::None)
{
    units_ = eIonMobilityUnits::none;
    if (dynamic_cast<detail::SpectrumList_Agilent*>(&*inner) != NULL)
    {
        equipment_ = eIonMobilityEquipment::AgilentDrift;
        units_ = eIonMobilityUnits::drift_time_msec;
    }
    else if (dynamic_cast<detail::SpectrumList_Waters*>(&*inner) != NULL)
    {
        equipment_ = eIonMobilityEquipment::WatersDrift;
        units_ = eIonMobilityUnits::drift_time_msec; 
    }
    else if (dynamic_cast<detail::SpectrumList_Bruker*>(&*inner) != NULL)
    {
        if ((dynamic_cast<detail::SpectrumList_Bruker*>(&*inner))->hasIonMobility())
        {
            equipment_ = eIonMobilityEquipment::BrukerTIMS;
            units_ = eIonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2;
        }
    }
    else if (dynamic_cast<detail::SpectrumList_Thermo*>(&*inner) != NULL)
    {
        if ((dynamic_cast<detail::SpectrumList_Thermo*>(&*inner))->hasIonMobility())
        {
            equipment_ = eIonMobilityEquipment::ThermoFAIMS;
            units_ = eIonMobilityUnits::compensation_V;
        }
    }
    else // reading an mzML conversion?
    {
        try
        {
            // See if first scan has any ion mobility data
            SpectrumPtr spectrum = inner->spectrum(0, false);
            if (spectrum != NULL)
            {
                Scan dummy;
                Scan& scan = spectrum->scanList.scans.empty() ? dummy : spectrum->scanList.scans[0];
                if (scan.hasCVParam(CVID::MS_ion_mobility_drift_time))
                    units_ = eIonMobilityUnits::drift_time_msec;
                else if (scan.hasCVParam(CVID::MS_inverse_reduced_ion_mobility))
                    units_ = eIonMobilityUnits::inverse_reduced_ion_mobility_Vsec_per_cm2;
                else if (scan.hasCVParam(CVID::MS_FAIMS_compensation_voltage))
                    units_ = eIonMobilityUnits::compensation_V;
                else if (!scan.userParam("drift time").empty()) // Oldest known mzML drift time style
                    units_ = eIonMobilityUnits::drift_time_msec;
            }
        }
        catch (...)
        {
            // No scans to check
        }
    }
}

PWIZ_API_DECL SpectrumList_IonMobility::eIonMobilityUnits SpectrumList_IonMobility::getIonMobilityUnits() const
{
    return units_;
}

PWIZ_API_DECL bool SpectrumList_IonMobility::accept(const msdata::SpectrumListPtr& inner)
{
    return true; // We'll wrap anything, but getIonMobilityUnits() may well return "none"
}

PWIZ_API_DECL SpectrumPtr SpectrumList_IonMobility::spectrum(size_t index, bool getBinaryData) const
{
    return inner_->spectrum(index, getBinaryData);
}

PWIZ_API_DECL bool SpectrumList_IonMobility::canConvertIonMobilityAndCCS(eIonMobilityUnits units) const
{
    if (units != units_)
        return false; // wrong units for this equipment

    switch (equipment_)
    {
    default:
        return false; // Only Agilent and Bruker provide this capabilty, for now

    case eIonMobilityEquipment::BrukerTIMS:
        return dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->canConvertInverseK0AndCCS();

    case eIonMobilityEquipment::AgilentDrift:
        return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->canConvertDriftTimeAndCCS();
    }
}

PWIZ_API_DECL double SpectrumList_IonMobility::ionMobilityToCCS(double ionMobility, double mz, int charge) const
{
    switch (equipment_)
    {
        default:
            throw runtime_error("SpectrumList_IonMobility::ionMobilityToCCS function only supported when reading native Agilent or Bruker files with ion-mobility data");

        case eIonMobilityEquipment::BrukerTIMS:  return dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->inverseK0ToCCS(ionMobility, mz, charge);

        case eIonMobilityEquipment::AgilentDrift: return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->driftTimeToCCS(ionMobility, mz, charge);
    }
}


PWIZ_API_DECL double SpectrumList_IonMobility::ccsToIonMobility(double ccs, double mz, int charge) const
{
    switch (equipment_)
    {
        default:
            throw runtime_error("SpectrumList_IonMobility::ccsToIonMobility] function only supported when reading native Agilent or Bruker files with ion-mobility data");

        case eIonMobilityEquipment::BrukerTIMS:  
            return dynamic_cast<detail::SpectrumList_Bruker*>(&*inner_)->ccsToInverseK0(ccs, mz, charge);
        case eIonMobilityEquipment::AgilentDrift: 
            return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->ccsToDriftTime(ccs, mz, charge);
    }
}

} // namespace analysis 
} // namespace pwiz
