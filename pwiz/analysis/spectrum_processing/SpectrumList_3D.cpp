//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_3D.hpp"
#include "pwiz/utility/misc/Std.hpp"

#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/data/vendor_readers/Waters/SpectrumList_Waters.hpp"
#include "pwiz/data/vendor_readers/UIMF/SpectrumList_UIMF.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_3D::SpectrumList_3D(const msdata::SpectrumListPtr& inner)
:   SpectrumListWrapper(inner),
    mode_(0)
{
    detail::SpectrumList_Agilent* agilent = dynamic_cast<detail::SpectrumList_Agilent*>(&*inner);
    if (agilent)
    {
        mode_ = 1;
    }

    detail::SpectrumList_Waters* waters = dynamic_cast<detail::SpectrumList_Waters*>(&*inner);
    if (waters)
    {
        mode_ = 2;
    }

    detail::SpectrumList_UIMF* uimf = dynamic_cast<detail::SpectrumList_UIMF*>(&*inner);
    if (uimf)
    {
        mode_ = 3;
    }
}


PWIZ_API_DECL bool SpectrumList_3D::accept(const msdata::SpectrumListPtr& inner)
{
    return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner) || dynamic_cast<detail::SpectrumList_Waters*>(&*inner) || dynamic_cast<detail::SpectrumList_UIMF*>(&*inner);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_3D::spectrum(size_t index, bool getBinaryData) const
{
    return inner_->spectrum(index, getBinaryData);
}


PWIZ_API_DECL Spectrum3DPtr SpectrumList_3D::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const
{
    switch (mode_)
    {
        default:
        case 0:
            throw runtime_error("[SpectrumList_3D::spectrum3d] 3d spectra currently only supported for Agilent, Waters, and UIMF");

        case 1:
            return dynamic_cast<detail::SpectrumList_Agilent*>(&*inner_)->spectrum3d(scanStartTime, driftTimeRanges);

        case 2:
            return dynamic_cast<detail::SpectrumList_Waters*>(&*inner_)->spectrum3d(scanStartTime, driftTimeRanges);

        case 3:
            return dynamic_cast<detail::SpectrumList_UIMF*>(&*inner_)->spectrum3d(scanStartTime, driftTimeRanges);
    }
}


} // namespace analysis 
} // namespace pwiz
