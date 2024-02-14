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


#ifndef _MZSHIFTFILTER_HPP_ 
#define _MZSHIFTFILTER_HPP_ 


#include "pwiz/analysis/common/DataFilter.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"


namespace pwiz {
namespace analysis {


using chemistry::MZTolerance;


struct PWIZ_API_DECL MzShiftFilter : public SpectrumDataFilter
{
    MzShiftFilter(const MZTolerance& mzShift, const util::IntegerSet& msLevelSet);
    void operator () (const pwiz::msdata::SpectrumPtr&) const override;
    void describe(pwiz::msdata::ProcessingMethod&) const override;

    private:
    MZTolerance mzShift_;
    util::IntegerSet msLevelSet_;
};

} // namespace analysis 
} // namespace pwiz


#endif // _MZSHIFTFILTER_HPP_ 
