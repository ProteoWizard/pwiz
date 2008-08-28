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

    Impl(Peptide* peptide, string::const_iterator begin, string::const_iterator end)
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

    Impl(const Chemistry::Formula& adduct,
         const Chemistry::Formula& deduct)
        :   formula_(new Formula(adduct-deduct)),
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
PWIZ_API_DECL Peptide::Peptide(std::string::const_iterator begin,
                               std::string::const_iterator end)
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

PWIZ_API_DECL double Peptide::monoisotopicMass(bool modified) const
{
    return impl_->monoMass(modified);
}

PWIZ_API_DECL double Peptide::molecularWeight(bool modified) const
{
    return impl_->avgMass(modified);
}

PWIZ_API_DECL ModificationMap& Peptide::modifications()
{
    return impl_->modifications();
}

PWIZ_API_DECL Modification::Modification(const Chemistry::Formula& adduct,
                                         const Chemistry::Formula& deduct)
:   impl_(new Impl(adduct, deduct))
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


} // namespace proteome
} // namespace pwiz

