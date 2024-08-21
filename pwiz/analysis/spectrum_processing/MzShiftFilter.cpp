//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 <a.t> gmail.com>
//
// Copyright 2024 University of Washington, Seattle, WA
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


#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "MzShiftFilter.hpp"

#include <boost/range/algorithm/for_each.hpp>

namespace pwiz {
namespace analysis {

using namespace msdata;
using namespace pwiz::util;
using namespace pwiz::chemistry;


PWIZ_API_DECL MzShiftFilter::MzShiftFilter(const MZTolerance& mzShift, const IntegerSet& msLevelSet)
    : mzShift_(mzShift), msLevelSet_(msLevelSet)
{
}

PWIZ_API_DECL void MzShiftFilter::operator()(const SpectrumPtr& s) const
{
    int msLevel = s->cvParamValueOrDefault(MS_ms_level, 0);
    if (!msLevelSet_.contains(msLevel) && !msLevelSet_.contains(msLevel - 1))
        return;

    auto doMzShift = [&](ParamContainer& p)
    {
        for (auto& cvParam : p.cvParams)
            if (cvParam.units == MS_m_z)
                cvParam.value = lexical_cast<string>(cvParam.valueAs<double>() + mzShift_);
    };

    // shift current ms level if specified in msLevelSet
    if (msLevelSet_.contains(msLevel))
    {
        BinaryData<double>& mzArray = s->getMZArray()->data;
        for (auto& mz : mzArray)
            mz += mzShift_;

        doMzShift(*s);
        for (auto& scan : s->scanList.scans) doMzShift(scan);
    }

    // shift precursor ms level if specified in msLevelSet
    if (msLevelSet_.contains(msLevel - 1))
        for (auto& precursor : s->precursors)
        {
            doMzShift(precursor);
            doMzShift(precursor.activation);
            doMzShift(precursor.isolationWindow);
            for (auto& si : precursor.selectedIons) doMzShift(si);
        }

    // CONSIDER: does this make sense for <product>?
    //if (msLevelSet_.contains(msLevel + 1))
    //    for (auto& product : s->products) doMzShift(product.isolationWindow);
}

PWIZ_API_DECL void MzShiftFilter::describe(ProcessingMethod& method) const
{
    method.set(MS_data_filtering);
    method.userParams.emplace_back("m/z shift", lexical_cast<string>(mzShift_));
    method.userParams.emplace_back("ms levels", lexical_cast<string>(msLevelSet_));
}

} // namespace analysis 
} // namespace pwiz
