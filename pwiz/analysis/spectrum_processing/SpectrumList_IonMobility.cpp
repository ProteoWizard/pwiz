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
//#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_IonMobility::SpectrumList_IonMobility(const msdata::SpectrumListPtr& inner)
:   SpectrumListWrapper(inner),
    mode_(0)
{
    detail::SpectrumList_Agilent* agilent = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner);
    if (agilent)
    {
        mode_ = 1;
    }

    /*detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner);
    if (waters)
    {
        mode_ = 2;
    }*/
}


PWIZ_API_DECL bool SpectrumList_IonMobility::accept(const msdata::SpectrumListPtr& inner)
{
    return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner) != NULL /*|| dynamic_cast<detail::SpectrumList_Waters*>(&*inner)*/;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_IonMobility::spectrum(size_t index, bool getBinaryData) const
{
    return inner_->spectrum(index, getBinaryData);
}

PWIZ_API_DECL bool SpectrumList_IonMobility::canConvertDriftTimeAndCCS() const
{
    switch (mode_)
    {
    case 0:
    default:
        return false; // Only Agilent provides this capabilty, for now

    case 1: return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->canConvertDriftTimeAndCCS();
    }
}

PWIZ_API_DECL double SpectrumList_IonMobility::driftTimeToCCS(double driftTime, double mz, int charge) const
{
    switch (mode_)
    {
        case 0:
        default:
            throw runtime_error("SpectrumList_IonMobility::driftTimeToCCS] function only supported when reading native Agilent MassHunter files with ion-mobility data");

        case 1: return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->driftTimeToCCS(driftTime, mz, charge);
    }
}


PWIZ_API_DECL double SpectrumList_IonMobility::ccsToDriftTime(double ccs, double mz, int charge) const
{
    switch (mode_)
    {
        case 0:
        default:
            throw runtime_error("SpectrumList_IonMobility::ccsToDriftTime] function only supported when reading native Agilent MassHunter files with ion-mobility data");

        case 1: return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->ccsToDriftTime(ccs, mz, charge);
    }
}


} // namespace analysis 
} // namespace pwiz
