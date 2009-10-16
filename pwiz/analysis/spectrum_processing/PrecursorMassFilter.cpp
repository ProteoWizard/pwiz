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


#include "pwiz/data/msdata/MSData.hpp"
#include "PrecursorMassFilter.hpp"
#include "pwiz/utility/misc/String.hpp"

#include "boost/foreach.hpp"

#include <iostream>

namespace pwiz {
namespace analysis {


using namespace std;
using namespace msdata;
using namespace pwiz::util;
using namespace pwiz::proteome;

///TODO: this struct probably belongs in a more visible location
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

PrecursorMassFilter::Config::Config(
    MZTolerance tolerance, 
    bool removePrecursor_, 
    bool removeReducedChargePrecursors_,
    bool removePossibleChargePrecursors_,
    bool removeMostIntensePeakInWindow_,
    bool useBlanketFiltering_,
    int  numNeutralLossSpecies,
    char* neutralLossSpecies_[])
    :    
    matchingTolerance(tolerance), 
    removePrecursor(removePrecursor_),
    removeReducedChargePrecursors(removeReducedChargePrecursors_),
    removePossibleChargePrecursors(removePossibleChargePrecursors_),
    removeMostIntensePeakInWindow(removeMostIntensePeakInWindow_),
    useBlanketFiltering(useBlanketFiltering_)
{
    for (int i=0; i<numNeutralLossSpecies; i++)
    {
        neutralLossSpecies.push_back(Chemistry::Formula(neutralLossSpecies_[i]));
    }
}

void PrecursorMassFilter::describe(ProcessingMethod& method) const
{
    method.set(MS_ECD_ETD_Precursor_Mass_Filter);
    method.userParams.push_back(UserParam("remove precursor", lexical_cast<string>(params.removePrecursor)));
    method.userParams.push_back(UserParam("filter charge reduced precursors", lexical_cast<string>(params.removeReducedChargePrecursors)));
    method.userParams.push_back(UserParam("remove neutral loss masses", lexical_cast<string>(params.neutralLossSpecies.size() > 0)));
    method.userParams.push_back(UserParam("selective removal of precursors (most intense peak in tolerance window)", lexical_cast<string>(params.removeMostIntensePeakInWindow)));
    method.userParams.push_back(UserParam("blanket removal of neutral loss masses", lexical_cast<string>(params.useBlanketFiltering)));
    method.userParams.push_back(UserParam("matching tolerance", lexical_cast<string>(params.matchingTolerance)));
}

void PrecursorMassFilter::operator () (const SpectrumPtr spectrum) const
{
    if (spectrum->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        spectrum->cvParam(MS_MSn_spectrum).empty() == false &&
        spectrum->precursors[0].empty() == false &&
        spectrum->precursors[0].selectedIons.empty() == false &&
        spectrum->precursors[0].selectedIons[0].empty() == false)
    {
        vector<double>& inputMasses = spectrum->getMZArray()->data;
        vector<double>& intensity = spectrum->getIntensityArray()->data;

        if (inputMasses.size() < 1)
        {
            //TODO: log encounter with empty spectrum?
            return;
        }

        vector<PrecursorReferenceMass> filterMassList;
        BOOST_FOREACH(Precursor& precursor, spectrum->precursors)
        {
            BOOST_FOREACH(SelectedIon& selectedIon, precursor.selectedIons)
            {
                double mz = 0.;
                vector<int> chargeStates;
                if (selectedIon.hasCVParam(MS_m_z))
                {
                    mz = selectedIon.cvParam(MS_m_z).valueAs<double>();
                }
                else if (selectedIon.hasCVParam(MS_selected_ion_m_z))
                {
                    mz = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
                }
                else
                {
                    //TODO: log warning, unable to read precursor mz
                    //cout << "unable to read precursor mz: " << spectrum->index << endl;
                }

                if (selectedIon.hasCVParam(MS_charge_state))
                {
                    chargeStates.push_back(selectedIon.cvParam(MS_charge_state).valueAs<int>());
                }
                else
                {
                    //TODO: log warning, unable to read precursor charge state
                    //cout << "unable to read precursor charge: " << spectrum->index << endl;
                }

                if (params.removePossibleChargePrecursors)
                {
                    BOOST_FOREACH(const CVParam& param, selectedIon.cvParams)
                    {
                        if (param.cvid == MS_possible_charge_state)
                            chargeStates.push_back(param.valueAs<int>());
                    }
                }

                if (params.removePrecursor)
                {
                    filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::ePrecursor, mz, 0, params.removeMostIntensePeakInWindow));
                }

                if (params.removeReducedChargePrecursors)
                {
                    BOOST_FOREACH(const int charge, chargeStates)
                    {
                        double mass = mz * charge;
                        for (int i = charge - 1; i > 0; --i)
                        {
                            double mzVal = (mass + (charge - i) * Chemistry::Electron) / (double) i;
                            filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::eChargeReducedPrecursor, 
                                                                            mzVal, 
                                                                            i, 
                                                                            params.removeMostIntensePeakInWindow));
                            // ignore removeMostIntensePeakInWindow flag for neutral losses pending
                            // a detailed examination of whether the flag should be used.
                            BOOST_FOREACH(Chemistry::Formula neutralLoss, params.neutralLossSpecies)
                            {
                                mzVal = (mass + (charge - i) * Chemistry::Electron - neutralLoss.monoisotopicMass()) / (double) i;
                                filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::eNeutralLoss, mzVal));
                            }
                        }
                    }
                }
            }
        }

        if (filterMassList.size() > 0)
        {
            //TODO: we should construct this list so that it doesn't require sorting.
            sort(filterMassList.begin(), filterMassList.end(), ReferenceMassByMass());

            BOOST_FOREACH(PrecursorReferenceMass mass, filterMassList)
            {
                MZTolerance matchingToleranceLeft = 0;
                MZTolerance matchingToleranceRight = 0;
                matchingToleranceLeft = matchingToleranceRight = params.matchingTolerance;

                if (params.useBlanketFiltering && mass.massType == PrecursorReferenceMass::eChargeReducedPrecursor)
                {
                    matchingToleranceLeft = MZTolerance(60. / (double) mass.charge);
                }

                // use STL's binary search algorithm to locate fragment ions within the mass tolerance window of the reference mass
                // this is an O(m*log(n)) operation for all matches.  
                vector<double>::iterator lowerBound = lower_bound(inputMasses.begin(), inputMasses.end(), mass.mass, MassLessThan(matchingToleranceLeft));
                vector<double>::iterator upperBound = upper_bound(inputMasses.begin(), inputMasses.end(), mass.mass, MassLessThan(matchingToleranceRight));

                int iLowerBound = lowerBound - inputMasses.begin();
                int iUpperBound = upperBound - inputMasses.begin();

                if (lowerBound != inputMasses.end())
                {
                    if (mass.matchMostIntense)
                    {
                        if (iUpperBound - iLowerBound > 0)
                        {
                            double maxIntensity = 0;
                            int itemToDelete = iLowerBound; // arbitrarily default to first item in current match list
                            for (vector<double>::iterator it = intensity.begin() + iLowerBound; it < intensity.begin() + iUpperBound; it++)
                            {
                                if (*it > maxIntensity)
                                {
                                    maxIntensity = *it;
                                    itemToDelete = it - intensity.begin();
                                }
                            }
                            inputMasses.erase(inputMasses.begin() + itemToDelete);
                            intensity.erase(intensity.begin() + itemToDelete);
                        }
                    }
                    else // remove all masses within the matching tolerance window
                    {
                        inputMasses.erase(lowerBound, upperBound);
                        intensity.erase(intensity.begin() + iLowerBound, intensity.begin() + iUpperBound);
                    }
                }

                spectrum->defaultArrayLength = inputMasses.size();
            }
        }
    }
}

} // namespace analysis 
} // namespace pwiz
