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
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <iostream>

namespace pwiz {
namespace analysis {

const double leftWindow = 60.;

using namespace msdata;
using namespace pwiz::util;
using namespace pwiz::chemistry;


namespace {

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

struct PrecursorReferenceMass
{
    enum Type {Precursor, ChargeReducedPrecursor, NeutralLoss};

    PrecursorReferenceMass(Type   type_ = PrecursorReferenceMass::Precursor, 
                           double mass_ = 0.0,
                           int    charge_ = 0)
        : massType(type_), mass(mass_), charge(charge_) {}

    Type massType;
    double mass;
    int charge;

    bool operator< (const PrecursorReferenceMass& rhs) const
    {
        return mass < rhs.mass;
    }
};

} // namespace


struct PrecursorMassFilter::Impl
{
    Impl(const Config& params_) : params(params_) {};

    void filter(const SpectrumPtr& spectrum) const;

    const Config& params;
};

void PrecursorMassFilter::Impl::filter(const SpectrumPtr& spectrum) const
{
    BinaryData<double>& massList_ = spectrum->getMZArray()->data;
    BinaryData<double>& intensities_ = spectrum->getIntensityArray()->data;

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

    for (size_t i=0; i < spectrum->precursors.size(); ++i)
    {
        const Precursor& precursor = spectrum->precursors[i];
        for (size_t j=0; j < precursor.selectedIons.size(); ++j)
        {
            const SelectedIon& selectedIon = precursor.selectedIons[j];
            precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            if (precursorMZ == 0)
            {
                // support legacy data
                precursorMZ = selectedIon.cvParam(MS_m_z).valueAs<double>();

                if (precursorMZ == 0)
                    //TODO: log warning, unable to read precursor mz
                    continue;
            }

            if (params.removePrecursor)
                filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::Precursor, precursorMZ, 0));

            precursorCharge = selectedIon.cvParam(MS_charge_state).valueAs<int>();
            if (precursorCharge != 0)
            {
                int charge = precursorCharge;
                chargeStates.push_back(charge);
                maxPrecursorMass = max(Ion::neutralMass(precursorMZ, charge), maxPrecursorMass);
                
                if (params.removeReducedChargePrecursors)
                {
                    double neutralMass = Ion::neutralMass(precursorMZ, charge);
                    for (int reducedCharge = charge - 1; reducedCharge > 0; --reducedCharge)
                    {
                        int electronDelta = charge - reducedCharge;
                        double reducedChargeMZ = Ion::mz(neutralMass, charge, electronDelta);
                        if (reducedChargeMZ < upperMassRange)
                            filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::ChargeReducedPrecursor, reducedChargeMZ, reducedCharge));

                        for(const chemistry::Formula& neutralLoss : params.neutralLossSpecies)
                        {
                            double neutralLossMZ = Ion::mz(neutralMass - neutralLoss.monoisotopicMass(), charge, electronDelta);
                            if (neutralLossMZ < upperMassRange)
                                filterMassList.push_back(PrecursorReferenceMass(PrecursorReferenceMass::NeutralLoss, neutralLossMZ, reducedCharge));
                        }
                    }
                }
            }
        }
    }

    if (filterMassList.size() > 0 && chargeStates.size() < 2)
    {
        //TODO: we should construct this list so that it doesn't require sorting.
        sort(filterMassList.begin(), filterMassList.end());

        BinaryData<double>::iterator lowerBound;
        BinaryData<double>::iterator upperBound;
        int iLowerBound = 0;
        int iUpperBound = 0;

        for(const PrecursorReferenceMass& mass : filterMassList)
        {
            MZTolerance matchingToleranceLeft = 0;
            MZTolerance matchingToleranceRight = 0;
            matchingToleranceLeft = matchingToleranceRight = params.matchingTolerance;

            if (params.useBlanketFiltering && mass.massType == PrecursorReferenceMass::ChargeReducedPrecursor)
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

        spectrum->defaultArrayLength = massList_.size();

        if (maxPrecursorMass == 0)
            return;

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




PWIZ_API_DECL PrecursorMassFilter::PrecursorMassFilter(const Config& config)
: params(config), impl_(new Impl(params))
{}


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


PWIZ_API_DECL void PrecursorMassFilter::describe(ProcessingMethod& method) const
{
    method.set(MS_data_filtering);
    method.userParams.push_back(UserParam("remove precursor", lexical_cast<string>(params.removePrecursor)));
    method.userParams.push_back(UserParam("filter charge reduced precursors", lexical_cast<string>(params.removeReducedChargePrecursors)));
    method.userParams.push_back(UserParam("remove neutral loss masses", lexical_cast<string>(params.neutralLossSpecies.size() > 0)));
    method.userParams.push_back(UserParam("blanket removal of neutral loss masses", lexical_cast<string>(params.useBlanketFiltering)));
    method.userParams.push_back(UserParam("matching tolerance", lexical_cast<string>(params.matchingTolerance)));
}


PWIZ_API_DECL void PrecursorMassFilter::operator () (const SpectrumPtr& spectrum) const
{
    if (spectrum->defaultArrayLength > 0 &&
        spectrum->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        spectrum->hasCVParam(MS_MSn_spectrum) &&
        !spectrum->precursors.empty() &&
        !spectrum->precursors[0].selectedIons.empty() &&
        !spectrum->precursors[0].selectedIons[0].empty() &&
        spectrum->precursors[0].activation.hasCVParam(MS_ETD))
    {
        impl_->filter(spectrum);
    }
}


} // namespace analysis 
} // namespace pwiz
