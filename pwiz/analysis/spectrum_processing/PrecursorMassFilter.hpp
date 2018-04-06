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
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"


namespace pwiz {
namespace analysis {

// See Table 1 of 
// "Post Acquisition ETD Spectral Processing of Increased Peptide Identifications" -- PUB 1
// by D. M. Good et al
// 
// Experimental Section and Table 1 of 
// "Analytical Utility of Small Neutral Losses from Reduced Species in Electron 
//  Capture Dissociation Studied Using SwedECD Database" by M. Falth et al  -- PUB 2
//
// Cooper, H. J., Hakansson, K., Marshall, A.G., Hudgins, R. R., Haselmann, K. F.,
// Kjeldsen, F., Budnik, B. A. Polfer, N. C., Zubarev, R. A., Letter: the diagnostic value of
// amino acid side-chain losses in electron capture dissociation of polypeptides. Comment
// on: "Can the (M(.)-X) region in electron capture dissociation provide reliable information on
// amino acid composition of polypeptides?", Eur. J. Mass Spectrom. 8, 461-469 (2002). -- PUB 3
    //17.027 Da NH3
    //18.011 Da H2O
    //27.995 Da CO
    //32.026 Da CH3OH
    //34.053 Da N2H6 (2xNH3)
    //35.037 Da H4NO
    //36.021 Da H4O2 (2xH2O)
    //74.019 Da C3H6S
    //82.053 Da C4H6N2
    //86.072 Da C3H8N3
    //99.068 Da C4H9N3
    //101.095 Da C4H11N3
    //108.058 Da C7H8O
    //(131.074 Da C9H9N)
    //44.037 Da CH4N2
    //45.021 Da CH3NO
    //46.006 Da CH2O2
    //46.042 Da C2H6O
    //59.037 Da C2H5NO
    //59.048 Da CH5N3
    //73.089 Da C4H11N

#define NUM_NEUTRAL_LOSS_SPECIES 25

// All entries are PUB 1 Table 1 except where noted
static const char* defaultNeutralLossFormulae[] = {
    "H1",          // ubiquitous neutral loss (PUB 2)
    "N1H2",        // ubiquitous neutral loss (PUB 2)
    "N1H3",
    "H2O1",
    "C1O1",
    "C1H4O1",
    "N2H6",        // 2 * NH3
    "H5N1O1",      // Typo in PUB 1 Table 1 NH3 + H2O
    "H4O2",        // 2 * H2O
    "C1H3N2",      // PUB 2 Table 1
    "C1H4N2",
    "C1H3N1O1",
    "C1H2O2",
    "C2H6O1",
    "C2H5N1O1",
    "C1H5N3",
    "C2H4O2",      // PUB 2 Table 1
    "C4H11N1",     // PUB 3
    "C3H6S1",      // PUB 3
    "C4H6N2",      // PUB 3
    "C3H8N3",      // PUB 3
    "C4H9N3",      // PUB 3
    "C4H11N3",     // PUB 3
    "C7H8O1",      // PUB 3
    "C9H9N1"       // PUB 3
};


using chemistry::MZTolerance;


struct PWIZ_API_DECL PrecursorMassFilter : public SpectrumDataFilter
{

    /// PrecursorMassFilter's parameters
    struct PWIZ_API_DECL Config
    {
        Config(
            MZTolerance    tolerance = MZTolerance(0.1), 
            bool           removePrecursor_ = true, 
            bool           removeReducedChargePrecursors_ = true,
            bool           useBlanketFiltering_ = false,
            int            numNeutralLossSpecies = NUM_NEUTRAL_LOSS_SPECIES,
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
        bool removeNeutralLossSpecies;

        /// flag indicates neutral loss removal by applying a charge scaled 60 Da exclusion window below the charge reduced precursor
        bool useBlanketFiltering;
    };

    PrecursorMassFilter(const Config&);
    virtual void operator () (const pwiz::msdata::SpectrumPtr&) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const PrecursorMassFilter::Config params;

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
};

} // namespace analysis 
} // namespace pwiz


#endif // _PRECURSORMASSFILTER_HPP_ 
