//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Modification.hpp"
#include <climits>
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace proteome {


using namespace chemistry;
using namespace pwiz::util;


class Modification::Impl
{
    public:

    Impl(const chemistry::Formula& formula)
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

    inline const chemistry::Formula& formula() const
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

PWIZ_API_DECL Modification::Modification()
:   impl_(new Impl(0, 0))
{
}

PWIZ_API_DECL Modification::Modification(const chemistry::Formula& formula)
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

PWIZ_API_DECL bool Modification::operator==(const Modification& rhs) const
{
    return monoisotopicDeltaMass() == rhs.monoisotopicDeltaMass() &&
           averageDeltaMass() == rhs.averageDeltaMass();
}

PWIZ_API_DECL bool Modification::operator<(const Modification& rhs) const
{
    return monoisotopicDeltaMass() < rhs.monoisotopicDeltaMass();
}


PWIZ_API_DECL ModificationList::ModificationList()
{
}

PWIZ_API_DECL ModificationList::ModificationList(const Modification& mod)
:   vector<Modification>(1, mod)
{
}

PWIZ_API_DECL ModificationList::ModificationList(const std::vector<Modification>& mods)
:   vector<Modification>(mods.begin(), mods.end())
{
}

PWIZ_API_DECL double ModificationList::monoisotopicDeltaMass() const
{
    double mass = 0;
    for (const_iterator itr = begin(); itr != end(); ++itr)
        mass += itr->monoisotopicDeltaMass();
    return mass;
}

PWIZ_API_DECL double ModificationList::averageDeltaMass() const
{
    double mass = 0;
    for (const_iterator itr = begin(); itr != end(); ++itr)
        mass += itr->averageDeltaMass();
    return mass;
}

PWIZ_API_DECL bool ModificationList::operator==(const ModificationList& rhs) const
{
    if (size() != rhs.size())
        return false;

    ModificationList::const_iterator itr, rhsItr;
    for (itr = begin(), rhsItr = rhs.begin();
         itr != end() && rhsItr != rhs.end();
         ++itr, ++rhsItr)
    {
        if (!(*itr == *rhsItr))
            return false;
    }
    return true; // lists are equal
}

PWIZ_API_DECL bool ModificationList::operator<(const ModificationList& rhs) const
{
    if (size() == rhs.size())
    {
        ModificationList::const_iterator itr, rhsItr;
        for (itr = begin(), rhsItr = rhs.begin();
             itr != end() && rhsItr != rhs.end();
             ++itr, ++rhsItr)
        {
            if (!(*itr == *rhsItr))
                return *itr < *rhsItr;
        }
        return false; // lists are equal
    } 

    return size() < rhs.size();
}


int ModificationMap::NTerminus() {return INT_MIN;}
int ModificationMap::CTerminus() {return INT_MAX;}

class ModificationMap::Impl
{
    public:

    Impl(ModificationMap* mods)
        :   dirty_(false),
            monoDeltaMass_(0), avgDeltaMass_(0),
            mods_(mods)
    {
    }

    Impl(ModificationMap* mods, const Impl& other)
        :   dirty_(other.dirty_),
            monoDeltaMass_(other.monoDeltaMass_),
            avgDeltaMass_(other.avgDeltaMass_),
            mods_(mods)
    {
        mods_->virtual_map<int, ModificationList>::insert(other.mods_->begin(), other.mods_->end());
    }

    inline double monoDeltaMass() const
    {
        calculateMasses();
        return monoDeltaMass_;
    }

    inline double avgDeltaMass() const
    {
        calculateMasses();
        return avgDeltaMass_;
    }

    mutable bool dirty_;
    mutable double monoDeltaMass_;
    mutable double avgDeltaMass_;

    private:

    ModificationMap* mods_;

    inline void calculateMasses() const
    {
        if (dirty_)
        {
            dirty_ = false;
            monoDeltaMass_ = avgDeltaMass_ = 0;
            for (ModificationMap::const_iterator itr = mods_->begin(); itr != mods_->end(); ++itr)
            {
                const ModificationList& modList = itr->second;
                monoDeltaMass_ += modList.monoisotopicDeltaMass();
                avgDeltaMass_ += modList.averageDeltaMass();
            }
        }
    }
};

PWIZ_API_DECL ModificationMap::~ModificationMap()
{
}

PWIZ_API_DECL ModificationMap::ModificationMap()
:   impl_(new Impl(this))
{
}

PWIZ_API_DECL ModificationMap::ModificationMap(const ModificationMap& other)
:   impl_(new Impl(this, *other.impl_))
{
}

PWIZ_API_DECL double ModificationMap::monoisotopicDeltaMass() const
{
    return impl_->monoDeltaMass();
}

PWIZ_API_DECL double ModificationMap::averageDeltaMass() const
{
    return impl_->avgDeltaMass();
}

PWIZ_API_DECL ModificationMap::iterator ModificationMap::begin()
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::begin();
}

PWIZ_API_DECL ModificationMap::iterator ModificationMap::end()
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::end();
}

PWIZ_API_DECL ModificationMap::reverse_iterator ModificationMap::rbegin()
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::rbegin();
}

PWIZ_API_DECL ModificationMap::reverse_iterator ModificationMap::rend()
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::rend();
}

PWIZ_API_DECL
ModificationMap::mapped_type&
ModificationMap::operator[](const ModificationMap::key_type& x)
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::operator[](x);
}

PWIZ_API_DECL
std::pair<ModificationMap::iterator, ModificationMap::iterator>
ModificationMap::equal_range(const ModificationMap::key_type& x)
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::equal_range(x);
}

PWIZ_API_DECL
ModificationMap::iterator
ModificationMap::find(const ModificationMap::key_type& x)
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::find(x);
}

PWIZ_API_DECL
ModificationMap::iterator
ModificationMap::lower_bound(const ModificationMap::key_type& x)
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::lower_bound(x);
}

PWIZ_API_DECL
ModificationMap::iterator
ModificationMap::upper_bound(const ModificationMap::key_type& x)
{
    impl_->dirty_ = true;
    return virtual_map<int, ModificationList>::upper_bound(x);
}

PWIZ_API_DECL void ModificationMap::clear()
{
    impl_->monoDeltaMass_ = impl_->avgDeltaMass_ = 0;
    virtual_map<int, ModificationList>::clear();
}

PWIZ_API_DECL void ModificationMap::erase(ModificationMap::iterator position)
{
    const ModificationList& modList = position->second;
    impl_->monoDeltaMass_ -= modList.monoisotopicDeltaMass();
    impl_->avgDeltaMass_ -= modList.averageDeltaMass();
    virtual_map<int, ModificationList>::erase(position);
}

PWIZ_API_DECL
void ModificationMap::erase(ModificationMap::iterator start,
                            ModificationMap::iterator finish)
{
    for (; start != finish; ++start)
    {
        const ModificationList& modList = start->second;
        impl_->monoDeltaMass_ -= modList.monoisotopicDeltaMass();
        impl_->avgDeltaMass_ -= modList.averageDeltaMass();
    }
    virtual_map<int, ModificationList>::erase(start, finish);
}

PWIZ_API_DECL ModificationMap::size_type ModificationMap::erase(const key_type& x)
{
    iterator itr = find(x);
    if (itr != end())
    {
        erase(itr);
        return 1;
    }
    return 0;
}

PWIZ_API_DECL
std::pair<ModificationMap::iterator, bool> ModificationMap::insert(const value_type& x)
{
    std::pair<ModificationMap::iterator, bool> insertResult =
        virtual_map<int, ModificationList>::insert(x);
    if (insertResult.second)
    {
        const ModificationList& modList = x.second;
        impl_->monoDeltaMass_ += modList.monoisotopicDeltaMass();
        impl_->avgDeltaMass_ += modList.averageDeltaMass();
    }
    return insertResult;
}

PWIZ_API_DECL
ModificationMap::iterator ModificationMap::insert(ModificationMap::iterator position,
                                                  const value_type& x)
{
    // ignore hint because that base function won't tell us if the insert happened or not
    return insert(x).first;
}

PWIZ_API_DECL
void ModificationMap::swap(ModificationMap& other)
{
    throw runtime_error("[ModificationMap::swap()] should not be called");
}

PWIZ_API_DECL
bool ModificationMap::operator==(const ModificationMap& rhs) const
{
	if (size() != rhs.size())
        return false;

	ModificationMap::const_iterator itr, rhsItr;
	for (itr = begin(), rhsItr = rhs.begin();
         itr != end() && rhsItr != rhs.end();
         ++itr, ++rhsItr)
    {
		// compare positions and modification lists
		if (itr->first != rhsItr->first || !(itr->second == rhsItr->second))
			return false;
	}
    return true;
}

PWIZ_API_DECL
bool ModificationMap::operator<(const ModificationMap& rhs) const
{
	if (size() < rhs.size())
	{
		ModificationMap::const_iterator itr, rhsItr;
		for (itr = begin(), rhsItr = rhs.begin();
			 itr != end() && rhsItr != rhs.end();
			 ++itr, ++rhsItr)
		{
			// compare positions
			if (itr->first == rhsItr->first)
			{
				// compare modification lists
				return itr->second < rhsItr->second;
			}
            else
				return itr->first < rhsItr->first;
		}
        return false;
	} 
	
	return size() < rhs.size();
}


} // namespace proteome
} // namespace pwiz
