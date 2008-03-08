//
// IsotopeEnvelopeEstimator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _ISOTOPEENVELOPEESTIMATOR_HPP_
#define _ISOTOPEENVELOPEESTIMATOR_HPP_


#include "Chemistry.hpp"
#include "IsotopeCalculator.hpp"
 

namespace pwiz {
namespace proteome {


/// Class used for calculating a theoretical isotope envelope for a given mass,
/// based on an estimate of the elemental composition for that mass.  For peptides,
/// the elemental composition estimate is based on the average elemental composition
/// of amino acid residues.  Mass distributions are calculated on construction of the 
/// object and cached for quick access.
class IsotopeEnvelopeEstimator
{
    public:

    struct Config
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

    Chemistry::MassDistribution isotopeEnvelope(double mass) const;

    private:

    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    IsotopeEnvelopeEstimator(const IsotopeEnvelopeEstimator&);
    IsotopeEnvelopeEstimator& operator=(const IsotopeEnvelopeEstimator&);
};


} // namespace proteome
} // namespace pwiz


#endif // _ISOTOPEENVELOPEESTIMATOR_HPP_

