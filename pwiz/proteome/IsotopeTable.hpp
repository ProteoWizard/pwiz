//
// IsotopeTable.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _ISOTOPETABLE_HPP_
#define _ISOTOPETABLE_HPP_


#include "Chemistry.hpp"


namespace pwiz {
namespace proteome {


/// Class representing a table of isotope distributions for collections of multiple
/// atoms of a single element; the table is computed on instantiation, based on the 
/// element's mass distribution, a maximum atom count, and abundance cutoff value.
class IsotopeTable
{
    public:

    IsotopeTable(const Chemistry::MassDistribution& md, int maxAtomCount, double cutoff); 
    ~IsotopeTable();

    Chemistry::MassDistribution distribution(int atomCount) const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    IsotopeTable(const IsotopeTable&);
    IsotopeTable& operator=(const IsotopeTable&);

    /// debugging
    friend std::ostream& operator<<(std::ostream& os, const IsotopeTable& isotopeTable);
};


} // namespace proteome
} // namespace pwiz


#endif // _ISOTOPETABLE_HPP_

