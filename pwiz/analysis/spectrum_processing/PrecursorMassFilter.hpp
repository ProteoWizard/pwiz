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


#ifndef _PRECURSORMASSFILTER_HPP_ 
#define _PRECURSORMASSFILTER_HPP_ 


#include "pwiz/analysis/common/DataFilter.hpp"
#include "pwiz/analysis/peakdetect/MZTolerance.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"


namespace pwiz {
namespace analysis {

// See Table 1 of 
// "Post Acquisition ETD Spectral Processing of Increased Peptide Identifications"
// by D. M. Good et al
///TODO: locate a more complete list (Zubarev Lab)
static const char* defaultNeutralLossFormulae[] = {
    "N1H3",
    "H2O1",
    "C1O1",
    "C1H4O1",
    "N2H6",       // 2 * NH3
    "H5N1O1",     // Typo in original source NH3 + H2O
    "H4O2",       // 2 * H2O
    "C1H4N2",
    "C1H3N1O1",
    "C1H2O2",
    "C2H6O1",
    "C2H5N1O1",
    "C1H5N3",
};


/**
 * Predicted mass value for identification and removal in ETD/ECD MS2 spectra
 */
struct PrecursorReferenceMass
{
    enum eMassType {ePrecursor, eChargeReducedPrecursor, eNeutralLoss};

    PrecursorReferenceMass( eMassType   massType_ = PrecursorReferenceMass::ePrecursor, 
                            double      mass_ = 0.0,
                            int         charge_ = 0,
                            bool        matchMostIntense_ = false)
        : massType(massType_), mass(mass_), charge(charge_), matchMostIntense(matchMostIntense_) {}

    eMassType massType;
    double mass;
    int charge;
    /**
     * flag used to indicate that the most intense mass within a tolerance window is to be identified as a unique match
     *
     * specifed on a per mass basis, as it may be something only applicable to certain mass values/types
     */
    bool matchMostIntense;
};

struct ReferenceMassByMass
{
    bool operator() (const PrecursorReferenceMass& a, const PrecursorReferenceMass& b)
    {
        return a.mass < b.mass;
    }
};

struct PrecursorMassFilter : public SpectrumDataFilter
{
     /// PrecursorMassFilter's parameters
    struct Config
    {
        Config(
            MZTolerance    tolerance = MZTolerance(0.1), 
            bool           removePrecursor_ = true, 
            bool           removeReducedChargePrecursors_ = true,
            bool           removePossibleChargePrecursors_ = true,
            bool           selectiveRemovalofPrecursors_ = false,
            bool           useBlanketFiltering_ = false,
            int            numNeutralLossSpecies = 13,
            const char*    neutralLossSpecies_[] = defaultNeutralLossFormulae
        );

        MZTolerance matchingTolerance;
        /// remove the precursor m/z from the MS2 spectrum
        bool removePrecursor;
        /** electron transfer in ETD or ECD creates intact precursors with reduced charge states
          * this flag specifies their removal. */
        bool removeReducedChargePrecursors;
        /** intact precursors can undergo loss of neutral molecules after the dissociation event
          * this flag specifies the removal of these "neutral loss" ions (precursor mass - neutral loss mass)/charge */
        std::vector<pwiz::chemistry::Formula> neutralLossSpecies;
        /** in cases where the precursor charge is indeterminate, but defined as being one of a series of values,
          * we use the following flag to remove precursors for all hypothetical values of the parent charge */
        bool removePossibleChargePrecursors;
        /** since precursors and charge reduced precursors are prominent features in an etd ms2 spectrum, an option
          * permits removal of only the most intense peak within a given matching tolerance.  The flag is ignored
          * currently for neutral loss masses */
        bool removeMostIntensePeakInWindow;
        /// flag indicates neutral loss removal by applying a charge scaled 60 Da exclusion window below the charge reduced precursor
        bool useBlanketFiltering;
    };

    PrecursorMassFilter(const PrecursorMassFilter::Config& params_) : params(params_) {}
    virtual void operator () (const pwiz::msdata::SpectrumPtr) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const PrecursorMassFilter::Config params;
};

} // namespace analysis 
} // namespace pwiz


#endif // _PRECURSORMASSFILTER_HPP_ 
