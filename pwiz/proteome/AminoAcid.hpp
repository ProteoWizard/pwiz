//
// AminoAcid.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _AMINOACID_HPP_
#define _AMINOACID_HPP_


#include "Chemistry.hpp"
#include <string>
#include <memory>


namespace pwiz {
namespace proteome {


/// scope for types related to amino acids
namespace AminoAcid {


/// enumeration of the amino acids 
enum Type 
{
    Alanine,
    Cysteine,
    AsparticAcid,
    GlutamicAcid,
    Phenylalanine,
    Glycine,
    Histidine,
    Isoleucine,
    Lysine,
    Leucine,
    Methionine,
    Asparagine,
    Proline,
    Glutamine,
    Arginine,
    Serine,
    Threonine,
    Valine,
    Tryptophan,
    Tyrosine,
    AspX,
    GlutX,
    Unknown
};


/// class for accessing information about the amino acids
class Info 
{
    public:

    Info();
    ~Info();

    /// struct for holding data for a single amino acid
    struct Record 
    {
        std::string name; 
        std::string abbreviation; 
        char symbol; 
        Chemistry::Formula formula;
        double abundance;
    };

    /// returns the amino acid's Record by type
    const Record& operator[](Type type) const;

    /// returns the amino acid's Record by symbol (may throw) 
    const Record& operator[](char symbol) const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
    Info(const Info&);
    const Info& operator=(const Info&);
};


} // namespace AminoAcid


} // namespace pwiz
} // namespace proteome


#endif // _AMINOACID_HPP_  

