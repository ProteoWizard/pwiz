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
#include <climits>
#include "utility/misc/Exception.hpp"

namespace pwiz {
namespace proteome {


using namespace std;
using namespace Chemistry;
using namespace pwiz::util;


class Peptide::Impl
{
    public:

    Impl(Peptide* peptide, const std::string& sequence)
        :   sequence_(sequence), mods_(peptide), dirty_(true)
    {
    }

    Impl(Peptide* peptide, const char* sequence)
        :   sequence_(sequence), mods_(peptide), dirty_(true)
    {
    }

    Impl(Peptide* peptide, string::const_iterator begin, string::const_iterator end)
        :   sequence_(begin, end), mods_(peptide), dirty_(true)
    {
    }

    Impl(Peptide* peptide, const char* begin, const char* end)
        :   sequence_(begin, end), mods_(peptide), dirty_(true)
    {
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
        if (modified && modItr != mods_.end() && modItr->first == ModificationMap::NTerminus)
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
        if (modified && modItr != mods_.end() && modItr->first == ModificationMap::NTerminus)
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
        calculateMasses();
        return modified ? monoMassModified_ : monoMass_;
    }

    inline double avgMass(bool modified) const
    {
        calculateMasses();
        return modified ? avgMassModified_ : avgMass_;
    }

    inline ModificationMap& modifications()
    {
        dirty_ = true;
        return mods_;
    }

    inline Fragmentation fragmentation(const Peptide& peptide, bool mono, bool mods)
    {
        calculateMasses();
        return Fragmentation(peptide, mono, mods);
    }

    private:
    string sequence_;
    ModificationMap mods_;
    mutable double monoMass_;
    mutable double avgMass_;
    mutable double monoMassModified_;
    mutable double avgMassModified_;

    inline void calculateMasses() const
    {
        if (dirty_)
        {
            dirty_ = false;
            Formula unmodifiedFormula = formula(false);
            monoMassModified_ = monoMass_ = unmodifiedFormula.monoisotopicMass();
            avgMassModified_ = avgMass_ = unmodifiedFormula.molecularWeight();

            for (ModificationMap::const_iterator modItr = mods_.begin();
                 modItr != mods_.end();
                 ++modItr)
            {
                const ModificationList& modList = modItr->second;
                for (size_t i=0, end=modList.size(); i < end; ++i)
                {
                    const Modification& mod = modList[i];
                    monoMassModified_ += mod.monoisotopicDeltaMass();
                    avgMassModified_ += mod.averageDeltaMass();
                }
            }
        }
    }

    public:
    mutable bool dirty_;
};


class Modification::Impl
{
    public:

    Impl(const Chemistry::Formula& formula)
        :   formula_(new Formula(formula)),
            monoDeltaMass(formula_->monoisotopicMass()),
            avgDeltaMass(formula_->molecularWeight())
    {
    }

    Impl(double monoisotopicDeltaMass,
         double averageDeltaMass)
        :   monoDeltaMass(monoisotopicDeltaMass),
            avgDeltaMass(averageDeltaMass)
    {
    }

    Impl(const Impl& mod)
        :   formula_(mod.hasFormula() ? new Formula(*mod.formula_) : NULL),
            monoDeltaMass(mod.monoDeltaMass),
            avgDeltaMass(mod.avgDeltaMass)
    {
    }

    inline bool hasFormula() const
    {
        return formula_.get() != NULL;
    }

    inline const Chemistry::Formula& formula() const
    {
        if (!formula_.get())
            throw runtime_error("[Modification::formula()] this mod was constructed with mass info only");
        return *formula_;
    }

    inline double monoisotopicDeltaMass() const
    {
        return monoDeltaMass;
    }

    inline double averageDeltaMass() const
    {
        return avgDeltaMass;
    }

    private:
    auto_ptr<Formula> formula_;
    double monoDeltaMass;
    double avgDeltaMass;
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

PWIZ_API_DECL double Peptide::monoisotopicMass(bool modified, int charge) const
{
    return charge == 0 ? impl_->monoMass(modified)
                       : (impl_->monoMass(modified) + Chemistry::Proton * charge) / charge;
}

PWIZ_API_DECL double Peptide::molecularWeight(bool modified, int charge) const
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


PWIZ_API_DECL Modification::Modification(const Chemistry::Formula& formula)
:   impl_(new Impl(formula))
{
}

PWIZ_API_DECL Modification::Modification(double monoisotopicDeltaMass,
                                         double averageDeltaMass)
:   impl_(new Impl(monoisotopicDeltaMass, averageDeltaMass))
{
}

PWIZ_API_DECL Modification::Modification(const Modification& mod)
:   impl_(new Impl(*mod.impl_))
{
}

PWIZ_API_DECL Modification& Modification::operator=(const Modification& rhs)
{
    impl_.reset(new Impl(*rhs.impl_));
    return *this;
}

PWIZ_API_DECL Modification::~Modification() {}
PWIZ_API_DECL bool Modification::hasFormula() const {return impl_->hasFormula();}
PWIZ_API_DECL const Formula& Modification::formula() const {return impl_->formula();}
PWIZ_API_DECL double Modification::monoisotopicDeltaMass() const {return impl_->monoisotopicDeltaMass();}
PWIZ_API_DECL double Modification::averageDeltaMass() const {return impl_->averageDeltaMass();}

PWIZ_API_DECL const int ModificationMap::NTerminus = INT_MIN;
PWIZ_API_DECL const int ModificationMap::CTerminus = INT_MAX;

PWIZ_API_DECL ModificationMap::ModificationMap(Peptide* peptide)
: peptide(peptide)
{
}

PWIZ_API_DECL void ModificationMap::clear()
{
    peptide->impl_->dirty_ = true;
    virtual_map<int, ModificationList>::clear();
}

PWIZ_API_DECL void ModificationMap::erase(ModificationMap::iterator position)
{
    peptide->impl_->dirty_ = true;
    virtual_map<int, ModificationList>::erase(position);
}

PWIZ_API_DECL
void ModificationMap::erase(ModificationMap::iterator start,
                            ModificationMap::iterator finish)
{
    peptide->impl_->dirty_ = true;
    virtual_map<int, ModificationList>::erase(start, finish);
}

PWIZ_API_DECL ModificationMap::size_type ModificationMap::erase(const key_type& x)
{
    peptide->impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::erase(x);
}

PWIZ_API_DECL
std::pair<ModificationMap::iterator, bool> ModificationMap::insert(const value_type& x)
{
    peptide->impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::insert(x);
}

PWIZ_API_DECL
ModificationMap::iterator ModificationMap::insert(ModificationMap::iterator position,
                                                  const value_type& x)
{
    peptide->impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::insert(position, x);
}

PWIZ_API_DECL
void ModificationMap::swap(ModificationMap& other)
{
    throw runtime_error("[ModificationMap::swap()] should not be called");
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

        if (modified && modItr != mods.end() && modItr->first == ModificationMap::NTerminus)
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

        if (modified && modItr != mods.end() && modItr->first == ModificationMap::CTerminus)
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

Fragmentation::Fragmentation(const Peptide& peptide,
                             bool monoisotopic,
                             bool modified)
:   impl_(new Impl(peptide, monoisotopic, modified))
{
}

Fragmentation::Fragmentation(const Fragmentation& other)
:   impl_(new Impl(*other.impl_))
{
}

Fragmentation::~Fragmentation() {}

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

double Fragmentation::a(size_t length, size_t charge) const
{
    return impl_->a(length, charge);
}

double Fragmentation::b(size_t length, size_t charge) const
{
    return impl_->b(length, charge);
}

double Fragmentation::c(size_t length, size_t charge) const
{
    if (length == impl_->maxLength)
        throw runtime_error("[Fragmentation::c()] c for full peptide length is impossible");

    return impl_->c(length, charge);
}

double Fragmentation::x(size_t length, size_t charge) const
{
    if (length == impl_->maxLength)
        throw runtime_error("[Fragmentation::x()] x for full peptide length is impossible");

    return impl_->x(length, charge);
}

double Fragmentation::y(size_t length, size_t charge) const
{
    return impl_->y(length, charge);
}

double Fragmentation::z(size_t length, size_t charge) const
{
    return impl_->z(length, charge);
}

} // namespace proteome
} // namespace pwiz

