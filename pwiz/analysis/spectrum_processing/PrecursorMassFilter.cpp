//
// $Id$
//
//
// Original author: Chris Paulse <cpaulse <a.t> systemsbiology.org>
//
// Copyright 2009 Institute for Systems Biology, Seattle, WA
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
#include "PrecursorMassFilter.hpp"
#include "pwiz/utility/misc/String.hpp"

#include "boost/foreach.hpp"

#include <iostream>

namespace pwiz {
namespace analysis {

const double leftWindow = 60.;

using namespace std;
using namespace msdata;
using namespace pwiz::util;
using namespace pwiz::chemistry;


///TODO: this struct probably belongs in a more central location
struct MassLessThan
{
    MassLessThan(const MZTolerance& tolerance_) : tolerance(tolerance_)
    {
    }

    bool operator () (double lhs, double rhs)
    {
        return lessThanTolerance(lhs, rhs, tolerance);
    }

    const MZTolerance tolerance;
};

// Filter params class initialization

PWIZ_API_DECL
PrecursorMassFilter::Config::Config(
    MZTolerance tolerance, 
    bool removePrecursor_, 
    bool removeReducedChargePrecursors_,
    bool useBlanketFiltering_,
    int  numNeutralLossSpecies,
    const char* neutralLossSpecies_[])
    :    
    matchingTolerance(tolerance), 
    removePrecursor(removePrecursor_),
    removeReducedChargePrecursors(removeReducedChargePrecursors_),
    useBlanketFiltering(useBlanketFiltering_)
{
    if (useBlanketFiltering == false)
    {
        for (int i=0; i<numNeutralLossSpecies; i++)
        {
            neutralLossSpecies.push_back(chemistry::Formula(neutralLossSpecies_[i]));
        }
    }
}

PWIZ_API_DECL
PrecursorMassFilter::FilterSpectrum::FilterSpectrum(const PrecursorMassFilter::Config& params_, 
                    const pwiz::msdata::SpectrumPtr spectrum_) 
                    : params(params_), 
                      spectrum(spectrum_), 
                      massList_(spectrum->getMZArray()->data), 
                      intensities_(spectrum->getIntensityArray()->data)
{
    if (massList_.size() < 1)
    {
        //TODO: log encounter with empty spectrum?
        return;
    }
    double upperMassRange = 10000.;
    if (spectrum->hasCVParam(MS_highest_observed_m_z))
    {
        upperMassRange = spectrum->cvParam(MS_highest_observed_m_z).valueAs<double>();
    }
    
    int precursorCharge = 0;
    double precursorMZ = 0.;
    double maxPrecursorMass = 0.;

    vector<PrecursorReferenceMass> filterMassList;
    vector<int> chargeStates;

    BOOST_FOREACH(Precursor& precursor, spectrum->precursors)
    {
        BOOST_FOREACH(SelectedIon& selectedIon, precursor.selectedIons)
        {
            if (selectedIon.hasCVParam(MS_m_z))
            {
                precursorMZ = selectedIon.cvParam(MS_m_z).valueAs<double>();
            }
            else if (selectedIon.hasCVParam(MS_selected_ion_m_z))
            {
                precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            }
            else
            {
                //TODO: log warning, unable to read precursor mz
                //cout << "unable to read precursor mz: " << spectrum->index << endl;
            }

            if (selectedIon.hasCVParam(MS_charge_state))
            {
                precursorCharge = selectedIon.cvParam(MS_charge_state).valueAs<int>();
                chargeStates.push_back(selectedIon.cvParam(MS_charge_state).valueAs<int>());
            }
            else
            {
                //TODO: log warning, unable to read precursor charge state
                //cout << "unable to read precursor charge: " << spectrum->index << endl;
            }

            if (precursorCharge > 0 && precursorMZ > 0)
            {
                maxPrecursorMass = precursorMZ * precursorCharge > maxPrecursorMass ? precursorMZ*precursorCharge : maxPrecursorMass;
            }

            if (params.removePrecursor)
            {
                filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::ePrecursor, precursorMZ, precursorCharge));
            }

            if (params.removeReducedChargePrecursors)
            {
                BOOST_FOREACH(const int charge, chargeStates)
                {
                    double mass = precursorMZ * charge;
                    for (int reducedCharge = charge - 1; reducedCharge > 0; --reducedCharge)
                    {
                        double mzVal = (mass + (charge - reducedCharge) * chemistry::Electron) / (double) reducedCharge;
                        if (mzVal < upperMassRange)
                        {
                            filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::eChargeReducedPrecursor, 
                                                                            mzVal, 
                                                                            reducedCharge));
                        }
                        double mzValMS2 = mzVal;
                        BOOST_FOREACH(chemistry::Formula neutralLoss, params.neutralLossSpecies)
                        {
                            double mzValNL = mzValMS2 - (neutralLoss.monoisotopicMass() / double(reducedCharge));
                            if (mzVal < upperMassRange)
                            {
                                filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::eNeutralLoss, mzValNL, reducedCharge));
                            }
                        }
                    }
                }
            }
        }
    }

    if (filterMassList.size() > 0 && chargeStates.size() == 1)
    {
        //TODO: we should construct this list so that it doesn't require sorting.
        sort(filterMassList.begin(), filterMassList.end(), ReferenceMassByMass());

        vector<double>::iterator lowerBound;
        vector<double>::iterator upperBound;
        int iLowerBound = 0;
        int iUpperBound = 0;

        BOOST_FOREACH(PrecursorReferenceMass& mass, filterMassList)
        {
            MZTolerance matchingToleranceLeft = 0;
            MZTolerance matchingToleranceRight = 0;
            matchingToleranceLeft = matchingToleranceRight = params.matchingTolerance;

            if (params.useBlanketFiltering && mass.massType == PrecursorReferenceMass::eChargeReducedPrecursor)
            {
                 matchingToleranceLeft = leftWindow / (double) mass.charge;
            }

            double massToUse = mass.mass;

            // use STL's binary search to locate fragment ions within the mass tolerance window of the reference mass
            // O(m*log(n)) for all matches (m-number of reference masses, n-number of observed masses in spectrum).  
            lowerBound = lower_bound(massList_.begin(), massList_.end(), massToUse, MassLessThan(matchingToleranceLeft));
            upperBound = upper_bound(massList_.begin(), massList_.end(), massToUse, MassLessThan(matchingToleranceRight));

            iLowerBound = lowerBound - massList_.begin();
            iUpperBound = upperBound - massList_.begin();

            massList_.erase(lowerBound, upperBound);
            intensities_.erase(intensities_.begin() + iLowerBound, intensities_.begin() + iUpperBound);

        } // for each reference mass

        // we don't expect any fragments above precursor mass - 60, so we remove all masses above this value
        // in case there are multiple precursors, we filter everything above the highest precursor mass - 60.
        // we might want to record observed neutral losses for diagnostic purposes in high resolution data
        lowerBound = lower_bound(massList_.begin(), massList_.end(), maxPrecursorMass - 60., MassLessThan(params.matchingTolerance));
        iLowerBound = lowerBound - massList_.begin();
        
        massList_.erase(lowerBound, massList_.end());
        intensities_.erase(intensities_.begin() + iLowerBound, intensities_.end());

        spectrum->defaultArrayLength = massList_.size();
    }
}

PWIZ_API_DECL void PrecursorMassFilter::describe(ProcessingMethod& method) const
{
    method.set(MS_SpectraFilter);
    method.userParams.push_back(UserParam("remove precursor", lexical_cast<string>(params.removePrecursor)));
    method.userParams.push_back(UserParam("filter charge reduced precursors", lexical_cast<string>(params.removeReducedChargePrecursors)));
    method.userParams.push_back(UserParam("remove neutral loss masses", lexical_cast<string>(params.neutralLossSpecies.size() > 0)));
    method.userParams.push_back(UserParam("blanket removal of neutral loss masses", lexical_cast<string>(params.useBlanketFiltering)));
    method.userParams.push_back(UserParam("matching tolerance", lexical_cast<string>(params.matchingTolerance)));
}

PWIZ_API_DECL void PrecursorMassFilter::operator () (const SpectrumPtr spectrum) const
{
    if (spectrum->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        spectrum->cvParam(MS_MSn_spectrum).empty() == false &&
        spectrum->precursors[0].empty() == false &&
        spectrum->precursors[0].selectedIons.empty() == false &&
        spectrum->precursors[0].selectedIons[0].empty() == false &&
        spectrum->precursors[0].activation.hasCVParam(MS_electron_transfer_dissociation))
    {
        FilterSpectrum(params, spectrum);
    }
}

} // namespace analysis 
} // namespace pwiz
