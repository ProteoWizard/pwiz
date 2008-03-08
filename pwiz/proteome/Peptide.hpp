//
// Peptide.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PEPTIDE_HPP_
#define _PEPTIDE_HPP_


#include "Chemistry.hpp"
#include <string>
#include <memory>


namespace pwiz {
namespace proteome {


/// class representing a peptide
class Peptide
{
    public:

    Peptide(const std::string& sequence);
    ~Peptide();

    std::string sequence() const;
    Chemistry::Formula formula() const;

    // vector<?> trypsinDigest() const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
    Peptide(const Peptide&);
    Peptide& operator=(const Peptide&);
};


} // namespace pwiz
} // namespace proteome


#endif // _PEPTIDE_HPP_

