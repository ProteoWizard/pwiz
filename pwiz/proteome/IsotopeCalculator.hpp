//
// IsotopeCalculator.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _ISOTOPECALCULATOR_HPP_
#define _ISOTOPECALCULATOR_HPP_


#include "Chemistry.hpp"
#include <memory>


namespace pwiz {
namespace proteome {


class IsotopeCalculator
{
    public:
    
    IsotopeCalculator(double abundanceCutoff, double massPrecision);
    ~IsotopeCalculator();

    enum NormalizationFlags
    {
        NormalizeMass = 0x01,       // shift masses -> monoisotopic_mass == 0
        NormalizeAbundance = 0x02   // scale abundances -> sum(abundance[i]^2) == 1 
    };
    
    Chemistry::MassDistribution distribution(const Chemistry::Formula& formula,
                                             int chargeState = 0,
                                             int normalization = 0) const;
    private:
    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    IsotopeCalculator(const IsotopeCalculator&);
    IsotopeCalculator& operator=(const IsotopeCalculator);
};


} // namespace proteome
} // namespace pwiz


#endif // _ISOTOPECALCULATOR_HPP_

