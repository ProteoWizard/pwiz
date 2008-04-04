//
// Chemistry.cpp 
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


#include "Chemistry.hpp"
#include "ChemistryData.hpp"
#include <iostream>
#include <map>
#include <stdexcept>
#include <iterator>
#include <sstream>
#include <numeric>
#include <algorithm>


using namespace std;


namespace pwiz {
namespace proteome {
namespace Chemistry { 


bool MassAbundance::operator==(const MassAbundance& that) const
{
    return this->mass==that.mass && this->abundance==that.abundance;
}


bool MassAbundance::operator!=(const MassAbundance& that) const
{
    return !operator==(that); 
}


ostream& operator<<(ostream& os, const MassAbundance& ma)
{
    os << "<" << ma.mass << ", " << ma.abundance << ">";
    return os;
}


ostream& operator<<(ostream& os, const MassDistribution& md)
{
    copy(md.begin(), md.end(), ostream_iterator<MassAbundance>(os, "\n"));
    return os;
}


namespace Element {


std::ostream& operator<<(std::ostream& os, Type type)
{
    Info info;
    os << info[type].symbol;
    return os;
}


class Info::Impl
{
    public:
    Impl();
    const Info::Record& record(Type type) const;

    private:
    // keep only one copy of the data
    static map<Type, Record> data_;
    static bool dataInitialized_;
    void initializeData();
};


Info::Impl::Impl() 
{
    if (!dataInitialized_) 
        initializeData();
}


const Info::Record& Info::Impl::record(Type type) const
{
    if (!data_.count(type))
        throw runtime_error("[Chemistry::Element::Info::Impl::record()]  Record not found.");

    return data_[type];
}


map<Type, Info::Record> Info::Impl::data_;
bool Info::Impl::dataInitialized_ = false;


void Info::Impl::initializeData()
{
    // iterate through the ChemistryData array and put it in our own data structure

    ChemistryData::Element* it = ChemistryData::elements();
    ChemistryData::Element* end = ChemistryData::elements() + ChemistryData::elementsSize();

    for (; it!=end; ++it)
    {
        Info::Record record;
        record.type = it->type;
        record.symbol = it->symbol;
        record.atomicNumber = it->atomicNumber;
        record.atomicWeight = it->atomicWeight;
        
        for (ChemistryData::Isotope* p=it->isotopes; p<it->isotopes+it->isotopesSize; ++p)
            record.isotopes.push_back(MassAbundance(p->mass, p->abundance));        

        data_[it->type] = record;        
    }
    
    dataInitialized_ = true;
}


// Info implementation

Info::Info() : impl_(new Impl) {}
Info::~Info() {} // auto destruction of impl_
const Info::Record& Info::operator[](Type type) const {return impl_->record(type);}


ostream& operator<<(ostream& os, const Info::Record& r)
{
    cout << r.symbol << " " << r.atomicNumber << " " << r.atomicWeight << " ";
    copy(r.isotopes.begin(), r.isotopes.end(), ostream_iterator<MassAbundance>(cout, " "));
    return os;
}


} // namespace Element


// implementation of element symbol->type (text->enum) mapping

namespace { 

map<string, Element::Type> mapTextEnum_;

void initializeTextEnumMap()
{
    for (ChemistryData::Element* it = ChemistryData::elements(); 
         it != ChemistryData::elements() + ChemistryData::elementsSize();
         ++it)
        mapTextEnum_[it->symbol] = it->type;
}

Element::Type text2enum(const string& text)
{
    if (mapTextEnum_.empty())
        initializeTextEnumMap();
    
    if (!mapTextEnum_.count(text))
        throw runtime_error(("[Chemistry::text2enum()] Error translating symbol " + text).c_str());

    return mapTextEnum_[text];
}

} // namespace


// Formula implementation

class Formula::Impl
{
    public:

    Impl(const string& formula);

    typedef map<Element::Type, int> Data;
    Data data; 
};


Formula::Impl::Impl(const string& formula)
{
    // parse the formula string

    // this implementation is correct, but should be done with a
    // regular expression library if performance becomes an issue

    const string& whitespace_ = " \t\n\r";
    const string& digits_ = "0123456789";
    const string& letters_ = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    string::size_type index = 0;
    while (index < formula.size())
    {
        string::size_type indexTypeBegin = formula.find_first_of(letters_, index);
        string::size_type indexTypeEnd = formula.find_first_not_of(letters_, indexTypeBegin);
        string::size_type indexCountBegin = formula.find_first_of(digits_, indexTypeEnd);
        string::size_type indexCountEnd = formula.find_first_not_of(digits_, indexCountBegin);

        if (indexTypeBegin==string::npos || indexCountBegin==string::npos) 
            throw runtime_error(("[Formula::Impl::Impl()] Invalid formula: " + formula).c_str());

        string symbol = formula.substr(indexTypeBegin, indexTypeEnd-indexTypeBegin);
        int count = atoi(formula.substr(indexCountBegin, indexCountEnd-indexCountBegin).c_str());

        data[text2enum(symbol)] = count; 
        index = formula.find_first_not_of(whitespace_, indexCountEnd);
    }
}


Formula::Formula(const string& formula)
:   impl_(new Impl(formula))
{}


Formula::Formula(const Formula& formula)
:   impl_(new Impl(*formula.impl_))
{}


const Formula& Formula::operator=(const Formula& formula)
{
    *impl_ = *formula.impl_;
    return *this;
}


Formula::~Formula()
{} // auto destruction of impl_


double Formula::monoisotopicMass() const
{
    Element::Info info;
    double result = 0;

    for (Impl::Data::const_iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
    {
        const Element::Info::Record& r = info[it->first]; 
        if (!r.isotopes.empty())
            result += r.isotopes[0].mass * it->second; 
    }

    return result;
}


double Formula::molecularWeight() const
{
    Element::Info info;
    double result = 0;

    for (Impl::Data::const_iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
        result += info[it->first].atomicWeight * it->second; 

    return result;
}


string Formula::formula() const
{
    // collect a term for each element
    vector<string> terms;
    for (Impl::Data::const_iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
    { 
        ostringstream term;
        if (it->second > 0)
            term << it->first << it->second;
        terms.push_back(term.str());
    }

    // sort alphabetically and return the concatenation
    sort(terms.begin(), terms.end());
    return accumulate(terms.begin(), terms.end(), string());
}


int Formula::operator[](Element::Type e) const
{
    return impl_->data[e];
}


int& Formula::operator[](Element::Type e)
{
    return impl_->data[e];
}


const map<Element::Type, int>& Formula::data() const
{
    return impl_->data;
}


Formula& Formula::operator+=(const Formula& that)
{
    for (Map::const_iterator it=that.data().begin(); it!=that.data().end(); ++it)
        impl_->data[it->first] += it->second;
    return *this;
}


Formula& Formula::operator-=(const Formula& that)
{
    for (Map::const_iterator it=that.data().begin(); it!=that.data().end(); ++it)
        impl_->data[it->first] -= it->second;
    return *this;
}


Formula& Formula::operator*=(int scalar)
{
    for (Map::iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
        it->second *= scalar;
    return *this;
}


Formula operator+(const Formula& a, const Formula& b)
{
    Formula result(a);
    result += b;
    return result;
}


Formula operator-(const Formula& a, const Formula& b)
{
    Formula result(a);
    result -= b;
    return result;
}


Formula operator*(const Formula& a, int scalar)
{
    Formula result(a);
    result *= scalar;
    return result;
}


Formula operator*(int scalar, const Formula& a)
{
    Formula result(a);
    result *= scalar;
    return result;
}


ostream& operator<<(ostream& os, const Formula& formula)
{
    os << formula.formula();
    return os;
}


} // namespace Chemistry
} // namespace proteome 
} // namespace pwiz


