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
#include "Modification.hpp"
#include "AminoAcid.hpp"
#include <iostream>
#include <climits>
#include "utility/misc/Exception.hpp"

namespace pwiz {
namespace proteome {


using namespace std;
using namespace Chemistry;


class Peptide::Impl
{
    public:

    Impl(Peptide* peptide, const std::string& sequence)
        :   sequence_(sequence), mods_(peptide)
    {
        calculateMasses();
    }

    Impl(Peptide* peptide, const char* sequence)
        :   sequence_(sequence), mods_(peptide)
    {
        calculateMasses();
    }

    Impl(Peptide* peptide, string::const_iterator begin, string::const_iterator end)
        :   sequence_(begin, end), mods_(peptide)
    {
        calculateMasses();
    }

    Impl(Peptide* peptide, const char* begin, const char* end)
        :   sequence_(begin, end), mods_(peptide)
    {
        calculateMasses();
    }

    inline const string& sequence() const
    {
        return sequence_;
    }

    inline Formula formula(bool modified) const
    {
        AminoAcid::Info info;
        Formula formula;

        ModificationMap::const_iterator modItr = mods_.begin();

        // add N terminus formula and modifications
        formula += Formula("H1");
        if (modified && modItr != mods_.end() && modItr->first == ModificationMap::NTerminus())
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

        for (size_t i=0, end=sequence_.length(); i < end; ++i)
        {
            formula += info[sequence_[i]].residueFormula; // add AA residue formula

            // add modification formulae
            if (modified && modItr != mods_.end() && modItr->first == (int) i)
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
        formula += Formula("H1O1");
        if (modified && modItr != mods_.end() && modItr->first == ModificationMap::NTerminus())
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
        return modified ? monoMass_ + mods_.monoisotopicDeltaMass() : monoMass_;
    }

    inline double avgMass(bool modified) const
    {
        return modified ? avgMass_ + mods_.averageDeltaMass() : avgMass_;
    }

    inline ModificationMap& modifications()
    {
        return mods_;
    }

    inline Fragmentation fragmentation(const Peptide& peptide, bool mono, bool mods) const
    {
        return Fragmentation(peptide, mono, mods);
    }

    private:
    string sequence_;
    ModificationMap mods_;
    double monoMass_;
    double avgMass_;

    inline void calculateMasses()
    {
        Formula unmodifiedFormula = formula(false);
        monoMass_ = unmodifiedFormula.monoisotopicMass();
        avgMass_ = unmodifiedFormula.molecularWeight();
    }
};


PWIZ_API_DECL Peptide::Peptide(const string& sequence) : impl_(new Impl(this, sequence)) {}
PWIZ_API_DECL Peptide::Peptide(const char* sequence) : impl_(new Impl(this, sequence)) {}
PWIZ_API_DECL Peptide::Peptide(std::string::const_iterator begin,
                               std::string::const_iterator end)
:   impl_(new Impl(this, begin, end)) {}

PWIZ_API_DECL Peptide::Peptide(const char* begin, const char* end)
:   impl_(new Impl(this, begin, end)) {}

PWIZ_API_DECL Peptide::Peptide(const Peptide& other)
:   impl_(new Impl(this, other.sequence()))
{
}

PWIZ_API_DECL Peptide& Peptide::operator=(const Peptide& rhs)
{
    impl_.reset(new Impl(this, rhs.sequence()));
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
    return charge == 0 ? impl_->monoMass(modified)
                       : (impl_->monoMass(modified) + Chemistry::Proton * charge) / charge;
}

PWIZ_API_DECL double Peptide::molecularWeight(int charge, bool modified) const
{
    return charge == 0 ? impl_->avgMass(modified)
                       : (impl_->avgMass(modified) + Chemistry::Proton * charge) / charge;
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
    return impl_->sequence() == rhs.impl_->sequence() &&
           impl_->monoMass(true) == rhs.impl_->monoMass(true);
}


class Fragmentation::Impl
{
    public:
    Impl(const Peptide& peptide, bool mono, bool modified)
        :   NTerminalDeltaMass(0), CTerminalDeltaMass(0),
            aMass(mono ? aMonoMass : aAvgMass),
            bMass(mono ? bMonoMass : bAvgMass),
            cMass(mono ? cMonoMass : cAvgMass),
            xMass(mono ? xMonoMass : xAvgMass),
            yMass(mono ? yMonoMass : yAvgMass),
            zMass(mono ? zMonoMass : zAvgMass)
    {
        if (aFormula.formula().empty())
        {
            aFormula = Formula("C-1O-1");
            bFormula = Formula(""); // proton only
            cFormula = Formula("N1H3");
            xFormula = Formula("C1O1H-2") + Formula("H2O1");
            yFormula = Formula("H2O1");
            zFormula = Formula("N-1H-3") + Formula("H2O1");

            aMonoMass = aFormula.monoisotopicMass();
            bMonoMass = bFormula.monoisotopicMass();
            cMonoMass = cFormula.monoisotopicMass();
            xMonoMass = xFormula.monoisotopicMass();
            yMonoMass = yFormula.monoisotopicMass();
            zMonoMass = zFormula.monoisotopicMass();

            aAvgMass = aFormula.molecularWeight();
            bAvgMass = bFormula.molecularWeight();
            cAvgMass = cFormula.molecularWeight();
            xAvgMass = xFormula.molecularWeight();
            yAvgMass = yFormula.molecularWeight();
            zAvgMass = zFormula.molecularWeight();
        }

        const string& sequence = peptide.sequence();
        maxLength = sequence.length();
        AminoAcid::Info info;

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
            const Formula& f = info[sequence[i]].residueFormula;
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

    inline double a(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+aMass
                           : (NTerminalDeltaMass+f(length)+aMass+Chemistry::Proton*charge) / charge;
    }

    inline double b(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+bMass
                           : (NTerminalDeltaMass+f(length)+bMass+Chemistry::Proton*charge) / charge;
    }

    inline double c(size_t length, size_t charge) const
    {
        return charge == 0 ? NTerminalDeltaMass+f(length)+cMass
                           : (NTerminalDeltaMass+f(length)+cMass+Chemistry::Proton*charge) / charge;
    }

    inline double x(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+xMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+xMass+Chemistry::Proton*charge) / charge;
    }

    inline double y(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+yMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+yMass+Chemistry::Proton*charge) / charge;
    }

    inline double z(size_t length, size_t charge) const
    {
        return charge == 0 ? CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass
                           : (CTerminalDeltaMass+masses.back()-f(maxLength-length)+zMass+Chemistry::Proton*charge) / charge;
    }

    size_t maxLength;

    private:
    vector<double> masses; // N terminal fragment masses
    inline double f(size_t length) const {return length == 0 ? 0 : masses[length-1];}

    double NTerminalDeltaMass;
    double CTerminalDeltaMass;
    double& aMass;
    double& bMass;
    double& cMass;
    double& xMass;
    double& yMass;
    double& zMass;

    static Chemistry::Formula aFormula;
    static Chemistry::Formula bFormula;
    static Chemistry::Formula cFormula;
    static Chemistry::Formula xFormula;
    static Chemistry::Formula yFormula;
    static Chemistry::Formula zFormula;

    static double aMonoMass, aAvgMass;
    static double bMonoMass, bAvgMass;
    static double cMonoMass, cAvgMass;
    static double xMonoMass, xAvgMass;
    static double yMonoMass, yAvgMass;
    static double zMonoMass, zAvgMass;
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

Chemistry::Formula Fragmentation::Impl::aFormula;
Chemistry::Formula Fragmentation::Impl::bFormula;
Chemistry::Formula Fragmentation::Impl::cFormula;
Chemistry::Formula Fragmentation::Impl::xFormula;
Chemistry::Formula Fragmentation::Impl::yFormula;
Chemistry::Formula Fragmentation::Impl::zFormula;

double Fragmentation::Impl::aMonoMass;
double Fragmentation::Impl::bMonoMass;
double Fragmentation::Impl::cMonoMass;
double Fragmentation::Impl::xMonoMass;
double Fragmentation::Impl::yMonoMass;
double Fragmentation::Impl::zMonoMass;

double Fragmentation::Impl::aAvgMass;
double Fragmentation::Impl::bAvgMass;
double Fragmentation::Impl::cAvgMass;
double Fragmentation::Impl::xAvgMass;
double Fragmentation::Impl::yAvgMass;
double Fragmentation::Impl::zAvgMass;

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

} // namespace proteome
} // namespace pwiz

