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

#include "Chemistry.hpp"
#include "ChemistryData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Singleton.hpp"


namespace pwiz {
namespace chemistry { 


PWIZ_API_DECL bool MassAbundance::operator==(const MassAbundance& that) const
{
    return this->mass==that.mass && this->abundance==that.abundance;
}


PWIZ_API_DECL bool MassAbundance::operator!=(const MassAbundance& that) const
{
    return !operator==(that); 
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const MassAbundance& ma)
{
    os << "<" << ma.mass << ", " << ma.abundance << ">";
    return os;
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const MassDistribution& md)
{
    copy(md.begin(), md.end(), ostream_iterator<MassAbundance>(os, "\n"));
    return os;
}


namespace Element {


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, Type type)
{
    os << Info::record(type).symbol;
    return os;
}


class RecordData : public boost::singleton<RecordData>
{
    public:
    RecordData(boost::restricted)
    {
        // iterate through the ChemistryData array and put it in our own data structure
        detail::Element* it = detail::elements();
        detail::Element* end = detail::elements() + detail::elementsSize();

        data_.resize(detail::elementsSize());

        for (; it!=end; ++it)
        {
            Info::Record record;
            record.type = it->type;
            record.symbol = it->symbol;
            record.atomicNumber = it->atomicNumber;
            record.atomicWeight = it->atomicWeight;
            
            record.monoisotope.abundance = 0;
            for (detail::Isotope* p=it->isotopes; p<it->isotopes+it->isotopesSize; ++p)
            {
                record.isotopes.push_back(MassAbundance(p->mass, p->abundance));
                if (p->abundance > record.monoisotope.abundance)
                    record.monoisotope = record.isotopes.back();
            }

            data_[it->type] = record;        
        }
    }

    inline const Info::Record& record(Type type) const
    {
        if (data_.size() <= size_t(type))
            throw runtime_error("[chemistry::Element::Info::Impl::record()]  Record not found.");

        return data_[type];
    }

    private:
    vector<Info::Record> data_;
};


// implementation of element symbol->type (text->enum) mapping
namespace { 

struct Text2EnumMap : public map<string, Element::Type>,
                      public boost::singleton<Text2EnumMap>
{
    Text2EnumMap(boost::restricted)
    {
        for (detail::Element* it = detail::elements();
            it != detail::elements() + detail::elementsSize();
            ++it)
        {
            insert(make_pair(it->symbol, it->type));
            if (it->synonym)
                insert(make_pair(it->synonym, it->type));
        }
    }
};

Element::Type text2enum(const string& text)
{
    Text2EnumMap::lease text2EnumMap;
    Text2EnumMap::const_iterator itr = text2EnumMap->find(text);
    if (itr == text2EnumMap->end())
        throw runtime_error(("[chemistry::text2enum()] Error translating symbol " + text).c_str());
    return itr->second;
}

} // namespace


PWIZ_API_DECL const Info::Record& Info::record(Type type) {return RecordData::instance->record(type);}
PWIZ_API_DECL const Info::Record& Info::record(const string& symbol) {return record(text2enum(symbol));}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Info::Record& r)
{
    cout << r.symbol << " " << r.atomicNumber << " " << r.atomicWeight << " " << r.monoisotope << " ";
    copy(r.isotopes.begin(), r.isotopes.end(), ostream_iterator<MassAbundance>(cout, " "));
    return os;
}


} // namespace Element



// Formula implementation

class Formula::Impl
{
    public:

    Impl(const string& formula);

    inline void calculateMasses()
    {
        if (dirty)
        {
            dirty = false;

            monoMass = avgMass = 0;
            for (size_t i=0; i <= size_t(Element::_15N); ++i)
            {
                int count = CHONSP_data[i];
                if (count != 0)
                {
                    const Element::Info::Record& r = Element::Info::record(Element::Type(i));
                    if (!r.isotopes.empty())
                        monoMass += r.monoisotope.mass * count;
                    avgMass += r.atomicWeight * count;
                }
            }

            vector<Data::iterator> zeroElements;
            for (Data::iterator it=data.begin(); it!=data.end(); ++it)
            {
                if (it->second == 0)
                    zeroElements.push_back(it);
                else
                {
                    const Element::Info::Record& r = Element::Info::record(it->first);
                    if (!r.isotopes.empty())
                        monoMass += r.monoisotope.mass * it->second;
                    avgMass += r.atomicWeight * it->second;
                }
            }

            for (size_t i=0; i < zeroElements.size(); ++i)
                data.erase(zeroElements[i]);
        }
    }

    typedef map<Element::Type, int> Data;
    Data data;
    vector<int> CHONSP_data;
    double monoMass;
    double avgMass;
    bool dirty; // true if masses need updating
};


Formula::Impl::Impl(const string& formula)
:   monoMass(0), avgMass(0), dirty(false)
{
    CHONSP_data.resize(size_t(Element::_15N)+1, 0);

    if (formula.empty())
        return;

    // parse the formula string

    // this implementation is correct, but should be done with a
    // regular expression library if performance becomes an issue

    const string& whitespace_ = " \t\n\r";
    const string& digits_ = "-0123456789";
    const string& symbolLeads_ = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_";
    const string& lowers_ = "abcdefghijklmnopqrstuvwxyz";

    string::size_type index = 0;
    while (index < formula.size() && index != string::npos)
    {
        string::size_type indexTypeBegin = formula.find_first_of(symbolLeads_, index);
        if (indexTypeBegin == string::npos)
            throw runtime_error("[Formula::Impl::Impl()] Invalid formula: " + formula);
        string::size_type indexTypeEnd = indexTypeBegin;
        if (formula[indexTypeBegin] == '_')
        {
            indexTypeEnd = formula.find_first_of(symbolLeads_, indexTypeBegin+1); // Skip the "2" in _2H
        }
        // Distinguish "H" and "He" or "Uuu" and "Uub"
        indexTypeEnd++;
        while (indexTypeEnd < formula.size() && lowers_.find_first_of(formula[indexTypeEnd]) != string::npos)
        {
            indexTypeEnd++;
        }
        string symbol = formula.substr(indexTypeBegin, indexTypeEnd - indexTypeBegin);
        string::size_type indexNextTypeBegin = (indexTypeEnd == formula.size()) ? string::npos :
            formula.find_first_of(symbolLeads_, indexTypeEnd);
        string::size_type indexCountBegin = (indexTypeEnd == formula.size()) ? string::npos :
            formula.find_first_of(digits_, indexTypeEnd);
        string::size_type indexCountEnd;

        int count;
        if (indexCountBegin == string::npos ||
            (indexNextTypeBegin != string::npos && indexNextTypeBegin < indexCountBegin))
        {
            count = 1;
            indexCountEnd = indexTypeEnd; // Start next symbol read from here
        }
        else
        {
            indexCountEnd = formula.find_first_not_of(digits_, indexCountBegin);
            if (indexCountEnd == string::npos)
            {
                indexCountEnd = formula.size();
            }

            try
            {
                count = lexical_cast<int>(formula.substr(indexCountBegin, indexCountEnd-indexCountBegin));
            }
            catch(bad_lexical_cast&)
            {
                throw runtime_error("[Formula::Impl::Impl()] Invalid count in formula: " + formula);
            }
        }

        Element::Type type = Element::text2enum(symbol);
        if (type > Element::_15N)
            data[type] = count;
        else
            CHONSP_data[size_t(type)] = count;

        index = formula.find_first_not_of(whitespace_, indexCountEnd);

        const Element::Info::Record& r = Element::Info::record(type);
        if (!r.isotopes.empty())
            monoMass += r.monoisotope.mass * count;
        avgMass += r.atomicWeight * count;
    }
}


PWIZ_API_DECL Formula::Formula(const string& formula)
:   impl_(new Impl(formula))
{}

PWIZ_API_DECL Formula::Formula(const char* formula)
:   impl_(new Impl(formula))
{}

PWIZ_API_DECL Formula::Formula(const Formula& formula)
:   impl_(new Impl(*formula.impl_))
{}


PWIZ_API_DECL const Formula& Formula::operator=(const Formula& formula)
{
    *impl_ = *formula.impl_;
    return *this;
}


PWIZ_API_DECL Formula::~Formula()
{} // auto destruction of impl_


PWIZ_API_DECL double Formula::monoisotopicMass() const
{
    impl_->calculateMasses();
    return impl_->monoMass;
}


PWIZ_API_DECL double Formula::molecularWeight() const
{
    impl_->calculateMasses();
    return impl_->avgMass;
}


PWIZ_API_DECL string Formula::formula() const
{
    // collect a term for each element
    vector<string> terms;

    for (size_t i=0; i <= size_t(Element::_15N); ++i)
    {
        int count = impl_->CHONSP_data[i];
        ostringstream term;
        if (count != 0)
            term << Element::Type(i) << count;
        terms.push_back(term.str());
    }

    for (Impl::Data::const_iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
    { 
        ostringstream term;
        if (it->second != 0)
            term << it->first << it->second;
        terms.push_back(term.str());
    }

    // sort alphabetically and return the concatenation
    sort(terms.begin(), terms.end());
    return accumulate(terms.begin(), terms.end(), string());
}


PWIZ_API_DECL int Formula::operator[](Element::Type e) const
{
    if (e > Element::_15N)
        return impl_->data[e];
    else
        return impl_->CHONSP_data[e];
}


PWIZ_API_DECL int& Formula::operator[](Element::Type e)
{
    impl_->dirty = true; // worst-case
    if (e > Element::_15N)
        return impl_->data[e];
    else
        return impl_->CHONSP_data[e];
}


PWIZ_API_DECL map<Element::Type, int> Formula::data() const
{
    map<Element::Type, int> dataCopy(impl_->data);
    for (size_t i=0; i <= size_t(Element::_15N); ++i)
    {
        int count = impl_->CHONSP_data[i];
        if (count != 0)
            dataCopy[Element::Type(i)] = count;
    }
    return dataCopy;
}


PWIZ_API_DECL Formula& Formula::operator+=(const Formula& that)
{
    impl_->CHONSP_data[0] += that.impl_->CHONSP_data[0];
    impl_->CHONSP_data[1] += that.impl_->CHONSP_data[1];
    impl_->CHONSP_data[2] += that.impl_->CHONSP_data[2];
    impl_->CHONSP_data[3] += that.impl_->CHONSP_data[3];
    impl_->CHONSP_data[4] += that.impl_->CHONSP_data[4];
    impl_->CHONSP_data[5] += that.impl_->CHONSP_data[5];
    impl_->CHONSP_data[6] += that.impl_->CHONSP_data[6];
    impl_->CHONSP_data[7] += that.impl_->CHONSP_data[7];
    impl_->CHONSP_data[8] += that.impl_->CHONSP_data[8];
    impl_->CHONSP_data[9] += that.impl_->CHONSP_data[9];

    for (Map::const_iterator it=that.impl_->data.begin(); it!=that.impl_->data.end(); ++it)
        impl_->data[it->first] += it->second;
    impl_->dirty = true;
    return *this;
}


PWIZ_API_DECL Formula& Formula::operator-=(const Formula& that)
{
    impl_->CHONSP_data[0] -= that.impl_->CHONSP_data[0];
    impl_->CHONSP_data[1] -= that.impl_->CHONSP_data[1];
    impl_->CHONSP_data[2] -= that.impl_->CHONSP_data[2];
    impl_->CHONSP_data[3] -= that.impl_->CHONSP_data[3];
    impl_->CHONSP_data[4] -= that.impl_->CHONSP_data[4];
    impl_->CHONSP_data[5] -= that.impl_->CHONSP_data[5];
    impl_->CHONSP_data[6] -= that.impl_->CHONSP_data[6];
    impl_->CHONSP_data[7] -= that.impl_->CHONSP_data[7];
    impl_->CHONSP_data[8] -= that.impl_->CHONSP_data[8];
    impl_->CHONSP_data[9] -= that.impl_->CHONSP_data[9];

    for (Map::const_iterator it=that.impl_->data.begin(); it!=that.impl_->data.end(); ++it)
        impl_->data[it->first] -= it->second;
    impl_->dirty = true;
    return *this;
}


PWIZ_API_DECL Formula& Formula::operator*=(int scalar)
{
    impl_->CHONSP_data[0] *= scalar;
    impl_->CHONSP_data[1] *= scalar;
    impl_->CHONSP_data[2] *= scalar;
    impl_->CHONSP_data[3] *= scalar;
    impl_->CHONSP_data[4] *= scalar;
    impl_->CHONSP_data[5] *= scalar;
    impl_->CHONSP_data[6] *= scalar;
    impl_->CHONSP_data[7] *= scalar;
    impl_->CHONSP_data[8] *= scalar;
    impl_->CHONSP_data[9] *= scalar;

    for (Map::iterator it=impl_->data.begin(); it!=impl_->data.end(); ++it)
        it->second *= scalar;
    impl_->dirty = true;
    return *this;
}


PWIZ_API_DECL bool Formula::operator==(const Formula& that) const
{
    if (impl_->CHONSP_data[0] != that.impl_->CHONSP_data[0] ||
        impl_->CHONSP_data[1] != that.impl_->CHONSP_data[1] ||
        impl_->CHONSP_data[2] != that.impl_->CHONSP_data[2] ||
        impl_->CHONSP_data[3] != that.impl_->CHONSP_data[3] ||
        impl_->CHONSP_data[4] != that.impl_->CHONSP_data[4] ||
        impl_->CHONSP_data[5] != that.impl_->CHONSP_data[5] ||
        impl_->CHONSP_data[6] != that.impl_->CHONSP_data[6] ||
        impl_->CHONSP_data[7] != that.impl_->CHONSP_data[7] ||
        impl_->CHONSP_data[8] != that.impl_->CHONSP_data[8] ||
        impl_->CHONSP_data[9] != that.impl_->CHONSP_data[9])
        return false;

    impl_->calculateMasses(); // will remove zero-count elements from data map
    that.impl_->calculateMasses();

    if (impl_->data.size() != that.impl_->data.size())
        return false;

    Map::const_iterator itr, thatItr;
    for (itr = impl_->data.begin(), thatItr = that.impl_->data.begin();
         itr != impl_->data.end();
         ++itr, ++thatItr)
         // equal maps are pairwise equal for both elements of the pair
         if (itr->first != thatItr->first || itr->second != thatItr->second)
             return false;
    return true;
}


PWIZ_API_DECL bool Formula::operator!=(const Formula& that) const
{
    return !(*this == that);
}


PWIZ_API_DECL Formula operator+(const Formula& a, const Formula& b)
{
    Formula result(a);
    result += b;
    return result;
}


PWIZ_API_DECL Formula operator-(const Formula& a, const Formula& b)
{
    Formula result(a);
    result -= b;
    return result;
}


PWIZ_API_DECL Formula operator*(const Formula& a, int scalar)
{
    Formula result(a);
    result *= scalar;
    return result;
}


PWIZ_API_DECL Formula operator*(int scalar, const Formula& a)
{
    Formula result(a);
    result *= scalar;
    return result;
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Formula& formula)
{
    os << formula.formula();
    return os;
}


} // namespace chemistry
} // namespace pwiz
