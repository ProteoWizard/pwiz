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


#define PWIZ_SOURCE

#include "IsotopeEnvelopeEstimator.hpp"
#include "IsotopeCalculator.hpp"
#include "pwiz/utility/math/round.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


namespace pwiz {
namespace chemistry {


class IsotopeEnvelopeEstimator::Impl
{
    public:

    Impl(const Config& config);
    MassDistribution isotopeEnvelope(double mass) const;

    private:

    Config config_;
    vector<MassDistribution> cache_;

    unsigned int massToIndex(double mass) const;
    double indexToMass(unsigned int index) const;
    void initializeCache();
};


IsotopeEnvelopeEstimator::Impl::Impl(const Config& config)
:   config_(config)
{
    initializeCache();
}


unsigned int IsotopeEnvelopeEstimator::Impl::massToIndex(double mass) const
{
    unsigned int index = (unsigned int)round(config_.cacheSize * mass / config_.cacheMaxMass);

    if (index >= config_.cacheSize) 
        throw runtime_error("IsotopeEnvelopeEstimator::massToIndex()] Index out of bounds."); 

    return index;
}


double IsotopeEnvelopeEstimator::Impl::indexToMass(unsigned int index) const
{
    double mass = config_.cacheMaxMass * index / config_.cacheSize;
    return mass;
}


namespace {
Formula estimateFormula(double mass)
{
    // estimate formula assuming it's a peptide, using average elemental composition
    // of amino acid residues

    using namespace Element;

    const double averageResidueMass = 111.10524;
    const double averageC = 4.944;
    const double averageH = 7.763;
    const double averageN = 1.357;
    const double averageO = 1.476;
    const double averageS = 0.042;

    Formula water("H2O1");
    double residueCount = (mass - water.monoisotopicMass())/averageResidueMass;
    if (residueCount < 0) residueCount = 0;

    Formula result;

    result[C] = (int)round(residueCount * averageC);
    result[H] = (int)round(residueCount * averageH);
    result[N] = (int)round(residueCount * averageN);
    result[O] = (int)round(residueCount * averageO);
    result[S] = (int)round(residueCount * averageS);

    result += water;
    return result;
}
} // namespace


void IsotopeEnvelopeEstimator::Impl::initializeCache()
{
    if (!config_.isotopeCalculator)
        throw runtime_error("[IsotopeEnvelopeEstimator::initializeCache()] Initialization with null IsotopeCalculator*.");

    cache_.reserve(config_.cacheSize);

    for (unsigned int index=0; index<config_.cacheSize; ++index)
    {
        // estimate the peptide formula and cache the normalized distribution 

        Formula formula = estimateFormula(indexToMass(index));

        MassDistribution md = 
            config_.isotopeCalculator->distribution(formula, 
                                                    0, // charge state
                                                    config_.normalization);
        cache_.push_back(md);
    }
}


MassDistribution IsotopeEnvelopeEstimator::Impl::isotopeEnvelope(double mass) const
{
    return cache_[massToIndex(mass)];
}


//
// forwarding to impl
//

PWIZ_API_DECL IsotopeEnvelopeEstimator::IsotopeEnvelopeEstimator(const Config& config)
:   impl_(new Impl(config))
{}


PWIZ_API_DECL IsotopeEnvelopeEstimator::~IsotopeEnvelopeEstimator() {} // auto destruction of impl_


PWIZ_API_DECL MassDistribution IsotopeEnvelopeEstimator::isotopeEnvelope(double mass) const
{
    return impl_->isotopeEnvelope(mass);
}


} // namespace chemistry
} // namespace pwiz
