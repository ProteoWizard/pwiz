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


#ifndef _SPECTRUMLIST_PEAKPICKER_HPP_ 
#define _SPECTRUMLIST_PEAKPICKER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/common/LocalMaximumPeakDetector.hpp"
#include "pwiz/analysis/common/CwtPeakDetector.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation to replace peak profiles with picked peaks
class PWIZ_API_DECL SpectrumList_PeakPicker : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_PeakPicker(const msdata::SpectrumListPtr& inner,
                            PeakDetectorPtr algorithm,
                            bool preferVendorPeakPicking,
                            const util::IntegerSet& msLevelsToPeakPick);

    /// returns true if the given file 
    static bool supportsVendorPeakPicking(const std::string& rawpath);

    static bool accept(const msdata::SpectrumListPtr& inner);

    virtual msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel detailLevel) const;
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    const util::IntegerSet& msLevels() const { return msLevelsToPeakPick_; }

    private:
    PeakDetectorPtr algorithm_;
    const util::IntegerSet msLevelsToPeakPick_;
    int mode_;
};

class PWIZ_API_DECL NoVendorPeakPickingException : public std::runtime_error
{
    public:
    NoVendorPeakPickingException() : std::runtime_error("[PeakDetector::NoVendorPeakPickingException]") {}
};



} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_PEAKPICKER_HPP_ 
