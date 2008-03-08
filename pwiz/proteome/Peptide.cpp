//
// Peptide.cpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "Peptide.hpp"
#include "AminoAcid.hpp"
#include <iostream>


namespace pwiz {
namespace proteome {


using namespace std;
using namespace Chemistry;


class Peptide::Impl
{
    public:

    Impl(const std::string& sequence);
    string sequence() const {return sequence_;}
    Formula formula() const {return formula_;}

    private:

    string sequence_;
    Formula formula_;
    
    void parseSequence();
};


Peptide::Impl::Impl(const string& sequence)
:   sequence_(sequence)
{
    parseSequence();
}


void Peptide::Impl::parseSequence()
{
    AminoAcid::Info info;

    // calculate sum of formulas of the amino acids 
    for (string::iterator it=sequence_.begin(); it!=sequence_.end(); ++it)
        formula_ += info[*it].formula;        

    // remove a water for each peptide bond
    int bondCount = !sequence_.empty() ? sequence_.size()-1 : 0;
    formula_ -= bondCount * Formula("H2O1");
}


Peptide::Peptide(const string& sequence) : impl_(new Impl(sequence)) {}
Peptide::~Peptide() {} // auto-destruction of impl_
string Peptide::sequence() const {return impl_->sequence();}
Formula Peptide::formula() const {return impl_->formula();}


} // namespace pwiz
} // namespace proteome

