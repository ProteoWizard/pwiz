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


#ifndef _PEPTIDEDATABASE_HPP_
#define _PEPTIDEDATABASE_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <memory>


namespace pwiz {
namespace proteome {


#pragma pack(1)
struct PWIZ_API_DECL PeptideDatabaseFormula
{
    int C;
    int H;
    int N;
    int O;
    int S;

    PeptideDatabaseFormula(int c=0, int h=0, int n=0, int o=0, int s=0)
    :   C(c), H(h), N(n), O(o), S(s)
    {}
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeptideDatabaseFormula& formula);


#pragma pack(1)
struct PWIZ_API_DECL PeptideDatabaseRecord
{
    int id_ipi;
    double abundance;
    double mass;
    PeptideDatabaseFormula formula;
    int sequenceKey;

    PeptideDatabaseRecord()
    :   id_ipi(0), abundance(1), mass(0), sequenceKey(0)
    {}
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeptideDatabaseRecord& record);
PWIZ_API_DECL bool operator==(const PeptideDatabaseFormula& r, const PeptideDatabaseFormula& s); 


class PWIZ_API_DECL PeptideDatabase
{
    public:

    static std::auto_ptr<PeptideDatabase> create();
    static std::auto_ptr<const PeptideDatabase> create(const std::string& filename);
    virtual ~PeptideDatabase(){}

    virtual int size() const = 0;
    virtual const PeptideDatabaseRecord* records() const = 0;
    virtual std::string sequence(const PeptideDatabaseRecord& record) const = 0;

    virtual void append(const PeptideDatabaseRecord& record, const std::string& sequence = "") = 0;
    //virtual void append(const std::string& sequence) = 0;
    virtual void write(const std::string& filename) const = 0;

    // sortByMass() (rewriteStringTable)

    // iterator interface
    typedef const PeptideDatabaseRecord* iterator;
    iterator begin() const {return records();}
    iterator end() const {return records() + size();}

    // range access (binary search algorithms assume database is sorted by mass)
    iterator mass_lower_bound(double mass) const;
    iterator mass_upper_bound(double mass) const;
};


} // namespace pwiz
} // namespace proteome


#endif // _PEPTIDEDATABASE_HPP_ 

