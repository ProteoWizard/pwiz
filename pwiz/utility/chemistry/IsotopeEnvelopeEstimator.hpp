//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _ISOTOPEENVELOPEESTIMATOR_HPP_
#define _ISOTOPEENVELOPEESTIMATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "Chemistry.hpp"
#include "IsotopeCalculator.hpp"
 

namespace pwiz {
namespace chemistry {


/// Class used for calculating a theoretical isotope envelope for a given mass,
/// based on an estimate of the elemental composition for that mass.  For peptides,
/// the elemental composition estimate is based on the average elemental composition
/// of amino acid residues.  Mass distributions are calculated on construction of the 
/// object and cached for quick access.
class PWIZ_API_DECL IsotopeEnvelopeEstimator
{
    public:

    struct PWIZ_API_DECL Config
    {
        enum Type {Peptide}; 

        Type type;
        unsigned int cacheSize; 
        double cacheMaxMass;
        int normalization;
        const IsotopeCalculator* isotopeCalculator; // must be valid during construction only

        Config()
        :   type(Peptide), 
            cacheSize(10000),
            cacheMaxMass(100000), 
            normalization(IsotopeCalculator::NormalizeMass |      // monoisotopic == 0
                          IsotopeCalculator::NormalizeAbundance), // norm_2 == 1 
            isotopeCalculator(0)
        {}
    };

    IsotopeEnvelopeEstimator(const Config& config);
    ~IsotopeEnvelopeEstimator();

    MassDistribution isotopeEnvelope(double mass) const;

    private:

    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    IsotopeEnvelopeEstimator(const IsotopeEnvelopeEstimator&);
    IsotopeEnvelopeEstimator& operator=(const IsotopeEnvelopeEstimator&);
};


} // namespace chemistry
} // namespace pwiz


#endif // _ISOTOPEENVELOPEESTIMATOR_HPP_
