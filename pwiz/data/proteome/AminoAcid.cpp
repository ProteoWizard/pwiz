//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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

#include "AminoAcid.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include "pwiz/utility/misc/CharIndexedVector.hpp"
#include "pwiz/utility/misc/Singleton.hpp"

using namespace pwiz::util;

namespace pwiz {
namespace proteome {
namespace AminoAcid {


namespace Info {


class RecordData : public boost::singleton<RecordData>
{
    public:
    RecordData(boost::restricted)
    {
        initializeRecords();
    }

    inline const Record& record(Type type) {return records_[type];}
    inline const Record* record(char symbol) {return (size_t) symbol >= recordsSymbolIndex_.size() ? NULL : recordsSymbolIndex_[symbol];}

    private:
    void initializeRecords();
    vector<Record> records_;
    CharIndexedVector<const Record*> recordsSymbolIndex_;
};


namespace {
Record createRecord(const std::string& name,
                    const std::string& abbreviation,
                    char symbol,
                    const std::string& formula, 
                    double abundance)
{
    Record result;
    result.name = name;
    result.abbreviation = abbreviation;
    result.symbol = symbol;
    result.residueFormula = chemistry::Formula(formula);
    result.formula = result.residueFormula + chemistry::Formula("H2O1");
    result.abundance = abundance;
    return result;
}
} // namespace


void RecordData::initializeRecords()
{
    using namespace chemistry;
    records_.resize(Unknown+1);
    records_[Alanine] = createRecord("Alanine", "Ala", 'A', "C3 H5 N1 O1 S0", .078);
    records_[Cysteine] = createRecord("Cysteine", "Cys", 'C', "C3 H5 N1 O1 S1", .019);
    records_[AsparticAcid] = createRecord("Aspartic Acid", "Asp", 'D', "C4 H5 N1 O3 S0", .053);
    records_[GlutamicAcid] = createRecord("Glutamic Acid", "Glu", 'E', "C5 H7 N1 O3 S0", .063);
    records_[Phenylalanine] = createRecord("Phenylalanine", "Phe", 'F', "C9 H9 N1 O1 S0", .039);
    records_[Glycine] = createRecord("Glycine", "Gly", 'G', "C2 H3 N1 O1 S0", .072);
    records_[Histidine] = createRecord("Histidine", "His", 'H', "C6 H7 N3 O1 S0", .023);
    records_[Isoleucine] = createRecord("Isoleucine", "Ile", 'I', "C6 H11 N1 O1 S0", .053);
    records_[Lysine] = createRecord("Lysine", "Lys", 'K', "C6 H12 N2 O1 S0", .059);
    records_[Leucine] = createRecord("Leucine", "Leu", 'L', "C6 H11 N1 O1 S0", .091);
    records_[Methionine] = createRecord("Methionine", "Met", 'M', "C5 H9 N1 O1 S1", .023);
    records_[Asparagine] = createRecord("Asparagine", "Asn", 'N', "C4 H6 N2 O2 S0", .043);
    records_[Proline] = createRecord("Proline", "Pro", 'P', "C5 H7 N1 O1 S0", .052);
    records_[Glutamine] = createRecord("Glutamine", "Gln", 'Q', "C5 H8 N2 O2 S0", .042);
    records_[Arginine] = createRecord("Arginine", "Arg", 'R', "C6 H12 N4 O1 S0", .051);
    records_[Serine] = createRecord("Serine", "Ser", 'S', "C3 H5 N1 O2 S0", .068);
    records_[Threonine] = createRecord("Threonine", "Thr", 'T', "C4 H7 N1 O2 S0", .059);
    records_[Valine] = createRecord("Valine", "Val", 'V', "C5 H9 N1 O1 S0", .066);
    records_[Tryptophan] = createRecord("Tryptophan", "Trp", 'W', "C11 H10 N2 O1 S0", .014);
    records_[Tyrosine] = createRecord("Tyrosine", "Tyr", 'Y', "C9 H9 N1 O2 S0", .032);
    records_[Selenocysteine] = createRecord("Selenocysteine", "Sec", 'U', "C3 H5 N1 O1 Se1", .00);
    records_[AspX] = createRecord("AspX", "Asx", 'B', "C4 H6 N2 O2 S0", .00);
    records_[GlutX] = createRecord("GlutX", "Glx", 'Z', "C5 H8 N2 O2 S0", .00);
    records_[Unknown] = createRecord("Unknown", "Unk", 'X', "C5 H6 N1 O1 S0", .00);
    //    records_[Unknown] = createRecord("Unknown", "Unk", 'X', "C4.9384 H7.7583 N1.357701 O1.4773 S0.0417", .00);


    //Averagine is really C4.9384H7.7583N1.3577O1.4773S0.0417 

    // create the symbol index for the records
    for (vector<Record>::iterator it=records_.begin(); it!=records_.end(); ++it)
        recordsSymbolIndex_[it->symbol] = &*it;
}


PWIZ_API_DECL const Record& record(Type type) {return RecordData::instance->record(type);}


PWIZ_API_DECL const Record& record(char symbol) 
{
    const Record* record = RecordData::instance->record(symbol);
    if (!record)
        throw runtime_error(string("[AminoAcid::Info] Invalid amino acid symbol: ") + symbol);
    return *record;
}


} // namespace Info


} // namespace AminoAcid
} // namespace proteome
} // namespace pwiz


