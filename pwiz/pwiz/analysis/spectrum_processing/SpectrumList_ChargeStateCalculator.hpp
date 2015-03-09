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


#ifndef _SPECTRUMLIST_CHARGESTATECALCULATOR_HPP_ 
#define _SPECTRUMLIST_CHARGESTATECALCULATOR_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation that assigns (probable) charge states to tandem mass spectra
class PWIZ_API_DECL SpectrumList_ChargeStateCalculator : public msdata::SpectrumListWrapper
{
    public:
    SpectrumList_ChargeStateCalculator(const msdata::SpectrumListPtr& inner,
                                       bool overrideExistingChargeState = true,
                                       int maxMultipleCharge = 3,
                                       int minMultipleCharge = 2,
                                       double intensityFractionBelowPrecursorForSinglyCharged = 0.9,
                                       int maxKnownCharge = 0,
                                       bool makeMS2 = false);

    /// accepts any tandem mass spectrum
    static bool accept(const msdata::SpectrumListPtr& inner) {return true;}

    /// charge calculation requires binary data to function, so returned spectra will always provide the binary data
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = true) const;

    private:
    bool override_;
    int maxCharge_;
    int minCharge_;
    double fraction_;
    int maxKnownCharge_;
    bool makeMS2_;
};


} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_CHARGESTATECALCULATOR_HPP_
