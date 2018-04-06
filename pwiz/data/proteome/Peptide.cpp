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

#include "Peptide.hpp"
#include "Modification.hpp"
#include "AminoAcid.hpp"
#include <climits>
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Singleton.hpp"


namespace pwiz {
namespace proteome {


using namespace chemistry;
using boost::shared_ptr;


class Peptide::Impl
{
    public:

    Impl(const std::string& sequence, ModificationParsing mp, ModificationDelimiter md)
        :   sequence_(new string(sequence))
    {
        parse(mp, md);
    }

    Impl(const char* sequence, ModificationParsing mp, ModificationDelimiter md)
        :   sequence_(new string(sequence))
    {
        parse(mp, md);
    }

    Impl(string::const_iterator begin, string::const_iterator end, ModificationParsing mp, ModificationDelimiter md)
        :   sequence_(new string(begin, end))
    {
        parse(mp, md);
    }

    Impl(const char* begin, const char* end, ModificationParsing mp, ModificationDelimiter md)
        :   sequence_(new string(begin, end))
    {
        parse(mp, md);
    }

    Impl(const Impl& other)
        :   sequence_(other.sequence_), mods_(other.mods_ ? new ModificationMap(*other.mods_) : 0),
            monoMass_(other.monoMass_), avgMass_(other.avgMass_), valid_(other.valid_)
    {
    }

    ~Impl()
    {
    }

    inline const string& sequence() const
    {
        return *sequence_;
    }

    inline Formula formula(bool modified) const
    {
        string& sequence = *sequence_;

        // an empty or unparsable sequence returns an empty formula
        if (sequence.empty() || !valid_)
            return Formula();

        Formula formula;

        ModificationMap::const_iterator modItr;
        if (mods_.get()) modItr = mods_->begin();

        // add N terminus formula and modifications
        formula[Element::H] += 1;
        if (mods_.get() && modified && modItr != mods_->end() && modItr->first == ModificationMap::NTerminus())
        {
            const ModificationList& modList = modItr->second;
            for (size_t i=0, end=modList.size(); i < end; ++i)
            {
                const Modification& mod = modList[i];
                if (!mod.hasFormula())
                    throw runtime_error("[Peptide::formula()] peptide formula cannot be generated when any modifications have no formula info");
                formula += mod.formula();
            }
            ++modItr;
        }

        for (size_t i=0, end=sequence.length(); i < end; ++i)
        {
            formula += AminoAcid::Info::record(sequence[i]).residueFormula; // add AA residue formula

            // add modification formulae
            if (mods_ && modified && modItr != mods_->end() && modItr->first == (int) i)
            {
                const ModificationList& modList = modItr->second;
                for (size_t i=0, end=modList.size(); i < end; ++i)
                {
                    const Modification& mod = modList[i];
                    if (!mod.hasFormula())
                        throw runtime_error("[Peptide::formula()] peptide formula cannot be generated when any modifications have no formula info");
                    formula += mod.formula();
                }
                ++modItr;
            }
        }

        // add C terminus formula and modifications
        formula[Element::O] += 1;
        formula[Element::H] += 1;
        if (mods_.get() && modified && modItr != mods_->end() && modItr->first == ModificationMap::CTerminus())
        {
            const ModificationList& modList = modItr->second;
            for (size_t i=0, end=modList.size(); i < end; ++i)
            {
                const Modification& mod = modList[i];
                if (!mod.hasFormula())
                    throw runtime_error("[Peptide::formula()] peptide formula cannot be generated when any modifications have no formula info");
                formula += mod.formula();
            }
            ++modItr;
        }

        return formula;
    }

    inline double monoMass(bool modified) const
    {
        return modified && mods_.get() ? monoMass_ + mods_->monoisotopicDeltaMass() : monoMass_;
    }

    inline double avgMass(bool modified) const
    {
        return modified && mods_.get() ? avgMass_ + mods_->averageDeltaMass() : avgMass_;
    }

    inline ModificationMap& modifications()
    {
        initMods();
        return *mods_;
    }

    inline const ModificationMap& modifications() const
    {
        static ModificationMap emptyModMap;
        return emptyModMap;
    }

    inline Fragmentation fragmentation(const Peptide& peptide, bool mono, bool mods) const
    {
        return Fragmentation(peptide, mono, mods);
    }

    private:
    // since sequence can't be changed after a Peptide is constructed,
    // the sequence can be shared between copies of the same Peptide
    shared_ptr<string> sequence_;
    shared_ptr<ModificationMap> mods_;
    double monoMass_;
    double avgMass_;
    bool valid_;

    inline void initMods()
    {
        // TODO: use boost::thread::once to make initialization thread safe
        if (!mods_.get()) mods_.reset(new ModificationMap());
    }

    inline void parse(ModificationParsing mp, ModificationDelimiter md)
    {
        string& sequence = *sequence_;
        valid_ = false;

        // strip non-AA characters and behave according to the specified parsing style
        char startDelimiter, endDelimiter;
        switch (md)
        {
            default:
            case ModificationDelimiter_Parentheses:
                startDelimiter = '('; endDelimiter = ')';
                break;

            case ModificationDelimiter_Brackets:
                startDelimiter = '['; endDelimiter = ']';
                break;

            case ModificationDelimiter_Braces:
                startDelimiter = '{'; endDelimiter = '}';
                break;
        }

        try
        {
            switch (mp)
            {
                case ModificationParsing_Off:
                    for (size_t i=0, end=sequence.length(); i < end; ++i)
                        try
                        {
                            AminoAcid::Info::record(sequence[i]);
                        }
                        catch (runtime_error&)
                        {
                            throw runtime_error("[Peptide::Impl::parse()] Invalid amino acid in sequence " + sequence);
                        }
                    break;

                case ModificationParsing_ByFormula:
                    for (size_t i=0; i < sequence.length(); ++i)
                    {
                        char& c = sequence[i];
                        if (c == startDelimiter)
                        {
                            for (size_t j=i+1; j < sequence.length(); ++j)
                                if (sequence[j] == endDelimiter)
                                {
                                    if (parseModByFormula(sequence, i, j))
                                        break;
                                    throw runtime_error("[Peptide::Impl::parse()] Expected a chemical formula for all modifications in sequence " + sequence);
                                }
                                else if (j+1 == sequence.length())
                                    throw runtime_error("[Peptide::Impl::parse()] Modification started but not ended in sequence " + sequence);
                        }
                    }
                    break;

                case ModificationParsing_ByMass:
                    for (size_t i=0; i < sequence.length(); ++i)
                    {
                        char& c = sequence[i];
                        if (c == startDelimiter)
                        {
                            for (size_t j=i+1; j < sequence.length(); ++j)
                                if (sequence[j] == endDelimiter)
                                {
                                    if (parseModByMass(sequence, i, j))
                                        break;
                                    throw runtime_error("[Peptide::Impl::parse()] Expected one or two comma-separated numbers in sequence " + sequence);
                                }
                                else if (j+1 == sequence.length())
                                    throw runtime_error("[Peptide::Impl::parse()] Modification started but not ended in sequence " + sequence);
                        }
                    }
                    break;

                default:
                case ModificationParsing_Auto:
                    for (size_t i=0; i < sequence.length(); ++i)
                    {
                        char& c = sequence[i];
                        if (c == startDelimiter)
                        {
                            for (size_t j=i+1; j < sequence.length(); ++j)
                                if (sequence[j] == endDelimiter)
                                {
                                    if (parseModByFormula(sequence, i, j) ||
                                        parseModByMass(sequence, i, j))
                                        break;
                                    throw runtime_error("[Peptide::Impl::parse()] Modification not parseable as either a formula or a mass in sequence " + sequence);
                                }
                                else if (j+1 == sequence.length())
                                    throw runtime_error("[Peptide::Impl::parse()] Modification started but not ended in sequence " + sequence);
                        }
                    }
            }

            valid_ = true;
            Formula unmodifiedFormula = formula(false);
            monoMass_ = unmodifiedFormula.monoisotopicMass();
            avgMass_ = unmodifiedFormula.molecularWeight();
        }
        catch (exception&)
        {
            // TODO: log a warning about an unparsable peptide with no mass information
            monoMass_ = 0;
            avgMass_ = 0;
        }

    }

    inline bool parseModByFormula(string& sequence_, size_t& i, size_t& j)
    {
        string& sequence = sequence_;
        initMods();
        int offset = (i == 0 ? ModificationMap::NTerminus()
                             : (j == sequence.length() ? ModificationMap::CTerminus()
                                                        : i-1));
        try
        {
            (*mods_)[offset].push_back(Formula(sequence.substr(i+1, j-i-1))); // exclude delimiters
        }
        catch (exception&)
        {
            return false;
        }
        sequence.erase(i, j-i+1); // erase the mod from the sequence
        --i;
        return true;
    }

    inline bool parseModByMass(string& sequence_, size_t& i, size_t& j)
    {
        string& sequence = sequence_;
        initMods();
        int offset = (i == 0 ? ModificationMap::NTerminus()
                             : (j == sequence.length() ? ModificationMap::CTerminus()
                                                        : i-1));
        try
        {
            string massStr = sequence.substr(i+1, j-i-1); // exclude delimiters
            vector<string> tokens;
            split(tokens, massStr, bal::is_any_of(","));
            if (tokens.size() == 1)
                (*mods_)[offset].push_back(Modification(lexical_cast<double>(massStr),
                                                         lexical_cast<double>(massStr)));
            else if (tokens.size() == 2)
                (*mods_)[offset].push_back(Modification(lexical_cast<double>(tokens[0]),
                                                         lexical_cast<double>(tokens[1])));
            else
                return false;
        }
        catch (exception&)
        {
            return false;
        }
        sequence.erase(i, j-i+1); // erase the mod from the sequence
        --i;
        return true;
    }
};


PWIZ_API_DECL Peptide::Peptide(const string& sequence, ModificationParsing mp, ModificationDelimiter md)
:   impl_(new Impl(sequence, mp, md)) {}

PWIZ_API_DECL Peptide::Peptide(const char* sequence, ModificationParsing mp, ModificationDelimiter md)
:   impl_(new Impl(sequence, mp, md)) {}

PWIZ_API_DECL Peptide::Peptide(std::string::const_iterator begin,
                               std::string::const_iterator end,
                               ModificationParsing mp,
                               ModificationDelimiter md)
:   impl_(new Impl(begin, end, mp, md)) {}

PWIZ_API_DECL Peptide::Peptide(const char* begin,
                               const char* end,
                               ModificationParsing mp,
                               ModificationDelimiter md)
:   impl_(new Impl(begin, end, mp, md)) {}

PWIZ_API_DECL Peptide::Peptide(const Peptide& other)
:   impl_(new Impl(*other.impl_))
{
}

PWIZ_API_DECL Peptide& Peptide::operator=(const Peptide& rhs)
{
    impl_.reset(new Impl(*rhs.impl_));
    return *this;
}

PWIZ_API_DECL Peptide::~Peptide()
{
}

PWIZ_API_DECL const string& Peptide::sequence() const
{
    return impl_->sequence();
}

PWIZ_API_DECL Formula Peptide::formula(bool modified) const
{
    return impl_->formula(modified);
}

PWIZ_API_DECL double Peptide::monoisotopicMass(int charge, bool modified) const
{
    return impl_->monoMass(false) == 0 ? 0 :
           charge == 0 ? impl_->monoMass(modified)
                       : (impl_->monoMass(modified) + chemistry::Proton * charge) / charge;
}

PWIZ_API_DECL double Peptide::molecularWeight(int charge, bool modified) const
{
    return impl_->avgMass(false) == 0 ? 0 :
           charge == 0 ? impl_->avgMass(modified)
                       : (impl_->avgMass(modified) + chemistry::Proton * charge) / charge;
}

PWIZ_API_DECL ModificationMap& Peptide::modifications()
{
    return impl_->modifications();
}

PWIZ_API_DECL const ModificationMap& Peptide::modifications() const
{
    return impl_->modifications();
}

PWIZ_API_DECL Fragmentation Peptide::fragmentation(bool monoisotopic, bool modified) const
{
    return impl_->fragmentation(*this, monoisotopic, modified);
}

PWIZ_API_DECL bool Peptide::operator==(const Peptide& rhs) const
{
    return sequence() == rhs.sequence() && modifications() == rhs.modifications();
}

PWIZ_API_DECL bool Peptide::operator<(const Peptide& rhs) const
{
    if (sequence().length() == rhs.sequence().length())
    {
        int cmp = sequence().compare(rhs.sequence());
        if (cmp == 0)
            return modifications() < rhs.modifications();
        return cmp < 0;
    }

    return sequence().length() < rhs.sequence().length();
}


class Fragmentation::Impl
{
    public:
    Impl(const Peptide& peptide, bool mono, bool modified)
        :   NTerminalDeltaMass(0), CTerminalDeltaMass(0)
    {
        StaticData::lease staticData;
        aMass = (mono ? staticData->aFormula.monoisotopicMass() : staticData->aFormula.molecularWeight());
        bMass = (mono ? staticData->bFormula.monoisotopicMass() : staticData->bFormula.molecularWeight());
        cMass = (mono ? staticData->cFormula.monoisotopicMass() : staticData->cFormula.molecularWeight());
        xMass = (mono ? staticData->xFormula.monoisotopicMass() : staticData->xFormula.molecularWeight());
        yMass = (mono ? staticData->yFormula.monoisotopicMass() : staticData->yFormula.molecularWeight());
        zMass = (mono ? staticData->zFormula.monoisotopicMass() : staticData->zFormula.molecularWeight());

        const string& sequence = peptide.sequence();
        maxLength = sequence.length();

        const ModificationMap& mods = peptide.modifications();
        ModificationMap::const_iterator modItr = mods.begin();

        if (modified && modItr != mods.end() && modItr->first == ModificationMap::NTerminus())
        {
            const ModificationList& modList = modItr->second;
            for (size_t i=0, end=modList.size(); i < end; ++i)
            {
                const Modification& mod = modList[i];
                NTerminalDeltaMass += (mono ? mod.monoisotopicDeltaMass() : mod.averageDeltaMass());
            }
            ++modItr;
        }

        double mass = 0;
        masses.resize(maxLength, 0);
        for (size_t i=0, end=maxLength; i < end; ++i)
        {
            const Formula& f = AminoAcid::Info::record(sequence[i]).residueFormula;
            mass += (mono ? f.monoisotopicMass() : f.molecularWeight());
            if (modified && modItr != mods.end() && modItr->first == (int) i)
            {
                const ModificationList& modList = modItr->second;
                for (size_t i=0, end=modList.size(); i < end; ++i)
                {
                    const Modification& mod = modList[i];
                    mass += (mono ? mod.monoisotopicDeltaMass() : mod.averageDeltaMass());
                }
                ++modItr;
            }
            masses[i] = mass;
        }

        if (modified && modItr != mods.end() && modItr->first == ModificationMap::CTerminus())
        {
            const ModificationList& modList = modItr->second;
            for (size_t i=0, end=modList.size(); i < end; ++i)
            {
                const Modification& mod = modList[i];
                CTerminalDeltaMass += (mono ? mod.monoisotopicDeltaMass() : mod.averageDeltaMass());
            }
        }
    }

    Impl(const Impl& other)
    {
    }

    ~Impl()
    {
    }

    inline double a(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+aMass
                           : (NTerminalDeltaMass+f(length)+aMass+chemistry::Proton*charge) / charge;
    }

    inline double b(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+bMass
                           : (NTerminalDeltaMass+f(length)+bMass+chemistry::Proton*charge) / charge;
    }

    inline double c(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+cMass
                           : (NTerminalDeltaMass+f(length)+cMass+chemistry::Proton*charge) / charge;
    }

    inline double x(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+xMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+xMass+chemistry::Proton*charge) / charge;
    }

    inline double y(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+yMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+yMass+chemistry::Proton*charge) / charge;
    }

    inline double z(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass+chemistry::Proton*charge) / charge;
    }

    inline double zRadical(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass+chemistry::Proton
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass+chemistry::Proton*(charge+1)) / charge;
    }

    size_t maxLength;

    private:
    vector<double> masses; // N terminal fragment masses
    inline double f(size_t length) const {return length == 0 ? 0 : masses[length-1];}

    double NTerminalDeltaMass;
    double CTerminalDeltaMass;
    double aMass;
    double bMass;
    double cMass;
    double xMass;
    double yMass;
    double zMass;

    struct StaticData : public boost::singleton<StaticData>
    {
        StaticData(boost::restricted)
        {
            aFormula = Formula("C-1O-1");
            bFormula = Formula(""); // proton only
            cFormula = Formula("N1H3");
            xFormula = Formula("C1O1H-2") + Formula("H2O1");
            yFormula = Formula("H2O1");
            zFormula = Formula("N-1H-3") + Formula("H2O1");
        }

        chemistry::Formula aFormula;
        chemistry::Formula bFormula;
        chemistry::Formula cFormula;
        chemistry::Formula xFormula;
        chemistry::Formula yFormula;
        chemistry::Formula zFormula;
    };
};

PWIZ_API_DECL
Fragmentation::Fragmentation(const Peptide& peptide,
                             bool monoisotopic,
                             bool modified)
:   impl_(new Impl(peptide, monoisotopic, modified))
{
}

PWIZ_API_DECL Fragmentation::Fragmentation(const Fragmentation& other)
:   impl_(new Impl(*other.impl_))
{
}

PWIZ_API_DECL Fragmentation::~Fragmentation() {}

PWIZ_API_DECL double Fragmentation::a(size_t length, size_t charge) const
{
    return impl_->a(length, charge);
}

PWIZ_API_DECL double Fragmentation::b(size_t length, size_t charge) const
{
    return impl_->b(length, charge);
}

PWIZ_API_DECL double Fragmentation::c(size_t length, size_t charge) const
{
    if (length == impl_->maxLength)
        throw runtime_error("[Fragmentation::c()] c for full peptide length is impossible");

    return impl_->c(length, charge);
}

PWIZ_API_DECL double Fragmentation::x(size_t length, size_t charge) const
{
    if (length == impl_->maxLength)
        throw runtime_error("[Fragmentation::x()] x for full peptide length is impossible");

    return impl_->x(length, charge);
}

PWIZ_API_DECL double Fragmentation::y(size_t length, size_t charge) const
{
    return impl_->y(length, charge);
}

PWIZ_API_DECL double Fragmentation::z(size_t length, size_t charge) const
{
    return impl_->z(length, charge);
}

PWIZ_API_DECL double Fragmentation::zRadical(size_t length, size_t charge) const
{
    return impl_->zRadical(length, charge);
}

} // namespace proteome
} // namespace pwiz

