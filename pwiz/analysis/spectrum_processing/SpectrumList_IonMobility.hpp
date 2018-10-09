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


#ifndef _SPECTRUMLIST_IONMOBILITY_HPP_ 
#define _SPECTRUMLIST_IONMOBILITY_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"

namespace pwiz {
namespace analysis {

/// SpectrumList implementation that provides access to vendor-specific ion mobility functions
class PWIZ_API_DECL SpectrumList_IonMobility : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_IonMobility(const msdata::SpectrumListPtr& inner);

    static bool accept(const msdata::SpectrumListPtr& inner);
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    // N.B this should agree with the enum IONMOBILITY_TYPE in pwiz_tools\BiblioSpec\src\BlibUtils.h
    enum class IonMobilityUnits { none, drift_time_msec, inverse_reduced_ion_mobility_Vsec_per_cm2, compensation_V };

    virtual IonMobilityUnits getIonMobilityUnits() const;

    /// returns true if file in question contains necessary information for CCS/IonMobility handling (as with Drift Time in Agilent)
    virtual bool canConvertIonMobilityAndCCS(IonMobilityUnits units) const;

    /// returns collisional cross-section associated with the ion mobility (units depend on IonMobilityEquipment)
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const;

    /// returns the ion mobility (units depend on IonMobilityEquipment) associated with the given collisional cross-section
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

    /// for Waters SONAR data, given a (0-based) function number, a precursor m/z, and a tolerance, return the corresponding start and end "drift" bins
    virtual std::pair<int, int> sonarMzToDriftBinRange(int function, float precursorMz, float precursorTolerance) const;


private:
    enum class IonMobilityEquipment { None, AgilentDrift, WatersDrift, WatersSonar, BrukerTIMS, ThermoFAIMS };
    IonMobilityEquipment equipment_;
    IonMobilityUnits units_;
    msdata::SpectrumListIonMobilityBase* sl_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_IONMOBILITY_HPP_ 
