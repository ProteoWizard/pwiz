//
// AminoAcid.cpp 
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


#include "AminoAcid.hpp"
#include <iostream>
#include <map>
#include <cmath>
#include <sstream>
#include <stdexcept>


using namespace std;


namespace pwiz {
namespace proteome {
namespace AminoAcid {


class Info::Impl
{
    public:

    Impl();
    const Record& record(Type type) {return records_[type];}
    const Record* record(char symbol) {return recordsSymbolIndex_[symbol];}

    private:
    void initializeRecords();
    map<Type, Record> records_;
    map<char, const Record*> recordsSymbolIndex_;
};


Info::Impl::Impl()
{
    initializeRecords();
}


namespace {
Info::Record createRecord(const std::string& name,
                          const std::string& abbreviation,
                          char symbol,
                          const std::string& formula, 
                          double abundance)
{
    Info::Record result;
    result.name = name;
    result.abbreviation = abbreviation;
    result.symbol = symbol;
    result.formula = Chemistry::Formula(formula);
    result.abundance = abundance;
    return result;
}
} // namespace


void Info::Impl::initializeRecords()
{
    using namespace Chemistry;
    records_[Alanine] = createRecord("Alanine", "Ala", 'A', "C3 H7 N1 O2 S0", .078);
    records_[Cysteine] = createRecord("Cysteine", "Cys", 'C', "C3 H7 N1 O2 S1", .019);
    records_[AsparticAcid] = createRecord("Aspartic Acid", "Asp", 'D', "C4 H7 N1 O4 S0", .053);
    records_[GlutamicAcid] = createRecord("Glutamic Acid", "Glu", 'E', "C5 H9 N1 O4 S0", .063);
    records_[Phenylalanine] = createRecord("Phenylalanine", "Phe", 'F', "C9 H11 N1 O2 S0", .039);
    records_[Glycine] = createRecord("Glycine", "Gly", 'G', "C2 H5 N1 O2 S0", .072);
    records_[Histidine] = createRecord("Histidine", "His", 'H', "C6 H9 N3 O2 S0", .023);
    records_[Isoleucine] = createRecord("Isoleucine", "Ile", 'I', "C6 H13 N1 O2 S0", .053);
    records_[Lysine] = createRecord("Lysine", "Lys", 'K', "C6 H14 N2 O2 S0", .059);
    records_[Leucine] = createRecord("Leucine", "Leu", 'L', "C6 H13 N1 O2 S0", .091);
    records_[Methionine] = createRecord("Methionine", "Met", 'M', "C5 H11 N1 O2 S1", .023);
    records_[Asparagine] = createRecord("Asparagine", "Asn", 'N', "C4 H8 N2 O3 S0", .043);
    records_[Proline] = createRecord("Proline", "Pro", 'P', "C5 H9 N1 O2 S0", .052);
    records_[Glutamine] = createRecord("Glutamine", "Gln", 'Q', "C5 H10 N2 O3 S0", .042);
    records_[Arginine] = createRecord("Arginine", "Arg", 'R', "C6 H14 N4 O2 S0", .051);
    records_[Serine] = createRecord("Serine", "Ser", 'S', "C3 H7 N1 O3 S0", .068);
    records_[Threonine] = createRecord("Threonine", "Thr", 'T', "C4 H9 N1 O3 S0", .059);
    records_[Valine] = createRecord("Valine", "Val", 'V', "C5 H11 N1 O2 S0", .066);
    records_[Tryptophan] = createRecord("Tryptophan", "Trp", 'W', "C11 H12 N2 O2 S0", .014);
    records_[Tyrosine] = createRecord("Tyrosine", "Tyr", 'Y', "C9 H11 N1 O3 S0", .032);
    records_[AspX] = createRecord("AspX", "Asx", 'B', "C4 H8 N2 O3 S0", .00);
    records_[GlutX] = createRecord("GlutX", "Glx", 'Z', "C5 H10 N2 O3 S0", .00);
    records_[Unknown] = createRecord("Unknown", "Unk", 'X', "C5 H8 N1 O2 S0", .00);
    //    records_[Unknown] = createRecord("Unknown", "Unk", 'X', "C4.9384 H7.7583 N1.357701 O1.4773 S0.0417", .00);


    //Averagine is really C4.9384H7.7583N1.3577O1.4773S0.0417 

    // create the symbol index for the records
    for (map<Type,Record>::iterator it=records_.begin(); it!=records_.end(); ++it)
        recordsSymbolIndex_[it->second.symbol] = &it->second;
}


Info::Info() : impl_(new Impl) {}
Info::~Info() {} // automatic destruction of impl_
const Info::Record& Info::operator[](Type type) const {return impl_->record(type);}


const Info::Record& Info::operator[](char symbol) const 
{
    const Info::Record* record = impl_->record(symbol);
    if (!record)
        throw runtime_error((string("[AminoAcid::Info] Invalid amino acid symbol: ") + symbol).c_str());
    return *record;
}


} // namespace AminoAcid
} // namespace pwiz
} // namespace proteome


