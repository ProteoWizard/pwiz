//
// Peptide.cpp 
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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
    int bondCount = !sequence_.empty() ? (int)sequence_.size()-1 : 0;
    formula_ -= bondCount * Formula("H2O1");
}


PWIZ_API_DECL Peptide::Peptide(const string& sequence) : impl_(new Impl(sequence)) {}
PWIZ_API_DECL Peptide::~Peptide() {} // auto-destruction of impl_
PWIZ_API_DECL string Peptide::sequence() const {return impl_->sequence();}
PWIZ_API_DECL Formula Peptide::formula() const {return impl_->formula();}


} // namespace pwiz
} // namespace proteome

