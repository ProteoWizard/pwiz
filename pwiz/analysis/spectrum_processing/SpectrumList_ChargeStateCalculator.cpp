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


#include "SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <numeric>


namespace {

bool mzIntensityPairLessThan (const pwiz::msdata::MZIntensityPair& lhs, const pwiz::msdata::MZIntensityPair& rhs)
{
    return lhs.mz > rhs.mz;
}

struct MZIntensityPairIntensitySum
{
    double operator() (double lhs, const pwiz::msdata::MZIntensityPair& rhs)
    {
        return lhs + rhs.intensity;
    }
};

} // namespace


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL SpectrumList_ChargeStateCalculator::SpectrumList_ChargeStateCalculator(
    const msdata::SpectrumListPtr& inner,
    bool overrideExistingChargeState,
    int maxMultipleCharge,
    int minMultipleCharge,
    double intensityFractionBelowPrecursorForSinglyCharged)
:   SpectrumListWrapper(inner),
    override_(overrideExistingChargeState),
    maxCharge_(maxMultipleCharge),
    minCharge_(minMultipleCharge),
    fraction_(intensityFractionBelowPrecursorForSinglyCharged)
{
}

PWIZ_API_DECL SpectrumPtr SpectrumList_ChargeStateCalculator::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s = inner_->spectrum(index, true);

    // return non-MS/MS as-is
    CVParam spectrumType = s->cvParamChild(MS_spectrum_type);
    if (spectrumType != MS_MSn_spectrum)
        return s;

    // return MS1 as-is
    CVParam msLevel = s->cvParam(MS_ms_level);
    if (msLevel.valueAs<int>() < 2)
        return s;

    // return precursorless MS/MS as-is
    if (s->precursors.empty() ||
        s->precursors[0].selectedIons.empty())
        return s;

    // use first selected ion in first precursor
    // TODO: how to deal with multiple precursors and/or selected ions?
    SelectedIon& selectedIon = s->precursors[0].selectedIons[0];

    // if overriding, erase any existing charge-state-related CV params;
    // otherwise:
    //   * keep track of existing "possible charge state"
    //   * return as-is if there is a "charge state"
    vector<CVParam>& cvParams = selectedIon.cvParams;
    IntegerSet possibleChargeStates;
    for(vector<CVParam>::iterator itr = cvParams.begin(); itr != cvParams.end(); ++itr)
    {
        if (override_ &&
            (itr->cvid == MS_charge_state ||
             itr->cvid == MS_possible_charge_state))
        {
            itr = cvParams.erase(itr);
            if (itr == cvParams.end())
                break;
        }
        else if (itr->cvid == MS_possible_charge_state)
            possibleChargeStates.insert(itr->valueAs<int>());
        else if (itr->cvid == MS_charge_state)
            return s;
    }

    double mz = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();

    vector<MZIntensityPair> mzIntensityPairs;
    s->getMZIntensityPairs(mzIntensityPairs);
    sort(mzIntensityPairs.begin(), mzIntensityPairs.end(), &mzIntensityPairLessThan);

    double tic = accumulate(mzIntensityPairs.begin(), mzIntensityPairs.end(), 0.0, MZIntensityPairIntensitySum());

    vector<MZIntensityPair>::iterator mzItr = lower_bound(mzIntensityPairs.begin(), mzIntensityPairs.end(), MZIntensityPair(mz, 0), &mzIntensityPairLessThan);
    double fractionTIC = 0, inverseFractionCutoff = 1 - fraction_;
    for (vector<MZIntensityPair>::iterator itr = mzIntensityPairs.begin();
         itr != mzItr && fractionTIC < inverseFractionCutoff;
         ++itr)
         fractionTIC += itr->intensity / tic;
    fractionTIC = 1 - fractionTIC; // invert

    if (fractionTIC >= fraction_)
    {
        // remove possible charge states
        for(vector<CVParam>::iterator itr = cvParams.begin(); itr != cvParams.end(); ++itr)
            if (itr->cvid == MS_possible_charge_state)
            {
                itr = cvParams.erase(itr);
                if (itr == cvParams.end())
                    break;
            }

        // set charge state to 1
        cvParams.push_back(CVParam(MS_charge_state, 1));
    }
    else if (maxCharge_ - minCharge_ == 0)
    {
        // set charge state to the single multiply charged state
        cvParams.push_back(CVParam(MS_charge_state, maxCharge_));
    }
    else
    {
        // add possible charge states in range [minMultipleCharge, maxMultipleCharge]
        for (int z = minCharge_; z <= maxCharge_; ++z)
            if (!possibleChargeStates.contains(z))
                cvParams.push_back(CVParam(MS_possible_charge_state, z));
    }
    return s;
}


} // namespace analysis
} // namespace pwiz
